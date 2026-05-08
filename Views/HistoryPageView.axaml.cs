using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using PoliCoLauncherApp.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PoliCoLauncherApp.Views
{
    public partial class HistoryPageView : UserControl
    {
        public event Action? LogoPressed;
        public event Action? NavigateToDashboard;

        public HistoryPageView()
        {
            InitializeComponent();
        }

        private void OnLogoPressed(object? sender, PointerPressedEventArgs e) => LogoPressed?.Invoke();
        private void OnBackClick(object? sender, RoutedEventArgs e) => NavigateToDashboard?.Invoke();

        public Task LoadHistory(string _)
        {
            var entries = HistoryService.Load();

            HistoryContainer.Children.Clear();

            if (entries.Count == 0)
            {
                HistoryStatusText.Text = "No history yet. Connect to start your first trip!";
                HistoryStatusText.IsVisible = true;
                HistoryListBorder.IsVisible = false;
                return Task.CompletedTask;
            }

            HistoryStatusText.IsVisible = false;
            HistoryListBorder.IsVisible = true;

            // newest first
            entries.Reverse();
            foreach (var e in entries)
                HistoryContainer.Children.Add(BuildCard(e));

            return Task.CompletedTask;
        }

        private static Border BuildCard(HistoryEntry e)
        {
            string route = $"{e.RouteFrom} → {e.RouteTo}";
            string train = $"{e.TrainType} {e.TrainNumber}".Trim();
            string time  = string.IsNullOrEmpty(e.DisconnectedAt)
                ? $"Dep. {e.ConnectedAt}"
                : $"{e.ConnectedAt} – {e.DisconnectedAt}";

            var left = new StackPanel { Spacing = 3 };
            left.Children.Add(new TextBlock
            {
                Text = route, FontWeight = FontWeight.Bold, FontSize = 14,
                Foreground = new SolidColorBrush(Color.Parse("#000000"))
            });
            if (!string.IsNullOrEmpty(train))
                left.Children.Add(new TextBlock
                {
                    Text = train, FontSize = 12,
                    Foreground = new SolidColorBrush(Color.Parse("#3458e1"))
                });
            if (!string.IsNullOrEmpty(e.Locomotive))
                left.Children.Add(new TextBlock
                {
                    Text = e.Locomotive, FontSize = 11,
                    Foreground = new SolidColorBrush(Color.Parse("#888888"))
                });
            if (e.WagonCount > 0)
                left.Children.Add(new TextBlock
                {
                    Text = $"{e.WagonCount} wagons", FontSize = 11,
                    Foreground = new SolidColorBrush(Color.Parse("#aaaaaa"))
                });

            var right = new StackPanel
            {
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                Spacing = 2
            };
            right.Children.Add(new TextBlock
            {
                Text = e.Date, FontSize = 12, FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Color.Parse("#000000")),
                TextAlignment = TextAlignment.Right
            });
            right.Children.Add(new TextBlock
            {
                Text = time, FontSize = 11,
                Foreground = new SolidColorBrush(Color.Parse("#888888")),
                TextAlignment = TextAlignment.Right
            });

            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            Grid.SetColumn(right, 1);
            row.Children.Add(left);
            row.Children.Add(right);

            return new Border
            {
                Background      = new SolidColorBrush(Color.Parse("#FFFFFF")),
                BorderBrush     = new SolidColorBrush(Color.Parse("#EEEEEE")),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding         = new Thickness(5, 10, 5, 10),
                Child           = row
            };
        }
    }
}
