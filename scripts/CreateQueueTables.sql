-- =============================================
-- SharePoint Permission Sync - Queue System Tables
-- =============================================
-- These are NEW tables for the queue processing system.
-- This script can be run manually OR you can use EF Core migrations.
-- =============================================

-- Ensure schema exists
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'ScyneShare')
BEGIN
    EXEC('CREATE SCHEMA ScyneShare');
END
GO

-- =============================================
-- Table: ProcessingJobs
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ProcessingJobs' AND schema_id = SCHEMA_ID('ScyneShare'))
BEGIN
    CREATE TABLE [ScyneShare].[ProcessingJobs] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [JobId] UNIQUEIDENTIFIER NOT NULL,
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
        [ErrorMessage] NVARCHAR(MAX) NULL,

        CONSTRAINT UQ_ProcessingJobs_JobId UNIQUE ([JobId])
    );

    -- Indexes for performance
    CREATE INDEX IX_ProcessingJobs_JobId ON [ScyneShare].[ProcessingJobs]([JobId]);
    CREATE INDEX IX_ProcessingJobs_Status ON [ScyneShare].[ProcessingJobs]([Status]);
    CREATE INDEX IX_ProcessingJobs_CreatedAt ON [ScyneShare].[ProcessingJobs]([CreatedAt] DESC);

    PRINT 'Created table ScyneShare.ProcessingJobs';
END
ELSE
BEGIN
    PRINT 'Table ScyneShare.ProcessingJobs already exists - skipping';
END
GO

-- =============================================
-- Table: ProcessingJobItems
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ProcessingJobItems' AND schema_id = SCHEMA_ID('ScyneShare'))
BEGIN
    CREATE TABLE [ScyneShare].[ProcessingJobItems] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [JobId] UNIQUEIDENTIFIER NOT NULL,
        [MessageId] UNIQUEIDENTIFIER NOT NULL,
        [ItemType] NVARCHAR(50) NULL,
        [ItemIdentifier] NVARCHAR(255) NULL,
        [Payload] NVARCHAR(MAX) NULL,
        [Status] NVARCHAR(20) NOT NULL DEFAULT 'Pending',
        [RetryCount] INT NOT NULL DEFAULT 0,
        [MaxRetries] INT NOT NULL DEFAULT 3,
        [ErrorMessage] NVARCHAR(MAX) NULL,
        [ProcessedAt] DATETIME2 NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

        CONSTRAINT UQ_ProcessingJobItems_MessageId UNIQUE ([MessageId]),
        CONSTRAINT FK_ProcessingJobItems_ProcessingJobs FOREIGN KEY ([JobId])
            REFERENCES [ScyneShare].[ProcessingJobs]([JobId]) ON DELETE CASCADE
    );

    -- Indexes for performance
    CREATE INDEX IX_ProcessingJobItems_JobId ON [ScyneShare].[ProcessingJobItems]([JobId]);
    CREATE INDEX IX_ProcessingJobItems_MessageId ON [ScyneShare].[ProcessingJobItems]([MessageId]);
    CREATE INDEX IX_ProcessingJobItems_Status ON [ScyneShare].[ProcessingJobItems]([Status]);

    PRINT 'Created table ScyneShare.ProcessingJobItems';
END
ELSE
BEGIN
    PRINT 'Table ScyneShare.ProcessingJobItems already exists - skipping';
END
GO

PRINT '';
PRINT '==============================================';
PRINT 'Queue system tables creation complete!';
PRINT '==============================================';
PRINT 'Created tables (if they did not exist):';
PRINT '  - ScyneShare.ProcessingJobs';
PRINT '  - ScyneShare.ProcessingJobItems';
PRINT '';
PRINT 'These tables are used to track CSV upload jobs';
PRINT 'and individual processing items in the queue.';
PRINT '==============================================';
GO
