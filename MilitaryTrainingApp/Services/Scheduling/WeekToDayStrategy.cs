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
            // Fallback: Currently the system time_nodes only expand downwards to WEEK level.
            // Day/Session operations function virtually within grid extensions out-of-scope for the recursive node-based hierarchy.
            // Logging warning.
            logAction(new ConflictLogItem
            {
                ProgramNodeId = 0,
                ProgramName = "Hệ Thống",
                Reason = "Cấp phân bổ Tuần -> Ngày đang ở chế độ Fallback, do cấu trúc CSDL không hạ đến DAY. Hãy xếp lịch theo cấp Tháng -> Tuần."
            });
            return Task.FromResult(true);
        }
    }
}
