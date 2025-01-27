using FFmpeg.AutoGen;
using LEDDE.Library.LED;
using LEDDE.Library.Scaling;
using LEDDE.Library.Processors.AutoGenHelpers;

namespace LEDDE.Library.Processors
{
    public class VideoProcessor
    {
        public static void InitializeFFmpeg(bool enableNetwork = false)
        {
            if (enableNetwork)
            {
                ffmpeg.avformat_network_init();

            }
            FFmpegBinariesHelper.RegisterFFmpegBinaries();

            Logger.Log("FFmpeg binaries registered.");
        }
        
        public static List<LEDMatrix> LoadVideo(string videoPath, Action<int> progressCallback)
        {
            InitializeFFmpeg();

            var videoMatrix = new List<LEDMatrix>();
            int totalFrames = 0;
            int processedFrames = 0;


            using (var videoFile = new VideoFileReader(videoPath))
            {
                Logger.Log($"Video file opened - {videoPath}.");
                totalFrames = (int)videoFile.FrameCount;

                /* int chunksize = 10;
                 int numOfChunks = (int) Math.Ceiling((double) (totalFrames / chunksize));


                 Parallel.For(0, numOfChunks, i =>
                 {

                 });
                */

                int c = 0;
                while (videoFile.ReadNextFrame(out var frame, ref c))
                {                            
                    videoMatrix.Add(frame);
                    processedFrames++;
                    int progress = (int)((processedFrames / (float)totalFrames) * 100);
                    progressCallback?.Invoke(progress);
                }
            }
            progressCallback?.Invoke(100);

            return videoMatrix;
        }

        public static List<LEDMatrix> ScaleVideo(List<LEDMatrix> matrix, int newWidth, int newHeight, ScalingAlgorithms.InterpolationAlgorithm scalingAlgorithm, Action<int> progressCallback)
        {
            InitializeFFmpeg();

            int progress=0;

            Parallel.ForEach(matrix, (frame, state, index) =>
            {
                LEDMatrix scaledFrame = ImageProcessor.ScaleLEDMatrix(frame, newWidth, newHeight, scalingAlgorithm);

                matrix[(int)index] = scaledFrame;

                progress = (int)((index + 1) / (float)matrix.Count * 100);

                progressCallback?.Invoke(progress);

            });

            return matrix;
        }

        #region maybe
        /*Instead of decoding all frames upfront, 
         * decode and process them lazily during simulation. 
         * This reduces memory usage and scales better for long videos*/
        public static IEnumerable<LEDMatrix> StreamVideoFrames(string videoPath)
        {
            InitializeFFmpeg();
            int c = 0;

            using (var videoFile = new VideoFileReader(videoPath))
            {
                while (videoFile.ReadNextFrame(out var frame, ref c))
                {
                    yield return frame;
                }
            }
        }
        public static List<LEDMatrix> LoadAndScaleVideo(string videoPath, int width, int height)
        {
            var scaledFrames = new List<LEDMatrix>();
            foreach (var frame in StreamVideoFrames(videoPath))
            {
                scaledFrames.Add(ImageProcessor.ScaleLEDMatrix(frame, width, height, ScalingAlgorithms.NearestNeighborInterpolate));
            }
            return scaledFrames;
        }
        #endregion

    }
    
}



