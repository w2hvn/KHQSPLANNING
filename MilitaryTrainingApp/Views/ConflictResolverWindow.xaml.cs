using System.Collections.Generic;
using System.Windows;
using MilitaryTrainingApp.Entities;

namespace MilitaryTrainingApp.Views
{
    public partial class ConflictResolverWindow : Window
    {
        public ProgramNode? SelectedNode { get; private set; }

        public ConflictResolverWindow(List<ProgramNode> conflictNodes, string timeContext)
        {
            InitializeComponent();
            txtMessage.Text = $"Quỹ thời gian của '{timeContext}' đã cạn nhưng có {conflictNodes.Count} nội dung có cùng điểm ưu tiên. Vui lòng chọn nội dung được xếp trước:";

            var options = new List<ConflictOption>();
            foreach (var node in conflictNodes)
            {
                options.Add(new ConflictOption { Node = node, DisplayText = $"[{node.Code}] {node.Name}" });
            }
            lstOptions.ItemsSource = options;
            lstOptions.SelectedIndex = 0;
        }

        private void BtnContinue_Click(object sender, RoutedEventArgs e)
        {
            if (lstOptions.SelectedItem is ConflictOption option)
            {
                SelectedNode = option.Node;
                this.DialogResult = true;
                this.Close();
            }
        }
    }

    public class ConflictOption
    {
        public ProgramNode Node { get; set; } = null!;
        public string DisplayText { get; set; } = string.Empty;
    }
}
