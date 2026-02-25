using System;
using System.ComponentModel;
using System.IO;
using System.Windows.Media;

namespace PhotoPatto.Models
{
    public class ImageItem : INotifyPropertyChanged
    {
        public string FilePath { get; }
        public string FileName => Path.GetFileName(FilePath);
        public DateTime DateModified { get; }

        private ImageSource? _thumbnail;
        public ImageSource? Thumbnail
        {
            get => _thumbnail;
            set { _thumbnail = value; OnPropertyChanged(nameof(Thumbnail)); }
        }

        // Rotation in degrees (0,90,180,270)
        private int _rotation;
        public int Rotation
        {
            get => _rotation;
            set { _rotation = value; OnPropertyChanged(nameof(Rotation)); }
        }

        public ImageItem(string filePath, DateTime dateModified)
        {
            FilePath = filePath;
            DateModified = dateModified;
            Rotation = 0;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
