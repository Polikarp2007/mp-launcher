using Avalonia.Controls;
using Avalonia.Media;
using System.Threading.Tasks;

namespace PoliCoLauncherApp.Views
{
    public partial class WelcomePageView : UserControl
    {
        public WelcomePageView()
        {
            InitializeComponent();
        }

        public async Task PlayFlipAnimation()
        {
            var welcomeTransform = WelcomeText.RenderTransform as ScaleTransform;
            var taglineTransform = TaglineText.RenderTransform as ScaleTransform;
            if (welcomeTransform == null || taglineTransform == null) return;

            const int steps = 20;
            const int stepDelay = 15;

            for (int i = steps; i >= 0; i--)
            {
                welcomeTransform.ScaleY = (double)i / steps;
                await Task.Delay(stepDelay);
            }
            WelcomeText.Opacity = 0;
            WelcomeText.IsVisible = false;

            TaglineText.IsVisible = true;
            TaglineText.Opacity = 1;
            for (int i = 0; i <= steps; i++)
            {
                taglineTransform.ScaleY = (double)i / steps;
                await Task.Delay(stepDelay);
            }
        }
    }
}
