using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using MilitaryTrainingApp.Entities;

namespace MilitaryTrainingApp.Views.SystemConfig
{
    public partial class PriorityRulesConfigView : UserControl
    {
        private int _planId;
        private ObservableCollection<SchedulingPriorityRule> _rules = new ObservableCollection<SchedulingPriorityRule>();

        public PriorityRulesConfigView()
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
                var data = await db.SchedulingPriorityRules.Where(r => r.PlanId == _planId).ToListAsync();

                if (!data.Any())
                {
                    data = new System.Collections.Generic.List<SchedulingPriorityRule>
                    {
                        new SchedulingPriorityRule { PlanId = _planId, RuleCode = "RULE_POLITICAL_FIRST", RuleName = "Ưu tiên môn Chính trị (CT) học trước", PriorityScore = 90 },
                        new SchedulingPriorityRule { PlanId = _planId, RuleCode = "RULE_MILITARY_CORE", RuleName = "Ưu tiên Quân sự / Điều lệnh (QS/ĐL)", PriorityScore = 70 },
                        new SchedulingPriorityRule { PlanId = _planId, RuleCode = "RULE_FATIGUE_BALANCING", RuleName = "Giãn cách Thể lực (TL/SK) tránh học liên tục", PriorityScore = 50 }
                    };
                    db.SchedulingPriorityRules.AddRange(data);
                    await db.SaveChangesAsync();
                }

                _rules = new ObservableCollection<SchedulingPriorityRule>(data);
                dgRules.ItemsSource = _rules;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tải cấu hình quy tắc: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshGrid();
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            _rules.Add(new SchedulingPriorityRule { PlanId = _planId, RuleCode = "RULE_NEW", RuleName = "Quy tắc mới", PriorityScore = 50, IsActive = true });
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (dgRules.SelectedItem is SchedulingPriorityRule selected)
            {
                _rules.Remove(selected);
            }
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var db = new AppDbContext();
                var oldRules = await db.SchedulingPriorityRules.Where(r => r.PlanId == _planId).ToListAsync();
                db.SchedulingPriorityRules.RemoveRange(oldRules);

                foreach (var rule in _rules)
                {
                    rule.Id = 0; // Reset ID for re-inserting
                    db.SchedulingPriorityRules.Add(rule);
                }

                await db.SaveChangesAsync();
                MessageBox.Show("Lưu quy tắc ưu tiên thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                RefreshGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi lưu cấu hình: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
