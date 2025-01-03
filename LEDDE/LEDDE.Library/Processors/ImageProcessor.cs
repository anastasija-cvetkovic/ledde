using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using LEDDE.Library.LED;
using LEDDE.Library.Scaling;
using Renderer = LEDDE.Library.LED.LEDMatrixRenderer;
using Validator = LEDDE.Library.Validators.ResourceValidator;

namespace LEDDE.Library.Processors
{
    public class ImageProcessor
    {
        public static LEDMatrix LoadImage(string imagePath)
        {
            Validator.ValidateResource(imagePath, "image");

            Image<Rgba32> image = Image.Load<Rgba32>(imagePath);

            return Renderer.ToLEDMatrix(image);
        }

        public static LEDMatrix ScaleLEDMatrix(LEDMatrix originalMatrix, int newWidth, int newHeight, ScalingAlgorithms.InterpolationAlgorithm interpolationAlgorithm)
        {
            int originalWidth=originalMatrix.Width;
            int originalHeight=originalMatrix.Height;

            LEDMatrix scaledMatrix = new(newWidth, newHeight);

            float xRatio =(float)(originalWidth-1)/ (newWidth - 1);
            float yRatio = (float)(originalHeight - 1) / (newHeight - 1);

            var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };

            Parallel.For(0, newHeight, options, newY =>
            {
                float gy = yRatio * newY;
                int y = (int)gy;
                float dy = gy - y;

                for (int newX = 0; newX < newWidth; newX++)
                {
                    float gx = xRatio * newX;
                    int x = (int)gx;
                    float dx = gx - x;

                    Rgba32 c00 = originalMatrix.GetPixelColor(x, y);
                    Rgba32 c10 = x + 1 < originalWidth ? originalMatrix.GetPixelColor(x + 1, y) : c00;
                    Rgba32 c01 = y + 1 < originalHeight ? originalMatrix.GetPixelColor(x, y + 1) : c00;
                    Rgba32 c11 = (x + 1 < originalWidth && y + 1 < originalHeight) ?
                          originalMatrix.GetPixelColor(x + 1, y + 1) : c00;

                    Rgba32 interpolatedColor = interpolationAlgorithm(c00, c10, c01, c11, dx, dy);
                    scaledMatrix.SetPixelColor(newX, newY, interpolatedColor);
                }
            });
            return scaledMatrix;
        }

        public static LEDMatrix ProcessImage(LEDMatrix loadedMatrix, int newWidth, int newHeight)
        {
            if (newWidth <= 0 || newHeight <= 0)
            {
                throw new ArgumentException("Matrix dimensions must be greater than zero");
            }

            LEDMatrix scaledMatrix = ScaleLEDMatrix(loadedMatrix,newWidth,newHeight,ScalingAlgorithms.NearestNeighborInterpolate);

            return scaledMatrix;
        }
    }
}
