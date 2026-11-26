using System;
using System.IO;
using UnityEngine;

namespace KerbalMemorial
{
    // High-resolution, anti-aliased font artwork is embedded in the DLL.
    // No KSP texture importer, runtime font lookup, or extra installation files.
    internal static class SafetySignArtwork
    {
        internal const int Width = 2048, Height = 2048;
        internal const float WorldAspect = 2f / 3f;
        private const int TileWidth = 192;
        private const int TileHeight = 440;
        private const int DigitCount = 12;
        private const int CardCount = 4;
        private const int TileY = Height - 1150 - TileHeight;
        private static readonly int[] TileX = { 883, 1149, 1416, 1683 };
        private static Color32[][] digitTiles;
        private const string ResourcePrefix = "KerbalMemorial.Artwork.";

        private static byte[] Resource(string name)
        {
            using (var stream = typeof(SafetySignArtwork).Assembly.GetManifestResourceStream(ResourcePrefix + name))
            {
                if (stream == null)
                    throw new InvalidOperationException("Missing sign artwork: " + name);
                using (var data = new MemoryStream())
                {
                    stream.CopyTo(data);
                    return data.ToArray();
                }
            }
        }

        internal static void Initialize(Texture2D texture)
        {
            // Keep the destination RGBA32 and its full mip chain even if
            // LoadImage would change the PNG decoder's texture format.
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!source.LoadImage(Resource("SignBoard.png"), false) || source.width != Width || source.height != Height)
                    throw new InvalidOperationException("Invalid safety sign board texture.");
                texture.SetPixels32(source.GetPixels32());
            }
            finally
            {
                UnityEngine.Object.Destroy(source);
            }
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Trilinear;
            texture.anisoLevel = 16;
            texture.mipMapBias = -0.5f;
            if (digitTiles != null)
                return;
            var atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!atlas.LoadImage(Resource("SignDigits.png"), false) || atlas.width != TileWidth * DigitCount || atlas.height != TileHeight * CardCount)
                    throw new InvalidOperationException("Invalid safety sign digit atlas.");
                var pixels = atlas.GetPixels32();
                var tiles = new Color32[CardCount * DigitCount][];
                for (int i = 0; i < tiles.Length; ++i)
                {
                    tiles[i] = new Color32[TileWidth * TileHeight];
                    int x = (i % DigitCount) * TileWidth;
                    int y = (CardCount - 1 - i / DigitCount) * TileHeight;
                    for (int row = 0; row < TileHeight; ++row)
                        Array.Copy(pixels, (y + row) * atlas.width + x, tiles[i], row * TileWidth, TileWidth);
                }
                digitTiles = tiles;
            }
            finally
            {
                UnityEngine.Object.Destroy(atlas);
            }
        }

        internal static void Paint(Texture2D texture, string firstLine)
        {
            if (digitTiles == null)
                throw new InvalidOperationException("Safety sign artwork not initialized.");
            string digits = Digits(firstLine);
            for (int i = 0; i < CardCount; ++i)
            {
                char c = digits[i];
                int index = c >= '0' && c <= '9' ? c - '0' : c == '-' ? 10 : 11;
                texture.SetPixels32(TileX[i], TileY, TileWidth, TileHeight, digitTiles[i * DigitCount + index], 0);
            }
            texture.Apply(true, false);
        }

        private static string Digits(string firstLine)
        {
            if (string.IsNullOrEmpty(firstLine))
                return "    ";
            firstLine = firstLine.Trim();
            if (firstLine.StartsWith("---", StringComparison.Ordinal))
                return "----";
            int end = 0;
            while (end < firstLine.Length && firstLine[end] >= '0' && firstLine[end] <= '9')
                ++end;
            if (end == 0)
                return "    ";
            // Remove leading zeros before checking length; avoid integer overflow.
            string raw = firstLine.Substring(0, end).TrimStart('0');
            if (raw.Length == 0)
                raw = "0";
            if (raw.Length > 4)
                raw = "9999";
            return raw.PadLeft(4, ' ');
        }
    }
}
