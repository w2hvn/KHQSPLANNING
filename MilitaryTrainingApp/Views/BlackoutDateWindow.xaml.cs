using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using MilitaryTrainingApp.Entities;

namespace MilitaryTrainingApp.Views
{
    public partial class BlackoutDateWindow : Window
    {
        private int _planId;
        private ObservableCollection<BlackoutDate> _dates = new ObservableCollection<BlackoutDate>();

        public BlackoutDateWindow(int planId)
        {
            InitializeComponent();
            _planId = planId;
            LoadData();
        }

        private async void LoadData()
        {
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
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi lưu dữ liệu: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
