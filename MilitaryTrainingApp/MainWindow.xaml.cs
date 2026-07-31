using System.Windows;
using MilitaryTrainingApp.Views;

namespace MilitaryTrainingApp
{
    public partial class MainWindow : Window
    {
        private ProgramTreePage _programTreePage;
        private CascadeAllocationPage _cascadeAllocationPage;
        private TimelineReportPage _timelineReportPage;

        private PlanManagementPage _planManagementPage;
        private TrainingTargetPage _trainingTargetPage;
        private TimeTreeManagementPage _timeTreeManagementPage;

        private ProgressReportPage _progressReportPage;
        private ExcelExportPage _excelExportPage;
        private Views.SystemConfig.SystemConfigHubPage _systemConfigHubPage;

        private int _currentPlanId = 0;
        private int _currentPlanTargetId = 0;
        private int? _currentTimeNodeId = null;

        public MainWindow()
        {
            InitializeComponent();

            // Khởi tạo các trang
            _programTreePage = new ProgramTreePage();
            _cascadeAllocationPage = new CascadeAllocationPage();
            _timelineReportPage = new TimelineReportPage();

            _planManagementPage = new PlanManagementPage();
            _trainingTargetPage = new TrainingTargetPage();
            _timeTreeManagementPage = new TimeTreeManagementPage();

            _progressReportPage = new ProgressReportPage();
            _excelExportPage = new ExcelExportPage();
            _systemConfigHubPage = new Views.SystemConfig.SystemConfigHubPage();

            // Lắng nghe sự kiện từ HeaderControl
            TopHeaderControl.GlobalContextChanged += TopHeaderControl_GlobalContextChanged;

            // Trang mặc định lúc mới khởi chạy
            mainFrame.Navigate(_programTreePage);
        }

        private void TopHeaderControl_GlobalContextChanged(object? sender, Controls.GlobalContextEventArgs e)
        {
            _currentPlanId = e.PlanId;
            _currentPlanTargetId = e.PlanTargetId;
            _currentTimeNodeId = e.TimeNodeId;

            // Cập nhật dữ liệu cho các trang (Pages)
            _programTreePage.RefreshData(_currentPlanTargetId);
            _cascadeAllocationPage.RefreshData(_currentPlanTargetId, _currentTimeNodeId);
            _timelineReportPage.RefreshData(_currentPlanTargetId);

            // Cập nhật Hub Cấu hình
            _systemConfigHubPage.RefreshData(_currentPlanId);
        }

        // --- SIDEBAR NAVIGATION HANDLERS ---

        // NHÓM 1
        private void Nav_PlanManagement_Click(object sender, RoutedEventArgs e)
        {
            _planManagementPage.RefreshData();
            mainFrame.Navigate(_planManagementPage);
        }

        private void Nav_TrainingTarget_Click(object sender, RoutedEventArgs e)
        {
            _trainingTargetPage.RefreshData();
            mainFrame.Navigate(_trainingTargetPage);
        }

        private void Nav_TimeTree_Click(object sender, RoutedEventArgs e)
        {
            _timeTreeManagementPage.RefreshData();
            mainFrame.Navigate(_timeTreeManagementPage);
        }

        private void Nav_ProgramTree_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPlanTargetId > 0)
                _programTreePage.RefreshData(_currentPlanTargetId);
            mainFrame.Navigate(_programTreePage);
        }

        // NHÓM 2
        private void Nav_CascadeAllocation_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPlanTargetId > 0)
                _cascadeAllocationPage.RefreshData(_currentPlanTargetId, _currentTimeNodeId);
            mainFrame.Navigate(_cascadeAllocationPage);
        }

        // NHÓM 3
        private void Nav_TimelineReport_Click(object sender, RoutedEventArgs e)
        {
            mainFrame.Navigate(_timelineReportPage);
        }

        private void Nav_ProgressReport_Click(object sender, RoutedEventArgs e)
        {
            mainFrame.Navigate(_progressReportPage);
        }

        private void Nav_ExcelExport_Click(object sender, RoutedEventArgs e)
        {
            mainFrame.Navigate(_excelExportPage);
        }

        // NHÓM 4
        private void Nav_SystemConfig_Click(object sender, RoutedEventArgs e)
        {
            mainFrame.Navigate(_systemConfigHubPage);
        }
    }
}
