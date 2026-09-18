using MessagePack;
using MessagePack.Resolvers;

namespace MoneyFlow.Data.Serialization;

/// <summary>
/// MessagePack-based binary serializer for CompanyDataStore.
/// Uses ContractlessStandardResolver so entity classes don't need MessagePack attributes.
/// </summary>
public static class CompanySerializer
{
    /// <summary>Current serializer version identifier.</summary>
    public const ushort Version = 1;

    private static readonly MessagePackSerializerOptions Options =
        MessagePackSerializerOptions.Standard
            .WithResolver(ContractlessStandardResolverAllowPrivate.Instance)
            .WithSecurity(MessagePackSecurity.UntrustedData);

    /// <summary>
    /// Serializes a CompanyDataStore to MessagePack binary format.
    /// </summary>
    public static byte[] Serialize(Storage.CompanyDataStore store)
    {
        return MessagePackSerializer.Serialize(store, Options);
    }

    /// <summary>
    /// Deserializes MessagePack binary data into a CompanyDataStore.
    /// </summary>
    public static Storage.CompanyDataStore Deserialize(byte[] data)
    {
        return MessagePackSerializer.Deserialize<Storage.CompanyDataStore>(data, Options)
            ?? throw new CompanyFileCorruptedException("Failed to deserialize company data: null result.");
    }
}
