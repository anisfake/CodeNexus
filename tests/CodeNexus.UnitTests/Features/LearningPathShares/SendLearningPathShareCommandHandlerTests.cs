using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.LearningPathShares.Commands.SendLearningPathShare;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;

namespace CodeNexus.UnitTests.Features.LearningPathShares;

public class SendLearningPathShareCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly Mock<ILearningPathShareRealtimeNotifier> _mockRealtimeNotifier;
    private readonly SendLearningPathShareCommandHandler _handler;

    public SendLearningPathShareCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockRealtimeNotifier = new Mock<ILearningPathShareRealtimeNotifier>();
        _handler = new SendLearningPathShareCommandHandler(_mockContext.Object, _mockCurrentUserService.Object, _mockRealtimeNotifier.Object);
    }

    [Fact]
    public async Task Handle_DraftPath_KeepsDraftStatusAndCreatesShareMessage()
    {
        var mentorId = NewId.NextGuid();
        var studentId = NewId.NextGuid();
        var pathId = NewId.NextGuid();

        var mentor = new User
        {
            UserId = mentorId,
            Username = "mentor",
            Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Mentor" }
        };

        var student = new User
        {
            UserId = studentId,
            Username = "student",
            Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Student" }
        };

        var learningPath = new LearningPath
        {
            PathId = pathId,
            UserId = mentorId,
            SubjectId = NewId.NextGuid(),
            Title = "Draft LP",
            Status = LearningPathStatus.Draft.ToString()
        };

        var chapterId = NewId.NextGuid();
        var lessonId = NewId.NextGuid();

        var chapter = new Chapter
        {
            ChapterId = chapterId,
            PathId = pathId,
            Title = "Chapter 1",
            OrderIndex = 1,
            IsDeleted = false
        };

        var lesson = new Lesson
        {
            LessonId = lessonId,
            ChapterId = chapterId,
            Title = "Lesson 1",
            Content = "Lesson content",
            OrderIndex = 1,
            LessonDay = DateTime.UtcNow,
            IsDeleted = false
        };

        var chapterTask = new CodeNexus.Domain.Entities.Tasks
        {
            TaskId = NewId.NextGuid(),
            ChapterId = chapterId,
            PathId = pathId,
            Title = "Practice task"
        };

        var quiz = new Quiz
        {
            QuizId = NewId.NextGuid(),
            LessonId = lessonId,
            Title = "Quiz 1",
            IsDeleted = false
        };

        var usersDbSet = new List<User> { mentor, student }.BuildMockDbSet();
        var pathsDbSet = new List<LearningPath> { learningPath }.BuildMockDbSet();
        var chaptersDbSet = new List<Chapter> { chapter }.BuildMockDbSet();
        var lessonsDbSet = new List<Lesson> { lesson }.BuildMockDbSet();
        var tasksDbSet = new List<CodeNexus.Domain.Entities.Tasks> { chapterTask }.BuildMockDbSet();
        var quizzesDbSet = new List<Quiz> { quiz }.BuildMockDbSet();

        var shares = new List<LearningPathShare>();
        var sharesDbSet = shares.BuildMockDbSet();
        sharesDbSet.Setup(x => x.Add(It.IsAny<LearningPathShare>()))
            .Callback<LearningPathShare>(shares.Add);

        var conversations = new List<DirectConversation>();
        var conversationsDbSet = conversations.BuildMockDbSet();
        conversationsDbSet.Setup(x => x.Add(It.IsAny<DirectConversation>()))
            .Callback<DirectConversation>(conversations.Add);

        var messages = new List<DirectMessage>();
        var messagesDbSet = messages.BuildMockDbSet();
        messagesDbSet.Setup(x => x.Add(It.IsAny<DirectMessage>()))
            .Callback<DirectMessage>(messages.Add);

        var receipts = new List<DirectMessageReceipt>();
        var receiptsDbSet = receipts.BuildMockDbSet();
        receiptsDbSet.Setup(x => x.Add(It.IsAny<DirectMessageReceipt>()))
            .Callback<DirectMessageReceipt>(receipts.Add);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(mentorId);
        _mockContext.Setup(x => x.Users).Returns(usersDbSet.Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(pathsDbSet.Object);
        _mockContext.Setup(x => x.Chapters).Returns(chaptersDbSet.Object);
        _mockContext.Setup(x => x.Lessons).Returns(lessonsDbSet.Object);
        _mockContext.Setup(x => x.Tasks).Returns(tasksDbSet.Object);
        _mockContext.Setup(x => x.Quizzes).Returns(quizzesDbSet.Object);
        _mockContext.Setup(x => x.LearningPathShares).Returns(sharesDbSet.Object);
        _mockContext.Setup(x => x.DirectConversations).Returns(conversationsDbSet.Object);
        _mockContext.Setup(x => x.DirectMessages).Returns(messagesDbSet.Object);
        _mockContext.Setup(x => x.DirectMessageReceipts).Returns(receiptsDbSet.Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _handler.Handle(new SendLearningPathShareCommand(pathId, studentId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        learningPath.Status.Should().Be(LearningPathStatus.Draft.ToString());
        shares.Should().HaveCount(1);
        messages.Should().HaveCount(1);
        messages[0].MessageType.Should().Be(DirectMessageType.LearningPathShare);
        messages[0].LearningPathShareId.Should().Be(shares[0].ShareId);
        receipts.Should().HaveCount(1);
        _mockRealtimeNotifier.Verify(x => x.NotifyShareSentAsync(
            studentId,
            It.IsAny<Guid>(),
            It.IsAny<string?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<CodeNexus.Application.Features.DirectChats.DTOs.DirectMessageDto>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ChapterWithoutTask_ReturnsFailure()
    {
        var mentorId = NewId.NextGuid();
        var studentId = NewId.NextGuid();
        var pathId = NewId.NextGuid();
        var chapterId = NewId.NextGuid();
        var lessonId = NewId.NextGuid();

        var mentor = new User
        {
            UserId = mentorId,
            Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Mentor" }
        };

        var student = new User
        {
            UserId = studentId,
            Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Student" }
        };

        var learningPath = new LearningPath
        {
            PathId = pathId,
            UserId = mentorId,
            SubjectId = NewId.NextGuid(),
            Title = "Path",
            Status = LearningPathStatus.Draft.ToString()
        };

        var chapter = new Chapter
        {
            ChapterId = chapterId,
            PathId = pathId,
            Title = "Chapter 1",
            OrderIndex = 1,
            IsDeleted = false
        };

        var lesson = new Lesson
        {
            LessonId = lessonId,
            ChapterId = chapterId,
            Title = "Lesson 1",
            Content = "Lesson content",
            OrderIndex = 1,
            LessonDay = DateTime.UtcNow,
            IsDeleted = false
        };

        var quiz = new Quiz
        {
            QuizId = NewId.NextGuid(),
            LessonId = lessonId,
            Title = "Quiz 1",
            IsDeleted = false
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(mentorId);
        _mockContext.Setup(x => x.Users).Returns(new List<User> { mentor, student }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new List<LearningPath> { learningPath }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPathShares).Returns(new List<LearningPathShare>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Chapters).Returns(new List<Chapter> { chapter }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Lessons).Returns(new List<Lesson> { lesson }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Tasks).Returns(new List<CodeNexus.Domain.Entities.Tasks>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Quizzes).Returns(new List<Quiz> { quiz }.BuildMockDbSet().Object);

        var result = await _handler.Handle(new SendLearningPathShareCommand(pathId, studentId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("CHAPTER_TASK_REQUIRED");
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_LessonWithoutQuiz_ReturnsFailure()
    {
        var mentorId = NewId.NextGuid();
        var studentId = NewId.NextGuid();
        var pathId = NewId.NextGuid();
        var chapterId = NewId.NextGuid();
        var lessonId = NewId.NextGuid();

        var mentor = new User
        {
            UserId = mentorId,
            Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Mentor" }
        };

        var student = new User
        {
            UserId = studentId,
            Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Student" }
        };

        var learningPath = new LearningPath
        {
            PathId = pathId,
            UserId = mentorId,
            SubjectId = NewId.NextGuid(),
            Title = "Path",
            Status = LearningPathStatus.Draft.ToString()
        };

        var chapter = new Chapter
        {
            ChapterId = chapterId,
            PathId = pathId,
            Title = "Chapter 1",
            OrderIndex = 1,
            IsDeleted = false
        };

        var lesson = new Lesson
        {
            LessonId = lessonId,
            ChapterId = chapterId,
            Title = "Lesson 1",
            Content = "Lesson content",
            OrderIndex = 1,
            LessonDay = DateTime.UtcNow,
            IsDeleted = false
        };

        var chapterTask = new CodeNexus.Domain.Entities.Tasks
        {
            TaskId = NewId.NextGuid(),
            ChapterId = chapterId,
            PathId = pathId,
            Title = "Practice task"
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(mentorId);
        _mockContext.Setup(x => x.Users).Returns(new List<User> { mentor, student }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new List<LearningPath> { learningPath }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPathShares).Returns(new List<LearningPathShare>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Chapters).Returns(new List<Chapter> { chapter }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Lessons).Returns(new List<Lesson> { lesson }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Tasks).Returns(new List<CodeNexus.Domain.Entities.Tasks> { chapterTask }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Quizzes).Returns(new List<Quiz>().BuildMockDbSet().Object);

        var result = await _handler.Handle(new SendLearningPathShareCommand(pathId, studentId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("LESSON_QUIZ_REQUIRED");
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ExistingPendingShare_ReturnsConflict()
    {
        var mentorId = NewId.NextGuid();
        var studentId = NewId.NextGuid();
        var pathId = NewId.NextGuid();

        var mentor = new User
        {
            UserId = mentorId,
            Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Mentor" }
        };

        var student = new User
        {
            UserId = studentId,
            Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Student" }
        };

        var existingShare = new LearningPathShare
        {
            ShareId = NewId.NextGuid(),
            PathId = pathId,
            MentorId = mentorId,
            StudentId = studentId,
            Status = LearningPathShareStatus.Pending,
            SentAt = DateTime.UtcNow
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(mentorId);
        _mockContext.Setup(x => x.Users).Returns(new List<User> { mentor, student }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new List<LearningPath>
        {
            new() { PathId = pathId, UserId = mentorId, SubjectId = NewId.NextGuid(), Title = "Path", Status = LearningPathStatus.Active.ToString() }
        }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPathShares).Returns(new List<LearningPathShare> { existingShare }.BuildMockDbSet().Object);

        var result = await _handler.Handle(new SendLearningPathShareCommand(pathId, studentId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("SHARE_ALREADY_PENDING");
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
