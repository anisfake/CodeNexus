using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.AIUsageLogs.Queries.GetMentorAiQuotaStatus;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.AIUsageLogs;

public class GetMentorAiQuotaStatusQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<IAIAccessPolicyService> _mockAiAccessPolicyService;
    private readonly GetMentorAiQuotaStatusQueryHandler _handler;

    public GetMentorAiQuotaStatusQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockAiAccessPolicyService = new Mock<IAIAccessPolicyService>();
        _handler = new GetMentorAiQuotaStatusQueryHandler(_mockContext.Object, _mockAiAccessPolicyService.Object);
    }

    [Fact]
    public async Task Handle_DefaultOnlyNearOrReached_ShouldReturnNearAndReachedMentors()
    {
        var mentorRoleId = Guid.NewGuid();
        var mentor1Id = Guid.NewGuid();
        var mentor2Id = Guid.NewGuid();
        var mentor3Id = Guid.NewGuid();
        var studentRoleId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        _mockContext.Setup(x => x.Roles).Returns(new List<Role>
        {
            new() { RoleId = mentorRoleId, RoleName = "Mentor" },
            new() { RoleId = studentRoleId, RoleName = "Student" }
        }.BuildMockDbSet().Object);

        _mockContext.Setup(x => x.Users).Returns(new List<User>
        {
            new() { UserId = mentor1Id, Username = "mentor.one", Email = "m1@test.com", RoleId = mentorRoleId },
            new() { UserId = mentor2Id, Username = "mentor.two", Email = "m2@test.com", RoleId = mentorRoleId },
            new() { UserId = mentor3Id, Username = "mentor.three", Email = "m3@test.com", RoleId = mentorRoleId },
            new() { UserId = studentId, Username = "student", Email = "s@test.com", RoleId = studentRoleId }
        }.BuildMockDbSet().Object);

        var logs = new List<FeatureUsageLog>();
        logs.AddRange(Enumerable.Range(1, 9).Select(_ => new FeatureUsageLog
        {
            FeatureUsageLogId = Guid.NewGuid(),
            UserId = mentor1Id,
            FeatureKey = SubscriptionFeatureKey.MentorPaidAiRequests,
            CreatedAt = DateTime.UtcNow
        }));
        logs.AddRange(Enumerable.Range(1, 10).Select(_ => new FeatureUsageLog
        {
            FeatureUsageLogId = Guid.NewGuid(),
            UserId = mentor2Id,
            FeatureKey = SubscriptionFeatureKey.MentorPaidAiRequests,
            CreatedAt = DateTime.UtcNow
        }));
        logs.AddRange(Enumerable.Range(1, 2).Select(_ => new FeatureUsageLog
        {
            FeatureUsageLogId = Guid.NewGuid(),
            UserId = mentor3Id,
            FeatureKey = SubscriptionFeatureKey.MentorPaidAiRequests,
            CreatedAt = DateTime.UtcNow
        }));

        _mockContext.Setup(x => x.FeatureUsageLogs).Returns(logs.BuildMockDbSet().Object);
        _mockAiAccessPolicyService.Setup(x => x.GetMentorPaidRequestsMonthlyLimitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(10);

        var result = await _handler.Handle(new GetMentorAiQuotaStatusQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.Items.Should().Contain(x => x.MentorId == mentor1Id && x.IsNearLimit && !x.IsReachedLimit);
        result.Value.Items.Should().Contain(x => x.MentorId == mentor2Id && x.IsReachedLimit);
        result.Value.Items.Should().NotContain(x => x.MentorId == mentor3Id);
    }

    [Fact]
    public async Task Handle_WithOnlyNearOrReachedFalse_ShouldReturnAllMentorsWithPaging()
    {
        var mentorRoleId = Guid.NewGuid();
        var mentor1Id = Guid.NewGuid();
        var mentor2Id = Guid.NewGuid();

        _mockContext.Setup(x => x.Roles).Returns(new List<Role>
        {
            new() { RoleId = mentorRoleId, RoleName = "Mentor" }
        }.BuildMockDbSet().Object);

        _mockContext.Setup(x => x.Users).Returns(new List<User>
        {
            new() { UserId = mentor1Id, Username = "alpha.mentor", Email = "a@test.com", RoleId = mentorRoleId },
            new() { UserId = mentor2Id, Username = "beta.mentor", Email = "b@test.com", RoleId = mentorRoleId }
        }.BuildMockDbSet().Object);

        _mockContext.Setup(x => x.FeatureUsageLogs).Returns(new List<FeatureUsageLog>
        {
            new()
            {
                FeatureUsageLogId = Guid.NewGuid(),
                UserId = mentor1Id,
                FeatureKey = SubscriptionFeatureKey.MentorPaidAiRequests,
                CreatedAt = DateTime.UtcNow
            }
        }.BuildMockDbSet().Object);

        _mockAiAccessPolicyService.Setup(x => x.GetMentorPaidRequestsMonthlyLimitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(100);

        var query = new GetMentorAiQuotaStatusQuery
        {
            OnlyNearOrReached = false,
            Search = "mentor",
            PageNumber = 1,
            PageSize = 1
        };

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.TotalCount.Should().Be(2);
        result.Value.Items.Should().HaveCount(1);
    }
}
