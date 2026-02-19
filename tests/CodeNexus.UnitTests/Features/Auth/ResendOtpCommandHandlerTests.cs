using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Auth.Commands.ResendOtp;
using FluentAssertions;
using Moq;

namespace CodeNexus.UnitTests.Features.Auth;

public class ResendOtpCommandHandlerTests
{
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<IOTPCacheService> _otpCacheServiceMock;
    private readonly ResendOtpCommandHandler _handler;

    public ResendOtpCommandHandlerTests()
    {
        _emailServiceMock = new Mock<IEmailService>();
        _otpCacheServiceMock = new Mock<IOTPCacheService>();
        _handler = new ResendOtpCommandHandler(_emailServiceMock.Object, _otpCacheServiceMock.Object);
    }

    [Fact]
    public async Task Handle_WhenNoRegistrationData_ReturnsFailure()
    {
        // Arrange
        var command = new ResendOtpCommand("notfound@test.com");
        _otpCacheServiceMock.Setup(x => x.ResendOtpAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("INVALID_EMAIL", "No pending verification found for this email"));

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
        _otpCacheServiceMock.Setup(x => x.ResendOtpAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("OTP_RATE_LIMITED", "Please wait 1 minute before requesting a new OTP"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("OTP_RATE_LIMITED");
    }

    [Fact]
    public async Task Handle_WhenValidRequest_GeneratesNewOtpAndSendsEmail()
    {
        // Arrange
        var command = new ResendOtpCommand("test@test.com");
        
        _otpCacheServiceMock.Setup(x => x.ResendOtpAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _otpCacheServiceMock.Setup(x => x.GetLastGeneratedOtp()).Returns("654321");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _emailServiceMock.Verify(x => x.SendOtpEmailAsync("test@test.com", "654321", It.IsAny<CancellationToken>()), Times.Once);
    }
}
