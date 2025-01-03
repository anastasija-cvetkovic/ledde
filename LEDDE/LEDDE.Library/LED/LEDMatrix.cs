using SixLabors.ImageSharp.PixelFormats;
using System.Text;

namespace LEDDE.Library.LED
{
    public class LEDMatrix
    {
        //an array instead of a matrix for faster access
        private readonly LEDPixel[] _matrix;
        public int Width { get; set; }
        public int Height { get; set; }
        public LEDMatrix(int width, int height)
        {
            Width = width;
            Height = height;
            _matrix = new LEDPixel[Width * Height];

            InitializePixels();
        }

        private void InitializePixels()
        {
            for (int i = 0; i < _matrix.Length; i++)
            {
                _matrix[i] = new LEDPixel();
            }
        }
        public int GetIndex(int x, int y)
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
        public Rgba32 GetPixelColor(int x, int y)
        {
            if (x < 0 || x >= Width) 
            {
                throw new ArgumentOutOfRangeException(nameof(x), "The x coordinate is out of bounds.");
            }
            if (y < 0 || y >= Height) {

                throw new ArgumentOutOfRangeException(nameof(y), "The y coordinate is out of bounds.");
            }   
            int index = GetIndex(x, y);
            return _matrix[index].Color;
        }
        public string SaveAsASCII()
        {
            var sb = new StringBuilder();

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    Rgba32 color = GetPixelColor(x, y);
                    sb.Append($"({color.R},{color.G},{color.B}) ");
                }
                sb.AppendLine(); // New line for each row
            }
            return sb.ToString();
        }
        public void SaveAsASCII(string folderPath, string baseFileName)
        {
            //Ensure the directory exists
            Directory.CreateDirectory(folderPath);

            string filePath = Path.Combine(folderPath, $"{baseFileName}");

            string asciiArt = SaveAsASCII();

            File.WriteAllText(filePath, asciiArt);
        }
    }
}
