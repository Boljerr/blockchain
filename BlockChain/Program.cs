using BlockChain.Hash;
using BlockChain.Input;

var inputReader = new InputReader();
var hashFunc = new HashFunc();

byte[] data;

while (true)
{
    Console.WriteLine("1 - Write text");
    Console.WriteLine("2 - Exit");

    string? choice = Console.ReadLine();
    
    switch (choice)
    {
        case "1":
            data = inputReader.ReadText();
            break;
        case "2":
            return;
        default:
            Console.WriteLine("Invalid choice. Try again.");
            continue;
    }

    break;
}

while (true)
{
    Console.WriteLine("1 - Igno implementation");
    string? choice = Console.ReadLine();
    
    switch (choice)
    {
        case "1":
            string hashIgno = hashFunc.ComputeHash(data);
            Console.WriteLine();
            Console.WriteLine($"Hash: {hashIgno}");
            break;
        default:
            Console.WriteLine("Invalid choice. Try again.");
            continue;
    }

    break;
}