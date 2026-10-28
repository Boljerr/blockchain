using System.Globalization;
using System.Text;
using BlockChain.Hash;

namespace BlockChain.Transactions;

public sealed class TransactionCodec
{
    private readonly HashFunc _hash = new();

    public byte[] Serialize(
        string sender,
        string receiver,
        long amount,
        long sequence)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(receiver);

        string amountText = amount.ToString(CultureInfo.InvariantCulture);
        string sequenceText = sequence.ToString(CultureInfo.InvariantCulture);

        string text = $"{sender}|{receiver}|{amountText}|{sequenceText}";

        return Encoding.UTF8.GetBytes(text);
    }

    public string ComputeId(
        string sender,
        string receiver,
        long amount,
        long sequence)
    {
        byte[] data = Serialize(sender, receiver, amount, sequence);

        return _hash.ComputeHash(data);
    }
}