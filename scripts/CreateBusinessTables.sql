-- =============================================
-- SharePoint Permission Sync - Business Tables
-- =============================================
-- These tables exist in PRODUCTION and should be created manually in DEV for testing.
-- DO NOT run this script against PRODUCTION - these tables already exist there!
-- =============================================

-- Ensure schema exists
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'ScyneShare')
BEGIN
    EXEC('CREATE SCHEMA ScyneShare');
END
GO

-- =============================================
-- Table: Engagement
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Engagement' AND schema_id = SCHEMA_ID('ScyneShare'))
BEGIN
    CREATE TABLE [ScyneShare].[Engagement] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        [Name] NVARCHAR(200) NULL,
        [SiteURL] NVARCHAR(255) NULL,
        [ClientId] INT NULL,
        [DueDate] DATETIME2 NULL,
        [SharePointFolderID] INT NULL,
        [StatusId] INT NULL,
        [IsNotWonEngagement] BIT NULL,
        [SharePointFolderName] NVARCHAR(200) NULL,

        -- Base entity fields
        [IsActive] BIT NULL,
        [CreatedOn] DATETIME2 NULL,
        [CreatedBy] NVARCHAR(200) NULL,
        [CreatedByFQN] NVARCHAR(200) NULL,
        [ModifiedOn] DATETIME2 NULL,
        [ModifiedBy] NVARCHAR(200) NULL,
        [ModifiedByFQN] NVARCHAR(200) NULL,
        [ModifiedOnTimeStamp] NVARCHAR(30) NULL
    );

    PRINT 'Created table ScyneShare.Engagement';
END
ELSE
BEGIN
    PRINT 'Table ScyneShare.Engagement already exists - skipping';
END
GO

-- =============================================
-- Table: Project
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Project' AND schema_id = SCHEMA_ID('ScyneShare'))
BEGIN
    CREATE TABLE [ScyneShare].[Project] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        [EngagementId] UNIQUEIDENTIFIER NULL,
        [Name] NVARCHAR(100) NULL,
        [Description] NVARCHAR(MAX) NULL,
        [ExpectedEndDate] DATETIME2 NULL,
        [StatusId] INT NULL,
        [SharePointFolderID] INT NULL,
        [SharePointFolderName] NVARCHAR(200) NULL,

        -- Base entity fields
        [IsActive] BIT NULL,
        [CreatedOn] DATETIME2 NULL,
        [CreatedBy] NVARCHAR(200) NULL,
        [CreatedByFQN] NVARCHAR(200) NULL,
        [ModifiedOn] DATETIME2 NULL,
        [ModifiedBy] NVARCHAR(200) NULL,
        [ModifiedByFQN] NVARCHAR(200) NULL,
        [ModifiedOnTimeStamp] NVARCHAR(30) NULL,

        CONSTRAINT FK_Project_Engagement FOREIGN KEY ([EngagementId])
            REFERENCES [ScyneShare].[Engagement]([Id]) ON DELETE NO ACTION
    );

    CREATE INDEX IX_Project_EngagementId ON [ScyneShare].[Project]([EngagementId]);

    PRINT 'Created table ScyneShare.Project';
END
ELSE
BEGIN
    PRINT 'Table ScyneShare.Project already exists - skipping';
END
GO

