using System.Security.Cryptography;
using System.Text;

namespace Anthology.Kernel;

public static class StreamId
{
    private static readonly Guid Namespace = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");

    public static Guid For(Guid userId, Guid titleId) =>
        CreateVersion5(Namespace, $"{userId}:{titleId}");

    private static Guid CreateVersion5(Guid ns, string name)
    {
        var namespaceBytes = ns.ToByteArray();
        SwapGuidBytes(namespaceBytes);

        var nameBytes = Encoding.UTF8.GetBytes(name);
        var input = new byte[namespaceBytes.Length + nameBytes.Length];
        namespaceBytes.CopyTo(input, 0);
        nameBytes.CopyTo(input, namespaceBytes.Length);

        var hash = SHA1.HashData(input);

        hash[6] = (byte)((hash[6] & 0x0F) | 0x50); // version 5
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80); // variant RFC 4122

        var result = new byte[16];
        Array.Copy(hash, result, 16);
        SwapGuidBytes(result);

        return new Guid(result);
    }

    private static void SwapGuidBytes(byte[] bytes)
    {
        (bytes[0], bytes[3]) = (bytes[3], bytes[0]);
        (bytes[1], bytes[2]) = (bytes[2], bytes[1]);
        (bytes[4], bytes[5]) = (bytes[5], bytes[4]);
        (bytes[6], bytes[7]) = (bytes[7], bytes[6]);
    }
}
