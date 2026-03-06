using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Collections.ObjectModel;
using PhotoPatto.Models;
using PhotoPatto.Services;
using System.Threading.Tasks;
using System.Linq;
using WinForms = System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Threading;
using System;

namespace PhotoPatto
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ObservableCollection<ImageItem> _items = new ObservableCollection<ImageItem>();
        private int _currentIndex = -1;
        private FullscreenWindow? _fsWindow;
        private DispatcherTimer? _videoTimer;
        private bool _isVideoSeeking = false;
        private bool _shouldShowFirstFrame = false;

        public MainWindow()
        {
            InitializeComponent();
            ThumbnailList.ItemsSource = _items;

            // load settings
            SettingsManager.Load();

            PopulateMonitors();

            // wire events
            BtnSelectFolder.Click += BtnSelectFolder_Click;
            BtnNext.Click += BtnNext_Click;
            BtnPrev.Click += BtnPrev_Click;
            ThumbnailList.SelectionChanged += ThumbnailList_SelectionChanged;
            BtnRotateLeft.Click += BtnRotateLeft_Click;
            BtnRotateRight.Click += BtnRotateRight_Click;
            BtnBlack.Checked += BtnBlack_Checked;
            BtnBlack.Unchecked += BtnBlack_Unchecked;
            BtnShow.Checked += BtnShow_Checked;
            BtnShow.Unchecked += BtnShow_Unchecked;
            this.PreviewKeyDown += MainWindow_PreviewKeyDown;
            ComboSort.SelectionChanged += ComboSort_SelectionChanged;
            BtnSortOrder.Click += BtnSortOrder_Click;
            ComboMonitor.SelectionChanged += ComboMonitor_SelectionChanged;
            this.Closing += MainWindow_Closing;

            // Video controls
            BtnPlayPause.Checked += BtnPlayPause_CheckedChanged;
            BtnPlayPause.Unchecked += BtnPlayPause_CheckedChanged;
            BtnStop.Click += BtnStop_Click;
            BtnLoop.Checked += BtnLoop_CheckedChanged;
            BtnLoop.Unchecked += BtnLoop_CheckedChanged;
            VideoSeekBar.ValueChanged += VideoSeekBar_ValueChanged;
            PreviewVideo.MediaOpened += PreviewVideo_MediaOpened;
            PreviewVideo.MediaEnded += PreviewVideo_MediaEnded;

            // Video timer for updating seek bar
            _videoTimer = new DispatcherTimer();
            _videoTimer.Interval = TimeSpan.FromMilliseconds(100);
            _videoTimer.Tick += VideoTimer_Tick;

            // if last folder exists, load it
            if (!string.IsNullOrEmpty(SettingsManager.Settings.LastFolder) && System.IO.Directory.Exists(SettingsManager.Settings.LastFolder))
            {
                TxtCurrentFolder.Text = "フォルダ: " + SettingsManager.Settings.LastFolder;
                _ = LoadFolderAsync(SettingsManager.Settings.LastFolder);
            }

            // initialize sort UI from settings
            ComboSort.SelectedIndex = SettingsManager.Settings.SortKey == "Date" ? 1 : 0;
            BtnSortOrder.IsChecked = SettingsManager.Settings.SortDesc;
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // save settings
            SettingsManager.Settings.MonitorIndex = ComboMonitor.SelectedIndex >= 0 ? ComboMonitor.SelectedIndex : 0;
            SettingsManager.Save();
            // ensure fullscreen window is closed as well
            try
            {
                if (_fsWindow != null)
                {
                    _fsWindow.Close();
                    _fsWindow = null;
                }
            }
            catch
            {
            }
        }

        private async void MainWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            // If Show button is ON by default, create and show fullscreen window (black background until thumbnail is selected)
            if (BtnShow.IsChecked == true)
            {
                await EnsureFullscreenWindowAsync();
            }
        }

        private void PopulateMonitors()
        {
            try
            {
                var screens = WinForms.Screen.AllScreens;
                var hwnd = new WindowInteropHelper(this).Handle;
                var currentScreen = WinForms.Screen.FromHandle(hwnd);

                ComboMonitor.Items.Clear();
                for (int i = 0; i < screens.Length; i++)
                {
                    var s = screens[i];
                    var item = new ComboBoxItem();
                    // simpler label: Display 1 (1280x720)
                    item.Content = $"Display {i + 1} ({s.Bounds.Width}x{s.Bounds.Height})";
                    // disable the display that contains the main window
                    if (s.DeviceName == currentScreen.DeviceName)
                    {
                        item.IsEnabled = false;
                        item.ToolTip = "このディスプレイにはメインウィンドウがあります。選択できません。";
                    }
                    ComboMonitor.Items.Add(item);
                }

                // select saved monitor if valid and enabled, otherwise choose first enabled
                if (ComboMonitor.Items.Count > 0)
                {
                    int desired = SettingsManager.Settings.MonitorIndex;
                    if (desired >= 0 && desired < ComboMonitor.Items.Count)
                    {
                        var cbItem = ComboMonitor.Items[desired] as ComboBoxItem;
                        if (cbItem != null && cbItem.IsEnabled)
                        {
                            ComboMonitor.SelectedIndex = desired;
                            return;
                        }
                    }

                    // fallback: pick first enabled
                    for (int i = 0; i < ComboMonitor.Items.Count; i++)
                    {
                        var cbItem = ComboMonitor.Items[i] as ComboBoxItem;
                        if (cbItem == null || cbItem.IsEnabled)
                        {
                            ComboMonitor.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }
            catch
            {
                // ignore
            }
        }

        private async void BtnSelectFolder_Click(object? sender, RoutedEventArgs e)
        {
            using (var dlg = new WinForms.FolderBrowserDialog())
            {
                dlg.Description = "画像フォルダを選択してください";
                if (!string.IsNullOrEmpty(SettingsManager.Settings.LastFolder) && System.IO.Directory.Exists(SettingsManager.Settings.LastFolder))
                {
                    dlg.SelectedPath = SettingsManager.Settings.LastFolder;
                }

                var res = dlg.ShowDialog();
                if (res == WinForms.DialogResult.OK)
                {
                    SettingsManager.Settings.LastFolder = dlg.SelectedPath;
                    TxtCurrentFolder.Text = "フォルダ: " + dlg.SelectedPath;
                    await LoadFolderAsync(dlg.SelectedPath);
                }
            }
        }

        private async Task LoadFolderAsync(string folder)
        {
            // clear thumbnails immediately before loading
            _items.Clear();
            TxtStatus.Text = "読み込み中...";
            try
            {
                int count = 0;
                // Stream thumbnails one by one for faster display
                await foreach (var item in ImageLoader.LoadFromFolderStreamAsync(folder))
                {
                    // Ensure UI updates happen on UI thread
                    await Dispatcher.InvokeAsync(() =>
                    {
                        _items.Add(item);
                        count++;
                        TxtStatus.Text = $"読み込み中... {count} 件";
                    });
                }

                // apply current sort after all loaded
                ApplySort();
                TxtStatus.Text = $"読み込み完了: {_items.Count} 件";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "読み込みエラー" + ex.Message;
            }
        }

        private void ComboSort_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            // 0: ファイル名, 1: 更新日付
            SettingsManager.Settings.SortKey = ComboSort.SelectedIndex == 1 ? "Date" : "FileName";
            ApplySort();
        }

        private async void ComboMonitor_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            // If Show is ON and fullscreen window exists, move it to the new monitor immediately
            if (BtnShow.IsChecked == true && _fsWindow != null)
            {
                try
                {
                    var screens = WinForms.Screen.AllScreens;
                    int idx = ComboMonitor.SelectedIndex >= 0 ? ComboMonitor.SelectedIndex : 0;
                    if (idx >= 0 && idx < screens.Length)
                    {
                        await _fsWindow.ShowOnMonitorAsync(idx);
                    }
                }
                catch
                {
                    // ignore
                }
            }
        }

        private void BtnSortOrder_Click(object? sender, RoutedEventArgs e)
        {
            SettingsManager.Settings.SortDesc = BtnSortOrder.IsChecked == true;
            ApplySort();
        }

        private void ApplySort()
        {
            if (_items.Count <= 1) return;

            // remember current selection
            string? currentPath = null;
            if (_currentIndex >= 0 && _currentIndex < _items.Count) currentPath = _items[_currentIndex].FilePath;

            IOrderedEnumerable<ImageItem> ordered;
            bool desc = SettingsManager.Settings.SortDesc;
            if (ComboSort.SelectedIndex == 1)
            {
                ordered = desc ? _items.OrderByDescending(i => i.DateModified) : _items.OrderBy(i => i.DateModified);
            }
            else
            {
                ordered = desc ? _items.OrderByDescending(i => i.FileName) : _items.OrderBy(i => i.FileName);
            }

            var list = ordered.ToList();
            _items.Clear();
            foreach (var it in list) _items.Add(it);

            // restore selection if possible
            if (currentPath != null)
            {
                var idx = _items.ToList().FindIndex(i => i.FilePath == currentPath);
                if (idx >= 0) ThumbnailList.SelectedIndex = idx;
            }
        }

        private async Task EnsureFullscreenWindowAsync()
        {
            try
            {
                var screens = System.Windows.Forms.Screen.AllScreens;
                if (screens.Length <= 1)
                {
                    // don't create fullscreen window when only one monitor
                    return;
                }

                if (_fsWindow == null)
                {
                    _fsWindow = new FullscreenWindow();
                    // wire up keyboard navigation from fullscreen window
                    _fsWindow.OnNavigationKeyPressed = (isNext) =>
                    {
                        if (isNext)
                            BtnNext_Click(this, new RoutedEventArgs());
                        else
                            BtnPrev_Click(this, new RoutedEventArgs());
                    };
                    // wire up video started callback for synchronization
                    _fsWindow.OnVideoStarted = () =>
                    {
                        Dispatcher.InvokeAsync(() =>
                        {
                            PreviewVideo.Play();
                            _videoTimer?.Start();
                        }, System.Windows.Threading.DispatcherPriority.Background);
                    };
                }

                int idx = ComboMonitor.SelectedIndex >= 0 ? ComboMonitor.SelectedIndex : 0;
                if (idx >= screens.Length) idx = 0;
                await _fsWindow.ShowOnMonitorAsync(idx);
            }
            catch
            {
                // ignore
            }
        }

        private async void BtnBlack_Checked(object? sender, RoutedEventArgs e)
        {
            // only if Show is ON
            if (BtnShow.IsChecked != true) return;

            await EnsureFullscreenWindowAsync();
            if (_fsWindow != null)
            {
                _fsWindow.IsBlack = true;
            }
        }

        private void BtnBlack_Unchecked(object? sender, RoutedEventArgs e)
        {
            if (_fsWindow != null)
            {
                _fsWindow.IsBlack = false;
                // restore current image
                if (_currentIndex >= 0 && _currentIndex < _items.Count)
                {
                    var it = _items[_currentIndex];

                    // 動画で既にロード済みの場合は BlackOverlay だけ非表示になっている
                    // CrossfadeToImageAsync を呼んでも MediaOpened が発火しない可能性があるため不要
                    if (!it.IsVideo || !_fsWindow.IsVideoReady)
                    {
                        _ = _fsWindow.CrossfadeToImageAsync(it.FilePath, it.Rotation, SettingsManager.Settings.FadeMilliseconds);
                    }
                }
            }
        }

        private async void BtnShow_Checked(object? sender, RoutedEventArgs e)
        {
            // Show fullscreen window and wait for it to be fully rendered
            await EnsureFullscreenWindowAsync();

            // display current image if available
            if (_fsWindow != null && _currentIndex >= 0 && _currentIndex < _items.Count)
            {
                var it = _items[_currentIndex];
                if (BtnBlack.IsChecked == true)
                {
                    // 動画の場合は先にロードしてから Black にする
                    if (it.IsVideo)
                    {
                        await _fsWindow.LoadVideoAndShowFirstFrameAsync(it.FilePath);
                        // 少し待ってから Black を設定
                        await Task.Delay(50);
                    }
                    _fsWindow.IsBlack = true;
                }
                else
                {
                    _ = _fsWindow.CrossfadeToImageAsync(it.FilePath, it.Rotation, SettingsManager.Settings.FadeMilliseconds);
                }
            }
        }

        private void BtnShow_Unchecked(object? sender, RoutedEventArgs e)
        {
            // Hide/close fullscreen window
            if (_fsWindow != null)
            {
                _fsWindow.Close();
                _fsWindow = null;
            }
        }

        private async void ThumbnailList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (ThumbnailList.SelectedItem is ImageItem it)
            {
                _currentIndex = ThumbnailList.SelectedIndex;
                _ = UpdatePreviewAsync(it);
                // show on fullscreen only if Show button is ON
                if (BtnShow.IsChecked == true)
                {
                    await EnsureFullscreenWindowAsync();
                    if (_fsWindow != null && !_fsWindow.IsBlack)
                    {
                        _ = _fsWindow.CrossfadeToImageAsync(it.FilePath, it.Rotation, SettingsManager.Settings.FadeMilliseconds);
                    }
                }
            }
        }

        private async Task UpdatePreviewAsync(ImageItem it)
        {
            // Stop any playing video first
            StopVideo();

            if (it.IsVideo)
            {
                // Show video, hide image
                await Dispatcher.InvokeAsync(() =>
                {
                    ImageViewbox.Visibility = Visibility.Collapsed;
                    PreviewVideo.Visibility = Visibility.Visible;
                    VideoControlsPanel.Visibility = Visibility.Visible;
                    TxtSelectedFile.Visibility = Visibility.Visible;
                    TxtSelectedFile.Text = "ファイル: " + it.FileName;

                    _shouldShowFirstFrame = true;
                    PreviewVideo.Source = new Uri(it.FilePath, UriKind.Absolute);
                    // MediaOpenedイベントで最初のフレームが表示される
                });

                // Sync with fullscreen if showing
                if (_fsWindow != null && BtnShow.IsChecked == true)
                {
                    await _fsWindow.LoadVideoAndShowFirstFrameAsync(it.FilePath);
                }
            }
            else
            {
                // Show image, hide video
                await Dispatcher.InvokeAsync(() =>
                {
                    ImageViewbox.Visibility = Visibility.Visible;
                    PreviewVideo.Visibility = Visibility.Collapsed;
                    VideoControlsPanel.Visibility = Visibility.Collapsed;
                    TxtSelectedFile.Visibility = Visibility.Visible;

                    PreviewImage.Source = it.Thumbnail;
                    PreviewImage.LayoutTransform = new RotateTransform(it.Rotation);
                    TxtSelectedFile.Text = "ファイル: " + it.FileName;
                });

                try
                {
                    var preview = await ImageLoader.LoadPreviewAsync(it.FilePath, 1600, 1200);
                    await Dispatcher.InvokeAsync(() =>
                    {
                        PreviewImage.Source = preview;
                        PreviewImage.LayoutTransform = new RotateTransform(it.Rotation);
                    });
                }
                catch
                {
                    // ignore preview load errors
                }
            }
        }

        private void BtnNext_Click(object? sender, RoutedEventArgs e)
        {
            if (BtnBlack.IsChecked == true) return; // disabled when black
            if (_currentIndex < 0) return;
            if (_currentIndex + 1 < _items.Count)
            {
                ThumbnailList.SelectedIndex = _currentIndex + 1;
                ThumbnailList.ScrollIntoView(ThumbnailList.SelectedItem);
            }
        }

        private void BtnPrev_Click(object? sender, RoutedEventArgs e)
        {
            if (BtnBlack.IsChecked == true) return; // disabled when black
            if (_currentIndex <= 0) return;
            ThumbnailList.SelectedIndex = _currentIndex - 1;
            ThumbnailList.ScrollIntoView(ThumbnailList.SelectedItem);
        }

        

        private void BtnRotateLeft_Click(object? sender, RoutedEventArgs e)
        {
            if (_currentIndex < 0) return;
            var it = _items[_currentIndex];
            it.Rotation = (it.Rotation - 90) % 360;
            if (it.Rotation < 0) it.Rotation += 360;
            _ = UpdatePreviewAsync(it);
            // update fullscreen immediately only if Show is ON
            if (BtnShow.IsChecked == true && _fsWindow != null && !_fsWindow.IsBlack)
            {
                _ = _fsWindow.CrossfadeToImageAsync(it.FilePath, it.Rotation, SettingsManager.Settings.FadeMilliseconds);
            }
        }

        private void BtnRotateRight_Click(object? sender, RoutedEventArgs e)
        {
            if (_currentIndex < 0) return;
            var it = _items[_currentIndex];
            it.Rotation = (it.Rotation + 90) % 360;
            _ = UpdatePreviewAsync(it);
            // update fullscreen immediately only if Show is ON
            if (BtnShow.IsChecked == true && _fsWindow != null && !_fsWindow.IsBlack)
            {
                _ = _fsWindow.CrossfadeToImageAsync(it.FilePath, it.Rotation, SettingsManager.Settings.FadeMilliseconds);
            }
        }

        private void MainWindow_PreviewKeyDown(object? sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Right)
            {
                BtnNext_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.Left)
            {
                BtnPrev_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
        }

        // Video control methods
        private void BtnPlayPause_CheckedChanged(object? sender, RoutedEventArgs e)
        {
            if (PreviewVideo.Source == null) return;

            if (BtnPlayPause.IsChecked == true)
            {
                // 再生開始 - アイコンを一時停止に切り替え
                PlayIcon.Visibility = Visibility.Collapsed;
                PauseIcon.Visibility = Visibility.Visible;

                // Fullscreenを先に再生（観客向けをスムーズに）
                if (_fsWindow != null && BtnShow.IsChecked == true && BtnBlack.IsChecked != true)
                {
                    _fsWindow.PlayVideo();
                    // OnVideoStartedコールバックでPreviewVideoが再生される
                    // ただし、既に準備完了済みの場合はコールバックが呼ばれないので、その場合はここで再生
                    if (_fsWindow.IsVideoReady)
                    {
                        PreviewVideo.Play();
                        _videoTimer?.Start();
                    }
                }
                else
                {
                    // Fullscreenなしの場合は即座に再生
                    PreviewVideo.Play();
                    _videoTimer?.Start();
                }
            }
            else
            {
                // 一時停止 - アイコンを再生に切り替え
                PlayIcon.Visibility = Visibility.Visible;
                PauseIcon.Visibility = Visibility.Collapsed;

                PreviewVideo.Pause();
                _videoTimer?.Stop();

                // Sync with fullscreen
                if (_fsWindow != null && BtnShow.IsChecked == true && BtnBlack.IsChecked != true)
                {
                    _fsWindow.PauseVideo();
                }
            }
        }

        private void BtnStop_Click(object? sender, RoutedEventArgs e)
        {
            StopVideo();

            // 停止時はボタンを再生状態に戻す
            BtnPlayPause.IsChecked = false;
        }

        private void BtnLoop_CheckedChanged(object? sender, RoutedEventArgs e)
        {
            // Loop state changed - will be checked in MediaEnded event
        }

        private void StopVideo()
        {
            if (PreviewVideo.Source != null)
            {
                PreviewVideo.Position = TimeSpan.Zero;
                PreviewVideo.Play();
                PreviewVideo.Pause();
                _videoTimer?.Stop();

                // Reset seek bar and time display
                _isVideoSeeking = true;
                VideoSeekBar.Value = 0;
                TxtCurrentTime.Text = "00:00";
                _isVideoSeeking = false;

                // Sync with fullscreen
                if (_fsWindow != null && BtnShow.IsChecked == true && BtnBlack.IsChecked != true)
                {
                    _fsWindow.StopVideo();
                }
            }
        }

        private void VideoSeekBar_ValueChanged(object? sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isVideoSeeking || PreviewVideo.Source == null || !PreviewVideo.NaturalDuration.HasTimeSpan)
                return;

            _isVideoSeeking = true;
            var totalSeconds = PreviewVideo.NaturalDuration.TimeSpan.TotalSeconds;
            var newPosition = TimeSpan.FromSeconds(totalSeconds * (VideoSeekBar.Value / 100.0));
            PreviewVideo.Position = newPosition;

            // Sync position with fullscreen
            if (_fsWindow != null && BtnShow.IsChecked == true && BtnBlack.IsChecked != true)
            {
                _fsWindow.SeekVideo(newPosition);
            }

            _isVideoSeeking = false;
        }

        private void PreviewVideo_MediaOpened(object? sender, RoutedEventArgs e)
        {
            if (PreviewVideo.NaturalDuration.HasTimeSpan)
            {
                TxtTotalTime.Text = FormatTime(PreviewVideo.NaturalDuration.TimeSpan);
            }

            // フラグがセットされていたら最初のフレームを表示
            if (_shouldShowFirstFrame)
            {
                PreviewVideo.Position = TimeSpan.Zero;
                PreviewVideo.Play();
                PreviewVideo.Pause();
                _shouldShowFirstFrame = false;
            }
        }

        private void PreviewVideo_MediaEnded(object? sender, RoutedEventArgs e)
        {
            // Check if loop is enabled
            if (BtnLoop.IsChecked == true)
            {
                PreviewVideo.Position = TimeSpan.Zero;
                PreviewVideo.Play();

                // Sync with fullscreen
                if (_fsWindow != null && BtnShow.IsChecked == true && BtnBlack.IsChecked != true)
                {
                    _fsWindow.SeekVideo(TimeSpan.Zero);
                    _fsWindow.PlayVideo();
                }

                // ループ再生時はボタンを再生中状態に保つ
                BtnPlayPause.IsChecked = true;
            }
            else
            {
                StopVideo();

                // 動画終了時はボタンを再生状態に戻す
                BtnPlayPause.IsChecked = false;
            }
        }

        private void VideoTimer_Tick(object? sender, EventArgs e)
        {
            if (PreviewVideo.Source == null || !PreviewVideo.NaturalDuration.HasTimeSpan)
                return;

            _isVideoSeeking = true;
            var totalSeconds = PreviewVideo.NaturalDuration.TimeSpan.TotalSeconds;
            if (totalSeconds > 0)
            {
                VideoSeekBar.Value = (PreviewVideo.Position.TotalSeconds / totalSeconds) * 100.0;
            }
            TxtCurrentTime.Text = FormatTime(PreviewVideo.Position);
            _isVideoSeeking = false;
        }

        private string FormatTime(TimeSpan time)
        {
            return $"{(int)time.TotalMinutes:D2}:{time.Seconds:D2}";
        }
    }
}
