using SixLabors.ImageSharp.PixelFormats;

namespace LEDDE.Library.LED
{
    public class LEDMatrix
    {
        private readonly LEDPixel[] _matrix;
        public int Width { get; set; }
        public int Height { get; set; }
        public LEDMatrix(int width, int height)
        {
            Width = width;
            Height = height;
            _matrix = new LEDPixel[Width * Height];

            //InitializePixels();
        }

        private void InitializePixels()
        {
            for (int i = 0; i < _matrix.Length; i++)
            {
                _matrix[i] = new LEDPixel();
            }
        }
        private int GetIndex(int x, int y)
        {
            return y * Width + x;
        }
        public void SetPixelColor(int x, int y, Rgba32 color)
        {
            //x - horizontal, y - vertical
            if (x < 0 || x >= Width)
            {
                throw new ArgumentOutOfRangeException(nameof(x), "The x coordinate is out of bounds.");
            }
            if (y < 0 || y >= Height)
            {
                throw new ArgumentOutOfRangeException(nameof(y), "The y coordinate is out of bounds.");
            }
            int index = GetIndex(x, y);
            _matrix[index].SetColor(color);
        }

    }
}
