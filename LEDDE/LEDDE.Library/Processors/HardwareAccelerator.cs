using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FFmpeg.AutoGen;
using LEDDE.Library.Processors.AutoGenHelpers;

namespace LEDDE.Library.Processors
{
    public class HardwareAccelerator
    {
        private readonly AVHWDeviceType[] preferredDecoders =
        [
            AVHWDeviceType.AV_HWDEVICE_TYPE_CUDA,
            AVHWDeviceType.AV_HWDEVICE_TYPE_DXVA2,
            AVHWDeviceType.AV_HWDEVICE_TYPE_VAAPI,
            AVHWDeviceType.AV_HWDEVICE_TYPE_QSV
        ];

        private List<AVHWDeviceType>? _availableDecoders;
        public List<AVHWDeviceType> AvailableDecoders => GetAvailableHWDecoders();
        
        private List<AVHWDeviceType> GetAvailableHWDecoders()
        {
            if (_availableDecoders == null)
            {
                _availableDecoders = [];
                var type = AVHWDeviceType.AV_HWDEVICE_TYPE_NONE;
                while ((type = ffmpeg.av_hwdevice_iterate_types(type)) != AVHWDeviceType.AV_HWDEVICE_TYPE_NONE)
                    _availableDecoders.Add(type);
            }
            return _availableDecoders;
        }
        public AVHWDeviceType ConfigureHWDecoder()
        {

            if (AvailableDecoders.Count == 0)
            {
                Logger.Log("No hardware decoders available. Falling back to software decoding.");
                return AVHWDeviceType.AV_HWDEVICE_TYPE_NONE;
            }

            foreach (var preferred in preferredDecoders)
            {
                if (AvailableDecoders.Contains(preferred))
                {
                    Logger.Log($"Using hardware decoder: {preferred}");
                    return preferred;
                }
            }

            var fallback = AvailableDecoders.First();
            Logger.Log($"Using fallback hardware decoder: {fallback}");
            return fallback;
        }
        public static AVPixelFormat GetHWPixelFormat(AVHWDeviceType hWDevice)
        {
            return hWDevice switch
            {
                AVHWDeviceType.AV_HWDEVICE_TYPE_NONE => AVPixelFormat.AV_PIX_FMT_NONE,
                AVHWDeviceType.AV_HWDEVICE_TYPE_VDPAU => AVPixelFormat.AV_PIX_FMT_VDPAU,
                AVHWDeviceType.AV_HWDEVICE_TYPE_CUDA => AVPixelFormat.AV_PIX_FMT_CUDA,
                AVHWDeviceType.AV_HWDEVICE_TYPE_VAAPI => AVPixelFormat.AV_PIX_FMT_VAAPI,
                AVHWDeviceType.AV_HWDEVICE_TYPE_DXVA2 => AVPixelFormat.AV_PIX_FMT_NV12,
                AVHWDeviceType.AV_HWDEVICE_TYPE_QSV => AVPixelFormat.AV_PIX_FMT_QSV,
                AVHWDeviceType.AV_HWDEVICE_TYPE_VIDEOTOOLBOX => AVPixelFormat.AV_PIX_FMT_VIDEOTOOLBOX,
                AVHWDeviceType.AV_HWDEVICE_TYPE_D3D11VA => AVPixelFormat.AV_PIX_FMT_NV12,
                AVHWDeviceType.AV_HWDEVICE_TYPE_DRM => AVPixelFormat.AV_PIX_FMT_DRM_PRIME,
                AVHWDeviceType.AV_HWDEVICE_TYPE_OPENCL => AVPixelFormat.AV_PIX_FMT_OPENCL,
                AVHWDeviceType.AV_HWDEVICE_TYPE_MEDIACODEC => AVPixelFormat.AV_PIX_FMT_MEDIACODEC,
                _ => AVPixelFormat.AV_PIX_FMT_NONE
            };
        }

    }
}
