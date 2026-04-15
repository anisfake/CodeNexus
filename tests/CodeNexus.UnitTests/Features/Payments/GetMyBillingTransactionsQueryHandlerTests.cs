using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Payments.Queries.GetMyBillingTransactions;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.Payments;

public class GetMyBillingTransactionsQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUser;
    private readonly GetMyBillingTransactionsQueryHandler _handler;

    public GetMyBillingTransactionsQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUser = new Mock<ICurrentUserService>();
        _handler = new GetMyBillingTransactionsQueryHandler(_mockContext.Object, _mockCurrentUser.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnOnlyCurrentUserTransactions()
    {
        var currentUserId = Guid.NewGuid();
        var anotherUserId = Guid.NewGuid();
        _mockCurrentUser.Setup(x => x.GetUserId()).Returns(currentUserId);

        var pkg = new TokenPackage { TokenPackageId = Guid.NewGuid(), Name = "Standard" };
        var transactions = new List<PaymentTransaction>
        {
            new()
            {
                PaymentTransactionId = Guid.NewGuid(),
                UserId = currentUserId,
                TokenPackageId = pkg.TokenPackageId,
                TokenPackage = pkg,
                Amount = 50000,
                CreditedAmountVnd = 52000,
                Provider = "VNPAY",
                TxnRef = "TXN-1",
                OrderInfo = "Buy package Standard",
                Status = PaymentStatus.Success,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                PaymentTransactionId = Guid.NewGuid(),
                UserId = anotherUserId,
                Amount = 100000,
                CreditedAmountVnd = 100000,
                Provider = "VNPAY",
                TxnRef = "TXN-2",
                OrderInfo = "Top-up",
                Status = PaymentStatus.Success,
                CreatedAt = DateTime.UtcNow
            }
        };

        _mockContext.Setup(x => x.PaymentTransactions).Returns(transactions.BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetMyBillingTransactionsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Single(result.Value!.Items);
        Assert.Equal("TXN-1", result.Value.Items[0].TxnRef);
        Assert.Equal("Standard", result.Value.Items[0].TokenPackageName);
    }
}
