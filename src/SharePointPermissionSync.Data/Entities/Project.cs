namespace SharePointPermissionSync.Data.Entities;

/// <summary>
/// Represents a project within an engagement
/// </summary>
public class Project : BaseEntity
{
    /// <summary>
    /// Parent engagement ID
    /// </summary>
    public Guid? EngagementId { get; set; }

    /// <summary>
    /// Name of the project
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Description of the project
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Expected end date for the project
    /// </summary>
    public DateTime? ExpectedEndDate { get; set; }

    /// <summary>
    /// Status of the project
    /// </summary>
    public int? StatusId { get; set; }

    /// <summary>
    /// SharePoint folder ID for this project
    /// </summary>
    public int? SharePointFolderID { get; set; }

    /// <summary>
    /// SharePoint folder name
    /// </summary>
    public string? SharePointFolderName { get; set; }

    // Navigation properties
    /// <summary>
    /// Parent engagement
    /// </summary>
    public virtual Engagement? Engagement { get; set; }

    /// <summary>
    /// Interactions belonging to this project
    /// </summary>
    public virtual ICollection<Interaction> Interactions { get; set; } = new List<Interaction>();
}
