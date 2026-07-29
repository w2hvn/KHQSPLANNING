using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using MilitaryTrainingApp.Entities;

namespace MilitaryTrainingApp.Views
{
    public partial class PlanManagementPage : Page
    {
        private ObservableCollection<Plan> _plans = new ObservableCollection<Plan>();

        public PlanManagementPage()
        {
            InitializeComponent();
            LoadData();
        }

        private async void LoadData()
        {
            try
            {
                using var db = new AppDbContext();
                var plans = await db.Plans.ToListAsync();
                _plans = new ObservableCollection<Plan>(plans);
                dgPlans.ItemsSource = _plans;

                var targets = await db.TrainingTargets.ToListAsync();
                cboTrainingTargets.ItemsSource = targets;
                if (targets.Any()) cboTrainingTargets.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tải dữ liệu: {ex.Message}");
            }
        }

        private void BtnAddPlan_Click(object sender, RoutedEventArgs e)
        {
            _plans.Add(new Plan { Code = "NEW_PLAN", Name = "Kế hoạch mới", Year = DateTime.Now.Year, Status = "DRAFT" });
        }

        private async void BtnDeletePlan_Click(object sender, RoutedEventArgs e)
        {
            if (dgPlans.SelectedItem is Plan selectedPlan)
            {
                var result = MessageBox.Show($"Xóa kế hoạch '{selectedPlan.Name}' sẽ xóa toàn bộ cây nội dung và phân bổ liên quan. Tiếp tục?", "Cảnh báo", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        using var db = new AppDbContext();
                        var dbPlan = await db.Plans.FindAsync(selectedPlan.Id);
                        if (dbPlan != null)
                        {
                            db.Plans.Remove(dbPlan);
                            await db.SaveChangesAsync();
                        }
                        _plans.Remove(selectedPlan);
                        dgPlanTargets.ItemsSource = null;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Lỗi xóa: {ex.Message}");
                    }
                }
            }
        }

        private async void BtnSavePlans_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var db = new AppDbContext();
                foreach (var plan in _plans)
                {
                    if (plan.Id == 0)
                        db.Plans.Add(plan);
                    else
                        db.Entry(plan).State = EntityState.Modified;
                }
                await db.SaveChangesAsync();
                MessageBox.Show("Lưu Kế hoạch thành công!");
                LoadData(); // reload
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi lưu dữ liệu: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        private async void DgPlans_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgPlans.SelectedItem is Plan selectedPlan && selectedPlan.Id > 0)
            {
                try
                {
                    using var db = new AppDbContext();
                    var planTargets = await db.PlanTargets
                        .Include(pt => pt.Target)
                        .Where(pt => pt.PlanId == selectedPlan.Id)
                        .ToListAsync();
                    dgPlanTargets.ItemsSource = new ObservableCollection<PlanTarget>(planTargets);
                }
                catch { }
            }
            else
            {
                dgPlanTargets.ItemsSource = null;
            }
        }

        private async void BtnAddPlanTarget_Click(object sender, RoutedEventArgs e)
        {
            if (dgPlans.SelectedItem is Plan selectedPlan && selectedPlan.Id > 0 && cboTrainingTargets.SelectedValue is int targetId)
            {
                try
                {
                    using var db = new AppDbContext();
                    bool exists = await db.PlanTargets.AnyAsync(pt => pt.PlanId == selectedPlan.Id && pt.TargetId == targetId);
                    if (!exists)
                    {
                        db.PlanTargets.Add(new PlanTarget { PlanId = selectedPlan.Id, TargetId = targetId });
                        await db.SaveChangesAsync();
                        DgPlans_SelectionChanged(this, null!); // refresh targets
                    }
                    else
                    {
                        MessageBox.Show("Đối tượng này đã có trong Kế hoạch.");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi thêm đối tượng: {ex.Message}");
                }
            }
            else
            {
                MessageBox.Show("Vui lòng lưu Kế hoạch mới (để có ID) trước khi thêm đối tượng.");
            }
        }

        private async void BtnRemovePlanTarget_Click(object sender, RoutedEventArgs e)
        {
            if (dgPlanTargets.SelectedItem is PlanTarget selectedPt)
            {
                try
                {
                    using var db = new AppDbContext();
                    var dbPt = await db.PlanTargets.FindAsync(selectedPt.Id);
                    if (dbPt != null)
                    {
                        db.PlanTargets.Remove(dbPt);
                        await db.SaveChangesAsync();
                        DgPlans_SelectionChanged(this, null!); // refresh targets
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi gỡ bỏ đối tượng: {ex.Message}");
                }
            }
        }
    }
}
