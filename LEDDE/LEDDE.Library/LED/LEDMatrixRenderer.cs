using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using LEDDE.Library.Processors;
using System.Drawing;

namespace LEDDE.Library.LED
{
    public class LEDMatrixRenderer
    {
        public static LEDMatrix ToLEDMatrix(Image<Rgba32> image)
        {
            int matrixWidth = image.Width;
            int matrixHeight = image.Height;

            LEDMatrix ledMatrix = new(matrixWidth, matrixHeight);

            if (ProcessorUtils.ShouldParallelize(matrixWidth, matrixHeight))
            {
                Parallel.For(0, matrixHeight,
                    row => ProcessorUtils.ProcessRow(image, ledMatrix, row, matrixWidth));
            }
            else
            {
                for (int row = 0; row < matrixHeight; row++)
                {
                    ProcessorUtils.ProcessRow(image, ledMatrix, row, matrixWidth);
                }
            }
            return ledMatrix;
        }
    }
}
