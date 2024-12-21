using SixLabors.ImageSharp.PixelFormats;

namespace LEDDE.Library.LED
{
    internal class LEDPixel
    {
        public Rgba32 Color { get; set; }
        public LEDPixel() : this(new Rgba32(0, 0, 0, 0)) { }
        public LEDPixel(Rgba32 color)
        {
            this.Color = color;
        }
        public void SetColor(Rgba32 color) 
        {
            Color = color;
        }

    }
}
