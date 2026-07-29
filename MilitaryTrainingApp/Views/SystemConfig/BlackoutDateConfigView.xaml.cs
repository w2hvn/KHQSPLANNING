using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using MilitaryTrainingApp.Entities;

namespace MilitaryTrainingApp.Views.SystemConfig
{
    public partial class BlackoutDateConfigView : UserControl
    {
        private int _planId;
        private ObservableCollection<BlackoutDate> _dates = new ObservableCollection<BlackoutDate>();

        public BlackoutDateConfigView()
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
                var data = await db.BlackoutDates.Where(b => b.PlanId == _planId).ToListAsync();
                _dates = new ObservableCollection<BlackoutDate>(data);
                dgBlackoutDates.ItemsSource = _dates;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tải ngày nghỉ: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshGrid();
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            _dates.Add(new BlackoutDate { PlanId = _planId, StartDate = DateTime.Now.Date, EndDate = DateTime.Now.Date, HolidayName = "Nghỉ lễ mới" });
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (dgBlackoutDates.SelectedItem is BlackoutDate selected)
            {
                _dates.Remove(selected);
            }
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var db = new AppDbContext();

                // Clear old
                var oldDates = await db.BlackoutDates.Where(b => b.PlanId == _planId).ToListAsync();
                db.BlackoutDates.RemoveRange(oldDates);

                // Add new
                foreach (var item in _dates)
                {
                    if (!string.IsNullOrWhiteSpace(item.HolidayName))
                    {
                        db.BlackoutDates.Add(new BlackoutDate
                        {
                            PlanId = _planId,
                            HolidayName = item.HolidayName,
                            StartDate = item.StartDate == default ? DateTime.Now : item.StartDate,
                            EndDate = item.EndDate == default ? DateTime.Now : item.EndDate,
                            Description = item.Description
                        });
                    }
                }

                await db.SaveChangesAsync();
                MessageBox.Show("Lưu cấu hình ngày nghỉ lễ thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                RefreshGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi lưu dữ liệu: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
