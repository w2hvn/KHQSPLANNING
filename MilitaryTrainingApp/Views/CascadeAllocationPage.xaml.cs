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
        private TimeTreeNodeItem? _selectedTimeNode = null;
        private List<TimeTreeNodeItem> _childTimeNodes = new List<TimeTreeNodeItem>();
        private List<AllocationRowItem> _rowItems = new List<AllocationRowItem>();
        private System.Collections.ObjectModel.ObservableCollection<ConflictLogItem> _conflictLogs = new System.Collections.ObjectModel.ObservableCollection<ConflictLogItem>();

        public CascadeAllocationPage()
        {
            InitializeComponent();
        }

        public void RefreshData(int planTargetId)
        {
            _currentPlanTargetId = planTargetId;
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

                txtParentNodeName.Text = "---";
                txtChildLevelName.Text = "---";
                _rowItems.Clear();
                dgAllocation.ItemsSource = null;
                ClearDynamicColumns();

                _conflictLogs.Clear();
                lstConflictLogs.ItemsSource = _conflictLogs;
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
                _selectedTimeNode = selectedNode;
                txtParentNodeName.Text = _selectedTimeNode.Name;

                try
                {
                    using var db = new AppDbContext();

                    // 1. Lấy danh sách các TimeNode CON trực tiếp
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

                    // 2. Build Cột Động cho DataGrid
                    BuildDynamicColumns();

                    // 3. Tải danh sách Bài Học và tính toán Ngân sách
                    await LoadProgramNodesAndAllocations(db);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi tải dữ liệu lưới: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
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

            // Lấy toàn bộ cây bài học của PlanTarget này
            var allProgramNodes = await db.ProgramNodes
                .Include(pn => pn.Decor)
                .Where(pn => pn.PlanTargetId == _currentPlanTargetId)
                .OrderBy(pn => pn.SortOrder).ThenBy(pn => pn.Id)
                .ToListAsync();

            var validRowItems = new List<AllocationRowItem>();

            // Nếu TimeNode Cha đang chọn ở Level 1 (NĂM) -> Hạn mức lấy từ Capacity của ProgramNode
            if (_selectedTimeNode.Level == 1)
            {
                foreach (var pn in allProgramNodes)
                {
                    // Lấy môn học có Capacity > 0, hoặc nút con có Capacity > 0 (nhưng yêu cầu quy định Root luôn hiển thị làm context)
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
                            ParentMaxBudget = pn.Capacity // Nguồn ngân sách gốc
                        });
                    }
                }
            }
            else
            {
                // Nếu TimeNode Cha ở Level > 1 -> Hạn mức lấy từ TimeAllocation của Node Cha này
                var parentAllocations = await db.TimeAllocations
                    .Where(ta => ta.TimeNodeId == _selectedTimeNode.Id && ta.AllocatedHours > 0)
                    .ToListAsync();

                var allocatedProgramIds = parentAllocations.Select(ta => ta.ProgramNodeId).ToHashSet();

                foreach (var pn in allProgramNodes)
                {
                    // Luôn giữ Root (Level 1) làm Context, HOẶC bài học đó đã được phân bổ giờ vào TimeNode Cha
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

            // Tiếp theo, lấy các bản ghi phân bổ ĐÃ CÓ cho các TimeNode CON
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
            // Update bindings immediately
            if (e.EditingElement is TextBox textBox && e.Row.Item is AllocationRowItem row)
            {
                var bindingExpression = textBox.GetBindingExpression(TextBox.TextProperty);
                if (bindingExpression != null)
                {
                    bindingExpression.UpdateSource();

                    // Logic Kiểm Tra Realtime
                    if (row.TotalAllocatedToChildren > row.ParentMaxBudget)
                    {
                        MessageBox.Show($"Tổng thời gian phân bổ ({row.TotalAllocatedToChildren}h) cho bài '{row.ProgramName}' vượt quá hạn mức cấp cha ({row.ParentMaxBudget}h)!",
                            "Cảnh báo Hạn Mức", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
        }

        private void BtnSystemConfig_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPlanId <= 0)
            {
                MessageBox.Show("Vui lòng chọn Kế hoạch trước.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var win = new SystemConfig.SystemConfigWindow(_currentPlanId);
            win.ShowDialog();
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
                    var strategy = MilitaryTrainingApp.Services.Scheduling.AllocationEngineFactory.GetStrategy(_selectedTimeNode.NodeTypeName); // node type code needed

                    // Lấy TimeNode Entities gốc để query chính xác TimeNodeLevel Code
                    var parentNodeEntity = await db.TimeNodes.Include(t => t.NodeType).FirstOrDefaultAsync(t => t.Id == _selectedTimeNode.Id);
                    if (parentNodeEntity == null) return;

                    strategy = MilitaryTrainingApp.Services.Scheduling.AllocationEngineFactory.GetStrategy(parentNodeEntity.NodeType.Code);

                    // Chạy Auto Schedule Engine (Bất đồng bộ trên Background Thread để UI không bị block)
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
                                Dispatcher.Invoke(() => _conflictLogs.Add(logItem));
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

        private void LstConflictLogs_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (lstConflictLogs.SelectedItem is ConflictLogItem log)
            {
                var row = _rowItems.FirstOrDefault(r => r.ProgramNodeId == log.ProgramNodeId);
                if (row != null)
                {
                    dgAllocation.ScrollIntoView(row);
                    dgAllocation.SelectedItem = row;
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

            // Kiểm tra validate trước khi lưu (tránh lưu nếu bị âm ngân sách)
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

                // Tải tất cả bản ghi phân bổ hiện tại của các ô trên lưới
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
                            // ZERO-VALUE ELIMINATION: Nếu value = 0 và đã có trong DB thì XÓA
                            if (existingRecord != null)
                            {
                                db.TimeAllocations.Remove(existingRecord);
                            }
                        }
                        else
                        {
                            // UPSERT
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

                // Load lại để đồng bộ state hoàn hảo
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

        public bool IsHighlight { get; set; } = false;

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
        public int ProgramNodeId { get; set; }
        public string ProgramName { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }
}
