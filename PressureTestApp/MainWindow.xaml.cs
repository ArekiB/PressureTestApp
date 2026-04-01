using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using LiveCharts;
using LiveCharts.Wpf;
using PressureTestApp.Models;
using PressureTestApp.Services;
using PressureTestApp.Views;

namespace PressureTestApp
{
    public partial class MainWindow : Window
    {
        private EmulationService _emulationService;
        private CancellationTokenSource _cts;
        private AppSettings _settings;

        private ObservableCollection<double> _pressureValues;
        private ObservableCollection<string> _timeLabels;
        private ChartValues<double> _chartValues;

        public MainWindow()
        {
            InitializeComponent();

            _emulationService = new EmulationService();
            _settings = SettingsService.LoadSettings();

            // Инициализация коллекций
            _pressureValues = new ObservableCollection<double>();
            _timeLabels = new ObservableCollection<string>();
            _chartValues = new ChartValues<double>();

            // Создаём серию графика
            var lineSeries = new LineSeries
            {
                Title = "Давление",
                Values = _chartValues,
                PointGeometrySize = 5,
                StrokeThickness = 2,
                Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 100, 150, 255)),
                AreaLimit = 0  // Заливка от линии до нуля
            };

            PressureChart.Series = new SeriesCollection { lineSeries };

            // Очищаем оси на всякий случай
            PressureChart.AxisX.Clear();
            PressureChart.AxisY.Clear();

            // Настройка оси X
            PressureChart.AxisX.Add(new Axis
            {
                Title = "Время",
                Labels = _timeLabels
            });

            // Настройка оси Y
            PressureChart.AxisY.Add(new Axis
            {
                Title = "Давление (усл. ед.)",
                LabelFormatter = value => Math.Round(value, 3).ToString("F3"),
                MinValue = 0
            });

            // Загружаем название из настроек
            TestNameBox.Text = _settings.LastTestName;

            // Подписываемся на события
            _emulationService.PressureUpdated += OnPressureUpdated;
            SettingsBtn.Click += SettingsBtn_Click;
            StartBtn.Click += StartBtn_Click;
            StopBtn.Click += StopBtn_Click;
        }

        private async void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TestNameBox.Text))
            {
                StatusText.Text = "Введите название испытания в настройках";
                return;
            }

            _settings = SettingsService.LoadSettings();

            // Очищаем все коллекции
            _pressureValues.Clear();
            _timeLabels.Clear();
            _chartValues.Clear();

            StartBtn.IsEnabled = false;
            StopBtn.IsEnabled = true;
            SettingsBtn.IsEnabled = false;
            StatusText.Text = "Испытание запущено...";

            double param = _settings.LastEmulationType switch
            {
                "Static" => _settings.StaticValue,
                "Ramp" => _settings.RampStep,
                "Random" => _settings.RandomLimit,
                _ => 0
            };

            _cts = new CancellationTokenSource();

            try
            {
                await _emulationService.StartEmulation(_settings.LastEmulationType, param, _cts.Token);
                StatusText.Text = $"Испытание завершено. Записано {_pressureValues.Count} точек";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Ошибка: {ex.Message}";
            }
            finally
            {
                StartBtn.IsEnabled = true;
                StopBtn.IsEnabled = false;
                SettingsBtn.IsEnabled = true;
            }
        }

        private void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            StatusText.Text = "Остановка испытания...";
        }

        private void OnPressureUpdated(object sender, double pressure)
        {
            Dispatcher.Invoke(() =>
            {
                // Обновляем отображение
                CurrentPressureText.Text = pressure.ToString("F3");

                // Добавляем данные в коллекции
                _pressureValues.Add(pressure);
                _timeLabels.Add(DateTime.Now.ToString("HH:mm:ss"));
                _chartValues.Add(pressure);

                // Обновляем метки времени на оси X
                if (PressureChart.AxisX.Count > 0)
                {
                    PressureChart.AxisX[0].Labels = _timeLabels;
                }

                // Обновляем масштаб оси Y
                if (PressureChart.AxisY.Count > 0 && _pressureValues.Count > 0)
                {
                    double maxValue = _pressureValues.Max();
                    double minValue = _pressureValues.Min();

                    PressureChart.AxisY[0].MaxValue = maxValue + (maxValue * 0.1);
                    PressureChart.AxisY[0].MinValue = Math.Max(0, minValue - (minValue * 0.1));
                }

                // Принудительно обновляем график
                PressureChart.Update();
            });
        }

        private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow();
            settingsWindow.Owner = this;
            settingsWindow.ShowDialog();

            _settings = SettingsService.LoadSettings();
            TestNameBox.Text = _settings.LastTestName;
            StatusText.Text = "Настройки сохранены. Нажмите Старт для начала испытания";
            StartBtn.IsEnabled = true;
        }
    }
}