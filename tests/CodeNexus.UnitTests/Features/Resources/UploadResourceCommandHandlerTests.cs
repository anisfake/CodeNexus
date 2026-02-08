using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Resources.Commands.UploadResource;
using CodeNexus.Application.Features.Resources.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using MassTransit;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.Resources;

public class UploadResourceCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly Mock<ICloudinaryService> _mockCloudinaryService;
    private readonly UploadResourceCommandHandler _handler;

    public UploadResourceCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockCloudinaryService = new Mock<ICloudinaryService>();
        _handler = new UploadResourceCommandHandler(_mockContext.Object, _mockCurrentUserService.Object, _mockCloudinaryService.Object);
    }

    [Fact]
    public async Task Handle_WithValidFileResource_ShouldUploadAndReturnSuccess()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var subjectId = NewId.NextGuid();
        var fileName = "test.pdf";
        var fileStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var uploadedUrl = "https://cloudinary.com/resources/test.pdf";

        var subject = new Subject { SubjectId = subjectId, Name = "Math" };
        var command = new UploadResourceCommand()
        {
            FileName = fileName,
            Title = "Test Resource",
            Type = ResourceType.File,
            FilePath = fileStream,
            Description = "Test Description",
            SubjectId = subjectId
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        
        var subjects = new List<Subject> { subject }.BuildMockDbSet().Object;
        _mockContext.Setup(x => x.Subjects).Returns(subjects);
        
        _mockCloudinaryService.Setup(x => x.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(uploadedUrl);
        
        var resources = new List<Resource>().BuildMockDbSet().Object;
        _mockContext.Setup(x => x.Resources).Returns(resources);
        
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Test Resource", result.Value.Title);
        Assert.Equal(uploadedUrl, result.Value.FilePath);
    }

    [Fact]
    public async Task Handle_WithValidLinkResource_ShouldReturnSuccess()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var subjectId = NewId.NextGuid();
        var url = "https://example.com/resource";

        var subject = new Subject { SubjectId = subjectId, Name = "Math" };
        var command = new UploadResourceCommand()
        {
            FileName = "link",
            Title = "Test Link",
            Type = ResourceType.Link,
            Url = url,
            Description = "Test Link Description",
            SubjectId = subjectId
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        
        var subjects = new List<Subject> { subject }.BuildMockDbSet().Object;
        _mockContext.Setup(x => x.Subjects).Returns(subjects);
        
        var resources = new List<Resource>().BuildMockDbSet().Object;
        _mockContext.Setup(x => x.Resources).Returns(resources);
        
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Test Link", result.Value.Title);
        Assert.Equal(url, result.Value.Url);
        _mockCloudinaryService.Verify(x => x.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithInvalidSubject_ShouldReturnFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var subjectId = NewId.NextGuid();
        var fileStream = new MemoryStream(new byte[] { 1, 2, 3 });

        var command = new UploadResourceCommand()
        {
            FileName = "test.pdf",
            Title = "Test Resource",
            Type = ResourceType.File,
            FilePath = fileStream,
            SubjectId = subjectId
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        
        var subjects = new List<Subject>().BuildMockDbSet().Object;
        _mockContext.Setup(x => x.Subjects).Returns(subjects);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("SUBJECT_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task Handle_WithFileUploadFailure_ShouldReturnFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var subjectId = NewId.NextGuid();
        var fileStream = new MemoryStream(new byte[] { 1, 2, 3 });

        var subject = new Subject { SubjectId = subjectId, Name = "Math" };
        var command = new UploadResourceCommand()
        {
            FileName = "test.pdf",
            Title = "Test Resource",
            Type = ResourceType.File,
            FilePath = fileStream,
            SubjectId = subjectId
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        
        var subjects = new List<Subject> { subject }.BuildMockDbSet().Object;
        _mockContext.Setup(x => x.Subjects).Returns(subjects);
        
        _mockCloudinaryService.Setup(x => x.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("UPLOAD_FAIL", result.ErrorCode);
    }

    [Fact]
    public async Task Handle_WithNullDescription_ShouldReturnSuccess()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var subjectId = NewId.NextGuid();
        var subject = new Subject { SubjectId = subjectId, Name = "Math" };

        var command = new UploadResourceCommand()
        {
            FileName = "test.pdf",
            Title = "Test Resource",
            Type = ResourceType.File,
            FilePath = new MemoryStream(new byte[] { 1, 2, 3 }),
            Description = null,
            SubjectId = subjectId
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        
        var subjects = new List<Subject> { subject }.BuildMockDbSet().Object;
        _mockContext.Setup(x => x.Subjects).Returns(subjects);
        
        _mockCloudinaryService.Setup(x => x.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("https://cloudinary.com/resource");
        
        var resources = new List<Resource>().BuildMockDbSet().Object;
        _mockContext.Setup(x => x.Resources).Returns(resources);
        
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Description);
    }
}
