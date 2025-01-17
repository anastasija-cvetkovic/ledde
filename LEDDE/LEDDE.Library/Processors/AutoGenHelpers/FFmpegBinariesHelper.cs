using FFmpeg.AutoGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LEDDE.Library.Processors.AutoGenHelpers
{
    public class FFmpegBinariesHelper
    {
        public static void RegisterFFmpegBinaries()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var current = Environment.CurrentDirectory;
                var probe = Path.Combine("LEDDE.Library", "FFmpeg");

                while (current != null)
                {
                    //Check if the FFmpeg directory exists
                    var ffmpegBinaryPath = Path.Combine(current, probe);

                    if (Directory.Exists(ffmpegBinaryPath))
                    {
                        Console.WriteLine($"FFmpeg binaries found in: {ffmpegBinaryPath}");
                        Environment.SetEnvironmentVariable("PATH", $"{ffmpegBinaryPath};{Environment.GetEnvironmentVariable("PATH")}");
                        ffmpeg.RootPath = ffmpegBinaryPath;
                        return;
                    }

                    current = Directory.GetParent(current)?.FullName;
                }
                throw new DirectoryNotFoundException("FFmpeg binaries not found in the expected solution root directory.");
            }
            else
                throw new NotSupportedException("This platform is not supported. Please add support for other platforms as needed.");
        }
    }
}
