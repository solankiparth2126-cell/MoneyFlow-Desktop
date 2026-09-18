using System;
using System.IO;
using System.Text;
using MoneyFlow.Data.Encryption;

namespace MoneyFlow.Data.Storage;

/// <summary>
/// Defines the binary structure of company.data files.
/// Handles reading/writing file headers, security metadata, and payload framing.
/// </summary>
public static class CompanyFileFormat
{
    // Magic bytes identifying a valid MyERP company file
    public static readonly byte[] FileMagic = Encoding.ASCII.GetBytes("MYERPDAT");
    public static readonly byte[] FooterMagic = Encoding.ASCII.GetBytes("MYERPEND");

    // Current format versions
    public const ushort CurrentFileFormatVersion = 1;
    public const ushort CurrentHeaderVersion = 1;
    public const ushort CurrentEncryptionVersion = 1;
    public const ushort CurrentDataVersion = 1;
    public const ushort CurrentSchemaVersion = 1;

    // Safe limits for length validation
    public const int MaxCompanyIdLength = 256;
    public const long MaxPayloadLength = 2L * 1024 * 1024 * 1024; // 2 GB
}

/// <summary>
/// File header — plaintext metadata at the start of company.data.
/// Only non-sensitive metadata; no business data.
/// </summary>
public sealed class FileHeader
{
    public byte[] Magic { get; set; } = CompanyFileFormat.FileMagic;
    public ushort FileFormatVersion { get; set; } = CompanyFileFormat.CurrentFileFormatVersion;
    public ushort HeaderVersion { get; set; } = CompanyFileFormat.CurrentHeaderVersion;
    public ushort EncryptionVersion { get; set; } = CompanyFileFormat.CurrentEncryptionVersion;
    public ushort DataVersion { get; set; } = CompanyFileFormat.CurrentDataVersion;
    public ushort SchemaVersion { get; set; } = CompanyFileFormat.CurrentSchemaVersion;
    public ushort SerializerVersion { get; set; } = Serialization.CompanySerializer.Version;
    public ushort CompressionVersion { get; set; } = Serialization.CompressionHelper.Version;
    public string CompanyId { get; set; } = string.Empty;
    public ulong RevisionNumber { get; set; }

    /// <summary>Writes this header to a BinaryWriter.</summary>
    public void WriteTo(BinaryWriter writer)
    {
        writer.Write(Magic);
        writer.Write(FileFormatVersion);
        writer.Write(HeaderVersion);
        writer.Write(EncryptionVersion);
        writer.Write(DataVersion);
        writer.Write(SchemaVersion);
        writer.Write(SerializerVersion);
        writer.Write(CompressionVersion);

        var companyIdBytes = Encoding.UTF8.GetBytes(CompanyId);
        writer.Write((ushort)companyIdBytes.Length);
        writer.Write(companyIdBytes);

        writer.Write(RevisionNumber);
    }

    /// <summary>Reads a file header from a BinaryReader.</summary>
    public static FileHeader ReadFrom(BinaryReader reader)
    {
        var header = new FileHeader();

        header.Magic = reader.ReadBytes(8);
        if (!header.Magic.AsSpan().SequenceEqual(CompanyFileFormat.FileMagic))
            throw new CompanyFileCorruptedException("Invalid file: not a MyERP company data file.");

        header.FileFormatVersion = reader.ReadUInt16();
        header.HeaderVersion = reader.ReadUInt16();
        header.EncryptionVersion = reader.ReadUInt16();
        header.DataVersion = reader.ReadUInt16();
        header.SchemaVersion = reader.ReadUInt16();
        header.SerializerVersion = reader.ReadUInt16();
        header.CompressionVersion = reader.ReadUInt16();

        var companyIdLen = reader.ReadUInt16();
        if (companyIdLen > CompanyFileFormat.MaxCompanyIdLength)
            throw new CompanyFileCorruptedException($"Invalid company ID length: {companyIdLen}.");
        header.CompanyId = Encoding.UTF8.GetString(reader.ReadBytes(companyIdLen));

        header.RevisionNumber = reader.ReadUInt64();

        return header;
    }

    /// <summary>Serializes header fields used as AAD for AES-GCM authentication.</summary>
    public byte[] ToAadBytes()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(Magic);
        writer.Write(FileFormatVersion);
        writer.Write(HeaderVersion);
        writer.Write(EncryptionVersion);
        writer.Write(DataVersion);
        writer.Write(SchemaVersion);
        var companyIdBytes = Encoding.UTF8.GetBytes(CompanyId);
        writer.Write((ushort)companyIdBytes.Length);
        writer.Write(companyIdBytes);
        writer.Write(RevisionNumber);
        return ms.ToArray();
    }
}

