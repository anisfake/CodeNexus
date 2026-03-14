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
    private readonly Mock<IPdfProcessingService> _mockPdfProcessingService;
    private readonly Mock<IResourceCacheService> _mockResourceCacheService;
    private readonly UploadResourceCommandHandler _handler;

    public UploadResourceCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockCloudinaryService = new Mock<ICloudinaryService>();
        _mockPdfProcessingService = new Mock<IPdfProcessingService>();
        _mockResourceCacheService = new Mock<IResourceCacheService>();
        _mockResourceCacheService
            .Setup(x => x.InvalidateUserResourcesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _handler = new UploadResourceCommandHandler(_mockContext.Object, _mockCurrentUserService.Object, _mockCloudinaryService.Object, _mockPdfProcessingService.Object, _mockResourceCacheService.Object);
    }

    [Fact]
    public async Task Handle_WithValidPdfResource_ShouldUploadAndReturnSuccess()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var subjectId = NewId.NextGuid();
        var fileName = "test.pdf";
        var fileStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var uploadedUrl = "https://cloudinary.com/resources/test.pdf";

        var command = new UploadResourceCommand()
        {
            FileName = fileName,
            Title = "Test Resource",
            FilePath = fileStream,
            Description = "Test Description",
            SubjectId = subjectId
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        
        _mockCloudinaryService.Setup(x => x.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(uploadedUrl);

        _mockPdfProcessingService.Setup(x => x.ProcessPdfAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync(new PdfProcessingResult
            {
                TotalPages = 5,
                Pages = new List<PdfPageData>
                {
                    new PdfPageData { PageNumber = 1, ImageUrl = "page1.jpg", ExtractedText = "Page 1" }
                }
            });
        
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
        Assert.Equal(5, result.Value.TotalPages);
    }

    [Fact]
    public async Task Handle_WithNullFile_ShouldReturnFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var subjectId = NewId.NextGuid();

        var command = new UploadResourceCommand()
        {
            FileName = "",
            Title = "Test Resource",
            FilePath = null,
            Description = "Test Description",
            SubjectId = subjectId
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_FILE", result.ErrorCode);
    }

    [Fact]
    public async Task Handle_WithNonPdfFile_ShouldReturnFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var subjectId = NewId.NextGuid();
        var fileStream = new MemoryStream(new byte[] { 1, 2, 3 });

        var command = new UploadResourceCommand()
        {
            FileName = "test.txt",
            Title = "Test Resource",
            FilePath = fileStream,
            SubjectId = subjectId
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_FILE_TYPE", result.ErrorCode);
    }

    [Fact]
    public async Task Handle_WithFileUploadFailure_ShouldReturnFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var subjectId = NewId.NextGuid();
        var fileStream = new MemoryStream(new byte[] { 1, 2, 3 });

        var command = new UploadResourceCommand()
        {
            FileName = "test.pdf",
            Title = "Test Resource",
            FilePath = fileStream,
            SubjectId = subjectId
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        
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

        var command = new UploadResourceCommand()
        {
            FileName = "test.pdf",
            Title = "Test Resource",
            FilePath = new MemoryStream(new byte[] { 1, 2, 3 }),
            Description = null,
            SubjectId = subjectId
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        
        _mockCloudinaryService.Setup(x => x.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("https://cloudinary.com/resource");

        _mockPdfProcessingService.Setup(x => x.ProcessPdfAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync(new PdfProcessingResult
            {
                TotalPages = 1,
                Pages = new List<PdfPageData>()
            });
        
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
