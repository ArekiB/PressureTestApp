using System.Windows;
using PressureTestApp.Models;
using PressureTestApp.Services;

namespace PressureTestApp.Views
{
    public partial class SettingsWindow : Window
    {
        private AppSettings _settings;

        public SettingsWindow()
        {
            InitializeComponent();

            // Загружаем настройки
            _settings = SettingsService.LoadSettings();

            // Заполняем поля
            TestNameBox.Text = _settings.LastTestName;

            // Заполняем ComboBox
            TypeCombo.Items.Add("Static");
            TypeCombo.Items.Add("Ramp");
            TypeCombo.Items.Add("Random");
            TypeCombo.SelectedItem = _settings.LastEmulationType;

            // Устанавливаем значения
            StaticValueBox.Text = _settings.StaticValue.ToString();
            RampStepBox.Text = _settings.RampStep.ToString();
            RandomLimitBox.Text = _settings.RandomLimit.ToString();

            // Подписываемся на события
            TypeCombo.SelectionChanged += TypeCombo_SelectionChanged;
            SaveBtn.Click += SaveBtn_Click;
            CancelBtn.Click += CancelBtn_Click;

            // Обновляем видимость панелей
            UpdatePanels();
        }

        private void TypeCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdatePanels();
        }

        private void UpdatePanels()
        {
            string selected = TypeCombo.SelectedItem?.ToString() ?? "Static";

            StaticPanel.Visibility = selected == "Static" ? Visibility.Visible : Visibility.Collapsed;
            RampPanel.Visibility = selected == "Ramp" ? Visibility.Visible : Visibility.Collapsed;
            RandomPanel.Visibility = selected == "Random" ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            // Сохраняем настройки
            _settings.LastTestName = TestNameBox.Text;
            _settings.LastEmulationType = TypeCombo.SelectedItem?.ToString() ?? "Static";

            // Парсим значения
            double.TryParse(StaticValueBox.Text, out double staticVal);
            double.TryParse(RampStepBox.Text, out double rampStep);
            double.TryParse(RandomLimitBox.Text, out double randomLimit);

            _settings.StaticValue = staticVal;
            _settings.RampStep = rampStep;
            _settings.RandomLimit = randomLimit;

            // Сохраняем в JSON
            SettingsService.SaveSettings(_settings);

            DialogResult = true;
            Close();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}