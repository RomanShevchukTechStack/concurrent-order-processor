using OrderProcessor.App;

namespace OrderProcessor.Tests;

public class OrderParserTests
{
    [Fact]
    public void Valid_line_gives_order()
    {
        var ok = OrderParser.TryParse("O000001,C0001,P001,3", 2, out var order, out var failure);

        Assert.True(ok);
        Assert.Null(failure);
        Assert.Equal(new Order("O000001", "C0001", "P001", 3), order);
    }

    [Fact]
    public void Fields_are_trimmed()
    {
        var ok = OrderParser.TryParse(" O1 , C1 , P001 , 5 ", 2, out var order, out _);

        Assert.True(ok);
        Assert.Equal(new Order("O1", "C1", "P001", 5), order);
    }

    [Theory]
    [InlineData("O1,C1,P001", "expected 4 fields, got 3")]
    [InlineData("O1,C1,P001,2,extra", "expected 4 fields, got 5")]
    [InlineData("O1,,P001,2", "empty customer id")]
    [InlineData("O1,C1,,2", "empty product id")]
    [InlineData("O1,C1,P001,abc", "quantity 'abc' is not a number")]
    [InlineData("O1,C1,P001,", "quantity '' is not a number")]
    [InlineData("O1,C1,P001,0", "quantity 0 must be positive")]
    [InlineData("O1,C1,P001,-4", "quantity -4 must be positive")]
    public void Invalid_line_is_reported_with_reason(string line, string expectedDetail)
    {
        var ok = OrderParser.TryParse(line, 7, out var order, out var failure);

        Assert.False(ok);
        Assert.Null(order);
        Assert.Equal(new FailedOrder("O1", FailureCategory.InvalidRow, expectedDetail), failure);
    }

    [Fact]
    public void Missing_order_id_is_reported_by_line_number()
    {
        var ok = OrderParser.TryParse(",C1,P001,2", 12, out _, out var failure);

        Assert.False(ok);
        Assert.Equal(new FailedOrder("line 12", FailureCategory.InvalidRow, "empty order id"), failure);
    }
}
