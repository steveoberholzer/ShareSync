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

                // Validate required parameters
                if (InteractionId <= 0 || ProjectId <= 0 || EngagementId <= 0)
                    throw new ArgumentException("InteractionId, ProjectId, and EngagementId are required");

                if (string.IsNullOrWhiteSpace(Environment))
                    throw new ArgumentException("Environment is required (DEV, UAT, or PROD)");

                if (string.IsNullOrWhiteSpace(SiteUrl))
                    throw new ArgumentException("SiteUrl is required");

                // Create services
                var connectionString = _serviceConfig["SQL Connection String"].ToString();
                using (var dbService = new DatabaseService(connectionString, _logger))
                {
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

                        // Create message
                        Guid messageId = Guid.NewGuid();
                        var message = new
                        {
                            MessageId = messageId,
                            JobId = jobId,
                            OperationType = "InteractionPermissionSync",
                            InteractionId = InteractionId,
                            ProjectId = ProjectId,
                            EngagementId = EngagementId,
                            SiteUrl = SiteUrl,
                            DocumentLibrary = "Documents",
                            SharePointFolderId = SharePointFolderId > 0 ? SharePointFolderId : (int?)null,
                            InternalPermission = InternalPermission ?? "Read",
                            InternalUserEmails = ParseEmailList(InternalUserEmails),
                            ExternalPermission = ExternalPermission ?? string.Empty,
                            ExternalUserEmails = ParseEmailList(ExternalUserEmails),
                            QueuedAt = DateTime.UtcNow
                        };

                        // Create job item
                        dbService.CreateJobItem(jobId, messageId, "InteractionPermissionSync", $"Interaction-{InteractionId}");

                        // Publish to queue
                        string queueName = _serviceConfig["Queue InteractionPermissions"].ToString();
                        int priorityValue = ConvertPriority(Priority ?? "Medium");
                        queueService.PublishMessage(queueName, message, priorityValue);

                        _logger.LogInformation($"Successfully queued InteractionPermissionSync for Interaction {InteractionId}, Job {jobId}");

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
            new[] { "InteractionName", "ProjectId", "EngagementId", "Environment", "SiteUrl" },
            new[] { "InteractionName", "ProjectId", "EngagementId", "Environment", "SiteUrl", "ProjectSubfolder", "InternalPermission", "InternalUserEmails", "ExternalPermission", "ExternalUserEmails", "Priority" },
            new[] { "ErrorNumber", "ErrorMessage", "JobId", "MessageId" })]
        public ShareSyncService CreateInteraction()
        {
            try
            {
                _logger.LogInformation($"Starting CreateInteraction for '{InteractionName}'");

                // Validate required parameters
                if (string.IsNullOrWhiteSpace(InteractionName))
                    throw new ArgumentException("InteractionName is required");

                if (ProjectId <= 0 || EngagementId <= 0)
                    throw new ArgumentException("ProjectId and EngagementId are required");

                if (string.IsNullOrWhiteSpace(Environment))
                    throw new ArgumentException("Environment is required (DEV, UAT, or PROD)");

                if (string.IsNullOrWhiteSpace(SiteUrl))
                    throw new ArgumentException("SiteUrl is required");

                // Create services
                var connectionString = _serviceConfig["SQL Connection String"].ToString();
                using (var dbService = new DatabaseService(connectionString, _logger))
                {
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

                        // Create message
                        Guid messageId = Guid.NewGuid();
                        var message = new
                        {
                            MessageId = messageId,
                            JobId = jobId,
                            OperationType = "InteractionCreation",
                            InteractionName = InteractionName,
                            ProjectId = ProjectId,
                            EngagementId = EngagementId,
                            SiteUrl = SiteUrl,
                            DocumentLibrary = "Documents",
                            ProjectSubfolder = ProjectSubfolder ?? string.Empty,
                            CreatedBy = "K2 Broker",
                            InternalPermission = InternalPermission ?? "Read",
                            InternalUserEmails = ParseEmailList(InternalUserEmails),
                            ExternalPermission = ExternalPermission ?? string.Empty,
                            ExternalUserEmails = ParseEmailList(ExternalUserEmails),
                            QueuedAt = DateTime.UtcNow
                        };

                        // Create job item
                        dbService.CreateJobItem(jobId, messageId, "InteractionCreation", InteractionName);

                        // Publish to queue
                        string queueName = _serviceConfig["Queue InteractionCreation"].ToString();
                        int priorityValue = ConvertPriority(Priority ?? "Medium");
                        queueService.PublishMessage(queueName, message, priorityValue);

                        _logger.LogInformation($"Successfully queued InteractionCreation for '{InteractionName}', Job {jobId}");

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
                string message = $"CreateInteraction failed for '{InteractionName}'. Error: {ex.Message}";
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

        // Interaction properties
        [Property("InteractionId", SoType.Number, "Interaction ID", "The ID of the interaction in the database.")]
        public int InteractionId { get; set; }

        [Property("InteractionName", SoType.Text, "Interaction Name", "The name of the interaction to create.")]
        public string InteractionName { get; set; }

        [Property("ProjectId", SoType.Number, "Project ID", "The ID of the project.")]
        public int ProjectId { get; set; }

        [Property("EngagementId", SoType.Number, "Engagement ID", "The ID of the engagement.")]
        public int EngagementId { get; set; }

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

        #endregion
    }
}
