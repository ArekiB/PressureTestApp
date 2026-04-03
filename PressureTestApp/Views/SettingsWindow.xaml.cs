using System;
using System.Windows;
using System.Windows.Controls;
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

            _settings = SettingsService.LoadSettings();

            // Заполняем поля
            TestNameBox.Text = _settings.LastTestName;

            // Выбираем тип эмуляции
            if (_settings.LastEmulationType == "Static")
                TypeCombo.SelectedIndex = 0;
            else if (_settings.LastEmulationType == "Ramp")
                TypeCombo.SelectedIndex = 1;
            else
                TypeCombo.SelectedIndex = 0;

            StaticValueBox.Text = _settings.StaticValue.ToString();
            RampStepBox.Text = _settings.RampStep.ToString();

            // Modbus настройки
            UseModbusCheck.IsChecked = _settings.UseModbus;
            ModbusIpBox.Text = _settings.ModbusIpAddress;
            ModbusPortBox.Text = _settings.ModbusPort.ToString();
            ModbusSlaveIdBox.Text = _settings.ModbusSlaveId.ToString();
            ModbusAddressBox.Text = _settings.ModbusRegisterAddress.ToString();

            // Выбираем тип данных
            switch (_settings.ModbusDataType)
            {
                case "float": ModbusTypeCombo.SelectedIndex = 0; break;
                case "int": ModbusTypeCombo.SelectedIndex = 1; break;
                case "bool": ModbusTypeCombo.SelectedIndex = 2; break;
                case "string": ModbusTypeCombo.SelectedIndex = 3; break;
                default: ModbusTypeCombo.SelectedIndex = 0; break;
            }

            ModbusScaleBox.Text = _settings.ModbusScale.ToString();

            // Подписываемся на события
            TypeCombo.SelectionChanged += TypeCombo_SelectionChanged;
            UseModbusCheck.Checked += UseModbusCheck_Changed;
            UseModbusCheck.Unchecked += UseModbusCheck_Changed;
            SaveBtn.Click += SaveBtn_Click;
            CancelBtn.Click += CancelBtn_Click;
            TestModbusBtn.Click += TestModbusBtn_Click;

            UpdatePanels();
            UpdateEmulationPanelVisibility();
        }

        private void TypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdatePanels();
        }

        private void UpdatePanels()
        {
            string selected = (TypeCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Static";

            StaticPanel.Visibility = selected == "Static" ? Visibility.Visible : Visibility.Collapsed;
            RampPanel.Visibility = selected == "Ramp" ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UseModbusCheck_Changed(object sender, RoutedEventArgs e)
        {
            UpdateEmulationPanelVisibility();
        }

        private void UpdateEmulationPanelVisibility()
        {
            bool useModbus = UseModbusCheck.IsChecked ?? false;
            EmulationPanel.Visibility = useModbus ? Visibility.Collapsed : Visibility.Visible;
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            // Сохраняем эмуляцию
            _settings.LastTestName = TestNameBox.Text;
            _settings.LastEmulationType = (TypeCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Static";

            double.TryParse(StaticValueBox.Text, out double staticVal);
            double.TryParse(RampStepBox.Text, out double rampStep);

            _settings.StaticValue = staticVal;
            _settings.RampStep = rampStep;

            // Сохраняем Modbus настройки
            _settings.UseModbus = UseModbusCheck.IsChecked ?? false;
            _settings.ModbusIpAddress = ModbusIpBox.Text;

            int.TryParse(ModbusPortBox.Text, out int port);
            _settings.ModbusPort = port == 0 ? 502 : port;

            int.TryParse(ModbusSlaveIdBox.Text, out int slaveId);
            _settings.ModbusSlaveId = slaveId == 0 ? 1 : slaveId;

            int.TryParse(ModbusAddressBox.Text, out int addr);
            _settings.ModbusRegisterAddress = addr;

            string dataType = (ModbusTypeCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "float";
            _settings.ModbusDataType = dataType;

            double.TryParse(ModbusScaleBox.Text, out double scale);
            _settings.ModbusScale = scale == 0 ? 1 : scale;

            SettingsService.SaveSettings(_settings);

            DialogResult = true;
            Close();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async void TestModbusBtn_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Тест Modbus...";

            var modbusService = new ModbusService(
                ModbusIpBox.Text,
                int.Parse(ModbusPortBox.Text),
                int.Parse(ModbusSlaveIdBox.Text));

            await modbusService.ConnectAsync();

            if (modbusService.IsConnected)
            {
                int address = int.Parse(ModbusAddressBox.Text);
                string dataType = (ModbusTypeCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "float";
                double scale = double.Parse(ModbusScaleBox.Text);

                double value = await modbusService.ReadValueAsync(address, dataType, scale);
                MessageBox.Show($"Прочитано значение: {value:F3}", "Результат теста");
                StatusText.Text = $"Тест завершён: {value:F3}";
            }
            else
            {
                MessageBox.Show("Не удалось подключиться к Modbus", "Ошибка");
                StatusText.Text = "Ошибка подключения";
            }

            modbusService.Disconnect();
        }
    }
}