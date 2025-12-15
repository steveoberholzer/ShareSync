namespace SharePointPermissionSync.Data.Entities;

/// <summary>
/// Represents membership/permissions for an interaction
/// </summary>
public class InteractionMembership
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Engagement membership ID this is associated with
    /// </summary>
    public Guid? EngagementMembershipId { get; set; }

    /// <summary>
    /// Interaction this membership belongs to
    /// </summary>
    public Guid? InteractionId { get; set; }

    /// <summary>
    /// Role type for this interaction
    /// </summary>
    public int? InteractionRoleTypeId { get; set; }

    /// <summary>
    /// Whether this is the primary member for this role
    /// </summary>
    public bool? IsPrimary { get; set; }

    /// <summary>
    /// Whether this membership is active
    /// </summary>
    public bool? IsActive { get; set; }

    /// <summary>
    /// When this membership was created
    /// </summary>
    public DateTime? CreatedOn { get; set; }

    /// <summary>
    /// Who created this membership
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// FQN of who created this membership
    /// </summary>
    public string? CreatedByFQN { get; set; }

    /// <summary>
    /// When this membership was last modified
    /// </summary>
    public DateTime? ModifiedOn { get; set; }

    /// <summary>
    /// Who last modified this membership
    /// </summary>
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// FQN of who last modified this membership
    /// </summary>
    public string? ModifiedByFQN { get; set; }

    /// <summary>
    /// Member ID
    /// </summary>
    public Guid? MemberId { get; set; }

    // Navigation properties
    /// <summary>
    /// Parent interaction
    /// </summary>
    public virtual Interaction? Interaction { get; set; }
}
