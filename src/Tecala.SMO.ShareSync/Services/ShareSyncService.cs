using Newtonsoft.Json;
using SourceCode.SmartObjects.Services.ServiceSDK;
using SourceCode.SmartObjects.Services.ServiceSDK.Attributes;
using SourceCode.SmartObjects.Services.ServiceSDK.Objects;
using SourceCode.SmartObjects.Services.ServiceSDK.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Tecala.SMO.ShareSync.Models;

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
            new[] { "InteractionId", "Environment", "UploadedBy" },
            new[] { "InteractionId", "Environment", "UploadedBy", "InternalPermission", "InternalUserEmails", "ExternalPermission", "ExternalUserEmails", "Priority" },
            new[] { "ErrorNumber", "ErrorMessage", "JobId", "MessageId" })]
        public ShareSyncService SyncInteractionPermissions()
        {
            try
            {
                _logger.LogInformation($"Starting SyncInteractionPermissions for Interaction {InteractionId}");

                // Validate and parse InteractionId
                if (string.IsNullOrWhiteSpace(InteractionId) || !Guid.TryParse(InteractionId, out Guid interactionGuid))
                    throw new ArgumentException("InteractionId is required and must be a valid GUID");

                if (string.IsNullOrWhiteSpace(Environment))
                    throw new ArgumentException("Environment is required (DEV, UAT, or PROD)");

                // Create services
                var connectionString = _serviceConfig["SQL Connection String"].ToString();
                using (var dbService = new DatabaseService(connectionString, _logger))
                {
                    // Query database for full hierarchy (includes ProjectId, EngagementId, SiteUrl)
                    var (interactionId, interactionName, interactionNumber, interactionSharePointFolderId,
                         projectId, projectName, projectSharePointFolderId,
                         engagementId, engagementName, engagementSharePointFolderId, siteUrl) =
                        dbService.GetInteractionHierarchy(interactionGuid);

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
                            UploadedBy,
                            Environment,
                            siteUrl ?? string.Empty,
                            Priority ?? "Medium");

                        // Create message with full hierarchy
                        Guid messageId = Guid.NewGuid();
                        var message = new
                        {
                            MessageId = messageId,
                            JobId = jobId,
                            OperationType = "InteractionPermissionSync",

                            // GUID identifiers
                            InteractionId = interactionId,
                            ProjectId = projectId,
                            EngagementId = engagementId,

                            // SharePoint folder IDs (non-nullable)
                            InteractionSharePointFolderId = interactionSharePointFolderId.Value,
                            ProjectSharePointFolderId = projectSharePointFolderId.Value,
                            EngagementSharePointFolderId = engagementSharePointFolderId.Value,

                            // Business identifiers (cleaned)
                            InteractionName = FormatInteractionName(interactionNumber, interactionName),
                            ProjectName = CleanFolderName(projectName),
                            EngagementName = CleanFolderName(engagementName),

                            // SharePoint configuration
                            SiteUrl = siteUrl ?? string.Empty,
                            DocumentLibrary = "Documents",

                            // We need the person who created the message
                            CreatedBy = UploadedBy,

                            // Permissions
                            InternalPermission = InternalPermission ?? "Read",
                            InternalUserEmails = ParseEmailList(InternalUserEmails),
                            ExternalPermission = ExternalPermission ?? string.Empty,
                            ExternalUserEmails = ParseEmailList(ExternalUserEmails),

                            QueuedAt = DateTime.UtcNow
                        };

                        // Create job item with payload
                        string itemIdentifier = $"{CleanFolderName(engagementName)} | {CleanFolderName(projectName)} | {FormatInteractionName(interactionNumber, interactionName)}";
                        dbService.CreateJobItem(jobId, messageId, "InteractionPermissionSync", itemIdentifier, message);

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
            new[] { "InteractionId", "Environment", "UploadedBy" },
            new[] { "InteractionId", "Environment", "UploadedBy", "ProjectSubfolder", "InternalPermission", "InternalUserEmails", "ExternalPermission", "ExternalUserEmails", "Priority" },
            new[] { "ErrorNumber", "ErrorMessage", "JobId", "MessageId" })]
        public ShareSyncService CreateInteraction()
        {
            try
            {
                _logger.LogInformation($"Starting CreateInteraction for Interaction {InteractionId}");

                // Validate and parse InteractionId
                if (string.IsNullOrWhiteSpace(InteractionId) || !Guid.TryParse(InteractionId, out Guid interactionGuid))
                    throw new ArgumentException("InteractionId is required and must be a valid GUID");

                if (string.IsNullOrWhiteSpace(Environment))
                    throw new ArgumentException("Environment is required (DEV, UAT, or PROD)");

                // Create services
                var connectionString = _serviceConfig["SQL Connection String"].ToString();
                using (var dbService = new DatabaseService(connectionString, _logger))
                {
                    // Query database for full hierarchy (includes ProjectId, EngagementId, SiteUrl)
                    var (interactionId, interactionName, interactionNumber, interactionSharePointFolderId,
                         projectId, projectName, projectSharePointFolderId,
                         engagementId, engagementName, engagementSharePointFolderId, siteUrl) =
                        dbService.GetInteractionHierarchy(interactionGuid);

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
                            UploadedBy,
                            Environment,
                            siteUrl ?? string.Empty,
                            Priority ?? "Medium");

                        // Create message with full hierarchy
                        Guid messageId = Guid.NewGuid();
                        var message = new
                        {
                            MessageId = messageId,
                            JobId = jobId,
                            OperationType = "InteractionCreation",

                            // GUID identifiers
                            InteractionId = interactionId,
                            ProjectId = projectId,
                            EngagementId = engagementId,

                            // SharePoint folder IDs (nullable - may need creation)
                            EngagementSharePointFolderId = engagementSharePointFolderId,
                            ProjectSharePointFolderId = projectSharePointFolderId,
                            InteractionSharePointFolderId = interactionSharePointFolderId,

                            // Business identifiers (cleaned)
                            InteractionName = FormatInteractionName(interactionNumber, interactionName),
                            ProjectName = CleanFolderName(projectName),
                            EngagementName = CleanFolderName(engagementName),

                            // SharePoint configuration
                            SiteUrl = siteUrl ?? string.Empty,
                            DocumentLibrary = "Documents",
                            ProjectSubfolder = ProjectSubfolder ?? string.Empty,
                            CreatedBy = UploadedBy,

                            // Permissions
                            InternalPermission = InternalPermission ?? "Read",
                            InternalUserEmails = ParseEmailList(InternalUserEmails),
                            ExternalPermission = ExternalPermission ?? string.Empty,
                            ExternalUserEmails = ParseEmailList(ExternalUserEmails),

                            QueuedAt = DateTime.UtcNow
                        };

                        // Create job item with payload
                        string itemIdentifier = $"{CleanFolderName(engagementName)} | {CleanFolderName(projectName)} | {FormatInteractionName(interactionNumber, interactionName)}";
                        dbService.CreateJobItem(jobId, messageId, "InteractionCreation", itemIdentifier, message);

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
        /// Create multiple interactions from a CSV file
        /// </summary>
        [Method(
            "CreateInteractionBulk",
            MethodType.Create,
            "Create Interactions Bulk",
            "Create multiple interaction folders in SharePoint from a CSV file.",
            new[] { "CsvFile", "Environment", "UploadedBy" },
            new[] { "CsvFile", "Environment", "UploadedBy", "Priority" },
            new[] { "ErrorNumber", "ErrorMessage", "JobId", "ItemCount" })]
        public ShareSyncService CreateInteractionBulk()
        {
            try
            {
                _logger.LogInformation("Starting CreateInteractionBulk");

                // Validate required parameters
                if (string.IsNullOrWhiteSpace(CsvFile))
                    throw new ArgumentException("CsvFile is required");

                if (string.IsNullOrWhiteSpace(Environment))
                    throw new ArgumentException("Environment is required (DEV, UAT, or PROD)");

                // Parse K2 file XML
                var (fileName, fileContent) = ParseK2File(CsvFile);
                _logger.LogInformation($"Processing file: {fileName}");

                // Parse CSV content
                var interactions = ParseCsvInteractions(fileContent);
                if (interactions.Count == 0)
                    throw new ArgumentException("CSV file contains no valid interaction records");

                _logger.LogInformation($"Found {interactions.Count} interactions in CSV");

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
                        // Create single job for bulk operation
                        Guid jobId = dbService.CreateJob(
                            "InteractionCreation",
                            UploadedBy,
                            Environment,
                            string.Empty, // No single site URL for bulk
                            Priority ?? "Medium");

                        string queueName = _serviceConfig["Queue InteractionCreation"].ToString();
                        int priorityValue = ConvertPriority(Priority ?? "Medium");
                        int itemCount = 0;

                        // Process each interaction
                        foreach (var interaction in interactions)
                        {
                            try
                            {
                                // Validate and parse InteractionId
                                if (!Guid.TryParse(interaction.InteractionId, out Guid interactionGuid))
                                    throw new ArgumentException($"Invalid InteractionId GUID: {interaction.InteractionId}");

                                // Query database for full hierarchy
                                var (interactionId, interactionName, interactionNumber, interactionSharePointFolderId,
                                     projectId, projectName, projectSharePointFolderId,
                                     engagementId, engagementName, engagementSharePointFolderId, siteUrl) =
                                    dbService.GetInteractionHierarchy(interactionGuid);

                                // Create message
                                Guid messageId = Guid.NewGuid();
                                var message = new
                                {
                                    MessageId = messageId,
                                    JobId = jobId,
                                    OperationType = "InteractionCreation",

                                    // GUID identifiers
                                    InteractionId = interactionId,
                                    ProjectId = projectId,
                                    EngagementId = engagementId,

                                    // SharePoint folder IDs
                                    EngagementSharePointFolderId = engagementSharePointFolderId,
                                    ProjectSharePointFolderId = projectSharePointFolderId,
                                    InteractionSharePointFolderId = interactionSharePointFolderId,

                                    // Business identifiers
                                    InteractionName = FormatInteractionName(interactionNumber, interactionName),
                                    ProjectName = CleanFolderName(projectName),
                                    EngagementName = CleanFolderName(engagementName),

                                    // SharePoint configuration
                                    SiteUrl = siteUrl ?? string.Empty,
                                    DocumentLibrary = "Documents",
                                    ProjectSubfolder = interaction.ProjectSubfolder ?? string.Empty,
                                    CreatedBy = UploadedBy,

                                    // Permissions
                                    InternalPermission = interaction.InternalPermission ?? "Read",
                                    InternalUserEmails = ParseEmailList(interaction.InternalUserEmails),
                                    ExternalPermission = interaction.ExternalPermission ?? string.Empty,
                                    ExternalUserEmails = ParseEmailList(interaction.ExternalUserEmails),

                                    QueuedAt = DateTime.UtcNow
                                };

                                // Create job item
                                string itemIdentifier = $"{CleanFolderName(engagementName)} | {CleanFolderName(projectName)} | {FormatInteractionName(interactionNumber, interactionName)}";
                                dbService.CreateJobItem(jobId, messageId, "InteractionCreation", itemIdentifier, message);

                                // Publish to queue
                                queueService.PublishMessage(queueName, message, priorityValue);

                                itemCount++;
                                _logger.LogInformation($"Queued interaction {itemCount}/{interactions.Count}: {itemIdentifier}");
                            }
                            catch (Exception ex)
                            {
                                // If ANY interaction fails, rollback and fail the whole job
                                throw new Exception($"Failed processing interaction '{interaction.InteractionId}': {ex.Message}", ex);
                            }
                        }

                        _logger.LogInformation($"Successfully queued {itemCount} interactions for Job {jobId}");

                        return new ShareSyncService
                        {
                            ErrorNumber = 0,
                            ErrorMessage = string.Empty,
                            JobId = jobId.ToString(),
                            ItemCount = itemCount.ToString()
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                string message = $"CreateInteractionBulk failed. Error: {ex.Message}";
                _logger.LogError(ex, message);
                return new ShareSyncService
                {
                    ErrorNumber = _errorNumberService.GetErrorNumber(),
                    ErrorMessage = message,
                    JobId = string.Empty,
                    ItemCount = "0"
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

        /// <summary>
        /// Stop a running job
        /// </summary>
        [Method(
            "StopJob",
            MethodType.Execute,
            "Stop Job",
            "Cancel a running job and all its pending items.",
            new[] { "JobId" },
            new[] { "JobId" },
            new[] { "ErrorNumber", "ErrorMessage", "JobId", "Status" })]
        public ShareSyncService StopJob()
        {
            try
            {
                _logger.LogInformation($"Cancelling Job {JobId}");

                if (string.IsNullOrWhiteSpace(JobId))
                    throw new ArgumentException("JobId is required");

                if (!Guid.TryParse(JobId, out Guid jobGuid))
                    throw new ArgumentException("JobId must be a valid GUID");

                var connectionString = _serviceConfig["SQL Connection String"].ToString();
                using (var dbService = new DatabaseService(connectionString, _logger))
                {
                    dbService.StopJob(jobGuid);

                    _logger.LogInformation($"Successfully cancelled Job {JobId}");

                    return new ShareSyncService
                    {
                        ErrorNumber = 0,
                        ErrorMessage = string.Empty,
                        JobId = JobId,
                        Status = "Cancelled"
                    };
                }
            }
            catch (Exception ex)
            {
                string message = $"StopJob failed for Job {JobId}. Error: {ex.Message}";
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

        [Property("CsvFile", SoType.File, "CSV File", "CSV file containing interaction details to create.")]
        public string CsvFile { get; set; }

        [Property("UploadedBy", SoType.Text, "Uploaded By", "The username that uploaded the document.")]
        public string UploadedBy { get; set; }

        [Property("ItemCount", SoType.Text, "Item Count", "Number of interactions successfully queued.")]
        public string ItemCount { get; set; }

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
            if (!interactionNumber.HasValue || interactionNumber.Value <= 0)
                return CleanFolderName(name);

            return $"Interaction{interactionNumber.Value}-R";
        }

        // Helper method to parse K2 file XML
        private (string fileName, string fileContent) ParseK2File(string fileXml)
        {
            try
            {
                var doc = XDocument.Parse(fileXml);
                var fileName = doc.Root?.Element("name")?.Value;
                var contentBase64 = doc.Root?.Element("content")?.Value;

                if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(contentBase64))
                    throw new ArgumentException("Invalid K2 file format");

                byte[] contentBytes = Convert.FromBase64String(contentBase64);
                string fileContent = Encoding.UTF8.GetString(contentBytes);

                return (fileName, fileContent);
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Failed to parse K2 file XML: {ex.Message}", ex);
            }
        }

        // Helper method to parse CSV content
        private List<CsvInteractionRow> ParseCsvInteractions(string csvContent)
        {
            var interactions = new List<CsvInteractionRow>();

            try
            {
                var lines = csvContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

                if (lines.Length < 2)
                    throw new ArgumentException("CSV file must contain at least a header row and one data row");

                // Parse header to find column indices
                var headers = lines[0].Split(',').Select(h => h.Trim()).ToArray();
                var columnMap = new Dictionary<string, int>();

                for (int i = 0; i < headers.Length; i++)
                {
                    columnMap[headers[i]] = i;
                }

                // Validate required columns
                if (!columnMap.ContainsKey("InteractionId"))
                    throw new ArgumentException("CSV must contain 'InteractionId' column");

                // Parse data rows
                for (int i = 1; i < lines.Length; i++)
                {
                    var values = lines[i].Split(',').Select(v => v.Trim()).ToArray();

                    var row = new CsvInteractionRow
                    {
                        InteractionId = GetColumnValue(values, columnMap, "InteractionId"),
                        ProjectSubfolder = GetColumnValue(values, columnMap, "ProjectSubfolder"),
                        InternalPermission = GetColumnValue(values, columnMap, "InternalPermission"),
                        InternalUserEmails = GetColumnValue(values, columnMap, "InternalUserEmails"),
                        ExternalPermission = GetColumnValue(values, columnMap, "ExternalPermission"),
                        ExternalUserEmails = GetColumnValue(values, columnMap, "ExternalUserEmails")
                    };

                    // Validate required field
                    if (string.IsNullOrWhiteSpace(row.InteractionId))
                        throw new ArgumentException($"Row {i + 1}: InteractionId is required");

                    interactions.Add(row);
                }

                return interactions;
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Failed to parse CSV content: {ex.Message}", ex);
            }
        }

        // Helper to safely get column value
        private string GetColumnValue(string[] values, Dictionary<string, int> columnMap, string columnName)
        {
            if (columnMap.TryGetValue(columnName, out int index) && index < values.Length)
                return values[index];
            return string.Empty;
        }

        #endregion
    }
}
