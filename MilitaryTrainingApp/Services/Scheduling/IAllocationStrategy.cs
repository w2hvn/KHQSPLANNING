using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MilitaryTrainingApp.Entities;
using MilitaryTrainingApp.Views;

namespace MilitaryTrainingApp.Services.Scheduling
{
    public interface IAllocationStrategy
    {
        Task<bool> ExecuteAsync(
            int planId,
            int planTargetId,
            TimeNode parentTimeNode,
            List<TimeTreeNodeItem> childTimeNodes,
            List<AllocationRowItem> gridRows,
            Action<ConflictLogItem> logAction,
            AppDbContext db,
            CancellationToken ct);
    }
}
