using Newtonsoft.Json;
using SourceCode.SmartObjects.Services.ServiceSDK;
using SourceCode.SmartObjects.Services.ServiceSDK.Attributes;
using SourceCode.SmartObjects.Services.ServiceSDK.Objects;
using SourceCode.SmartObjects.Services.ServiceSDK.Types;
using System;
using System.Collections.Generic;
using Tecala.SMO.ShareSync.Services;

namespace Tecala.SMO.ShareSync.Services
{
    [ServiceObject(
        "Tecala.SMO.ShareSync",
        "SharePoint Permission Sync",
        "Service Object for synchronizing SharePoint permissions and creating interactions.")]
    public class ShareSyncService
    {
        private ServiceConfiguration _serviceConfig;
        private readonly ILogger _logger;
        private readonly ErrorNumberResolver _errorNumberService;

        #region Constructors

        public ShareSyncService()
        {
            _serviceConfig = new ServiceConfiguration();
            _logger = new Logger();
            _errorNumberService = new ErrorNumberResolver(this);
        }

        public ShareSyncService(ServiceConfiguration config)
        {
            _serviceConfig = config;
            _logger = new Logger();
            _errorNumberService = new ErrorNumberResolver(this);
        }

        public ServiceConfiguration ServiceConfiguration
        {
            get => _serviceConfig;
            set => _serviceConfig = value;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Sync permissions for a single interaction
        /// </summary>
        [Method(
            "SyncInteractionPermissions",
            MethodType.Execute,
            "Sync Interaction Permissions",
            "Queue a permission sync operation for a SharePoint interaction folder.",
            new[] { "InteractionId", "ProjectId", "EngagementId", "Environment", "SiteUrl" },
            new[] { "InteractionId", "ProjectId", "EngagementId", "SharePointFolderId", "Environment", "SiteUrl", "InternalPermission", "InternalUserEmails", "ExternalPermission", "ExternalUserEmails", "Priority" },
            new[] { "ErrorNumber", "ErrorMessage", "JobId", "MessageId" })]
        public ShareSyncService SyncInteractionPermissions()
        {
            try
            {
                _logger.LogInformation($"Starting SyncInteractionPermissions for Interaction {InteractionId}");

                // Validate and parse GUIDs
                if (string.IsNullOrWhiteSpace(InteractionId) || !Guid.TryParse(InteractionId, out Guid interactionGuid))
                    throw new ArgumentException("InteractionId is required and must be a valid GUID");

                if (string.IsNullOrWhiteSpace(ProjectId) || !Guid.TryParse(ProjectId, out Guid projectGuid))
                    throw new ArgumentException("ProjectId is required and must be a valid GUID");

                if (string.IsNullOrWhiteSpace(EngagementId) || !Guid.TryParse(EngagementId, out Guid engagementGuid))
                    throw new ArgumentException("EngagementId is required and must be a valid GUID");

                if (string.IsNullOrWhiteSpace(Environment))
                    throw new ArgumentException("Environment is required (DEV, UAT, or PROD)");

                if (string.IsNullOrWhiteSpace(SiteUrl))
                    throw new ArgumentException("SiteUrl is required");

                // Create services
                var connectionString = _serviceConfig["SQL Connection String"].ToString();
                using (var dbService = new DatabaseService(connectionString, _logger))
                {
                    // Query database for entity details
                    var (interactionName, interactionNumber, interactionSharePointFolderId) = dbService.GetInteractionDetails(interactionGuid);
                    var (projectName, projectSharePointFolderId) = dbService.GetProjectDetails(projectGuid);
                    var (engagementName, engagementSharePointFolderId) = dbService.GetEngagementDetails(engagementGuid);

                    // Validate that all SharePoint folder IDs exist
                    if (!interactionSharePointFolderId.HasValue || interactionSharePointFolderId.Value <= 0)
                        throw new ArgumentException($"Interaction '{interactionName}' has no SharePoint folder ID");

                    if (!projectSharePointFolderId.HasValue || projectSharePointFolderId.Value <= 0)
                        throw new ArgumentException($"Project '{projectName}' has no SharePoint folder ID");

                    if (!engagementSharePointFolderId.HasValue || engagementSharePointFolderId.Value <= 0)
                        throw new ArgumentException($"Engagement '{engagementName}' has no SharePoint folder ID");

                    var rabbitHost = _serviceConfig["RabbitMQ Host"].ToString();
                    var rabbitPort = int.Parse(_serviceConfig["RabbitMQ Port"].ToString());
                    var rabbitUser = _serviceConfig["RabbitMQ Username"].ToString();
                    var rabbitPass = _serviceConfig["RabbitMQ Password"].ToString();
                    var rabbitVHost = _serviceConfig["RabbitMQ VirtualHost"].ToString();

                    using (var queueService = new QueueService(rabbitHost, rabbitPort, rabbitUser, rabbitPass, rabbitVHost, _logger))
                    {
                        // Create job
                        Guid jobId = dbService.CreateJob(
                            "InteractionPermissionSync",
                            "K2 Broker",
                            Environment,
                            SiteUrl,
                            Priority ?? "Medium");

                        // Create message with full hierarchy
                        Guid messageId = Guid.NewGuid();
                        var message = new
                        {
                            MessageId = messageId,
                            JobId = jobId,
                            OperationType = "InteractionPermissionSync",

                            // GUID identifiers
                            InteractionId = interactionGuid,
                            ProjectId = projectGuid,
                            EngagementId = engagementGuid,

                            // SharePoint folder IDs (non-nullable)
                            InteractionSharePointFolderId = interactionSharePointFolderId.Value,
                            ProjectSharePointFolderId = projectSharePointFolderId.Value,
                            EngagementSharePointFolderId = engagementSharePointFolderId.Value,

                            // Business identifiers (cleaned)
                            InteractionName = FormatInteractionName(interactionNumber, interactionName),
                            ProjectName = CleanFolderName(projectName),
                            EngagementName = CleanFolderName(engagementName),

                            // SharePoint configuration
                            SiteUrl = SiteUrl,
                            DocumentLibrary = "Documents",

                            // Permissions
                            InternalPermission = InternalPermission ?? "Read",
                            InternalUserEmails = ParseEmailList(InternalUserEmails),
                            ExternalPermission = ExternalPermission ?? string.Empty,
                            ExternalUserEmails = ParseEmailList(ExternalUserEmails),

                            QueuedAt = DateTime.UtcNow
                        };

                        // Create job item with payload
                        dbService.CreateJobItem(jobId, messageId, "InteractionPermissionSync", FormatInteractionName(interactionNumber, interactionName), message);

                        // Publish to queue
                        string queueName = _serviceConfig["Queue InteractionPermissions"].ToString();
                        int priorityValue = ConvertPriority(Priority ?? "Medium");
                        queueService.PublishMessage(queueName, message, priorityValue);

                        _logger.LogInformation($"Successfully queued InteractionPermissionSync for '{FormatInteractionName(interactionNumber, interactionName)}', Job {jobId}");

                        return new ShareSyncService
                        {
                            ErrorNumber = 0,
                            ErrorMessage = string.Empty,
                            JobId = jobId.ToString(),
                            MessageId = messageId.ToString()
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                string message = $"SyncInteractionPermissions failed for Interaction {InteractionId}. Error: {ex.Message}";
                _logger.LogError(ex, message);
                return new ShareSyncService
                {
                    ErrorNumber = _errorNumberService.GetErrorNumber(),
                    ErrorMessage = message,
                    JobId = string.Empty,
                    MessageId = string.Empty
                };
            }
        }

        /// <summary>
        /// Create a new interaction with folder structure and permissions
        /// </summary>
        [Method(
            "CreateInteraction",
            MethodType.Create,
            "Create Interaction",
            "Create a new interaction folder in SharePoint with permissions.",
            new[] { "InteractionId", "ProjectId", "EngagementId", "Environment", "SiteUrl" },
            new[] { "InteractionId", "ProjectId", "EngagementId", "Environment", "SiteUrl", "ProjectSubfolder", "InternalPermission", "InternalUserEmails", "ExternalPermission", "ExternalUserEmails", "Priority" },
            new[] { "ErrorNumber", "ErrorMessage", "JobId", "MessageId" })]
        public ShareSyncService CreateInteraction()
        {
            try
            {
                _logger.LogInformation($"Starting CreateInteraction for Interaction {InteractionId}");

                // Validate and parse GUIDs
                if (string.IsNullOrWhiteSpace(InteractionId) || !Guid.TryParse(InteractionId, out Guid interactionGuid))
                    throw new ArgumentException("InteractionId is required and must be a valid GUID");

                if (string.IsNullOrWhiteSpace(ProjectId) || !Guid.TryParse(ProjectId, out Guid projectGuid))
                    throw new ArgumentException("ProjectId is required and must be a valid GUID");

                if (string.IsNullOrWhiteSpace(EngagementId) || !Guid.TryParse(EngagementId, out Guid engagementGuid))
                    throw new ArgumentException("EngagementId is required and must be a valid GUID");

                if (string.IsNullOrWhiteSpace(Environment))
                    throw new ArgumentException("Environment is required (DEV, UAT, or PROD)");

                if (string.IsNullOrWhiteSpace(SiteUrl))
                    throw new ArgumentException("SiteUrl is required");

                // Create services
                var connectionString = _serviceConfig["SQL Connection String"].ToString();
                using (var dbService = new DatabaseService(connectionString, _logger))
                {
                    // Query database for entity details
                    var (interactionName, interactionNumber, interactionSharePointFolderId) = dbService.GetInteractionDetails(interactionGuid);
                    var (projectName, projectSharePointFolderId) = dbService.GetProjectDetails(projectGuid);
                    var (engagementName, engagementSharePointFolderId) = dbService.GetEngagementDetails(engagementGuid);

                    var rabbitHost = _serviceConfig["RabbitMQ Host"].ToString();
                    var rabbitPort = int.Parse(_serviceConfig["RabbitMQ Port"].ToString());
                    var rabbitUser = _serviceConfig["RabbitMQ Username"].ToString();
                    var rabbitPass = _serviceConfig["RabbitMQ Password"].ToString();
                    var rabbitVHost = _serviceConfig["RabbitMQ VirtualHost"].ToString();

                    using (var queueService = new QueueService(rabbitHost, rabbitPort, rabbitUser, rabbitPass, rabbitVHost, _logger))
                    {
                        // Create job
                        Guid jobId = dbService.CreateJob(
                            "InteractionCreation",
                            "K2 Broker",
                            Environment,
                            SiteUrl,
                            Priority ?? "Medium");

                        // Create message with full hierarchy
                        Guid messageId = Guid.NewGuid();
                        var message = new
                        {
                            MessageId = messageId,
                            JobId = jobId,
                            OperationType = "InteractionCreation",

                            // GUID identifiers
                            InteractionId = interactionGuid,
                            ProjectId = projectGuid,
                            EngagementId = engagementGuid,

                            // SharePoint folder IDs (nullable - may need creation)
                            EngagementSharePointFolderId = engagementSharePointFolderId,
                            ProjectSharePointFolderId = projectSharePointFolderId,
                            InteractionSharePointFolderId = interactionSharePointFolderId,

                            // Business identifiers (cleaned)
                            InteractionName = FormatInteractionName(interactionNumber, interactionName),
                            ProjectName = CleanFolderName(projectName),
                            EngagementName = CleanFolderName(engagementName),

                            // SharePoint configuration
                            SiteUrl = SiteUrl,
                            DocumentLibrary = "Documents",
                            ProjectSubfolder = ProjectSubfolder ?? string.Empty,
                            CreatedBy = "K2 Broker",

                            // Permissions
                            InternalPermission = InternalPermission ?? "Read",
                            InternalUserEmails = ParseEmailList(InternalUserEmails),
                            ExternalPermission = ExternalPermission ?? string.Empty,
                            ExternalUserEmails = ParseEmailList(ExternalUserEmails),

                            QueuedAt = DateTime.UtcNow
                        };

                        // Create job item with payload
                        dbService.CreateJobItem(jobId, messageId, "InteractionCreation", FormatInteractionName(interactionNumber, interactionName), message);

                        // Publish to queue
                        string queueName = _serviceConfig["Queue InteractionCreation"].ToString();
                        int priorityValue = ConvertPriority(Priority ?? "Medium");
                        queueService.PublishMessage(queueName, message, priorityValue);

                        _logger.LogInformation($"Successfully queued InteractionCreation for '{FormatInteractionName(interactionNumber, interactionName)}', Job {jobId}");

                        return new ShareSyncService
                        {
                            ErrorNumber = 0,
                            ErrorMessage = string.Empty,
                            JobId = jobId.ToString(),
                            MessageId = messageId.ToString()
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                string message = $"CreateInteraction failed for Interaction {InteractionId}. Error: {ex.Message}";
                _logger.LogError(ex, message);
                return new ShareSyncService
                {
                    ErrorNumber = _errorNumberService.GetErrorNumber(),
                    ErrorMessage = message,
                    JobId = string.Empty,
                    MessageId = string.Empty
                };
            }
        }

        /// <summary>
        /// Get the status of a processing job
        /// </summary>
        [Method(
            "GetJobStatus",
            MethodType.Read,
            "Get Job Status",
            "Retrieve the current status of a processing job.",
            new[] { "JobId" },
            new[] { "JobId" },
            new[] { "ErrorNumber", "ErrorMessage", "JobId", "Status" })]
        public ShareSyncService GetJobStatus()
        {
            try
            {
                _logger.LogInformation($"Getting status for Job {JobId}");

                if (string.IsNullOrWhiteSpace(JobId))
                    throw new ArgumentException("JobId is required");

                if (!Guid.TryParse(JobId, out Guid jobGuid))
                    throw new ArgumentException("JobId must be a valid GUID");

                var connectionString = _serviceConfig["SQL Connection String"].ToString();
                using (var dbService = new DatabaseService(connectionString, _logger))
                {
                    string status = dbService.GetJobStatus(jobGuid);

                    _logger.LogInformation($"Job {JobId} status: {status}");

                    return new ShareSyncService
                    {
                        ErrorNumber = 0,
                        ErrorMessage = string.Empty,
                        JobId = JobId,
                        Status = status
                    };
                }
            }
            catch (Exception ex)
            {
                string message = $"GetJobStatus failed for Job {JobId}. Error: {ex.Message}";
                _logger.LogError(ex, message);
                return new ShareSyncService
                {
                    ErrorNumber = _errorNumberService.GetErrorNumber(),
                    ErrorMessage = message,
                    JobId = JobId,
                    Status = "Error"
                };
            }
        }

        #endregion

        #region Properties

        // Common properties
        [Property("ErrorNumber", SoType.Number, "Error Number", "Error code. 0 indicates success.")]
        public int ErrorNumber { get; set; }

        [Property("ErrorMessage", SoType.Text, "Error Message", "Error message if operation failed.")]
        public string ErrorMessage { get; set; }

        [Property("JobId", SoType.Text, "Job ID", "The unique identifier for the processing job.")]
        public string JobId { get; set; }

        [Property("MessageId", SoType.Text, "Message ID", "The unique identifier for the queued message.")]
        public string MessageId { get; set; }

        [Property("Status", SoType.Text, "Status", "The current status of the job.")]
        public string Status { get; set; }

        // Interaction properties (now using GUIDs as Text since K2 doesn't support Guid type)
        [Property("InteractionId", SoType.Text, "Interaction ID", "The GUID of the interaction in the database.")]
        public string InteractionId { get; set; }

        [Property("InteractionName", SoType.Text, "Interaction Name", "The name of the interaction to create.")]
        public string InteractionName { get; set; }

        [Property("ProjectId", SoType.Text, "Project ID", "The GUID of the project.")]
        public string ProjectId { get; set; }

        [Property("EngagementId", SoType.Text, "Engagement ID", "The GUID of the engagement.")]
        public string EngagementId { get; set; }

        [Property("SharePointFolderId", SoType.Number, "SharePoint Folder ID", "The SharePoint folder ID (if known).")]
        public int SharePointFolderId { get; set; }

        // Environment and site
        [Property("Environment", SoType.Text, "Environment", "Target environment (DEV, UAT, PROD).")]
        public string Environment { get; set; }

        [Property("SiteUrl", SoType.Text, "Site URL", "The SharePoint site URL.")]
        public string SiteUrl { get; set; }

        [Property("ProjectSubfolder", SoType.Text, "Project Subfolder", "Optional project subfolder path.")]
        public string ProjectSubfolder { get; set; }

        // Permissions
        [Property("InternalPermission", SoType.Text, "Internal Permission", "Permission level for internal users (Read, Contribute, Edit, etc.).")]
        public string InternalPermission { get; set; }

        [Property("InternalUserEmails", SoType.Memo, "Internal User Emails", "Semicolon-separated list of internal user emails.")]
        public string InternalUserEmails { get; set; }

        [Property("ExternalPermission", SoType.Text, "External Permission", "Permission level for external users.")]
        public string ExternalPermission { get; set; }

        [Property("ExternalUserEmails", SoType.Memo, "External User Emails", "Semicolon-separated list of external user emails.")]
        public string ExternalUserEmails { get; set; }

        [Property("Priority", SoType.Text, "Priority", "Job priority (Low, Medium, High, Critical).")]
        public string Priority { get; set; }

        #endregion

        #region Helper Methods

        private List<string> ParseEmailList(string emails)
        {
            if (string.IsNullOrWhiteSpace(emails))
                return new List<string>();

            var emailList = new List<string>();
            foreach (var email in emails.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = email.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                    emailList.Add(trimmed);
            }
            return emailList;
        }

        private int ConvertPriority(string priority)
        {
            switch (priority?.ToUpper())
            {
                case "LOW":
                    return 3;
                case "MEDIUM":
                    return 5;
                case "HIGH":
                    return 7;
                case "CRITICAL":
                    return 10;
                default:
                    return 5;
            }
        }

        private string CleanFolderName(string name, int maxLength = 256)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            // Remove illegal SharePoint characters: \ / : * ? " < > | # { } % ~ &
            char[] illegalChars = new[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|', '#', '{', '}', '%', '~', '&' };
            string cleaned = name;
            foreach (char illegalChar in illegalChars)
            {
                cleaned = cleaned.Replace(illegalChar.ToString(), string.Empty);
            }

            // Replace multiple spaces with single space
            while (cleaned.Contains("  "))
            {
                cleaned = cleaned.Replace("  ", " ");
            }

            // Trim and enforce max length
            cleaned = cleaned.Trim();
            if (cleaned.Length > maxLength)
            {
                cleaned = cleaned.Substring(0, maxLength).TrimEnd();
            }

            return cleaned;
        }

        private string FormatInteractionName(int? interactionNumber, string name, int paddingWidth = 5)
        {
            string cleanedName = CleanFolderName(name);

            if (!interactionNumber.HasValue || interactionNumber.Value <= 0)
                return cleanedName;

            string paddedNumber = interactionNumber.Value.ToString().PadLeft(paddingWidth, '0');
            return string.IsNullOrWhiteSpace(cleanedName)
                ? paddedNumber
                : $"{paddedNumber} - {cleanedName}";
        }

        #endregion
    }
}
