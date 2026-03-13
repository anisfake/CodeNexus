using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Common.Interfaces
{
    public interface IAIGeneratorService
    {
        Task<T> GenerateStructureAsync<T>(string prompt, AIUsageType usageType = AIUsageType.StructureGeneration);
        Task<string> GenerateContentAsync(string prompt, AIUsageType usageType = AIUsageType.StructureGeneration);
    }
}
