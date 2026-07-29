using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using MilitaryTrainingApp.Entities;
using MilitaryTrainingApp.Views;

namespace MilitaryTrainingApp.Helpers
{
    public class AutoScheduleEngine
    {
        public static void RunEngine(
            int currentPlanId,
            List<TimeTreeNodeItem> childTimeNodes,
            List<AllocationRowItem> rowItems,
            List<TimeNode> childTimeNodeEntities,
            AppDbContext db)
        {
            if (!childTimeNodes.Any() || !rowItems.Any())
            {
                MessageBox.Show("Không có dữ liệu hợp lệ để chạy xếp lịch.", "Lỗi Engine", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 1. Tải cấu hình ngày nghỉ lễ (Blackout Dates)
            var blackouts = db.BlackoutDates.Where(b => b.PlanId == currentPlanId).ToList();

            // 2. Tải quy tắc ưu tiên (Priority Rules)
            var rules = db.SchedulingPriorityRules.Where(r => r.PlanId == currentPlanId && r.IsActive).ToList();
            int politicalScore = rules.FirstOrDefault(r => r.RuleCode == "RULE_POLITICAL_FIRST")?.PriorityScore ?? 0;
            int militaryScore = rules.FirstOrDefault(r => r.RuleCode == "RULE_MILITARY_CORE")?.PriorityScore ?? 0;
            int fatigueScore = rules.FirstOrDefault(r => r.RuleCode == "RULE_FATIGUE_BALANCING")?.PriorityScore ?? 0;

            // 3. Khởi tạo / Tính toán năng lực thực tế của từng TimeNode con (sau khi trừ nghỉ lễ)
            // Giả định mỗi tuần chuẩn (WEEK) có 40 giờ huấn luyện. Tháng/Giai đoạn tùy thuộc cấp.
            // Ở đây đơn giản hóa: chia đều dung lượng còn lại, hoặc lấp đầy dần từ trái qua phải.
            var availableHours = new Dictionary<int, decimal>();
            foreach (var tn in childTimeNodes)
            {
                var entity = childTimeNodeEntities.FirstOrDefault(e => e.Id == tn.Id);
                decimal baseHours = 40; // Giả định cấp Tuần là 40h/tuần

                if (entity != null && entity.StartDate.HasValue && entity.EndDate.HasValue)
                {
                    // Lọc khung khả dụng (Trừ ngày nghỉ)
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
                    baseHours -= (overlapDays * 8); // Trừ 8h cho mỗi ngày nghỉ
                    if (baseHours < 0) baseHours = 0;
                }

                // Nếu TimeNode này chưa có giá trị, gán default = 40h
                availableHours[tn.Id] = baseHours;
            }

            // 4. Xóa kết quả phân bổ cũ (reset lưới) trên giao diện trước khi tự động chia lại
            foreach (var row in rowItems)
            {
                var currentDict = row.GetChildAllocations().Keys.ToList();
                foreach (var key in currentDict)
                {
                    row[key] = ""; // Xóa thành 0/empty
                }
            }

            // 5. Gán ô Cố định - Hard Rules
            foreach (var row in rowItems)
            {
                if (row.ProgramCode != null)
                {
                    if (row.ProgramCode.StartsWith("CC", StringComparison.OrdinalIgnoreCase))
                    {
                        // Chào cờ -> Luôn 2h vào slot đầu tiên khả dụng
                        var firstSlot = availableHours.FirstOrDefault(kv => kv.Value >= 2m);
                        if (firstSlot.Key > 0)
                        {
                            row[firstSlot.Key] = "2";
                            availableHours[firstSlot.Key] -= 2;
                        }
                    }
                    else if (row.ProgramCode.StartsWith("CTVHTT", StringComparison.OrdinalIgnoreCase))
                    {
                        // CTVHTT -> Xếp 2h vào slot phù hợp (VD: giữa khoảng)
                        var midSlot = availableHours.Skip(availableHours.Count / 2).FirstOrDefault(kv => kv.Value >= 2m);
                        if (midSlot.Key == 0) midSlot = availableHours.FirstOrDefault(kv => kv.Value >= 2m); // Fallback

                        if (midSlot.Key > 0)
                        {
                            row[midSlot.Key] = "2";
                            availableHours[midSlot.Key] -= 2;
                        }
                    }
                }
            }

            // 6. Xếp lịch động (Heuristic Soft Rules)
            // Cố gắng chia nốt RemainingBudget của mỗi Row vào các TimeNodes.
            var rowsToSchedule = rowItems
                .Where(r => r.RemainingBudget > 0 && r.Level > 1) // Chỉ xếp cho các node con (Bài học), không xếp thẳng lên Môn
                .ToList();

            while (rowsToSchedule.Any(r => r.RemainingBudget > 0))
            {
                // Tìm TimeNode con đầu tiên còn giờ trống
                var currentSlot = availableHours.FirstOrDefault(kv => kv.Value > 0);
                if (currentSlot.Key == 0)
                {
                    MessageBox.Show("Đã hết quỹ thời gian khả dụng để chia!", "Hết giờ", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
                }

                // Chấm điểm cho các bài học đang chờ
                var candidateScores = new Dictionary<AllocationRowItem, int>();
                foreach (var row in rowsToSchedule.Where(r => r.RemainingBudget > 0))
                {
                    int score = 0;
                    if (row.ProgramCode != null)
                    {
                        if (row.ProgramCode.StartsWith("CT", StringComparison.OrdinalIgnoreCase)) score += politicalScore;
                        if (row.ProgramCode.StartsWith("QS", StringComparison.OrdinalIgnoreCase) || row.ProgramCode.StartsWith("ĐL", StringComparison.OrdinalIgnoreCase)) score += militaryScore;
                        if (row.ProgramCode.StartsWith("TL", StringComparison.OrdinalIgnoreCase) || row.ProgramCode.StartsWith("SK", StringComparison.OrdinalIgnoreCase)) score += fatigueScore;
                    }
                    // Ưu tiên các môn có khối lượng còn lại lớn (Greedy)
                    score += (int)row.RemainingBudget;

                    candidateScores[row] = score;
                }

                if (!candidateScores.Any()) break;

                // Chọn người chiến thắng
                int maxScore = candidateScores.Max(kv => kv.Value);
                var topCandidates = candidateScores.Where(kv => kv.Value == maxScore).Select(kv => kv.Key).ToList();

                AllocationRowItem winner;

                if (topCandidates.Count > 1)
                {
                    // PAUSE ON CONFLICT: Mở Cửa sổ giải quyết xung đột
                    List<ProgramNode> conflictNodes = topCandidates.Select(r => new ProgramNode { Id = r.ProgramNodeId, Code = r.ProgramCode, Name = r.ProgramName }).ToList();

                    var timeNodeName = childTimeNodes.First(t => t.Id == currentSlot.Key).Name;
                    var resolver = new ConflictResolverWindow(conflictNodes, timeNodeName);

                    if (resolver.ShowDialog() == true && resolver.SelectedNode != null)
                    {
                        winner = topCandidates.First(r => r.ProgramNodeId == resolver.SelectedNode.Id);
                    }
                    else
                    {
                        // Nếu tắt ngang -> Chọn random/đầu tiên
                        winner = topCandidates.First();
                    }
                }
                else
                {
                    winner = topCandidates.First();
                }

                // Gán giờ cho Winner (Tối đa 4h mỗi lần xếp cho 1 slot để chia đều, hoặc lấp đầy)
                decimal allocateAmount = Math.Min(4m, Math.Min(winner.RemainingBudget, currentSlot.Value));

                // Lấy giá trị cũ trong ô này cộng thêm
                decimal existing = 0;
                var childDict = winner.GetChildAllocations();
                if (childDict.ContainsKey(currentSlot.Key)) existing = childDict[currentSlot.Key];

                winner[currentSlot.Key] = (existing + allocateAmount).ToString("0.##");

                // Trừ ngân sách của Slot
                availableHours[currentSlot.Key] -= allocateAmount;
            }

            MessageBox.Show("Hoàn tất chạy Thuật toán Xếp lịch Tự động!", "Auto-Schedule Engine", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
