using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Application.Common.Interfaces
{
    public interface IAIGeneratorService
    {
        Task<T> GenerateStructureAsync<T>(string prompt);
    }
}
