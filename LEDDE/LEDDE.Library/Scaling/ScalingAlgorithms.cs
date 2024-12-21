using SixLabors.ImageSharp.PixelFormats;
using System.Numerics;

namespace LEDDE.Library.Scaling
{
    public class ScalingAlgorithms
    {
        public delegate Rgba32 InterpolationAlgorithm(
        Rgba32 c00, Rgba32 c10, Rgba32 c01, Rgba32 c11, float dx, float dy);

        public static Rgba32 BilinearInterpolate(Rgba32 c00, Rgba32 c10, Rgba32 c01, Rgba32 c11, float dx, float dy)
        {
            var w00 = (1 - dx) * (1 - dy);
            var w10 = dx * (1 - dy);
            var w01 = (1 - dx) * dy;
            var w11 = dx * dy;

            var c00Vec = new Vector4(c00.R, c00.G, c00.B, c00.A);
            var c10Vec = new Vector4(c10.R, c10.G, c10.B, c10.A);
            var c01Vec = new Vector4(c01.R, c01.G, c01.B, c01.A);
            var c11Vec = new Vector4(c11.R, c11.G, c11.B, c11.A);

            var resultVec = w00 * c00Vec + w10 * c10Vec + w01 * c01Vec + w11 * c11Vec;

            return new Rgba32((byte)resultVec.X, (byte)resultVec.Y, (byte)resultVec.Z, (byte)resultVec.W);
        }
        public static Rgba32 NearestNeighborInterpolate(Rgba32 c00, Rgba32 c10, Rgba32 c01, Rgba32 c11, float dx, float dy)
        {
            return (dx + dy < 1) ? c00 : c11;
        }
    }
}
