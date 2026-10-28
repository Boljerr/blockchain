using System.Globalization;
using System.Text;
using BlockChain.Hash;
using BlockChain.Models;

namespace BlockChain.Tests;

internal static class TestTransactions
{
    private static readonly HashFunc HashFunc = new();

    public static Transaction Create(string sender, string receiver, long amount, long sequence)
    {
        string serialized = string.Create(CultureInfo.InvariantCulture, $"{sender}|{receiver}|{amount}|{sequence}");
        string id = HashFunc.ComputeHash(Encoding.UTF8.GetBytes(serialized));
        return new Transaction(sender, receiver, amount, sequence, id);
    }
}