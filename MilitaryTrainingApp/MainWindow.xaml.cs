using System.Windows;
using MilitaryTrainingApp.Views;

namespace MilitaryTrainingApp
{
    public partial class MainWindow : Window
    {
        private ProgramTreePage _programTreePage;
        private CascadeAllocationPage _cascadeAllocationPage;
        private TimelineReportPage _timelineReportPage;
        private int _currentPlanTargetId = 0;

        public MainWindow()
        {
            InitializeComponent();

            // Khởi tạo các trang
            _programTreePage = new ProgramTreePage();
            _cascadeAllocationPage = new CascadeAllocationPage();
            _timelineReportPage = new TimelineReportPage();

            // Lắng nghe sự kiện từ HeaderControl
            TopHeaderControl.OnPlanTargetChanged += TopHeaderControl_OnPlanTargetChanged;

            // Trang mặc định lúc mới khởi chạy
            mainFrame.Navigate(_programTreePage);
        }

        private void TopHeaderControl_OnPlanTargetChanged(object? sender, int planTargetId)
        {
            _currentPlanTargetId = planTargetId;

            // Fix WPF Navigation Lifecycle: Trực tiếp gọi hàm trên instance của trang
            _programTreePage.RefreshData(_currentPlanTargetId);
            _cascadeAllocationPage.RefreshData(_currentPlanTargetId);
            _timelineReportPage.RefreshData(_currentPlanTargetId);
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
    }
}
