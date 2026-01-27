using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Utilities;
using CodeNexus.Application.Features.Auth.Commands.VerifyOtp;
using CodeNexus.Domain.Entities;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Moq;

namespace CodeNexus.UnitTests.Features.Auth;

public class VerifyOtpCommandHandlerPropertyTests
{

    [Property(MaxTest = 100)]
    public Property ValidOtpVerification_ShouldCreateUser()
    {
        return Prop.ForAll(
            ValidEmailArb(),
            ValidUsernameArb(),
            SixDigitOtpArb(),
            (email, username, otp) =>
            {
                // Arrange
                var otpHash = OtpHasher.Hash(otp);
                var now = DateTime.UtcNow;
                var otpVerification = new OtpVerification
                {
                    Id = Guid.NewGuid(),
                    Email = email,
                    Username = username,
                    PasswordHash = "hashedpassword",
                    OtpHash = otpHash,
                    CreatedAt = now,
                    ExpiresAt = now.AddMinutes(5), // Valid, not expired
                    AttemptCount = 0,
                    ResendCount = 0
                };

                User? capturedUser = null;
                OtpVerification? removedOtp = null;

                var mockDbContext = CreateMockDbContext(
                    existingUsers: new List<User>(),
                    existingOtpVerifications: new List<OtpVerification> { otpVerification },
                    onUserAdded: user => capturedUser = user,
                    onOtpRemoved: otp => removedOtp = otp);

                var handler = new VerifyOtpCommandHandler(mockDbContext.Object);
                var command = new VerifyOtpCommand(email, otp);

                // Act
                var result = handler.Handle(command, CancellationToken.None).GetAwaiter().GetResult();

                // Assert
                var isSuccess = result.IsSuccess;
                var userCreated = capturedUser != null;
                var emailMatches = capturedUser?.Email == email;
                var usernameMatches = capturedUser?.Username == username;

                return (isSuccess && userCreated && emailMatches && usernameMatches)
                    .Label($"Valid OTP verification should create user. " +
                           $"Success: {isSuccess}, UserCreated: {userCreated}, " +
                           $"EmailMatches: {emailMatches}, UsernameMatches: {usernameMatches}");
            });
    }

    [Property(MaxTest = 100)]
    public Property InvalidOtp_ShouldBeRejected()
    {
        return Prop.ForAll(
            ValidEmailArb(),
            ValidUsernameArb(),
            TwoDifferentOtpsArb(),
            (email, username, otpPair) =>
            {
                var (storedOtp, submittedOtp) = otpPair;

                // Arrange
                var otpHash = OtpHasher.Hash(storedOtp);
                var now = DateTime.UtcNow;
                var otpVerification = new OtpVerification
                {
                    Id = Guid.NewGuid(),
                    Email = email,
                    Username = username,
                    PasswordHash = "hashedpassword",
                    OtpHash = otpHash,
                    CreatedAt = now,
                    ExpiresAt = now.AddMinutes(5), // Valid, not expired
                    AttemptCount = 0,
                    ResendCount = 0
                };

                User? capturedUser = null;

                var mockDbContext = CreateMockDbContext(
                    existingUsers: new List<User>(),
                    existingOtpVerifications: new List<OtpVerification> { otpVerification },
                    onUserAdded: user => capturedUser = user);

                var handler = new VerifyOtpCommandHandler(mockDbContext.Object);
                var command = new VerifyOtpCommand(email, submittedOtp);

                // Act
                var result = handler.Handle(command, CancellationToken.None).GetAwaiter().GetResult();

                // Assert
                var isFailure = result.IsFailure;
                var userNotCreated = capturedUser == null;
                var hasInvalidOtpError = result.ErrorCode == "INVALID_OTP";

                return (isFailure && userNotCreated && hasInvalidOtpError)
                    .Label($"Invalid OTP should be rejected. " +
                           $"IsFailure: {isFailure}, UserNotCreated: {userNotCreated}, " +
                           $"ErrorCode: {result.ErrorCode}");
            });
    }

    [Property(MaxTest = 100)]
    public Property SuccessfulVerification_ShouldRemoveOtpRecord()
    {
        return Prop.ForAll(
            ValidEmailArb(),
            ValidUsernameArb(),
            SixDigitOtpArb(),
            (email, username, otp) =>
            {
                // Arrange
                var otpHash = OtpHasher.Hash(otp);
                var now = DateTime.UtcNow;
                var otpVerification = new OtpVerification
                {
                    Id = Guid.NewGuid(),
                    Email = email,
                    Username = username,
                    PasswordHash = "hashedpassword",
                    OtpHash = otpHash,
                    CreatedAt = now,
                    ExpiresAt = now.AddMinutes(5), // Valid, not expired
                    AttemptCount = 0,
                    ResendCount = 0
                };

                OtpVerification? removedOtp = null;

                var mockDbContext = CreateMockDbContext(
                    existingUsers: new List<User>(),
                    existingOtpVerifications: new List<OtpVerification> { otpVerification },
                    onOtpRemoved: otp => removedOtp = otp);

                var handler = new VerifyOtpCommandHandler(mockDbContext.Object);
                var command = new VerifyOtpCommand(email, otp);

                // Act
                var result = handler.Handle(command, CancellationToken.None).GetAwaiter().GetResult();

                // Assert
                var isSuccess = result.IsSuccess;
                var otpRemoved = removedOtp != null;
                var correctOtpRemoved = removedOtp?.Email == email;

                return (isSuccess && otpRemoved && correctOtpRemoved)
                    .Label($"Successful verification should remove OTP record. " +
                           $"Success: {isSuccess}, OtpRemoved: {otpRemoved}, " +
                           $"CorrectOtpRemoved: {correctOtpRemoved}");
            });
    }

