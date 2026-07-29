using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using MilitaryTrainingApp.Entities;

namespace MilitaryTrainingApp.Views
{
    public partial class TrainingTargetPage : Page
    {
        private ObservableCollection<TrainingTarget> _targets = new ObservableCollection<TrainingTarget>();

        public TrainingTargetPage()
        {
            InitializeComponent();
            LoadData();
        }

        private async void LoadData()
        {
            try
            {
                using var db = new AppDbContext();
                var data = await db.TrainingTargets.OrderBy(t => t.SortOrder).ToListAsync();
                _targets = new ObservableCollection<TrainingTarget>(data);
                dgTargets.ItemsSource = _targets;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tải dữ liệu: {ex.Message}");
            }
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            _targets.Add(new TrainingTarget { Code = "NEW_CODE", Name = "Đối tượng mới", SortOrder = _targets.Count + 1 });
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (dgTargets.SelectedItem is TrainingTarget selected)
            {
                var result = MessageBox.Show($"Xóa đối tượng '{selected.Name}' sẽ ảnh hưởng đến các Kế hoạch đang chứa nó. Tiếp tục?", "Cảnh báo", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        using var db = new AppDbContext();
                        var dbObj = await db.TrainingTargets.FindAsync(selected.Id);
                        if (dbObj != null)
                        {
                            db.TrainingTargets.Remove(dbObj);
                            await db.SaveChangesAsync();
                        }
                        _targets.Remove(selected);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Lỗi xóa: {ex.Message}");
                    }
                }
            }
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var db = new AppDbContext();
                foreach (var t in _targets)
                {
                    if (t.Id == 0)
                        db.TrainingTargets.Add(t);
                    else
                        db.Entry(t).State = EntityState.Modified;
                }
                await db.SaveChangesAsync();
                MessageBox.Show("Lưu danh mục thành công!");
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi lưu dữ liệu: {ex.InnerException?.Message ?? ex.Message}");
            }
        }
    }
}
