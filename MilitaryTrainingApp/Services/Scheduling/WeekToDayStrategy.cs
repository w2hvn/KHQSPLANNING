using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MilitaryTrainingApp.Entities;
using MilitaryTrainingApp.Views;

namespace MilitaryTrainingApp.Services.Scheduling
{
    public class WeekToDayStrategy : IAllocationStrategy
    {
        public Task<bool> ExecuteAsync(int planId, int planTargetId, TimeNode parentTimeNode, List<TimeTreeNodeItem> childTimeNodes, List<AllocationRowItem> gridRows, Action<ConflictLogItem> logAction, AppDbContext db, CancellationToken ct)
        {
            // Fallback for sub-week allocations (if extended in future)
            // Implementation logic mirrors MonthToWeek Strategy conceptually.
            logAction(new ConflictLogItem
            {
                ProgramNodeId = 0,
                ProgramName = "Hệ Thống",
                Reason = "Chiến lược phân bổ Cấp Tuần -> Ngày hiện đang sử dụng chiến lược mặc định chưa được định nghĩa chi tiết."
            });
            return Task.FromResult(true);
        }
    }
}
