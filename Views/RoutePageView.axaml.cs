using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;

namespace PoliCoLauncherApp.Views
{
    public partial class RoutePageView : UserControl
    {
        public event Action? LogoPressed;
        public event Action? NavigateToDashboard;
        public event Action? NavigateToTrainSelect;

        public RoutePageView()
        {
            InitializeComponent();
        }

        private void OnLogoPressed(object? sender, PointerPressedEventArgs e) => LogoPressed?.Invoke();
        private void OnBackClick(object? sender, RoutedEventArgs e) => NavigateToDashboard?.Invoke();
        private void OnRouteClick(object? sender, RoutedEventArgs e) => NavigateToTrainSelect?.Invoke();
    }
}
