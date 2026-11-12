using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Exceptions;

namespace MoneyFlow.Data.Storage;

public static class CompanyDataSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    /// <summary>
    /// Serializes company domain data to deterministic bytes and compresses with GZip.
    /// Order: Domain Data -> Binary Serialization -> Compression.
    /// </summary>
    public static byte[] SerializeAndCompress(CompanyData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));

        try
        {
            using var uncompressedStream = new MemoryStream();
            JsonSerializer.Serialize(uncompressedStream, data, JsonOptions);
            uncompressedStream.Position = 0;

            using var compressedStream = new MemoryStream();
            using (var gzip = new GZipStream(compressedStream, CompressionLevel.Optimal, leaveOpen: true))
            {
                uncompressedStream.CopyTo(gzip);
            }

            return compressedStream.ToArray();
        }
        catch (Exception ex)
        {
            throw new CompanyDataCorruptedException($"Failed to serialize company data: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Decompresses GZip payload and deserializes to CompanyData.
    /// Order: Decompression -> Deserialization.
    /// </summary>
    public static CompanyData DecompressAndDeserialize(byte[] compressedBytes)
    {
        if (compressedBytes == null || compressedBytes.Length == 0)
        {
            throw new ArgumentException("Payload bytes cannot be null or empty.", nameof(compressedBytes));
        }

        try
        {
            using var compressedStream = new MemoryStream(compressedBytes);
            using var gzip = new GZipStream(compressedStream, CompressionMode.Decompress);
            using var decompressedStream = new MemoryStream();
            gzip.CopyTo(decompressedStream);
            decompressedStream.Position = 0;

            var data = JsonSerializer.Deserialize<CompanyData>(decompressedStream, JsonOptions);
            return data ?? throw new CompanyDataCorruptedException("Deserialized company data resulted in null object.");
        }
        catch (Exception ex) when (ex is not CompanyDataCorruptedException)
        {
            throw new CompanyDataCorruptedException($"Failed to decompress and deserialize company data: {ex.Message}", ex);
        }
    }
}
