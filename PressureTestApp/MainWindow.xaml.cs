using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
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
        private ModbusService _modbusService;
        private CancellationTokenSource _cts;
        private AppSettings _settings;
        private TestSession _currentSession;

        // Данные для графика
        private ObservableCollection<double> _pressureValues;
        private ObservableCollection<string> _timeLabels;
        private ChartValues<double> _chartValues;
        private DateTime _testStartTime;

        // Статистика
        private List<double> _allValues;
        private double _currentMax;
        private double _currentMin;
        private double _currentSum;
        private int _currentCount;

        // Modbus таймер
        private DispatcherTimer _modbusTimer;
        private bool _isModbusMode = false;

        public MainWindow()
        {
            InitializeComponent();

            _emulationService = new EmulationService();
            _dataService = new LiteDbService();
            _settings = SettingsService.LoadSettings();

            // Инициализация Modbus
            _modbusService = new ModbusService(
                _settings.ModbusIpAddress,
                _settings.ModbusPort,
                _settings.ModbusSlaveId);
            _modbusService.StatusChanged += OnModbusStatusChanged;

            // Инициализация коллекций
            _pressureValues = new ObservableCollection<double>();
            _timeLabels = new ObservableCollection<string>();
            _chartValues = new ChartValues<double>();
            _allValues = new List<double>();

            // Настройка графика
            var lineSeries = new LineSeries
            {
                Title = "Давление",
                Values = _chartValues,
                PointGeometrySize = 5,
                StrokeThickness = 2,
                Fill = Brushes.LightBlue,
                AreaLimit = 0
            };

            PressureChart.Series = new SeriesCollection { lineSeries };

            // Оси
            PressureChart.AxisX.Clear();
            PressureChart.AxisY.Clear();

            PressureChart.AxisX.Add(new Axis
            {
                Title = "Время (сек)",
                LabelFormatter = value => TimeSpan.FromSeconds(value).ToString(@"mm\:ss")
            });

            PressureChart.AxisY.Add(new Axis
            {
                Title = "Давление (усл. ед.)",
                LabelFormatter = value => Math.Round(value, 3).ToString("F3"),
                MinValue = 0
            });

            TestNameBox.Text = _settings.LastTestName;

            // Modbus таймер
            _modbusTimer = new DispatcherTimer();
            _modbusTimer.Interval = TimeSpan.FromMilliseconds(1000);
            _modbusTimer.Tick += ModbusTimer_Tick;

            // Подписки
            _emulationService.PressureUpdated += OnPressureUpdated;
            SettingsBtn.Click += SettingsBtn_Click;
            StartBtn.Click += StartBtn_Click;
            StopBtn.Click += StopBtn_Click;
            HistoryBtn.Click += HistoryBtn_Click;

            UpdateConnectionUI();

            if (_settings.UseModbus)
            {
                _ = _modbusService.ConnectAsync();
            }
        }

        private void OnModbusStatusChanged(object sender, ConnectionStatus status)
        {
            Dispatcher.Invoke(() => UpdateConnectionUI());
        }

        private void UpdateConnectionUI()
        {
            switch (_modbusService.Status)
            {
                case ConnectionStatus.Connected:
                    ConnectionIndicator.Fill = Brushes.Green;
                    ConnectionStatusText.Text = "Connected";
                    ConnectionStatusText.Foreground = Brushes.Green;
                    break;
                case ConnectionStatus.Connecting:
                    ConnectionIndicator.Fill = Brushes.Orange;
                    ConnectionStatusText.Text = "Connecting...";
                    ConnectionStatusText.Foreground = Brushes.Orange;
                    break;
                case ConnectionStatus.Timeout:
                    ConnectionIndicator.Fill = Brushes.Orange;
                    ConnectionStatusText.Text = "Timeout";
                    ConnectionStatusText.Foreground = Brushes.Orange;
                    break;
                case ConnectionStatus.Error:
                    ConnectionIndicator.Fill = Brushes.Red;
                    ConnectionStatusText.Text = "Error";
                    ConnectionStatusText.Foreground = Brushes.Red;
                    break;
                default:
                    ConnectionIndicator.Fill = Brushes.Red;
                    ConnectionStatusText.Text = "Disconnected";
                    ConnectionStatusText.Foreground = Brushes.Red;
                    break;
            }
        }

        private void SetDebug(string msg)
        {
            Dispatcher.Invoke(() =>
            {
                string currentText = DebugText.Text;
                string newText = $"{DateTime.Now:HH:mm:ss} - {msg}\n" + currentText;
                var lines = newText.Split('\n');
                if (lines.Length > 20)
                {
                    newText = string.Join("\n", lines.Take(20));
                }
                DebugText.Text = newText;
                System.Diagnostics.Debug.WriteLine(msg);
            });
        }

        private void AddPressurePoint(double pressure)
        {
            Dispatcher.Invoke(() =>
            {
                // Пропускаем падающие точки при остановке Ramp
                if (_currentSession != null && _currentSession.EmulationType == "Ramp" && _pressureValues.Count > 0)
                {
                    double lastPressure = _pressureValues.Last();
                    if (pressure < lastPressure)
                    {
                        System.Diagnostics.Debug.WriteLine($"Ramp: пропущена точка {pressure} < {lastPressure}");
                        return;
                    }
                }

                double elapsedSeconds = (DateTime.Now - _testStartTime).TotalSeconds;
                elapsedSeconds = Math.Max(0, Math.Round(elapsedSeconds, 2));

                CurrentPressureText.Text = pressure.ToString("F3");

                _pressureValues.Add(pressure);
                _chartValues.Add(pressure);
                _timeLabels.Add(elapsedSeconds.ToString("F2"));

                // Статистика
                _allValues.Add(pressure);
                _currentCount++;
                _currentSum += pressure;

                if (pressure > _currentMax) _currentMax = pressure;
                if (pressure < _currentMin) _currentMin = pressure;

                MaxValueText.Text = _currentMax.ToString("F3");
                MinValueText.Text = _currentMin.ToString("F3");
                AvgValueText.Text = (_currentSum / _currentCount).ToString("F3");

                // Ось X
                if (PressureChart.AxisX.Count > 0)
                {
                    PressureChart.AxisX[0].Labels = _timeLabels;
                }

                // Ось Y
                if (PressureChart.AxisY.Count > 0)
                {
                    if (_pressureValues.Count > 0)
                    {
                        double maxValue = _pressureValues.Max();
                        double minValue = _pressureValues.Min();

                        if (Math.Abs(maxValue - minValue) < 0.001)
                        {
                            maxValue = minValue + 10;
                            minValue = Math.Max(0, minValue - 10);
                        }

                        PressureChart.AxisY[0].MaxValue = maxValue + (maxValue * 0.1);
                        PressureChart.AxisY[0].MinValue = Math.Max(0, minValue - (minValue * 0.1));
                    }
                    else
                    {
                        PressureChart.AxisY[0].MaxValue = 100;
                        PressureChart.AxisY[0].MinValue = 0;
                    }
                }

                // Сохранение в БД
                if (_currentSession != null)
                {
                    var measurement = new Measurement
                    {
                        SessionId = _currentSession.Id,
                        TestName = _currentSession.Name,
                        Timestamp = DateTime.Now,
                        Pressure = pressure
                    };
                    _dataService.SaveMeasurement(measurement);
                }

                PressureChart.Update();
            });
        }

        private async void ModbusTimer_Tick(object sender, EventArgs e)
        {
            if (!_isModbusMode || !_modbusService.IsConnected) return;

            _modbusTimer.Stop();

            var now = DateTime.Now;

            try
            {
                double value = await _modbusService.ReadValueAsync(
                    _settings.ModbusRegisterAddress,
                    _settings.ModbusDataType,
                    _settings.ModbusScale);

                double elapsedSeconds = (now - _testStartTime).TotalSeconds;
                elapsedSeconds = Math.Max(0, Math.Round(elapsedSeconds, 2));

                Dispatcher.Invoke(() =>
                {
                    CurrentPressureText.Text = value.ToString("F3");

                    _pressureValues.Add(value);
                    _chartValues.Add(value);
                    _timeLabels.Add(elapsedSeconds.ToString("F2"));

                    // Статистика
                    _allValues.Add(value);
                    _currentCount++;
                    _currentSum += value;

                    if (value > _currentMax) _currentMax = value;
                    if (value < _currentMin) _currentMin = value;

                    MaxValueText.Text = _currentMax.ToString("F3");
                    MinValueText.Text = _currentMin.ToString("F3");
                    AvgValueText.Text = (_currentSum / _currentCount).ToString("F3");

                    // Ось X
                    if (PressureChart.AxisX.Count > 0)
                    {
                        PressureChart.AxisX[0].Labels = _timeLabels;
                    }

                    // Ось Y
                    if (PressureChart.AxisY.Count > 0 && _pressureValues.Count > 0)
                    {
                        double maxValue = _pressureValues.Max();
                        double minValue = _pressureValues.Min();

                        if (Math.Abs(maxValue - minValue) < 0.001)
                        {
                            maxValue = minValue + 10;
                            minValue = Math.Max(0, minValue - 10);
                        }

                        PressureChart.AxisY[0].MaxValue = maxValue + (maxValue * 0.1);
                        PressureChart.AxisY[0].MinValue = Math.Max(0, minValue - (minValue * 0.1));
                    }

                    // Сохранение в БД
                    if (_currentSession != null)
                    {
                        var measurement = new Measurement
                        {
                            SessionId = _currentSession.Id,
                            TestName = _currentSession.Name,
                            Timestamp = now,
                            Pressure = value
                        };
                        _dataService.SaveMeasurement(measurement);
                    }

                    PressureChart.Update();
                });
            }
            catch (Exception ex)
            {
                SetDebug($"Ошибка Modbus: {ex.Message}");
            }
            finally
            {
                if (_isModbusMode)
                {
                    _modbusTimer.Start();
                }
            }
        }

        private async void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            SetDebug("StartBtn_Click вызван");

            if (string.IsNullOrWhiteSpace(TestNameBox.Text))
            {
                StatusText.Text = "Введите название испытания";
                return;
            }

            _settings = SettingsService.LoadSettings();
            _isModbusMode = _settings.UseModbus && _modbusService.IsConnected;

            SetDebug($"UseModbus={_settings.UseModbus}, IsConnected={_modbusService.IsConnected}, _isModbusMode={_isModbusMode}");

            // Оси
            PressureChart.AxisX.Clear();
            PressureChart.AxisY.Clear();

            PressureChart.AxisX.Add(new Axis
            {
                Title = "Время (сек)",
                LabelFormatter = value => TimeSpan.FromSeconds(value).ToString(@"mm\:ss")
            });

            PressureChart.AxisY.Add(new Axis
            {
                Title = "Давление (усл. ед.)",
                LabelFormatter = value => Math.Round(value, 3).ToString("F3"),
                MinValue = 0
            });

            // Очистка
            _pressureValues.Clear();
            _timeLabels.Clear();
            _chartValues.Clear();
            _allValues.Clear();

            // Сброс статистики
            _currentMax = double.MinValue;
            _currentMin = double.MaxValue;
            _currentSum = 0;
            _currentCount = 0;

            MaxValueText.Text = "0";
            MinValueText.Text = "0";
            AvgValueText.Text = "0";

            _testStartTime = DateTime.Now;

            double param = _settings.LastEmulationType switch
            {
                "Static" => _settings.StaticValue,
                "Ramp" => _settings.RampStep,
                _ => 0
            };

            _currentSession = new TestSession
            {
                Id = Guid.NewGuid().ToString(),
                DisplayName = TestNameBox.Text,
                Name = $"{TestNameBox.Text}_{DateTime.Now:yyyyMMdd_HHmmss}",
                StartTime = DateTime.Now,
                EmulationType = _isModbusMode ? "Modbus" : _settings.LastEmulationType,
                Param1 = param
            };
            _dataService.SaveSession(_currentSession);

            StartBtn.IsEnabled = false;
            StopBtn.IsEnabled = true;
            SettingsBtn.IsEnabled = false;
            HistoryBtn.IsEnabled = false;
            StatusText.Text = _isModbusMode ? "Modbus режим, ожидание данных..." : "Эмуляция запущена...";

            if (_isModbusMode)
            {
                SetDebug("Запуск Modbus таймера");
                _modbusTimer.Start();
            }
            else
            {
                _cts = new CancellationTokenSource();
                try
                {
                    await _emulationService.StartEmulation(_settings.LastEmulationType, param, _cts.Token);

                    await Task.Delay(50);

                    if (_currentSession != null)
                    {
                        _currentSession.EndTime = DateTime.Now;
                        _currentSession.PointsCount = _pressureValues.Count;
                        _dataService.SaveSession(_currentSession);
                        _currentSession = null;
                    }

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
                    HistoryBtn.IsEnabled = true;
                }
            }
        }

        private void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_isModbusMode)
            {
                _modbusTimer.Stop();
                _isModbusMode = false;
            }
            else
            {
                _cts?.Cancel();
                Thread.Sleep(50);
                _cts?.Dispose();
                _cts = null;
            }

            if (_currentSession != null)
            {
                _currentSession.EndTime = DateTime.Now;
                _currentSession.PointsCount = _pressureValues.Count;
                _dataService.SaveSession(_currentSession);
                _currentSession = null;
            }

            StartBtn.IsEnabled = true;
            StopBtn.IsEnabled = false;
            SettingsBtn.IsEnabled = true;
            HistoryBtn.IsEnabled = true;
            StatusText.Text = _isModbusMode ? "Modbus опрос остановлен" : "Эмуляция остановлена";
        }

        private void OnPressureUpdated(object sender, double pressure)
        {
            Dispatcher.Invoke(() => AddPressurePoint(pressure));
        }

        private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow();
            settingsWindow.Owner = this;
            settingsWindow.ShowDialog();

            _settings = SettingsService.LoadSettings();
            TestNameBox.Text = _settings.LastTestName;
            StatusText.Text = "Настройки сохранены";
            StartBtn.IsEnabled = true;

            _modbusService.UpdateSettings(_settings.ModbusIpAddress, _settings.ModbusPort, _settings.ModbusSlaveId);

            if (_settings.UseModbus && !_modbusService.IsConnected)
            {
                _ = _modbusService.ConnectAsync();
            }
        }

        private void HistoryBtn_Click(object sender, RoutedEventArgs e)
        {
            var historyWindow = new HistoryWindow(_dataService, this);
            historyWindow.Owner = this;
            historyWindow.ShowDialog();
        }

        public void LoadHistoricalData(string sessionId)
        {
            Dispatcher.Invoke(() =>
            {
                var session = _dataService.GetSession(sessionId);
                if (session == null)
                {
                    StatusText.Text = "Ошибка: сессия не найдена";
                    return;
                }

                // Сортируем по времени (от старых к новым)
                var measurements = _dataService.GetMeasurementsByTestName(session.Name)
                    .OrderBy(m => m.Timestamp)
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"Загружено {measurements.Count} точек");

                // Пересоздаём оси
                PressureChart.AxisX.Clear();
                PressureChart.AxisY.Clear();

                PressureChart.AxisX.Add(new Axis
                {
                    Title = "Время (сек)",
                    LabelFormatter = value => TimeSpan.FromSeconds(value).ToString(@"mm\:ss")
                });

                PressureChart.AxisY.Add(new Axis
                {
                    Title = "Давление (усл. ед.)",
                    LabelFormatter = value => Math.Round(value, 3).ToString("F3"),
                    MinValue = 0
                });

                var newPressureValues = new ObservableCollection<double>();
                var newTimeLabels = new ObservableCollection<string>();
                var newChartValues = new ChartValues<double>();

                var startTime = measurements.FirstOrDefault()?.Timestamp ?? DateTime.Now;

                foreach (var m in measurements)
                {
                    double elapsed = (m.Timestamp - startTime).TotalSeconds;
                    elapsed = Math.Max(0, Math.Round(elapsed, 2));

                    newPressureValues.Add(m.Pressure);
                    newChartValues.Add(m.Pressure);
                    newTimeLabels.Add(elapsed.ToString("F2"));

                    System.Diagnostics.Debug.WriteLine($"  давление={m.Pressure}, время={m.Timestamp:HH:mm:ss.fff}, elapsed={elapsed}");
                }

                _pressureValues = newPressureValues;
                _timeLabels = newTimeLabels;
                _chartValues = newChartValues;

                // Пересоздаём серию
                PressureChart.Series.Clear();
                var newSeries = new LineSeries
                {
                    Title = "Давление",
                    Values = _chartValues,
                    PointGeometrySize = 5,
                    StrokeThickness = 2,
                    Fill = Brushes.LightBlue,
                    AreaLimit = 0
                };
                PressureChart.Series.Add(newSeries);

                if (PressureChart.AxisX.Count > 0)
                {
                    PressureChart.AxisX[0].Labels = _timeLabels;
                }

                if (PressureChart.AxisY.Count > 0 && _pressureValues.Count > 0)
                {
                    double maxValue = _pressureValues.Max();
                    double minValue = _pressureValues.Min();

                    if (Math.Abs(maxValue - minValue) < 0.001)
                    {
                        maxValue = minValue + 10;
                        minValue = Math.Max(0, minValue - 10);
                    }

                    PressureChart.AxisY[0].MaxValue = maxValue + (maxValue * 0.1);
                    PressureChart.AxisY[0].MinValue = Math.Max(0, minValue - (minValue * 0.1));
                }
                else if (PressureChart.AxisY.Count > 0)
                {
                    PressureChart.AxisY[0].MaxValue = 100;
                    PressureChart.AxisY[0].MinValue = 0;
                }

                if (_pressureValues.Count > 0)
                {
                    MaxValueText.Text = _pressureValues.Max().ToString("F3");
                    MinValueText.Text = _pressureValues.Min().ToString("F3");
                    AvgValueText.Text = _pressureValues.Average().ToString("F3");
                }

                TestNameBox.Text = session.DisplayName;
                StatusText.Text = $"Загружено испытание \"{session.DisplayName}\" ({_pressureValues.Count} точек)";

                PressureChart.Update(true, true);
            });
        }
    }
}