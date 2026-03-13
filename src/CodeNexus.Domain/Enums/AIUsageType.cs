namespace CodeNexus.Domain.Enums;

public enum AIUsageType
{
    StructureGeneration = 1,  
    ContentGeneration = 2,    
    Verification = 3,         // Verify task completion, validate goal
    Assistant = 4             // Chat bot, resource summary, general Q&A
}
