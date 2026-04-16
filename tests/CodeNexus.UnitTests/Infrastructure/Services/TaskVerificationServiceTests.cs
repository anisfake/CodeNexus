using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Domain.Enums;
using CodeNexus.Infrastructure.Services;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Infrastructure.Services;

public class TaskVerificationServiceTests
{
    [Fact]
    public async Task VerifyCodeSubmissionAsync_WithGibberishInput_ShouldStillCallAiAndReturnParsedResult()
    {
        // Arrange
        var aiGeneratorMock = new Mock<IAIGeneratorService>();
        aiGeneratorMock
            .Setup(x => x.GenerateStructureAsync<TaskVerificationService.AIVerificationResponse>(
                It.IsAny<string>(),
                AIUsageType.Verification))
            .ReturnsAsync(new TaskVerificationService.AIVerificationResponse
            {
                Score = 10,
                Feedback = "Bubble sort cua ban chua dung vi vong lap sap xep mang chua day du."
            });

        var service = new TaskVerificationService(aiGeneratorMock.Object);

        // Act
        var result = await service.VerifyCodeSubmissionAsync(
            "Viet ham Bubble Sort",
            "Sap xep mang tang dan",
            "fsccxzcx",
            "Check bubble sort");

        // Assert
        Assert.False(result.IsPass);
        Assert.Equal(10, result.Score);
        Assert.Contains("Bubble sort", result.Feedback);
        aiGeneratorMock.Verify(x => x.GenerateStructureAsync<TaskVerificationService.AIVerificationResponse>(
            It.IsAny<string>(),
            AIUsageType.Verification), Times.Once);
    }
}
