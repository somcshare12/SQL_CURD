using SqlDataManager.Infrastructure.Sql;
using Xunit;

namespace SqlDataManager.UnitTests;

public class SqlIdentifierTests
{
    [Fact]
    public void Quote_wraps_name_in_brackets()
    {
        Assert.Equal("[Customers]", SqlIdentifier.Quote("Customers"));
    }

    [Fact]
    public void Quote_doubles_embedded_closing_bracket_to_prevent_injection()
    {
        // A malicious name containing "]" must be neutralised by doubling it.
        Assert.Equal("[evil]] ]]--]", SqlIdentifier.Quote("evil] ]--"));
    }

    [Fact]
    public void QuoteQualified_quotes_both_parts()
    {
        Assert.Equal("[dbo].[Customers]", SqlIdentifier.QuoteQualified("dbo", "Customers"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Quote_rejects_empty_names(string name)
    {
        Assert.ThrowsAny<System.ArgumentException>(() => SqlIdentifier.Quote(name));
    }

    [Fact]
    public void Quote_rejects_names_over_128_chars()
    {
        Assert.Throws<System.ArgumentException>(() => SqlIdentifier.Quote(new string('a', 129)));
    }
}
