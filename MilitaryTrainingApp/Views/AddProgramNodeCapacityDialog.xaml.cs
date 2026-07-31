using MilitaryTrainingApp.Entities;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System;

namespace MilitaryTrainingApp.Views
{
    public partial class AddProgramNodeCapacityDialog : Window
    {
        private readonly int _planTargetId;
        private readonly int _timeNodeId;
        private readonly int? _parentId;

        public ProgramNode CreatedProgramNode { get; private set; } = null!;
        public ProgramNodeCapacity CreatedCapacity { get; private set; } = null!;

        public AddProgramNodeCapacityDialog(int planTargetId, int timeNodeId, int? parentId = null)
        {
            InitializeComponent();
            _planTargetId = planTargetId;
            _timeNodeId = timeNodeId;
            _parentId = parentId;
            LoadData();
        }

        private void LoadData()
        {
            using var context = new AppDbContext();

            // Load Node Types
            cboNodeType.ItemsSource = context.ProgramNodeTypes.ToList();
            if (cboNodeType.Items.Count > 0)
                cboNodeType.SelectedIndex = 0;

            // Load Facility Types
            var facilities = context.Set<FacilityType>().ToList();
            cboFacility.ItemsSource = facilities;

            // Load Prerequisite Nodes
            var preNodes = context.ProgramNodes
                                  .Where(n => n.PlanTargetId == _planTargetId)
                                  .ToList();
            cboPrerequisite.ItemsSource = preNodes;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Vui lòng nhập tên bài học.", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(txtMasterCapacity.Text, out decimal masterCapacity))
            {
                MessageBox.Show("Master Capacity không hợp lệ.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!decimal.TryParse(txtSubCapacity.Text, out decimal subCapacity))
            {
                MessageBox.Show("Sub-Capacity không hợp lệ.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (subCapacity > masterCapacity)
            {
                MessageBox.Show("Sub-Capacity không được lớn hơn Master Capacity.", "Lỗi Cấu trúc Dual-Level", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            using var context = new AppDbContext();
            using var transaction = context.Database.BeginTransaction();

            try
            {
                // Create ProgramNode
                var newNode = new ProgramNode
                {
                    PlanTargetId = _planTargetId,
                    ParentId = _parentId,
                    NodeTypeId = (int)cboNodeType.SelectedValue,
                    Code = txtCode.Text,
                    Name = txtName.Text,
                    Capacity = masterCapacity, // Master capacity
                    ComplexityLevel = int.Parse(((ComboBoxItem)cboComplexity.SelectedItem).Tag.ToString() ?? "0"),
                    IsNightTraining = chkNight.IsChecked ?? false,
                    IsOutdoor = chkOutdoor.IsChecked ?? false,
                    IsHeavyPhysical = chkHeavy.IsChecked ?? false,
                    RequiresField = chkField.IsChecked ?? false,
                    FacilityTypeId = cboFacility.SelectedValue as int?,
                    PrerequisiteNodeId = cboPrerequisite.SelectedValue as int?
                };

                context.ProgramNodes.Add(newNode);
                context.SaveChanges(); // to get Id

                // Ensure ProgramNodeCapacity table entry (Sub-capacity allocation for the specific time node)
                var newCapacity = new ProgramNodeCapacity
                {
                    ProgramNodeId = newNode.Id,
                    TimeNodeId = _timeNodeId,
                    AllocatedCapacity = subCapacity,
                    IsManual = true
                };

                context.Set<ProgramNodeCapacity>().Add(newCapacity);
                context.SaveChanges();

                transaction.Commit();

                CreatedProgramNode = newNode;
                CreatedCapacity = newCapacity;

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                MessageBox.Show("Lỗi khi lưu dữ liệu:\n" + ex.Message + (ex.InnerException != null ? "\n" + ex.InnerException.Message : ""), "Lỗi System", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
