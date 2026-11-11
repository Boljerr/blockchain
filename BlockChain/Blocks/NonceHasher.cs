using System.Globalization;
using System.Text;
using BlockChain.Hash;
using BlockChain.Models;

namespace BlockChain.Blocks;

internal sealed class NonceHasher
{
    private const int MaxNonceDigits = 20;

    private readonly HashState _prefixState;
    private readonly int _prefixLength;
    private readonly byte[] _suffix;
    private readonly byte[] _tail;

    public NonceHasher(BlockHeader template)
    {
        ArgumentNullException.ThrowIfNull(template);

        byte[] prefix = Encoding.UTF8.GetBytes(BlockHasher.SerializeBeforeNonce(template));

        _prefixState = HashFunc.Absorb(HashFunc.InitialState, prefix, 0);
        _prefixLength = prefix.Length;
        _suffix = Encoding.UTF8.GetBytes(BlockHasher.SerializeAfterNonce(template));
        _tail = new byte[MaxNonceDigits + _suffix.Length];
    }

    public HashState Digest(ulong nonce)
    {
        nonce.TryFormat(_tail, out int digits, default, CultureInfo.InvariantCulture);
        _suffix.CopyTo(_tail, digits);

        return HashFunc.Finish(
            HashFunc.Absorb(_prefixState, _tail.AsSpan(0, digits + _suffix.Length), _prefixLength));
    }
}
