using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.AIConfigs.Commands.CreateAIConfig;
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

            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> CreateAIConfig(CreateAIConfigRequest request, CancellationToken cancellationToken)
        {
            var command = new CreateAIConfigCommand(request.ProviderName, request.ApiKey, request.ConfigJson, request.IsEnabel);

            var result = await _sender.Send(command, cancellationToken);

            return Ok(result);
        }
    }
}
