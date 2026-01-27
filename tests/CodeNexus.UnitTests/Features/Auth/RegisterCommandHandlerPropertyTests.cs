using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Auth.Commands.Register;
using CodeNexus.Domain.Entities;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Moq;

namespace CodeNexus.UnitTests.Features.Auth;

public class RegisterCommandHandlerPropertyTests
{
    private const int ExpectedOtpExpirationMinutes = 5;

    [Property(MaxTest = 100)]
    public Property OtpVerification_ExpiresAt_ShouldBeExactly5MinutesAfterCreatedAt()
    {
        return Prop.ForAll(
            ValidEmailArb(),
            ValidUsernameArb(),
            ValidPasswordArb(),
            (email, username, password) =>
            {
                // Arrange
                OtpVerification? capturedOtpVerification = null;

                var mockDbContext = CreateMockDbContext(
                    existingUsers: new List<User>(),
                    existingOtpVerifications: new List<OtpVerification>(),
                    onOtpAdded: otp => capturedOtpVerification = otp);

                var mockEmailService = new Mock<IEmailService>();
                mockEmailService
                    .Setup(x => x.SendOtpEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

                var handler = new RegisterCommandHandler(mockDbContext.Object, mockEmailService.Object);
                var command = new RegisterCommand(email, username, password);

                // Act
                var result = handler.Handle(command, CancellationToken.None).GetAwaiter().GetResult();

                // Assert
                if (capturedOtpVerification == null)
                {
                    return false.Label("OtpVerification was not created");
                }

                var expectedExpiration = capturedOtpVerification.CreatedAt.AddMinutes(ExpectedOtpExpirationMinutes);
                var actualExpiration = capturedOtpVerification.ExpiresAt;

                // Allow for small time differences due to test execution
                var timeDifference = Math.Abs((expectedExpiration - actualExpiration).TotalSeconds);
                var isWithinTolerance = timeDifference < 1;

                return (result.IsSuccess && isWithinTolerance)
                    .Label($"ExpiresAt should be exactly 5 minutes after CreatedAt. " +
                           $"CreatedAt: {capturedOtpVerification.CreatedAt}, " +
                           $"ExpiresAt: {actualExpiration}, " +
                           $"Expected: {expectedExpiration}");
            });
    }

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

    private static Arbitrary<string> ValidPasswordArb()
    {
        // Generate passwords that meet complexity requirements:
        // 8+ chars, at least 1 uppercase, 1 lowercase, 1 number
        return Arb.From(
            from upper in Gen.Elements("A", "B", "C", "D", "E")
            from lower in Gen.Elements("abcde", "fghij", "klmno")
            from number in Gen.Choose(100, 999)
            select $"{upper}{lower}{number}!");
    }

    private static Mock<IApplicationDbContext> CreateMockDbContext(
        List<User> existingUsers,
        List<OtpVerification> existingOtpVerifications,
        Action<OtpVerification>? onOtpAdded = null)
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
        mockOtpDbSet.Setup(d => d.Add(It.IsAny<OtpVerification>()))
            .Callback<OtpVerification>(otp => onOtpAdded?.Invoke(otp));
        mockContext.Setup(c => c.OtpVerification).Returns(mockOtpDbSet.Object);

        mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        return mockContext;
    }
}

// Helper classes for async query support in mocking
internal class TestAsyncQueryProvider<TEntity> : IQueryProvider, IAsyncQueryProvider
{
    private readonly IQueryProvider _inner;

    internal TestAsyncQueryProvider(IQueryProvider inner)
    {
        _inner = inner;
    }

    public IQueryable CreateQuery(System.Linq.Expressions.Expression expression)
    {
        return new TestAsyncEnumerable<TEntity>(expression);
    }

    public IQueryable<TElement> CreateQuery<TElement>(System.Linq.Expressions.Expression expression)
    {
        return new TestAsyncEnumerable<TElement>(expression);
    }

    public object? Execute(System.Linq.Expressions.Expression expression)
    {
        return _inner.Execute(expression);
    }

    public TResult Execute<TResult>(System.Linq.Expressions.Expression expression)
    {
        return _inner.Execute<TResult>(expression);
    }

    public TResult ExecuteAsync<TResult>(System.Linq.Expressions.Expression expression, CancellationToken cancellationToken = default)
    {
        var resultType = typeof(TResult).GetGenericArguments()[0];
        var executionResult = typeof(IQueryProvider)
            .GetMethod(
                name: nameof(IQueryProvider.Execute),
                genericParameterCount: 1,
                types: new[] { typeof(System.Linq.Expressions.Expression) })!
            .MakeGenericMethod(resultType)
            .Invoke(this, new[] { expression });

        return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))!
            .MakeGenericMethod(resultType)
            .Invoke(null, new[] { executionResult })!;
    }
}

internal class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
{
    public TestAsyncEnumerable(IEnumerable<T> enumerable) : base(enumerable) { }
    public TestAsyncEnumerable(System.Linq.Expressions.Expression expression) : base(expression) { }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());
    }

    IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);
}

internal class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
{
    private readonly IEnumerator<T> _inner;

    public TestAsyncEnumerator(IEnumerator<T> inner)
    {
        _inner = inner;
    }

    public T Current => _inner.Current;

    public ValueTask<bool> MoveNextAsync()
    {
        return new ValueTask<bool>(_inner.MoveNext());
    }

    public ValueTask DisposeAsync()
    {
        _inner.Dispose();
        return new ValueTask();
    }
}
