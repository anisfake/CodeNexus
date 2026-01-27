using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Utilities;
using CodeNexus.Application.Features.Auth.Commands.ResendOtp;
using CodeNexus.Domain.Entities;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CodeNexus.UnitTests.Features.Auth;

public class ResendOtpCommandHandlerPropertyTests
{

    [Property(MaxTest = 100)]
    public Property ResendOtp_ShouldInvalidatePreviousOtp()
    {
        return Prop.ForAll(
            ValidEmailArb(),
            ValidOtpArb(),
            (email, originalOtp) =>
            {
                // Arrange
                var originalOtpHash = OtpHasher.Hash(originalOtp);
                var now = DateTime.UtcNow;

                var existingOtpVerification = new OtpVerification
                {
                    Id = Guid.NewGuid(),
                    Email = email,
                    Username = "testuser",
                    PasswordHash = "hashedpassword",
                    OtpHash = originalOtpHash,
                    CreatedAt = now.AddMinutes(-2), // Created 2 minutes ago
                    ExpiresAt = now.AddMinutes(3), // Still valid
                    AttemptCount = 0,
                    ResendCount = 0,
                    LastResendAt = now.AddMinutes(-2) // Last resend was 2 minutes ago (allows resend)
                };

                string? capturedNewOtpHash = null;

                var mockDbContext = CreateMockDbContext(
                    existingOtpVerifications: new List<OtpVerification> { existingOtpVerification },
                    onOtpUpdated: otp => capturedNewOtpHash = otp.OtpHash);

                var mockEmailService = new Mock<IEmailService>();
                mockEmailService
                    .Setup(x => x.SendOtpEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

                var handler = new ResendOtpCommandHandler(mockDbContext.Object, mockEmailService.Object);
                var command = new ResendOtpCommand(email);

                // Act
                var result = handler.Handle(command, CancellationToken.None).GetAwaiter().GetResult();

                // Assert
                if (!result.IsSuccess)
                {
                    return false.Label($"Resend should succeed but failed with: {result.ErrorMessage}");
                }

                if (capturedNewOtpHash == null)
                {
                    return false.Label("New OTP hash was not captured");
                }

                // The original OTP should no longer verify against the new hash
                var originalOtpStillValid = OtpHasher.Verify(originalOtp, capturedNewOtpHash);

                // The new hash should be different from the original hash
                var hashesAreDifferent = capturedNewOtpHash != originalOtpHash;

                return (!originalOtpStillValid && hashesAreDifferent)
                    .Label($"Previous OTP should be invalidated. " +
                           $"Original OTP still valid: {originalOtpStillValid}, " +
                           $"Hashes different: {hashesAreDifferent}");
            });
    }

    private static Arbitrary<string> ValidEmailArb()
    {
        return Arb.From(
            from localPart in Gen.Elements("user", "test", "admin", "john", "jane")
            from domain in Gen.Elements("example.com", "test.org", "mail.net")
            select $"{localPart}@{domain}");
    }

    private static Arbitrary<string> ValidOtpArb()
    {
        return Arb.From(
            from num in Gen.Choose(0, 999999)
            select num.ToString().PadLeft(6, '0'));
    }

    private static Mock<IApplicationDbContext> CreateMockDbContext(
        List<OtpVerification> existingOtpVerifications,
        Action<OtpVerification>? onOtpUpdated = null)
    {
        var mockContext = new Mock<IApplicationDbContext>();

        // Setup OtpVerification DbSet
        var otpQueryable = existingOtpVerifications.AsQueryable();
        var mockOtpDbSet = new Mock<DbSet<OtpVerification>>();
        mockOtpDbSet.As<IQueryable<OtpVerification>>().Setup(m => m.Provider).Returns(new TestAsyncQueryProvider<OtpVerification>(otpQueryable.Provider));
        mockOtpDbSet.As<IQueryable<OtpVerification>>().Setup(m => m.Expression).Returns(otpQueryable.Expression);
        mockOtpDbSet.As<IQueryable<OtpVerification>>().Setup(m => m.ElementType).Returns(otpQueryable.ElementType);
        mockOtpDbSet.As<IQueryable<OtpVerification>>().Setup(m => m.GetEnumerator()).Returns(otpQueryable.GetEnumerator());
        mockOtpDbSet.As<IAsyncEnumerable<OtpVerification>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(new TestAsyncEnumerator<OtpVerification>(otpQueryable.GetEnumerator()));
        mockContext.Setup(c => c.OtpVerification).Returns(mockOtpDbSet.Object);

        // Capture updates when SaveChangesAsync is called
        mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                // The handler modifies the existing entity directly, so we capture it here
                if (existingOtpVerifications.Count > 0)
                {
                    onOtpUpdated?.Invoke(existingOtpVerifications[0]);
                }
            })
            .ReturnsAsync(1);

        return mockContext;
    }
}
