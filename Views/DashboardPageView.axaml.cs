using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PoliCoLauncherApp.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace PoliCoLauncherApp.Views
{
    public partial class DashboardPageView : UserControl
    {
        public event Action? LogoPressed;
        public event Action? NavigateToConnect;

        public DashboardPageView()
        {
            InitializeComponent();
        }

        private void OnLogoPressed(object? sender, PointerPressedEventArgs e) => LogoPressed?.Invoke();
        private void OnConnectClick(object? sender, RoutedEventArgs e) => NavigateToConnect?.Invoke();

        public void SetUser(UserSession session)
        {
            string fullName = $"{session.Name} {session.LastName}".Trim();
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                UserNameText.Text = !string.IsNullOrEmpty(fullName) ? fullName : "User";
            });
            if (!string.IsNullOrEmpty(session.SteamURL))
                _ = LoadSteamAvatarAsync(session.SteamURL);
        }

        private async Task LoadSteamAvatarAsync(string steamUrl)
        {
            try
            {
                string dir = Path.Combine(AppContext.BaseDirectory, "Assets", "Dashboard");
                Directory.CreateDirectory(dir);
                string localPath = Path.Combine(dir, "steam_avatar.jpg");

                // Serve from local cache when available
                if (File.Exists(localPath))
                {
                    var cached = new Bitmap(localPath);
                    await Dispatcher.UIThread.InvokeAsync(() => SteamAvatarImage.Source = cached);
                    return;
                }

                if (string.IsNullOrWhiteSpace(steamUrl)) return;

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");

                // Steam XML API — appending /?xml=1 returns an XML doc with <avatarFull> URL
                string xmlUrl = steamUrl.TrimEnd('/') + "/?xml=1";
                string xmlContent = await client.GetStringAsync(xmlUrl);

                var doc = XDocument.Parse(xmlContent);
                string? avatarUrl = doc.Descendants("avatarFull").FirstOrDefault()?.Value?.Trim();
                if (string.IsNullOrEmpty(avatarUrl)) return;

                byte[] bytes = await client.GetByteArrayAsync(avatarUrl);
                await File.WriteAllBytesAsync(localPath, bytes);

                using var ms = new MemoryStream(bytes);
                var bitmap = new Bitmap(ms);
                await Dispatcher.UIThread.InvokeAsync(() => SteamAvatarImage.Source = bitmap);
            }
            catch { }
        }
        private void OnMapLinkClick(object? sender, RoutedEventArgs e) => OpenUrl("https://map.poli-co.com");
        private void OnSiteLinkClick(object? sender, RoutedEventArgs e) => OpenUrl("https://poli-co.com");
        private void OnHistoryClick(object? sender, RoutedEventArgs e) { }

        public async Task LoadNews()
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                string response = await client.GetStringAsync("http://116.203.229.254:5001/get_news");
                using JsonDocument doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    if (root.TryGetProperty("version", out var v))
                        VersionDisplay.Text = $"Version {v.GetString()}";

                    var textPart = new List<Control>();
                    var imagePart = new List<Control>();

                    if (root.TryGetProperty("news", out var newsArray) && newsArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in newsArray.EnumerateArray())
                        {
                            string type = item.TryGetProperty("type", out var t) ? t.GetString() ?? "text" : "text";
                            string content = item.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";

                            if (type == "header")
                                textPart.Add(new TextBlock { Text = content, FontWeight = FontWeight.Bold, FontSize = 18, Foreground = new SolidColorBrush(Color.Parse("#070e2e")), Margin = new Avalonia.Thickness(0, 10, 0, 5) });
                            else if (type == "bold")
                                textPart.Add(new TextBlock { Text = content, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap, Margin = new Avalonia.Thickness(0, 2) });
                            else if (type == "text")
                                textPart.Add(new TextBlock { Text = content, TextWrapping = TextWrapping.Wrap, Margin = new Avalonia.Thickness(0, 2) });
                            else if (type == "image" && !string.IsNullOrEmpty(content))
                            {
                                var bitmap = await LoadImageFromUrl(content);
                                if (bitmap != null)
                                    imagePart.Add(new Image { Source = bitmap, Margin = new Avalonia.Thickness(2, 10), Stretch = Stretch.Uniform, MaxWidth = 422, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center });
                            }
                        }
                    }

                    var combined = new List<Control>();
                    combined.AddRange(textPart);
                    combined.AddRange(imagePart);
                    combined.Add(new Border { Height = 50 });
                    NewsContainer.ItemsSource = combined;
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NEWS ERROR]: {ex.Message}");
            }
        }

        private static async Task<Bitmap?> LoadImageFromUrl(string url)
        {
            try
            {
                using var client = new HttpClient();
                var bytes = await client.GetByteArrayAsync(url);
                using var ms = new MemoryStream(bytes);
                return new Bitmap(ms);
            }
            catch { return null; }
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
