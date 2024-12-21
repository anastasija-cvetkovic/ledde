using SixLabors.ImageSharp.PixelFormats;
using System.Numerics;

namespace LEDDE.Library.Scaling
{
    public class ScalingHelper
    {
        // Helper method to convert Rgba32 to Vector4
        public static Vector4 ToVector4(Rgba32 color)
        {
            return new Vector4(color.R, color.G, color.B, color.A);
        }

        // Helper method to convert Vector4 back to Rgba32
        public static Rgba32 ToRgba32(Vector4 vector)
        {
            // Ensure values are within the 0-255 range
            return new Rgba32(
                (byte)Math.Clamp(vector.X, 0, 255),
                (byte)Math.Clamp(vector.Y, 0, 255),
                (byte)Math.Clamp(vector.Z, 0, 255),
                (byte)Math.Clamp(vector.W, 0, 255)
            );
        }
    }
}
