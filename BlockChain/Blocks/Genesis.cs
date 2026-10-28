using BlockChain.Models;

namespace BlockChain.Blocks;

public static class Genesis
{
    public static Block Create(int difficulty)
    {
        var header = new BlockHeader(
            PrevHash: new string('0', 64),
            Timestamp: 0,
            Version: 1,
            TxRoot: TxRoot.Compute([]),
            Nonce: 0,
            Difficulty: difficulty);

        return new Block(header, [], BlockHasher.ComputeHash(header));
    }
}