-- =============================================
-- Table: Interaction
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Interaction' AND schema_id = SCHEMA_ID('ScyneShare'))
BEGIN
    CREATE TABLE [ScyneShare].[Interaction] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        [ParentId] UNIQUEIDENTIFIER NULL,
        [InteractionTypeId] INT NULL,
        [EngagementId] UNIQUEIDENTIFIER NULL,
        [ProjectId] UNIQUEIDENTIFIER NULL,
        [InteractionNumber] INT NULL,
        [Name] NVARCHAR(500) NULL,
        [Description] NVARCHAR(MAX) NULL,
        [IsPrivate] BIT NULL,
        [DueDate] DATETIME2 NULL,
        [LatestReleaseDate] DATETIME2 NULL,
        [LatestSubmissionDate] DATETIME2 NULL,
        [LatestReturnDate] DATETIME2 NULL,
        [SharePointFolderID] INT NULL,
        [StatusId] INT NULL,
        [SharePointFolderName] NVARCHAR(500) NULL,
        [SharePointFileCount] INT NULL,
        [StatusLastChangedOn] DATETIME2 NULL,
        [StatusLastChangedBy] NVARCHAR(200) NULL,
        [StatusLastChangedByFQN] NVARCHAR(200) NULL,
        [ActivatedOn] DATETIME2 NULL,
        [InternalUsers] NVARCHAR(MAX) NULL,
        [ExternalUsers] NVARCHAR(MAX) NULL,

        -- Base entity fields
        [IsActive] BIT NULL,
        [CreatedOn] DATETIME2 NULL,
        [CreatedBy] NVARCHAR(200) NULL,
        [CreatedByFQN] NVARCHAR(200) NULL,
        [ModifiedOn] DATETIME2 NULL,
        [ModifiedBy] NVARCHAR(200) NULL,
        [ModifiedByFQN] NVARCHAR(200) NULL,
        [ModifiedOnTimeStamp] NVARCHAR(30) NULL,

        CONSTRAINT FK_Interaction_Engagement FOREIGN KEY ([EngagementId])
            REFERENCES [ScyneShare].[Engagement]([Id]) ON DELETE NO ACTION,
        CONSTRAINT FK_Interaction_Project FOREIGN KEY ([ProjectId])
            REFERENCES [ScyneShare].[Project]([Id]) ON DELETE NO ACTION
    );

    CREATE INDEX IX_Interaction_EngagementId ON [ScyneShare].[Interaction]([EngagementId]);
    CREATE INDEX IX_Interaction_ProjectId ON [ScyneShare].[Interaction]([ProjectId]);
    CREATE INDEX IX_Interaction_SharePointFolderID ON [ScyneShare].[Interaction]([SharePointFolderID]);

    PRINT 'Created table ScyneShare.Interaction';
END
ELSE
BEGIN
    PRINT 'Table ScyneShare.Interaction already exists - skipping';
END
GO

-- =============================================
-- Table: InteractionMembership
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'InteractionMembership' AND schema_id = SCHEMA_ID('ScyneShare'))
BEGIN
    CREATE TABLE [ScyneShare].[InteractionMembership] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        [EngagementMembershipId] UNIQUEIDENTIFIER NULL,
        [InteractionId] UNIQUEIDENTIFIER NULL,
        [InteractionRoleTypeId] INT NULL,
        [IsPrimary] BIT NULL,
        [IsActive] BIT NULL,
        [CreatedOn] DATETIME2 NULL,
        [CreatedBy] NVARCHAR(200) NULL,
        [CreatedByFQN] NVARCHAR(200) NULL,
        [ModifiedOn] DATETIME2 NULL,
        [ModifiedBy] NVARCHAR(200) NULL,
        [ModifiedByFQN] NVARCHAR(200) NULL,
        [MemberId] UNIQUEIDENTIFIER NULL,

        CONSTRAINT FK_InteractionMembership_Interaction FOREIGN KEY ([InteractionId])
            REFERENCES [ScyneShare].[Interaction]([Id]) ON DELETE CASCADE
    );

    CREATE INDEX IX_InteractionMembership_InteractionId ON [ScyneShare].[InteractionMembership]([InteractionId]);
    CREATE INDEX IX_InteractionMembership_MemberId ON [ScyneShare].[InteractionMembership]([MemberId]);

    PRINT 'Created table ScyneShare.InteractionMembership';
END
ELSE
BEGIN
    PRINT 'Table ScyneShare.InteractionMembership already exists - skipping';
END
GO

PRINT '';
PRINT '==============================================';
PRINT 'Business tables creation complete!';
PRINT '==============================================';
PRINT 'Created tables (if they did not exist):';
PRINT '  - ScyneShare.Engagement';
PRINT '  - ScyneShare.Project';
PRINT '  - ScyneShare.Interaction';
PRINT '  - ScyneShare.InteractionMembership';
PRINT '';
PRINT 'NOTE: These tables are EXCLUDED from EF Core migrations.';
PRINT 'They are mapped for querying only.';
PRINT '==============================================';
GO
