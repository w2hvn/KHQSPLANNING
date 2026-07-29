using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using MilitaryTrainingApp.Entities;

namespace MilitaryTrainingApp.Views.SystemConfig
{
    public partial class TargetRuleConfigView : UserControl
    {
        private int _planId;
        private List<TargetRuleModel> _data = new List<TargetRuleModel>();

        public TargetRuleConfigView()
        {
            InitializeComponent();
        }

        public void LoadData(int planId)
        {
            _planId = planId;
            RefreshGrid();
        }

        private async void RefreshGrid()
        {
            if (_planId <= 0) return;
            try
            {
                using var db = new AppDbContext();

                var allTrainingTargets = await db.TrainingTargets.OrderBy(t => t.SortOrder).ToListAsync();
                cboTrainingTargets.ItemsSource = allTrainingTargets;
                if (allTrainingTargets.Any()) cboTrainingTargets.SelectedIndex = 0;

                var targets = await db.PlanTargets
                    .Include(pt => pt.Target)
                    .Where(pt => pt.PlanId == _planId)
                    .OrderBy(pt => pt.Target.SortOrder)
                    .ToListAsync();

                _data = targets.Select(t => new TargetRuleModel
                {
                    PlanTargetId = t.Id,
                    TargetCode = t.Target.Code,
                    TargetName = t.Target.Name,
                    DaysPerWeek = t.DaysPerWeek,
                    MorningHours = t.MorningHours,
                    AfternoonHours = t.AfternoonHours,
                    NightHours = t.NightHours
                }).ToList();

                dgTargets.ItemsSource = _data;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tải định mức: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshGrid();
        }

        private async void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (cboTrainingTargets.SelectedValue is int targetId)
            {
                try
                {
                    using var db = new AppDbContext();
                    bool exists = await db.PlanTargets.AnyAsync(pt => pt.PlanId == _planId && pt.TargetId == targetId);
                    if (!exists)
                    {
                        var newPt = new PlanTarget
                        {
                            PlanId = _planId,
                            TargetId = targetId,
                            DaysPerWeek = 5,
                            MorningHours = 4.0m,
                            AfternoonHours = 3.0m,
                            NightHours = 2.0m
                        };
                        db.PlanTargets.Add(newPt);
                        await db.SaveChangesAsync();
                        RefreshGrid();
                    }
                    else
                    {
                        MessageBox.Show("Đối tượng này đã có trong Kế hoạch.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi thêm đối tượng: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (dgTargets.SelectedItem is TargetRuleModel selected)
            {
                var result = MessageBox.Show($"Xóa đối tượng '{selected.TargetName}' khỏi Kế hoạch sẽ làm mất toàn bộ cấu hình con. Tiếp tục?", "Cảnh báo", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        using var db = new AppDbContext();
                        var pt = await db.PlanTargets.FindAsync(selected.PlanTargetId);
                        if (pt != null)
                        {
                            db.PlanTargets.Remove(pt);
                            await db.SaveChangesAsync();
                            RefreshGrid();
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Lỗi xóa đối tượng: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var db = new AppDbContext();
                foreach (var item in _data)
                {
                    var pt = await db.PlanTargets.FindAsync(item.PlanTargetId);
                    if (pt != null)
                    {
                        pt.DaysPerWeek = item.DaysPerWeek;
                        pt.MorningHours = item.MorningHours;
                        pt.AfternoonHours = item.AfternoonHours;
                        pt.NightHours = item.NightHours;
                    }
                }
                await db.SaveChangesAsync();
                MessageBox.Show("Lưu định mức đối tượng thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                RefreshGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi lưu dữ liệu: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class TargetRuleModel : INotifyPropertyChanged
    {
        public int PlanTargetId { get; set; }
        public string TargetCode { get; set; } = string.Empty;
        public string TargetName { get; set; } = string.Empty;

        private int _daysPerWeek;
        public int DaysPerWeek
        {
            get => _daysPerWeek;
            set { _daysPerWeek = value; OnPropertyChanged(); OnPropertyChanged(nameof(MaxHoursPerWeek)); }
        }

        private decimal _morningHours;
        public decimal MorningHours
        {
            get => _morningHours;
            set { _morningHours = value; OnPropertyChanged(); OnPropertyChanged(nameof(MaxHoursPerWeek)); }
        }

        private decimal _afternoonHours;
        public decimal AfternoonHours
        {
            get => _afternoonHours;
            set { _afternoonHours = value; OnPropertyChanged(); OnPropertyChanged(nameof(MaxHoursPerWeek)); }
        }

        private decimal _nightHours;
        public decimal NightHours
        {
            get => _nightHours;
            set { _nightHours = value; OnPropertyChanged(); OnPropertyChanged(nameof(MaxHoursPerWeek)); }
        }

        // Công thức tính Ngân sách Cực đại
        public decimal MaxHoursPerWeek => DaysPerWeek * (MorningHours + AfternoonHours + NightHours);

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
