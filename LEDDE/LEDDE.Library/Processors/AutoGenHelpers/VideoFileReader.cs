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
        private AVFrame* _frame = null;
        private int _videoStreamIndex = -1;
        public int FrameCount { get; private set; }
        private byte* _frameBuffer = null;

        const int AVERROR_EAGAIN = -11;
        private const int AVERROR_EOF = -541478725;

        private AVHWDeviceType _hwDevice;
        private AVBufferRef* _hwDeviceContext = null;
        private SwsContext* _swsContext = null;
        private AVFrame* _convertedFrame = null;

        const int AV_CODEC_HW_CONFIG_METHOD_HW_DEVICE_CTX = 0x01;

        private FFmpeg.AutoGen.AVCodecContext_get_format _getFormatDelegate;

        public VideoFileReader(string videoPath)
        {
            var hwAccelerator = new HardwareAccelerator();
            _hwDevice = hwAccelerator.ConfigureHWDecoder();

            InitializeFormatContext(videoPath);
            InitializeVideoStream();
            AllocateFrame();
            InitializeCodec(hwAccelerator);
            InitializeConversionContext();
            FrameCount = GetTotalFrames();
            Logger.Log($"number of read frames is {FrameCount}");
        }
        private int GetTotalFrames()
        {
            var videoStream = _formatContext->streams[_videoStreamIndex];
            long durationInMicroseconds = _formatContext->duration; // Duration in microseconds

            // If the video stream has the number of frames (nb_frames), use that if available
            if (videoStream->nb_frames > 0)
            {
                return (int)videoStream->nb_frames;
            }

            // Estimate the total frame count if nb_frames is not available
            double frameRate = videoStream->avg_frame_rate.num / (double)videoStream->avg_frame_rate.den;
            double durationInSeconds = durationInMicroseconds / 1000000.0;

            // Calculate the total frame count as an integer, ensuring that it's at least 1 frame.
            int totalFrames = (int)Math.Ceiling(frameRate * durationInSeconds);  // Use Ceil to round up to avoid fractional frames

            // Ensure totalFrames is at least 1, as even a very short video should have at least one frame
            return totalFrames > 0 ? totalFrames : 1;
        }
        private int EstimateFrameCount()
        {
            var videoStream = _formatContext->streams[_videoStreamIndex];
            double frameRate = videoStream->avg_frame_rate.num / (double)videoStream->avg_frame_rate.den;
            double durationInSeconds = _formatContext->duration / 1000000.0;
            return (int)(frameRate * durationInSeconds);
        }
        private void InitializeFormatContext(string videoPath)
        {
            fixed (AVFormatContext** formatContextPtr = &_formatContext)
            {
                if (ffmpeg.avformat_open_input(formatContextPtr, videoPath, null, null) != 0)
                    throw new InvalidOperationException("Failed to open video file.");
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
        private void AllocateFrame()
        {
            _frame = ffmpeg.av_frame_alloc();
            if (_frame == null)
                throw new InvalidOperationException("Failed to allocate memory for frame.");
        }
        private void AllocateFrameBuffer()
        {
            _frame = ffmpeg.av_frame_alloc();
            if (_frame == null)
                throw new InvalidOperationException("Failed to allocate memory for frame.");

            int bufferSize = ffmpeg.av_image_get_buffer_size(AVPixelFormat.AV_PIX_FMT_RGB24, _formatContext->streams[_videoStreamIndex]->codecpar->width, _formatContext->streams[_videoStreamIndex]->codecpar->height, 1);
            _frameBuffer = (byte*)ffmpeg.av_malloc((ulong)bufferSize);
            if (_frameBuffer == null)
                throw new InvalidOperationException("Failed to allocate memory for frame buffer.");
        }
        private void InitializeCodec(HardwareAccelerator hardwareAccelerator)
        {
            var codecParams = _formatContext->streams[_videoStreamIndex]->codecpar;
            var codec = ffmpeg.avcodec_find_decoder(codecParams->codec_id);
            if (codec == null)
                throw new InvalidOperationException("Failed to find video codec.");

            _codecContext = ffmpeg.avcodec_alloc_context3(codec);
            if (_codecContext == null)
                throw new InvalidOperationException("Failed to allocate codec context.");

            if (ffmpeg.avcodec_parameters_to_context(_codecContext, _formatContext->streams[_videoStreamIndex]->codecpar) < 0)
                throw new InvalidOperationException("Failed to copy codec parameters to codec context.");

            // Configure hardware device if available
            if (_hwDevice != AVHWDeviceType.AV_HWDEVICE_TYPE_NONE)
            {
                // Get compatible HW config for the codec
                AVCodecHWConfig* config = null;
                for (int i = 0; ; i++)
                {
                    config = ffmpeg.avcodec_get_hw_config(codec, i);
                    if (config == null) break;
                    if (config->device_type == _hwDevice &&
                       (config->methods & AV_CODEC_HW_CONFIG_METHOD_HW_DEVICE_CTX) != 0)
                    {
                        break;
                    }
                }
                if (config != null)
                {
                    fixed (AVBufferRef** hwDeviceContext = &_hwDeviceContext)
                    {
                        ffmpeg.av_hwdevice_ctx_create(hwDeviceContext, _hwDevice, null, null, 0);
                    }
                    _codecContext->hw_device_ctx = ffmpeg.av_buffer_ref(_hwDeviceContext);

                    // Store "this" in the opaque field
                    _codecContext->opaque = (void*)GCHandle.ToIntPtr(GCHandle.Alloc(this));

                    // 2. Assign the static method to AutoGen’s delegate
                    _getFormatDelegate = GetHwFormat;

                    // 3. Convert the delegate to AutoGen’s struct
                    FFmpeg.AutoGen.AVCodecContext_get_format_func getFormatFunc = _getFormatDelegate;

                    // 4. Assign the struct to FFmpeg’s get_format field
                    _codecContext->get_format = getFormatFunc;
                }
            
            }

            if (ffmpeg.avcodec_open2(_codecContext, codec, null) < 0)
                throw new InvalidOperationException("Failed to open codec.");
        }

        // Static callback method (matches FFmpeg.AutoGen's delegate signature)
        private static AVPixelFormat GetHwFormat(AVCodecContext* ctx, AVPixelFormat* fmt)
        {
            var handle = GCHandle.FromIntPtr((IntPtr)ctx->opaque);
            var instance = (VideoFileReader)handle.Target; 
            return instance.InstanceGetHwFormat(ctx, fmt);
        }

        private AVPixelFormat InstanceGetHwFormat(AVCodecContext* ctx, AVPixelFormat* fmt)
        {
            var hwPixelFormat = HardwareAccelerator.GetHWPixelFormat(_hwDevice);

            while (*fmt != AVPixelFormat.AV_PIX_FMT_NONE)
            {
                if (*fmt == hwPixelFormat)
                    return *fmt;
                fmt++;
            }

            return AVPixelFormat.AV_PIX_FMT_NONE;
        }

        private void InitializeConversionContext()
        {
            // Prepare to convert decoded frames to RGB24
            _swsContext = ffmpeg.sws_getContext(
                _codecContext->width, _codecContext->height, _codecContext->pix_fmt,
                _codecContext->width, _codecContext->height, AVPixelFormat.AV_PIX_FMT_RGB24,
                ffmpeg.SWS_BILINEAR, null, null, null
            );

            _convertedFrame = ffmpeg.av_frame_alloc();
            _convertedFrame->format = (int)AVPixelFormat.AV_PIX_FMT_RGB24;
            _convertedFrame->width = _codecContext->width;
            _convertedFrame->height = _codecContext->height;
            ffmpeg.av_frame_get_buffer(_convertedFrame, 0);
        }

        private bool DecodeFrame(AVPacket* packet)
        {
            int sendResult = ffmpeg.avcodec_send_packet(_codecContext, packet);
            if (sendResult < 0)
            {
                // Log error and exit
                Logger.Log($"Error sending packet to codec: {sendResult}");
                return false;
            }

            // Receive frame
            int receiveResult = ffmpeg.avcodec_receive_frame(_codecContext, _frame);
            if (receiveResult < 0)
            {
                if (receiveResult == AVERROR_EAGAIN)
                {
                    // Need more data, skip this packet
                    return false;
                }

                Logger.Log($"Error receiving frame: {receiveResult}");
                return false;
            }

            // Transfer hardware frame to CPU if needed
            if (_frame->hw_frames_ctx != null)
            {
                var tmpFrame = ffmpeg.av_frame_alloc();
                ffmpeg.av_hwframe_transfer_data(tmpFrame, _frame, 0);
                ffmpeg.av_frame_unref(_frame);
                ffmpeg.av_frame_move_ref(_frame, tmpFrame);
                ffmpeg.av_frame_free(&tmpFrame);
            }

            // Convert to RGB24 using SWScale
            ffmpeg.sws_scale(_swsContext, _frame->data, _frame->linesize, 0, _frame->height,
                            _convertedFrame->data, _convertedFrame->linesize);

            return true;
        }
        private LEDMatrix ProcessRGBFrame(AVFrame* frame)
        {
            var ledMatrix = new LEDMatrix(frame->width, frame->height);
            byte* data = frame->data[0];
            int stride = frame->linesize[0];

            Parallel.For(0, frame->height, y =>
            {
                for (int x = 0; x < frame->width; x++)
                {
                    int index = y * stride + x * 3; // RGB24 has 3 bytes per pixel
                    byte r = data[index];
                    byte g = data[index + 1];
                    byte b = data[index + 2];
                    ledMatrix.SetPixelColor(x, y, new Rgba32(r, g, b));
                }
            });

            return ledMatrix;
        }
        public bool ReadNextFrame(out LEDMatrix frameImage, ref int c)
        {
            frameImage = null;

            AVPacket* packet = ffmpeg.av_packet_alloc();
            if (packet == null)
                throw new InvalidOperationException("Failed to allocate packet.");

            try
            {
                while (true)
                {
                    Logger.Log($"{c}");
                    c++;

                    int readResult = ffmpeg.av_read_frame(_formatContext, packet);

                    if (readResult < 0)
                    {
                        // Handle EOF or read error
                        if (readResult == AVERROR_EOF)
                        {
                            Logger.Log("AVERROR_EOF, end of video stream reached?");
                            return false;
                        }
                        else if (readResult == AVERROR_EAGAIN)
                        {
                            Logger.Log("AVEERROR_EAGAIN, temporary error occurred, retrying..");
                        }
                        Logger.Log("Error reading frame from video stream");
                    }

                    if (packet->stream_index == _videoStreamIndex)
                    {
                        if (DecodeFrame(packet))
                        {
                            Logger.Log($"frame format {_frame->format}");
                            frameImage = ConvertToLEDMatrixOptimized(*_frame);
                            return true;
                        }
                        else Logger.Log("Failed to decode frame");
                    }

                    ffmpeg.av_packet_unref(packet);

                }
            }
            finally
            {
                ffmpeg.av_packet_free(&packet);
            }
        }
   
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
            // Use _convertedFrame which is in RGB24
            return ProcessRGBFrame(_convertedFrame);

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

            // Use parallelism for processing rows
            Parallel.For(0, height, y =>
            {
                for (int x = 0; x < width; x++)
                {
                    // Calculate indices for Y, U, and V planes
                    int yIndex = y * linesizeY + x;  // Y plane
                    int uIndex = (y / 2) * linesizeU + (x / 2); // U plane (subsampled)
                    int vIndex = (y / 2) * linesizeV + (x / 2); // V plane (subsampled)

                    // Fetch Y, U, V values
                    byte yValue = dataY[yIndex];
                    byte uValue = dataU[uIndex];
                    byte vValue = dataV[vIndex];

                    // Convert YUV to RGB
                    int c = yValue - offsetY;
                    int d = uValue - offsetUV;
                    int e = vValue - offsetUV;

                    byte r = ClampToByte((coeffY * c + coeffR * e + 128) >> 8);
                    byte g = ClampToByte((coeffY * c - coeffG1 * d - coeffG2 * e + 128) >> 8);
                    byte b = ClampToByte((coeffY * c + coeffB * d + 128) >> 8);

                    // Set the RGB value in the LEDMatrix
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
            if (_frame != null)
            {
                fixed (AVFrame** framePtr = &_frame)
                    ffmpeg.av_frame_free(framePtr);
            }

            if (_codecContext != null)
            {
                fixed (AVCodecContext** codecContextPtr = &_codecContext)
                    ffmpeg.avcodec_free_context(codecContextPtr);
            }

            if (_formatContext != null)
            {
                fixed (AVFormatContext** formatContextPtr = &_formatContext)
                    ffmpeg.avformat_close_input(formatContextPtr);
            }

            if (_swsContext != null) 
            {
                fixed(SwsContext** swsContextPtr = &_swsContext)
                    ffmpeg.sws_freeContext(_swsContext);
            }
                

            if (_convertedFrame != null) 
            {
                fixed(AVFrame** convertedFramePtr = &_convertedFrame)
                    ffmpeg.av_frame_free(convertedFramePtr);
            }
                

            if (_hwDeviceContext != null) 
            {
                fixed(AVBufferRef** hwDeviceContextPtr = &_hwDeviceContext)
                    ffmpeg.av_buffer_unref(hwDeviceContextPtr);
            }
                

            GC.SuppressFinalize(this);
        }

        public unsafe AVFrame* GetLastDecodedFrame()
        {
            return _frame;
        }

    }

}
