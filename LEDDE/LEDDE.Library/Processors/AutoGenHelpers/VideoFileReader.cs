using FFmpeg.AutoGen;
using LEDDE.Library.LED;
using SixLabors.ImageSharp.PixelFormats;
using System.Runtime.InteropServices;

namespace LEDDE.Library.Processors.AutoGenHelpers
{
    public unsafe class VideoFileReader : IDisposable
    {
        private AVFormatContext* _formatContext = null;
        private AVCodecContext* _codecContext = null;
        private AVCodec* _codec = null;
        private AVFrame* _frame = null;
        private AVPacket* _packet = null;
        private int _videoStreamIndex = -1;
        public int FrameCount = 0;
        const int AVERROR_EAGAIN = -11;

        private AVBufferRef* _hwDeviceContext = null;

        private byte* _yuv420pBuffer;

        private unsafe delegate AVPixelFormat GetFormatDelegate(AVCodecContext* ctx, AVPixelFormat* fmt);


        public VideoFileReader(string videoPath, AVHWDeviceType hwDeviceType = AVHWDeviceType.AV_HWDEVICE_TYPE_DXVA2)
        {
            InitializeFormatContext(videoPath);
            InitializeVideoStream();
            InitializeCodec(hwDeviceType);
            AllocateFrameAndPacket();

            FrameCount = GetTotalFrames();
            Logger.Log($"Total number of frames: {FrameCount}");
        }
        private int GetTotalFrames()
        {
            // Ensure the video stream index is initialized
            if (_videoStreamIndex == -1)
            {
                throw new InvalidOperationException("Video stream not initialized.");
            }

            // Get the stream corresponding to the video stream index
            var videoStream = _formatContext->streams[_videoStreamIndex];

            // Check if nb_frames is available and return it
            if (videoStream->nb_frames > 0)
            {
                return (int)videoStream->nb_frames; // Return the number of frames in the stream
            }

            // If nb_frames is not available, we can try calculating it manually
            // by decoding the video or using the duration of the video and the frame rate.
            double frameRate = videoStream->avg_frame_rate.num / (double)videoStream->avg_frame_rate.den;
            double durationInSeconds = _formatContext->duration / 1000000.0; // Convert duration from microseconds to seconds

            // Calculate total frames manually
            int totalFrames = (int)(frameRate * durationInSeconds);
            return totalFrames;
        }
        private AVFrame* ConvertPixelFormatToYUV420P(AVCodecContext* codecContext, AVFrame* inputFrame)
        {
            // Allocate a new frame for the converted format
            AVFrame* yuv420pFrame = ffmpeg.av_frame_alloc();
            if (yuv420pFrame == null)
            {
                throw new InvalidOperationException("Failed to allocate YUV420P frame.");
            }

            // Set frame properties for the YUV420P format
            yuv420pFrame->width = codecContext->width;
            yuv420pFrame->height = codecContext->height;
            yuv420pFrame->format = (int)AVPixelFormat.AV_PIX_FMT_YUV420P;

            // Allocate buffer for the YUV420P frame
            int bufferSize = ffmpeg.av_image_get_buffer_size(AVPixelFormat.AV_PIX_FMT_YUV420P, codecContext->width, codecContext->height, 1);
            byte* buffer = (byte*)ffmpeg.av_malloc((ulong)bufferSize);
            if (buffer == null)
            {
                ffmpeg.av_frame_free(&yuv420pFrame);
                throw new InvalidOperationException("Failed to allocate buffer for YUV420P frame.");
            }

            // Create temporary variables for the first 4 pointers
            byte_ptrArray4 yuvData = new byte_ptrArray4();
            int_array4 yuvLineSize = new int_array4();

            // Copy relevant data from rgbaFrame->data and rgbaFrame->linesize
            for (uint i = 0; i < 4; i++)
            {
                yuvData[i] = yuv420pFrame->data[i];
                yuvLineSize[i] = yuv420pFrame->linesize[i];
            }

            ffmpeg.av_image_fill_arrays(
                ref yuvData,
                ref yuvLineSize,
                buffer,
                AVPixelFormat.AV_PIX_FMT_YUV420P,
                codecContext->width,
                codecContext->height,
                1);

            // Create a scaling context for the conversion
            SwsContext* swsCtx = ffmpeg.sws_getContext(
                codecContext->width, codecContext->height, codecContext->pix_fmt,
                codecContext->width, codecContext->height, AVPixelFormat.AV_PIX_FMT_YUV420P,
                ffmpeg.SWS_BILINEAR, null, null, null);

            if (swsCtx == null)
            {
                ffmpeg.av_free(buffer);
                ffmpeg.av_frame_free(&yuv420pFrame);
                throw new InvalidOperationException("Failed to create SwsContext for YUV420P conversion.");
            }

            // Perform the conversion
            ffmpeg.sws_scale(
                swsCtx,
                inputFrame->data,
                inputFrame->linesize,
                0,
                codecContext->height,
                yuv420pFrame->data,
                yuv420pFrame->linesize);

            // Free the scaling context
            ffmpeg.sws_freeContext(swsCtx);

            return yuv420pFrame;
        }
        private void InitializeFormatContext(string videoPath)
        {
            fixed (AVFormatContext** formatContextPtr = &_formatContext)
            {
                if (ffmpeg.avformat_open_input(formatContextPtr, videoPath, null, null) != 0)
                {
                    throw new InvalidOperationException("Failed to open video file.");
                }
            }

            if (ffmpeg.avformat_find_stream_info(_formatContext, null) < 0)
                throw new InvalidOperationException("Failed to retrieve stream info.");
        }
        private void InitializeVideoStream()
        {
            for (int i = 0; i < _formatContext->nb_streams; i++)
            {
                if (_formatContext->streams[i]->codecpar->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
                {
                    _videoStreamIndex = i;
                    break;
                }
            }

            if (_videoStreamIndex == -1)
                throw new InvalidOperationException("No video stream found.");
        }
        private void InitializeCodec(AVHWDeviceType hwDeviceType)
        {
            _codec = ffmpeg.avcodec_find_decoder(_formatContext->streams[_videoStreamIndex]->codecpar->codec_id);

            if (_codec == null)
                throw new InvalidOperationException("Failed to find video codec.");

            _codecContext = ffmpeg.avcodec_alloc_context3(_codec);
            if (_codecContext == null)
                throw new InvalidOperationException("Failed to allocate codec context.");

            if (ffmpeg.avcodec_parameters_to_context(_codecContext, _formatContext->streams[_videoStreamIndex]->codecpar) < 0)
                throw new InvalidOperationException("Failed to copy codec parameters to codec context.");

            if (EnableHardwareAcceleration(hwDeviceType) == false)
            {
                Logger.Log("Hardware acceleration not supported for this codec, falling back to software decoding.");
            }

            //_codecContext->get_format = GetFormatCallback;

            if (ffmpeg.avcodec_open2(_codecContext, _codec, null) < 0)
                throw new InvalidOperationException("Failed to open codec.");
        }

        private static AVPixelFormat[] GetPixelFormats(AVPixelFormat* fmt)
        {
            var formats = new List<AVPixelFormat>();
            while (*fmt != AVPixelFormat.AV_PIX_FMT_NONE)
            {
                formats.Add(*fmt);
                fmt++;
            }
            return formats.ToArray();
        }

        private static AVPixelFormat GetFormatCallback(AVCodecContext* ctx, AVPixelFormat* fmt)
        {
            var formats = GetPixelFormats(fmt); // Convert the format array
            Logger.Log($"Available pixel formats: {string.Join(", ", formats)}");

            foreach (var format in formats)
            {
                if (format == AVPixelFormat.AV_PIX_FMT_NV12) // Hardware-accelerated format
                {
                    Logger.Log("Selecting hardware-accelerated pixel format: AV_PIX_FMT_NV12");
                    return AVPixelFormat.AV_PIX_FMT_NV12;
                }
            }

            Logger.Log("No hardware-accelerated pixel format found. Falling back to default.");
            return formats[0];
        }

        private bool EnableHardwareAcceleration(AVHWDeviceType hwDeviceType)
        {
            AVHWDeviceType[] availableDeviceTypes = {
            AVHWDeviceType.AV_HWDEVICE_TYPE_DXVA2, // Windows (DXVA2)
            AVHWDeviceType.AV_HWDEVICE_TYPE_CUDA, // NVIDIA (CUDA)
            AVHWDeviceType.AV_HWDEVICE_TYPE_VAAPI, // Linux (VAAPI)
            AVHWDeviceType.AV_HWDEVICE_TYPE_VDPAU, // Linux (VDPAU)
            AVHWDeviceType.AV_HWDEVICE_TYPE_QSV,   // Intel Quick Sync (QSV)
            AVHWDeviceType.AV_HWDEVICE_TYPE_OPENCL, // OpenCL (cross-platform)
            };
            foreach (var deviceType in availableDeviceTypes)
            {
                fixed (AVBufferRef** hwDeviceContextPtr = &_hwDeviceContext)
                {
                    if (ffmpeg.av_hwdevice_ctx_create(hwDeviceContextPtr, deviceType, null, null, 0) >= 0)
                    {
                        Logger.Log($"Hardware acceleration enabled using {deviceType}.");
                        _codecContext->hw_device_ctx = ffmpeg.av_buffer_ref(_hwDeviceContext);
                        return true; // Success, hardware acceleration enabled.
                    }
                }
            }
            Logger.Log("Hardware acceleration not supported.");
            return false; // Failed, hardware acceleration not available.
        }
        private void AllocateFrameAndPacket()
        {
            _frame = ffmpeg.av_frame_alloc();
            _packet = ffmpeg.av_packet_alloc();

            if (_frame == null || _packet == null)
            {
                throw new InvalidOperationException("Failed to allocate memory for frame or packet.");
            }
        }

        public bool ReadNextFrame(out LEDMatrix frameImage)
        {
            frameImage = null;

            if (ffmpeg.av_read_frame(_formatContext, _packet) < 0)
                return false;

            if (_packet->stream_index != _videoStreamIndex)
                return false;

            if (ffmpeg.avcodec_send_packet(_codecContext, _packet) < 0)
                return false;

            while (true)
            {
                int receiveFrameResult = ffmpeg.avcodec_receive_frame(_codecContext, _frame);
                if (receiveFrameResult == AVERROR_EAGAIN)
                    continue;
                else if (receiveFrameResult < 0)
                    return false;

                Logger.Log($"Frame received with format: {(AVPixelFormat)_frame->format}");

                if (_frame->format == (int)AVPixelFormat.AV_PIX_FMT_NV12)
                {
                    Logger.Log("Hardware frame received. Converting...");
                    frameImage = ConvertToLEDMatrixFromHardwareFrame(_frame);
                }
                else
                {
                    Logger.Log("Software frame received.");
                    frameImage = ConvertToLEDMatrixOptimized(*_frame);
                }
                return true;
            }
        }

        private LEDMatrix ConvertToLEDMatrixFromHardwareFrame(AVFrame* frame)
        {
            // Ensure the frame format matches expectations
            if (frame->format != (int)AVPixelFormat.AV_PIX_FMT_NV12)
            {
                throw new InvalidOperationException("Unsupported pixel format. Expected NV12.");
            }

            // Extract frame dimensions
            int width = frame->width;
            int height = frame->height;

            // Create the LEDMatrix
            var ledMatrix = new LEDMatrix(width, height);

            // Extract pointers and strides
            byte* yPlane = frame->data[0];
            byte* uvPlane = frame->data[1];
            int yStride = frame->linesize[0];
            int uvStride = frame->linesize[1];

            // Parallelize the processing of rows
            Parallel.For(0, height, y =>
            {
                int uvRow = y / 2; // UV is subsampled by 2 in height
                byte* yRow = yPlane + y * yStride;
                byte* uvRowPtr = uvPlane + uvRow * uvStride;

                for (int x = 0; x < width; x++)
                {
                    // Calculate UV indices for every second pixel
                    int uvIndex = (x / 2) * 2;

                    // Fetch Y, U, and V values
                    byte yValue = yRow[x];
                    byte uValue = uvRowPtr[uvIndex];
                    byte vValue = uvRowPtr[uvIndex + 1];

                    // Convert YUV to RGB and set pixel color
                    ledMatrix.SetPixelColor(x, y, FastYuvToRgb(yValue, uValue, vValue));
                }
            });

            return ledMatrix;
        }

        // Optimized YUV to RGB conversion
        private static Rgba32 FastYuvToRgb(byte y, byte u, byte v)
        {
            // Precompute constants
            const int shift = 8;
            const int maxVal = 255;

            int c = y - 16;
            int d = u - 128;
            int e = v - 128;

            // Approximation with pre-shifted coefficients to reduce multiplications
            int r = (298 * c + 409 * e + (1 << (shift - 1))) >> shift;
            int g = (298 * c - 100 * d - 208 * e + (1 << (shift - 1))) >> shift;
            int b = (298 * c + 516 * d + (1 << (shift - 1))) >> shift;

            // Clamp values using a faster branching method
            r = r < 0 ? 0 : r > maxVal ? maxVal : r;
            g = g < 0 ? 0 : g > maxVal ? maxVal : g;
            b = b < 0 ? 0 : b > maxVal ? maxVal : b;

            return new Rgba32((byte)r, (byte)g, (byte)b);
        }

        public unsafe LEDMatrix ConvertToLEDMatrixOptimized(AVFrame sourceFrame)
        {
            // Precompute constants for YUV to RGB conversion
            const int offsetY = 16, offsetUV = 128;
            const int coeffY = 298, coeffR = 409, coeffG1 = 100, coeffG2 = 208, coeffB = 516;

            // Create LEDMatrix with dimensions of the source frame
            var ledMatrix = new LEDMatrix(sourceFrame.width, sourceFrame.height);

            // Extract frame dimensions and data
            int width = sourceFrame.width;
            int height = sourceFrame.height;
            int linesizeY = sourceFrame.linesize[0];
            int linesizeU = sourceFrame.linesize[1];
            int linesizeV = sourceFrame.linesize[2];
            byte* dataY = sourceFrame.data[0];
            byte* dataU = sourceFrame.data[1];
            byte* dataV = sourceFrame.data[2];

            // Use parallelism for rows
            Parallel.For(0, height, y =>
            {
                int yHalf = y / 2;
                for (int x = 0; x < width; x++)
                {
                    int xHalf = x / 2;

                    // Calculate indices for Y, U, and V planes
                    int yIndex = y * linesizeY + x;
                    int uIndex = yHalf * linesizeU + xHalf;
                    int vIndex = yHalf * linesizeV + xHalf;

                    // Fetch YUV values
                    int c = dataY[yIndex] - offsetY;
                    int d = dataU[uIndex] - offsetUV;
                    int e = dataV[vIndex] - offsetUV;

                    // Compute RGB values using optimized formula
                    byte r = ClampToByte((coeffY * c + coeffR * e + 128) >> 8);
                    byte g = ClampToByte((coeffY * c - coeffG1 * d - coeffG2 * e + 128) >> 8);
                    byte b = ClampToByte((coeffY * c + coeffB * d + 128) >> 8);

                    // Update LEDMatrix pixel
                    ledMatrix.SetPixelColor(x, y, new Rgba32(r, g, b));
                }
            });

            return ledMatrix;
        }

        private static byte ClampToByte(int value)
        {
            return (byte)(value < 0 ? 0 : (value > 255 ? 255 : value));
        }
   
        private static (byte R, byte G, byte B) ConvertYuvToRgb(byte y, byte u, byte v)
        {
            // Convert YUV to RGB using standard formulas
            int c = y - 16;
            int d = u - 128;
            int e = v - 128;

            int r = (298 * c + 409 * e + 128) >> 8;
            int g = (298 * c - 100 * d - 208 * e + 128) >> 8;
            int b = (298 * c + 516 * d + 128) >> 8;

            // Clamp values to the byte range [0, 255]
            r = Math.Clamp(r, 0, 255);
            g = Math.Clamp(g, 0, 255);
            b = Math.Clamp(b, 0, 255);

            return ((byte)r, (byte)g, (byte)b);
        }

        public void Dispose()
        {
            // Ensure the pointers are pinned before passing them to unmanaged FFmpeg functions
            fixed (AVPacket** packetPtr = &_packet)
            {
                ffmpeg.av_packet_free(packetPtr);
            }

            fixed (AVFrame** framePtr = &_frame)
            {
                ffmpeg.av_frame_free(framePtr);
            }

            fixed (AVCodecContext** codecContextPtr = &_codecContext)
            {
                ffmpeg.avcodec_free_context(codecContextPtr);
            }

            fixed (AVFormatContext** formatContextPtr = &_formatContext)
            {
                ffmpeg.avformat_close_input(formatContextPtr);
            }

            GC.SuppressFinalize(this);
        }
    }
}
