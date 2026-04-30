using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using PoliCoLauncherApp.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PoliCoLauncherApp.Views
{
    public partial class FinalPageView : UserControl
    {
        public event Action? LogoPressed;
        public event Action? Connected;
        public event Action? LeaveRequested;

        private TrainData? _data;
        private UserSession? _user;

        // ── HUD overlay process ──────────────────────────────────────────────
        private Process? _hudProcess;

        // ── RailDriver bridge ────────────────────────────────────────────────
        private CancellationTokenSource? _bridgeCts;
        private IntPtr _rdHandle = IntPtr.Zero;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate float GetControllerValueDelegate(int ctrl, int param);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void SetBoolDelegate([MarshalAs(UnmanagedType.Bool)] bool val);

        private GetControllerValueDelegate? _getCV;

        private const int ID_SPEED = 58;
        private const int ID_LAT   = 400;
        private const int ID_LON   = 401;

        private static readonly string[] RailDriverSearchPaths =
        {
            @"D:\My Steam\steamapps\common\RailWorks\plugins\RailDriver64.dll",
            @"D:\Steam\steamapps\common\RailWorks\plugins\RailDriver64.dll",
            @"E:\My Steam\steamapps\common\RailWorks\plugins\RailDriver64.dll",
            @"E:\Steam\steamapps\common\RailWorks\plugins\RailDriver64.dll",
            @"C:\Program Files (x86)\Steam\steamapps\common\RailWorks\plugins\RailDriver64.dll",
            @"C:\Program Files\Steam\steamapps\common\RailWorks\plugins\RailDriver64.dll",
        };

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

        public void LoadUser(UserSession? user)
        {
            _user = user;
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
            _ = SyncHud(0, 0, 0);

            TryLoadRailDriver();
            _bridgeCts = new CancellationTokenSource();
            _ = RunGameBridge(_bridgeCts.Token);

            await Task.Delay(3000);
            FinalConnectButton.IsVisible = false;
            LeaveMultiplayerBtn.IsVisible = true;
            Connected?.Invoke();
            LaunchHud();
        }

        private async void OnLeaveClick(object? sender, RoutedEventArgs e)
        {
            _bridgeCts?.Cancel();
            _bridgeCts = null;
            KillHud();
            _ = SendDisconnect();

            if (_rdHandle != IntPtr.Zero)
            {
                try { NativeLibrary.Free(_rdHandle); } catch { }
                _rdHandle = IntPtr.Zero;
                _getCV = null;
            }

            FinalConnectButton.IsVisible = true;
            FinalConnectButton.IsEnabled = true;
            ConnectButtonText.Text = "Connect to PC|MP";
            LeaveMultiplayerBtn.IsVisible = false;
            LeaveRequested?.Invoke();
            Environment.Exit(0);
        }

        private void LaunchHud()
        {
            try
            {
                string baseDir  = AppDomain.CurrentDomain.BaseDirectory;
                string exePath  = Path.Combine(baseDir, "Developer", "PoliCo_HUD.exe");
                string pyPath   = Path.Combine(baseDir, "Developer", "pcImp_v1.py");
                string assetsDir = Path.Combine(baseDir, "Cache", "RAILWORKS", "PCIMP");

                ProcessStartInfo psi;

                if (File.Exists(exePath))
                {
                    psi = new ProcessStartInfo
                    {
                        FileName         = exePath,
                        UseShellExecute  = false,
                        WorkingDirectory = assetsDir,
                    };
                }
                else if (File.Exists(pyPath))
                {
                    string? python = FindPython();
                    if (python == null)
                    {
                        Debug.WriteLine("HUD: Python not found");
                        return;
                    }
                    psi = new ProcessStartInfo
                    {
                        FileName         = python,
                        Arguments        = $"\"{pyPath}\"",
                        UseShellExecute  = false,
                        WorkingDirectory = assetsDir,
                    };
                }
                else
                {
                    Debug.WriteLine("HUD: neither PoliCo_HUD.exe nor pcImp_v1.py found in Developer/");
                    return;
                }

                _hudProcess = Process.Start(psi);
                Debug.WriteLine("HUD launched, PID=" + _hudProcess?.Id);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("HUD launch failed: " + ex.Message);
            }
        }

        private void KillHud()
        {
            try
            {
                if (_hudProcess != null && !_hudProcess.HasExited)
                    _hudProcess.Kill(entireProcessTree: true);
            }
            catch { }
            finally { _hudProcess = null; }
        }

        private static string? FindPython()
        {
            // pythonw — no console window; python — fallback
            foreach (string exe in new[] { "pythonw.exe", "python.exe" })
            {
                string? inPath = FindInPath(exe);
                if (inPath != null) return inPath;

                string user = Environment.UserName;
                foreach (string ver in new[] { "Python313", "Python312", "Python311", "Python310" })
                {
                    string[] candidates =
                    {
                        $@"C:\{ver}\{exe}",
                        $@"C:\Users\{user}\AppData\Local\Programs\Python\{ver}\{exe}",
                    };
                    foreach (string c in candidates)
                        if (File.Exists(c)) return c;
                }
            }
            return null;
        }

        private static string? FindInPath(string name)
        {
            string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string dir in pathEnv.Split(Path.PathSeparator))
            {
                string full = Path.Combine(dir.Trim(), name);
                if (File.Exists(full)) return full;
            }
            return null;
        }

        // ── RailDriver bridge ────────────────────────────────────────────────

        private bool TryLoadRailDriver()
        {
            // Try to find Steam install path via registry
            var candidates = new List<string>(RailDriverSearchPaths);
            try
            {
                using var regKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\WOW6432Node\Valve\Steam");
                if (regKey?.GetValue("InstallPath") is string steamPath)
                {
                    candidates.Insert(0, Path.Combine(steamPath, "steamapps", "common",
                        "RailWorks", "plugins", "RailDriver64.dll"));
                }
            }
            catch { }

            foreach (var path in candidates)
            {
                if (!File.Exists(path)) continue;
                try
                {
                    _rdHandle = NativeLibrary.Load(path);
                    var cvPtr = NativeLibrary.GetExport(_rdHandle, "GetControllerValue");
                    _getCV = Marshal.GetDelegateForFunctionPointer<GetControllerValueDelegate>(cvPtr);

                    if (NativeLibrary.TryGetExport(_rdHandle, "SetRailSimConnected", out var simPtr))
                        Marshal.GetDelegateForFunctionPointer<SetBoolDelegate>(simPtr)(true);
                    if (NativeLibrary.TryGetExport(_rdHandle, "SetRailDriverConnected", out var rdPtr))
                        Marshal.GetDelegateForFunctionPointer<SetBoolDelegate>(rdPtr)(true);

                    Debug.WriteLine($"RailDriver DLL loaded: {path}");
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"RailDriver load failed ({path}): {ex.Message}");
                }
            }

            Debug.WriteLine("RailDriver64.dll not found — GPS coords will be 0");
            return false;
        }

        private async Task RunGameBridge(CancellationToken token)
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };

            while (!token.IsCancellationRequested)
            {
                try
                {
                    float lat = 0f, lon = 0f, speed = 0f;
                    if (_getCV != null)
                    {
                        lat   = _getCV(ID_LAT,   0);
                        lon   = _getCV(ID_LON,   0);
                        speed = _getCV(ID_SPEED, 0);
                    }

                    var payload = new
                    {
                        key          = _user?.Key ?? "",
                        name         = _user?.Name ?? "",
                        last_name    = _user?.LastName ?? "",
                        lat          = (double)lat,
                        lon          = (double)lon,
                        speed        = Math.Round((double)speed, 1),
                        train_type   = _data?.TrainType ?? "",
                        train_number = _data?.TrainNumber ?? "",
                        locomotive   = _data?.Locomotive ?? "",
                        wagon_count  = _data?.WagonCount ?? 0,
                        route_from   = _data?.StartStation ?? "",
                        route_to     = _data?.EndStation ?? "",
                        departure_time    = _data?.DepartureTime ?? "",
                        intermediate_stops = _data?.IntermediateStopMinutes ?? new Dictionary<string, int>()
                    };

                    var json = JsonSerializer.Serialize(payload);
                    await http.PostAsync(
                        "https://map.poli-co.com/mp/update_player",
                        new StringContent(json, Encoding.UTF8, "application/json"),
                        token);

                    _ = SyncHud((double)lat, (double)lon, Math.Round((double)speed, 1));
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { Debug.WriteLine($"Bridge error: {ex.Message}"); }

                try { await Task.Delay(2000, token); }
                catch (OperationCanceledException) { break; }
            }
        }

        private async Task SendDisconnect()
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                var json = JsonSerializer.Serialize(new { key = _user?.Key ?? "" });
                await http.PostAsync(
                    "https://map.poli-co.com/mp/leave_player",
                    new StringContent(json, Encoding.UTF8, "application/json"));
            }
            catch { }
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

        public async Task SyncHud(double lat, double lon, double speed)
        {
            if (_data == null) return;
            try
            {
                string[] timeParts = _data.DepartureTime.Split(':');
                int hours   = timeParts.Length > 0 && int.TryParse(timeParts[0], out int h) ? h : 0;
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
                    train_num    = $"{_data.TrainType} {_data.TrainNumber}",
                    train_type   = _data.TrainType,
                    train_number = _data.TrainNumber,
                    route        = $"{_data.StartStation} to {_data.EndStation}",
                    route_from   = _data.StartStation,
                    route_to     = _data.EndStation,
                    stations     = stationsList,
                    lat,
                    lon,
                    speed,
                    status       = "online"
                };

                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                var json = JsonSerializer.Serialize(hudData);
                await client.PostAsync("http://116.203.229.254:3000/update_hud",
                    new StringContent(json, Encoding.UTF8, "application/json"));
            }
            catch (Exception ex)
            {
                Debug.WriteLine("HUD sync error: " + ex.Message);
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
