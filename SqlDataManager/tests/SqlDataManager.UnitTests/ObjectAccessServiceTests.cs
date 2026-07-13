using Microsoft.Extensions.Options;
using SqlDataManager.Application.Abstractions;
using SqlDataManager.Application.Configuration;
using SqlDataManager.Application.Services;
using SqlDataManager.Domain.Enums;
using SqlDataManager.Domain.Metadata;
using Xunit;

namespace SqlDataManager.UnitTests;

public class ObjectAccessServiceTests
{
    private static ObjectMetadata Table(string schema, string name, bool hasKey = true) => new()
    {
        Schema = schema,
        Name = name,
        ObjectType = DatabaseObjectType.Table,
        Permissions = new ObjectPermissions { CanRead = true, CanCreate = true, CanUpdate = hasKey, CanDelete = hasKey },
        Columns = [new ColumnMetadata { Name = "Id", OrdinalPosition = 1, SqlType = "int", ClrType = typeof(int), IsNullable = false }],
        KeyColumns = hasKey ? ["Id"] : [],
    };

    private static ObjectAccessService Build(SqlDataManagerOptions options, bool authorized = true) =>
        new(Options.Create(options), new FakeUser(authorized));

    [Fact]
    public void Denied_schema_wins_over_allow_list()
    {
        var options = new SqlDataManagerOptions();
        options.ObjectAccess.AllowedSchemas = ["dbo"];
        options.ObjectAccess.DeniedSchemas = ["dbo"]; // deny must win

        var permissions = Build(options).ResolvePermissions(Table("dbo", "Customers"));
        Assert.False(permissions.CanRead);
    }

    [Fact]
    public void Denied_object_is_not_visible()
    {
        var options = new SqlDataManagerOptions();
        options.ObjectAccess.DeniedObjects = ["dbo.Users"];

        Assert.False(Build(options).ResolvePermissions(Table("dbo", "Users")).CanRead);
    }

    [Fact]
    public void Not_in_allow_list_is_not_visible()
    {
        var options = new SqlDataManagerOptions();
        options.ObjectAccess.AllowedSchemas = ["sales"];

        Assert.False(Build(options).ResolvePermissions(Table("dbo", "Customers")).CanRead);
    }

    [Fact]
    public void Default_read_only_disables_writes_even_when_capable()
    {
        var options = new SqlDataManagerOptions();
        options.ObjectAccess.AllowedSchemas = ["dbo"];
        options.ObjectAccess.DefaultTableAccess = "ReadOnly";

        var permissions = Build(options).ResolvePermissions(Table("dbo", "Customers"));
        Assert.True(permissions.CanRead);
        Assert.False(permissions.CanCreate);
        Assert.False(permissions.CanUpdate);
        Assert.False(permissions.CanDelete);
    }

    [Fact]
    public void Per_object_permission_overrides_default()
    {
        var options = new SqlDataManagerOptions();
        options.ObjectAccess.AllowedSchemas = ["dbo"];
        options.ObjectPermissions["dbo.Customers"] = new ObjectPermissionOptions
        {
            Read = true, Create = true, Update = true, Delete = false,
        };

        var permissions = Build(options).ResolvePermissions(Table("dbo", "Customers"));
        Assert.True(permissions.CanCreate);
        Assert.True(permissions.CanUpdate);
        Assert.False(permissions.CanDelete); // explicitly denied
    }

    [Fact]
    public void Keyless_table_cannot_update_or_delete_even_if_configured()
    {
        var options = new SqlDataManagerOptions();
        options.ObjectAccess.AllowedSchemas = ["dbo"];
        options.ObjectPermissions["dbo.Log"] = new ObjectPermissionOptions
        {
            Read = true, Create = true, Update = true, Delete = true,
        };

        var permissions = Build(options).ResolvePermissions(Table("dbo", "Log", hasKey: false));
        Assert.False(permissions.CanUpdate);
        Assert.False(permissions.CanDelete);
    }

    [Fact]
    public void Global_crud_flag_disables_create()
    {
        var options = new SqlDataManagerOptions();
        options.ObjectAccess.AllowedSchemas = ["dbo"];
        options.ObjectAccess.DefaultTableAccess = "ReadWrite";
        options.Crud.EnableCreate = false;

        Assert.False(Build(options).ResolvePermissions(Table("dbo", "Customers")).CanCreate);
    }

    [Fact]
    public void Unauthorized_user_loses_access_when_auth_required()
    {
        var options = new SqlDataManagerOptions();
        options.ObjectAccess.AllowedSchemas = ["dbo"];
        options.Security.RequireAuthentication = true;

        Assert.False(Build(options, authorized: false).ResolvePermissions(Table("dbo", "Customers")).CanRead);
    }

    private sealed class FakeUser(bool authorized) : ICurrentUser
    {
        public string UserId => "test";
        public bool IsAuthenticated => authorized;
        public bool IsAuthorized(string policy) => authorized;
    }
}
