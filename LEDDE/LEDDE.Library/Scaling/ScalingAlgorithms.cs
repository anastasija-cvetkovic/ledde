using SixLabors.ImageSharp.PixelFormats;
using System.Numerics;
using System;

namespace LEDDE.Library.Scaling
{
    public class ScalingAlgorithms
    {
        public delegate Rgba32 InterpolationAlgorithm(
        Rgba32 c00, Rgba32 c10, Rgba32 c01, Rgba32 c11, float dx, float dy);

        public static Rgba32 BilinearInterpolate(Rgba32 c00, Rgba32 c10, Rgba32 c01, Rgba32 c11, float dx, float dy)
        {
            float w00 = (1 - dx) * (1 - dy);
            float w10 = dx * (1 - dy);
            float w01 = (1 - dx) * dy;
            float w11 = dx * dy;

            Vector4 c00Vec = ScalingHelper.ToVector4(c00);
            Vector4 c10Vec = ScalingHelper.ToVector4(c10);
            Vector4 c01Vec = ScalingHelper.ToVector4(c01);
            Vector4 c11Vec = ScalingHelper.ToVector4(c11);

            Vector4 resultVec = w00 * c00Vec + w10 * c10Vec + w01 * c01Vec + w11 * c11Vec;

            return ScalingHelper.ToRgba32(resultVec);
        }
        public static Rgba32 NearestNeighborInterpolate(Rgba32 c00, Rgba32 c10, Rgba32 c01, Rgba32 c11, float dx, float dy)
        {
            return (dx + dy < 1) ? c00 : c11;
        }
    }
}
