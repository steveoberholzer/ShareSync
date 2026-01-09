using Newtonsoft.Json;
using System;
using System.Data;
using System.Data.SqlClient;

namespace Tecala.SMO.ShareSync.Services
{
    /// <summary>
    /// Service for database operations
    /// </summary>
    public class DatabaseService : IDisposable
    {
        private readonly string _connectionString;
        private readonly ILogger _logger;
        private SqlConnection _connection;

        public DatabaseService(string connectionString, ILogger logger)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        /// <summary>
        /// Create a new processing job in the database
        /// </summary>
        public Guid CreateJob(string jobType, string uploadedBy, string environment, string siteUrl, string priority, int totalItems = 0)
        {
            Guid jobId = Guid.NewGuid();

            try
            {
                EnsureConnection();

                string sql = @"
                    INSERT INTO ScyneShare.ProcessingJobs
                        (JobId, JobType, UploadedBy, Environment, SiteUrl, Priority, Status, TotalItems, ProcessedItems, FailedItems, CreatedAt)
                    VALUES
                        (@JobId, @JobType, @UploadedBy, @Environment, @SiteUrl, @Priority, @Status, @TotalItems, @ProcessedItems, @FailedItems, @CreatedAt)";

                using (var cmd = new SqlCommand(sql, _connection))
                {
                    cmd.Parameters.AddWithValue("@JobId", jobId);
                    cmd.Parameters.AddWithValue("@JobType", jobType);
                    cmd.Parameters.AddWithValue("@UploadedBy", uploadedBy ?? "K2 Broker");
                    cmd.Parameters.AddWithValue("@Environment", environment);
                    cmd.Parameters.AddWithValue("@SiteUrl", siteUrl ?? string.Empty);
                    cmd.Parameters.AddWithValue("@Priority", priority ?? "Medium");
                    cmd.Parameters.AddWithValue("@Status", "Queued");
                    cmd.Parameters.AddWithValue("@TotalItems", totalItems);
                    cmd.Parameters.AddWithValue("@ProcessedItems", 0);
                    cmd.Parameters.AddWithValue("@FailedItems", 0);
                    cmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);

                    cmd.ExecuteNonQuery();
                }

                _logger.LogInformation($"Created job {jobId} of type {jobType}");
                return jobId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to create job of type {jobType}");
                throw;
            }
        }

