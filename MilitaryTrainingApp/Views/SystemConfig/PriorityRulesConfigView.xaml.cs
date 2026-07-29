using System;
using System.Collections.Generic;
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
        private List<SchedulingPriorityRule> _rules = new List<SchedulingPriorityRule>();

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
                _rules = await db.SchedulingPriorityRules.Where(r => r.PlanId == _planId).ToListAsync();

                if (!_rules.Any())
                {
                    _rules = new List<SchedulingPriorityRule>
                    {
                        new SchedulingPriorityRule { PlanId = _planId, RuleCode = "RULE_POLITICAL_FIRST", RuleName = "Ưu tiên môn Chính trị (CT) học trước", PriorityScore = 90 },
                        new SchedulingPriorityRule { PlanId = _planId, RuleCode = "RULE_MILITARY_CORE", RuleName = "Ưu tiên Quân sự / Điều lệnh (QS/ĐL)", PriorityScore = 70 },
                        new SchedulingPriorityRule { PlanId = _planId, RuleCode = "RULE_FATIGUE_BALANCING", RuleName = "Giãn cách Thể lực (TL/SK) tránh học liên tục", PriorityScore = 50 }
                    };
                    db.SchedulingPriorityRules.AddRange(_rules);
                    await db.SaveChangesAsync();
                }

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

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var db = new AppDbContext();
                foreach (var rule in _rules)
                {
                    db.Entry(rule).State = EntityState.Modified;
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
