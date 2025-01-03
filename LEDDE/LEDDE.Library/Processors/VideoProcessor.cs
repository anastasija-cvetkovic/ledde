using FFmpeg.AutoGen;
using LEDDE.Library.LED;
using LEDDE.Library.Scaling;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;

namespace LEDDE.Library.Processors
{
    internal class VideoProcessor
    {

        public static void InitializeFFmpeg()
        {
            // Ensure that FFmpeg is properly initialized
            ffmpeg.avformat_network_init();
        }
        public static void ProcessVideo(string videoPath)
        {
            // Initialize FFmpeg libraries
            InitializeFFmpeg();

            // Open video file
            using (var videoFile = new VideoFileReader(videoPath))
            {
                string videoFileName = Path.GetFileNameWithoutExtension(videoPath);
                string outputFolder = Path.Combine(Directory.GetCurrentDirectory(), videoFileName);
                Directory.CreateDirectory(outputFolder);

                int frameNumber = 0;
                while (videoFile.ReadNextFrame(out var frame))
                {
                    // Convert frame to LEDMatrix
                    LEDMatrix frameMatrix = LEDMatrixRenderer.ToLEDMatrix(frame);

                    // Scale the frame (optional: customize scaling resolution)
                    LEDMatrix scaledMatrix = ImageProcessor.ScaleLEDMatrix(frameMatrix, 32, 32, ScalingAlgorithms.NearestNeighborInterpolate);

                    // Save the scaled frame as ASCII
                    //scaledMatrix.SaveAsASCII(outputFolder, $"frame_{frameNumber}");

                    frameNumber++;
                }
            }
        }
    }
    public unsafe class VideoFileReader : IDisposable
    {
        private AVFormatContext* _formatContext = null;
        private AVCodecContext* _codecContext = null;
        private AVCodec* _codec = null;
        private AVFrame* _frame = null;
        private AVPacket* _packet = null;
        private int _videoStreamIndex = -1;

        public VideoFileReader(string videoPath)
        {
            // Pin _formatContext before passing it to unmanaged code
            fixed (AVFormatContext** formatContextPtr = &_formatContext)
            {
                if (ffmpeg.avformat_open_input(formatContextPtr, videoPath, null, null) != 0)
                {
                    throw new InvalidOperationException("Failed to open video file.");
                }

                // Other operations with ffmpeg can continue here, like avformat_find_stream_info
            }
            //if (ffmpeg.avformat_open_input(&_formatContext, videoPath, null, null) != 0)
            //  throw new InvalidOperationException("Failed to open video file.");

            if (ffmpeg.avformat_find_stream_info(_formatContext, null) < 0)
                throw new InvalidOperationException("Failed to retrieve stream info.");

            // Find the first video stream
            _videoStreamIndex = -1;
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

            _codecContext = ffmpeg.avcodec_alloc_context3(null);
            ffmpeg.avcodec_parameters_to_context(_codecContext, _formatContext->streams[_videoStreamIndex]->codecpar);
            _codec = ffmpeg.avcodec_find_decoder(_codecContext->codec_id);

            if (_codec == null || ffmpeg.avcodec_open2(_codecContext, _codec, null) < 0)
                throw new InvalidOperationException("Failed to open video codec.");

            _frame = ffmpeg.av_frame_alloc();
            _packet = ffmpeg.av_packet_alloc();
        }

        public bool ReadNextFrame(out Image<Rgba32> frameImage)
        {
            frameImage = null;

            if (ffmpeg.av_read_frame(_formatContext, _packet) < 0)
                return false;

            if (_packet->stream_index == _videoStreamIndex)
            {
                ffmpeg.avcodec_send_packet(_codecContext, _packet);
                if (ffmpeg.avcodec_receive_frame(_codecContext, _frame) == 0)
                {
                    // Convert frame to image here (you may need a library like ImageSharp for this)
                    frameImage = ConvertFrameToImage(_frame);
                    return true;
                }
            }

            return false;
        }

        private Image<Rgba32> ConvertFrameToImage(AVFrame* frame)
        {
            // You can use a conversion process here to turn the AVFrame into an Image<Rgba32>
            // For simplicity, you might use some third-party libraries or manual conversion.
            throw new NotImplementedException();
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
            /*ffmpeg.av_packet_free(&_packet);
            ffmpeg.av_frame_free(&_frame);
            ffmpeg.avcodec_free_context(&_codecContext);
            ffmpeg.avformat_close_input(&_formatContext);
            */

            GC.SuppressFinalize(this);
        }
    }
}



