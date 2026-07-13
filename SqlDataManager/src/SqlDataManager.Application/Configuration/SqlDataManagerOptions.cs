using SqlDataManager.Domain.Enums;

namespace SqlDataManager.Application.Configuration;

/// <summary>
/// Strongly-typed representation of the <c>Modules:SqlDataManager</c>
/// configuration section. Every runtime behaviour of the module (which schemas
/// are visible, which CRUD operations are enabled, page-size limits, security
/// policies, and so on) is driven from this single options object.
///
/// The database connection details live here too, but they never leave the
/// backend: no endpoint returns the server, database, username or password.
/// </summary>
public sealed class SqlDataManagerOptions
{
    /// <summary>The configuration section path used everywhere for binding.</summary>
    public const string SectionName = "Modules:SqlDataManager";

    /// <summary>Master on/off switch for the whole module.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Display name so the parent app can rebrand the module.</summary>
    public string DisplayName { get; set; } = "SQL Data Manager";

    /// <summary>Default API route prefix (overridable at registration time).</summary>
    public string ApiRoutePrefix { get; set; } = "/api/sql-data-manager";

    public ConnectionOptions Connection { get; set; } = new();
    public DataDisplayOptions DataDisplay { get; set; } = new();
    public CrudOptions Crud { get; set; } = new();
    public ObjectAccessOptions ObjectAccess { get; set; } = new();

    /// <summary>Per-object permission overrides keyed by <c>schema.object</c>.</summary>
    public Dictionary<string, ObjectPermissionOptions> ObjectPermissions { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Per-object sensitive-column configuration keyed by <c>schema.object</c>.</summary>
    public Dictionary<string, ObjectSensitivityOptions> ObjectSensitivity { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public SettingsStorageOptions SettingsStorage { get; set; } = new();
    public SecurityOptions Security { get; set; } = new();
}

/// <summary>SQL Server connection settings (never exposed to the client).</summary>
public sealed class ConnectionOptions
{
    public string Server { get; set; } = "localhost";
    public string Database { get; set; } = "master";

    /// <summary>"Windows" for integrated auth, or "SqlPassword".</summary>
    public string AuthenticationType { get; set; } = "SqlPassword";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public bool Encrypt { get; set; } = true;
    public bool TrustServerCertificate { get; set; } = false;
    public int ConnectionTimeoutSeconds { get; set; } = 30;
    public int CommandTimeoutSeconds { get; set; } = 60;
    public string ApplicationName { get; set; } = "SqlDataManager";

    /// <summary>How long to cache discovered metadata before re-reading it.</summary>
    public int MetadataCacheSeconds { get; set; } = 300;
}

public sealed class DataDisplayOptions
{
    public int DefaultPageSize { get; set; } = 100;

    // NOTE: intentionally NOT initialised with defaults. The configuration
    // binder appends to a pre-populated array, which would duplicate values.
    public int[] AllowedPageSizes { get; set; } = [];

    public int MaximumPageSize { get; set; } = 500;
    public int MaximumDisplayedTextLength { get; set; } = 5000;
    public RowCountMode ExactRowCountMode { get; set; } = RowCountMode.OnDemand;
}

public sealed class CrudOptions
{
    public bool EnableCreate { get; set; } = true;
    public bool EnableUpdate { get; set; } = true;
    public bool EnableDelete { get; set; } = true;
    public bool RequireCreateConfirmation { get; set; } = true;
    public bool RequireUpdateConfirmation { get; set; } = true;
    public bool RequireDeleteConfirmation { get; set; } = true;
    public bool RequireTypedDeleteConfirmation { get; set; }
    public bool AllowBulkDelete { get; set; }
    public int MaximumBulkDeleteRows { get; set; } = 20;
}

public sealed class ObjectAccessOptions
{
    /// <summary>"ReadOnly" or "ReadWrite" default for tables.</summary>
    public string DefaultTableAccess { get; set; } = "ReadOnly";
    public string DefaultViewAccess { get; set; } = "ReadOnly";
    public List<string> AllowedSchemas { get; set; } = [];
    public List<string> DeniedSchemas { get; set; } = [];
    public List<string> AllowedObjects { get; set; } = [];
    public List<string> DeniedObjects { get; set; } = [];
}

public sealed class ObjectPermissionOptions
{
    public bool Read { get; set; } = true;
    public bool Create { get; set; }
    public bool Update { get; set; }
    public bool Delete { get; set; }
}

public sealed class ObjectSensitivityOptions
{
    /// <summary>Column name -> behaviour ("Masked", "Hidden", "ReadOnly").</summary>
    public Dictionary<string, string> SensitiveColumns { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class SettingsStorageOptions
{
    public string Provider { get; set; } = "JsonFile";
    public string Directory { get; set; } = "AppData/SqlDataManager";
    public bool AutoSave { get; set; } = true;
    public int SaveDebounceMilliseconds { get; set; } = 750;
}

public sealed class SecurityOptions
{
    public bool RequireAuthentication { get; set; }
    public string ReadPolicy { get; set; } = "SqlDataManager.Read";
    public string CreatePolicy { get; set; } = "SqlDataManager.Create";
    public string UpdatePolicy { get; set; } = "SqlDataManager.Update";
    public string DeletePolicy { get; set; } = "SqlDataManager.Delete";
    public string ManageSettingsPolicy { get; set; } = "SqlDataManager.ManageSettings";
    public string RefreshMetadataPolicy { get; set; } = "SqlDataManager.RefreshMetadata";
}
