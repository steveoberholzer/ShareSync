namespace SharePointPermissionSync.Data.Entities;

/// <summary>
/// Base entity with common audit fields
/// </summary>
public abstract class BaseEntity
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Whether this entity is active
    /// </summary>
    public bool? IsActive { get; set; }

    /// <summary>
    /// When this entity was created
    /// </summary>
    public DateTime? CreatedOn { get; set; }

    /// <summary>
    /// Who created this entity (display name)
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Fully qualified name of who created this entity
    /// </summary>
    public string? CreatedByFQN { get; set; }

    /// <summary>
    /// When this entity was last modified
    /// </summary>
    public DateTime? ModifiedOn { get; set; }

    /// <summary>
    /// Who last modified this entity (display name)
    /// </summary>
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// Fully qualified name of who last modified this entity
    /// </summary>
    public string? ModifiedByFQN { get; set; }

    /// <summary>
    /// Timestamp of last modification
    /// </summary>
    public string? ModifiedOnTimeStamp { get; set; }
}
