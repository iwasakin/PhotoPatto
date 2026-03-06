using System.Configuration;
using System.Data;
using System.Windows;

namespace PhotoPatto
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            // No FFmpeg initialization needed - using Windows Runtime APIs
        }
    }
}
