using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using MilitaryTrainingApp.Entities;
using MilitaryTrainingApp.Helpers;

namespace MilitaryTrainingApp.Views
{
    public partial class TimeTreeManagementPage : Page
    {
        public TimeTreeManagementPage()
        {
            InitializeComponent();
            LoadPlans();
        }

        private async void LoadPlans()
        {
            try
            {
                using var db = new AppDbContext();
                var plans = await db.Plans.OrderByDescending(p => p.Year).ToListAsync();
                cboPlans.ItemsSource = plans;
                if (plans.Any()) cboPlans.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tải Kế hoạch: {ex.Message}");
            }
        }

        public void RefreshData()
        {
            LoadPlans();
        }

        private void CboPlans_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadTimeTree();
        }

        private async void LoadTimeTree()
        {
            if (cboPlans.SelectedItem is Plan selectedPlan)
            {
                try
                {
                    using var db = new AppDbContext();
                    var rawNodes = await db.TimeNodes
                        .Where(tn => tn.PlanId == selectedPlan.Id)
                        .OrderBy(tn => tn.SortOrder).ThenBy(tn => tn.Id)
                        .ToListAsync();

                    var treeNodes = BuildTimeTree(rawNodes, null);
                    tvTimeNodes.ItemsSource = treeNodes;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi tải Cây Thời gian: {ex.Message}");
                }
            }
        }

        private List<TimeTreeDisplayItem> BuildTimeTree(List<TimeNode> allNodes, int? parentId)
        {
            return allNodes
                .Where(n => n.ParentId == parentId)
                .Select(n => new TimeTreeDisplayItem
                {
                    Id = n.Id,
                    Name = n.Name,
                    DateRangeStr = (n.StartDate.HasValue && n.EndDate.HasValue) ? $"({n.StartDate.Value:dd/MM} - {n.EndDate.Value:dd/MM})" : "",
                    Children = BuildTimeTree(allNodes, n.Id)
                })
                .ToList();
        }
    }

    public class TimeTreeDisplayItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string DateRangeStr { get; set; } = string.Empty;
        public List<TimeTreeDisplayItem> Children { get; set; } = new List<TimeTreeDisplayItem>();
    }
}
