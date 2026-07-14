using SqlDataManager.Contracts;
using SqlDataManager.Domain.Enums;
using SqlDataManager.Domain.Metadata;
using SqlDataManager.Domain.Mutations;
using SqlDataManager.Domain.Querying;

namespace SqlDataManager.Application.Mapping;

/// <summary>
/// Translates between the wire DTOs (Contracts) and the strongly-typed Domain
/// model. Parsing the loosely-typed strings on the DTOs (filter operators, sort
/// direction) into closed Domain enums here is itself a safety step: anything
/// that does not map to a known enum value is rejected immediately.
/// </summary>
public static class ContractMapping
{
    // ---- Metadata -> DTO ---------------------------------------------------

    public static PermissionsDto ToDto(this ObjectPermissions p) => new()
    {
        CanRead = p.CanRead,
        CanCreate = p.CanCreate,
        CanUpdate = p.CanUpdate,
        CanDelete = p.CanDelete
    };

    public static DatabaseObjectDto ToDto(this DatabaseObject o) => new()
    {
        Schema = o.Schema,
        Name = o.Name,
        ObjectType = o.ObjectType.ToString(),
        FullName = o.FullName,
        Permissions = o.Permissions.ToDto()
    };

    public static ColumnMetadataDto ToDto(this ColumnMetadata c) => new()
    {
        Name = c.Name,
        OrdinalPosition = c.OrdinalPosition,
        SqlType = c.SqlType,
        ClrType = c.ClrType.Name,
        IsNullable = c.IsNullable,
        MaxLength = c.MaxLength,
        NumericPrecision = c.NumericPrecision,
        NumericScale = c.NumericScale,
        HasDefault = c.HasDefault,
        IsIdentity = c.IsIdentity,
        IsComputed = c.IsComputed,
        IsPrimaryKey = c.IsPrimaryKey,
        IsRowVersion = c.IsRowVersion,
        IsInsertable = c.IsInsertable,
        IsUpdatable = c.IsUpdatable,
        IsFilterable = c.IsFilterable,
        IsSortable = c.IsSortable,
        IsSearchable = c.IsSearchable,
        Sensitivity = c.Sensitivity.ToString(),
        DisplayName = c.DisplayName
    };

    public static ObjectMetadataDto ToDto(this ObjectMetadata m) => new()
    {
        Schema = m.Schema,
        Name = m.Name,
        ObjectType = m.ObjectType.ToString(),
        FullName = m.FullName,
        Permissions = m.Permissions.ToDto(),
        Columns = m.Columns.Select(c => c.ToDto()).ToList(),
        KeyColumns = m.KeyColumns,
        RowVersionColumn = m.RowVersionColumn,
        HasStableKey = m.HasStableKey
    };

    // ---- Query DTO -> Domain ----------------------------------------------

    public static QueryRequest ToDomain(this QueryRowsRequest r) => new()
    {
        Page = r.Page,
        PageSize = r.PageSize,
        SelectedColumns = r.SelectedColumns,
        GlobalSearch = r.GlobalSearch,
        IncludeTotalCount = r.IncludeTotalCount,
        Sort = r.Sort.Select(s => new SortDefinition
        {
            Column = s.Column,
            Direction = ParseDirection(s.Direction)
        }).ToList(),
        Filters = r.Filters.Select(f => new FilterDefinition
        {
            Column = f.Column,
            Operator = ParseOperator(f.Operator),
            Value = f.Value,
            SecondValue = f.SecondValue
        }).ToList()
    };

    public static QueryRowsResponse ToDto(this QueryResult result) => new()
    {
        Rows = result.Rows,
        Page = result.Page,
        PageSize = result.PageSize,
        TotalCount = result.TotalCount,
        Columns = result.Columns,
        UnstableOrdering = result.UnstableOrdering
    };

    // ---- Mutation DTO -> Domain -------------------------------------------

    public static RecordKey ToDomain(this IReadOnlyList<RecordKeyValueDto> keys) =>
        new(keys.Select(k => new RecordKeyValue(k.Column, k.Value)).ToList());

    public static CreateRecordRequest ToDomain(this CreateRecordRequestDto dto) => new()
    {
        Fields = dto.Fields.Select(f => new FieldValue(f.Column, f.Value)).ToList()
    };

    public static UpdateRecordRequest ToDomain(this UpdateRecordRequestDto dto) => new()
    {
        Key = dto.Keys.ToDomain(),
        ChangedFields = dto.ChangedFields.Select(f => new FieldValue(f.Column, f.Value)).ToList(),
        ConcurrencyToken = dto.ConcurrencyToken,
        OriginalValues = dto.OriginalValues.Select(f => new FieldValue(f.Column, f.Value)).ToList()
    };

    public static DeleteRecordRequest ToDomain(this DeleteRecordRequestDto dto) => new()
    {
        Key = dto.Keys.ToDomain(),
        ConcurrencyToken = dto.ConcurrencyToken
    };

    public static RecordMutationResultDto ToDto(this RecordMutationResult r) => new()
    {
        Success = r.Success,
        AffectedRows = r.AffectedRows,
        GeneratedKeys = r.GeneratedKeys,
        Record = r.Record
    };

    // ---- Parsing helpers (closed vocabularies) -----------------------------

    private static SortDirection ParseDirection(string? direction) =>
        string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase)
            ? SortDirection.Descending
            : SortDirection.Ascending;

    private static FilterOperator ParseOperator(string raw)
    {
        // Strip separators so "greater_than", "greaterThan" and "GREATERTHAN"
        // all resolve to the same enum member.
        var cleaned = raw.Replace("_", string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty);
        if (Enum.TryParse<FilterOperator>(cleaned, ignoreCase: true, out var op))
        {
            return op;
        }

        throw new Domain.Exceptions.SqlDataManagerException(
            Domain.Exceptions.ErrorCodes.ValidationFailed,
            $"Unknown filter operator '{raw}'.");
    }
}
