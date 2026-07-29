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
