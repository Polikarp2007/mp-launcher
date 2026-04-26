using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using PoliCoLauncherApp.Models;
using PoliCoLauncherApp.Services;
using System;

namespace PoliCoLauncherApp.Views
{
    public partial class LoginPageView : UserControl
    {
        public event Action? LogoPressed;
        public event Action<UserSession>? LoginSucceeded;

        private TextBox[] _keyBoxes = null!;
        private bool _suppressChange = false;


        public LoginPageView()
        {
            InitializeComponent();
            _keyBoxes = new[] { Key1, Key2, Key3, Key4 };
        }

        private void OnLogoPressed(object? sender, PointerPressedEventArgs e) => LogoPressed?.Invoke();

        // ── Key input handling ───────────────────────────────────────────────

        public void OnKeyTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (_suppressChange || sender is not TextBox box) return;

            int idx = Array.IndexOf(_keyBoxes, box);
            if (idx < 0) return;

            string text = box.Text ?? "";

            // Handle paste of full key like "TN6D-MG3C-BY5Z-QS6S" into first box
            if (idx == 0 && text.Contains('-') && text.Length >= 14)
            {
                var parts = text.Split('-');
                if (parts.Length == 4)
                {
                    _suppressChange = true;
                    Key1.Text = Truncate(parts[0].ToUpperInvariant(), 4);
                    Key2.Text = Truncate(parts[1].ToUpperInvariant(), 4);
                    Key3.Text = Truncate(parts[2].ToUpperInvariant(), 4);
                    Key4.Text = Truncate(parts[3].ToUpperInvariant(), 4);
                    _suppressChange = false;
                    Key4.Focus();
                    return;
                }
            }

            // Force uppercase, strip non-alphanumeric
            string clean = "";
            foreach (char c in text)
                if (char.IsLetterOrDigit(c)) clean += char.ToUpperInvariant(c);
            clean = Truncate(clean, 4);

            if (clean != text)
            {
                _suppressChange = true;
                box.Text = clean;
                box.CaretIndex = clean.Length;
                _suppressChange = false;
            }

            // Auto-advance
            if (clean.Length == 4 && idx < 3)
            {
                _keyBoxes[idx + 1].Focus();
                _keyBoxes[idx + 1].SelectAll();
            }
        }

        // ── Login button ─────────────────────────────────────────────────────

        private async void OnLoginClick(object? sender, RoutedEventArgs e)
        {
            string key = BuildKey();
            if (key.Length != 19)
            {
                ShowError("Please enter all 4 parts of the key (XXXX-XXXX-XXXX-XXXX).");
                return;
            }

            ShowLoadingState(true);

            var result = await LoginService.ValidateAndBindKeyAsync(key);

            if (result.Success && result.Session != null)
            {
                LoginService.SaveCache(result.Session);
                LoginSucceeded?.Invoke(result.Session);
                // Keep loading state — transition overlay covers the page
            }
            else
            {
                ShowLoadingState(false);
                ShowError(result.ErrorMessage);
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private string BuildKey()
        {
            string p1 = Key1.Text ?? "";
            string p2 = Key2.Text ?? "";
            string p3 = Key3.Text ?? "";
            string p4 = Key4.Text ?? "";
            if (p1.Length == 4 && p2.Length == 4 && p3.Length == 4 && p4.Length == 4)
                return $"{p1}-{p2}-{p3}-{p4}";
            return "";
        }

        private void ShowLoadingState(bool loading)
        {
            LoginPanel.IsVisible = !loading;
            LoadingPanel.IsVisible = loading;
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.IsVisible = true;
        }

        private static string Truncate(string s, int max) =>
            s.Length > max ? s[..max] : s;
    }
}
