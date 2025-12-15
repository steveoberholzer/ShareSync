-- Create ProcessingJobs table
CREATE TABLE [ScyneShare].[ProcessingJobs] (
    [Id] INT IDENTITY(1,1) PRIMARY KEY,
    [JobId] UNIQUEIDENTIFIER NOT NULL UNIQUE,
    [JobType] NVARCHAR(50) NOT NULL,
    [FileName] NVARCHAR(255) NULL,
    [UploadedBy] NVARCHAR(100) NULL,
    [Environment] NVARCHAR(10) NULL,
    [SiteUrl] NVARCHAR(500) NULL,
    [TotalItems] INT NOT NULL DEFAULT 0,
    [ProcessedItems] INT NOT NULL DEFAULT 0,
    [FailedItems] INT NOT NULL DEFAULT 0,
    [Status] NVARCHAR(20) NOT NULL DEFAULT 'Queued',
    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [StartedAt] DATETIME2 NULL,
    [CompletedAt] DATETIME2 NULL,
    [ErrorMessage] NVARCHAR(MAX) NULL
);

CREATE INDEX IX_ProcessingJobs_JobId ON [ScyneShare].[ProcessingJobs]([JobId]);
CREATE INDEX IX_ProcessingJobs_Status ON [ScyneShare].[ProcessingJobs]([Status]);
CREATE INDEX IX_ProcessingJobs_CreatedAt ON [ScyneShare].[ProcessingJobs]([CreatedAt] DESC);

-- Create ProcessingJobItems table
CREATE TABLE [ScyneShare].[ProcessingJobItems] (
    [Id] INT IDENTITY(1,1) PRIMARY KEY,
    [JobId] UNIQUEIDENTIFIER NOT NULL,
    [MessageId] UNIQUEIDENTIFIER NOT NULL UNIQUE,
    [ItemType] NVARCHAR(50) NULL,
    [ItemIdentifier] NVARCHAR(255) NULL,
    [Payload] NVARCHAR(MAX) NULL,
    [Status] NVARCHAR(20) NOT NULL DEFAULT 'Pending',
    [RetryCount] INT NOT NULL DEFAULT 0,
    [MaxRetries] INT NOT NULL DEFAULT 3,
    [ErrorMessage] NVARCHAR(MAX) NULL,
    [ProcessedAt] DATETIME2 NULL,
    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_ProcessingJobItems_ProcessingJobs
        FOREIGN KEY ([JobId]) REFERENCES [ScyneShare].[ProcessingJobs]([JobId])
        ON DELETE CASCADE
);

CREATE INDEX IX_ProcessingJobItems_JobId ON [ScyneShare].[ProcessingJobItems]([JobId]);
CREATE INDEX IX_ProcessingJobItems_MessageId ON [ScyneShare].[ProcessingJobItems]([MessageId]);
CREATE INDEX IX_ProcessingJobItems_Status ON [ScyneShare].[ProcessingJobItems]([Status]);
