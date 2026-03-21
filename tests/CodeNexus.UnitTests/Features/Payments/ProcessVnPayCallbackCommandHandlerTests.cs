using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Payments.Commands.ProcessVnPayCallback;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.Payments;

public class ProcessVnPayCallbackCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<IVnPayService> _mockVnPayService;
    private readonly ProcessVnPayCallbackCommandHandler _handler;

    public ProcessVnPayCallbackCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockVnPayService = new Mock<IVnPayService>();
        _handler = new ProcessVnPayCallbackCommandHandler(_mockContext.Object, _mockVnPayService.Object);
    }

    [Fact]
    public async Task Handle_WithInvalidSignature_ShouldFail()
    {
        _mockVnPayService.Setup(x => x.ValidateSignature(It.IsAny<IDictionary<string, string>>()))
            .Returns(false);

        var result = await _handler.Handle(new ProcessVnPayCallbackCommand(new Dictionary<string, string>
        {
            ["vnp_TxnRef"] = "abc"
        }), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_SIGNATURE", result.ErrorCode);
    }

    [Fact]
    public async Task Handle_WithSuccessResponse_ShouldActivateSubscription()
    {
        var planId = Guid.NewGuid();
        var user = new User { UserId = Guid.NewGuid() };
        var payment = new PaymentTransaction
        {
            PaymentTransactionId = Guid.NewGuid(),
            UserId = user.UserId,
            User = user,
            Amount = 100000m,
            SubscriptionPlanId = planId,
            TxnRef = "txn123",
            Status = PaymentStatus.Pending
        };

        _mockVnPayService.Setup(x => x.ValidateSignature(It.IsAny<IDictionary<string, string>>()))
            .Returns(true);

        _mockContext.Setup(x => x.SubscriptionPlans).Returns(new[]
        {
            new SubscriptionPlan
            {
                SubscriptionPlanId = planId,
                Name = "Standard",
                DurationDays = 30,
                IsActive = true
            }
        }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.PaymentTransactions).Returns(new[] { payment }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Users).Returns(new[] { user }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var parameters = new Dictionary<string, string>
        {
            ["vnp_TxnRef"] = "txn123",
            ["vnp_ResponseCode"] = "00",
            ["vnp_TransactionNo"] = "10001",
            ["vnp_BankCode"] = "NCB",
            ["vnp_SecureHash"] = "hash"
        };

        var result = await _handler.Handle(new ProcessVnPayCallbackCommand(parameters), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentStatus.Success, result.Value!.Status);
        Assert.Equal(planId, user.SubscriptionPlanId);
        Assert.True(user.PlanExpiresAt > DateTime.UtcNow);
    }
}
