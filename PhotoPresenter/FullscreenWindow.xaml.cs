using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace PhotoPresenter
{
    public partial class FullscreenWindow : Window
    {
        private bool _showingA = true;
        public Action<bool>? OnNavigationKeyPressed { get; set; } // true = Next, false = Prev

        public bool IsBlack
        {
            get => BlackOverlay.Visibility == Visibility.Visible;
            set => BlackOverlay.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }

        public FullscreenWindow()
        {
            InitializeComponent();
            this.PreviewKeyDown += FullscreenWindow_PreviewKeyDown;
        }

        private void FullscreenWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Right)
            {
                OnNavigationKeyPressed?.Invoke(true); // Next
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.Left)
            {
                OnNavigationKeyPressed?.Invoke(false); // Prev
                e.Handled = true;
            }
        }

        public void ShowOnMonitor(int monitorIndex)
        {
            var screens = System.Windows.Forms.Screen.AllScreens;
            if (monitorIndex < 0 || monitorIndex >= screens.Length) monitorIndex = 0;
            var s = screens[monitorIndex];

            // Position window to the selected screen bounds
            this.WindowStartupLocation = WindowStartupLocation.Manual;
            this.Left = s.Bounds.Left;
            this.Top = s.Bounds.Top;
            this.Width = s.Bounds.Width;
            this.Height = s.Bounds.Height;

            // ensure visible
            this.Show();
        }

        public async Task CrossfadeToImageAsync(string filePath, int rotationDegrees, int fadeMilliseconds)
        {
            if (IsBlack) return; // when black, ignore changes

            // load image
            BitmapImage bi = new BitmapImage();
            using (var fs = File.OpenRead(filePath))
            {
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                bi.StreamSource = fs;
                bi.EndInit();
                bi.Freeze();
            }

            await Dispatcher.InvokeAsync(() =>
            {
                System.Windows.Controls.Image target = _showingA ? ImgB : ImgA;
                System.Windows.Controls.Image other = _showingA ? ImgA : ImgB;

                target.Source = bi;
                target.Opacity = 0;
                target.LayoutTransform = new RotateTransform(rotationDegrees);
                other.LayoutTransform = new RotateTransform(0);

                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(fadeMilliseconds));
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(fadeMilliseconds));

                target.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                other.BeginAnimation(UIElement.OpacityProperty, fadeOut);

                _showingA = !_showingA;
            });
        }
    }
}
