using System.Windows;

namespace TimerAlarmPlugin
{
    public partial class DismissWindow : Window
    {
        public DismissWindow(string message)
        {
            InitializeComponent();
            MessageText.Text = message;
            Loaded += (s, e) =>
            {
                DismissButton.Focus();
            };
        }

        private void DismissButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }
    }
}