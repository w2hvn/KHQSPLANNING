using System;
using System.Collections.Generic;
using System.Linq;
using MilitaryTrainingApp.Entities;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace MilitaryTrainingApp.Helpers
{
    public class StageConfig
    {
        public string StageCode { get; set; } = null!;
        public string StageName { get; set; } = null!;
        public List<int> Months { get; set; } = new List<int>();
    }

    public class WeekInfo
    {
        public int WeekIndexInYear { get; set; }
        public int WeekIndexInMonth { get; set; }
        public int AssignedMonth { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string DisplayName { get; set; } = null!;
    }

    public class TimeStructureGenerator
    {
        /// <summary>
        /// Tạo cấu trúc danh sách tuần của 1 năm, áp dụng Quy tắc Đa số (Majority Rule)
        /// để gán tuần vào tháng tương ứng.
        /// </summary>
        /// <param name="year">Năm cần tính toán</param>
        /// <returns>Danh sách các tuần trong năm</returns>
        public static List<WeekInfo> GenerateWeeksForYear(int year)
        {
            var weeks = new List<WeekInfo>();

            // Tìm ngày Thứ 2 của tuần đầu tiên (tuần chứa ngày 01/01)
            DateTime firstDayOfYear = new DateTime(year, 1, 1);
            int daysOffset = DayOfWeek.Monday - firstDayOfYear.DayOfWeek;
            if (daysOffset > 0)
            {
                daysOffset -= 7;
            }
            DateTime currentMonday = firstDayOfYear.AddDays(daysOffset);

            int weekIndexInYear = 1;

            // Dừng vòng lặp khi Thứ 2 của tuần hiện tại đã sang năm tiếp theo.
            while (currentMonday.Year <= year)
            {
                // Nếu tuần đầu tiên rơi vào năm trước (ví dụ 30/12), vẫn tính
                if (weekIndexInYear == 1 && currentMonday.Year < year)
                {
                    // Lệch sang năm trước
                }
                else if (currentMonday.Year > year)
                {
                    break;
                }

                DateTime currentSunday = currentMonday.AddDays(6);

                // Nếu ngày Chủ nhật vắt sang năm mới và rơi vào tháng 1,
                // theo Majority Rule nếu tháng 1 >= 4 ngày, thì tuần này lẽ ra thuộc tháng 1 của NĂM SAU.
                // Do chúng ta chỉ đang tính phân bổ cho "Năm hiện tại", ta sẽ cố định tuần này vào Tháng 12
                // hoặc dừng vòng lặp tùy nghiệp vụ.
                // Ở đây, ta kiểm tra nếu Chủ nhật sang năm sau và thuộc Tháng 1:
                bool overlapsNextYear = currentSunday.Year > year;

                // Áp dụng Quy tắc Đa số: Tìm tháng chiếm số ngày nhiều nhất trong tuần (>= 4 ngày)
                int assignedMonth = 1;
                if (currentMonday.Month == currentSunday.Month)
                {
                    assignedMonth = currentMonday.Month;
                }
                else
                {
                    // Đếm số ngày của tháng bắt đầu
                    int daysInFirstMonth = DateTime.DaysInMonth(currentMonday.Year, currentMonday.Month) - currentMonday.Day + 1;
                    if (daysInFirstMonth >= 4)
                    {
                        assignedMonth = currentMonday.Month;
                    }
                    else
                    {
                        assignedMonth = currentSunday.Month;

                        // FIX LOGIC BUG: Nếu tháng chiếm đa số lại là Tháng 1 của NĂM SAU,
                        // ta gán nó vào tháng 12 của năm hiện tại để không bị nhầm vào Tháng 1 đầu năm.
                        if (overlapsNextYear && assignedMonth == 1)
                        {
                            assignedMonth = 12;
                        }
                    }
                }

                weeks.Add(new WeekInfo
                {
                    WeekIndexInYear = weekIndexInYear,
                    AssignedMonth = assignedMonth,
                    StartDate = currentMonday,
                    EndDate = currentSunday,
                    DisplayName = $"Tuần {weekIndexInYear} ({currentMonday:dd/MM} - {currentSunday:dd/MM})"
                });

                currentMonday = currentMonday.AddDays(7);
                weekIndexInYear++;
            }

            // Đánh lại chỉ số WeekIndexInMonth
            foreach (var group in weeks.GroupBy(w => w.AssignedMonth))
            {
                int indexInMonth = 1;
                foreach (var week in group.OrderBy(w => w.StartDate))
                {
                    week.WeekIndexInMonth = indexInMonth++;
                }
            }

            return weeks;
        }

        /// <summary>
        /// Tạo cây thời gian hoàn chỉnh trong CSDL (Năm -> Giai đoạn -> Tháng -> Tuần).
        /// </summary>
        /// <param name="db">Instance của AppDbContext đang mở (cho phép dùng chung Transaction)</param>
        /// <param name="planId">Id của Plan cần tạo cây</param>
        /// <param name="year">Năm</param>
        /// <param name="stageConfigs">Cấu hình các Giai đoạn và Tháng</param>
        public static async Task BuildCompleteTimeTreeAsync(AppDbContext db, int planId, int year, List<StageConfig> stageConfigs)
        {
            // 1. Xóa các TimeNode cũ của planId (nếu có - các node con sẽ tự động xóa nhờ Cascade Delete)
            var oldNodes = await db.TimeNodes.Where(tn => tn.PlanId == planId && tn.ParentId == null).ToListAsync();
            db.TimeNodes.RemoveRange(oldNodes);
            await db.SaveChangesAsync();

            // Lấy các NodeTypeId
            var nodeTypes = await db.TimeNodeTypes.ToDictionaryAsync(nt => nt.Code, nt => nt.Id);
            if (!nodeTypes.ContainsKey("YEAR") || !nodeTypes.ContainsKey("STAGE") || !nodeTypes.ContainsKey("MONTH") || !nodeTypes.ContainsKey("WEEK"))
            {
                throw new Exception("Bảng time_node_type thiếu các cấu hình cơ bản (YEAR, STAGE, MONTH, WEEK).");
            }

            // 2. Tạo Node Cấp 1 (NĂM - Root)
            var yearNode = new TimeNode
            {
                PlanId = planId,
                NodeTypeId = nodeTypes["YEAR"],
                Code = $"Y{year}",
                Name = $"Năm {year}",
                ParentId = null, // Root node
                IsManual = false,
                IsLocked = false
            };
            db.TimeNodes.Add(yearNode);
            await db.SaveChangesAsync(); // Lưu để lấy Id (Trigger trong MySQL sẽ lo tree_path & level)

            // Sinh danh sách tuần trong năm
            var allWeeks = GenerateWeeksForYear(year);

            // 3. Duyệt danh sách Giai đoạn
            int stageOrder = 1;
            foreach (var stageConfig in stageConfigs)
            {
                var stageNode = new TimeNode
                {
                    PlanId = planId,
                    ParentId = yearNode.Id,
                    NodeTypeId = nodeTypes["STAGE"],
                    Code = stageConfig.StageCode,
                    Name = stageConfig.StageName,
                    SortOrder = stageOrder++,
                    IsManual = false,
                    IsLocked = false
                };
                db.TimeNodes.Add(stageNode);
                await db.SaveChangesAsync();

                // 4. Duyệt các Tháng trong Giai đoạn
                foreach (var monthIndex in stageConfig.Months)
                {
                    var monthNode = new TimeNode
                    {
                        PlanId = planId,
                        ParentId = stageNode.Id,
                        NodeTypeId = nodeTypes["MONTH"],
                        Code = $"M{monthIndex:D2}",
                        Name = $"Tháng {monthIndex}",
                        SortOrder = monthIndex,
                        IsManual = false,
                        IsLocked = false
                    };
                    db.TimeNodes.Add(monthNode);
                    await db.SaveChangesAsync();

                    // 5. Duyệt các Tuần thuộc Tháng này
                    var weeksInMonth = allWeeks.Where(w => w.AssignedMonth == monthIndex).OrderBy(w => w.WeekIndexInMonth).ToList();

                    foreach (var week in weeksInMonth)
                    {
                        var weekNode = new TimeNode
                        {
                            PlanId = planId,
                            ParentId = monthNode.Id,
                            NodeTypeId = nodeTypes["WEEK"],
                            Code = $"W{week.WeekIndexInYear:D2}",
                            Name = week.DisplayName, // "Tuần x (dd/MM - dd/MM)"
                            StartDate = week.StartDate,
                            EndDate = week.EndDate,
                            SortOrder = week.WeekIndexInMonth,
                            IsManual = false,
                            IsLocked = false
                        };
                        db.TimeNodes.Add(weekNode);
                    }
                    // Lưu hàng loạt các tuần của 1 tháng
                    await db.SaveChangesAsync();
                }
            }
        }
    }
}
