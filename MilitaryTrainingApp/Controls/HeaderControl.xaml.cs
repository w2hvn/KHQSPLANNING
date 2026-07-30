using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using MilitaryTrainingApp.Entities;

namespace MilitaryTrainingApp.Controls
{
    public partial class HeaderControl : UserControl
    {
        public event EventHandler<GlobalContextEventArgs>? GlobalContextChanged;

        // Cờ ngăn chặn bắn sự kiện nhiều lần khi đang load danh sách
        private bool _isDataLoading = false;

        public HeaderControl()
        {
            InitializeComponent();
            this.Loaded += HeaderControl_Loaded;
        }

        private async void HeaderControl_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _isDataLoading = true;
                using var db = new AppDbContext();
                var plans = await db.Plans.OrderByDescending(p => p.Year).ToListAsync();
                cboPlan.ItemsSource = plans;

                if (plans.Any())
                {
                    cboPlan.SelectedIndex = 0; // Trigger CboPlan_SelectionChanged
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tải Kế hoạch: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isDataLoading = false;
            }
        }

        private async void CboPlan_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cboPlan.SelectedValue is int planId)
            {
                try
                {
                    _isDataLoading = true;

                    using var db = new AppDbContext();

                    // 1. Tải danh sách Đối tượng
                    var targets = await db.PlanTargets
                        .Include(pt => pt.Target)
                        .Where(pt => pt.PlanId == planId)
                        .OrderBy(pt => pt.Target.SortOrder)
                        .Select(pt => new PlanTargetDisplayModel
                        {
                            PlanTargetId = pt.Id,
                            DisplayText = $"[{pt.Target.Code}] {pt.Target.Name}"
                        })
                        .ToListAsync();

                    cboTarget.ItemsSource = targets;
                    if (targets.Any())
                    {
                        cboTarget.SelectedIndex = 0;
                    }
                    else
                    {
                        cboTarget.ItemsSource = null;
                    }

                    // 2. Tải cây Thời gian
                    var timeNodes = await db.TimeNodes
                        .Where(tn => tn.PlanId == planId)
                        .OrderBy(tn => tn.SortOrder).ThenBy(tn => tn.Id)
                        .ToListAsync();

                    var flatList = new List<TimeNodeDisplayModel>();

                    // Thêm phần tử Root ảo cho toàn bộ Kế hoạch
                    var plan = await db.Plans.FindAsync(planId);
                    flatList.Add(new TimeNodeDisplayModel
                    {
                        TimeNodeId = null,
                        DisplayText = $"[Toàn Kế hoạch] {plan?.Name ?? "Năm"}"
                    });

                    // Build đệ quy nhánh phẳng
                    var rootNodes = timeNodes.Where(n => n.ParentId == null).ToList();
                    for (int i = 0; i < rootNodes.Count; i++)
                    {
                        bool isLastRoot = (i == rootNodes.Count - 1);
                        BuildFlatTimeTree(rootNodes[i], timeNodes, flatList, "", isLastRoot);
                    }

                    cboActiveTimeNode.ItemsSource = flatList;
                    cboActiveTimeNode.SelectedIndex = 0; // Chọn Root ảo mặc định
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi tải Context: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    _isDataLoading = false;
                    FireGlobalContextChanged();
                }
            }
        }

        private void BuildFlatTimeTree(TimeNode currentNode, List<TimeNode> allNodes, List<TimeNodeDisplayModel> flatList, string prefix, bool isLastSibling)
        {
            string marker = isLastSibling ? "└─ " : "├─ ";
            string label = $"{prefix}{marker}{currentNode.Name}";

            if (currentNode.StartDate.HasValue && currentNode.EndDate.HasValue)
            {
                label += $" ({currentNode.StartDate.Value:dd/MM} - {currentNode.EndDate.Value:dd/MM})";
            }

            flatList.Add(new TimeNodeDisplayModel
            {
                TimeNodeId = currentNode.Id,
                DisplayText = label
            });

            string childPrefix = prefix + (isLastSibling ? "   " : "│  ");
            var children = allNodes.Where(n => n.ParentId == currentNode.Id).ToList();

            for (int i = 0; i < children.Count; i++)
            {
                bool isLastChild = (i == children.Count - 1);
                BuildFlatTimeTree(children[i], allNodes, flatList, childPrefix, isLastChild);
            }
        }

        private void CboTarget_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isDataLoading)
            {
                FireGlobalContextChanged();
            }
        }

        private void CboActiveTimeNode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isDataLoading)
            {
                FireGlobalContextChanged();
            }
        }

        private void FireGlobalContextChanged()
        {
            if (cboPlan.SelectedValue is int planId && cboTarget.SelectedValue is int targetId)
            {
                int? timeNodeId = cboActiveTimeNode.SelectedValue as int?;
                GlobalContextChanged?.Invoke(this, new GlobalContextEventArgs
                {
                    PlanId = planId,
                    PlanTargetId = targetId,
                    TimeNodeId = timeNodeId
                });
            }
        }
    }

    public class PlanTargetDisplayModel
    {
        public int PlanTargetId { get; set; }
        public string DisplayText { get; set; } = string.Empty;
    }

    public class TimeNodeDisplayModel
    {
        public int? TimeNodeId { get; set; }
        public string DisplayText { get; set; } = string.Empty;
    }
}
