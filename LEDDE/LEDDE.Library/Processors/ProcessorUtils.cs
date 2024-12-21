using LEDDE.Library.LED;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;

namespace LEDDE.Library.Processors
{
    public class ProcessorUtils
    {
        public static bool ShouldParallelize(int matrixWidth,int matrixHeight)
        {
            return (matrixWidth * matrixHeight > 1000) && (Environment.ProcessorCount > 1);
        }
        public static void ProcessRow(Image<Rgba32> image, LEDMatrix ledMatrix, int row, int matrixWidth)
        {
            image.ProcessPixelRows(pixelAccessor =>
            {
                Span<Rgba32> rowPixels = pixelAccessor.GetRowSpan(row);
                for (int col = 0; col < matrixWidth; col++)
                {
                    Rgba32 pixelColor = rowPixels[col];
                    ledMatrix.SetPixelColor(col, row, pixelColor);
                }
            });
        }
    }
}
