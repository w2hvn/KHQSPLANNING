using System.Windows;

namespace MilitaryTrainingApp.Views
{
    public partial class ConflictResolverWindow : Window
    {
        public int SelectedOption { get; private set; } = 3; // Mặc định tự nhập

        public ConflictResolverWindow(string subjectName, decimal budget, int currentIndex, int totalItems)
        {
            InitializeComponent();
            int percent = totalItems > 0 ? (int)((double)currentIndex / totalItems * 100) : 0;
            txtTitle.Text = $"Phát hiện phân vân tại Môn: {subjectName} (Tổng {budget}h) - Tiến độ: {currentIndex}/{totalItems} ({percent}%)";
            pbProgress.Value = percent;
        }

        private void BtnOption1_Click(object sender, RoutedEventArgs e)
        {
            SelectedOption = 1;
            this.DialogResult = true;
            this.Close();
        }

        private void BtnOption2_Click(object sender, RoutedEventArgs e)
        {
            SelectedOption = 2;
            this.DialogResult = true;
            this.Close();
        }

        private void BtnOption3_Click(object sender, RoutedEventArgs e)
        {
            SelectedOption = 3;
            this.DialogResult = true;
            this.Close();
        }
    }
}
