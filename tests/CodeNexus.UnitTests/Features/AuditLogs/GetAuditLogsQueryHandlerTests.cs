using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.AuditLogs.Queries.GetAuditLogs;
using CodeNexus.Domain.Entities;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.AuditLogs;

public class GetAuditLogsQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly GetAuditLogsQueryHandler _handler;

    public GetAuditLogsQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _handler = new GetAuditLogsQueryHandler(_mockContext.Object);
    }

    private static User CreateUser(string username = "john_doe")
    {
        return new User
        {
            UserId = NewId.NextGuid(),
            Username = username,
            Email = $"{username}@test.com",
            PasswordHash = "hash",
            FirstName = "John",
            LastName = "Doe"
        };
    }

    private static List<AuditLog> CreateSampleAuditLogs(User? user = null)
    {
        var testUser = user ?? CreateUser();
        return new List<AuditLog>
        {
            new()
            {
                LogId = NewId.NextGuid(),
                UserId = testUser.UserId,
                User = testUser,
                Action = "Added",
                TableName = "Users",
                RecordId = NewId.NextGuid(),
                OldValue = null,
                NewValue = "{\"Username\":\"new_user\"}",
                Timestamp = DateTime.UtcNow.AddHours(-2),
                IPAddress = "192.168.1.1"
            },
            new()
            {
                LogId = NewId.NextGuid(),
                UserId = testUser.UserId,
                User = testUser,
                Action = "Modified",
                TableName = "Users",
                RecordId = NewId.NextGuid(),
                OldValue = "{\"Status\":\"Active\"}",
                NewValue = "{\"Status\":\"Banned\"}",
                Timestamp = DateTime.UtcNow.AddHours(-1),
                IPAddress = "192.168.1.2"
            },
            new()
            {
                LogId = NewId.NextGuid(),
                UserId = null,
                User = null,
                Action = "Added",
                TableName = "Goals",
                RecordId = NewId.NextGuid(),
                OldValue = null,
                NewValue = "{\"Title\":\"Learn C#\"}",
                Timestamp = DateTime.UtcNow,
                IPAddress = "10.0.0.1"
            }
        };
    }

    [Fact]
    public async Task Handle_DefaultQuery_ReturnsAllLogsSortedByTimestampDescending()
    {
        // Arrange
        var logs = CreateSampleAuditLogs();
        _mockContext.Setup(x => x.AuditLogs).Returns(logs.BuildMockDbSet().Object);

        var query = new GetAuditLogsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Items.Should().HaveCount(3);
        result.Value.TotalCount.Should().Be(3);
        result.Value.PageNumber.Should().Be(1);
        result.Value.PageSize.Should().Be(10);
        result.Value.Items[0].Timestamp.Should().BeOnOrAfter(result.Value.Items[1].Timestamp);
    }

    [Fact]
    public async Task Handle_EmptyLogs_ReturnsEmptyPaginatedResult()
    {
        // Arrange
        _mockContext.Setup(x => x.AuditLogs).Returns(new List<AuditLog>().BuildMockDbSet().Object);

        var query = new GetAuditLogsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_FilterByAction_ReturnsMatchingLogs()
    {
        // Arrange
        var logs = CreateSampleAuditLogs();
        _mockContext.Setup(x => x.AuditLogs).Returns(logs.BuildMockDbSet().Object);

        var query = new GetAuditLogsQuery { Action = "Added" };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.Items.Should().AllSatisfy(item => item.Action.Should().Be("Added"));
        result.Value.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_FilterByTableName_ReturnsMatchingLogs()
    {
        // Arrange
        var logs = CreateSampleAuditLogs();
        _mockContext.Setup(x => x.AuditLogs).Returns(logs.BuildMockDbSet().Object);

        var query = new GetAuditLogsQuery { TableName = "Goals" };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.Items[0].TableName.Should().Be("Goals");
    }

    [Fact]
    public async Task Handle_FilterByUserId_ReturnsMatchingLogs()
    {
        // Arrange
        var user = CreateUser();
        var logs = CreateSampleAuditLogs(user);
        _mockContext.Setup(x => x.AuditLogs).Returns(logs.BuildMockDbSet().Object);

        var query = new GetAuditLogsQuery { UserId = user.UserId };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.Items.Should().AllSatisfy(item => item.UserId.Should().Be(user.UserId));
    }

    [Fact]
    public async Task Handle_FilterByDateRange_ReturnsMatchingLogs()
    {
        // Arrange
        var logs = CreateSampleAuditLogs();
        _mockContext.Setup(x => x.AuditLogs).Returns(logs.BuildMockDbSet().Object);

        var fromDate = DateTime.UtcNow.AddHours(-1.5);
        var toDate = DateTime.UtcNow.AddMinutes(-30);
        var query = new GetAuditLogsQuery { FromDate = fromDate, ToDate = toDate };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.Items[0].Action.Should().Be("Modified");
    }

    [Fact]
    public async Task Handle_Pagination_ReturnsCorrectPage()
    {
        // Arrange
        var user = CreateUser();
        var logs = Enumerable.Range(1, 15).Select(i => new AuditLog
        {
            LogId = NewId.NextGuid(),
            UserId = user.UserId,
            User = user,
            Action = "Added",
            TableName = "Users",
            RecordId = NewId.NextGuid(),
            Timestamp = DateTime.UtcNow.AddMinutes(-i),
            IPAddress = "127.0.0.1"
        }).ToList();
        _mockContext.Setup(x => x.AuditLogs).Returns(logs.BuildMockDbSet().Object);

        var query = new GetAuditLogsQuery { PageNumber = 2, PageSize = 5 };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(5);
        result.Value.TotalCount.Should().Be(15);
        result.Value.PageNumber.Should().Be(2);
        result.Value.PageSize.Should().Be(5);
        result.Value.HasPreviousPage.Should().BeTrue();
        result.Value.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_SortByActionAscending_ReturnsSortedLogs()
    {
        // Arrange
        var logs = CreateSampleAuditLogs();
        _mockContext.Setup(x => x.AuditLogs).Returns(logs.BuildMockDbSet().Object);

        var query = new GetAuditLogsQuery { SortBy = AuditLogSortBy.Action, SortDescending = false };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(3);
        result.Value.Items[0].Action.Should().Be("Added");
    }

    [Fact]
    public async Task Handle_SortByTableNameDescending_ReturnsSortedLogs()
    {
        // Arrange
        var logs = CreateSampleAuditLogs();
        _mockContext.Setup(x => x.AuditLogs).Returns(logs.BuildMockDbSet().Object);

        var query = new GetAuditLogsQuery { SortBy = AuditLogSortBy.TableName, SortDescending = true };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(3);
        result.Value.Items[0].TableName.Should().Be("Users");
    }

    [Fact]
    public async Task Handle_UserIsNull_ReturnsNullUsername()
    {
        // Arrange
        var logs = new List<AuditLog>
        {
            new()
            {
                LogId = NewId.NextGuid(),
                UserId = null,
                User = null,
                Action = "Added",
                TableName = "Users",
                RecordId = NewId.NextGuid(),
                Timestamp = DateTime.UtcNow,
                IPAddress = "10.0.0.1"
            }
        };
        _mockContext.Setup(x => x.AuditLogs).Returns(logs.BuildMockDbSet().Object);

        var query = new GetAuditLogsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.Items[0].UserId.Should().BeNull();
        result.Value.Items[0].Username.Should().BeNull();
    }

    [Fact]
    public async Task Handle_UserExists_ReturnsUsername()
    {
        // Arrange
        var user = CreateUser("admin_user");
        var logs = new List<AuditLog>
        {
            new()
            {
                LogId = NewId.NextGuid(),
                UserId = user.UserId,
                User = user,
                Action = "Modified",
                TableName = "Goals",
                RecordId = NewId.NextGuid(),
                OldValue = "{\"Title\":\"Old\"}",
                NewValue = "{\"Title\":\"New\"}",
                Timestamp = DateTime.UtcNow,
                IPAddress = "192.168.1.1"
            }
        };
        _mockContext.Setup(x => x.AuditLogs).Returns(logs.BuildMockDbSet().Object);

        var query = new GetAuditLogsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items[0].Username.Should().Be("admin_user");
        result.Value.Items[0].UserId.Should().Be(user.UserId);
    }

    [Fact]
    public async Task Handle_FilterByActionCaseInsensitive_ReturnsMatchingLogs()
    {
        // Arrange
        var logs = CreateSampleAuditLogs();
        _mockContext.Setup(x => x.AuditLogs).Returns(logs.BuildMockDbSet().Object);

        var query = new GetAuditLogsQuery { Action = "added" };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_MapsAllFieldsCorrectly()
    {
        // Arrange
        var user = CreateUser();
        var recordId = NewId.NextGuid();
        var logId = NewId.NextGuid();
        var timestamp = DateTime.UtcNow;
        var logs = new List<AuditLog>
        {
            new()
            {
                LogId = logId,
                UserId = user.UserId,
                User = user,
                Action = "Modified",
                TableName = "Users",
                RecordId = recordId,
                OldValue = "{\"Status\":\"Active\"}",
                NewValue = "{\"Status\":\"Banned\"}",
                Timestamp = timestamp,
                IPAddress = "192.168.1.100"
            }
        };
        _mockContext.Setup(x => x.AuditLogs).Returns(logs.BuildMockDbSet().Object);

        var query = new GetAuditLogsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var item = result.Value!.Items[0];
        item.LogId.Should().Be(logId);
        item.UserId.Should().Be(user.UserId);
        item.Username.Should().Be(user.Username);
        item.Action.Should().Be("Modified");
        item.TableName.Should().Be("Users");
        item.RecordId.Should().Be(recordId);
        item.OldValue.Should().Be("{\"Status\":\"Active\"}");
        item.NewValue.Should().Be("{\"Status\":\"Banned\"}");
        item.Timestamp.Should().Be(timestamp);
        item.IPAddress.Should().Be("192.168.1.100");
    }

    [Fact]
    public async Task Handle_LastPage_HasNextPageIsFalse()
    {
        // Arrange
        var user = CreateUser();
        var logs = Enumerable.Range(1, 5).Select(i => new AuditLog
        {
            LogId = NewId.NextGuid(),
            UserId = user.UserId,
            User = user,
            Action = "Added",
            TableName = "Users",
            RecordId = NewId.NextGuid(),
            Timestamp = DateTime.UtcNow.AddMinutes(-i),
            IPAddress = "127.0.0.1"
        }).ToList();
        _mockContext.Setup(x => x.AuditLogs).Returns(logs.BuildMockDbSet().Object);

        var query = new GetAuditLogsQuery { PageNumber = 1, PageSize = 10 };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.HasNextPage.Should().BeFalse();
        result.Value.HasPreviousPage.Should().BeFalse();
        result.Value.TotalPages.Should().Be(1);
    }

    [Fact]
    public async Task Handle_CombinedFilters_ReturnsFilteredResults()
    {
        // Arrange
        var user1 = CreateUser("user1");
        var user2 = CreateUser("user2");
        var logs = new List<AuditLog>
        {
            new()
            {
                LogId = NewId.NextGuid(),
                UserId = user1.UserId,
                User = user1,
                Action = "Modified",
                TableName = "Users",
                RecordId = NewId.NextGuid(),
                Timestamp = DateTime.UtcNow,
                IPAddress = "10.0.0.1"
            },
            new()
            {
                LogId = NewId.NextGuid(),
                UserId = user1.UserId,
                User = user1,
                Action = "Added",
                TableName = "Goals",
                RecordId = NewId.NextGuid(),
                Timestamp = DateTime.UtcNow,
                IPAddress = "10.0.0.1"
            },
            new()
            {
                LogId = NewId.NextGuid(),
                UserId = user2.UserId,
                User = user2,
                Action = "Modified",
                TableName = "Users",
                RecordId = NewId.NextGuid(),
                Timestamp = DateTime.UtcNow,
                IPAddress = "10.0.0.2"
            }
        };
        _mockContext.Setup(x => x.AuditLogs).Returns(logs.BuildMockDbSet().Object);

        var query = new GetAuditLogsQuery
        {
            Action = "Modified",
            TableName = "Users",
            UserId = user1.UserId
        };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.Items[0].Username.Should().Be("user1");
        result.Value.Items[0].Action.Should().Be("Modified");
        result.Value.Items[0].TableName.Should().Be("Users");
    }
}
