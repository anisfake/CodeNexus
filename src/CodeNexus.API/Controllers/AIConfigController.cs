using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.AIConfigs.Commands.CreateAIConfig;
using CodeNexus.Application.Features.AIConfigs.Commands.UpdateAIConfig;
using CodeNexus.Application.Features.AIConfigs.Commands.DeleteAIConfig;
using CodeNexus.Application.Features.AIConfigs.DTOs;
using CodeNexus.Application.Features.AIConfigs.Queries.GetAllAIConfigs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
            var command = new CreateAIConfigCommand(request.ProviderName, request.ApiKey, request.ConfigJson, request.AIUsageType, request.IsEnabel);

            var result = await _sender.Send(command, cancellationToken);

            return ToActionResult(result);
        }

        [HttpPut("{providerName}")]
        public async Task<IActionResult> UpdateAIConfig(
            string providerName,
            UpdateAIConfigRequest request,
            CancellationToken cancellationToken)
        {
            var command = new UpdateAIConfigCommand(
                providerName,
                request.ApiKey,
                request.ConfigJson,
                request.IsEnabled
            );

            var result = await _sender.Send(command, cancellationToken);

            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    "PROVIDER_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
                    _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
                };
            }

            return Ok(result.Value);
        }

        [HttpDelete("{providerName}")]
        public async Task<IActionResult> DeleteAIConfig(
            string providerName,
            CancellationToken cancellationToken)
        {
            var command = new DeleteAIConfigCommand(providerName);
            var result = await _sender.Send(command, cancellationToken);

            return ToActionResult(result);
        }
        private IActionResult ToActionResult(Result result)
        {
            if (result.IsSuccess)
                return Ok(new { message = "Operation completed successfully" });

            return result.ErrorCode switch
            {
                "PROVIDER_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
                _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
            };
        }

        private IActionResult ToActionResult<T>(Result<T> result)
        {
            if (result.IsSuccess)
                return Ok(result);

            return result.ErrorCode switch
            {
                "PROVIDER_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
                _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
            };
        }
    }
}
