using SqlDataManager.Application.Mapping;
using SqlDataManager.Contracts;
using SqlDataManager.Domain.Enums;
using Xunit;

namespace SqlDataManager.UnitTests;

public class MappingTests
{
    [Theory]
    [InlineData("contains", FilterOperator.Contains)]
    [InlineData("greaterThanOrEqual", FilterOperator.GreaterThanOrEqual)]
    [InlineData("greater_than", FilterOperator.GreaterThan)]
    [InlineData("IS_NULL", FilterOperator.IsNull)]
    public void Parses_filter_operators_from_various_string_forms(string raw, FilterOperator expected)
    {
        var request = new QueryRowsRequest { Filters = [new FilterDto { Column = "C", Operator = raw }] };
        var domain = request.ToDomain();
        Assert.Equal(expected, domain.Filters[0].Operator);
    }

    [Fact]
    public void Unknown_operator_throws()
    {
        var request = new QueryRowsRequest { Filters = [new FilterDto { Column = "C", Operator = "explode" }] };
        Assert.ThrowsAny<System.Exception>(() => request.ToDomain());
    }

    [Theory]
    [InlineData("asc", SortDirection.Ascending)]
    [InlineData("desc", SortDirection.Descending)]
    [InlineData("DESC", SortDirection.Descending)]
    public void Parses_sort_direction(string raw, SortDirection expected)
    {
        var request = new QueryRowsRequest { Sort = [new SortDto { Column = "C", Direction = raw }] };
        Assert.Equal(expected, request.ToDomain().Sort[0].Direction);
    }
}
