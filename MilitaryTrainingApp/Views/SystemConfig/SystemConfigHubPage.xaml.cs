using System.Windows.Controls;

namespace MilitaryTrainingApp.Views.SystemConfig
{
    public partial class SystemConfigHubPage : Page
    {
        private int _currentPlanId = 0;

        public SystemConfigHubPage()
        {
            InitializeComponent();
        }

        public void RefreshData(int planId)
        {
            if (planId <= 0 || planId == _currentPlanId) return;

            _currentPlanId = planId;

            // Route planId down to the embedded UserControls
            viewTargetRule.LoadData(_currentPlanId);
            viewPriorityRule.LoadData(_currentPlanId);
            viewBlackoutDate.LoadData(_currentPlanId);
        }
    }
}
