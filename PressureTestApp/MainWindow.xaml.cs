using System.Windows;
using PressureTestApp.Views;

namespace PressureTestApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow();
            settingsWindow.Owner = this;
            settingsWindow.ShowDialog();

            TxtStatus.Text = "Настройки сохранены. Нажмите Старт для начала испытания";
            BtnStart.IsEnabled = true;
        }
    }
}