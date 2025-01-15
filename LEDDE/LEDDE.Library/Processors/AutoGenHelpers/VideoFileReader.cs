using FFmpeg.AutoGen;
using LEDDE.Library.LED;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

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
        

        const int AVERROR_EAGAIN = -11;

        private AVBufferRef* _hwDeviceContext = null;

        public int FrameCount=0;

        public VideoFileReader(string videoPath, AVHWDeviceType hwDeviceType = AVHWDeviceType.AV_HWDEVICE_TYPE_DXVA2)
        {
            InitializeFormatContext(videoPath);
            InitializeVideoStream();
            InitializeCodec(hwDeviceType);
            AllocateFrameAndPacket();

            // Now call GetTotalFrames to initialize the total frame count
            FrameCount = GetTotalFrames();  // Store the total frames count for later use
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

        #region on trial

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

            if (ffmpeg.avcodec_open2(_codecContext, _codec, null) < 0)
                throw new InvalidOperationException("Failed to open codec.");
        }
        private bool EnableHardwareAcceleration(AVHWDeviceType hwDeviceType)
        {
            fixed (AVBufferRef** hwDeviceContextPtr = &_hwDeviceContext)
            {
                if (ffmpeg.av_hwdevice_ctx_create(hwDeviceContextPtr, hwDeviceType, null, null, 0) < 0)
                {
                    Logger.Log("Failed to create hardware device context.");
                    return false;
                }
            }

            _codecContext->hw_device_ctx = ffmpeg.av_buffer_ref(_hwDeviceContext);
            return true;
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

        private LEDMatrix ConvertToLEDMatrixFromHardwareFrame(AVFrame* hwFrame)
        {
            // Conversion logic for hardware-accelerated frames
            Logger.Log("Performing hardware-to-software frame conversion...");
            // Example: Map hardware frame back to software for processing if needed.
            // Implement specific mapping logic based on hardware type.
            return new LEDMatrix(hwFrame->width, hwFrame->height);
        }

        #endregion


        public unsafe LEDMatrix ConvertToLEDMatrixOptimized(AVFrame sourceFrame)
        {
            // Precompute constants
            const int offsetY = 16;
            const int offsetUV = 128;
            const int coeffY = 298;
            const int coeffR = 409;
            const int coeffG1 = 100;
            const int coeffG2 = 208;
            const int coeffB = 516;

            // Create an LEDMatrix with the same dimensions as the source frame
            var ledMatrix = new LEDMatrix(sourceFrame.width, sourceFrame.height);

            // Extract dimensions and data pointers
            int width = sourceFrame.width;
            int height = sourceFrame.height;
            int linesizeY = sourceFrame.linesize[0];
            int linesizeU = sourceFrame.linesize[1];
            int linesizeV = sourceFrame.linesize[2];
            byte* dataY = sourceFrame.data[0];
            byte* dataU = sourceFrame.data[1];
            byte* dataV = sourceFrame.data[2];

            // Parallel processing
            Parallel.For(0, height, y =>
            {
                int yHalf = y / 2;
                for (int x = 0; x < width; x++)
                {
                    int xHalf = x / 2;

                    // YUV to RGB conversion
                    int yIndex = y * linesizeY + x;
                    int uIndex = yHalf * linesizeU + xHalf;
                    int vIndex = yHalf * linesizeV + xHalf;

                    int c = dataY[yIndex] - offsetY;
                    int d = dataU[uIndex] - offsetUV;
                    int e = dataV[vIndex] - offsetUV;

                    byte r = ClampToByte((coeffY * c + coeffR * e + 128) >> 8);
                    byte g = ClampToByte((coeffY * c - coeffG1 * d - coeffG2 * e + 128) >> 8);
                    byte b = ClampToByte((coeffY * c + coeffB * d + 128) >> 8);

                    ledMatrix.SetPixelColor(x, y, new Rgba32(r, g, b));
                }
            });

            return ledMatrix;
        }

        private static byte ClampToByte(int value)
        {
            return (byte)(value < 0 ? 0 : (value > 255 ? 255 : value));
        }

        #region needs better performance
        public unsafe LEDMatrix ConvertToLEDMatrixNEW(AVFrame sourceFrame)
        {
            // Create an LEDMatrix with the same dimensions as the source frame
            var ledMatrix = new LEDMatrix(sourceFrame.width, sourceFrame.height);

            // Extract dimensions
            int width = sourceFrame.width;
            int height = sourceFrame.height;
            int linesizeY = sourceFrame.linesize[0];
            int linesizeU = sourceFrame.linesize[1];
            int linesizeV = sourceFrame.linesize[2];

            byte* dataY = sourceFrame.data[0];
            byte* dataU = sourceFrame.data[1];
            byte* dataV = sourceFrame.data[2];

            // Iterate through the frame's pixels
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Calculate Y plane index
                    int yIndex = y * linesizeY + x;
                    byte yValue = dataY[yIndex];

                    // Calculate U and V plane indices, considering chroma subsampling
                    int uIndex = (y / 2) * linesizeU + (x / 2);
                    int vIndex = (y / 2) * linesizeV + (x / 2);
                    byte uValue = dataU[uIndex];
                    byte vValue = dataV[vIndex];

                    // Convert YUV to RGB
                    var (r, g, b) = ConvertYuvToRgb(yValue, uValue, vValue);

                    // Create Rgba32 color
                    var color = new Rgba32(r, g, b);

                    // Set the pixel in the LEDMatrix
                    ledMatrix.SetPixelColor(x, y, color);
                }
            }

            return ledMatrix;
        }

        // Helper method to convert YUV to RGB
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

        #endregion

        #region doesn't work
        private LEDMatrix ConvertFrameToImage(AVFrame* frame)
        {
            if (frame == null)
            {
                throw new ArgumentNullException(nameof(frame));
            }

            int width = frame->width;
            int height = frame->height;

            // Create an LEDMatrix with the frame's dimensions
            LEDMatrix ledMatrix = new(width, height);

            Logger.Log($"Initial frame pixel format: {frame->format}");

            if (frame->format != (int)AVPixelFormat.AV_PIX_FMT_RGBA)
            {
                Logger.Log($"Frame pixel format ({frame->format}) is not RGBA. Converting...");

                // Allocate a new frame for RGBA data
                AVFrame* rgbaFrame = ffmpeg.av_frame_alloc();
                if (rgbaFrame == null)
                {
                    throw new InvalidOperationException("Failed to allocate frame for RGBA frame.");
                }

                // Allocate buffer for RGBA frame
                int bufferSize = ffmpeg.av_image_get_buffer_size(AVPixelFormat.AV_PIX_FMT_RGBA, width, height, 1);
                byte* buffer = (byte*)ffmpeg.av_malloc((ulong)bufferSize);
                if (buffer == null)
                {
                    ffmpeg.av_frame_free(&rgbaFrame);
                    throw new InvalidOperationException("Failed to allocate buffer for RGBA frame.");
                }

                // Create temporary variables for the first 4 pointers
                byte_ptrArray4 rgbaData = new byte_ptrArray4();
                int_array4 rgbaLineSize = new int_array4();

                // Copy relevant data from rgbaFrame->data and rgbaFrame->linesize
                for (uint i = 0; i < 4; i++)
                {
                    rgbaData[i] = rgbaFrame->data[i];
                    rgbaLineSize[i] = rgbaFrame->linesize[i];
                }

                if (ffmpeg.av_image_fill_arrays(
                    ref rgbaData,
                    ref rgbaLineSize,
                    buffer,
                    AVPixelFormat.AV_PIX_FMT_RGBA,
                    width,
                    height,
                    1) < 0)
                {
                    ffmpeg.av_free(buffer);
                    ffmpeg.av_frame_free(&rgbaFrame);
                    throw new InvalidOperationException("Failed to fill RGBA frame arrays.");
                }

                // Convert the frame to RGBA using sws_scale
                SwsContext* swsCtx = ffmpeg.sws_getContext(
                    width, height, (AVPixelFormat)frame->format,
                    width, height, AVPixelFormat.AV_PIX_FMT_RGBA,
                    ffmpeg.SWS_BILINEAR, null, null, null);

                if (swsCtx == null)
                {
                    ffmpeg.av_free(buffer);
                    ffmpeg.av_frame_free(&rgbaFrame);
                    throw new InvalidOperationException("Failed to create SwsContext for conversion.");
                }

                ffmpeg.sws_scale(
                    swsCtx,
                    frame->data,
                    frame->linesize,
                    0,
                    height,
                    rgbaFrame->data,
                    rgbaFrame->linesize);

                // Release the SwsContext
                ffmpeg.sws_freeContext(swsCtx);

                // Use the converted RGBA frame data
                byte* dataPtr = rgbaFrame->data[0];
                int lineSize = rgbaFrame->linesize[0];

                if (dataPtr == null)
                {
                    ffmpeg.av_free(buffer);
                    ffmpeg.av_frame_free(&rgbaFrame);

                    throw new InvalidOperationException("RGBA frame data pointer is null after conversion.");
                }

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int offset = y * lineSize + x * 4;

                        byte r = dataPtr[offset];
                        byte g = dataPtr[offset + 1];
                        byte b = dataPtr[offset + 2];
                        byte a = dataPtr[offset + 3];

                        Rgba32 color = new(r, g, b, a);
                        ledMatrix.SetPixelColor(x, y, color);
                    }
                }

                // Free the allocated buffer and frame
                ffmpeg.av_free(buffer);
                ffmpeg.av_frame_free(&rgbaFrame);
            }
            else
            {
                // If the frame is already in RGBA, process it directly
                byte* dataPtr = frame->data[0];
                int lineSize = frame->linesize[0];

                if (dataPtr == null)
                {
                    throw new InvalidOperationException("Frame data pointer is null for RGBA frame.");
                }

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int offset = y * lineSize + x * 4;

                        byte r = dataPtr[offset];
                        byte g = dataPtr[offset + 1];
                        byte b = dataPtr[offset + 2];
                        byte a = dataPtr[offset + 3];

                        Rgba32 color = new(r, g, b, a);
                        ledMatrix.SetPixelColor(x, y, color);
                    }
                }
            }

            return ledMatrix;
        }
        #endregion
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
            /*ffmpeg.av_packet_free(&_packet);
            ffmpeg.av_frame_free(&_frame);
            ffmpeg.avcodec_free_context(&_codecContext);
            ffmpeg.avformat_close_input(&_formatContext);
            */

            GC.SuppressFinalize(this);
        }
    }
}
