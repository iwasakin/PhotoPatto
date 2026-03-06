using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using PhotoPatto.Models;
using System.Windows.Media;
using System.Windows;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace PhotoPatto.Services
{
    public static class ImageLoader
    {
        private static readonly string[] _imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
        private static readonly string[] _videoExtensions = new[] { ".mp4", ".avi", ".mov", ".wmv", ".mkv", ".flv", ".webm" };

        // Stream thumbnails one by one for faster initial display
        public static async IAsyncEnumerable<ImageItem> LoadFromFolderStreamAsync(string folder)
        {
            await Task.CompletedTask; // make method truly async

            var allFiles = Directory.EnumerateFiles(folder)
                .Where(f =>
                {
                    var ext = Path.GetExtension(f).ToLowerInvariant();
                    return _imageExtensions.Contains(ext) || _videoExtensions.Contains(ext);
                })
                .OrderBy(f => f)
                .ToList();

            foreach (var filePath in allFiles)
            {
                var ext = Path.GetExtension(filePath).ToLowerInvariant();
                var isVideo = _videoExtensions.Contains(ext);

                var item = new ImageItem(filePath, File.GetLastWriteTimeUtc(filePath), isVideo);

                // Load thumbnail on background thread
                await Task.Run(async () =>
                {
                    try
                    {
                        if (isVideo)
                        {
                            // Windowsのサムネイル機能を使用
                            item.Thumbnail = await CreateVideoThumbnailAsync(filePath, 150, 90);
                        }
                        else
                        {
                            var thumb = CreateThumbnail(filePath, 150, 90);
                            item.Thumbnail = thumb;
                        }
                    }
                    catch
                    {
                        // ignore individual load failures
                    }
                });

                yield return item;
            }
        }

        public static async Task<List<ImageItem>> LoadFromFolderAsync(string folder)
        {
            var list = new List<ImageItem>();
            await foreach (var item in LoadFromFolderStreamAsync(folder))
            {
                list.Add(item);
            }
            return list;
        }

        private static ImageSource CreateThumbnail(string path, int maxWidth, int maxHeight)
        {
            // Load into BitmapImage with DecodePixelWidth for thumbnail
            var bi = new BitmapImage();
            using (var fs = File.OpenRead(path))
            {
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                bi.DecodePixelWidth = maxWidth;
                bi.StreamSource = fs;
                bi.EndInit();
                bi.Freeze();
            }

            // Try to correct orientation using metadata
            try
            {
                using (var fs = File.OpenRead(path))
                {
                    var frame = BitmapFrame.Create(fs, BitmapCreateOptions.DelayCreation, BitmapCacheOption.OnLoad);
                    if (frame != null && frame.Metadata is BitmapMetadata meta)
                    {
                        object? o = null;
                        if (meta.ContainsQuery("/app1/ifd/{ushort=274}"))
                        {
                            o = meta.GetQuery("/app1/ifd/{ushort=274}");
                        }

                        if (o is ushort orientation)
                        {
                            TransformedBitmap? transformed = null;
                            switch (orientation)
                            {
                                case 3:
                                    var rt3 = new System.Windows.Media.RotateTransform(180);
                                    rt3.Freeze();
                                    transformed = new TransformedBitmap(bi, rt3);
                                    break;
                                case 6:
                                    var rt6 = new System.Windows.Media.RotateTransform(90);
                                    rt6.Freeze();
                                    transformed = new TransformedBitmap(bi, rt6);
                                    break;
                                case 8:
                                    var rt8 = new System.Windows.Media.RotateTransform(270);
                                    rt8.Freeze();
                                    transformed = new TransformedBitmap(bi, rt8);
                                    break;
                            }
                            if (transformed != null)
                            {
                                transformed.Freeze();
                                return transformed;
                            }
                        }
                    }
                }
            }
            catch
            {
                // ignore metadata errors
            }

            return bi;
        }

        private static async Task<ImageSource> CreateVideoThumbnailAsync(string path, int maxWidth, int maxHeight)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Extracting Windows thumbnail for: {path}");

                // StorageFileを取得（Windowsのファイルシステム経由）
                var file = await StorageFile.GetFileFromPathAsync(path);

                // Windowsのサムネイルを取得（エクスプローラーと同じ機能）
                var thumbnail = await file.GetThumbnailAsync(
                    ThumbnailMode.VideosView,
                    (uint)maxWidth,
                    ThumbnailOptions.UseCurrentScale);

                if (thumbnail != null && thumbnail.Size > 0)
                {
                    // Streamから BitmapImageを作成
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = thumbnail.AsStreamForRead();
                    bitmap.DecodePixelWidth = maxWidth;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    System.Diagnostics.Debug.WriteLine($"  Thumbnail extracted successfully via Windows API");
                    return bitmap;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Windows thumbnail extraction failed: {ex.Message}");
            }

            // 失敗した場合はプレースホルダーを返す
            System.Diagnostics.Debug.WriteLine($"Falling back to placeholder for {Path.GetFileName(path)}");
            return CreateVideoPlaceholder(maxWidth, maxHeight, Path.GetFileName(path));
        }

        private static ImageSource CreateVideoPlaceholder(int maxWidth, int maxHeight, string? fileName = null)
        {
            var drawingVisual = new DrawingVisual();
            using (var context = drawingVisual.RenderOpen())
            {
                // Draw background with gradient
                var gradient = new System.Windows.Media.LinearGradientBrush(
                    System.Windows.Media.Color.FromRgb(45, 45, 48),
                    System.Windows.Media.Color.FromRgb(30, 30, 33),
                    90);
                context.DrawRectangle(gradient, null, new System.Windows.Rect(0, 0, maxWidth, maxHeight));

                // Draw filmstrip border effect
                var borderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 85));
                var borderPen = new System.Windows.Media.Pen(borderBrush, 2);
                context.DrawRectangle(null, borderPen, new System.Windows.Rect(2, 2, maxWidth - 4, maxHeight - 4));

                // Draw play icon (larger and centered)
                var center = new System.Windows.Point(maxWidth / 2.0, maxHeight / 2.0);
                var playIcon = new System.Windows.Media.PathGeometry();
                var figure = new System.Windows.Media.PathFigure { StartPoint = new System.Windows.Point(center.X - 12, center.Y - 16) };
                figure.Segments.Add(new System.Windows.Media.LineSegment(new System.Windows.Point(center.X + 18, center.Y), true));
                figure.Segments.Add(new System.Windows.Media.LineSegment(new System.Windows.Point(center.X - 12, center.Y + 16), true));
                figure.IsClosed = true;
                playIcon.Figures.Add(figure);

                var playBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255));
                playBrush.Opacity = 0.9;
                context.DrawGeometry(playBrush, null, playIcon);

                // Draw "VIDEO" text
                var videoText = new System.Windows.Media.FormattedText(
                    "🎬 VIDEO",
                    System.Globalization.CultureInfo.CurrentCulture,
                    System.Windows.FlowDirection.LeftToRight,
                    new System.Windows.Media.Typeface(new System.Windows.Media.FontFamily("Segoe UI"), 
                        System.Windows.FontStyles.Normal, 
                        System.Windows.FontWeights.SemiBold, 
                        System.Windows.FontStretches.Normal),
                    10,
                    new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 200, 200)),
                    96);
                context.DrawText(videoText, new System.Windows.Point((maxWidth - videoText.Width) / 2, 8));
            }

            var bitmap = new RenderTargetBitmap(maxWidth, maxHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(drawingVisual);
            bitmap.Freeze();
            return bitmap;
        }

        public static async Task<ImageSource> LoadPreviewAsync(string path, int maxWidth, int maxHeight)
        {
            return await Task.Run(() =>
            {
                var bi = new BitmapImage();
                using (var fs = File.OpenRead(path))
                {
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                    // set decode pixel to limit memory but keep quality
                    bi.DecodePixelWidth = maxWidth;
                    bi.StreamSource = fs;
                    bi.EndInit();
                    bi.Freeze();
                }

                // apply EXIF orientation if present
                try
                {
                    using (var fs = File.OpenRead(path))
                    {
                        var frame = BitmapFrame.Create(fs, BitmapCreateOptions.DelayCreation, BitmapCacheOption.OnLoad);
                        if (frame != null && frame.Metadata is BitmapMetadata meta)
                        {
                            object? o = null;
                            if (meta.ContainsQuery("/app1/ifd/{ushort=274}"))
                            {
                                o = meta.GetQuery("/app1/ifd/{ushort=274}");
                            }

                            if (o is ushort orientation)
                            {
                                TransformedBitmap? transformed = null;
                                switch (orientation)
                                {
                                    case 3:
                                        var rt3 = new System.Windows.Media.RotateTransform(180);
                                        rt3.Freeze();
                                        transformed = new TransformedBitmap(bi, rt3);
                                        break;
                                    case 6:
                                        var rt6 = new System.Windows.Media.RotateTransform(90);
                                        rt6.Freeze();
                                        transformed = new TransformedBitmap(bi, rt6);
                                        break;
                                    case 8:
                                        var rt8 = new System.Windows.Media.RotateTransform(270);
                                        rt8.Freeze();
                                        transformed = new TransformedBitmap(bi, rt8);
                                        break;
                                }
                                if (transformed != null)
                                {
                                    transformed.Freeze();
                                    return transformed;
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // ignore
                }

                return bi as ImageSource;
            });
        }
    }
}
