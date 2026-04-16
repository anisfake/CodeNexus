using System.Reflection;
using CodeNexus.Application.Common.Behaviors;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.LearningPathShares.Services;
using CodeNexus.Application.Features.Users.Services;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace CodeNexus.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        services.AddScoped<ILearningPathSharePathSyncService, LearningPathSharePathSyncService>();
        services.AddScoped<IDailyReminderTimeInferenceService, DailyReminderTimeInferenceService>();
        
        // Add validation behavior
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }
}
