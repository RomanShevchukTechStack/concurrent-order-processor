using System.Globalization;

namespace OrderProcessor.App;

public static class OrderParser
{
    /// <summary>
    /// Parses one CSV data line. On failure, <paramref name="failure"/> describes why. The failure
    /// is identified by the order id, or by the line number when the id is missing.
    /// </summary>
    public static bool TryParse(string line, int lineNumber, out Order? order, out FailedOrder? failure)
    {
        order = null;
        failure = null;

        var fields = line.Split(',');
        var orderId = fields.Length > 0 ? fields[0].Trim() : string.Empty;
        var id = orderId.Length > 0 ? orderId : $"line {lineNumber}";

        if (fields.Length != 4)
        {
            failure = Invalid(id, $"expected 4 fields, got {fields.Length}");
            return false;
        }

        var customerId = fields[1].Trim();
        var productId = fields[2].Trim();
        var quantityText = fields[3].Trim();

        if (orderId.Length == 0)
        {
            failure = Invalid(id, "empty order id");
            return false;
        }

        if (customerId.Length == 0)
        {
            failure = Invalid(id, "empty customer id");
            return false;
        }

        if (productId.Length == 0)
        {
            failure = Invalid(id, "empty product id");
            return false;
        }

        if (!int.TryParse(quantityText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var quantity))
        {
            failure = Invalid(id, $"quantity '{quantityText}' is not a number");
            return false;
        }

        if (quantity <= 0)
        {
            failure = Invalid(id, $"quantity {quantity} must be positive");
            return false;
        }

        order = new Order(orderId, customerId, productId, quantity);
        return true;
    }

    private static FailedOrder Invalid(string id, string detail) =>
        new(id, FailureCategory.InvalidRow, detail);
}
