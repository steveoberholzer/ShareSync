namespace SharePointPermissionSync.Data.Entities;

/// <summary>
/// Represents an interaction within a project
/// </summary>
public class Interaction : BaseEntity
{
    /// <summary>
    /// Parent interaction ID (for nested interactions)
    /// </summary>
    public Guid? ParentId { get; set; }

    /// <summary>
    /// Type of interaction
    /// </summary>
    public int? InteractionTypeId { get; set; }

    /// <summary>
    /// Parent engagement ID
    /// </summary>
    public Guid? EngagementId { get; set; }

    /// <summary>
    /// Parent project ID
    /// </summary>
    public Guid? ProjectId { get; set; }

    /// <summary>
    /// Interaction number
    /// </summary>
    public int? InteractionNumber { get; set; }

    /// <summary>
    /// Name of the interaction
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Description of the interaction
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether this interaction is private
    /// </summary>
    public bool? IsPrivate { get; set; }

    /// <summary>
    /// Due date for the interaction
    /// </summary>
    public DateTime? DueDate { get; set; }

    /// <summary>
    /// Latest release date
    /// </summary>
    public DateTime? LatestReleaseDate { get; set; }

    /// <summary>
    /// Latest submission date
    /// </summary>
    public DateTime? LatestSubmissionDate { get; set; }

    /// <summary>
    /// Latest return date
    /// </summary>
    public DateTime? LatestReturnDate { get; set; }

    /// <summary>
    /// SharePoint folder ID for this interaction
    /// </summary>
    public int? SharePointFolderID { get; set; }

    /// <summary>
    /// Status of the interaction
    /// </summary>
    public int? StatusId { get; set; }

    /// <summary>
    /// SharePoint folder name
    /// </summary>
    public string? SharePointFolderName { get; set; }

    /// <summary>
    /// Number of files in SharePoint folder
    /// </summary>
    public int? SharePointFileCount { get; set; }

    /// <summary>
    /// When status was last changed
    /// </summary>
    public DateTime? StatusLastChangedOn { get; set; }

    /// <summary>
    /// Who last changed the status
    /// </summary>
    public string? StatusLastChangedBy { get; set; }

    /// <summary>
    /// FQN of who last changed the status
    /// </summary>
    public string? StatusLastChangedByFQN { get; set; }

    /// <summary>
    /// When this interaction was activated
    /// </summary>
    public DateTime? ActivatedOn { get; set; }

    /// <summary>
    /// Semicolon-separated list of internal user emails
    /// </summary>
    public string? InternalUsers { get; set; }

    /// <summary>
    /// Semicolon-separated list of external user emails
    /// </summary>
    public string? ExternalUsers { get; set; }

    // Navigation properties
    /// <summary>
    /// Parent engagement
    /// </summary>
    public virtual Engagement? Engagement { get; set; }

    /// <summary>
    /// Parent project
    /// </summary>
    public virtual Project? Project { get; set; }

    /// <summary>
    /// Interaction memberships (permissions)
    /// </summary>
    public virtual ICollection<InteractionMembership> InteractionMemberships { get; set; } = new List<InteractionMembership>();
}
