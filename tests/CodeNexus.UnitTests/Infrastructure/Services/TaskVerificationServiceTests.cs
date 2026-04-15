using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Infrastructure.Services;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Infrastructure.Services;

public class TaskVerificationServiceTests
{
    [Fact]
    public async Task VerifyCodeSubmissionAsync_WithGibberishInput_ShouldReturnLowScoreWithoutCallingAi()
    {
        // Arrange
        var aiGeneratorMock = new Mock<IAIGeneratorService>();
        var service = new TaskVerificationService(aiGeneratorMock.Object);

        // Act
        var result = await service.VerifyCodeSubmissionAsync(
            "Viết hàm Bubble Sort",
            "Sắp xếp mảng tăng dần",
            "fsccxzcx",
            "Check bubble sort");

        // Assert
        Assert.False(result.IsPass);
        Assert.True(result.Score <= 15);
        Assert.Contains("chưa giống mã nguồn", result.Feedback);
        aiGeneratorMock.VerifyNoOtherCalls();
    }
}
