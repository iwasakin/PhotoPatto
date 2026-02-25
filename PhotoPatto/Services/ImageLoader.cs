using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using PhotoPatto.Models;
using System.Windows.Media;

namespace PhotoPatto.Services
{
    public static class ImageLoader
    {
        private static readonly string[] _extensions = new[] { ".jpg", ".jpeg", ".png", ".bmp" };

        // Stream thumbnails one by one for faster initial display
        public static async IAsyncEnumerable<ImageItem> LoadFromFolderStreamAsync(string folder)
        {
            await Task.CompletedTask; // make method truly async

            var files = Directory.EnumerateFiles(folder)
                .Where(f => _extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f)
                .ToList();

            foreach (var filePath in files)
            {
                var item = new ImageItem(filePath, File.GetLastWriteTimeUtc(filePath));

                // Load thumbnail on background thread
                await Task.Run(() =>
                {
                    try
                    {
                        var thumb = CreateThumbnail(filePath, 150, 90);
                        item.Thumbnail = thumb;
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
