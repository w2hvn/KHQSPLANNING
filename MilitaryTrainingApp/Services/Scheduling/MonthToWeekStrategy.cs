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
    public class MonthToWeekStrategy : IAllocationStrategy
    {
        public async Task<bool> ExecuteAsync(int planId, int planTargetId, TimeNode parentTimeNode, List<TimeTreeNodeItem> childTimeNodes, List<AllocationRowItem> gridRows, Action<ConflictLogItem> logAction, AppDbContext db, CancellationToken ct)
        {
            var planTarget = await db.PlanTargets.FirstOrDefaultAsync(pt => pt.Id == planTargetId, ct);
            if (planTarget == null) return false;
            decimal maxWeeklyHours = planTarget.DaysPerWeek * (planTarget.MorningHours + planTarget.AfternoonHours);

            var blackouts = await db.BlackoutDates.Where(b => b.PlanId == planId).ToListAsync(ct);
            var rules = await db.SchedulingPriorityRules.Where(r => r.PlanId == planId && r.IsActive).ToListAsync(ct);
            int politicalScore = rules.FirstOrDefault(r => r.RuleCode == "RULE_POLITICAL_FIRST")?.PriorityScore ?? 0;
            int militaryScore = rules.FirstOrDefault(r => r.RuleCode == "RULE_MILITARY_CORE")?.PriorityScore ?? 0;
            int fatigueScore = rules.FirstOrDefault(r => r.RuleCode == "RULE_FATIGUE_BALANCING")?.PriorityScore ?? 0;

            var childIds = childTimeNodes.Select(c => c.Id).ToList();
            var childEntities = await db.TimeNodes.Where(tn => childIds.Contains(tn.Id)).ToListAsync(ct);

            var availableHours = new Dictionary<int, decimal>();
            foreach (var tn in childTimeNodes)
            {
                var entity = childEntities.FirstOrDefault(e => e.Id == tn.Id);
                decimal baseHours = maxWeeklyHours;
                if (entity != null && entity.StartDate.HasValue && entity.EndDate.HasValue)
                {
                    int overlapDays = 0;
                    foreach (var b in blackouts)
                    {
                        if (entity.StartDate.Value <= b.EndDate && entity.EndDate.Value >= b.StartDate)
                        {
                            var overlapStart = entity.StartDate.Value > b.StartDate ? entity.StartDate.Value : b.StartDate;
                            var overlapEnd = entity.EndDate.Value < b.EndDate ? entity.EndDate.Value : b.EndDate;
                            overlapDays += (overlapEnd - overlapStart).Days + 1;
                        }
                    }
                    baseHours -= (overlapDays * (planTarget.MorningHours + planTarget.AfternoonHours));
                    if (baseHours < 0) baseHours = 0;
                }
                availableHours[tn.Id] = baseHours;
            }

            foreach (var row in gridRows.Where(r => r.Level > 1))
            {
                foreach (var k in childTimeNodes) row[k.Id] = "";
            }

            var lastScheduledTimeIndexByParent = new Dictionary<int, int>();

            var rowsToSchedule = gridRows.Where(r => r.RemainingBudget > 0 && r.Level > 1)
                                         .OrderByDescending(r => GetBaseScore(r.ProgramCode, politicalScore, militaryScore, fatigueScore))
                                         .ToList();

            int totalItems = rowsToSchedule.Count;
            int currentIndex = 0;

            foreach (var row in rowsToSchedule)
            {
                currentIndex++;

                if (row.PrerequisiteNodeId.HasValue)
                {
                    var preReqRow = gridRows.FirstOrDefault(r => r.ProgramNodeId == row.PrerequisiteNodeId.Value);
                    if (preReqRow != null && preReqRow.RemainingBudget > 0)
                    {
                        logAction(new ConflictLogItem
                        {
                            ProgramNodeId = row.ProgramNodeId,
                            ProgramName = row.ProgramName,
                            Reason = $"Xung đột bài tiền đề '{preReqRow.ProgramName}' chưa xếp xong. Bỏ qua."
                        });
                        continue;
                    }
                }

                while (row.RemainingBudget > 0)
                {
                    ct.ThrowIfCancellationRequested();

                    var candidateSlots = availableHours.Where(kv => kv.Value > 0).ToList();
                    if (!candidateSlots.Any())
                    {
                        logAction(new ConflictLogItem { ProgramNodeId = row.ProgramNodeId, ProgramName = row.ProgramName, Reason = "Hết quỹ thời gian Tuần khả dụng để phân bổ." });
                        break;
                    }

                    var pnEntity = await db.ProgramNodes.FirstOrDefaultAsync(p => p.Id == row.ProgramNodeId, ct);
                    int minSlotIndex = 0;
                    if (pnEntity != null && pnEntity.ParentId.HasValue)
                    {
                        if (lastScheduledTimeIndexByParent.TryGetValue(pnEntity.ParentId.Value, out int lastIdx))
                        {
                            minSlotIndex = lastIdx;
                        }
                    }

                    var validSlots = new List<KeyValuePair<int, decimal>>();
                    for (int i = minSlotIndex; i < childTimeNodes.Count; i++)
                    {
                        var tNode = childTimeNodes[i];
                        if (availableHours[tNode.Id] > 0)
                        {
                            validSlots.Add(new KeyValuePair<int, decimal>(tNode.Id, availableHours[tNode.Id]));
                        }
                    }

                    if (!validSlots.Any())
                    {
                        logAction(new ConflictLogItem { ProgramNodeId = row.ProgramNodeId, ProgramName = row.ProgramName, Reason = "Bỏ qua Sequence Constraint do bó hẹp không gian trống." });
                        validSlots = candidateSlots;
                    }

                    var currentSlot = validSlots.First();
                    int currentSlotId = currentSlot.Key;
                    int slotIndex = childTimeNodes.FindIndex(c => c.Id == currentSlotId);

                    int score = GetBaseScore(row.ProgramCode, politicalScore, militaryScore, fatigueScore);

                    // Lấy các competitor chưa hoàn thành và loại trừ bản thân
                    var competitors = rowsToSchedule.Where(r => r.RemainingBudget > 0 && r.ProgramNodeId != row.ProgramNodeId).ToList();
                    var sameScoreCompetitors = competitors.Where(c => GetBaseScore(c.ProgramCode, politicalScore, militaryScore, fatigueScore) == score).ToList();

                    // PHÂN VÂN VI MÔ: Rất nhiều môn cùng điểm ưu tiên đòi giành Slot hiện tại
                    // => Áp dụng luật Resolver
                    if (sameScoreCompetitors.Any() && childTimeNodes.Count >= 2)
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
                                tcs.SetResult(3);
                            }
                        });

                        int decision = await tcs.Task;
                        row.IsHighlight = false;

                        if (decision == 1)
                        {
                            // Ép toàn bộ giờ còn lại vào Slot đầu tiên (nếu chứa được)
                            // (Mặc định vòng while dưới đây sẽ đổ cho đến khi đầy slot hiện tại)
                        }
                        else if (decision == 2)
                        {
                            // Trải đều ra tất cả các slot còn lại thay vì nhồi nhét
                            decimal distributeVal = row.RemainingBudget / validSlots.Count;
                            foreach(var slot in validSlots)
                            {
                                decimal toAlloc = Math.Min(distributeVal, slot.Value);
                                decimal currentHas = 0;
                                if (row.GetChildAllocations().ContainsKey(slot.Key)) currentHas = row.GetChildAllocations()[slot.Key];

                                row[slot.Key] = (currentHas + toAlloc).ToString("0.##");
                                availableHours[slot.Key] -= toAlloc;
                            }

                            if (pnEntity != null && pnEntity.ParentId.HasValue)
                            {
                                lastScheduledTimeIndexByParent[pnEntity.ParentId.Value] = childTimeNodes.Count - 1; // đã dải ra cuối
                            }
                            break; // Xong vòng while cho row này
                        }
                        else if (decision == 3)
                        {
                            logAction(new ConflictLogItem { ProgramNodeId = row.ProgramNodeId, ProgramName = row.ProgramName, Reason = "Chỉ huy chọn tự nhập số giờ thủ công. Đã Skip." });
                            break;
                        }
                    }

                    decimal allocateAmount = Math.Min(4m, Math.Min(row.RemainingBudget, currentSlot.Value));

                    decimal existing = 0;
                    var childDict = row.GetChildAllocations();
                    if (childDict.ContainsKey(currentSlotId)) existing = childDict[currentSlotId];

                    row[currentSlotId] = (existing + allocateAmount).ToString("0.##");
                    availableHours[currentSlotId] -= allocateAmount;

                    if (pnEntity != null && pnEntity.ParentId.HasValue)
                    {
                        lastScheduledTimeIndexByParent[pnEntity.ParentId.Value] = slotIndex;
                    }
                }
            }

            return true;
        }

        private int GetBaseScore(string? code, int pScore, int mScore, int fScore)
        {
            int score = 0;
            if (code != null)
            {
                if (code.StartsWith("CT", StringComparison.OrdinalIgnoreCase)) score += pScore;
                if (code.StartsWith("QS", StringComparison.OrdinalIgnoreCase) || code.StartsWith("ĐL", StringComparison.OrdinalIgnoreCase)) score += mScore;
                if (code.StartsWith("TL", StringComparison.OrdinalIgnoreCase) || code.StartsWith("SK", StringComparison.OrdinalIgnoreCase)) score += fScore;
            }
            return score;
        }
    }
}
