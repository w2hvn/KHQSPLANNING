using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using MilitaryTrainingApp.Entities;

namespace MilitaryTrainingApp.Views
{
    public partial class ProgramTreePage : Page
    {
        private int _currentPlanTargetId = 0;
        private bool _isAddingNew = false;
        private int? _addingParentId = null;
        private ProgramTreeNodeItem? _selectedNode = null;

        public ProgramTreePage()
        {
            InitializeComponent();
            LoadNodeTypes();
        }

        private async void LoadNodeTypes()
        {
            try
            {
                using var db = new AppDbContext();
                var nodeTypes = await db.ProgramNodeTypes.OrderBy(nt => nt.DepthLevel).ToListAsync();
                cboNodeType.ItemsSource = nodeTypes;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tải danh sách Loại Node: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void RefreshData(int planTargetId)
        {
            _currentPlanTargetId = planTargetId;
            LoadTreeData();
        }

        private async void LoadTreeData()
        {
            if (_currentPlanTargetId <= 0) return;

            try
            {
                using var db = new AppDbContext();
                // Tải dữ liệu Cây và Decor
                var rawNodes = await db.ProgramNodes
                    .Include(pn => pn.Decor)
                    .Where(pn => pn.PlanTargetId == _currentPlanTargetId)
                    .OrderBy(pn => pn.SortOrder).ThenBy(pn => pn.Id)
                    .ToListAsync();

                // Build Cây
                var treeNodes = BuildTree(rawNodes, null);

                // Update UI
                tvProgram.ItemsSource = treeNodes;

                // Xóa form sau khi tải lại
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tải Dữ liệu Cây: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private List<ProgramTreeNodeItem> BuildTree(List<ProgramNode> allNodes, int? parentId)
        {
            return allNodes
                .Where(n => n.ParentId == parentId)
                .Select(n => new ProgramTreeNodeItem
                {
                    Id = n.Id,
                    ParentId = n.ParentId,
                    Code = n.Code,
                    Name = n.Name,
                    Capacity = n.Capacity,
                    Level = n.Level,
                    NodeTypeId = n.NodeTypeId,
                    BgColorHex = n.Decor?.BgColorHex ?? "#FFFFFF",
                    BorderColorHex = n.Decor?.BorderColorHex ?? "#0066CC",
                    Children = BuildTree(allNodes, n.Id)
                })
                .ToList();
        }

        private void ClearForm()
        {
            _isAddingNew = false;
            _addingParentId = null;
            _selectedNode = null;

            txtCode.Text = string.Empty;
            txtName.Text = string.Empty;
            cboNodeType.SelectedIndex = -1;
            txtCapacity.Text = "0";
            txtBgColorHex.Text = "#FFFFFF";
            txtBorderColorHex.Text = "#0066CC";

            EnableDecorFields(true); // Mặc định mở
            btnSave.Content = "Lưu Cập Nhật";
        }

        private void EnableDecorFields(bool enable)
        {
            txtBgColorHex.IsEnabled = enable;
            txtBorderColorHex.IsEnabled = enable;
        }

        private void TvProgram_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (tvProgram.SelectedItem is ProgramTreeNodeItem node)
            {
                _isAddingNew = false;
                _addingParentId = null;
                _selectedNode = node;

                txtCode.Text = node.Code;
                txtName.Text = node.Name;
                cboNodeType.SelectedValue = node.NodeTypeId;
                txtCapacity.Text = node.Capacity.ToString("0.##");

                txtBgColorHex.Text = node.BgColorHex;
                txtBorderColorHex.Text = node.BorderColorHex;

                EnableDecorFields(node.Level == 1);
                btnSave.Content = "Lưu Cập Nhật";
            }
        }

        private void BtnAddRoot_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
            _isAddingNew = true;
            _addingParentId = null;
            EnableDecorFields(true);
            btnSave.Content = "Thêm Môn (Root)";
            txtName.Focus();
        }

        private void BtnAddChild_Click(object sender, RoutedEventArgs e)
        {
            if (tvProgram.SelectedItem is ProgramTreeNodeItem parentNode)
            {
                ClearForm();
                _isAddingNew = true;
                _addingParentId = parentNode.Id;
                EnableDecorFields(false); // Con thì không có Decor
                btnSave.Content = $"Thêm Nút Con cho [{parentNode.Name}]";
                txtName.Focus();
            }
            else
            {
                MessageBox.Show("Vui lòng chọn một Nút cha trên cây trước khi Thêm nút con.", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (tvProgram.SelectedItem is ProgramTreeNodeItem node)
            {
                var result = MessageBox.Show($"Bạn có chắc chắn muốn xóa nút '{node.Name}' và TOÀN BỘ các nút con bên trong không?", "Xác nhận Xóa", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        using var db = new AppDbContext();
                        var dbNode = await db.ProgramNodes.FindAsync(node.Id);
                        if (dbNode != null)
                        {
                            db.ProgramNodes.Remove(dbNode);
                            await db.SaveChangesAsync();
                            LoadTreeData();
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Lỗi khi xóa: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadTreeData();
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Vui lòng nhập Tên nội dung.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (cboNodeType.SelectedValue == null)
            {
                MessageBox.Show("Vui lòng chọn Loại Node.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(txtCapacity.Text, out decimal capacity))
            {
                MessageBox.Show("Chỉ tiêu (Giờ) không hợp lệ.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var db = new AppDbContext();

                if (_isAddingNew)
                {
                    // Thêm mới
                    var newNode = new ProgramNode
                    {
                        PlanTargetId = _currentPlanTargetId,
                        ParentId = _addingParentId,
                        NodeTypeId = (int)cboNodeType.SelectedValue,
                        Code = txtCode.Text,
                        Name = txtName.Text,
                        Capacity = capacity
                    };

                    db.ProgramNodes.Add(newNode);

                    // Nếu là Root (ParentId == null), tạo kèm Decor
                    if (_addingParentId == null)
                    {
                        newNode.Decor = new ProgramNodeDecor
                        {
                            BgColorHex = txtBgColorHex.Text,
                            BorderColorHex = txtBorderColorHex.Text,
                            TextColorHex = "#000000"
                        };
                    }

                    await db.SaveChangesAsync();
                    MessageBox.Show("Thêm mới thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // Cập nhật
                    if (_selectedNode == null) return;

                    var dbNode = await db.ProgramNodes
                        .Include(pn => pn.Decor)
                        .FirstOrDefaultAsync(pn => pn.Id == _selectedNode.Id);

                    if (dbNode != null)
                    {
                        dbNode.Code = txtCode.Text;
                        dbNode.Name = txtName.Text;
                        dbNode.NodeTypeId = (int)cboNodeType.SelectedValue;
                        dbNode.Capacity = capacity;

                        if (dbNode.Level == 1)
                        {
                            if (dbNode.Decor == null)
                            {
                                dbNode.Decor = new ProgramNodeDecor { ProgramNodeId = dbNode.Id };
                            }
                            dbNode.Decor.BgColorHex = txtBgColorHex.Text;
                            dbNode.Decor.BorderColorHex = txtBorderColorHex.Text;
                        }

                        await db.SaveChangesAsync();
                        MessageBox.Show("Cập nhật thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }

                LoadTreeData();
            }
            catch (DbUpdateException ex)
            {
                // Bắt lỗi Trigger từ MySQL (Lỗi dung lượng vượt quá cha)
                if (ex.InnerException != null && ex.InnerException.Message.Contains("45000"))
                {
                    MessageBox.Show("Lỗi ràng buộc khối lượng:\nTổng chỉ tiêu thời gian (Capacity) của các nút con KHÔNG ĐƯỢC VƯỢT QUÁ chỉ tiêu của nút cha.",
                        "Lỗi Khối Lượng", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    MessageBox.Show($"Lỗi khi lưu dữ liệu (DbUpdateException): {ex.InnerException?.Message ?? ex.Message}",
                        "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi không xác định: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class ProgramTreeNodeItem
    {
        public int Id { get; set; }
        public int? ParentId { get; set; }
        public string? Code { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Capacity { get; set; }
        public int Level { get; set; }
        public int NodeTypeId { get; set; }

        public string BgColorHex { get; set; } = "#FFFFFF";
        public string BorderColorHex { get; set; } = "#0066CC";

        public List<ProgramTreeNodeItem> Children { get; set; } = new List<ProgramTreeNodeItem>();

        // Helpers cho UI XAML
        public bool IsRootLevel => Level == 1;

        public Brush BgBrush
        {
            get
            {
                try { return (Brush)new BrushConverter().ConvertFromString(BgColorHex)!; }
                catch { return Brushes.Transparent; }
            }
        }
    }
}
