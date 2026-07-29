using System.Windows.Controls;

namespace MilitaryTrainingApp.Views
{
    public partial class CascadeAllocationPage : Page
    {
        public CascadeAllocationPage()
        {
            InitializeComponent();
        }

        public void RefreshData(int planTargetId)
        {
            txtTargetId.Text = $"Đang tải dữ liệu phân bổ cho PlanTarget ID: {planTargetId}";
        }
    }
}
