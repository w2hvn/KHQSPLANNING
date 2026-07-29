using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MilitaryTrainingApp.Entities;
using MilitaryTrainingApp.Views;

namespace MilitaryTrainingApp.Services.Scheduling
{
    public class YearToStageStrategy : IAllocationStrategy
    {
        public async Task<bool> ExecuteAsync(int planId, int planTargetId, TimeNode parentTimeNode, List<TimeTreeNodeItem> childTimeNodes, List<AllocationRowItem> gridRows, Action<ConflictLogItem> logAction, AppDbContext db, CancellationToken ct)
        {
            // Tải toàn bộ cấu trúc bài học con để thực hiện Bottom-Up
            var allProgramNodes = await db.ProgramNodes.Where(pn => pn.PlanTargetId == planTargetId).ToListAsync();

            foreach (var row in gridRows.Where(r => r.Level == 1 && r.ParentMaxBudget > 0)) // Duyệt các Môn học (Level 1)
            {
                // Bottom-Up: Tính tỷ lệ Complexity từ các bài con (Leaf nodes)
                var leafNodes = GetLeafNodes(allProgramNodes, row.ProgramNodeId);

                decimal totalComp1 = leafNodes.Where(n => n.ComplexityLevel == 1).Sum(n => n.Capacity);
                decimal totalComp2 = leafNodes.Where(n => n.ComplexityLevel >= 2).Sum(n => n.Capacity);

                decimal totalLeafHours = totalComp1 + totalComp2;

                decimal ratioComp1 = totalLeafHours > 0 ? (totalComp1 / totalLeafHours) : 0.5m; // Mặc định 50/50 nếu trống
                decimal ratioComp2 = totalLeafHours > 0 ? (totalComp2 / totalLeafHours) : 0.5m;

                // Xóa dữ liệu cũ
                var dict = row.GetChildAllocations().Keys.ToList();
                foreach (var k in dict) row[k] = "";

                // Top-Down: Phân bổ ma trận. Giả định TimeNode có 2 nhánh chính (Nửa đầu / Nửa sau)
                if (childTimeNodes.Count >= 2)
                {
                    int halfIndex = childTimeNodes.Count / 2;
                    var earlyNodes = childTimeNodes.Take(halfIndex).ToList();
                    var lateNodes = childTimeNodes.Skip(halfIndex).ToList();

                    // Đổ ngân sách Comp1 vào các nút Early
                    decimal budgetComp1 = row.ParentMaxBudget * ratioComp1;
                    decimal hoursPerEarlyNode = earlyNodes.Any() ? budgetComp1 / earlyNodes.Count : 0;
                    foreach(var tn in earlyNodes)
                    {
                        row[tn.Id] = hoursPerEarlyNode.ToString("0.##");
                    }

                    // Đổ ngân sách Comp2 vào các nút Late
                    decimal budgetComp2 = row.ParentMaxBudget * ratioComp2;
                    decimal hoursPerLateNode = lateNodes.Any() ? budgetComp2 / lateNodes.Count : 0;
                    foreach(var tn in lateNodes)
                    {
                        row[tn.Id] = hoursPerLateNode.ToString("0.##");
                    }
                }
                else if (childTimeNodes.Count == 1)
                {
                    row[childTimeNodes.First().Id] = row.ParentMaxBudget.ToString("0.##");
                }
            }

            return true;
        }

        private List<ProgramNode> GetLeafNodes(List<ProgramNode> allNodes, int parentId)
        {
            var leaves = new List<ProgramNode>();
            var children = allNodes.Where(n => n.ParentId == parentId).ToList();
            if (!children.Any())
            {
                var self = allNodes.FirstOrDefault(n => n.Id == parentId);
                if (self != null) leaves.Add(self);
            }
            else
            {
                foreach(var c in children)
                {
                    leaves.AddRange(GetLeafNodes(allNodes, c.Id));
                }
            }
            return leaves;
        }
    }
}
