using System;

namespace KerbalMemorial
{
    // Image-only guard against blank frames and high-frequency television noise.
    // Kept independent of Unity so the regression runner can exercise it.
    internal static class PortraitQuality
    {
        internal static bool IsUsable(int[] light, int width, int height)
        {
            if (light == null || width < 2 || height < 2 || light.Length != width * height) return false;
            int min = 765, max = 0;
            double sum = 0, squares = 0, horizontal = 0, vertical = 0;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x, v = light[i];
                    min = Math.Min(min, v); max = Math.Max(max, v);
                    sum += v; squares += (double)v * v;
                    if (x > 0) { double d = v - light[i - 1]; horizontal += d * d; }
                    if (y > 0) { double d = v - light[i - width]; vertical += d * d; }
                }
            double mean = sum / light.Length;
            double variance = squares / light.Length - mean * mean;
            if (max - min < 24 || variance < 16) return false;
            // Independent random noise has an adjacent squared difference of about
            // twice its variance. Real portraits have spatially coherent regions.
            double h = horizontal / ((width - 1) * height) / variance;
            double vRatio = vertical / (width * (height - 1)) / variance;
            if (h > 1.45 && vRatio > 1.45) return false;
            // Also reject strongly alternating scanlines/checkerboard patterns.
            if (Math.Max(h, vRatio) > 2.7) return false;
            return true;
        }
    }
}
