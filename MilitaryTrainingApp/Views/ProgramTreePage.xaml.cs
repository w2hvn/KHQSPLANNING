using System.Windows.Controls;

namespace MilitaryTrainingApp.Views
{
    public partial class ProgramTreePage : Page
    {
        public ProgramTreePage()
        {
            InitializeComponent();
        }

        public void RefreshData(int planTargetId)
        {
            txtTargetId.Text = $"Đang tải dữ liệu cây chương trình cho PlanTarget ID: {planTargetId}";
        }
    }
}
