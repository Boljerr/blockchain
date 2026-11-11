using System.Numerics;

namespace BlockChain.Hash;

public class HashFunc
{
    private const ulong MultiplierA = 14844827832870488419UL;
    private const ulong MultiplierB = 4702101796626366811UL;
    private const ulong MultiplierC = 12760894674763063281UL;
    private const ulong MultiplierD = 3200907817819897205UL;
    private const ulong MultiplierIndex = 11132683126343784489UL;

    internal static readonly HashState InitialState = new(
        15036463542186079559UL,
        12483322617323990657UL,
        2658435062210208447UL,
        1587461109643488129UL);

    public string ComputeHash(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        return Format(Finish(Absorb(InitialState, data, 0)));
    }

    internal static HashState Absorb(HashState state, ReadOnlySpan<byte> data, int startIndex)
    {
        ulong a = state.A;
        ulong b = state.B;
        ulong c = state.C;
        ulong d = state.D;

        for (int i = 0; i < data.Length; i++)
        {
            byte value = data[i];
            ulong input = unchecked((value + (ulong)(startIndex + i)) * MultiplierIndex);

            a = unchecked(a ^ (input + b) * MultiplierA) ^ d;
            b = BitOperations.RotateLeft(b, 21);

            b = unchecked(((b + c) ^ a) * MultiplierB) ^ d;
            d = BitOperations.RotateLeft(d, 27);

            c = unchecked((c ^ b) * MultiplierC + d) ^ a;
            c = BitOperations.RotateLeft(c, 61);

            d = unchecked(((d ^ c) + a) * MultiplierD) ^ b;
            a = BitOperations.RotateLeft(a, 73);
        }

        return new HashState(a, b, c, d);
    }

    internal static HashState Finish(HashState state)
    {
        ulong a = state.A;
        ulong b = state.B;
        ulong c = state.C;
        ulong d = state.D;

        a = unchecked((a ^ (b * MultiplierB)) * MultiplierA) ^ c;
        d = unchecked(((b + c) ^ a) * MultiplierC) ^ c;
        c = unchecked((c ^ d) * MultiplierD) ^ a;
        b = unchecked(((d + b) ^ a) * MultiplierA) ^ c;
        d = BitOperations.RotateLeft(d, 67);

        return new HashState(a, b, c, d);
    }

    internal static string Format(HashState digest)
        => $"{digest.A:x16}{digest.B:x16}{digest.C:x16}{digest.D:x16}";
}

internal readonly record struct HashState(ulong A, ulong B, ulong C, ulong D)
{
    public int LeadingZeroBits
        => A != 0 ? BitOperations.LeadingZeroCount(A)
            : B != 0 ? 64 + BitOperations.LeadingZeroCount(B)
            : C != 0 ? 128 + BitOperations.LeadingZeroCount(C)
            : 192 + BitOperations.LeadingZeroCount(D);
}
