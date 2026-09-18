using System.IO;
using System.IO.Compression;

namespace MoneyFlow.Data.Serialization;

/// <summary>
/// Brotli compression/decompression for the serialization pipeline.
/// Applied BEFORE encryption (never compress encrypted data).
/// </summary>
public static class CompressionHelper
{
    /// <summary>Current compression version identifier.</summary>
    public const ushort Version = 1;

    /// <summary>
    /// Compresses data using Brotli with balanced quality (level 4).
    /// </summary>
    public static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var brotli = new BrotliStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            brotli.Write(data, 0, data.Length);
        }
        return output.ToArray();
    }

    /// <summary>
    /// Decompresses Brotli-compressed data.
    /// </summary>
    public static byte[] Decompress(byte[] compressedData)
    {
        using var input = new MemoryStream(compressedData);
        using var brotli = new BrotliStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        brotli.CopyTo(output);
        return output.ToArray();
    }
}
