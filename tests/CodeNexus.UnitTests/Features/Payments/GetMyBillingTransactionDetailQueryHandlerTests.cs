using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Payments.Queries.GetMyBillingTransactionDetail;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.Payments;

public class GetMyBillingTransactionDetailQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUser;
    private readonly GetMyBillingTransactionDetailQueryHandler _handler;

    public GetMyBillingTransactionDetailQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUser = new Mock<ICurrentUserService>();
        _handler = new GetMyBillingTransactionDetailQueryHandler(_mockContext.Object, _mockCurrentUser.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenTransactionDoesNotBelongToCurrentUser()
    {
        var currentUserId = Guid.NewGuid();
        var anotherUserId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        _mockCurrentUser.Setup(x => x.GetUserId()).Returns(currentUserId);

        var transactions = new List<PaymentTransaction>
        {
            new()
            {
                PaymentTransactionId = transactionId,
                UserId = anotherUserId,
                Amount = 50000,
                CreditedTokens = 50000,
                Provider = "VNPAY",
                TxnRef = "TXN-999",
                OrderInfo = "Top-up",
                Status = PaymentStatus.Success,
                CreatedAt = DateTime.UtcNow
            }
        };

        _mockContext.Setup(x => x.PaymentTransactions).Returns(transactions.BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetMyBillingTransactionDetailQuery(transactionId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("PAYMENT_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task Handle_ShouldReturnTransaction_WhenBelongsToCurrentUser()
    {
        var currentUserId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        _mockCurrentUser.Setup(x => x.GetUserId()).Returns(currentUserId);

        var pkg = new TokenPackage { TokenPackageId = Guid.NewGuid(), Name = "Pro" };
        var transactions = new List<PaymentTransaction>
        {
            new()
            {
                PaymentTransactionId = transactionId,
                UserId = currentUserId,
                TokenPackageId = pkg.TokenPackageId,
                TokenPackage = pkg,
                Amount = 200000,
                CreditedTokens = 220000,
                Provider = "VNPAY",
                TxnRef = "TXN-123",
                OrderInfo = "Buy package Pro",
                Status = PaymentStatus.Success,
                CreatedAt = DateTime.UtcNow
            }
        };

        _mockContext.Setup(x => x.PaymentTransactions).Returns(transactions.BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetMyBillingTransactionDetailQuery(transactionId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("TXN-123", result.Value!.TxnRef);
        Assert.Equal("Pro", result.Value.TokenPackageName);
    }
}

