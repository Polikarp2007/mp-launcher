using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace PoliCoLauncherApp.Views
{
    public partial class FinalPageView : UserControl
    {
        public event Action? LogoPressed;
        public event Action? Connected;
        public event Action? LeaveRequested;

        private TrainData? _data;

        public FinalPageView()
        {
            InitializeComponent();
        }

        private void OnLogoPressed(object? sender, PointerPressedEventArgs e) => LogoPressed?.Invoke();

        public void LoadData(TrainData data)
        {
            _data = data;
            GenerateTimetable();
        }

        public void UpdateUI(bool isConnected, bool hasData)
        {
            if (isConnected)
            {
                FinalConnectButton.IsVisible = false;
                LeaveMultiplayerBtn.IsVisible = true;
            }
            else
            {
                FinalConnectButton.IsVisible = true;
                FinalConnectButton.IsEnabled = true;
                ConnectButtonText.Text = "Connect to PC|MP";
                LeaveMultiplayerBtn.IsVisible = false;
            }
        }

        private async void OnConnectClick(object? sender, RoutedEventArgs e)
        {
            ConnectButtonText.Text = "Connecting...";
            FinalConnectButton.IsEnabled = false;
            _ = SyncHud();
            await Task.Delay(3000);
            FinalConnectButton.IsVisible = false;
            LeaveMultiplayerBtn.IsVisible = true;
            Connected?.Invoke();
        }

        private void OnLeaveClick(object? sender, RoutedEventArgs e)
        {
            FinalConnectButton.IsVisible = true;
            FinalConnectButton.IsEnabled = true;
            ConnectButtonText.Text = "Connect to PC|MP";
            LeaveMultiplayerBtn.IsVisible = false;
            LeaveRequested?.Invoke();
        }

        private void GenerateTimetable()
        {
            if (_data == null) return;

            TimetableDate.Text = $"Date: {DateTime.Now:dd MMMM yyyy}";
            TT_TrainInfo.Text = $"{_data.TrainType} {_data.TrainNumber}";
            TT_Locomotive.Text = _data.Locomotive;
            TT_Route.Text = $"{_data.StartStation} → {_data.EndStation}";
            TT_Wagons.Text = $"{_data.WagonCount} wagons";

            TimetableTable.Children.Clear();

            string[] timeParts = _data.DepartureTime.Split(':');
            int hours = timeParts.Length > 0 && int.TryParse(timeParts[0], out int h) ? h : 0;
            int minutes = timeParts.Length > 1 && int.TryParse(timeParts[1], out int m) ? m : 0;

            bool isDirect = !_data.IntermediateStopMinutes.Values.Any(v => v > 0);
            var rows = new List<TimetableRow>();

            if (_data.StartStation == "Radna" && _data.EndStation == "Arad")
            {
                rows.Add(new TimetableRow("Radna", null, $"{hours:D2}:{minutes:D2}"));
                int[] travel = { 6, 4, 5, 10, 8 };
                string[] stations = { "Paulis", "Paulis hc.", "Ghioroc", "Glogovat" };
                BuildRows(rows, ref hours, ref minutes, travel, stations, isDirect, "Arad");
            }
            else if (_data.StartStation == "Arad" && _data.EndStation == "Radna")
            {
                rows.Add(new TimetableRow("Arad", null, $"{hours:D2}:{minutes:D2}"));
                int[] travel = { 8, 10, 5, 4, 6 };
                string[] stations = { "Glogovat", "Ghioroc", "Paulis hc.", "Paulis" };
                BuildRows(rows, ref hours, ref minutes, travel, stations, isDirect, "Radna");
            }

            // Header row
            var headerGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*") };
            AddHeaderCell(headerGrid, "Station", 0, TextAlignment.Left, new Thickness(15, 10, 10, 10));
            AddHeaderCell(headerGrid, "Arrival Time", 1, TextAlignment.Center, new Thickness(10));
            AddHeaderCell(headerGrid, "Departure Time", 2, TextAlignment.Center, new Thickness(10));
            TimetableTable.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.Parse("#000000")),
                Child = headerGrid,
                BorderBrush = new SolidColorBrush(Color.Parse("#000000")),
                BorderThickness = new Thickness(1)
            });

            if (isDirect)
            {
                var label = new TextBlock
                {
                    Text = "DIRECT — No intermediate stops",
                    FontWeight = FontWeight.Bold, FontSize = 12,
                    Foreground = new SolidColorBrush(Color.Parse("#3458e1")),
                    Margin = new Thickness(15, 10), TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
                };
                TimetableTable.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Color.Parse("#E8F0FE")),
                    Child = label,
                    BorderBrush = new SolidColorBrush(Color.Parse("#CCCCCC")),
                    BorderThickness = new Thickness(1, 0, 1, 0)
                });
            }

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var rowGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*") };
                string bg = i % 2 == 0 ? "#FFFFFF" : "#F5F5F5";

                var stationTb = new TextBlock { Text = row.Station, FontWeight = FontWeight.SemiBold, FontSize = 12, Foreground = new SolidColorBrush(Color.Parse("#000000")), Margin = new Thickness(15, 8, 10, 8), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
                var arrTb = new TextBlock { Text = row.ArrivalTime ?? "--:--", FontSize = 12, Foreground = new SolidColorBrush(Color.Parse("#000000")), Margin = new Thickness(10), TextAlignment = TextAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, FontWeight = row.ArrivalTime != null ? FontWeight.Bold : FontWeight.Normal };
                var depTb = new TextBlock { Text = row.DepartureTime ?? "--:--", FontSize = 12, Foreground = new SolidColorBrush(Color.Parse("#000000")), Margin = new Thickness(10), TextAlignment = TextAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, FontWeight = row.DepartureTime != null ? FontWeight.Bold : FontWeight.Normal };

                Grid.SetColumn(stationTb, 0);
                Grid.SetColumn(arrTb, 1);
                Grid.SetColumn(depTb, 2);
                rowGrid.Children.Add(stationTb);
                rowGrid.Children.Add(arrTb);
                rowGrid.Children.Add(depTb);

                TimetableTable.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Color.Parse(bg)),
                    Child = rowGrid,
                    BorderBrush = new SolidColorBrush(Color.Parse("#CCCCCC")),
                    BorderThickness = new Thickness(1, 0, 1, 1)
                });
            }
        }

        private void BuildRows(List<TimetableRow> rows, ref int hours, ref int minutes, int[] travel, string[] stations, bool isDirect, string endStation)
        {
            int totalMinutes = hours * 60 + minutes;

            if (isDirect)
            {
                int arr = totalMinutes + travel.Sum();
                rows.Add(new TimetableRow(endStation, $"{(arr / 60) % 24:D2}:{arr % 60:D2}", null));
                return;
            }

            for (int i = 0; i < stations.Length; i++)
            {
                totalMinutes += travel[i];
                int arrival = totalMinutes;
                int stopMins = GetStopMinutes(stations[i]);

                if (stopMins > 0)
                {
                    string arr = $"{(arrival / 60) % 24:D2}:{arrival % 60:D2}";
                    totalMinutes += stopMins;
                    string dep = $"{(totalMinutes / 60) % 24:D2}:{totalMinutes % 60:D2}";
                    rows.Add(new TimetableRow(stations[i], arr, dep));
                }
            }

            int finalArr = totalMinutes + travel[^1];
            rows.Add(new TimetableRow(endStation, $"{(finalArr / 60) % 24:D2}:{finalArr % 60:D2}", null));
        }

        private int GetStopMinutes(string station) =>
            _data?.IntermediateStopMinutes.TryGetValue(station, out int m) == true ? m : 0;

        private static void AddHeaderCell(Grid grid, string text, int column, TextAlignment align, Thickness margin)
        {
            var tb = new TextBlock { Text = text, FontWeight = FontWeight.Bold, FontSize = 13, Foreground = Brushes.White, Margin = margin, TextAlignment = align };
            Grid.SetColumn(tb, column);
            grid.Children.Add(tb);
        }

        public async Task SyncHud()
        {
            if (_data == null) return;
            try
            {
                string[] timeParts = _data.DepartureTime.Split(':');
                int hours = timeParts.Length > 0 && int.TryParse(timeParts[0], out int h) ? h : 0;
                int minutes = timeParts.Length > 1 && int.TryParse(timeParts[1], out int m) ? m : 0;
                int totalMinutes = hours * 60 + minutes;

                bool isDirect = !_data.IntermediateStopMinutes.Values.Any(v => v > 0);
                var stationsList = new List<string[]>
                {
                    new[] { _data.StartStation, "--:--", $"{hours:D2}:{minutes:D2}" }
                };

                if (_data.StartStation == "Radna" && _data.EndStation == "Arad")
                {
                    int[] travel = { 6, 4, 5, 10, 8 };
                    string[] stations = { "Paulis", "Paulis hc.", "Ghioroc", "Glogovat" };
                    BuildHudStations(stationsList, ref totalMinutes, travel, stations, isDirect, _data.EndStation);
                }
                else if (_data.StartStation == "Arad" && _data.EndStation == "Radna")
                {
                    int[] travel = { 8, 10, 5, 4, 6 };
                    string[] stations = { "Glogovat", "Ghioroc", "Paulis hc.", "Paulis" };
                    BuildHudStations(stationsList, ref totalMinutes, travel, stations, isDirect, _data.EndStation);
                }

                var hudData = new
                {
                    train_num = $"{_data.TrainType} {_data.TrainNumber}",
                    route = $"{_data.StartStation} to {_data.EndStation}",
                    stations = stationsList,
                    status = "online"
                };

                using var client = new HttpClient();
                var json = JsonSerializer.Serialize(hudData);
                await client.PostAsync("http://116.203.229.254:3000/update_hud",
                    new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json"));
                Debug.WriteLine("HUD: Успешно отправлено!");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("HUD Error: " + ex.Message);
            }
        }

        private void BuildHudStations(List<string[]> list, ref int totalMinutes, int[] travel, string[] stations, bool isDirect, string endStation)
        {
            if (isDirect)
            {
                int arr = totalMinutes + travel.Sum();
                list.Add(new[] { endStation, $"{(arr / 60) % 24:D2}:{arr % 60:D2}", "--:--" });
                return;
            }

            for (int i = 0; i < stations.Length; i++)
            {
                totalMinutes += travel[i];
                int arrival = totalMinutes;
                int stopMins = GetStopMinutes(stations[i]);

                if (stopMins > 0)
                {
                    string arr = $"{(arrival / 60) % 24:D2}:{arrival % 60:D2}";
                    totalMinutes += stopMins;
                    string dep = $"{(totalMinutes / 60) % 24:D2}:{totalMinutes % 60:D2}";
                    list.Add(new[] { stations[i], arr, dep });
                }
            }

            int finalArr = totalMinutes + travel[^1];
            list.Add(new[] { endStation, $"{(finalArr / 60) % 24:D2}:{finalArr % 60:D2}", "--:--" });
        }
    }

    internal class TimetableRow
    {
        public string Station { get; }
        public string? ArrivalTime { get; }
        public string? DepartureTime { get; }

        public TimetableRow(string station, string? arrivalTime, string? departureTime)
        {
            Station = station;
            ArrivalTime = arrivalTime;
            DepartureTime = departureTime;
        }
    }
}
