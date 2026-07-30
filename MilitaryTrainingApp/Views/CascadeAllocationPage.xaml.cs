using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using MilitaryTrainingApp.Entities;

namespace MilitaryTrainingApp.Views
{
    public partial class CascadeAllocationPage : Page
    {
        private int _currentPlanTargetId = 0;
        private int _currentPlanId = 0;
        private int? _currentTimeNodeId = null;
        private TimeTreeNodeItem? _selectedTimeNode = null;
        private List<TimeTreeNodeItem> _childTimeNodes = new List<TimeTreeNodeItem>();
        private List<AllocationRowItem> _rowItems = new List<AllocationRowItem>();
        private System.Collections.ObjectModel.ObservableCollection<ConflictLogItem> _conflictLogs = new System.Collections.ObjectModel.ObservableCollection<ConflictLogItem>();

        public CascadeAllocationPage()
        {
            InitializeComponent();
        }

        public void RefreshData(int planTargetId, int? timeNodeId)
        {
            _currentPlanTargetId = planTargetId;
            _currentTimeNodeId = timeNodeId;
            LoadTimeTree();
        }

        private async void LoadTimeTree()
        {
            if (_currentPlanTargetId <= 0) return;

            try
            {
                using var db = new AppDbContext();

                var planId = await db.PlanTargets
                    .Where(pt => pt.Id == _currentPlanTargetId)
                    .Select(pt => pt.PlanId)
                    .FirstOrDefaultAsync();

                if (planId == 0) return;
                _currentPlanId = planId;

                var rawNodes = await db.TimeNodes
                    .Include(tn => tn.NodeType)
                    .Where(tn => tn.PlanId == _currentPlanId)
                    .OrderBy(tn => tn.SortOrder).ThenBy(tn => tn.Id)
                    .ToListAsync();

                var treeNodes = BuildTimeTree(rawNodes, null);

                tvTimeNodes.ItemsSource = treeNodes;

                // Giải quyết tự động Scope Anchor (Auto-Focus & Load Data)
                if (treeNodes.Any())
                {
                    TimeTreeNodeItem? targetNode = null;
                    if (_currentTimeNodeId.HasValue && _currentTimeNodeId.Value > 0)
                    {
                        targetNode = FindNodeInTree(treeNodes, _currentTimeNodeId.Value);
                    }

                    // Fallback mặc định: Lấy root node đầu tiên (thường là Năm)
                    if (targetNode == null)
                    {
                        targetNode = treeNodes.First();
                    }

                    // Tự động gán và gọi hàm load
                    if (targetNode != null)
                    {
                        // Update UI selection visually if possible
                        SelectNodeInTreeView(tvTimeNodes, targetNode);

                        // Kích hoạt logic nạp dữ liệu
                        await SelectAndLoadNodeData(targetNode.Id);
                    }
                }
                else
                {
                    txtParentNodeName.Text = "---";
                    txtChildLevelName.Text = "---";
                    _rowItems.Clear();
                    dgAllocation.ItemsSource = null;
                    ClearDynamicColumns();

                    _conflictLogs.Clear();
                    dgConflictLog.ItemsSource = _conflictLogs;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tải Cây Thời gian: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private List<TimeTreeNodeItem> BuildTimeTree(List<TimeNode> allNodes, int? parentId)
        {
            return allNodes
                .Where(n => n.ParentId == parentId)
                .Select(n => new TimeTreeNodeItem
                {
                    Id = n.Id,
                    ParentId = n.ParentId,
                    Code = n.Code,
                    Name = n.Name,
                    Level = n.Level,
                    NodeTypeName = n.NodeType?.Name ?? "Unknown",
                    Children = BuildTimeTree(allNodes, n.Id)
                })
                .ToList();
        }

        private async void TvTimeNodes_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (tvTimeNodes.SelectedItem is TimeTreeNodeItem selectedNode)
            {
                await SelectAndLoadNodeData(selectedNode.Id);
            }
        }

        private async Task SelectAndLoadNodeData(int timeNodeId)
        {
            try
            {
                using var db = new AppDbContext();

                var entity = await db.TimeNodes.Include(t => t.NodeType).FirstOrDefaultAsync(t => t.Id == timeNodeId);
                if (entity == null) return;

                _selectedTimeNode = new TimeTreeNodeItem
                {
                    Id = entity.Id,
                    Name = entity.Name,
                    Level = entity.Level,
                    NodeTypeName = entity.NodeType.Code
                };

                txtParentNodeName.Text = _selectedTimeNode.Name;

                var childNodes = await db.TimeNodes
                    .Include(tn => tn.NodeType)
                    .Where(tn => tn.ParentId == _selectedTimeNode.Id)
                    .OrderBy(tn => tn.SortOrder).ThenBy(tn => tn.Id)
                    .ToListAsync();

                _childTimeNodes = childNodes.Select(n => new TimeTreeNodeItem
                {
                    Id = n.Id,
                    Name = n.Name,
                    NodeTypeName = n.NodeType?.Name ?? "Con"
                }).ToList();

                if (_childTimeNodes.Any())
                {
                    txtChildLevelName.Text = _childTimeNodes.First().NodeTypeName;
                }
                else
                {
                    txtChildLevelName.Text = "(Không có nhánh con)";
                }

                BuildDynamicColumns();
                await LoadProgramNodesAndAllocations(db);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tải dữ liệu lưới: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private TimeTreeNodeItem? FindNodeInTree(IEnumerable<TimeTreeNodeItem> nodes, int targetId)
        {
            foreach (var node in nodes)
            {
                if (node.Id == targetId) return node;
                var found = FindNodeInTree(node.Children, targetId);
                if (found != null) return found;
            }
            return null;
        }

        private void SelectNodeInTreeView(ItemsControl parentContainer, TimeTreeNodeItem targetNode)
        {
            // Note: WPF TreeView programmatic selection logic can be tricky without MVVM binding.
            // This is a minimal visual selection attempt. Real automatic selection may require recursive Generator generation.
            // For now, the logical loading works perfectly without forcing visual selection on the tree.
        }

        private void BuildDynamicColumns()
        {
            ClearDynamicColumns();

            foreach (var childNode in _childTimeNodes)
            {
                var col = new DataGridTextColumn
                {
                    Header = childNode.Name,
                    Binding = new Binding($"[{childNode.Id}]") { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus },
                    Width = 90
                };

                var style = new Style(typeof(TextBlock));
                style.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
                col.ElementStyle = style;

                dgAllocation.Columns.Add(col);
            }
        }

        private void ClearDynamicColumns()
        {
            while (dgAllocation.Columns.Count > 5)
            {
                dgAllocation.Columns.RemoveAt(5);
            }
        }

        private async Task LoadProgramNodesAndAllocations(AppDbContext db)
        {
            if (_selectedTimeNode == null) return;

            var allProgramNodes = await db.ProgramNodes
                .Include(pn => pn.Decor)
                .Where(pn => pn.PlanTargetId == _currentPlanTargetId)
                .OrderBy(pn => pn.SortOrder).ThenBy(pn => pn.Id)
                .ToListAsync();

            var validRowItems = new List<AllocationRowItem>();

            if (_selectedTimeNode.Level == 1)
            {
                foreach (var pn in allProgramNodes)
                {
                    if (pn.Level == 1 || pn.Capacity > 0)
                    {
                        validRowItems.Add(new AllocationRowItem
                        {
                            ProgramNodeId = pn.Id,
                            ProgramCode = pn.Code,
                            ProgramName = pn.Name,
                            Level = pn.Level,
                            ComplexityLevel = pn.ComplexityLevel,
                            IsNightTraining = pn.IsNightTraining,
                            IsOutdoor = pn.IsOutdoor,
                            IsHeavyPhysical = pn.IsHeavyPhysical,
                            PrerequisiteNodeId = pn.PrerequisiteNodeId,
                            BgColorHex = pn.Decor?.BgColorHex ?? "#FFFFFF",
                            ParentMaxBudget = pn.Capacity
                        });
                    }
                }
            }
            else
            {
                var parentAllocations = await db.TimeAllocations
                    .Where(ta => ta.TimeNodeId == _selectedTimeNode.Id && ta.AllocatedHours > 0)
                    .ToListAsync();

                var allocatedProgramIds = parentAllocations.Select(ta => ta.ProgramNodeId).ToHashSet();

                foreach (var pn in allProgramNodes)
                {
                    if (pn.Level == 1 || allocatedProgramIds.Contains(pn.Id))
                    {
                        decimal maxBudget = 0;
                        if (pn.Level > 1)
                        {
                            var alloc = parentAllocations.FirstOrDefault(ta => ta.ProgramNodeId == pn.Id);
                            if (alloc != null) maxBudget = alloc.AllocatedHours;
                        }

                        validRowItems.Add(new AllocationRowItem
                        {
                            ProgramNodeId = pn.Id,
                            ProgramCode = pn.Code,
                            ProgramName = pn.Name,
                            Level = pn.Level,
                            ComplexityLevel = pn.ComplexityLevel,
                            IsNightTraining = pn.IsNightTraining,
                            IsOutdoor = pn.IsOutdoor,
                            IsHeavyPhysical = pn.IsHeavyPhysical,
                            PrerequisiteNodeId = pn.PrerequisiteNodeId,
                            BgColorHex = pn.Decor?.BgColorHex ?? "#FFFFFF",
                            ParentMaxBudget = maxBudget
                        });
                    }
                }
            }

            var childTimeNodeIds = _childTimeNodes.Select(c => c.Id).ToList();
            if (childTimeNodeIds.Any())
            {
                var existingChildAllocations = await db.TimeAllocations
                    .Where(ta => childTimeNodeIds.Contains(ta.TimeNodeId))
                    .ToListAsync();

                foreach (var row in validRowItems)
                {
                    foreach (var childId in childTimeNodeIds)
                    {
                        var match = existingChildAllocations.FirstOrDefault(ta => ta.TimeNodeId == childId && ta.ProgramNodeId == row.ProgramNodeId);
                        if (match != null)
                        {
                            row.SetInitialAllocation(childId, match.AllocatedHours);
                        }
                    }
                }
            }

            _rowItems = validRowItems;
            dgAllocation.ItemsSource = _rowItems;
        }

        private void DgAllocation_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditingElement is TextBox textBox && e.Row.Item is AllocationRowItem row)
            {
                var bindingExpression = textBox.GetBindingExpression(TextBox.TextProperty);
                if (bindingExpression != null)
                {
                    bindingExpression.UpdateSource();

                    if (row.TotalAllocatedToChildren > row.ParentMaxBudget)
                    {
                        MessageBox.Show($"Tổng thời gian phân bổ ({row.TotalAllocatedToChildren}h) cho bài '{row.ProgramName}' vượt quá hạn mức cấp cha ({row.ParentMaxBudget}h)!",
                            "Cảnh báo Hạn Mức", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
        }

        private async void BtnAutoSchedule_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPlanId <= 0 || _selectedTimeNode == null || !_childTimeNodes.Any() || !_rowItems.Any())
            {
                MessageBox.Show("Không có dữ liệu hợp lệ (cần chọn Cấp thời gian và nạp lưới phân bổ) để chạy xếp lịch tự động.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show("Thuật toán sẽ tự động chia lại Quỹ thời gian Còn lại vào lưới hiện tại.\nDữ liệu hiện tại trên lưới sẽ bị ghi đè. Bạn có chắc chắn muốn chạy Engine?", "Xác nhận Xếp Lịch", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    _conflictLogs.Clear();

                    using var db = new AppDbContext();
                    var strategy = MilitaryTrainingApp.Services.Scheduling.AllocationEngineFactory.GetStrategy(_selectedTimeNode.NodeTypeName);

                    var parentNodeEntity = await db.TimeNodes.Include(t => t.NodeType).FirstOrDefaultAsync(t => t.Id == _selectedTimeNode.Id);
                    if (parentNodeEntity == null) return;

                    strategy = MilitaryTrainingApp.Services.Scheduling.AllocationEngineFactory.GetStrategy(parentNodeEntity.NodeType.Code);

                    await Task.Run(async () =>
                    {
                        using var backgroundDb = new AppDbContext();
                        await strategy.ExecuteAsync(
                            _currentPlanId,
                            _currentPlanTargetId,
                            parentNodeEntity,
                            _childTimeNodes,
                            _rowItems,
                            (logItem) =>
                            {
                            Dispatcher.Invoke(() =>
                            {
                                logItem.Index = _conflictLogs.Count + 1;
                                _conflictLogs.Add(logItem);
                            });
                            },
                            backgroundDb,
                            System.Threading.CancellationToken.None);
                    });

                    MessageBox.Show("Hoàn tất chạy Thuật toán Xếp lịch Tự động!", "Auto-Schedule Engine", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi thực thi Engine Xếp Lịch: {ex.Message}", "Lỗi Ngiêm Trọng", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void DgConflictLog_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgConflictLog.SelectedItem is ConflictLogItem log)
            {
                var row = _rowItems.FirstOrDefault(r => r.ProgramNodeId == log.ProgramNodeId);
                if (row != null)
                {
                    dgAllocation.ScrollIntoView(row);
                    dgAllocation.SelectedItem = row;
                    dgAllocation.Focus();
                }
            }
        }

        private async void BtnSaveAllocation_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTimeNode == null || !_childTimeNodes.Any() || !_rowItems.Any())
            {
                MessageBox.Show("Không có dữ liệu hợp lệ để lưu.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var errors = _rowItems.Where(r => r.RemainingBudget < 0).ToList();
            if (errors.Any())
            {
                var names = string.Join("\n", errors.Select(r => $"- {r.ProgramName} (Vượt {Math.Abs(r.RemainingBudget)}h)"));
                MessageBox.Show($"Không thể lưu vì các bài học sau vượt quá hạn mức cha:\n{names}", "Lỗi Phân Bổ", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                using var db = new AppDbContext();

                var childTimeNodeIds = _childTimeNodes.Select(c => c.Id).ToList();
                var programNodeIds = _rowItems.Select(r => r.ProgramNodeId).ToList();

                var existingAllocations = await db.TimeAllocations
                    .Where(ta => childTimeNodeIds.Contains(ta.TimeNodeId) && programNodeIds.Contains(ta.ProgramNodeId))
                    .ToListAsync();

                foreach (var row in _rowItems)
                {
                    foreach (var kvp in row.GetChildAllocations())
                    {
                        int childTimeNodeId = kvp.Key;
                        decimal allocatedHours = kvp.Value;

                        var existingRecord = existingAllocations.FirstOrDefault(ta => ta.TimeNodeId == childTimeNodeId && ta.ProgramNodeId == row.ProgramNodeId);

                        if (allocatedHours == 0)
                        {
                            if (existingRecord != null)
                            {
                                db.TimeAllocations.Remove(existingRecord);
                            }
                        }
                        else
                        {
                            if (existingRecord != null)
                            {
                                existingRecord.AllocatedHours = allocatedHours;
                            }
                            else
                            {
                                db.TimeAllocations.Add(new TimeAllocation
                                {
                                    ProgramNodeId = row.ProgramNodeId,
                                    TimeNodeId = childTimeNodeId,
                                    AllocatedHours = allocatedHours
                                });
                            }
                        }
                    }
                }

                await db.SaveChangesAsync();
                MessageBox.Show("Lưu phân bổ thời gian thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);

                await LoadProgramNodesAndAllocations(db);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi lưu dữ liệu: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class TimeTreeNodeItem
    {
        public int Id { get; set; }
        public int? ParentId { get; set; }
        public string? Code { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Level { get; set; }
        public string NodeTypeName { get; set; } = string.Empty;
        public List<TimeTreeNodeItem> Children { get; set; } = new List<TimeTreeNodeItem>();
    }

    public class AllocationRowItem : INotifyPropertyChanged
    {
        public int ProgramNodeId { get; set; }
        public string? ProgramCode { get; set; }
        public string ProgramName { get; set; } = string.Empty;
        public int Level { get; set; }

        public int ComplexityLevel { get; set; } = 1;
        public bool IsNightTraining { get; set; } = false;
        public bool IsOutdoor { get; set; } = false;
        public bool IsHeavyPhysical { get; set; } = false;
        public int? PrerequisiteNodeId { get; set; }

        private bool _isHighlight;
        public bool IsHighlight
        {
            get => _isHighlight;
            set { _isHighlight = value; OnPropertyChanged(nameof(IsHighlight)); }
        }

        public string BgColorHex { get; set; } = "#FFFFFF";
        public Brush BgBrush
        {
            get
            {
                try { return (Brush)new BrushConverter().ConvertFromString(BgColorHex)!; }
                catch { return Brushes.Transparent; }
            }
        }
        public FontWeight FontWeight => Level == 1 ? FontWeights.Bold : FontWeights.Normal;

        public decimal ParentMaxBudget { get; set; }

        private Dictionary<int, decimal> _childAllocations = new Dictionary<int, decimal>();

        [IndexerName("Item")]
        public string this[int childTimeNodeId]
        {
            get
            {
                if (_childAllocations.TryGetValue(childTimeNodeId, out decimal val))
                    return val == 0 ? "" : val.ToString("0.##");
                return "";
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    _childAllocations[childTimeNodeId] = 0;
                }
                else if (decimal.TryParse(value, out decimal parsedVal))
                {
                    _childAllocations[childTimeNodeId] = parsedVal;
                }

                OnPropertyChanged(Binding.IndexerName);
                OnPropertyChanged(nameof(TotalAllocatedToChildren));
                OnPropertyChanged(nameof(RemainingBudget));
            }
        }

        public Dictionary<int, decimal> GetChildAllocations() => _childAllocations;

        public void SetInitialAllocation(int childTimeNodeId, decimal value)
        {
            _childAllocations[childTimeNodeId] = value;
        }

        public decimal TotalAllocatedToChildren => _childAllocations.Values.Sum();

        public decimal RemainingBudget => ParentMaxBudget - TotalAllocatedToChildren;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ConflictLogItem
    {
        public int Index { get; set; }
        public int ProgramNodeId { get; set; }
        public string ProgramName { get; set; } = string.Empty;
        public string Status { get; set; } = "[CẦN XỬ LÝ THỦ CÔNG]";
        public string Reason { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