/// <summary>
/// Security header — key envelope data stored in company.data.
/// </summary>
public sealed class SecurityHeader
{
    public SecurityMode SecurityMode { get; set; }
    public byte KdfAlgorithm { get; set; } = 1;
    public int KdfIterations { get; set; } = 600_000;
    public byte[] Salt { get; set; } = new byte[32];
    public ushort KeyProtectionVersion { get; set; } = 1;
    public byte[] DekWrapNonce { get; set; } = new byte[AesGcmEncryptor.NonceSizeBytes];
    public byte[] WrappedDek { get; set; } = new byte[AesGcmEncryptor.KeySizeBytes];
    public byte[] DekWrapTag { get; set; } = new byte[AesGcmEncryptor.TagSizeBytes];

    public void WriteTo(BinaryWriter writer)
    {
        writer.Write((byte)SecurityMode);
        writer.Write(KdfAlgorithm);
        writer.Write(KdfIterations);
        writer.Write(Salt);
        writer.Write(KeyProtectionVersion);
        writer.Write(DekWrapNonce);
        writer.Write(WrappedDek);
        writer.Write(DekWrapTag);
    }

    public static SecurityHeader ReadFrom(BinaryReader reader)
    {
        var header = new SecurityHeader();
        header.SecurityMode = (SecurityMode)reader.ReadByte();
        header.KdfAlgorithm = reader.ReadByte();
        header.KdfIterations = reader.ReadInt32();
        header.Salt = reader.ReadBytes(32);
        header.KeyProtectionVersion = reader.ReadUInt16();
        header.DekWrapNonce = reader.ReadBytes(AesGcmEncryptor.NonceSizeBytes);
        header.WrappedDek = reader.ReadBytes(AesGcmEncryptor.KeySizeBytes);
        header.DekWrapTag = reader.ReadBytes(AesGcmEncryptor.TagSizeBytes);
        return header;
    }

    /// <summary>Converts to a KeyEnvelope for the KeyManager.</summary>
    public KeyEnvelope ToKeyEnvelope() => new()
    {
        Mode = SecurityMode,
        KdfAlgorithm = KdfAlgorithm,
        KdfIterations = KdfIterations,
        Salt = Salt,
        KeyProtectionVersion = KeyProtectionVersion,
        DekWrapNonce = DekWrapNonce,
        WrappedDek = WrappedDek,
        DekWrapTag = DekWrapTag
    };

    /// <summary>Creates from a KeyEnvelope.</summary>
    public static SecurityHeader FromKeyEnvelope(KeyEnvelope envelope) => new()
    {
        SecurityMode = envelope.Mode,
        KdfAlgorithm = envelope.KdfAlgorithm,
        KdfIterations = envelope.KdfIterations,
        Salt = envelope.Salt,
        KeyProtectionVersion = envelope.KeyProtectionVersion,
        DekWrapNonce = envelope.DekWrapNonce,
        WrappedDek = envelope.WrappedDek,
        DekWrapTag = envelope.DekWrapTag
    };
}

/// <summary>
/// Payload header — metadata about the encrypted payload.
/// </summary>
public sealed class PayloadHeader
{
    public byte[] PayloadNonce { get; set; } = new byte[AesGcmEncryptor.NonceSizeBytes];
    public long PayloadLength { get; set; }
    public byte[] PayloadTag { get; set; } = new byte[AesGcmEncryptor.TagSizeBytes];

    public void WriteTo(BinaryWriter writer)
    {
        writer.Write(PayloadNonce);
        writer.Write(PayloadLength);
        writer.Write(PayloadTag);
    }

    public static PayloadHeader ReadFrom(BinaryReader reader)
    {
        var header = new PayloadHeader();
        header.PayloadNonce = reader.ReadBytes(AesGcmEncryptor.NonceSizeBytes);
        header.PayloadLength = reader.ReadInt64();

        if (header.PayloadLength < 0 || header.PayloadLength > CompanyFileFormat.MaxPayloadLength)
            throw new CompanyFileCorruptedException($"Invalid payload length: {header.PayloadLength}.");

        header.PayloadTag = reader.ReadBytes(AesGcmEncryptor.TagSizeBytes);
        return header;
    }
}

/// <summary>
/// Footer — integrity check at the end of company.data.
/// </summary>
public sealed class FileFooter
{
    public byte[] Magic { get; set; } = CompanyFileFormat.FooterMagic;
    public ulong Revision { get; set; }
    public long TotalFileLength { get; set; }

    public void WriteTo(BinaryWriter writer)
    {
        writer.Write(Magic);
        writer.Write(Revision);
        writer.Write(TotalFileLength);
    }

    public static FileFooter ReadFrom(BinaryReader reader)
    {
        var footer = new FileFooter();
        footer.Magic = reader.ReadBytes(8);
        if (!footer.Magic.AsSpan().SequenceEqual(CompanyFileFormat.FooterMagic))
            throw new CompanyFileCorruptedException("Invalid file footer: company data may be truncated or corrupted.");
        footer.Revision = reader.ReadUInt64();
        footer.TotalFileLength = reader.ReadInt64();
        return footer;
    }
}
