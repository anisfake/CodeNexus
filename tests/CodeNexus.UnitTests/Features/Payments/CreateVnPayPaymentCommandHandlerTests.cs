using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Payments.Commands.CreateVnPayPayment;
using CodeNexus.Application.Features.Payments.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.UnitTests.Helpers;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.Payments;

public class CreateVnPayPaymentCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly Mock<IVnPayService> _mockVnPayService;
    private readonly CreateVnPayPaymentCommandHandler _handler;

    public CreateVnPayPaymentCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockVnPayService = new Mock<IVnPayService>();

        _handler = new CreateVnPayPaymentCommandHandler(
            _mockContext.Object,
            _mockCurrentUserService.Object,
            _mockVnPayService.Object);
    }

    [Fact]
    public async Task Handle_WithValidRequest_ShouldReturnPaymentUrl()
    {
        var userId = Guid.NewGuid();
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Users).Returns(new[]
        {
            new User { UserId = userId }
        }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.PaymentTransactions).Returns(new List<PaymentTransaction>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _mockVnPayService
            .Setup(x => x.CreatePaymentUrl(
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>()))
            .Returns("https://vnpay.test/pay");

        var command = new CreateVnPayPaymentCommand(
            null,
            100000m,
            "Buy Standard",
            "https://localhost:5001/api/payments/vnpay/return",
            "127.0.0.1");

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("https://vnpay.test/pay", result.Value!.PaymentUrl);
        Assert.NotEqual(Guid.Empty, result.Value.PaymentTransactionId);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.TxnRef));
    }

    [Fact]
    public async Task Handle_WithTopUpAmount_ShouldUseRequestedPrice()
    {
        var userId = Guid.NewGuid();
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Users).Returns(new[]
        {
            new User { UserId = userId }
        }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.PaymentTransactions).Returns(new List<PaymentTransaction>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _mockVnPayService
            .Setup(x => x.CreatePaymentUrl(
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>()))
            .Returns("https://vnpay.test/pay");

        var command = new CreateVnPayPaymentCommand(
            null,
            99000m,
            null,
            "https://localhost:5001/api/payments/vnpay/return",
            "127.0.0.1");

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        _mockVnPayService.Verify(x => x.CreatePaymentUrl(
            It.IsAny<string>(),
            99000m,
            It.IsAny<string>(),
            "127.0.0.1",
            "https://localhost:5001/api/payments/vnpay/return",
            It.IsAny<string?>()), Times.Once);
    }
}
