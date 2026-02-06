using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Resources.DTOs;
using CodeNexus.Application.Features.Resources.Queries.GetMyResources;
using CodeNexus.Domain.Entities;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.Resources
{
    public class GetMyResourcesQueryHandlerTests
    {
        private readonly Mock<IApplicationDbContext> _mockContext;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly GetMyResourcesQueryHandler _handler;

        public GetMyResourcesQueryHandlerTests()
        {
            _mockContext = new Mock<IApplicationDbContext>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _handler = new GetMyResourcesQueryHandler(_mockContext.Object, _mockCurrentUserService.Object);
        }

        [Fact]
        public async Task Handle_WithValidQuery_ReturnsResourcesWithPagination()
        {
            // Arrange
            var userId = NewId.NextGuid();
            var subjectId = NewId.NextGuid();
            var cancellationToken = CancellationToken.None;

            var subject = new Subject { SubjectId = subjectId, Name = "C# Programming" };
            var resources = new List<Resource>
            {
                new Resource
                {
                    ResourceId = NewId.NextGuid(),
                    UserId = userId,
                    SubjectId = subjectId,
                    Title = "C# Basics",
                    Type = "pdf",
                    URL = "https://example.com/csharp-basics.pdf",
                    Description = "Introduction to C#",
                    FilePath = "/resources/csharp-basics.pdf",
                    UploadedAt = DateTime.UtcNow.AddDays(-5),
                    Subject = subject
                },
                new Resource
                {
                    ResourceId = NewId.NextGuid(),
                    UserId = userId,
                    SubjectId = subjectId,
                    Title = "Advanced C#",
                    Type = "pdf",
                    URL = "https://example.com/advanced-csharp.pdf",
                    Description = "Advanced C# concepts",
                    FilePath = "/resources/advanced-csharp.pdf",
                    UploadedAt = DateTime.UtcNow.AddDays(-3),
                    Subject = subject
                }
            };

            var query = new GetMyResourcesQuery
            {
                PageNumber = 1,
                PageSize = 10,
                SortBy = "CreatedAt",
                SortDescending = true
            };

            _mockCurrentUserService.Setup(s => s.GetUserId()).Returns(userId);
            _mockContext.Setup(c => c.Resources).Returns(resources.BuildMockDbSet().Object);

            // Act
            var result = await _handler.Handle(query, cancellationToken);

            // Assert
            result.Should().NotBeNull();
            result.Items.Should().HaveCount(2);
            result.TotalCount.Should().Be(2);
            result.PageNumber.Should().Be(1);
            result.PageSize.Should().Be(10);
            result.HasNextPage.Should().BeFalse();
            result.HasPreviousPage.Should().BeFalse();
            result.Items[0].Title.Should().Be("Advanced C#");
            result.Items[1].Title.Should().Be("C# Basics");
        }

        [Fact]
        public async Task Handle_WithSearchTerm_ReturnsFilteredResources()
        {
            // Arrange
            var userId = NewId.NextGuid();
            var subjectId = NewId.NextGuid();
            var cancellationToken = CancellationToken.None;

            var subject = new Subject { SubjectId = subjectId, Name = "Programming" };
            var resources = new List<Resource>
            {
                new Resource
                {
                    ResourceId = NewId.NextGuid(),
                    UserId = userId,
                    SubjectId = subjectId,
                    Title = "C# Basics",
                    Type = "pdf",
                    URL = "https://example.com/csharp.pdf",
                    Description = "Learn C#",
                    FilePath = "/resources/csharp.pdf",
                    UploadedAt = DateTime.UtcNow,
                    Subject = subject
                },
                new Resource
                {
                    ResourceId = NewId.NextGuid(),
                    UserId = userId,
                    SubjectId = subjectId,
                    Title = "Java Basics",
                    Type = "pdf",
                    URL = "https://example.com/java.pdf",
                    Description = "Learn Java",
                    FilePath = "/resources/java.pdf",
                    UploadedAt = DateTime.UtcNow,
                    Subject = subject
                }
            };

            var query = new GetMyResourcesQuery
            {
                PageNumber = 1,
                PageSize = 10,
                SearchTerm = "C#"
            };

            _mockCurrentUserService.Setup(s => s.GetUserId()).Returns(userId);
            _mockContext.Setup(c => c.Resources).Returns(resources.BuildMockDbSet().Object);

            // Act
            var result = await _handler.Handle(query, cancellationToken);

            // Assert
            result.Items.Should().HaveCount(1);
            result.Items[0].Title.Should().Be("C# Basics");
            result.TotalCount.Should().Be(1);
        }

        [Fact]
        public async Task Handle_WithSubjectFilter_ReturnsResourcesForSpecificSubject()
        {
            // Arrange
            var userId = NewId.NextGuid();
            var subjectId1 = NewId.NextGuid();
            var subjectId2 = NewId.NextGuid();
            var cancellationToken = CancellationToken.None;

            var subject1 = new Subject { SubjectId = subjectId1, Name = "C#" };
            var subject2 = new Subject { SubjectId = subjectId2, Name = "Java" };

            var resources = new List<Resource>
            {
                new Resource
                {
                    ResourceId = NewId.NextGuid(),
                    UserId = userId,
                    SubjectId = subjectId1,
                    Title = "C# Guide",
                    Type = "pdf",
                    URL = "https://example.com/csharp.pdf",
                    Description = "C# guide",
                    FilePath = "/resources/csharp.pdf",
                    UploadedAt = DateTime.UtcNow,
                    Subject = subject1
                },
                new Resource
                {
                    ResourceId = NewId.NextGuid(),
                    UserId = userId,
                    SubjectId = subjectId2,
                    Title = "Java Guide",
                    Type = "pdf",
                    URL = "https://example.com/java.pdf",
                    Description = "Java guide",
                    FilePath = "/resources/java.pdf",
                    UploadedAt = DateTime.UtcNow,
                    Subject = subject2
                }
            };

            var query = new GetMyResourcesQuery
            {
                PageNumber = 1,
                PageSize = 10,
                SubjectId = subjectId1
            };

            _mockCurrentUserService.Setup(s => s.GetUserId()).Returns(userId);
            _mockContext.Setup(c => c.Resources).Returns(resources.BuildMockDbSet().Object);

            // Act
            var result = await _handler.Handle(query, cancellationToken);

            // Assert
            result.Items.Should().HaveCount(1);
            result.Items[0].SubjectId.Should().Be(subjectId1);
            result.Items[0].SubjectName.Should().Be("C#");
        }

        [Fact]
        public async Task Handle_WithTypeFilter_ReturnsResourcesOfSpecificType()
        {
            // Arrange
            var userId = NewId.NextGuid();
            var subjectId = NewId.NextGuid();
            var cancellationToken = CancellationToken.None;

            var subject = new Subject { SubjectId = subjectId, Name = "Programming" };
            var resources = new List<Resource>
            {
                new Resource
                {
                    ResourceId = NewId.NextGuid(),
                    UserId = userId,
                    SubjectId = subjectId,
                    Title = "PDF Guide",
                    Type = "pdf",
                    URL = "https://example.com/guide.pdf",
                    Description = "PDF guide",
                    FilePath = "/resources/guide.pdf",
                    UploadedAt = DateTime.UtcNow,
                    Subject = subject
                },
                new Resource
                {
                    ResourceId = NewId.NextGuid(),
                    UserId = userId,
                    SubjectId = subjectId,
                    Title = "Video Tutorial",
                    Type = "video",
                    URL = "https://example.com/tutorial.mp4",
                    Description = "Video tutorial",
                    FilePath = "/resources/tutorial.mp4",
                    UploadedAt = DateTime.UtcNow,
                    Subject = subject
                }
            };

            var query = new GetMyResourcesQuery
            {
                PageNumber = 1,
                PageSize = 10,
                Type = "pdf"
            };

            _mockCurrentUserService.Setup(s => s.GetUserId()).Returns(userId);
            _mockContext.Setup(c => c.Resources).Returns(resources.BuildMockDbSet().Object);

            // Act
            var result = await _handler.Handle(query, cancellationToken);

            // Assert
            result.Items.Should().HaveCount(1);
            result.Items[0].Type.Should().Be("pdf");
        }

        [Fact]
        public async Task Handle_WithPagination_ReturnsPaginatedResults()
        {
            // Arrange
            var userId = NewId.NextGuid();
            var subjectId = NewId.NextGuid();
            var cancellationToken = CancellationToken.None;

            var subject = new Subject { SubjectId = subjectId, Name = "Programming" };
            var resources = Enumerable.Range(1, 25).Select(i => new Resource
            {
                ResourceId = NewId.NextGuid(),
                UserId = userId,
                SubjectId = subjectId,
                Title = $"Resource {i}",
                Type = "pdf",
                URL = $"https://example.com/resource{i}.pdf",
                Description = $"Resource {i} description",
                FilePath = $"/resources/resource{i}.pdf",
                UploadedAt = DateTime.UtcNow.AddDays(-i),
                Subject = subject
            }).ToList();

            var query = new GetMyResourcesQuery
            {
                PageNumber = 2,
                PageSize = 10
            };

            _mockCurrentUserService.Setup(s => s.GetUserId()).Returns(userId);
            _mockContext.Setup(c => c.Resources).Returns(resources.BuildMockDbSet().Object);

            // Act
            var result = await _handler.Handle(query, cancellationToken);

            // Assert
            result.Items.Should().HaveCount(10);
            result.PageNumber.Should().Be(2);
            result.PageSize.Should().Be(10);
            result.TotalCount.Should().Be(25);
            result.TotalPages.Should().Be(3);
            result.HasPreviousPage.Should().BeTrue();
            result.HasNextPage.Should().BeTrue();
        }

        [Fact]
        public async Task Handle_WithSortByTitle_ReturnsSortedByTitle()
        {
            // Arrange
            var userId = NewId.NextGuid();
            var subjectId = NewId.NextGuid();
            var cancellationToken = CancellationToken.None;

            var subject = new Subject { SubjectId = subjectId, Name = "Programming" };
            var resources = new List<Resource>
            {
                new Resource
                {
                    ResourceId = NewId.NextGuid(),
                    UserId = userId,
                    SubjectId = subjectId,
                    Title = "Zebra Guide",
                    Type = "pdf",
                    URL = "https://example.com/zebra.pdf",
                    Description = "Zebra",
                    FilePath = "/resources/zebra.pdf",
                    UploadedAt = DateTime.UtcNow,
                    Subject = subject
                },
                new Resource
                {
                    ResourceId = NewId.NextGuid(),
                    UserId = userId,
                    SubjectId = subjectId,
                    Title = "Apple Guide",
                    Type = "pdf",
                    URL = "https://example.com/apple.pdf",
                    Description = "Apple",
                    FilePath = "/resources/apple.pdf",
                    UploadedAt = DateTime.UtcNow,
                    Subject = subject
                }
            };

            var query = new GetMyResourcesQuery
            {
                PageNumber = 1,
                PageSize = 10,
                SortBy = "title",
                SortDescending = false
            };

            _mockCurrentUserService.Setup(s => s.GetUserId()).Returns(userId);
            _mockContext.Setup(c => c.Resources).Returns(resources.BuildMockDbSet().Object);

            // Act
            var result = await _handler.Handle(query, cancellationToken);

            // Assert
            result.Items[0].Title.Should().Be("Apple Guide");
            result.Items[1].Title.Should().Be("Zebra Guide");
        }

        [Fact]
        public async Task Handle_WithNoResources_ReturnsEmptyList()
        {
            // Arrange
            var userId = NewId.NextGuid();
            var cancellationToken = CancellationToken.None;

            var resources = new List<Resource>();

            var query = new GetMyResourcesQuery
            {
                PageNumber = 1,
                PageSize = 10
            };

            _mockCurrentUserService.Setup(s => s.GetUserId()).Returns(userId);
            _mockContext.Setup(c => c.Resources).Returns(resources.BuildMockDbSet().Object);

            // Act
            var result = await _handler.Handle(query, cancellationToken);

            // Assert
            result.Items.Should().BeEmpty();
            result.TotalCount.Should().Be(0);
            result.TotalPages.Should().Be(0);
        }

        [Fact]
        public async Task Handle_WithCombinedFilters_ReturnsCorrectResults()
        {
            // Arrange
            var userId = NewId.NextGuid();
            var subjectId = NewId.NextGuid();
            var cancellationToken = CancellationToken.None;

            var subject = new Subject { SubjectId = subjectId, Name = "C#" };
            var resources = new List<Resource>
            {
                new Resource
                {
                    ResourceId = NewId.NextGuid(),
                    UserId = userId,
                    SubjectId = subjectId,
                    Title = "C# Advanced Patterns",
                    Type = "pdf",
                    URL = "https://example.com/patterns.pdf",
                    Description = "Advanced design patterns in C#",
                    FilePath = "/resources/patterns.pdf",
                    UploadedAt = DateTime.UtcNow.AddDays(-2),
                    Subject = subject
                },
                new Resource
                {
                    ResourceId = NewId.NextGuid(),
                    UserId = userId,
                    SubjectId = subjectId,
                    Title = "C# Basics Video",
                    Type = "video",
                    URL = "https://example.com/basics.mp4",
                    Description = "Basic C# concepts",
                    FilePath = "/resources/basics.mp4",
                    UploadedAt = DateTime.UtcNow.AddDays(-1),
                    Subject = subject
                }
            };

            var query = new GetMyResourcesQuery
            {
                PageNumber = 1,
                PageSize = 10,
                SubjectId = subjectId,
                Type = "pdf",
                SearchTerm = "Advanced"
            };

            _mockCurrentUserService.Setup(s => s.GetUserId()).Returns(userId);
            _mockContext.Setup(c => c.Resources).Returns(resources.BuildMockDbSet().Object);

            // Act
            var result = await _handler.Handle(query, cancellationToken);

            // Assert
            result.Items.Should().HaveCount(1);
            result.Items[0].Title.Should().Be("C# Advanced Patterns");
            result.Items[0].Type.Should().Be("pdf");
        }

        [Fact]
        public async Task Handle_WithSearchInDescription_ReturnsMatchingResources()
        {
            // Arrange
            var userId = NewId.NextGuid();
            var subjectId = NewId.NextGuid();
            var cancellationToken = CancellationToken.None;

            var subject = new Subject { SubjectId = subjectId, Name = "Programming" };
            var resources = new List<Resource>
            {
                new Resource
                {
                    ResourceId = NewId.NextGuid(),
                    UserId = userId,
                    SubjectId = subjectId,
                    Title = "Resource 1",
                    Type = "pdf",
                    URL = "https://example.com/resource1.pdf",
                    Description = "Contains important information",
                    FilePath = "/resources/resource1.pdf",
                    UploadedAt = DateTime.UtcNow,
                    Subject = subject
                },
                new Resource
                {
                    ResourceId = NewId.NextGuid(),
                    UserId = userId,
                    SubjectId = subjectId,
                    Title = "Resource 2",
                    Type = "pdf",
                    URL = "https://example.com/resource2.pdf",
                    Description = "Basic overview",
                    FilePath = "/resources/resource2.pdf",
                    UploadedAt = DateTime.UtcNow,
                    Subject = subject
                }
            };

            var query = new GetMyResourcesQuery
            {
                PageNumber = 1,
                PageSize = 10,
                SearchTerm = "important"
            };

            _mockCurrentUserService.Setup(s => s.GetUserId()).Returns(userId);
            _mockContext.Setup(c => c.Resources).Returns(resources.BuildMockDbSet().Object);

            // Act
            var result = await _handler.Handle(query, cancellationToken);

            // Assert
            result.Items.Should().HaveCount(1);
            result.Items[0].Title.Should().Be("Resource 1");
        }
    }
}
