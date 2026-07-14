using SqlDataManager.Application.Services;
using SqlDataManager.Application.Utilities;
using SqlDataManager.Domain.Enums;
using SqlDataManager.Domain.Metadata;
using SqlDataManager.Domain.Mutations;
using Xunit;

namespace SqlDataManager.UnitTests;

public class SqlValueConverterTests
{
    private static ColumnMetadata Column(Type clr, string sqlType, bool nullable = true, int? maxLen = null) => new()
    {
        Name = "C", OrdinalPosition = 1, SqlType = sqlType, ClrType = clr, IsNullable = nullable, MaxLength = maxLen,
    };

    [Fact]
    public void Coerces_integer_string_to_int()
    {
        Assert.True(SqlValueConverter.TryCoerce(Column(typeof(int), "int"), "42", out var value, out _));
        Assert.Equal(42, value);
    }

    [Fact]
    public void Coerces_decimal_preserving_value()
    {
        Assert.True(SqlValueConverter.TryCoerce(Column(typeof(decimal), "decimal"), "19.99", out var value, out _));
        Assert.Equal(19.99m, value);
    }

    [Fact]
    public void Coerces_guid()
    {
        var guid = System.Guid.NewGuid();
        Assert.True(SqlValueConverter.TryCoerce(Column(typeof(System.Guid), "uniqueidentifier"), guid.ToString(), out var value, out _));
        Assert.Equal(guid, value);
    }

    [Fact]
    public void Null_stays_null()
    {
        Assert.True(SqlValueConverter.TryCoerce(Column(typeof(int), "int"), null, out var value, out _));
        Assert.Null(value);
    }

    [Fact]
    public void Rejects_non_numeric_for_int()
    {
        Assert.False(SqlValueConverter.TryCoerce(Column(typeof(int), "int"), "abc", out _, out var error));
        Assert.NotNull(error);
    }
}

public class RecordValidatorTests
{
    private static ObjectMetadata Metadata() => new()
    {
        Schema = "dbo", Name = "T", ObjectType = DatabaseObjectType.Table,
        Permissions = ObjectPermissions.ReadOnly,
        KeyColumns = ["Id"],
        Columns =
        [
            new ColumnMetadata { Name = "Id", OrdinalPosition = 1, SqlType = "int", ClrType = typeof(int), IsNullable = false, IsIdentity = true, IsInsertable = false, IsUpdatable = false, IsPrimaryKey = true },
            new ColumnMetadata { Name = "Name", OrdinalPosition = 2, SqlType = "nvarchar", ClrType = typeof(string), IsNullable = false, MaxLength = 5, IsInsertable = true, IsUpdatable = true },
            new ColumnMetadata { Name = "Note", OrdinalPosition = 3, SqlType = "nvarchar", ClrType = typeof(string), IsNullable = true, IsInsertable = true, IsUpdatable = true },
        ],
    };

    private readonly RecordValidator _validator = new();

    [Fact]
    public void Create_requires_non_nullable_column_without_default()
    {
        var result = _validator.ValidateCreate(Metadata(), [new FieldValue("Note", "x")], out _);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "Name");
    }

    [Fact]
    public void Create_rejects_value_for_identity_column()
    {
        var result = _validator.ValidateCreate(Metadata(), [new FieldValue("Id", 5), new FieldValue("Name", "ok")], out _);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "Id");
    }

    [Fact]
    public void Create_enforces_max_length()
    {
        var result = _validator.ValidateCreate(Metadata(), [new FieldValue("Name", "toolong")], out _);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "Name");
    }

    [Fact]
    public void Create_succeeds_with_valid_values()
    {
        var result = _validator.ValidateCreate(Metadata(), [new FieldValue("Name", "abc")], out var coerced);
        Assert.True(result.IsValid);
        Assert.Contains(coerced, c => c.Column.Name == "Name");
    }

    [Fact]
    public void Update_with_no_changes_is_invalid()
    {
        var result = _validator.ValidateUpdate(Metadata(), [], out _);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Update_rejects_non_updatable_column()
    {
        var result = _validator.ValidateUpdate(Metadata(), [new FieldValue("Id", 9)], out _);
        Assert.False(result.IsValid);
    }
}
