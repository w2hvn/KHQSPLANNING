using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using MilitaryTrainingApp.Entities;

namespace MilitaryTrainingApp.Controls
{
    public partial class HeaderControl : UserControl
    {
        // Sự kiện bắn ra khi Đối tượng thay đổi
        public event EventHandler<int>? OnPlanTargetChanged;

        public HeaderControl()
        {
            InitializeComponent();
            this.Loaded += HeaderControl_Loaded;
        }

        private async void HeaderControl_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                using var db = new AppDbContext();
                // Tải danh sách Kế hoạch lên ComboBox
                var plans = await db.Plans.OrderByDescending(p => p.Year).ToListAsync();
                cboPlans.ItemsSource = plans;

                if (plans.Any())
                {
                    cboPlans.SelectedIndex = 0; // Chọn cái đầu tiên mặc định
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tải Kế hoạch: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void CboPlans_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cboPlans.SelectedValue is int planId)
            {
                try
                {
                    using var db = new AppDbContext();
                    // Load các đối tượng huấn luyện thuộc Kế hoạch này
                    var targets = await db.PlanTargets
                        .Include(pt => pt.Target)
                        .Where(pt => pt.PlanId == planId)
                        .OrderBy(pt => pt.Target.SortOrder)
                        .Select(pt => new PlanTargetDisplayModel
                        {
                            PlanTargetId = pt.Id,
                            DisplayText = $"[{pt.Target.Code}] {pt.Target.Name}"
                        })
                        .ToListAsync();

                    lstPlanTargets.ItemsSource = targets;

                    if (targets.Any())
                    {
                        lstPlanTargets.SelectedIndex = 0;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi tải Đối tượng huấn luyện: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void LstPlanTargets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstPlanTargets.SelectedItem is PlanTargetDisplayModel selectedTarget)
            {
                OnPlanTargetChanged?.Invoke(this, selectedTarget.PlanTargetId);
            }
        }
    }

    // Helper class để format chuỗi hiển thị
    public class PlanTargetDisplayModel
    {
        public int PlanTargetId { get; set; }
        public string DisplayText { get; set; } = string.Empty;
    }
}
