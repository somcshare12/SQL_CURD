using SqlDataManager.Application.Abstractions;
using SqlDataManager.Application.Utilities;
using SqlDataManager.Domain.Exceptions;
using SqlDataManager.Domain.Mutations;
using SqlDataManager.Domain.Validation;

namespace SqlDataManager.Application.Services;

/// <summary>
/// Shared orchestration for the three mutation use cases. Each concrete service
/// (create/update/delete) enforces the relevant permission, validates the
/// payload, writes an audit event and delegates the transactional database work
/// to the <see cref="IMutationRepository"/>.
/// </summary>
public abstract class MutationServiceBase(
    MetadataService metadataService,
    IMutationRepository mutationRepository,
    ISqlDataManagerAuditWriter auditWriter)
{
    protected MetadataService MetadataService { get; } = metadataService;
    protected IMutationRepository MutationRepository { get; } = mutationRepository;

    protected async Task AuditAsync(
        string operation, string schema, string name, RecordMutationResult result, CancellationToken ct)
    {
        await auditWriter.WriteAsync(new SqlDataManagerAuditEvent
        {
            Operation = operation,
            Schema = schema,
            ObjectName = name,
            Success = result.Success,
            AffectedRows = result.AffectedRows
        }, ct);
    }

    protected static void ThrowIfInvalid(ValidationResult validation)
    {
        if (!validation.IsValid)
        {
            throw new ValidationFailedException(validation.ToDictionary());
        }
    }

    /// <summary>Decodes the optional base64 rowversion concurrency token.</summary>
    protected static byte[]? DecodeToken(string? token) =>
        string.IsNullOrEmpty(token) ? null : Convert.FromBase64String(token);
}

/// <summary>Inserts a new record after validation and confirmation on the client.</summary>
public sealed class CreateRecordService(
    MetadataService metadataService,
    IMutationRepository mutationRepository,
    RecordValidator validator,
    ISqlDataManagerAuditWriter auditWriter)
    : MutationServiceBase(metadataService, mutationRepository, auditWriter)
{
    public async Task<RecordMutationResult> CreateAsync(
        string schema, string name, CreateRecordRequest request, CancellationToken ct)
    {
        var metadata = await MetadataService.GetAccessibleMetadataAsync(schema, name, ct);

        if (!metadata.Permissions.CanCreate)
        {
            throw new OperationNotAllowedException(ErrorCodes.CreateNotAllowed, "Create is not permitted.");
        }

        var validation = validator.ValidateCreate(metadata, request.Fields, out var coerced);
        ThrowIfInvalid(validation);

        var command = new PreparedCreate
        {
            Metadata = metadata,
            Values = coerced.Select(c => new PreparedValue(c.Column, c.Value)).ToList()
        };

        var result = await MutationRepository.CreateAsync(command, ct);
        await AuditAsync("Create", schema, name, result, ct);
        return result;
    }
}

/// <summary>Updates exactly one record with optimistic-concurrency protection.</summary>
public sealed class UpdateRecordService(
    MetadataService metadataService,
    IMutationRepository mutationRepository,
    RecordValidator validator,
    ISqlDataManagerAuditWriter auditWriter)
    : MutationServiceBase(metadataService, mutationRepository, auditWriter)
{
    public async Task<RecordMutationResult> UpdateAsync(
        string schema, string name, UpdateRecordRequest request, CancellationToken ct)
    {
        var metadata = await MetadataService.GetAccessibleMetadataAsync(schema, name, ct);

        if (!metadata.Permissions.CanUpdate)
        {
            throw new OperationNotAllowedException(ErrorCodes.UpdateNotAllowed, "Update is not permitted.");
        }

        // A stable key is mandatory; never update using "all displayed columns".
        RowQueryService.EnsureKeyMatches(metadata, request.Key);
        var preparedKey = KeyPreparer.Prepare(metadata, request.Key);

        var validation = validator.ValidateUpdate(metadata, request.ChangedFields, out var coerced);
        ThrowIfInvalid(validation);

        var command = new PreparedUpdate
        {
            Metadata = metadata,
            Key = preparedKey,
            Changes = coerced.Select(c => new PreparedValue(c.Column, c.Value)).ToList(),
            ConcurrencyToken = DecodeToken(request.ConcurrencyToken)
        };

        var result = await MutationRepository.UpdateAsync(command, ct);
        await AuditAsync("Update", schema, name, result, ct);
        return result;
    }
}

/// <summary>Deletes exactly one record after explicit confirmation on the client.</summary>
public sealed class DeleteRecordService(
    MetadataService metadataService,
    IMutationRepository mutationRepository,
    ISqlDataManagerAuditWriter auditWriter)
    : MutationServiceBase(metadataService, mutationRepository, auditWriter)
{
    public async Task<RecordMutationResult> DeleteAsync(
        string schema, string name, DeleteRecordRequest request, CancellationToken ct)
    {
        var metadata = await MetadataService.GetAccessibleMetadataAsync(schema, name, ct);

        if (!metadata.Permissions.CanDelete)
        {
            throw new OperationNotAllowedException(ErrorCodes.DeleteNotAllowed, "Delete is not permitted.");
        }

        RowQueryService.EnsureKeyMatches(metadata, request.Key);
        var preparedKey = KeyPreparer.Prepare(metadata, request.Key);

        var command = new PreparedDelete
        {
            Metadata = metadata,
            Key = preparedKey,
            ConcurrencyToken = DecodeToken(request.ConcurrencyToken)
        };

        var result = await MutationRepository.DeleteAsync(command, ct);
        await AuditAsync("Delete", schema, name, result, ct);
        return result;
    }
}
