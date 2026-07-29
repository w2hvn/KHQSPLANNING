using System.Windows;
using MilitaryTrainingApp.Views;

namespace MilitaryTrainingApp
{
    public partial class MainWindow : Window
    {
        private ProgramTreePage _programTreePage;
        private CascadeAllocationPage _cascadeAllocationPage;
        private int _currentPlanTargetId = 0;

        public MainWindow()
        {
            InitializeComponent();

            // Khởi tạo các trang
            _programTreePage = new ProgramTreePage();
            _cascadeAllocationPage = new CascadeAllocationPage();

            // Lắng nghe sự kiện từ HeaderControl
            TopHeaderControl.OnPlanTargetChanged += TopHeaderControl_OnPlanTargetChanged;

            // Trang mặc định lúc mới khởi chạy
            mainFrame.Navigate(_programTreePage);
        }

        private void TopHeaderControl_OnPlanTargetChanged(object? sender, int planTargetId)
        {
            _currentPlanTargetId = planTargetId;
            RefreshCurrentPage();
        }

        private void BtnProgramTree_Click(object sender, RoutedEventArgs e)
        {
            mainFrame.Navigate(_programTreePage);
            RefreshCurrentPage();
        }

        private void BtnCascade_Click(object sender, RoutedEventArgs e)
        {
            mainFrame.Navigate(_cascadeAllocationPage);
            RefreshCurrentPage();
        }

        private void RefreshCurrentPage()
        {
            if (_currentPlanTargetId <= 0) return;

            if (mainFrame.Content is ProgramTreePage treePage)
            {
                treePage.RefreshData(_currentPlanTargetId);
            }
            else if (mainFrame.Content is CascadeAllocationPage cascadePage)
            {
                cascadePage.RefreshData(_currentPlanTargetId);
            }
        }
    }
}
