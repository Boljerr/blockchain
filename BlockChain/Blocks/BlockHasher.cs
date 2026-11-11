using System.Globalization;
using System.Text;
using BlockChain.Hash;
using BlockChain.Models;

namespace BlockChain.Blocks;

public static class BlockHasher
{
    private static readonly HashFunc HashFunc = new();

    public static string SerializeHeader(BlockHeader header)
    {
        ArgumentNullException.ThrowIfNull(header);

        return SerializeBeforeNonce(header)
            + header.Nonce.ToString(CultureInfo.InvariantCulture)
            + SerializeAfterNonce(header);
    }

    public static string ComputeHash(BlockHeader header)
        => HashFunc.ComputeHash(Encoding.UTF8.GetBytes(SerializeHeader(header)));

    internal static string SerializeBeforeNonce(BlockHeader header)
        => string.Join('|',
            header.PrevHash,
            header.Timestamp.ToString(CultureInfo.InvariantCulture),
            header.Version.ToString(CultureInfo.InvariantCulture),
            header.TxRoot,
            "");

    internal static string SerializeAfterNonce(BlockHeader header)
        => "|" + header.Difficulty.ToString(CultureInfo.InvariantCulture);
}