        /// <summary>
        /// Create a job item for tracking individual messages
        /// </summary>
        public void CreateJobItem(Guid jobId, Guid messageId, string itemType, string itemIdentifier, object payload = null)
        {
            try
            {
                EnsureConnection();

                // Serialize payload to JSON if provided
                string payloadJson = null;
                if (payload != null)
                {
                    payloadJson = JsonConvert.SerializeObject(payload, Formatting.Indented);
                }

                string sql = @"
                    INSERT INTO ScyneShare.ProcessingJobItems
                        (JobId, MessageId, ItemType, ItemIdentifier, Payload, Status, CreatedAt)
                    VALUES
                        (@JobId, @MessageId, @ItemType, @ItemIdentifier, @Payload, @Status, @CreatedAt)";

                using (var cmd = new SqlCommand(sql, _connection))
                {
                    cmd.Parameters.AddWithValue("@JobId", jobId);
                    cmd.Parameters.AddWithValue("@MessageId", messageId);
                    cmd.Parameters.AddWithValue("@ItemType", itemType);
                    cmd.Parameters.AddWithValue("@ItemIdentifier", itemIdentifier ?? string.Empty);
                    cmd.Parameters.AddWithValue("@Payload", (object)payloadJson ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Status", "Pending");
                    cmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);

                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to create job item for message {messageId}");
                throw;
            }
        }

        /// <summary>
        /// Get job status by Job ID
        /// </summary>
        public string GetJobStatus(Guid jobId)
        {
            try
            {
                EnsureConnection();

                string sql = "SELECT Status FROM ScyneShare.ProcessingJobs WHERE JobId = @JobId";

                using (var cmd = new SqlCommand(sql, _connection))
                {
                    cmd.Parameters.AddWithValue("@JobId", jobId);
                    var result = cmd.ExecuteScalar();
                    return result?.ToString() ?? "Unknown";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to get job status for {jobId}");
                throw;
            }
        }

        /// <summary>
        /// Stop a running job by updating its status to Cancelled
        /// Cancels all pending job items (items already processing will complete normally)
        /// </summary>
        public void StopJob(Guid jobId)
        {
            try
            {
                EnsureConnection();

                // First, update all pending job items to Cancelled (let processing items complete)
                string updateItemsSql = @"
                    UPDATE ScyneShare.ProcessingJobItems
                    SET Status = @Status
                    WHERE JobId = @JobId
                    AND Status = 'Pending'";

                using (var cmd = new SqlCommand(updateItemsSql, _connection))
                {
                    cmd.Parameters.AddWithValue("@JobId", jobId);
                    cmd.Parameters.AddWithValue("@Status", "Cancelled");

                    int itemsAffected = cmd.ExecuteNonQuery();
                    _logger.LogInformation($"Cancelled {itemsAffected} pending job items for job {jobId}");
                }

                // Then, update the job itself to Cancelled
                string updateJobSql = @"
                    UPDATE ScyneShare.ProcessingJobs
                    SET Status = @Status, CompletedAt = @CompletedAt
                    WHERE JobId = @JobId";

                using (var cmd = new SqlCommand(updateJobSql, _connection))
                {
                    cmd.Parameters.AddWithValue("@JobId", jobId);
                    cmd.Parameters.AddWithValue("@Status", "Cancelled");
                    cmd.Parameters.AddWithValue("@CompletedAt", DateTime.UtcNow);

                    int rowsAffected = cmd.ExecuteNonQuery();

                    if (rowsAffected == 0)
                    {
                        throw new Exception($"Job {jobId} not found");
                    }
                }

                _logger.LogInformation($"Cancelled job {jobId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to cancel job {jobId}");
                throw;
            }
        }

        /// <summary>
        /// Update the total items count for a job
        /// </summary>
        public void UpdateJobTotalItems(Guid jobId, int totalItems)
        {
            try
            {
                EnsureConnection();

                string sql = @"
                    UPDATE ScyneShare.ProcessingJobs
                    SET TotalItems = @TotalItems
                    WHERE JobId = @JobId";

                using (var cmd = new SqlCommand(sql, _connection))
                {
                    cmd.Parameters.AddWithValue("@JobId", jobId);
                    cmd.Parameters.AddWithValue("@TotalItems", totalItems);

                    int rowsAffected = cmd.ExecuteNonQuery();

                    if (rowsAffected == 0)
                    {
                        throw new Exception($"Job {jobId} not found");
                    }
                }

                _logger.LogInformation($"Updated total items for job {jobId} to {totalItems}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to update total items for job {jobId}");
                throw;
            }
        }

        /// <summary>
        /// Get interaction details by ID
        /// </summary>
        public (string Name, int? InteractionNumber, int? SharePointFolderID) GetInteractionDetails(Guid interactionId)
        {
            try
            {
                EnsureConnection();

                string sql = @"
                    SELECT Name, InteractionNumber, SharePointFolderID
                    FROM ScyneShare.Interaction
                    WHERE Id = @Id";

                using (var cmd = new SqlCommand(sql, _connection))
                {
                    cmd.Parameters.AddWithValue("@Id", interactionId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return (
                                reader["Name"]?.ToString(),
                                reader["InteractionNumber"] as int?,
                                reader["SharePointFolderID"] as int?
                            );
                        }
                    }
                }

                throw new Exception($"Interaction {interactionId} not found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to get interaction details for {interactionId}");
                throw;
            }
        }

        /// <summary>
        /// Get project details by ID
        /// </summary>
        public (string Name, int? SharePointFolderID) GetProjectDetails(Guid projectId)
        {
            try
            {
                EnsureConnection();

                string sql = @"
                    SELECT Name, SharePointFolderID
                    FROM ScyneShare.Project
                    WHERE Id = @Id";

                using (var cmd = new SqlCommand(sql, _connection))
                {
                    cmd.Parameters.AddWithValue("@Id", projectId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return (
                                reader["Name"]?.ToString(),
                                reader["SharePointFolderID"] as int?
                            );
                        }
                    }
                }

                throw new Exception($"Project {projectId} not found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to get project details for {projectId}");
                throw;
            }
        }

