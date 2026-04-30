using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PoliCoLauncherApp.Views
{
    public partial class TrainSelectPageView : UserControl
    {
        public event Action? LogoPressed;
        public event Action<TrainData>? TrainDataSaved;

        public TrainSelectPageView()
        {
            InitializeComponent();
        }

        private void OnLogoPressed(object? sender, PointerPressedEventArgs e) => LogoPressed?.Invoke();

        private void OnLocomotiveLinkClick(object? sender, RoutedEventArgs e) => OpenUrl("https://poli-co.com");
        private void OnWagonLinkClick(object? sender, RoutedEventArgs e) => OpenUrl("https://poli-co.com");

        public void OnStartStationChanged(object? sender, SelectionChangedEventArgs e)
        {
            IntermediatePanel.Children.Clear();
            string startStation = (StartStationCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

            var stations = startStation == "Radna"
                ? new List<string> { "Paulis", "Paulis hc.", "Ghioroc", "Glogovat" }
                : startStation == "Arad"
                    ? new List<string> { "Glogovat", "Ghioroc", "Paulis hc.", "Paulis" }
                    : new List<string>();

            foreach (string station in stations)
            {
                var minutesDisplay = new TextBox
                {
                    Height = 32, Width = 50, Padding = new Thickness(0), FontSize = 14,
                    IsReadOnly = true, Text = "0",
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                };
                minutesDisplay.Classes.Add("NumericInput");

                var decreaseBtn = new Button
                {
                    Width = 36, Height = 32,
                    Background = new SolidColorBrush(Color.Parse("#EEEEEE")),
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(Color.Parse("#CCCCCC")),
                    CornerRadius = new CornerRadius(4),
                    Content = "-", Padding = new Thickness(0), FontSize = 18,
                    HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center
                };

                var increaseBtn = new Button
                {
                    Width = 36, Height = 32,
                    Background = new SolidColorBrush(Color.Parse("#EEEEEE")),
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(Color.Parse("#CCCCCC")),
                    CornerRadius = new CornerRadius(4),
                    Content = "+", Padding = new Thickness(0), FontSize = 18,
                    HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center
                };

                decreaseBtn.Click += (s, e) =>
                {
                    if (int.TryParse(minutesDisplay.Text, out int m) && m > 0)
                        minutesDisplay.Text = (m - 1).ToString();
                };
                increaseBtn.Click += (s, e) =>
                {
                    if (int.TryParse(minutesDisplay.Text, out int m) && m < 60)
                        minutesDisplay.Text = (m + 1).ToString();
                };

                var stationLabel = new TextBlock
                {
                    Text = station, FontSize = 14, Foreground = Brushes.Black,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    Width = 100, TextAlignment = TextAlignment.Right
                };

                var panel = new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    Spacing = 10,
                    Margin = new Thickness(0, 0, 0, 10),
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left
                };
                panel.Children.Add(stationLabel);
                panel.Children.Add(decreaseBtn);
                panel.Children.Add(minutesDisplay);
                panel.Children.Add(increaseBtn);

                IntermediatePanel.Children.Add(panel);
            }
        }

        public void OnHoursTextInput(object? sender, TextInputEventArgs e)
        {
            if (sender is not TextBox hoursBox) return;
            if (string.IsNullOrEmpty(e.Text) || !char.IsDigit(e.Text[0]))
            {
                e.Handled = true;
                return;
            }
            if (hoursBox.Text?.Length == 2)
                MinutesInput.Focus();
        }

        public void OnMinutesTextInput(object? sender, TextInputEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Text) || !char.IsDigit(e.Text[0]))
                e.Handled = true;
        }

        public void OnTrainNumberTextInput(object? sender, TextInputEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Text) || !char.IsDigit(e.Text[0]))
                e.Handled = true;
        }

        public void OnWagonDecreaseClick(object? sender, RoutedEventArgs e)
        {
            if (int.TryParse(WagonCountDisplay.Text, out int count) && count > 0)
            {
                WagonCountDisplay.Text = (count - 1).ToString();
                UpdateWagonPanels(count - 1);
            }
        }

        public void OnWagonIncreaseClick(object? sender, RoutedEventArgs e)
        {
            if (int.TryParse(WagonCountDisplay.Text, out int count) && count < 20)
            {
                WagonCountDisplay.Text = (count + 1).ToString();
                UpdateWagonPanels(count + 1);
            }
        }

        private void UpdateWagonPanels(int wagonCount)
        {
            WagonsPanel.Children.Clear();
            for (int i = 1; i <= wagonCount; i++)
            {
                var wagonCombo = new ComboBox
                {
                    Height = 40, Width = 300,
                    Padding = new Thickness(10, 8), FontSize = 14,
                    PlaceholderText = $"Select wagon {i} number",
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left
                };
                wagonCombo.Items.Add(new ComboBoxItem { Content = "AVA PASSENGER CAR's from RS" });
                WagonsPanel.Children.Add(wagonCombo);
            }
        }

        private int GetStopMinutes(string stationName)
        {
            foreach (var child in IntermediatePanel.Children)
            {
                if (child is StackPanel panel && panel.Children.Count >= 3 &&
                    (panel.Children[0] as TextBlock)?.Text == stationName)
                {
                    string minText = (panel.Children[2] as TextBox)?.Text ?? "0";
                    return int.TryParse(minText, out int m) ? m : 0;
                }
            }
            return 0;
        }

        private void OnSaveAndConnectClick(object? sender, RoutedEventArgs e)
        {
            string trainType  = (TrainTypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            string trainNum   = TrainNumberInput.Text?.Trim() ?? "";
            string start      = (StartStationCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            string end        = (EndStationCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            string hours      = HoursInput.Text?.Trim() ?? "";
            string minutes    = MinutesInput.Text?.Trim() ?? "";
            string locomotive = (LocomotiveCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

            static bool empty(string s) => string.IsNullOrWhiteSpace(s);

            string? error = null;
            if (empty(trainType))
                error = "Please select a Train Type.";
            else if (empty(trainNum))
                error = "Please enter the Train Number.";
            else if (empty(start))
                error = "Please select a Start Station.";
            else if (empty(end))
                error = "Please select an End Station.";
            else if (start == end)
                error = "Start and End stations cannot be the same.";
            else if (empty(hours) || empty(minutes))
                error = "Please enter the Departure Time (HH:MM).";
            else if (empty(locomotive))
                error = "Please select a Locomotive.";

            if (error != null)
            {
                ValidationLabel.Text = error;
                ValidationLabel.IsVisible = true;
                return;
            }

            ValidationLabel.IsVisible = false;

            var data = new TrainData
            {
                TrainType    = trainType,
                TrainNumber  = trainNum,
                StartStation = start,
                EndStation   = end,
                DepartureTime = $"{hours}:{minutes}",
                Locomotive   = locomotive,
            };

            int.TryParse(WagonCountDisplay.Text, out int wagonCount);
            data.WagonCount = wagonCount;

            foreach (var child in WagonsPanel.Children)
            {
                if (child is ComboBox combo)
                    data.WagonNumbers.Add((combo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Not selected");
            }

            var orderedStations = data.StartStation == "Radna"
                ? new[] { "Paulis", "Paulis hc.", "Ghioroc", "Glogovat" }
                : new[] { "Glogovat", "Ghioroc", "Paulis hc.", "Paulis" };

            foreach (string station in orderedStations)
                data.IntermediateStopMinutes[station] = GetStopMinutes(station);

            TrainDataSaved?.Invoke(data);
        }

        private static void OpenUrl(string url)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    Process.Start("xdg-open", url);
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    Process.Start("open", url);
            }
            catch { }
        }
    }
}
