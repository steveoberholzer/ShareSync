-- Migration: AddPriorityColumn
-- Date: 2025-01-05
-- Description: Adds Priority column to ProcessingJobs table with index

USE ScyneShareDEV;
GO

-- Check if column already exists before adding
IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'ScyneShare'
    AND TABLE_NAME = 'ProcessingJobs'
    AND COLUMN_NAME = 'Priority'
)
BEGIN
    -- Add Priority column
    ALTER TABLE [ScyneShare].[ProcessingJobs]
    ADD [Priority] NVARCHAR(10) NOT NULL DEFAULT 'Medium';

    PRINT 'Priority column added successfully';
END
ELSE
BEGIN
    PRINT 'Priority column already exists';
END
GO

-- Check if index already exists before creating
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_ProcessingJobs_Priority'
    AND object_id = OBJECT_ID('[ScyneShare].[ProcessingJobs]')
)
BEGIN
    -- Create index on Priority column
    CREATE NONCLUSTERED INDEX [IX_ProcessingJobs_Priority]
    ON [ScyneShare].[ProcessingJobs]([Priority]);

    PRINT 'Index on Priority column created successfully';
END
ELSE
BEGIN
    PRINT 'Index on Priority column already exists';
END
GO

PRINT 'Migration completed successfully';
GO
