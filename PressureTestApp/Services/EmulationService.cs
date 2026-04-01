using System;
using System.Threading;
using System.Threading.Tasks;

namespace PressureTestApp.Services
{
    public class EmulationService
    {
        private readonly Random _random = new Random();
        private bool _isRunning;

        public bool IsRunning => _isRunning;

        public event EventHandler<double> PressureUpdated;

        public async Task StartEmulation(string type, double param1, CancellationToken cancellationToken)
        {
            _isRunning = true;
            double currentPressure = 0;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    double newPressure = type switch
                    {
                        "Static" => param1,
                        "Ramp" => currentPressure += param1,
                        "Random" => _random.NextDouble() * param1,
                        _ => 0
                    };

                    newPressure = Math.Max(0, Math.Round(newPressure, 3));

                    PressureUpdated?.Invoke(this, newPressure);
                    currentPressure = newPressure;

                    await Task.Delay(1000, cancellationToken);
                }
            }
            catch (TaskCanceledException)
            {
                // Нормальная остановка
            }
            finally
            {
                _isRunning = false;
            }
        }

        public void StopEmulation()
        {
            // Метод для остановки (будем использовать CancellationTokenSource)
        }
    }
}