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
                var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

                var ffmpegBinaryPath = Path.Combine(baseDirectory, "../../../../../LEDDE.Library/FFmpeg");
                ffmpegBinaryPath = Path.GetFullPath(ffmpegBinaryPath); 

                if (Directory.Exists(ffmpegBinaryPath))
                {
                    Environment.SetEnvironmentVariable("PATH", $"{ffmpegBinaryPath};{Environment.GetEnvironmentVariable("PATH")}");
                    ffmpeg.RootPath = ffmpegBinaryPath;
                }
                else
                {
                    throw new DirectoryNotFoundException($"FFmpeg binaries not found in: {ffmpegBinaryPath}");
                }
            }
            else
            {
                throw new NotSupportedException("This platform is not supported. Please add support for other platforms as needed.");
            }
        }
    }
}
