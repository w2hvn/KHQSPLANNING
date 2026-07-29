using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using MilitaryTrainingApp.Entities;

namespace MilitaryTrainingApp.Views
{
    public partial class PriorityRuleConfigWindow : Window
    {
        private int _planId;
        private List<SchedulingPriorityRule> _rules = new List<SchedulingPriorityRule>();

        public PriorityRuleConfigWindow(int planId)
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
                _rules = await db.SchedulingPriorityRules.Where(r => r.PlanId == _planId).ToListAsync();

                // Sinh mặc định nếu chưa có
                if (!_rules.Any())
                {
                    _rules = new List<SchedulingPriorityRule>
                    {
                        new SchedulingPriorityRule { PlanId = _planId, RuleCode = "RULE_POLITICAL_FIRST", RuleName = "Ưu tiên môn Chính trị (CT) học trước", PriorityScore = 90 },
                        new SchedulingPriorityRule { PlanId = _planId, RuleCode = "RULE_MILITARY_CORE", RuleName = "Ưu tiên Quân sự / Điều lệnh (QS/ĐL)", PriorityScore = 70 },
                        new SchedulingPriorityRule { PlanId = _planId, RuleCode = "RULE_FATIGUE_BALANCING", RuleName = "Giãn cách Thể lực (TL) tránh học liên tục", PriorityScore = 50 }
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
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi lưu cấu hình: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
