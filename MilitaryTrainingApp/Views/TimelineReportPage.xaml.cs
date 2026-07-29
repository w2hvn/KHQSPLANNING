using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using MilitaryTrainingApp.Entities;

namespace MilitaryTrainingApp.Views
{
    public partial class TimelineReportPage : Page
    {
        private int _currentPlanTargetId = 0;
        private List<ReportRowItem> _currentReportData = new List<ReportRowItem>();

        public TimelineReportPage()
        {
            InitializeComponent();
        }

        public void RefreshData(int planTargetId)
        {
            _currentPlanTargetId = planTargetId;
            LoadReportData();
        }

        private void CboTimeLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.IsLoaded)
            {
                LoadReportData();
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadReportData();
        }

        private async void LoadReportData()
        {
            if (_currentPlanTargetId <= 0) return;

            string targetNodeTypeCode = "MONTH"; // Mặc định
            if (cboTimeLevel.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                targetNodeTypeCode = item.Tag.ToString()!;
            }

            try
            {
                using var db = new AppDbContext();

                // 1. Lấy danh sách ProgramNodes của Đối tượng
                var programNodes = await db.ProgramNodes
                    .Include(pn => pn.Decor)
                    .Where(pn => pn.PlanTargetId == _currentPlanTargetId)
                    .ToListAsync();
                var programNodeDict = programNodes.ToDictionary(pn => pn.Id);

                // 2. Lấy danh sách toàn bộ TimeNodes của Kế hoạch này
                var planId = await db.PlanTargets.Where(pt => pt.Id == _currentPlanTargetId).Select(pt => pt.PlanId).FirstOrDefaultAsync();
                var allTimeNodes = await db.TimeNodes
                    .Include(tn => tn.NodeType)
                    .Where(tn => tn.PlanId == planId)
                    .ToListAsync();
                var timeNodeDict = allTimeNodes.ToDictionary(tn => tn.Id);

                // 3. Lấy tất cả phân bổ thời gian (TimeAllocation)
                // Lưu ý: Trong kiến trúc, dữ liệu chuẩn được lưu ở các lá con (thường là cấp WEEK).
                var allAllocations = await db.TimeAllocations
                    .Where(ta => programNodeDict.Keys.Contains(ta.ProgramNodeId))
                    .ToListAsync();

                // 4. Logic Roll-up (Cộng dồn)
                var rolledUpData = new Dictionary<string, ReportRowItem>();

                foreach (var alloc in allAllocations)
                {
                    if (!timeNodeDict.TryGetValue(alloc.TimeNodeId, out var timeNode)) continue;

                    // Tìm TimeNode tổ tiên (hoặc chính nó) khớp với targetNodeTypeCode (VD: MONTH)
                    var targetTimeNode = FindAncestorByTypeCode(timeNode, targetNodeTypeCode, timeNodeDict);
                    if (targetTimeNode == null) continue; // Nếu không tìm thấy, bỏ qua bản ghi này

                    var programNode = programNodeDict[alloc.ProgramNodeId];

                    // Tạo Key gom nhóm: targetTimeNode.Id + "-" + programNode.Id
                    string compositeKey = $"{targetTimeNode.Id}-{programNode.Id}";

                    if (!rolledUpData.ContainsKey(compositeKey))
                    {
                        rolledUpData[compositeKey] = new ReportRowItem
                        {
                            TimeSortOrder = targetTimeNode.SortOrder ?? 0,
                            TimeNodeId = targetTimeNode.Id,
                            TimeLabel = FormatTimeNodeLabel(targetTimeNode),

                            ProgramSortOrder = programNode.SortOrder ?? 0,
                            ProgramNodeId = programNode.Id,
                            ProgramCode = programNode.Code,
                            ProgramName = programNode.Name,
                            Level = programNode.Level,

                            BgColorHex = programNode.Decor?.BgColorHex ?? "#FFFFFF",
                            AllocatedHours = 0
                        };
                    }

                    // Cộng dồn số giờ
                    rolledUpData[compositeKey].AllocatedHours += alloc.AllocatedHours;
                }

                // 5. Sắp xếp kết quả để hiển thị đẹp mắt
                // Ưu tiên: Thời gian -> Môn -> Bài
                _currentReportData = rolledUpData.Values
                    .OrderBy(r => r.TimeSortOrder).ThenBy(r => r.TimeNodeId) // Nhóm theo mốc thời gian
                    .ThenBy(r => r.Level == 1 ? 0 : 1)                       // Đưa các node Môn học (Level 1) lên trước trong nhóm
                    .ThenBy(r => r.ProgramSortOrder).ThenBy(r => r.ProgramNodeId) // Sắp xếp theo cấu trúc cây nội dung
                    .ToList();

                dgTimeline.ItemsSource = _currentReportData;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi lập báo cáo: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private TimeNode? FindAncestorByTypeCode(TimeNode currentNode, string targetTypeCode, Dictionary<int, TimeNode> timeNodeDict)
        {
            if (currentNode.NodeType.Code == targetTypeCode) return currentNode;
            if (currentNode.ParentId.HasValue && timeNodeDict.TryGetValue(currentNode.ParentId.Value, out var parentNode))
            {
                return FindAncestorByTypeCode(parentNode, targetTypeCode, timeNodeDict);
            }
            return null;
        }

        private string FormatTimeNodeLabel(TimeNode node)
        {
            if (node.NodeType.Code == "WEEK" && node.StartDate.HasValue && node.EndDate.HasValue)
            {
                return $"{node.Name} ({node.StartDate.Value:dd/MM} - {node.EndDate.Value:dd/MM})";
            }
            return node.Name;
        }

        private void BtnExportExcel_Click(object sender, RoutedEventArgs e)
        {
            if (_currentReportData == null || !_currentReportData.Any())
            {
                MessageBox.Show("Không có dữ liệu để xuất Excel.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SaveFileDialog sfd = new SaveFileDialog
            {
                Filter = "Excel Files|*.xlsx",
                Title = "Lưu Báo Cáo Tiến Độ Huấn Luyện",
                FileName = $"BaoCao_TienDo_HuanLuyen_{DateTime.Now:yyyyMMdd}.xlsx"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    using (var workbook = new XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add("Tiến độ Huấn luyện");

                        // 1. Header Title
                        worksheet.Cell(1, 1).Value = $"BẢNG TỔNG HỢP TIẾN ĐỘ HUẤN LUYỆN QUÂN SỰ - NĂM {DateTime.Now.Year}";
                        var titleRange = worksheet.Range("A1:D1");
                        titleRange.Merge();
                        titleRange.Style.Font.Bold = true;
                        titleRange.Style.Font.FontSize = 16;
                        titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                        // 2. Table Headers
                        worksheet.Cell(3, 1).Value = "Thời Gian Huấn Luyện";
                        worksheet.Cell(3, 2).Value = "Mã Môn/Bài";
                        worksheet.Cell(3, 3).Value = "Nội dung huấn luyện";
                        worksheet.Cell(3, 4).Value = "Số Giờ (h)";

                        var headerRange = worksheet.Range("A3:D3");
                        headerRange.Style.Font.Bold = true;
                        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#2E4A3E"); // Primary Military Color
                        headerRange.Style.Font.FontColor = XLColor.White;
                        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                        // 3. Table Data
                        int currentRow = 4;
                        decimal totalHours = 0;

                        foreach (var item in _currentReportData)
                        {
                            worksheet.Cell(currentRow, 1).Value = item.TimeLabel;
                            worksheet.Cell(currentRow, 2).Value = item.ProgramCode;

                            // Tạo indent thụt lề bằng khoảng trắng (Excel-friendly indent)
                            string indent = new string(' ', (item.Level - 1) * 4);
                            worksheet.Cell(currentRow, 3).Value = indent + item.ProgramName;

                            worksheet.Cell(currentRow, 4).Value = item.AllocatedHours;

                            // Highlight Level 1 (Môn học)
                            if (item.Level == 1)
                            {
                                var rowRange = worksheet.Range($"A{currentRow}:D{currentRow}");
                                rowRange.Style.Font.Bold = true;

                                // Áp dụng BgColorHex (nếu hợp lệ)
                                try
                                {
                                    rowRange.Style.Fill.BackgroundColor = XLColor.FromHtml(item.BgColorHex);
                                }
                                catch { /* Ignore invalid hex */ }
                            }

                            totalHours += item.AllocatedHours;
                            currentRow++;
                        }

                        // 4. Bổ sung dòng Tổng
                        worksheet.Cell(currentRow, 3).Value = "TỔNG CỘNG:";
                        worksheet.Cell(currentRow, 3).Style.Font.Bold = true;
                        worksheet.Cell(currentRow, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                        worksheet.Cell(currentRow, 4).Value = totalHours;
                        worksheet.Cell(currentRow, 4).Style.Font.Bold = true;
                        worksheet.Cell(currentRow, 4).Style.Font.FontColor = XLColor.DarkRed;

                        // 5. Khung (Borders)
                        var dataRange = worksheet.Range($"A3:D{currentRow}");
                        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                        // Auto-fit columns
                        worksheet.Columns().AdjustToContents();

                        // Lưu file
                        workbook.SaveAs(sfd.FileName);
                        MessageBox.Show("Xuất báo cáo thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi xuất file Excel: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    public class ReportRowItem
    {
        public int TimeSortOrder { get; set; }
        public int TimeNodeId { get; set; }
        public string TimeLabel { get; set; } = string.Empty;

        public int ProgramSortOrder { get; set; }
        public int ProgramNodeId { get; set; }
        public string? ProgramCode { get; set; }
        public string ProgramName { get; set; } = string.Empty;
        public int Level { get; set; }

        public decimal AllocatedHours { get; set; }

        public string BgColorHex { get; set; } = "#FFFFFF";
        public Brush BgBrush
        {
            get
            {
                try { return (Brush)new BrushConverter().ConvertFromString(BgColorHex)!; }
                catch { return Brushes.Transparent; }
            }
        }
        public FontWeight FontWeight => Level == 1 ? FontWeights.Bold : FontWeights.Normal;
    }
}
