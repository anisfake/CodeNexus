using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Queries.GetAllLearningPaths
{
    public class GetAllLearningPathQueryHandler : IRequestHandler<GetAllLearningPathQuery, Result<List<LearningPathResponse>>>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        public GetAllLearningPathQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }
        public async Task<Result<List<LearningPathResponse>>> Handle(GetAllLearningPathQuery request, CancellationToken cancellationToken)
        {
            var userId = _currentUserService.GetUserId();

            var user = _context.Users.Include(x => x.Role).FirstOrDefault(u => u.UserId == userId);

            if (user == null)
            {
                return Result<List<LearningPathResponse>>.Failure("USER_NOT_FOUND", "User not found.");
            }

            if (user.Role?.RoleName != "Mentor")
            {
                return Result<List<LearningPathResponse>>.Failure("ACCESS_DENIED", "Only mentors can access learning paths.");
            }

            var learningPaths = _context.LearningPaths
                .Include(lp => lp.Subject)
                .Include(lp => lp.Goal)
                .Include(lp => lp.User)
                .Include(lp => lp.Chapters).ThenInclude(c => c.Lessons).ThenInclude(l => l.Quizzes)
                .Include(lp => lp.Chapters).ThenInclude(c => c.Tasks)
                .Select(lp => new LearningPathResponse(
                lp.PathId,
                lp.SubjectId,
                lp.Subject.Name,
                lp.Goal.GoalId,
                lp.Goal.Title,
                lp.StartDate,
                lp.EndDate,
                lp.Title,
                lp.Description,
                lp.Status.ToString(),
                lp.CreatedByType,
                lp.UserId,
                lp.User.Username,
                lp.Chapters.Select(c => new ChapterDto(
                    c.ChapterId,
                    c.Title,
                    c.Content,
                    c.OrderIndex,
                    c.Lessons.Select(l => new LessonDto(
                        l.LessonId,
                        l.Title,
                        l.Content,
                        l.Quizzes.Select(q => new QuizDto(
                            q.QuizId,
                            q.Title,
                            q.Description
                        )).ToList()
                    )).ToList(),
                    c.Tasks.Select(t => new TaskDto(
                        t.TaskId,
                        t.Title,
                        t.Description
                    )).ToList()
                )).ToList(),
                lp.Chapters.Count(),
                lp.CreatedAt
            )).ToList();

            return Result<List<LearningPathResponse>>.Success(learningPaths);
        }
    }
}
