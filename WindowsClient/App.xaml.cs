using FFmpeg.AutoGen;
using System.Configuration;
using System.Data;
using System.Windows;

namespace WindowsClient
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            ffmpeg.RootPath = Environment.GetEnvironmentVariable("FFMPEG_ROOT_PATH");
        }
    }

}
