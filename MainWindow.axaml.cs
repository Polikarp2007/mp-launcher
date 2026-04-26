using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using PoliCoLauncherApp.Models;
using PoliCoLauncherApp.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PoliCoLauncherApp
{
    public partial class MainWindow : Window
    {
        private bool _isConnected = false;
        private bool _hasConnectionData = false;
        private TrainData? _currentTrainData;
        private UserSession? _currentUser;

        private CancellationTokenSource? _toastCts;
        private TranslateTransform _toastTransform = null!;

        private CancellationTokenSource? _navCts;
        private ScaleTransform _brandScale = null!;

        public MainWindow()
        {
            InitializeComponent();

            // Set window icon — prefer ICO (multi-size) for crisp Windows rendering
            try
            {
                using var stream = AssetLoader.Open(new Uri("avares://PoliCoLauncherApp/Assets/app_logo.ico"));
                Icon = new WindowIcon(stream);
            }
            catch
            {
                try
                {
                    using var stream = AssetLoader.Open(new Uri("avares://PoliCoLauncherApp/Assets/app_logo.png"));
                    Icon = new WindowIcon(stream);
                }
                catch { }
            }

            // Toast slide transition
            _toastTransform = (TranslateTransform)ToastNotification.RenderTransform!;
            _toastTransform.Transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = TranslateTransform.XProperty,
                    Duration = TimeSpan.FromMilliseconds(300),
                    Easing = new CubicEaseOut()
                }
            };

            // Brand overlay: ScaleTransform with spring/bounce (BackEaseOut)
            _brandScale = new ScaleTransform(0, 0);
            _brandScale.Transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = ScaleTransform.ScaleXProperty,
                    Duration = TimeSpan.FromMilliseconds(500),
                    Easing = new BackEaseOut()
                },
                new DoubleTransition
                {
                    Property = ScaleTransform.ScaleYProperty,
                    Duration = TimeSpan.FromMilliseconds(500),
                    Easing = new BackEaseOut()
                }
            };
            BrandContainer.RenderTransform = _brandScale;
            BrandContainer.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);

            // Overlay fade transition
            TransitionOverlay.Transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = Visual.OpacityProperty,
                    Duration = TimeSpan.FromMilliseconds(280),
                    Easing = new CubicEaseInOut()
                }
            };

            WirePageEvents();
            StartIntroSequence();
        }

        private void WirePageEvents()
        {
            LoginPage.LogoPressed += GoToLogin;
            LoginPage.LoginSucceeded += OnLoginSucceeded;

            DashboardPage.LogoPressed += GoToDashboard;
            DashboardPage.NavigateToConnect += OnNavigateToConnect;

            RoutePage.LogoPressed += GoToDashboard;
            RoutePage.NavigateToDashboard += GoToDashboard;
            RoutePage.NavigateToTrainSelect += GoToTrainSelect;

            TrainSelectPage.LogoPressed += GoToDashboard;
            TrainSelectPage.TrainDataSaved += OnTrainDataSaved;

            FinalPage.LogoPressed += GoToDashboard;
            FinalPage.Connected += OnConnected;
            FinalPage.LeaveRequested += OnLeaveRequested;
        }

        // --- BRAND TRANSITION (~3 seconds total) ---

        private async Task NavigateTo(Action switchPage)
        {
            _navCts?.Cancel();
            _navCts = new CancellationTokenSource();
            var token = _navCts.Token;

            try
            {
                // Reset brand logo (no transition fires while overlay is hidden)
                _brandScale.ScaleX = 0;
                _brandScale.ScaleY = 0;
                TransitionOverlay.Opacity = 0;
                TransitionOverlay.IsVisible = true;

                // Commit initial state before triggering transitions
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
                token.ThrowIfCancellationRequested();

                // Fade-in overlay (280ms) + spring logo (500ms) — concurrent
                TransitionOverlay.Opacity = 1;
                _brandScale.ScaleX = 1;
                _brandScale.ScaleY = 1;

                // Hold: wait for full fade-in (280ms) + display time = ~1.6s total visible
                await Task.Delay(1600, token);

                // Switch page while hidden
                switchPage();

                // Fade out
                TransitionOverlay.Opacity = 0;
                await Task.Delay(340, token);
                TransitionOverlay.IsVisible = false;
            }
            catch (OperationCanceledException)
            {
                TransitionOverlay.IsVisible = false;
                TransitionOverlay.Opacity = 0;
                switchPage();
            }
        }

        // --- NAVIGATION ---

        private async void GoToLogin()
        {
            await NavigateTo(() =>
            {
                AppLogo.IsVisible = false;
                WelcomePage.IsVisible = false;
                LoginPage.IsVisible = true;
                DashboardPage.IsVisible = false;
                RoutePage.IsVisible = false;
                TrainSelectPage.IsVisible = false;
                FinalPage.IsVisible = false;
            });
        }

        private async void GoToDashboard()
        {
            await NavigateTo(() =>
            {
                AppLogo.IsVisible = false;
                WelcomePage.IsVisible = false;
                LoginPage.IsVisible = false;
                DashboardPage.IsVisible = true;
                RoutePage.IsVisible = false;
                TrainSelectPage.IsVisible = false;
                FinalPage.IsVisible = false;
            });
        }

        private async void OnNavigateToConnect()
        {
            await NavigateTo(() =>
            {
                DashboardPage.IsVisible = false;
                if (_isConnected || _hasConnectionData)
                {
                    FinalPage.IsVisible = true;
                    FinalPage.UpdateUI(_isConnected, _hasConnectionData);
                }
                else
                {
                    RoutePage.IsVisible = true;
                }
            });
        }

        private async void GoToTrainSelect()
        {
            await NavigateTo(() =>
            {
                RoutePage.IsVisible = false;
                TrainSelectPage.IsVisible = true;
            });
        }

        // --- EVENT HANDLERS FROM PAGES ---

        private async void OnLoginSucceeded(UserSession session)
        {
            _currentUser = session;
            DashboardPage.SetUser(session);
            await NavigateTo(() =>
            {
                LoginPage.IsVisible = false;
                DashboardPage.IsVisible = true;
            });
        }

        private async void OnTrainDataSaved(TrainData data)
        {
            _currentTrainData = data;
            _hasConnectionData = true;
            await NavigateTo(() =>
            {
                FinalPage.LoadData(data);
                TrainSelectPage.IsVisible = false;
                FinalPage.IsVisible = true;
            });
            ShowToast("Saved Successful!");
        }

        private void OnConnected()
        {
            _isConnected = true;
            ConnectedIndicator.IsVisible = true;
            ShowToast("Connected to PC|MP!");
        }

        private void OnLeaveRequested()
        {
            _isConnected = false;
            _hasConnectionData = false;
            ConnectedIndicator.IsVisible = false;
        }

        // --- GLOBAL LOGO CLICK ---

        public void LogoClicked(object? sender, PointerPressedEventArgs e) => GoToDashboard();

        // --- INTRO ---

        private async void StartIntroSequence()
        {
            _ = DashboardPage.LoadNews();

            // Welcome animation always plays on every launch
            await Task.Delay(3000);
            await WelcomePage.PlayFlipAnimation();
            await Task.Delay(3000);
            // AppLogo stays visible until NavigateTo hides it

            if (LoginService.HasCache())
            {
                var result = await LoginService.LoginWithCacheAsync();
                if (result.Success && result.Session != null)
                {
                    _currentUser = result.Session;
                    DashboardPage.SetUser(result.Session);
                    GoToDashboard();
                    return;
                }
                LoginService.ClearCache();
            }

            GoToLogin();
        }

        // --- TOAST ---

        private async void ShowToast(string message)
        {
            _toastCts?.Cancel();
            _toastCts = new CancellationTokenSource();
            var token = _toastCts.Token;

            ToastText.Text = message;
            ToastNotification.IsVisible = false;
            _toastTransform.X = 240;
            ToastNotification.IsVisible = true;

            try
            {
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
                token.ThrowIfCancellationRequested();

                _toastTransform.X = 0;
                await Task.Delay(2800, token);

                _toastTransform.X = 240;
                await Task.Delay(350, token);
                ToastNotification.IsVisible = false;
            }
            catch (OperationCanceledException) { }
        }

        private void CloseToast(object? sender, RoutedEventArgs e)
        {
            _toastCts?.Cancel();
            ToastNotification.IsVisible = false;
        }
    }
}
