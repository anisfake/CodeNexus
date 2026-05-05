using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Users.DTOs;
using CodeNexus.Domain.Entities;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CodeNexus.Application.Features.Users.Commands.CreateMentorAccount;

public class CreateMentorAccountCommandHandler
    : IRequestHandler<CreateMentorAccountCommand, Result<CreateMentorAccountResponse>>
{
    private const int MaxUsernameLength = 50;

    private readonly IApplicationDbContext _context;
    private readonly IPasswordService _passwordService;
    private readonly IEmailService _emailService;
    private readonly IAchievementService _achievementService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<CreateMentorAccountCommandHandler> _logger;

    public CreateMentorAccountCommandHandler(
        IApplicationDbContext context,
        IPasswordService passwordService,
        IEmailService emailService,
        IAchievementService achievementService,
        ICurrentUserService currentUserService,
        ILogger<CreateMentorAccountCommandHandler> logger)
    {
        _context = context;
        _passwordService = passwordService;
        _emailService = emailService;
        _achievementService = achievementService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result<CreateMentorAccountResponse>> Handle(
        CreateMentorAccountCommand request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        var providedUsername = string.IsNullOrWhiteSpace(request.Username) ? null : request.Username.Trim();

        var emailExists = await _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.Email == email, cancellationToken);

        if (emailExists)
            return Result<CreateMentorAccountResponse>.Failure("EMAIL_EXISTS", "Email already registered.");

        if (!string.IsNullOrWhiteSpace(providedUsername))
        {
            var usernameExists = await _context.Users
                .AsNoTracking()
                .AnyAsync(u => u.Username == providedUsername, cancellationToken);

            if (usernameExists)
                return Result<CreateMentorAccountResponse>.Failure("USERNAME_EXISTS", "Username already taken.");
        }

        var role = await _context.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RoleName == request.Role, cancellationToken);

        if (role == null)
            return Result<CreateMentorAccountResponse>.Failure("ROLE_NOT_FOUND", $"{request.Role} role not found.");

        var username = providedUsername ?? await EnsureUniqueUsernameAsync(BuildUsernameBase(email, request.Role), cancellationToken);
        var temporaryPassword = GenerateTemporaryPassword();

        var user = new User
        {
            UserId = NewId.NextGuid(),
            Email = email,
            Username = username,
            PasswordHash = _passwordService.HashPassword(temporaryPassword),
            FirstName = Normalize(request.FirstName),
            LastName = Normalize(request.LastName),
            CreatedAt = DateTime.UtcNow,
            Status = "Active",
            RoleId = role.RoleId
        };

        var profile = new UserProfile
        {
            ProfileId = NewId.NextGuid(),
            UserId = user.UserId,
            Bio = Normalize(request.Bio),
            Phone = Normalize(request.Phone),
            Address = Normalize(request.Address),
            DateOfBirth = request.DateOfBirth?.Date,
            UpdatedAt = DateTime.UtcNow
        };

        await _context.Users.AddAsync(user, cancellationToken);
        await _context.UserProfiles.AddAsync(profile, cancellationToken);

        TrySetAuditUser();
        await _context.SaveChangesAsync(cancellationToken);
        await _achievementService.InitializeUserAchievementsAsync(user.UserId);

        var setupEmailSent = false;
        string? setupEmailError = null;

        if (request.SendSetupEmail)
        {
            try
            {
                await _emailService.SendNotificationEmailAsync(
                    email,
                    $"{request.Role} account created",
                    BuildSetupMessage(user, temporaryPassword, request.Role),
                    cancellationToken);

                setupEmailSent = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send setup email to {Email}", email);
                setupEmailError = "Failed to send setup email. Please share the temporary password manually.";
            }
        }

        return Result<CreateMentorAccountResponse>.Success(new CreateMentorAccountResponse(
            user.UserId,
            user.Email,
            user.Username,
            user.FirstName,
            user.LastName,
            request.Role,
            temporaryPassword,
            setupEmailSent,
            setupEmailError,
            user.CreatedAt));
    }

    private async Task<string> EnsureUniqueUsernameAsync(string baseUsername, CancellationToken cancellationToken)
    {
        var candidate = baseUsername;
        var suffix = 0;

        while (await _context.Users.AsNoTracking().AnyAsync(u => u.Username == candidate, cancellationToken))
        {
            suffix++;
            var suffixText = suffix.ToString(CultureInfo.InvariantCulture);
            var maxBaseLength = Math.Max(1, MaxUsernameLength - suffixText.Length);
            var truncatedBase = baseUsername.Length > maxBaseLength ? baseUsername[..maxBaseLength] : baseUsername;
            candidate = $"{truncatedBase}{suffixText}";
        }

        return candidate;
    }

    private static string BuildUsernameBase(string email, string role)
    {
        var localPart = email.Split('@')[0].Trim();
        var sanitized = Regex.Replace(localPart, @"[^a-zA-Z0-9._]", string.Empty);

        if (string.IsNullOrWhiteSpace(sanitized))
            sanitized = role.ToLowerInvariant();

        if (sanitized.Length < 3)
            sanitized = sanitized.PadRight(3, '0');

        return sanitized.Length > MaxUsernameLength ? sanitized[..MaxUsernameLength] : sanitized;
    }

    private static string GenerateTemporaryPassword(int length = 14)
    {
        if (length < 12) length = 12;

        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "@#$%&*!";
        var all = upper + lower + digits + symbols;

        var chars = new char[length];
        var randomBytes = new byte[length];
        RandomNumberGenerator.Fill(randomBytes);

        chars[0] = upper[randomBytes[0] % upper.Length];
        chars[1] = lower[randomBytes[1] % lower.Length];
        chars[2] = digits[randomBytes[2] % digits.Length];
        chars[3] = symbols[randomBytes[3] % symbols.Length];

        for (var i = 4; i < length; i++)
            chars[i] = all[randomBytes[i] % all.Length];

        Shuffle(chars);
        return new string(chars);
    }

    private static void Shuffle(Span<char> chars)
    {
        var random = new byte[sizeof(uint)];
        for (var i = chars.Length - 1; i > 0; i--)
        {
            RandomNumberGenerator.Fill(random);
            var value = BitConverter.ToUInt32(random, 0);
            var j = (int)(value % (uint)(i + 1));
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string BuildSetupMessage(User user, string temporaryPassword, string role) =>
        $"Your {role.ToLowerInvariant()} account has been created by the administrator.\n\n" +
        $"Email: {user.Email}\n" +
        $"Username: {user.Username}\n" +
        $"Temporary Password: {temporaryPassword}\n\n" +
        "Please sign in and change your password immediately.";

    private void TrySetAuditUser()
    {
        try
        {
            var currentUserId = _currentUserService.GetUserId();
            _context.SetAuditUserId(currentUserId);
        }
        catch
        {
            // Best-effort only.
        }
    }
}
