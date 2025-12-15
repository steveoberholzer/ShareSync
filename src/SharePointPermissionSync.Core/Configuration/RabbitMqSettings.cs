namespace SharePointPermissionSync.Core.Configuration;

/// <summary>
/// RabbitMQ connection and queue configuration
/// </summary>
public class RabbitMqSettings
{
    /// <summary>
    /// RabbitMQ server hostname
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// RabbitMQ server port
    /// </summary>
    public int Port { get; set; } = 5672;

    /// <summary>
    /// RabbitMQ username
    /// </summary>
    public string Username { get; set; } = "guest";

    /// <summary>
    /// RabbitMQ password
    /// </summary>
    public string Password { get; set; } = "guest";

    /// <summary>
    /// Virtual host
    /// </summary>
    public string VirtualHost { get; set; } = "/";

    /// <summary>
    /// Queue names for different operation types
    /// </summary>
    public QueueConfiguration Queues { get; set; } = new();
}

/// <summary>
/// Queue name configuration
/// </summary>
public class QueueConfiguration
{
    /// <summary>
    /// Queue for interaction permission updates
    /// </summary>
    public string InteractionPermissions { get; set; } = "sharepoint.interaction.permissions";

    /// <summary>
    /// Queue for interaction creation
    /// </summary>
    public string InteractionCreation { get; set; } = "sharepoint.interaction.creation";

    /// <summary>
    /// Queue for removing unique permissions
    /// </summary>
    public string RemovePermissions { get; set; } = "sharepoint.remove.permissions";

    /// <summary>
    /// Dead letter queue for failed messages
    /// </summary>
    public string DeadLetter { get; set; } = "sharepoint.deadletter";
}
