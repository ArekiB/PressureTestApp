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
        private IDataService _dataService;
        private CancellationTokenSource _cts;
        private AppSettings _settings;
        private TestSession _currentSession;

        private ObservableCollection<double> _pressureValues;
        private ObservableCollection<string> _timeLabels;
        private ChartValues<double> _chartValues;

        public MainWindow()
        {
            InitializeComponent();

            _emulationService = new EmulationService();
            _dataService = new LiteDbService();
            _settings = SettingsService.LoadSettings();

            // Инициализация коллекций
            _pressureValues = new ObservableCollection<double>();
            _timeLabels = new ObservableCollection<string>();
            _chartValues = new ChartValues<double>();

            var lineSeries = new LineSeries
            {
                Title = "Давление",
                Values = _chartValues,
                PointGeometrySize = 5,
                StrokeThickness = 2,
                Fill = System.Windows.Media.Brushes.LightBlue,
                AreaLimit = 0
            };

            PressureChart.Series = new SeriesCollection { lineSeries };

            PressureChart.AxisX.Clear();
            PressureChart.AxisY.Clear();

            PressureChart.AxisX.Add(new Axis
            {
                Title = "Время",
                Labels = _timeLabels
            });

            PressureChart.AxisY.Add(new Axis
            {
                Title = "Давление (усл. ед.)",
                LabelFormatter = value => Math.Round(value, 3).ToString("F3"),
                MinValue = 0
            });

            TestNameBox.Text = _settings.LastTestName;

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

            _pressureValues.Clear();
            _timeLabels.Clear();
            _chartValues.Clear();

            // Создаём сессию испытания
            double param = _settings.LastEmulationType switch
            {
                "Static" => _settings.StaticValue,
                "Ramp" => _settings.RampStep,
                "Random" => _settings.RandomLimit,
                _ => 0
            };

            _currentSession = new TestSession
            {
                Name = TestNameBox.Text,
                StartTime = DateTime.Now,
                EmulationType = _settings.LastEmulationType,
                Param1 = param
            };
            _dataService.SaveSession(_currentSession);

            StartBtn.IsEnabled = false;
            StopBtn.IsEnabled = true;
            SettingsBtn.IsEnabled = false;
            StatusText.Text = "Испытание запущено...";

            _cts = new CancellationTokenSource();

            try
            {
                await _emulationService.StartEmulation(_settings.LastEmulationType, param, _cts.Token);

                _currentSession.EndTime = DateTime.Now;
                _currentSession.PointsCount = _pressureValues.Count;
                _dataService.SaveSession(_currentSession);

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
                CurrentPressureText.Text = pressure.ToString("F3");

                _pressureValues.Add(pressure);
                _timeLabels.Add(DateTime.Now.ToString("HH:mm:ss"));
                _chartValues.Add(pressure);

                // Сохраняем каждую точку в БД
                var measurement = new Measurement
                {
                    TestName = TestNameBox.Text,
                    Timestamp = DateTime.Now,
                    Pressure = pressure
                };
                _dataService.SaveMeasurement(measurement);

                if (PressureChart.AxisX.Count > 0)
                {
                    PressureChart.AxisX[0].Labels = _timeLabels;
                }

                if (PressureChart.AxisY.Count > 0 && _pressureValues.Count > 0)
                {
                    double maxValue = _pressureValues.Max();
                    double minValue = _pressureValues.Min();

                    PressureChart.AxisY[0].MaxValue = maxValue + (maxValue * 0.1);
                    PressureChart.AxisY[0].MinValue = Math.Max(0, minValue - (minValue * 0.1));
                }

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

        public void LoadHistoricalData(string testName)
        {
            var measurements = _dataService.GetMeasurementsByTestName(testName);

            _pressureValues.Clear();
            _timeLabels.Clear();
            _chartValues.Clear();

            foreach (var m in measurements)
            {
                _pressureValues.Add(m.Pressure);
                _chartValues.Add(m.Pressure);
                _timeLabels.Add(m.Timestamp.ToString("HH:mm:ss"));
            }

            if (PressureChart.AxisX.Count > 0)
            {
                PressureChart.AxisX[0].Labels = _timeLabels;
            }

            if (PressureChart.AxisY.Count > 0 && _pressureValues.Count > 0)
            {
                double maxValue = _pressureValues.Max();
                double minValue = _pressureValues.Min();

                PressureChart.AxisY[0].MaxValue = maxValue + (maxValue * 0.1);
                PressureChart.AxisY[0].MinValue = Math.Max(0, minValue - (minValue * 0.1));
            }

            TestNameBox.Text = testName;
            StatusText.Text = $"Загружено испытание \"{testName}\" ({_pressureValues.Count} точек)";
            PressureChart.Update();
        }
    }
}