    #region Arbitrary Generators

    private static Arbitrary<string> ValidEmailArb()
    {
        return Arb.From(
            from localPart in Gen.Elements("user", "test", "admin", "john", "jane")
            from domain in Gen.Elements("example.com", "test.org", "mail.net")
            select $"{localPart}@{domain}");
    }

    private static Arbitrary<string> ValidUsernameArb()
    {
        return Arb.From(
            from prefix in Gen.Elements("user", "test", "admin", "john", "jane")
            from suffix in Gen.Choose(1, 9999)
            select $"{prefix}{suffix}");
    }

    private static Arbitrary<string> SixDigitOtpArb()
    {
        return Arb.From(
            Gen.Choose(0, 999999)
                .Select(n => n.ToString().PadLeft(6, '0')));
    }
    private static Arbitrary<(string StoredOtp, string SubmittedOtp)> TwoDifferentOtpsArb()
    {
        return Arb.From(
            from storedNum in Gen.Choose(0, 499999)
            from submittedNum in Gen.Choose(500000, 999999)
            let storedOtp = storedNum.ToString().PadLeft(6, '0')
            let submittedOtp = submittedNum.ToString().PadLeft(6, '0')
            select (storedOtp, submittedOtp));
    }

    #endregion

    #region Mock Setup Helpers

    private static Mock<IApplicationDbContext> CreateMockDbContext(
        List<User> existingUsers,
        List<OtpVerification> existingOtpVerifications,
        Action<User>? onUserAdded = null,
        Action<OtpVerification>? onOtpRemoved = null)
    {
        var mockContext = new Mock<IApplicationDbContext>();

        // Setup Users DbSet
        var usersQueryable = existingUsers.AsQueryable();
        var mockUsersDbSet = new Mock<DbSet<User>>();
        mockUsersDbSet.As<IQueryable<User>>().Setup(m => m.Provider).Returns(new TestAsyncQueryProvider<User>(usersQueryable.Provider));
        mockUsersDbSet.As<IQueryable<User>>().Setup(m => m.Expression).Returns(usersQueryable.Expression);
        mockUsersDbSet.As<IQueryable<User>>().Setup(m => m.ElementType).Returns(usersQueryable.ElementType);
        mockUsersDbSet.As<IQueryable<User>>().Setup(m => m.GetEnumerator()).Returns(usersQueryable.GetEnumerator());
        mockUsersDbSet.As<IAsyncEnumerable<User>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(new TestAsyncEnumerator<User>(usersQueryable.GetEnumerator()));
        mockUsersDbSet.Setup(d => d.Add(It.IsAny<User>()))
            .Callback<User>(user => onUserAdded?.Invoke(user));
        mockContext.Setup(c => c.Users).Returns(mockUsersDbSet.Object);

        // Setup OtpVerification DbSet
        var otpQueryable = existingOtpVerifications.AsQueryable();
        var mockOtpDbSet = new Mock<DbSet<OtpVerification>>();
        mockOtpDbSet.As<IQueryable<OtpVerification>>().Setup(m => m.Provider).Returns(new TestAsyncQueryProvider<OtpVerification>(otpQueryable.Provider));
        mockOtpDbSet.As<IQueryable<OtpVerification>>().Setup(m => m.Expression).Returns(otpQueryable.Expression);
        mockOtpDbSet.As<IQueryable<OtpVerification>>().Setup(m => m.ElementType).Returns(otpQueryable.ElementType);
        mockOtpDbSet.As<IQueryable<OtpVerification>>().Setup(m => m.GetEnumerator()).Returns(otpQueryable.GetEnumerator());
        mockOtpDbSet.As<IAsyncEnumerable<OtpVerification>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(new TestAsyncEnumerator<OtpVerification>(otpQueryable.GetEnumerator()));
        mockOtpDbSet.Setup(d => d.Remove(It.IsAny<OtpVerification>()))
            .Callback<OtpVerification>(otp => onOtpRemoved?.Invoke(otp));
        mockContext.Setup(c => c.OtpVerification).Returns(mockOtpDbSet.Object);

        mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        return mockContext;
    }

    #endregion
}
