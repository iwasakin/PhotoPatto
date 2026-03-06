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
                    // 動画準備中でない場合のみ停止・削除
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

            // フラグがセットされていたら最初のフレームを表示
            if (_shouldShowFirstFrame)
            {
                // 元々の黒画面状態を保存
                bool wasBlack = BlackOverlay.Visibility == Visibility.Visible;
                System.Diagnostics.Debug.WriteLine($"[FS] MediaOpened: wasBlack={wasBlack}, BlackOverlay.Visibility={BlackOverlay.Visibility}");

                // デコーダー初期化のため一時的に黒画面にする（元々黒くなければ）
                if (!wasBlack)
                {
                    System.Diagnostics.Debug.WriteLine("[FS] MediaOpened: Setting BlackOverlay to Visible");
                    BlackOverlay.Visibility = Visibility.Visible;
                }

                FullscreenVideo.Position = TimeSpan.Zero;
                FullscreenVideo.Play();
                System.Diagnostics.Debug.WriteLine("[FS] MediaOpened: Started Play, waiting 100ms...");

                // デコーダー初期化のため100ms待つ
                await Task.Delay(100);

                FullscreenVideo.Pause();
                FullscreenVideo.Position = TimeSpan.Zero;
                System.Diagnostics.Debug.WriteLine("[FS] MediaOpened: Paused, waiting 100ms...");
                await Task.Delay(100); // Position設定後に待機

                // 元々黒画面でなかった場合のみ非表示に戻す
                if (!wasBlack)
                {
                    System.Diagnostics.Debug.WriteLine("[FS] MediaOpened: Setting BlackOverlay to Collapsed");
                    BlackOverlay.Visibility = Visibility.Collapsed;
                }

                _shouldShowFirstFrame = false;
                System.Diagnostics.Debug.WriteLine($"[FS] MediaOpened: Complete, BlackOverlay.Visibility={BlackOverlay.Visibility}");
                System.Diagnostics.Debug.WriteLine($"[FS] MediaOpened: Complete, FullscreenVideo.Visibility={FullscreenVideo.Visibility}, ImgA.Visibility={ImgA.Visibility}, ImgB.Visibility={ImgB.Visibility}");
            }

            // 保留中の再生があれば開始
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

            // Wait for MediaElement to be fully loaded
            System.Diagnostics.Debug.WriteLine($"[FS] ShowOnMonitorAsync: FullscreenVideo.IsLoaded={FullscreenVideo.IsLoaded}");
            if (!FullscreenVideo.IsLoaded)
            {
                System.Diagnostics.Debug.WriteLine("[FS] ShowOnMonitorAsync: Waiting for FullscreenVideo.Loaded...");
                var mediaTcs = new TaskCompletionSource<bool>();
                RoutedEventHandler? mediaHandler = null;
                mediaHandler = (s, e) =>
                {
                    System.Diagnostics.Debug.WriteLine("[FS] ShowOnMonitorAsync: FullscreenVideo.Loaded fired");
                    FullscreenVideo.Loaded -= mediaHandler;
                    mediaTcs.TrySetResult(true);
                };
                FullscreenVideo.Loaded += mediaHandler;
                await mediaTcs.Task;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[FS] ShowOnMonitorAsync: FullscreenVideo already loaded");
            }

            System.Diagnostics.Debug.WriteLine("[FS] ShowOnMonitorAsync: Complete");
        }

        public async Task CrossfadeToImageAsync(string filePath, int rotationDegrees, int fadeMilliseconds)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            var isVideo = _videoExtensions.Contains(ext);
            System.Diagnostics.Debug.WriteLine($"[FS] CrossfadeToImageAsync: {System.IO.Path.GetFileName(filePath)}, isVideo={isVideo}, IsBlack={IsBlack}");

            // 動画以外で黒画面の場合は変更を無視
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

                _shouldShowFirstFrame = true;
                _pendingPlay = false;
                System.Diagnostics.Debug.WriteLine($"[FS] LoadVideoAndShowFirstFrameAsync: Visibility set, yielding to Dispatcher...");
            });

            // Visibility変更をUIスレッドに処理させる
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);
            System.Diagnostics.Debug.WriteLine($"[FS] LoadVideoAndShowFirstFrameAsync: Dispatcher yielded, setting Source...");

            await Dispatcher.InvokeAsync(() =>
            {
                FullscreenVideo.Source = new Uri(filePath, UriKind.Absolute);
                System.Diagnostics.Debug.WriteLine($"[FS] LoadVideoAndShowFirstFrameAsync: FullscreenVideo.Visibility={FullscreenVideo.Visibility}, ImgA.Visibility={ImgA.Visibility}, ImgB.Visibility={ImgB.Visibility}");
                // MediaOpenedイベントで最初のフレームが表示される
            });
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
                        // 準備完了済み
                        FullscreenVideo.Play();
                    }
                    else
                    {
                        // まだ準備中、保留
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
