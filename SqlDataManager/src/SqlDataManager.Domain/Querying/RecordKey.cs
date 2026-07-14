namespace SqlDataManager.Domain.Querying;

/// <summary>
/// A single component of a record key: the column name and the value that
/// identifies the row for that column. Composite keys are represented as an
/// ordered collection of these.
/// </summary>
public sealed record RecordKeyValue(string Column, object? Value);

/// <summary>
/// A structured, stable identifier for exactly one row. This is deliberately a
/// list of name/value pairs (never a row number and never "all displayed
/// columns") so that update and delete operations always target one specific
/// record through its primary/unique key.
/// </summary>
public sealed record RecordKey(IReadOnlyList<RecordKeyValue> Keys)
{
    public bool IsEmpty => Keys.Count == 0;
}
