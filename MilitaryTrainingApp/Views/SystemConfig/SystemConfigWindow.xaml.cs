using System.Windows;

namespace MilitaryTrainingApp.Views.SystemConfig
{
    public partial class SystemConfigWindow : Window
    {
        public SystemConfigWindow(int planId)
        {
            InitializeComponent();

            // Phân phối planId xuống các UserControl con
            viewTargetRule.LoadData(planId);
            viewPriorityRule.LoadData(planId);
            viewBlackoutDate.LoadData(planId);
        }
    }
}
