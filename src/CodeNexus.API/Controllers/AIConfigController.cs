using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.AIConfigs.Commands.CreateAIConfig;
using CodeNexus.Application.Features.AIConfigs.Commands.UpdateAIConfig;
using CodeNexus.Application.Features.AIConfigs.Commands.DeleteAIConfig;
using CodeNexus.Application.Features.AIConfigs.Commands.SetActiveConfig;
using CodeNexus.Application.Features.AIConfigs.DTOs;
using CodeNexus.Application.Features.AIConfigs.Queries.GetAllAIConfigs;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CodeNexus.API.Controllers
{
    [ApiController]
    [Route("api/admin/ai-configs")]
    [Authorize(Roles = "Admin")]
    public class AIConfigController : ControllerBase
    {
        private readonly ISender _sender;

        public AIConfigController(ISender sender)
        {
            _sender = sender;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllConfigs(CancellationToken cancellationToken)
        {
            var query = new GetAllAIConfigsQuery();
            var result = await _sender.Send(query, cancellationToken);

            return ToActionResult(result);
        }

        [HttpPost]
        public async Task<IActionResult> CreateAIConfig(CreateAIConfigRequest request, CancellationToken cancellationToken)
        {
            var command = new CreateAIConfigCommand(request.ProviderName, request.ApiKey, request.ConfigJson, request.AIUsageType, request.AccessTier, request.IsEnabel);

            var result = await _sender.Send(command, cancellationToken);

            return ToActionResult(result);
        }

        [HttpPut("{configId}")]
        public async Task<IActionResult> UpdateAIConfig(
            Guid configId,
            UpdateAIConfigRequest request,
            CancellationToken cancellationToken)
        {
            var command = new UpdateAIConfigCommand(
                configId,
                request.ProviderName,
                request.ApiKey,
                request.ConfigJson,
                request.IsActive,
                request.UsageType,
                request.AccessTier
            );

            var result = await _sender.Send(command, cancellationToken);

            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    "CONFIG_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
                    _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
                };
            }

            return Ok(result.Value);
        }

        [HttpDelete("{configId}")]
        public async Task<IActionResult> DeleteAIConfig(
            Guid configId,
            CancellationToken cancellationToken)
        {
            var command = new DeleteAIConfigCommand(configId);
            var result = await _sender.Send(command, cancellationToken);

            return ToActionResult(result);
        }

        [HttpPost("{configId}/set-active")]
        public async Task<IActionResult> SetActiveConfig(
            Guid configId,
            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] SetActiveConfigRequest? request,
            CancellationToken cancellationToken)
        {
            var command = new SetActiveConfigCommand(configId, request?.UsageType, request?.AccessTier);
            var result = await _sender.Send(command, cancellationToken);

            return ToActionResult(result);
        }

        private IActionResult ToActionResult(Result result)
        {
            if (result.IsSuccess)
                return Ok(new { message = "Operation completed successfully" });

            return result.ErrorCode switch
            {
                "CONFIG_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
                _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
            };
        }

        private IActionResult ToActionResult<T>(Result<T> result)
        {
            if (result.IsSuccess)
                return Ok(result);

            return result.ErrorCode switch
            {
                "CONFIG_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
                _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
            };
        }

    }
}
