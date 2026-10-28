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

        return string.Join('|',
            header.PrevHash,
            header.Timestamp.ToString(CultureInfo.InvariantCulture),
            header.Version.ToString(CultureInfo.InvariantCulture),
            header.TxRoot,
            header.Nonce.ToString(CultureInfo.InvariantCulture),
            header.Difficulty.ToString(CultureInfo.InvariantCulture));
    }

    public static string ComputeHash(BlockHeader header)
        => HashFunc.ComputeHash(Encoding.UTF8.GetBytes(SerializeHeader(header)));
}