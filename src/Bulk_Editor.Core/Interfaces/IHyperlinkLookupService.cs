using System.Threading.Tasks;
using Doc_Helper.Core.Models;

namespace Doc_Helper.Core.Interfaces
{
    public interface IHyperlinkLookupService
    {
        Task<DatabaseHealthResult> CheckDatabaseHealthAsync();
        Task<OptimizationResult> OptimizePerformanceAsync();
        LookupPerformanceStats GetPerformanceStats();
    }
}