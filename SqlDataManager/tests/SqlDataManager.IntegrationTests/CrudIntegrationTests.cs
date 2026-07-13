using System.Net;
using System.Net.Http.Json;
using SqlDataManager.Contracts;
using Xunit;

namespace SqlDataManager.IntegrationTests;

/// <summary>
/// End-to-end integration tests that exercise the real SQL Server demo database
/// through the HTTP API: metadata discovery, access rules, paging and a full
/// create → read → update → delete round trip, plus the safety guardrails.
/// </summary>
public class CrudIntegrationTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private HttpClient Client => fixture.CreateClient();

    [SkippableFact]
    public async Task Objects_endpoint_lists_allowed_and_hides_denied()
    {
        Skip.IfNot(fixture.DatabaseAvailable, "SQL Server not available.");

        var objects = await fixture.GetJson<List<DatabaseObjectDto>>(Client, "/api/sql-data-manager/objects");
        var names = objects.Select(o => o.FullName).ToList();

        Assert.Contains("dbo.Customers", names);
        Assert.Contains("reporting.MonthlySales", names);
        Assert.DoesNotContain("dbo.Users", names);      // denied object
        Assert.DoesNotContain("internal.Secret", names); // denied schema
    }

    [SkippableFact]
    public async Task Metadata_reports_identity_key_and_rowversion()
    {
        Skip.IfNot(fixture.DatabaseAvailable, "SQL Server not available.");

        var metadata = await fixture.GetJson<ObjectMetadataDto>(
            Client, "/api/sql-data-manager/objects/dbo/Customers/metadata");

        Assert.Equal(["CustomerId"], metadata.KeyColumns);
        Assert.Equal("RowVersion", metadata.RowVersionColumn);
        Assert.Contains(metadata.Columns, c => c is { Name: "CustomerId", IsIdentity: true, IsInsertable: false });
    }

    [SkippableFact]
    public async Task Query_returns_a_page_with_total_count()
    {
        Skip.IfNot(fixture.DatabaseAvailable, "SQL Server not available.");

        var response = await Client.PostAsJsonAsync(
            "/api/sql-data-manager/objects/dbo/Customers/rows/query",
            new QueryRowsRequest { Page = 1, PageSize = 2, IncludeTotalCount = true });

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<QueryRowsResponse>(ApiFixture.Json);
        Assert.NotNull(result);
        Assert.True(result!.Rows.Count <= 2);
        Assert.NotNull(result.TotalCount);
    }

    [SkippableFact]
    public async Task Full_create_read_update_delete_round_trip()
    {
        Skip.IfNot(fixture.DatabaseAvailable, "SQL Server not available.");
        var client = Client;

        // CREATE
        var create = await client.PostAsJsonAsync(
            "/api/sql-data-manager/objects/dbo/Departments/rows",
            new CreateRecordRequestDto { Fields = [new FieldValueDto { Column = "Name", Value = "IntTest Dept" }] });
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<RecordMutationResultDto>(ApiFixture.Json);
        var id = created!.GeneratedKeys!["DepartmentId"];
        Assert.NotNull(id);

        // READ
        var read = await client.PostAsJsonAsync(
            "/api/sql-data-manager/objects/dbo/Departments/rows/read",
            new ReadRecordRequest { Keys = [new RecordKeyValueDto { Column = "DepartmentId", Value = id }] });
        read.EnsureSuccessStatusCode();

        // UPDATE
        var update = await client.PutAsJsonAsync(
            "/api/sql-data-manager/objects/dbo/Departments/rows",
            new UpdateRecordRequestDto
            {
                Keys = [new RecordKeyValueDto { Column = "DepartmentId", Value = id }],
                ChangedFields = [new FieldValueDto { Column = "Name", Value = "IntTest Dept Renamed" }],
            });
        update.EnsureSuccessStatusCode();

        // DELETE
        var delete = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete,
            "/api/sql-data-manager/objects/dbo/Departments/rows")
        {
            Content = JsonContent.Create(new DeleteRecordRequestDto
            {
                Keys = [new RecordKeyValueDto { Column = "DepartmentId", Value = id }],
            }),
        });
        delete.EnsureSuccessStatusCode();
    }

    [SkippableFact]
    public async Task Update_on_keyless_table_is_forbidden()
    {
        Skip.IfNot(fixture.DatabaseAvailable, "SQL Server not available.");

        var response = await Client.PutAsJsonAsync(
            "/api/sql-data-manager/objects/dbo/KeylessLog/rows",
            new UpdateRecordRequestDto
            {
                Keys = [new RecordKeyValueDto { Column = "Message", Value = "x" }],
                ChangedFields = [new FieldValueDto { Column = "Message", Value = "y" }],
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [SkippableFact]
    public async Task Delete_on_denied_object_returns_not_found()
    {
        Skip.IfNot(fixture.DatabaseAvailable, "SQL Server not available.");

        var response = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete,
            "/api/sql-data-manager/objects/dbo/Users/rows")
        {
            Content = JsonContent.Create(new DeleteRecordRequestDto
            {
                Keys = [new RecordKeyValueDto { Column = "UserId", Value = 1 }],
            }),
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
