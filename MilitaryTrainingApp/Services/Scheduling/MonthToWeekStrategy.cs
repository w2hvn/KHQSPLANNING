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
    public class MonthToWeekStrategy : IAllocationStrategy
    {
        public async Task<bool> ExecuteAsync(int planId, int planTargetId, TimeNode parentTimeNode, List<TimeTreeNodeItem> childTimeNodes, List<AllocationRowItem> gridRows, Action<ConflictLogItem> logAction, AppDbContext db, CancellationToken ct)
        {
            // 1. Tải cấu hình ngày nghỉ lễ (Blackout Dates)
            var blackouts = await db.BlackoutDates.Where(b => b.PlanId == planId).ToListAsync(ct);

            // 2. Tải quy tắc ưu tiên
            var rules = await db.SchedulingPriorityRules.Where(r => r.PlanId == planId && r.IsActive).ToListAsync(ct);
            int politicalScore = rules.FirstOrDefault(r => r.RuleCode == "RULE_POLITICAL_FIRST")?.PriorityScore ?? 0;
            int militaryScore = rules.FirstOrDefault(r => r.RuleCode == "RULE_MILITARY_CORE")?.PriorityScore ?? 0;
            int fatigueScore = rules.FirstOrDefault(r => r.RuleCode == "RULE_FATIGUE_BALANCING")?.PriorityScore ?? 0;

            // Lấy thực thể TimeNode con để có StartDate/EndDate
            var childIds = childTimeNodes.Select(c => c.Id).ToList();
            var childEntities = await db.TimeNodes.Where(tn => childIds.Contains(tn.Id)).ToListAsync(ct);

            // 3. Tính quỹ thời gian khả dụng
            var availableHours = new Dictionary<int, decimal>();
            foreach (var tn in childTimeNodes)
            {
                var entity = childEntities.FirstOrDefault(e => e.Id == tn.Id);
                decimal baseHours = 40; // Giả định cấp Tuần là 40h/tuần
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
                    baseHours -= (overlapDays * 8);
                    if (baseHours < 0) baseHours = 0;
                }
                availableHours[tn.Id] = baseHours;
            }

            // Xóa dữ liệu cũ trên lưới (các ô của node con)
            foreach (var row in gridRows.Where(r => r.Level > 1)) // Chỉ xóa bài học, giữ nguyên Môn học
            {
                foreach (var k in childTimeNodes) row[k.Id] = "";
            }

            // Ghi nhận trạng thái duyệt (Index cột thời gian tối đa mà Bài/Nhánh đó đã xếp tới)
            var lastScheduledTimeIndexByParent = new Dictionary<int, int>();

            var rowsToSchedule = gridRows.Where(r => r.RemainingBudget > 0 && r.Level > 1).ToList();

            foreach (var row in rowsToSchedule)
            {
                // RÀNG BUỘC TIỀN ĐỀ: Kiểm tra Prerequisite Node đã được xếp chưa?
                if (row.PrerequisiteNodeId.HasValue)
                {
                    var preReqRow = gridRows.FirstOrDefault(r => r.ProgramNodeId == row.PrerequisiteNodeId.Value);
                    if (preReqRow != null && preReqRow.RemainingBudget > 0) // Tiền đề chưa được xếp xong
                    {
                        logAction(new ConflictLogItem
                        {
                            ProgramNodeId = row.ProgramNodeId,
                            ProgramName = row.ProgramName,
                            Reason = $"Xung đột bài tiền đề '{preReqRow.ProgramName}' chưa được xếp lịch xong. Bỏ qua."
                        });
                        continue;
                    }
                }

                while (row.RemainingBudget > 0)
                {
                    // Lấy các Slot còn quỹ thời gian
                    var candidateSlots = availableHours.Where(kv => kv.Value > 0).ToList();
                    if (!candidateSlots.Any())
                    {
                        logAction(new ConflictLogItem { ProgramNodeId = row.ProgramNodeId, ProgramName = row.ProgramName, Reason = "Hết quỹ thời gian khả dụng để phân bổ." });
                        break;
                    }

                    // SEQUENTIAL CONSTRAINT: Cùng cha thì bài sau phải xếp cùng hoặc sau bài trước
                    // Lấy cha của dòng hiện tại (giả định EF query ProgramNode)
                    var pnEntity = await db.ProgramNodes.FirstOrDefaultAsync(p => p.Id == row.ProgramNodeId, ct);
                    int minSlotIndex = 0;
                    if (pnEntity != null && pnEntity.ParentId.HasValue)
                    {
                        if (lastScheduledTimeIndexByParent.TryGetValue(pnEntity.ParentId.Value, out int lastIdx))
                        {
                            minSlotIndex = lastIdx;
                        }
                    }

                    // Tìm danh sách slot thỏa mãn Sequence
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
                        // Fallback: Nếu bó buộc quá mức, thả lỏng sequential nhưng cảnh báo log
                        logAction(new ConflictLogItem { ProgramNodeId = row.ProgramNodeId, ProgramName = row.ProgramName, Reason = "Cảnh báo: Bỏ qua Sequence do bó hẹp." });
                        validSlots = candidateSlots;
                    }

                    // Tạm dừng (PAUSE ON CONFLICT) nếu có sự cạnh tranh - Ở bài toán này ta xét tranh chấp khi chọn Slots
                    var currentSlot = validSlots.First();
                    int currentSlotId = currentSlot.Key;
                    int slotIndex = childTimeNodes.FindIndex(c => c.Id == currentSlotId);

                    // Giải lập điểm
                    int score = 0;
                    if (row.ProgramCode != null)
                    {
                        if (row.ProgramCode.StartsWith("CT", StringComparison.OrdinalIgnoreCase)) score += politicalScore;
                        if (row.ProgramCode.StartsWith("QS", StringComparison.OrdinalIgnoreCase) || row.ProgramCode.StartsWith("ĐL", StringComparison.OrdinalIgnoreCase)) score += militaryScore;
                        if (row.ProgramCode.StartsWith("TL", StringComparison.OrdinalIgnoreCase) || row.ProgramCode.StartsWith("SK", StringComparison.OrdinalIgnoreCase)) score += fatigueScore;
                    }

                    // Nếu có nhiều môn đang khao khát cùng 1 khung giờ đầu tiên này
                    var competitors = rowsToSchedule.Where(r => r.RemainingBudget > 0 && r.ProgramNodeId != row.ProgramNodeId).ToList();
                    var sameScoreCompetitors = competitors.Where(c =>
                    {
                        int cScore = 0;
                        if (c.ProgramCode != null)
                        {
                            if (c.ProgramCode.StartsWith("CT", StringComparison.OrdinalIgnoreCase)) cScore += politicalScore;
                            if (c.ProgramCode.StartsWith("QS", StringComparison.OrdinalIgnoreCase) || c.ProgramCode.StartsWith("ĐL", StringComparison.OrdinalIgnoreCase)) cScore += militaryScore;
                            if (c.ProgramCode.StartsWith("TL", StringComparison.OrdinalIgnoreCase) || c.ProgramCode.StartsWith("SK", StringComparison.OrdinalIgnoreCase)) cScore += fatigueScore;
                        }
                        return cScore == score;
                    }).ToList();

                    if (sameScoreCompetitors.Any())
                    {
                        row.IsHighlight = true;

                        // Khởi tạo TaskCompletionSource để Pause UI thread
                        var tcs = new TaskCompletionSource<int>();

                        List<ProgramNode> conflictNodes = new List<ProgramNode> { new ProgramNode { Id = row.ProgramNodeId, Code = row.ProgramCode, Name = row.ProgramName } };
                        conflictNodes.AddRange(sameScoreCompetitors.Take(2).Select(c => new ProgramNode { Id = c.ProgramNodeId, Code = c.ProgramCode, Name = c.ProgramName }));

                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            var timeNodeName = childTimeNodes.First(t => t.Id == currentSlotId).Name;
                            var resolver = new ConflictResolverWindow(conflictNodes, timeNodeName);
                            if (resolver.ShowDialog() == true && resolver.SelectedNode != null)
                            {
                                // Option 1/2: Chọn 1 thằng
                                tcs.SetResult(resolver.SelectedNode.Id);
                            }
                            else
                            {
                                // Option 3 (Tắt ngang): Tự nhập thủ công -> Skip dòng này
                                tcs.SetResult(0);
                            }
                        });

                        int decision = await tcs.Task;
                        row.IsHighlight = false;

                        if (decision == 0)
                        {
                            logAction(new ConflictLogItem { ProgramNodeId = row.ProgramNodeId, ProgramName = row.ProgramName, Reason = "Chỉ huy chọn xử lý thủ công." });
                            break; // Dừng vòng lặp while của row này (Skip)
                        }
                        else if (decision != row.ProgramNodeId)
                        {
                            // Nhường slot cho competitor, tạm dừng row này lại để vòng while quét lại sau
                            break;
                        }
                    }

                    decimal allocateAmount = Math.Min(4m, Math.Min(row.RemainingBudget, currentSlot.Value));

                    decimal existing = 0;
                    var childDict = row.GetChildAllocations();
                    if (childDict.ContainsKey(currentSlotId)) existing = childDict[currentSlotId];

                    row[currentSlotId] = (existing + allocateAmount).ToString("0.##");
                    availableHours[currentSlotId] -= allocateAmount;

                    // Ghi nhận tiến trình sequence
                    if (pnEntity != null && pnEntity.ParentId.HasValue)
                    {
                        lastScheduledTimeIndexByParent[pnEntity.ParentId.Value] = slotIndex;
                    }
                }
            }

            return true;
        }
    }
}
