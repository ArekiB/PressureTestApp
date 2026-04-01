using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using PressureTestApp.Helpers;
using PressureTestApp.Models;
using PressureTestApp.Services;

namespace PressureTestApp.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private AppSettings _settings;

        public List<string> EmulationTypes { get; } = new List<string>
        {
            "Static",
            "Ramp",
            "Random"
        };

        private string _selectedType;
        public string SelectedType
        {
            get => _selectedType;
            set
            {
                _selectedType = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsStatic));
                OnPropertyChanged(nameof(IsRamp));
                OnPropertyChanged(nameof(IsRandom));
            }
        }

        public bool IsStatic => SelectedType == "Static";
        public bool IsRamp => SelectedType == "Ramp";
        public bool IsRandom => SelectedType == "Random";

        private double _staticValue;
        public double StaticValue
        {
            get => _staticValue;
            set { _staticValue = value; OnPropertyChanged(); }
        }

        private double _rampStep;
        public double RampStep
        {
            get => _rampStep;
            set { _rampStep = value; OnPropertyChanged(); }
        }

        private double _randomLimit;
        public double RandomLimit
        {
            get => _randomLimit;
            set { _randomLimit = value; OnPropertyChanged(); }
        }

        private string _testName;
        public string TestName
        {
            get => _testName;
            set { _testName = value; OnPropertyChanged(); }
        }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public SettingsViewModel()
        {
            LoadSettings();

            SaveCommand = new RelayCommand(_ => SaveAndClose());
            CancelCommand = new RelayCommand(_ => Close());
        }

        private void LoadSettings()
        {
            _settings = SettingsService.LoadSettings();
            SelectedType = _settings.LastEmulationType;
            StaticValue = _settings.StaticValue;
            RampStep = _settings.RampStep;
            RandomLimit = _settings.RandomLimit;
            TestName = _settings.LastTestName;
        }

        private void SaveAndClose()
        {
            _settings.LastEmulationType = SelectedType;
            _settings.StaticValue = StaticValue;
            _settings.RampStep = RampStep;
            _settings.RandomLimit = RandomLimit;
            _settings.LastTestName = TestName;

            SettingsService.SaveSettings(_settings);
            Close();
        }

        private void Close()
        {
            foreach (Window window in Application.Current.Windows)
            {
                if (window.DataContext == this)
                {
                    window.DialogResult = true;
                    window.Close();
                    break;
                }
            }
        }
    }
}