        /// <summary>
        /// Get full interaction hierarchy (Interaction, Project, Engagement) from just the InteractionId
        /// </summary>
        public (
            Guid InteractionId, string InteractionName, int? InteractionNumber, int? InteractionSharePointFolderId,
            Guid ProjectId, string ProjectName, int? ProjectSharePointFolderId,
            Guid EngagementId, string EngagementName, int? EngagementSharePointFolderId, string SiteUrl
        ) GetInteractionHierarchy(Guid interactionId)
        {
            try
            {
                EnsureConnection();

                string sql = @"
                    SELECT
                        i.Id AS InteractionId,
                        i.Name AS InteractionName,
                        i.InteractionNumber,
                        i.SharePointFolderID AS InteractionSharePointFolderId,
                        i.ProjectId,
                        p.Name AS ProjectName,
                        p.SharePointFolderID AS ProjectSharePointFolderId,
                        i.EngagementId,
                        e.Name AS EngagementName,
                        e.SharePointFolderID AS EngagementSharePointFolderId,
                        e.SiteUrl
                    FROM ScyneShare.Interaction i
                    INNER JOIN ScyneShare.Project p ON i.ProjectId = p.Id
                    INNER JOIN ScyneShare.Engagement e ON i.EngagementId = e.Id
                    WHERE i.Id = @Id";

                using (var cmd = new SqlCommand(sql, _connection))
                {
                    cmd.Parameters.AddWithValue("@Id", interactionId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return (
                                (Guid)reader["InteractionId"],
                                reader["InteractionName"]?.ToString(),
                                reader["InteractionNumber"] as int?,
                                reader["InteractionSharePointFolderId"] as int?,
                                (Guid)reader["ProjectId"],
                                reader["ProjectName"]?.ToString(),
                                reader["ProjectSharePointFolderId"] as int?,
                                (Guid)reader["EngagementId"],
                                reader["EngagementName"]?.ToString(),
                                reader["EngagementSharePointFolderId"] as int?,
                                reader["SiteUrl"]?.ToString()
                            );
                        }
                    }
                }

                throw new Exception($"Interaction {interactionId} or its hierarchy not found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to get interaction hierarchy for {interactionId}");
                throw;
            }
        }

        /// <summary>
        /// Get engagement details by ID
        /// </summary>
        public (string Name, int? SharePointFolderID, string SiteUrl) GetEngagementDetails(Guid engagementId)
        {
            try
            {
                EnsureConnection();

                string sql = @"
                    SELECT Name, SharePointFolderID, SiteUrl
                    FROM ScyneShare.Engagement
                    WHERE Id = @Id";

                using (var cmd = new SqlCommand(sql, _connection))
                {
                    cmd.Parameters.AddWithValue("@Id", engagementId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return (
                                reader["Name"]?.ToString(),
                                reader["SharePointFolderID"] as int?,
                                reader["SiteUrl"]?.ToString()
                            );
                        }
                    }
                }

                throw new Exception($"Engagement {engagementId} not found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to get engagement details for {engagementId}");
                throw;
            }
        }

        private void EnsureConnection()
        {
            if (_connection == null)
            {
                _connection = new SqlConnection(_connectionString);
            }

            if (_connection.State != ConnectionState.Open)
            {
                _connection.Open();
            }
        }

        public void Dispose()
        {
            _connection?.Dispose();
        }
    }
}
