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

            // Lắng nghe sự kiện từ HeaderControl
            TopHeaderControl.GlobalContextChanged += TopHeaderControl_GlobalContextChanged;

            // Trang mặc định lúc mới khởi chạy
            mainFrame.Navigate(_programTreePage);
        }

        private void TopHeaderControl_GlobalContextChanged(object? sender, Controls.GlobalContextEventArgs e)
        {
            _currentPlanTargetId = e.PlanTargetId;
            _currentTimeNodeId = e.TimeNodeId;

            // Fix WPF Navigation Lifecycle: Trực tiếp gọi hàm trên instance của trang
            _programTreePage.RefreshData(_currentPlanTargetId);
            _cascadeAllocationPage.RefreshData(_currentPlanTargetId);
            _timelineReportPage.RefreshData(_currentPlanTargetId);

            // Lưu ý: Các Page bên dưới sẽ cần cập nhật sau nếu muốn xài TimeNodeId để lọc sâu hơn
            // Hiện tại ta chỉ cập nhật anchor _currentPlanTargetId cho chúng như cũ.
        }

        private void BtnProgramTree_Click(object sender, RoutedEventArgs e)
        {
            mainFrame.Navigate(_programTreePage);
        }

        private void BtnCascade_Click(object sender, RoutedEventArgs e)
        {
            mainFrame.Navigate(_cascadeAllocationPage);
        }

        private void BtnTimelineReport_Click(object sender, RoutedEventArgs e)
        {
            mainFrame.Navigate(_timelineReportPage);
        }

        private void BtnPlanManagement_Click(object sender, RoutedEventArgs e)
        {
            mainFrame.Navigate(_planManagementPage);
        }

        private void BtnTrainingTarget_Click(object sender, RoutedEventArgs e)
        {
            mainFrame.Navigate(_trainingTargetPage);
        }

        private void BtnTimeTree_Click(object sender, RoutedEventArgs e)
        {
            mainFrame.Navigate(_timeTreeManagementPage);
        }
    }
}
