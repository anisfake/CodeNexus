using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.LearningPathMentorReviews.Commands.SendMentorReviewReminder;

public class SendMentorReviewReminderCommandHandler : IRequestHandler<SendMentorReviewReminderCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailService _emailService;

    public SendMentorReviewReminderCommandHandler(IApplicationDbContext context, IEmailService emailService)
    {
        _context = context;
        _emailService = emailService;
    }

    public async Task<Result> Handle(SendMentorReviewReminderCommand request, CancellationToken cancellationToken)
    {
        var review = await _context.LearningPathMentorReviews
            .Include(r => r.Student)
            .Include(r => r.Mentor)
            .Include(r => r.LearningPath)
            .FirstOrDefaultAsync(r => r.ReviewId == request.ReviewId, cancellationToken);

        if (review == null)
            return Result.Failure("REVIEW_NOT_FOUND", "Mentor review not found.");

        if (review.DecisionStatus != LearningPathMentorReviewDecisionStatus.WaitingStudentResponse)
            return Result.Failure("INVALID_STATUS", "Review is not in WaitingStudentResponse status.");

        var studentName = review.Student.FirstName ?? review.Student.Username;
        var mentorName = review.Mentor.FirstName ?? review.Mentor.Username;
        var pathTitle = review.LearningPath.Title;

        var subject = "Nhắc nhở: Lộ trình học của bạn đang chờ phản hồi";
        var message = $"Xin chào {studentName},\n\n" +
                      $"Mentor {mentorName} đã hoàn thành review lộ trình \"{pathTitle}\" của bạn và đang chờ bạn phản hồi.\n\n" +
                      $"Vui lòng đăng nhập vào hệ thống để xem nhận xét và chấp nhận hoặc từ chối đề xuất của mentor.\n\n" +
                      $"Trân trọng,\nCodeNexus";

        await _emailService.SendNotificationEmailAsync(review.Student.Email, subject, message, cancellationToken);

        return Result.Success();
    }
}
