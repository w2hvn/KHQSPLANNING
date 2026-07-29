using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using MilitaryTrainingApp.Entities;
using MilitaryTrainingApp.Helpers;

namespace MilitaryTrainingApp.Views
{
    public partial class TimeTreeManagementPage : Page
    {
        public TimeTreeManagementPage()
        {
            InitializeComponent();
            LoadPlans();
        }

        private async void LoadPlans()
        {
            try
            {
                using var db = new AppDbContext();
                var plans = await db.Plans.OrderByDescending(p => p.Year).ToListAsync();
                cboPlans.ItemsSource = plans;
                if (plans.Any()) cboPlans.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tải Kế hoạch: {ex.Message}");
            }
        }

        private void CboPlans_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadTimeTree();
        }

        private async void LoadTimeTree()
        {
            if (cboPlans.SelectedItem is Plan selectedPlan)
            {
                try
                {
                    using var db = new AppDbContext();
                    var rawNodes = await db.TimeNodes
                        .Where(tn => tn.PlanId == selectedPlan.Id)
                        .OrderBy(tn => tn.SortOrder).ThenBy(tn => tn.Id)
                        .ToListAsync();

                    var treeNodes = BuildTimeTree(rawNodes, null);
                    tvTimeNodes.ItemsSource = treeNodes;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi tải Cây Thời gian: {ex.Message}");
                }
            }
        }

        private List<TimeTreeDisplayItem> BuildTimeTree(List<TimeNode> allNodes, int? parentId)
        {
            return allNodes
                .Where(n => n.ParentId == parentId)
                .Select(n => new TimeTreeDisplayItem
                {
                    Id = n.Id,
                    Name = n.Name,
                    DateRangeStr = (n.StartDate.HasValue && n.EndDate.HasValue) ? $"({n.StartDate.Value:dd/MM} - {n.EndDate.Value:dd/MM})" : "",
                    Children = BuildTimeTree(allNodes, n.Id)
                })
                .ToList();
        }

        private async void BtnGenerateTree_Click(object sender, RoutedEventArgs e)
        {
            if (cboPlans.SelectedItem is Plan selectedPlan)
            {
                var result = MessageBox.Show($"Bạn có chắc chắn muốn TẠO MỚI (và xóa cũ) cây thời gian cho Kế hoạch {selectedPlan.Name} (Năm {selectedPlan.Year})?", "Cảnh báo Xóa Dữ Liệu", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        // Giả lập Stage Config mặc định (2 giai đoạn, chia tháng)
                        var stageConfigs = new List<StageConfig>
                        {
                            new StageConfig { StageCode = "GD1", StageName = "Giai đoạn 1", Months = new List<int> { 2, 3, 4, 5, 6 } },
                            new StageConfig { StageCode = "GD2", StageName = "Giai đoạn 2", Months = new List<int> { 7, 8, 9, 10, 11, 12 } }
                        };

                        await TimeStructureGenerator.BuildCompleteTimeTreeAsync(selectedPlan.Id, selectedPlan.Year, stageConfigs);

                        MessageBox.Show("Khởi tạo Cây thời gian thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                        LoadTimeTree(); // Refresh UI
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Lỗi khởi tạo cây thời gian: {ex.Message}");
                    }
                }
            }
        }
    }

    public class TimeTreeDisplayItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string DateRangeStr { get; set; } = string.Empty;
        public List<TimeTreeDisplayItem> Children { get; set; } = new List<TimeTreeDisplayItem>();
    }
}
