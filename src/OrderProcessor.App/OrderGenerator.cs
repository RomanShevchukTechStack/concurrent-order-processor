using System.Globalization;
using System.Text;

namespace OrderProcessor.App;

/// <summary>Writes a reproducible orders CSV, including invalid rows and unknown products.</summary>
public static class OrderGenerator
{
    public const string Header = "OrderId,CustomerId,ProductId,Quantity";

    private const int ProductCount = 200;
    private const double InvalidRate = 0.03;
    private const double UnknownProductRate = 0.03;

    public static void Generate(string path, int rows, int seed)
    {
        var random = new Random(seed);
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        using var writer = new StreamWriter(path, append: false, new UTF8Encoding(false));
        writer.WriteLine(Header);

        for (var i = 1; i <= rows; i++)
        {
            var orderId = $"O{i:D6}";
            var customerId = $"C{random.Next(1, 1001):D4}";
            var productId = $"P{random.Next(1, ProductCount + 1):D3}";
            var quantity = random.Next(1, 21).ToString(CultureInfo.InvariantCulture);

            var roll = random.NextDouble();
            if (roll < InvalidRate)
            {
                // Invalid row: bad quantity, or empty customer.
                switch (random.Next(3))
                {
                    case 0: quantity = "abc"; break;
                    case 1: quantity = random.Next(-5, 1).ToString(CultureInfo.InvariantCulture); break;
                    default: customerId = string.Empty; break;
                }
            }
            else if (roll < InvalidRate + UnknownProductRate)
            {
                productId = $"X{random.Next(1, 1000):D3}";
            }

            writer.WriteLine($"{orderId},{customerId},{productId},{quantity}");
        }
    }
}
