using PressureTestApp.Models;
using PressureTestApp.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace PressureTestApp.Views
{
    public partial class HistoryWindow : Window
    {
        private IDataService _dataService;
        private List<TestSession> _allSessions;
        private MainWindow _mainWindow;

        public HistoryWindow(IDataService dataService, MainWindow mainWindow)
        {
            InitializeComponent();

            _dataService = dataService;
            _mainWindow = mainWindow;

            LoadSessions();

            FilterBox.TextChanged += FilterBox_TextChanged;
            SortCombo.SelectionChanged += SortCombo_SelectionChanged;
            SessionsGrid.SelectionChanged += SessionsGrid_SelectionChanged;
            LoadBtn.Click += LoadBtn_Click;
            DeleteBtn.Click += DeleteBtn_Click;
            CloseBtn.Click += CloseBtn_Click;
        }

        private void LoadSessions()
        {
            _allSessions = _dataService.GetAllSessions();
            ApplyFilterAndSort();
        }

        private void ApplyFilterAndSort()
        {
            var filtered = _allSessions;

            // Фильтр по названию
            string filter = FilterBox.Text.Trim().ToLower();
            if (!string.IsNullOrEmpty(filter))
            {
                filtered = filtered.Where(x => x.Name.ToLower().Contains(filter)).ToList();
            }

            // Сортировка
            string sortBy = (SortCombo.SelectedItem as ComboBoxItem)?.Content.ToString();
            switch (sortBy)
            {
                case "По дате (новые сверху)":
                    filtered = filtered.OrderByDescending(x => x.StartTime).ToList();
                    break;
                case "По дате (старые сверху)":
                    filtered = filtered.OrderBy(x => x.StartTime).ToList();
                    break;
                case "По названию (А-Я)":
                    filtered = filtered.OrderBy(x => x.Name).ToList();
                    break;
                case "По названию (Я-А)":
                    filtered = filtered.OrderByDescending(x => x.Name).ToList();
                    break;
            }

            SessionsGrid.ItemsSource = filtered;
        }

        private void FilterBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            ApplyFilterAndSort();
        }

        private void SortCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            ApplyFilterAndSort();
        }

        private void SessionsGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            bool hasSelection = SessionsGrid.SelectedItem != null;
            LoadBtn.IsEnabled = hasSelection;
            DeleteBtn.IsEnabled = hasSelection;
        }

        private void LoadBtn_Click(object sender, RoutedEventArgs e)
        {
            var session = SessionsGrid.SelectedItem as TestSession;
            if (session != null)
            {
                _mainWindow.LoadHistoricalData(session.Id);
                Close();
            }
        }

        private void DeleteBtn_Click(object sender, RoutedEventArgs e)
        {
            var session = SessionsGrid.SelectedItem as TestSession;
            if (session == null) return;

            var result = MessageBox.Show(
                $"Удалить испытание \"{session.Name}\" от {session.StartTime:dd.MM.yyyy HH:mm}?\nВсе данные будут удалены безвозвратно.",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _dataService.DeleteSession(session.Id);
                LoadSessions();
            }
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}