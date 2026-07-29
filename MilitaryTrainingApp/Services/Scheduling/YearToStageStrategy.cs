using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using MilitaryTrainingApp.Entities;
using MilitaryTrainingApp.Views;

namespace MilitaryTrainingApp.Services.Scheduling
{
    public class YearToStageStrategy : IAllocationStrategy
    {
        public async Task<bool> ExecuteAsync(int planId, int planTargetId, TimeNode parentTimeNode, List<TimeTreeNodeItem> childTimeNodes, List<AllocationRowItem> gridRows, Action<ConflictLogItem> logAction, AppDbContext db, CancellationToken ct)
        {
            var allProgramNodes = await db.ProgramNodes.Where(pn => pn.PlanTargetId == planTargetId).ToListAsync(ct);

            var rowsToSchedule = gridRows.Where(r => r.Level == 1 && r.ParentMaxBudget > 0).ToList();
            int totalItems = rowsToSchedule.Count;
            int currentIndex = 0;

            foreach (var row in rowsToSchedule)
            {
                currentIndex++;
                ct.ThrowIfCancellationRequested();

                var leafNodes = GetLeafNodes(allProgramNodes, row.ProgramNodeId);

                decimal totalComp1 = leafNodes.Where(n => n.ComplexityLevel == 1).Sum(n => n.Capacity);
                decimal totalComp2 = leafNodes.Where(n => n.ComplexityLevel >= 2).Sum(n => n.Capacity);

                decimal totalLeafHours = totalComp1 + totalComp2;

                decimal ratioComp1 = totalLeafHours > 0 ? (totalComp1 / totalLeafHours) : 0.5m;
                decimal ratioComp2 = totalLeafHours > 0 ? (totalComp2 / totalLeafHours) : 0.5m;

                var dict = row.GetChildAllocations().Keys.ToList();
                foreach (var k in dict) row[k] = ""; // Xóa dữ liệu cũ

                if (childTimeNodes.Count >= 2)
                {
                    int halfIndex = childTimeNodes.Count / 2;
                    var earlyNodes = childTimeNodes.Take(halfIndex).ToList();
                    var lateNodes = childTimeNodes.Skip(halfIndex).ToList();

                    // TÌNH HUỐNG PHÂN VÂN: Tỷ lệ bằng nhau (50-50) và có đủ không gian thời gian (đều là 2 mốc)
                    if (ratioComp1 == 0.5m && ratioComp2 == 0.5m && earlyNodes.Count > 0 && lateNodes.Count > 0)
                    {
                        row.IsHighlight = true;

                        var tcs = new TaskCompletionSource<int>();

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            var resolver = new ConflictResolverWindow(row.ProgramName, row.ParentMaxBudget, currentIndex, totalItems);
                            if (resolver.ShowDialog() == true)
                            {
                                tcs.SetResult(resolver.SelectedOption);
                            }
                            else
                            {
                                tcs.SetResult(3); // Mặc định bỏ qua nếu đóng cửa sổ
                            }
                        });

                        int decision = await tcs.Task;
                        row.IsHighlight = false;

                        if (decision == 1)
                        {
                            // Option 1: 100% vào nửa đầu
                            ratioComp1 = 1.0m;
                            ratioComp2 = 0.0m;
                        }
                        else if (decision == 3)
                        {
                            // Option 3: Bỏ qua (Skip) -> Ghi log và tiếp tục
                            logAction(new ConflictLogItem { ProgramNodeId = row.ProgramNodeId, ProgramName = row.ProgramName, Reason = "Chỉ huy chọn tự nhập số giờ (Bỏ qua Auto)." });
                            continue;
                        }
                        // Option 2: Giữ nguyên 50-50
                    }

                    decimal budgetComp1 = row.ParentMaxBudget * ratioComp1;
                    decimal hoursPerEarlyNode = earlyNodes.Any() ? budgetComp1 / earlyNodes.Count : 0;
                    foreach(var tn in earlyNodes)
                    {
                        if (hoursPerEarlyNode > 0) row[tn.Id] = hoursPerEarlyNode.ToString("0.##");
                    }

                    decimal budgetComp2 = row.ParentMaxBudget * ratioComp2;
                    decimal hoursPerLateNode = lateNodes.Any() ? budgetComp2 / lateNodes.Count : 0;
                    foreach(var tn in lateNodes)
                    {
                        if (hoursPerLateNode > 0) row[tn.Id] = hoursPerLateNode.ToString("0.##");
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
