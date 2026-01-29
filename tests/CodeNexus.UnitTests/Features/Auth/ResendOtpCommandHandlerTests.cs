using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Auth.Commands.ResendOtp;
using CodeNexus.Domain.Entities;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CodeNexus.UnitTests.Features.Auth;

public class ResendOtpCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<IOTPService> _otpServiceMock;
    private readonly ResendOtpCommandHandler _handler;

    public ResendOtpCommandHandlerTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _emailServiceMock = new Mock<IEmailService>();
        _otpServiceMock = new Mock<IOTPService>();
        _handler = new ResendOtpCommandHandler(_contextMock.Object, _emailServiceMock.Object, _otpServiceMock.Object);
    }

    [Fact]
    public async Task Handle_WhenNoOtpFound_ReturnsFailure()
    {
        // Arrange
        var command = new ResendOtpCommand("notfound@test.com");
        SetupOtpDbSet(new List<OtpVerification>());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_EMAIL");
    }

    [Fact]
    public async Task Handle_WhenRateLimited_ReturnsFailure()
    {
        // Arrange
        var command = new ResendOtpCommand("test@test.com");
        var now = DateTime.Now;
        var otp = new OtpVerification
        {
            Email = "test@test.com",
            LastResendAt = now // Just now - should be rate limited
        };
        SetupOtpDbSet(new List<OtpVerification> { otp });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("OTP_RATE_LIMITED");
    }

    [Fact]
    public async Task Handle_WhenMaxResendExceeded_ReturnsFailure()
    {
        // Arrange
        var command = new ResendOtpCommand("test@test.com");
        var otp = new OtpVerification
        {
            Email = "test@test.com",
            LastResendAt = DateTime.Now.AddMinutes(-5),
            ResendCount = 5
        };
        SetupOtpDbSet(new List<OtpVerification> { otp });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("RESEND_RATE_LIMITED");
    }


    [Fact]
    public async Task Handle_WhenValidRequest_UpdatesOtpAndSendsEmail()
    {
        // Arrange
        var command = new ResendOtpCommand("test@test.com");
        var otp = new OtpVerification
        {
            Email = "test@test.com",
            LastResendAt = DateTime.Now.AddMinutes(-5),
            ResendCount = 0,
            AttemptCount = 3
        };
        SetupOtpDbSet(new List<OtpVerification> { otp });
        _otpServiceMock.Setup(x => x.GenerateOtp(It.IsAny<int>())).Returns("654321");
        _otpServiceMock.Setup(x => x.HashOtp("654321")).Returns("newHashedOtp");
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        otp.OtpHash.Should().Be("newHashedOtp");
        otp.AttemptCount.Should().Be(0);
        otp.ResendCount.Should().Be(1);
        _emailServiceMock.Verify(x => x.SendOtpEmailAsync("test@test.com", "654321", It.IsAny<CancellationToken>()), Times.Once);
    }

    private void SetupOtpDbSet(List<OtpVerification> otpList)
    {
        var queryable = new TestAsyncEnumerable<OtpVerification>(otpList);
        var dbSetMock = new Mock<DbSet<OtpVerification>>();
        dbSetMock.As<IQueryable<OtpVerification>>().Setup(m => m.Provider).Returns(queryable.AsQueryable().Provider);
        dbSetMock.As<IQueryable<OtpVerification>>().Setup(m => m.Expression).Returns(queryable.AsQueryable().Expression);
        dbSetMock.As<IQueryable<OtpVerification>>().Setup(m => m.ElementType).Returns(queryable.AsQueryable().ElementType);
        dbSetMock.As<IQueryable<OtpVerification>>().Setup(m => m.GetEnumerator()).Returns(queryable.AsQueryable().GetEnumerator());
        dbSetMock.As<IAsyncEnumerable<OtpVerification>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(queryable.GetAsyncEnumerator());
        _contextMock.Setup(x => x.OtpVerification).Returns(dbSetMock.Object);
    }
}
