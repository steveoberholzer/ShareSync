namespace SharePointPermissionSync.Data.Entities;

/// <summary>
/// Represents an engagement (top-level container for projects)
/// </summary>
public class Engagement : BaseEntity
{
    /// <summary>
    /// Name of the engagement
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// SharePoint site URL for this engagement
    /// </summary>
    public string? SiteURL { get; set; }

    /// <summary>
    /// Client this engagement belongs to
    /// </summary>
    public int? ClientId { get; set; }

    /// <summary>
    /// Due date for the engagement
    /// </summary>
    public DateTime? DueDate { get; set; }

    /// <summary>
    /// SharePoint folder ID for this engagement
    /// </summary>
    public int? SharePointFolderID { get; set; }

    /// <summary>
    /// Status of the engagement
    /// </summary>
    public int? StatusId { get; set; }

    /// <summary>
    /// Whether this is a "not won" engagement
    /// </summary>
    public bool? IsNotWonEngagement { get; set; }

    /// <summary>
    /// SharePoint folder name
    /// </summary>
    public string? SharePointFolderName { get; set; }

    // Navigation properties
    /// <summary>
    /// Projects belonging to this engagement
    /// </summary>
    public virtual ICollection<Project> Projects { get; set; } = new List<Project>();

    /// <summary>
    /// Interactions belonging to this engagement
    /// </summary>
    public virtual ICollection<Interaction> Interactions { get; set; } = new List<Interaction>();
}
