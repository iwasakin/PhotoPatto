using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace PhotoPatto
{
    public partial class FullscreenWindow : Window
    {
        private bool _showingA = true;
        private static readonly string[] _videoExtensions = new[] { ".mp4", ".avi", ".mov", ".wmv", ".mkv", ".flv", ".webm" };
        private bool _shouldShowFirstFrame = false;
        private bool _pendingPlay = false;
        public Action<bool>? OnNavigationKeyPressed { get; set; } // true = Next, false = Prev
        public Action? OnVideoStarted { get; set; } // Fullscreen video started playing

        public bool IsVideoReady => FullscreenVideo.Source != null && FullscreenVideo.NaturalDuration.HasTimeSpan;

        public bool IsBlack
        {
            get => BlackOverlay.Visibility == Visibility.Visible;
            set
            {
                BlackOverlay.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                if (value)
                {
                    // 蜍慕判貅門ｙ荳ｭ縺ｧ縺ｪ縺・ｴ蜷医・縺ｿ蛛懈ｭ｢繝ｻ蜑企勁
                    if (FullscreenVideo.Source != null && !_shouldShowFirstFrame)
                    {
                        FullscreenVideo.Stop();
                        FullscreenVideo.Source = null;
                        FullscreenVideo.Visibility = Visibility.Collapsed;
                    }
                }
            }
        }

        public FullscreenWindow()
        {
            InitializeComponent();
            this.PreviewKeyDown += FullscreenWindow_PreviewKeyDown;
            FullscreenVideo.MediaOpened += FullscreenVideo_MediaOpened;
        }

        private async void FullscreenVideo_MediaOpened(object? sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[FS] MediaOpened: _shouldShowFirstFrame={_shouldShowFirstFrame}, _pendingPlay={_pendingPlay}");

            // 繝輔Λ繧ｰ縺後そ繝・ヨ縺輔ｌ縺ｦ縺・◆繧画怙蛻昴・繝輔Ξ繝ｼ繝繧定｡ｨ遉ｺ
            if (_shouldShowFirstFrame)
            {
                // 蜈・・・鮟堤判髱｢迥ｶ諷九ｒ菫晏ｭ・
                bool wasBlack = BlackOverlay.Visibility == Visibility.Visible;
                System.Diagnostics.Debug.WriteLine($"[FS] MediaOpened: wasBlack={wasBlack}, BlackOverlay.Visibility={BlackOverlay.Visibility}");

                // 繝・さ繝ｼ繝繝ｼ蛻晄悄蛹悶・縺溘ａ荳譎ら噪縺ｫ鮟堤判髱｢縺ｫ縺吶ｋ・亥・縲・ｻ偵￥縺ｪ縺代ｌ縺ｰ・・
                if (!wasBlack)
                {
                    System.Diagnostics.Debug.WriteLine("[FS] MediaOpened: Setting BlackOverlay to Visible");
                    BlackOverlay.Visibility = Visibility.Visible;
                }

                FullscreenVideo.Position = TimeSpan.Zero;
                FullscreenVideo.Play();
                System.Diagnostics.Debug.WriteLine("[FS] MediaOpened: Started Play, waiting 100ms...");

                // 繝・さ繝ｼ繝繝ｼ蛻晄悄蛹悶・縺溘ａ100ms蠕・▽
                await Task.Delay(100);

                FullscreenVideo.Pause();
                FullscreenVideo.Position = TimeSpan.Zero;
                System.Diagnostics.Debug.WriteLine("[FS] MediaOpened: Paused, waiting 100ms...");
                await Task.Delay(100); // Position險ｭ螳壼ｾ後↓蠕・ｩ・

                // 蜈・・ｻ堤判髱｢縺ｧ縺ｪ縺九▲縺溷ｴ蜷医・縺ｿ髱櫁｡ｨ遉ｺ縺ｫ謌ｻ縺・
                if (!wasBlack)
                {
                    System.Diagnostics.Debug.WriteLine("[FS] MediaOpened: Setting BlackOverlay to Collapsed");
                    BlackOverlay.Visibility = Visibility.Collapsed;
                }

                _shouldShowFirstFrame = false;
                System.Diagnostics.Debug.WriteLine($"[FS] MediaOpened: Complete, BlackOverlay.Visibility={BlackOverlay.Visibility}");
                System.Diagnostics.Debug.WriteLine($"[FS] MediaOpened: Complete, FullscreenVideo.Visibility={FullscreenVideo.Visibility}, ImgA.Visibility={ImgA.Visibility}, ImgB.Visibility={ImgB.Visibility}");
            }

            // 菫晉蕗荳ｭ縺ｮ蜀咲函縺後≠繧後・髢句ｧ・
            if (_pendingPlay)
            {
                System.Diagnostics.Debug.WriteLine("[FS] MediaOpened: Starting pending play");
                FullscreenVideo.Play();
                _pendingPlay = false;
                OnVideoStarted?.Invoke();
            }
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

        public async Task ShowOnMonitorAsync(int monitorIndex)
        {
            System.Diagnostics.Debug.WriteLine($"[FS] ShowOnMonitorAsync: Start, IsVisible={this.IsVisible}");

            var screens = System.Windows.Forms.Screen.AllScreens;
            if (monitorIndex < 0 || monitorIndex >= screens.Length) monitorIndex = 0;
            var s = screens[monitorIndex];

            // Position window to the selected screen bounds
            this.WindowStartupLocation = WindowStartupLocation.Manual;
            this.Left = s.Bounds.Left;
            this.Top = s.Bounds.Top;
            this.Width = s.Bounds.Width;
            this.Height = s.Bounds.Height;

            // If window is already visible, no need to wait
            if (this.IsVisible)
            {
                System.Diagnostics.Debug.WriteLine("[FS] ShowOnMonitorAsync: Already visible, returning");
                return;
            }

            System.Diagnostics.Debug.WriteLine("[FS] ShowOnMonitorAsync: Waiting for ContentRendered...");
            // Show window and wait for ContentRendered to ensure it's fully initialized
            var tcs = new TaskCompletionSource<bool>();
            EventHandler? handler = null;
            handler = (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine("[FS] ShowOnMonitorAsync: ContentRendered fired");
                this.ContentRendered -= handler;
                tcs.TrySetResult(true);
            };
            this.ContentRendered += handler;
            this.Show();
            await tcs.Task;

            System.Diagnostics.Debug.WriteLine("[FS] ShowOnMonitorAsync: Complete");
        }

        public async Task CrossfadeToImageAsync(string filePath, int rotationDegrees, int fadeMilliseconds)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            var isVideo = _videoExtensions.Contains(ext);
            System.Diagnostics.Debug.WriteLine($"[FS] CrossfadeToImageAsync: {System.IO.Path.GetFileName(filePath)}, isVideo={isVideo}, IsBlack={IsBlack}");

            // 蜍慕判莉･螟悶〒鮟堤判髱｢縺ｮ蝣ｴ蜷医・螟画峩繧堤┌隕・
            if (IsBlack && !isVideo)
            {
                System.Diagnostics.Debug.WriteLine("[FS] CrossfadeToImageAsync: Skipping (IsBlack && !isVideo)");
                return;
            }

            if (isVideo)
            {
                System.Diagnostics.Debug.WriteLine("[FS] CrossfadeToImageAsync: Calling LoadVideoAndShowFirstFrameAsync");
                // Video will be loaded separately via LoadVideoAndShowFirstFrameAsync
                await LoadVideoAndShowFirstFrameAsync(filePath);
            }
            else
            {
                // Hide video, show images
                await Dispatcher.InvokeAsync(() =>
                {
                    if (FullscreenVideo.Source != null)
                    {
                        FullscreenVideo.Stop();
                        FullscreenVideo.Source = null;
                        FullscreenVideo.Visibility = Visibility.Collapsed;
                    }

                    ImgA.Visibility = Visibility.Visible;
                    ImgB.Visibility = Visibility.Visible;
                });

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

        // Video synchronization methods
        public async Task LoadVideoAndShowFirstFrameAsync(string filePath)
        {
            System.Diagnostics.Debug.WriteLine($"[FS] LoadVideoAndShowFirstFrameAsync: {System.IO.Path.GetFileName(filePath)}");
            await Dispatcher.InvokeAsync(() =>
            {
                ImgA.Visibility = Visibility.Collapsed;
                ImgB.Visibility = Visibility.Collapsed;
                FullscreenVideo.Visibility = Visibility.Visible;
                FullscreenVideo.Opacity = 1;

                _shouldShowFirstFrame = true;
                _pendingPlay = false;
                System.Diagnostics.Debug.WriteLine($"[FS] LoadVideoAndShowFirstFrameAsync: Visibility set, waiting for Loaded...");
            });

            // Loaded繧､繝吶Φ繝亥ｾ・ｩ・
            await Dispatcher.InvokeAsync(async () =>
            {
                if (FullscreenVideo.IsLoaded)
                {
                    System.Diagnostics.Debug.WriteLine($"[FS] LoadVideoAndShowFirstFrameAsync: FullscreenVideo already loaded, setting Source...");
                    // Force reload to avoid missing the first MediaOpened after startup.
                    FullscreenVideo.Stop();
                    FullscreenVideo.Source = null;
                    FullscreenVideo.Source = new Uri(filePath, UriKind.Absolute);
                    System.Diagnostics.Debug.WriteLine($"[FS] LoadVideoAndShowFirstFrameAsync: Source set (IsLoaded), FullscreenVideo.Visibility={FullscreenVideo.Visibility}, ImgA.Visibility={ImgA.Visibility}, ImgB.Visibility={ImgB.Visibility}");
                }
                else
                {
                    RoutedEventHandler? loadedHandler = null;
                    loadedHandler = (s, e) =>
                    {
                        FullscreenVideo.Loaded -= loadedHandler;
                        System.Diagnostics.Debug.WriteLine($"[FS] LoadVideoAndShowFirstFrameAsync: FullscreenVideo Loaded event fired, setting Source...");
                        // Force reload to avoid missing the first MediaOpened after startup.
                        FullscreenVideo.Stop();
                        FullscreenVideo.Source = null;
                        FullscreenVideo.Source = new Uri(filePath, UriKind.Absolute);
                        System.Diagnostics.Debug.WriteLine($"[FS] LoadVideoAndShowFirstFrameAsync: Source set (Loaded event), FullscreenVideo.Visibility={FullscreenVideo.Visibility}, ImgA.Visibility={ImgA.Visibility}, ImgB.Visibility={ImgB.Visibility}");
                    };
                    FullscreenVideo.Loaded += loadedHandler;
                }
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        public void LoadVideo(string filePath)
        {
            Dispatcher.Invoke(() =>
            {
                ImgA.Visibility = Visibility.Collapsed;
                ImgB.Visibility = Visibility.Collapsed;
                FullscreenVideo.Visibility = Visibility.Visible;

                _shouldShowFirstFrame = false;
                FullscreenVideo.Source = new Uri(filePath, UriKind.Absolute);
            });
        }

        public void PlayVideo()
        {
            Dispatcher.Invoke(() =>
            {
                if (FullscreenVideo.Source != null)
                {
                    if (FullscreenVideo.NaturalDuration.HasTimeSpan)
                    {
                        // 貅門ｙ螳御ｺ・ｸ医∩
                        FullscreenVideo.Play();
                    }
                    else
                    {
                        // 縺ｾ縺貅門ｙ荳ｭ縲∽ｿ晉蕗
                        _pendingPlay = true;
                    }
                }
            });
        }

        public void PauseVideo()
        {
            Dispatcher.Invoke(() =>
            {
                if (FullscreenVideo.Source != null)
                {
                    _pendingPlay = false;
                    FullscreenVideo.Pause();
                }
            });
        }

        public void StopVideo()
        {
            Dispatcher.Invoke(() =>
            {
                if (FullscreenVideo.Source != null)
                {
                    _pendingPlay = false;
                    FullscreenVideo.Position = TimeSpan.Zero;
                    FullscreenVideo.Play();
                    FullscreenVideo.Pause();
                }
            });
        }

        public void SeekVideo(TimeSpan position)
        {
            Dispatcher.Invoke(() =>
            {
                if (FullscreenVideo.Source != null)
                {
                    FullscreenVideo.Position = position;
                }
            });
        }
    }
}

