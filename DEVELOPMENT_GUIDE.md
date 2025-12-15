# SharePoint Permission Sync System - Development Guide

## Project Overview

A distributed system to replace unreliable K2 workflows for managing SharePoint permissions and interaction creation. Uses message queue pattern for controlled, resilient processing.

## Business Problem

**Current State:**
- K2 workflows apply SharePoint permissions/create interactions
- Multiple simultaneous executions cause SharePoint throttling (429 errors)
- Workflows fail, permissions go out of sync
- K2 server gets overwhelmed (1000+ concurrent instances)
- Manual CSV-based fixes using WPF app (local only)

**Target State:**
- Web-based portal accessible to team
- CSV upload for bulk operations
- Message queue for controlled processing
- Resilient retry logic
- Real-time progress visibility
- Audit trail of all operations

## System Architecture

```
┌──────────────────────────────────────────────────────────────┐
│                        Web Portal                             │
│  - CSV Upload & Validation                                    │
│  - Queue Status Dashboard                                     │
│  - Real-time Progress (SignalR)                              │
│  - Environment Selector (DEV/UAT/PROD)                       │
└─────────────────────┬────────────────────────────────────────┘
                      │
                      ▼
┌──────────────────────────────────────────────────────────────┐
│                      RabbitMQ Queue                           │
│  - Interaction Permission Updates                             │
│  - Interaction Creation                                       │
│  - Dead Letter Queue for failures                            │
└─────────────────────┬────────────────────────────────────────┘
                      │
                      ▼
┌──────────────────────────────────────────────────────────────┐
│                    Worker Service(s)                          │
│  - FIFO Processing                                            │
│  - Configurable Throttling (500ms-2s delays)                 │
│  - Exponential Backoff Retry                                 │
│  - Detailed Logging                                           │
│  - Health Check Endpoint                                      │
└─────────────────────┬────────────────────────────────────────┘
                      │
                      ▼
┌──────────────────────────────────────────────────────────────┐
│              SharePoint Online + SQL Server                   │
│  - Apply Folder Permissions (CSOM)                           │
│  - Create Interactions                                        │
│  - Update SQL Database                                        │
└──────────────────────────────────────────────────────────────┘
```

## Technology Stack

### Core Technologies
- **.NET 8** (ASP.NET Core MVC + Worker Service)
- **RabbitMQ 3.13+** (Message broker)
- **SQL Server** (Existing ScyneShare database)
- **SignalR** (Real-time updates)
- **Entity Framework Core 8** (Data access)

### SharePoint Integration
- **Tecala.SMO.SharePoint** - **EXISTING PRODUCTION LIBRARY** (>1 year in production)
  - Already tested and proven
  - Contains SharePoint CSOM operations
  - Handles authentication (MSAL certificate-based)
  - Contains K2 SmartObject service broker implementation
  - **MUST BE REUSED** - do not recreate SharePoint communication layer
- **Microsoft.SharePoint.Client (CSOM)** - Already referenced by Tecala.SMO.SharePoint
- **Microsoft.Identity.Client (MSAL)** - Already referenced by Tecala.SMO.SharePoint
- **PnP.Framework** - Already referenced by Tecala.SMO.SharePoint

### Supporting Libraries
- **CsvHelper** - CSV parsing
- **Serilog** - Structured logging
- **Polly** - Retry policies
- **RabbitMQ.Client** - Message queue client

## Data Models

### Domain Entities

#### Engagement → Project → Interaction Hierarchy
```
Engagement (Id)
  └─ Projects (Many, EngagementId FK)
       └─ Interactions (Many, ProjectId FK)
```

**Permission Inheritance:**
- Engagement: Has unique permissions
- Project: Inherits from parent Engagement
- Interaction: Has unique permissions (specific users)

### Message Queue Models

#### Base Message
```csharp
public abstract class QueueMessage
{
    public Guid MessageId { get; set; }
    public string OperationType { get; set; }
    public DateTime EnqueuedAt { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; } = 3;
}
```

#### Interaction Permission Message
```csharp
public class InteractionPermissionMessage : QueueMessage
{
    public int InteractionId { get; set; }
    public int ProjectId { get; set; }
    public string SiteUrl { get; set; }
    public string DocumentLibrary { get; set; }
    public int FolderId { get; set; }
    public List<PermissionEntry> Permissions { get; set; }
}

public class PermissionEntry
{
    public string UserEmail { get; set; }
    public string Role { get; set; } // "Read", "Contribute", "FullControl"
}
```

#### Interaction Creation Message
```csharp
public class InteractionCreationMessage : QueueMessage
{
    public string InteractionName { get; set; }
    public int ProjectId { get; set; }
    public int EngagementId { get; set; }
    public string SiteUrl { get; set; }
    public string DocumentLibrary { get; set; }
    public string CreatedBy { get; set; }
    public List<PermissionEntry> Permissions { get; set; }
}
```

### Database Models

#### ProcessingJob (Tracking Table)
```sql
CREATE TABLE ProcessingJobs (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    JobId UNIQUEIDENTIFIER NOT NULL,
    JobType NVARCHAR(50) NOT NULL, -- 'PermissionSync', 'InteractionCreation'
    FileName NVARCHAR(255),
    UploadedBy NVARCHAR(100),
    Environment NVARCHAR(10), -- 'DEV', 'UAT', 'PROD'
    TotalItems INT NOT NULL,
    ProcessedItems INT DEFAULT 0,
    FailedItems INT DEFAULT 0,
    Status NVARCHAR(20), -- 'Queued', 'Processing', 'Completed', 'Failed'
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    CompletedAt DATETIME2 NULL,
    CONSTRAINT UQ_JobId UNIQUE (JobId)
);
```

#### ProcessingJobItem (Detail Tracking)
```sql
CREATE TABLE ProcessingJobItems (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    JobId UNIQUEIDENTIFIER NOT NULL,
    MessageId UNIQUEIDENTIFIER NOT NULL,
    ItemType NVARCHAR(50), -- 'InteractionPermission', 'InteractionCreation'
    ItemIdentifier NVARCHAR(255), -- InteractionId or name
    Status NVARCHAR(20), -- 'Pending', 'Processing', 'Completed', 'Failed'
    RetryCount INT DEFAULT 0,
    ErrorMessage NVARCHAR(MAX) NULL,
    ProcessedAt DATETIME2 NULL,
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    FOREIGN KEY (JobId) REFERENCES ProcessingJobs(JobId)
);
```

## CSV File Formats

### Interaction Permissions CSV
```csv
InteractionId,ProjectId,SiteUrl,DocumentLibrary,FolderId,UserEmail,Role
12345,678,https://scyneadvisory.sharepoint.com,Documents,98765,user1@domain.com,Read
12345,678,https://scyneadvisory.sharepoint.com,Documents,98765,user2@domain.com,Contribute
12346,679,https://scyneadvisory.sharepoint.com,Documents,98766,user3@domain.com,Read
```

### Interaction Creation CSV
```csv
InteractionName,ProjectId,EngagementId,SiteUrl,DocumentLibrary,CreatedBy,UserEmail,Role
"Q4 2024 Review",678,123,https://scyneadvisory.sharepoint.com,Documents,steven.oberholzer@domain.com,user1@domain.com,Read
"Q4 2024 Review",678,123,https://scyneadvisory.sharepoint.com,Documents,steven.oberholzer@domain.com,user2@domain.com,Contribute
"Annual Audit",679,124,https://scyneadvisory.sharepoint.com,Documents,steven.oberholzer@domain.com,user3@domain.com,Read
```

## Existing SharePoint Service Library API

### Available Methods (from Tecala.SMO.SharePoint)

#### Interaction Operations
```csharp
// Create new interaction with folder structure
NewInteraction(
    SiteUrl,
    DocumentLibrary,
    ProjectID,
    InteractionName,
    ProjectSubfolder,
    InternalPermission,
    ListOfInternalEmailAddresses,
    ExternalPermission,
    ListOfExternalEmailAddresses
) -> SharePointService { ID, ErrorNumber, ErrorMessage }

// Change interaction permissions
PermissionChangeInteraction(
    SiteUrl,
    DocumentLibrary,
    InteractionID,
    InternalPermission,
    ListOfInternalEmailAddresses,
    ExternalPermission,
    ListOfExternalEmailAddresses
) -> SharePointService { ErrorNumber, ErrorMessage }

// Close interaction (remove permissions)
CloseInteraction(
    SiteUrl,
    DocumentLibrary,
    InteractionID
) -> SharePointService { ErrorNumber, ErrorMessage }
```

#### Project Operations
```csharp
// Create new project
NewProject(
    SiteUrl,
    DocumentLibrary,
    EngagementID,
    ProjectName,
    EngagementSubfolder
) -> SharePointService { ID, ErrorNumber, ErrorMessage }

// Change project permissions
PermissionChangeProject(
    SiteUrl,
    DocumentLibrary,
    ProjectID,
    InternalPermission,
    ListOfInternalEmailAddresses,
    ExternalPermission,
    ListOfExternalEmailAddresses
) -> SharePointService { ErrorNumber, ErrorMessage }
```

#### Engagement Operations
```csharp
// Create new engagement
NewEngagement(
    SiteUrl,
    DocumentLibrary,
    EngagementName,
    InternalPermission,
    ListOfInternalEmailAddresses,
    ExternalPermission,
    ListOfExternalEmailAddresses
) -> SharePointService { ID, ErrorNumber, ErrorMessage }

// Change engagement permissions
PermissionChangeEngagement(
    SiteUrl,
    DocumentLibrary,
    EngagementID,
    InternalPermission,
    ListOfInternalEmailAddresses,
    ExternalPermission,
    ListOfExternalEmailAddresses
) -> SharePointService { ErrorNumber, ErrorMessage }
```

#### Utility Operations
```csharp
// Upload file to SharePoint
UploadFileToSharePointFolder(
    SiteUrl,
    DocumentLibrary,
    ID, // Folder ID
    File // File content
) -> SharePointService { ID, ErrorNumber, ErrorMessage }

// Get folder item count
GetFolderItemCount(
    SiteUrl,
    DocumentLibrary,
    ID
) -> SharePointService { ItemCount, ErrorNumber, ErrorMessage }
```

### Helper Extension Methods (Available)
```csharp
// String extensions
string.MapToSharePointPermission() // "Read", "Contribute", "Full Control", "Restricted View"
string.CleanupEmailAddressString() // Validates and formats emails
string.SanitizeFolderName() // Removes invalid characters
string.EnsureEmptySharePointFolderPath() // Normalizes folder paths

// Permission mapping examples:
"Read" -> "Read"
"Edit" -> "Contribute"  
"Full" -> "Full Control"
"No Access" -> "None"
```

### Configuration Requirements (ServiceConfiguration)
```csharp
var config = new ServiceConfiguration();
config.Add("Entra-TenantId", false, "your-tenant-id");
config.Add("Entra-ClientId", false, "your-client-id");
config.Add("Entra-CertificateThumbprint", false, "cert-thumbprint");
config.Add("SharePoint-TenantName", false, "scyneadvisory");
config.Add("InteractionFolderTemplateJSON", false, "{ \"Finding Documents\": {}, \"Response Documents\": {}, \"Template\": {}, \"Utility\": {} }");
config.Add("ProjectFolderTemplateJSON", false, "{ \"Template\": {}, \"Utility\": {} }");
config.Add("EngagementFolderTemplateJSON", false, "{ \"Template\": {}, \"Utility\": {} }");
config.Add("ScyneDB-ConnectionString", false, "connection-string");
```

### When to Use Broker vs. Direct CSOM

**Use SharePoint Service Broker for:**
- ✅ Creating Interactions (`NewInteraction`)
- ✅ Creating Projects (`NewProject`)  
- ✅ Creating Engagements (`NewEngagement`)
- ✅ Applying permissions to Interactions (`PermissionChangeInteraction`)
- ✅ Applying permissions to Projects (`PermissionChangeProject`)
- ✅ Applying permissions to Engagements (`PermissionChangeEngagement`)
- ✅ Removing permissions (`CloseInteraction`, `CloseProject`, `CloseEngagement`)
- ✅ File uploads (`UploadFileToSharePointFolder`)

**Use Direct CSOM for:**
- ⚠️ Removing unique permissions (inherit from parent) - not in broker
- ⚠️ Validating folder existence by ID - not in broker
- ⚠️ Bulk permission resets across multiple folders - needs custom logic
- ⚠️ Querying subfolder permissions - needs custom traversal

## Database Schema (ScyneShare)

### Core Tables

#### Engagement
```sql
CREATE TABLE [ScyneShare].[Engagement] (
    [Id] INT IDENTITY(1,1) PRIMARY KEY,
    [EngagementGUID] UNIQUEIDENTIFIER NOT NULL,
    [Name] NVARCHAR(500),
    [SharePointSiteUrl] NVARCHAR(500),
    [SharePointFolderID] INT,
    [CreatedDate] DATETIME2,
    [ModifiedDate] DATETIME2
);
```

#### Project
```sql
CREATE TABLE [ScyneShare].[Project] (
    [Id] INT IDENTITY(1,1) PRIMARY KEY,
    [ProjectGUID] UNIQUEIDENTIFIER NOT NULL,
    [EngagementId] INT FOREIGN KEY REFERENCES [ScyneShare].[Engagement](Id),
    [Name] NVARCHAR(500),
    [SharePointFolderID] INT,
    [CreatedDate] DATETIME2,
    [ModifiedDate] DATETIME2
);
```

#### Interaction
```sql
CREATE TABLE [ScyneShare].[Interaction] (
    [Id] INT IDENTITY(1,1) PRIMARY KEY,
    [InteractionGUID] UNIQUEIDENTIFIER NOT NULL,
    [ProjectId] INT FOREIGN KEY REFERENCES [ScyneShare].[Project](Id),
    [EngagementId] INT FOREIGN KEY REFERENCES [ScyneShare].[Engagement](Id),
    [Name] NVARCHAR(500),
    [SharePointFolderID] INT,
    [CreatedDate] DATETIME2,
    [ModifiedDate] DATETIME2
);
```

#### InteractionMembership (Permissions)
```sql
CREATE TABLE [ScyneShare].[InteractionMembership] (
    [Id] INT IDENTITY(1,1) PRIMARY KEY,
    [InteractionId] INT FOREIGN KEY REFERENCES [ScyneShare].[Interaction](Id),
    [UserEmail] NVARCHAR(255),
    [Permission] NVARCHAR(50), -- 'Read', 'Contribute', 'FullControl'
    [IsExternal] BIT,
    [CreatedDate] DATETIME2
);
```

### New Tables for Queue System

#### ProcessingJobs
```sql
CREATE TABLE [ScyneShare].[ProcessingJobs] (
    [Id] INT IDENTITY(1,1) PRIMARY KEY,
    [JobId] UNIQUEIDENTIFIER NOT NULL UNIQUE,
    [JobType] NVARCHAR(50) NOT NULL, -- 'InteractionPermissionSync', 'InteractionCreation', 'RemoveUniquePermissions'
    [FileName] NVARCHAR(255),
    [UploadedBy] NVARCHAR(100),
    [Environment] NVARCHAR(10), -- 'DEV', 'UAT', 'PROD'
    [SiteUrl] NVARCHAR(500),
    [TotalItems] INT NOT NULL DEFAULT 0,
    [ProcessedItems] INT DEFAULT 0,
    [FailedItems] INT DEFAULT 0,
    [Status] NVARCHAR(20) DEFAULT 'Queued', -- 'Queued', 'Processing', 'Completed', 'Failed', 'Paused'
    [CreatedAt] DATETIME2 DEFAULT GETDATE(),
    [StartedAt] DATETIME2 NULL,
    [CompletedAt] DATETIME2 NULL,
    [ErrorMessage] NVARCHAR(MAX) NULL
);

CREATE INDEX IX_ProcessingJobs_Status ON [ScyneShare].[ProcessingJobs]([Status]);
CREATE INDEX IX_ProcessingJobs_CreatedAt ON [ScyneShare].[ProcessingJobs]([CreatedAt] DESC);
```

#### ProcessingJobItems
```sql
CREATE TABLE [ScyneShare].[ProcessingJobItems] (
    [Id] INT IDENTITY(1,1) PRIMARY KEY,
    [JobId] UNIQUEIDENTIFIER NOT NULL,
    [MessageId] UNIQUEIDENTIFIER NOT NULL UNIQUE,
    [ItemType] NVARCHAR(50), -- 'InteractionPermission', 'InteractionCreation', 'RemoveUniquePermission'
    [ItemIdentifier] NVARCHAR(255), -- InteractionID, FolderID, etc.
    [Payload] NVARCHAR(MAX), -- JSON of message details
    [Status] NVARCHAR(20) DEFAULT 'Pending', -- 'Pending', 'Processing', 'Completed', 'Failed', 'Requeued'
    [RetryCount] INT DEFAULT 0,
    [MaxRetries] INT DEFAULT 3,
    [ErrorMessage] NVARCHAR(MAX) NULL,
    [ProcessedAt] DATETIME2 NULL,
    [CreatedAt] DATETIME2 DEFAULT GETDATE(),
    FOREIGN KEY ([JobId]) REFERENCES [ScyneShare].[ProcessingJobs]([JobId]) ON DELETE CASCADE
);

CREATE INDEX IX_ProcessingJobItems_JobId ON [ScyneShare].[ProcessingJobItems]([JobId]);
CREATE INDEX IX_ProcessingJobItems_Status ON [ScyneShare].[ProcessingJobItems]([Status]);
CREATE INDEX IX_ProcessingJobItems_MessageId ON [ScyneShare].[ProcessingJobItems]([MessageId]);
```

## Project Structure

```
SharePointPermissionSync/
│
├── src/
│   ├── SharePointPermissionSync.Web/          # ASP.NET Core MVC Web Portal
│   │   ├── Controllers/
│   │   │   ├── HomeController.cs
│   │   │   ├── JobsController.cs              # Job management & monitoring
│   │   │   └── OperationsController.cs        # CSV upload & queue submission
│   │   ├── Models/
│   │   │   ├── JobViewModel.cs
│   │   │   ├── CsvUploadViewModel.cs
│   │   │   └── JobStatusViewModel.cs
│   │   ├── Views/
│   │   │   ├── Home/Index.cshtml
│   │   │   ├── Jobs/
│   │   │   │   ├── Index.cshtml              # Job list dashboard
│   │   │   │   ├── Details.cshtml            # Job detail view
│   │   │   │   └── Monitor.cshtml            # Real-time monitoring
│   │   │   └── Operations/
│   │   │       ├── PermissionSync.cshtml     # Upload CSV for permission sync
│   │   │       ├── InteractionCreation.cshtml
│   │   │       └── RemovePermissions.cshtml
│   │   ├── Hubs/
│   │   │   └── JobProgressHub.cs             # SignalR hub for real-time updates
│   │   ├── Services/
│   │   │   ├── QueueService.cs               # RabbitMQ publishing
│   │   │   └── JobService.cs                 # Database job tracking
│   │   ├── wwwroot/
│   │   │   ├── css/
│   │   │   ├── js/
│   │   │   │   └── jobMonitor.js            # SignalR client
│   │   │   └── lib/
│   │   ├── appsettings.json
│   │   └── Program.cs
│   │
│   ├── SharePointPermissionSync.Worker/       # .NET Worker Service
│   │   ├── Workers/
│   │   │   └── QueueProcessorWorker.cs       # Main background worker
│   │   ├── Services/
│   │   │   ├── SharePointOperationService.cs # Wraps Tecala.SMO.SharePoint
│   │   │   ├── MessageProcessor.cs           # Processes queue messages
│   │   │   └── ThrottleManager.cs            # Adaptive throttling
│   │   ├── Handlers/
│   │   │   ├── IOperationHandler.cs
│   │   │   ├── InteractionPermissionHandler.cs
│   │   │   ├── InteractionCreationHandler.cs
│   │   │   ├── RemoveUniquePermissionHandler.cs
│   │   │   └── ValidateFolderHandler.cs
│   │   ├── appsettings.json
│   │   └── Program.cs
│   │
│   ├── SharePointPermissionSync.Core/         # Shared library
│   │   ├── Models/
│   │   │   ├── Messages/
│   │   │   │   ├── QueueMessageBase.cs
│   │   │   │   ├── InteractionPermissionMessage.cs
│   │   │   │   ├── InteractionCreationMessage.cs
│   │   │   │   ├── RemoveUniquePermissionMessage.cs
│   │   │   │   └── PermissionEntry.cs
│   │   │   ├── DTOs/
│   │   │   │   ├── JobDto.cs
│   │   │   │   └── JobItemDto.cs
│   │   │   └── Enums/
│   │   │       ├── JobStatus.cs
│   │   │       └── OperationType.cs
│   │   ├── Configuration/
│   │   │   ├── RabbitMqSettings.cs
│   │   │   ├── SharePointSettings.cs
│   │   │   └── EnvironmentSettings.cs
│   │   └── Constants/
│   │       ├── QueueNames.cs
│   │       └── PermissionLevels.cs
│   │
│   └── SharePointPermissionSync.Data/         # Entity Framework Core
│       ├── ScyneShareContext.cs
│       ├── Entities/
│       │   ├── Engagement.cs
│       │   ├── Project.cs
│       │   ├── Interaction.cs
│       │   ├── ProcessingJob.cs
│       │   └── ProcessingJobItem.cs
│       ├── Repositories/
│       │   ├── IJobRepository.cs
│       │   ├── JobRepository.cs
│       │   ├── IInteractionRepository.cs
│       │   └── InteractionRepository.cs
│       └── Migrations/
│
├── tests/
│   ├── SharePointPermissionSync.Tests/
│   └── SharePointPermissionSync.IntegrationTests/
│
├── docker/
│   ├── docker-compose.yml                     # RabbitMQ + Web + Worker
│   └── Dockerfile.worker
│
├── scripts/
│   ├── setup-rabbitmq.ps1
│   ├── deploy-worker-service.ps1
│   └── seed-test-data.sql
│
```

## Message Queue Structures

### Base Message Class
```csharp
public abstract class QueueMessageBase
{
    public Guid MessageId { get; set; } = Guid.NewGuid();
    public Guid JobId { get; set; }
    public string OperationType { get; set; }
    public DateTime EnqueuedAt { get; set; } = DateTime.UtcNow;
    public int RetryCount { get; set; } = 0;
    public int MaxRetries { get; set; } = 3;
    public string Environment { get; set; } // DEV, UAT, PROD
}
```

### 1. Interaction Permission Sync Message
```csharp
public class InteractionPermissionMessage : QueueMessageBase
{
    public int InteractionId { get; set; }
    public int ProjectId { get; set; }
    public int EngagementId { get; set; }
    public string SiteUrl { get; set; }
    public string DocumentLibrary { get; set; } = "Documents";
    public int SharePointFolderId { get; set; }
    
    public string InternalPermission { get; set; } // "Read", "Contribute", "Full Control"
    public List<string> InternalUserEmails { get; set; } = new();
    
    public string ExternalPermission { get; set; }
    public List<string> ExternalUserEmails { get; set; } = new();
}
```

**CSV Format:**
```csv
InteractionId,ProjectId,EngagementId,SiteUrl,SharePointFolderId,InternalPermission,InternalUsers,ExternalPermission,ExternalUsers
12345,678,123,https://scyneadvisory.sharepoint.com/sites/MySite,98765,Contribute,user1@domain.com;user2@domain.com,Read,external1@client.com
12346,679,124,https://scyneadvisory.sharepoint.com/sites/MySite,98766,Read,user3@domain.com,,
```

### 2. Interaction Creation Message
```csharp
public class InteractionCreationMessage : QueueMessageBase
{
    public string InteractionName { get; set; }
    public int ProjectId { get; set; }
    public int EngagementId { get; set; }
    public string SiteUrl { get; set; }
    public string DocumentLibrary { get; set; } = "Documents";
    public string ProjectSubfolder { get; set; } // Optional subfolder within project
    public string CreatedBy { get; set; }
    
    public string InternalPermission { get; set; }
    public List<string> InternalUserEmails { get; set; } = new();
    
    public string ExternalPermission { get; set; }
    public List<string> ExternalUserEmails { get; set; } = new();
    
    // Will be populated after successful creation
    public int? CreatedSharePointFolderId { get; set; }
}
```

**CSV Format:**
```csv
InteractionName,ProjectId,EngagementId,SiteUrl,ProjectSubfolder,CreatedBy,InternalPermission,InternalUsers,ExternalPermission,ExternalUsers
"Q4 2024 Review",678,123,https://scyneadvisory.sharepoint.com/sites/MySite,,steven.oberholzer@domain.com,Contribute,user1@domain.com;user2@domain.com,Read,external1@client.com
"Annual Audit 2025",679,124,https://scyneadvisory.sharepoint.com/sites/MySite,Audits,steven.oberholzer@domain.com,Read,user3@domain.com,,
```

### 3. Remove Unique Permissions Message
```csharp
public class RemoveUniquePermissionMessage : QueueMessageBase
{
    public string SiteUrl { get; set; }
    public string DocumentLibrary { get; set; } = "Documents";
    public int FolderId { get; set; }
    public string FolderType { get; set; } // "Interaction", "Project", "Engagement", "Folder"
}
```

**CSV Format:**
```csv
SiteUrl,FolderId,FolderType
https://scyneadvisory.sharepoint.com/sites/MySite,98765,Interaction
https://scyneadvisory.sharepoint.com/sites/MySite,98766,Folder
```

### 4. Validate Folder Existence Message
```csharp
public class ValidateFolderMessage : QueueMessageBase
{
    public string SiteUrl { get; set; }
    public string DocumentLibrary { get; set; } = "Documents";
    public List<int> FolderIds { get; set; } = new();
}
```

## Worker Service Implementation

### 1. SharePointOperationService (Wrapper for Tecala.SMO.SharePoint)

```csharp
public class SharePointOperationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SharePointOperationService> _logger;
    private ServiceConfiguration _serviceConfig;
    
    public SharePointOperationService(
        IConfiguration configuration,
        ILogger<SharePointOperationService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        InitializeServiceConfiguration();
    }
    
    private void InitializeServiceConfiguration()
    {
        var env = _configuration["Environment"]; // DEV, UAT, PROD
        
        _serviceConfig = new ServiceConfiguration();
        _serviceConfig.Add("Entra-TenantId", false, 
            _configuration[$"SharePoint:{env}:TenantId"]);
        _serviceConfig.Add("Entra-ClientId", false, 
            _configuration[$"SharePoint:{env}:ClientId"]);
        _serviceConfig.Add("Entra-CertificateThumbprint", false, 
            _configuration[$"SharePoint:{env}:CertificateThumbprint"]);
        _serviceConfig.Add("SharePoint-TenantName", false, 
            _configuration[$"SharePoint:{env}:TenantName"]);
        _serviceConfig.Add("InteractionFolderTemplateJSON", false, 
            _configuration["SharePoint:InteractionFolderTemplate"]);
        _serviceConfig.Add("ProjectFolderTemplateJSON", false, 
            _configuration["SharePoint:ProjectFolderTemplate"]);
        _serviceConfig.Add("EngagementFolderTemplateJSON", false, 
            _configuration["SharePoint:EngagementFolderTemplate"]);
        _serviceConfig.Add("ScyneDB-ConnectionString", false, 
            _configuration[$"ConnectionStrings:{env}"]);
    }
    
    public async Task<OperationResult> ApplyInteractionPermissions(
        InteractionPermissionMessage message)
    {
        try
        {
            _logger.LogInformation(
                "Applying permissions for Interaction {InteractionId}", 
                message.InteractionId);
            
            var service = new SharePointService
            {
                ServiceConfiguration = _serviceConfig,
                SiteUrl = message.SiteUrl,
                DocumentLibrary = message.DocumentLibrary,
                InteractionID = message.InteractionId,
                InternalPermission = message.InternalPermission,
                ListOfInternalEmailAddresses = string.Join(";", message.InternalUserEmails),
                ExternalPermission = message.ExternalPermission,
                ListOfExternalEmailAddresses = string.Join(";", message.ExternalUserEmails)
            };
            
            // Call the broker method
            var result = service.PermissionChangeInteraction();
            
            return new OperationResult
            {
                Success = result.Success,
                ErrorCode = result.ErrorNumber,
                ErrorMessage = result.ErrorMessage
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error applying permissions for Interaction {InteractionId}", 
                message.InteractionId);
            return new OperationResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
    
    public async Task<OperationResult<int>> CreateInteraction(
        InteractionCreationMessage message)
    {
        try
        {
            _logger.LogInformation(
                "Creating Interaction '{InteractionName}' for Project {ProjectId}", 
                message.InteractionName, message.ProjectId);
            
            var service = new SharePointService
            {
                ServiceConfiguration = _serviceConfig,
                SiteUrl = message.SiteUrl,
                DocumentLibrary = message.DocumentLibrary,
                ProjectID = message.ProjectId,
                InteractionName = message.InteractionName,
                ProjectSubfolder = message.ProjectSubfolder ?? string.Empty,
                InternalPermission = message.InternalPermission,
                ListOfInternalEmailAddresses = string.Join(";", message.InternalUserEmails),
                ExternalPermission = message.ExternalPermission,
                ListOfExternalEmailAddresses = string.Join(";", message.ExternalUserEmails)
            };
            
            var result = service.NewInteraction();
            
            return new OperationResult<int>
            {
                Success = result.Success,
                ErrorCode = result.ErrorNumber,
                ErrorMessage = result.ErrorMessage,
                Data = result.ID // SharePoint folder ID
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error creating Interaction '{InteractionName}'", 
                message.InteractionName);
            return new OperationResult<int>
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
}

public class OperationResult
{
    public bool Success { get; set; }
    public int ErrorCode { get; set; }
    public string ErrorMessage { get; set; }
}

public class OperationResult<T> : OperationResult
{
    public T Data { get; set; }
}
```

### 2. Direct CSOM Handler (for operations not in broker)

```csharp
public class DirectSharePointService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DirectSharePointService> _logger;
    private IConfidentialClientApplication _app;
    private string[] _scopes;
    private AuthenticationResult _currentAuthResult;
    private DateTime _tokenExpiresAt;
    
    public async Task<OperationResult> RemoveUniquePermissions(
        RemoveUniquePermissionMessage message)
    {
        try
        {
            var token = await GetAccessTokenAsync(message.SiteUrl);
            
            using (var ctx = new ClientContext(message.SiteUrl))
            {
                ctx.ExecutingWebRequest += async (s, e) =>
                {
                    var accessToken = await GetAccessTokenAsync(message.SiteUrl);
                    e.WebRequestExecutor.RequestHeaders["Authorization"] = 
                        "Bearer " + accessToken;
                };
                
                var list = ctx.Web.Lists.GetByTitle(message.DocumentLibrary);
                ctx.Load(list);
                await ctx.ExecuteQueryAsync();
                
                var item = list.GetItemById(message.FolderId);
                ctx.Load(item, 
                    i => i.Id, 
                    i => i.HasUniqueRoleAssignments, 
                    i => i.FileSystemObjectType);
                await ctx.ExecuteQueryAsync();
                
                if (item.FileSystemObjectType == FileSystemObjectType.Folder && 
                    item.HasUniqueRoleAssignments)
                {
                    item.ResetRoleInheritance();
                    await ctx.ExecuteQueryAsync();
                    
                    _logger.LogInformation(
                        "Reset permissions for folder ID {FolderId}", 
                        message.FolderId);
                    
                    return new OperationResult { Success = true };
                }
                else
                {
                    return new OperationResult 
                    { 
                        Success = false, 
                        ErrorMessage = "Folder doesn't have unique permissions or is not a folder" 
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error removing unique permissions for folder {FolderId}", 
                message.FolderId);
            return new OperationResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
    
    private async Task<string> GetAccessTokenAsync(string siteUrl)
    {
        // Token refresh logic similar to your scripts
        bool needsRefresh = _currentAuthResult == null || 
                           DateTime.Now.AddMinutes(15) >= _tokenExpiresAt;
        
        if (needsRefresh)
        {
            var env = _configuration["Environment"];
            var tenantId = _configuration[$"SharePoint:{env}:TenantId"];
            var clientId = _configuration[$"SharePoint:{env}:ClientId"];
            var thumbprint = _configuration[$"SharePoint:{env}:CertificateThumbprint"];
            
            var cert = GetCertificateFromStore(thumbprint);
            
            if (_app == null)
            {
                _app = ConfidentialClientApplicationBuilder
                    .Create(clientId)
                    .WithAuthority(AzureCloudInstance.AzurePublic, tenantId)
                    .WithCertificate(cert)
                    .Build();
                
                var tenantName = new Uri(siteUrl).Host.Split('.')[0];
                _scopes = new[] { $"https://{tenantName}.sharepoint.com/.default" };
            }
            
            _currentAuthResult = await _app.AcquireTokenForClient(_scopes).ExecuteAsync();
            _tokenExpiresAt = _currentAuthResult.ExpiresOn.DateTime.ToLocalTime().AddMinutes(-5);
            
            _logger.LogInformation("Token refreshed. Expires at {ExpiresAt}", _tokenExpiresAt);
        }
        
        return _currentAuthResult.AccessToken;
    }
    
    private X509Certificate2 GetCertificateFromStore(string thumbprint)
    {
        var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
        try
        {
            store.Open(OpenFlags.ReadOnly);
            var certs = store.Certificates.Find(
                X509FindType.FindByThumbprint, 
                thumbprint, 
                false);
            return certs.Count > 0 ? certs[0] : null;
        }
        finally
        {
            store.Close();
        }
    }
}
```

### 3. Message Processor with Throttling

```csharp
public class MessageProcessor
{
    private readonly SharePointOperationService _sharePointService;
    private readonly DirectSharePointService _directService;
    private readonly IJobRepository _jobRepository;
    private readonly ThrottleManager _throttleManager;
    private readonly ILogger<MessageProcessor> _logger;
    
    public async Task ProcessMessage(QueueMessageBase message)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            // Update job item status to Processing
            await _jobRepository.UpdateJobItemStatus(
                message.MessageId, 
                "Processing");
            
            OperationResult result = null;
            
            // Route to appropriate handler
            switch (message.OperationType)
            {
                case "InteractionPermissionSync":
                    result = await _sharePointService.ApplyInteractionPermissions(
                        (InteractionPermissionMessage)message);
                    break;
                    
                case "InteractionCreation":
                    var createResult = await _sharePointService.CreateInteraction(
                        (InteractionCreationMessage)message);
                    result = createResult;
                    
                    // Update database with created folder ID
                    if (createResult.Success)
                    {
                        await UpdateInteractionFolderId(
                            ((InteractionCreationMessage)message).InteractionName,
                            createResult.Data);
                    }
                    break;
                    
                case "RemoveUniquePermission":
                    result = await _directService.RemoveUniquePermissions(
                        (RemoveUniquePermissionMessage)message);
                    break;
                    
                default:
                    throw new NotSupportedException(
                        $"Operation type {message.OperationType} not supported");
            }
            
            // Update job item based on result
            if (result.Success)
            {
                await _jobRepository.UpdateJobItemStatus(
                    message.MessageId, 
                    "Completed");
                await _jobRepository.IncrementProcessedCount(message.JobId);
                
                // Successful operation - reduce delay
                _throttleManager.ReportSuccess();
            }
            else
            {
                await HandleFailure(message, result.ErrorMessage);
            }
            
            // Apply throttling delay
            await Task.Delay(_throttleManager.CurrentDelay);
        }
        catch (Exception ex) when (IsThrottlingException(ex))
        {
            _logger.LogWarning("Throttling detected. Increasing delay.");
            _throttleManager.ReportThrottling();
            
            // Requeue message
            await HandleFailure(message, ex.Message);
            
            // Apply longer delay
            await Task.Delay(_throttleManager.CurrentDelay * 2);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message {MessageId}", message.MessageId);
            await HandleFailure(message, ex.Message);
        }
    }
    
    private async Task HandleFailure(QueueMessageBase message, string errorMessage)
    {
        message.RetryCount++;
        
        if (message.RetryCount < message.MaxRetries)
        {
            // Requeue for retry
            await _jobRepository.UpdateJobItemStatus(
                message.MessageId, 
                "Requeued", 
                errorMessage, 
                message.RetryCount);
            
            // Republish to queue (handled by queue service)
        }
        else
        {
            // Max retries exceeded - mark as failed
            await _jobRepository.UpdateJobItemStatus(
                message.MessageId, 
                "Failed", 
                errorMessage, 
                message.RetryCount);
            await _jobRepository.IncrementFailedCount(message.JobId);
        }
    }
    
    private bool IsThrottlingException(Exception ex)
    {
        if (ex is ServerException serverEx)
        {
            return serverEx.ServerErrorCode == -2147429894 || 
                   serverEx.Message.Contains("429") ||
                   serverEx.Message.Contains("too many requests");
        }
        return false;
    }
}
```

### 4. Throttle Manager (Adaptive)

```csharp
public class ThrottleManager
{
    private int _currentDelay = 500; // Start at 500ms
    private readonly int _minDelay = 50;
    private readonly int _maxDelay = 5000;
    private int _successCount = 0;
    private int _throttleCount = 0;
    
    public int CurrentDelay => _currentDelay;
    
    public void ReportSuccess()
    {
        _successCount++;
        
        // After 10 consecutive successes, reduce delay by 10%
        if (_successCount >= 10)
        {
            _currentDelay = Math.Max(_minDelay, (int)(_currentDelay * 0.9));
            _successCount = 0;
        }
    }
    
    public void ReportThrottling()
    {
        _throttleCount++;
        _successCount = 0; // Reset success counter
        
        // Double the delay, up to max
        _currentDelay = Math.Min(_maxDelay, _currentDelay * 2);
    }
    
    public ThrottleStats GetStats()
    {
        return new ThrottleStats
        {
            CurrentDelay = _currentDelay,
            SuccessCount = _successCount,
            ThrottleCount = _throttleCount
        };
    }
}
```

## Configuration (appsettings.json)

### Web Application
```json
{
  "ConnectionStrings": {
    "DEV": "Server=sql-dev.internal;Database=ScyneShare;...",
    "UAT": "Server=sql-uat.internal;Database=ScyneShare;...",
    "PROD": "Server=sql-prod.internal;Database=ScyneShare;..."
  },
  "RabbitMQ": {
    "Host": "localhost",
    "Port": 5672,
    "Username": "guest",
    "Password": "guest",
    "VirtualHost": "/",
    "Queues": {
      "InteractionPermissions": "sharepoint.interaction.permissions",
      "InteractionCreation": "sharepoint.interaction.creation",
      "RemovePermissions": "sharepoint.remove.permissions",
      "DeadLetter": "sharepoint.deadletter"
    }
  },
  "SharePoint": {
    "DEV": {
      "TenantId": "fcbce1cd-2ec5-4340-9e8b-7e3a7bbf755f",
      "ClientId": "200294cd-4940-49d0-935d-809d2b81b19d",
      "CertificateThumbprint": "D3B62D579DC78102E4B9E62F55665923507AEBE3",
      "TenantName": "scyneadvisory"
    },
    "UAT": {
      "TenantId": "...",
      "ClientId": "...",
      "CertificateThumbprint": "...",
      "TenantName": "scyneadvisory"
    },
    "PROD": {
      "TenantId": "...",
      "ClientId": "...",
      "CertificateThumbprint": "...",
      "TenantName": "scyneadvisory"
    },
    "InteractionFolderTemplate": "{ \"Finding Documents\": {}, \"Response Documents\": {}, \"Template\": {}, \"Utility\": {} }",
    "ProjectFolderTemplate": "{ \"Template\": {}, \"Utility\": {} }",
    "EngagementFolderTemplate": "{ \"Template\": {}, \"Utility\": {} }"
  },
  "Processing": {
    "DefaultDelay": 500,
    "MinDelay": 50,
    "MaxDelay": 5000,
    "MaxRetries": 3,
    "BatchSize": 50
  }
}
```

### Worker Service
```json
{
  "Environment": "UAT",  // Switch between DEV/UAT/PROD
  "ConnectionStrings": { /* same as web */ },
  "RabbitMQ": { /* same as web */ },
  "SharePoint": { /* same as web */ },
  "Processing": { /* same as web */ },
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      {
        "Name": "Console"
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/worker-.log",
          "rollingInterval": "Day"
        }
      }
    ]
  }
}
```

## Getting Started

### Prerequisites
1. **.NET 8 SDK**
2. **SQL Server** (existing ScyneShare database)
3. **RabbitMQ** (Docker or standalone)
4. **Certificate** installed in LocalMachine\My store
5. **Visual Studio 2022** or **VS Code** with C# extension

### Quick Start

#### 1. Clone and Setup
```bash
git clone <repository-url>
cd SharePointPermissionSync
```

#### 2. Install RabbitMQ (Docker)
```bash
docker run -d --name rabbitmq \
  -p 5672:5672 \
  -p 15672:15672 \
  rabbitmq:3-management
```

#### 3. Update Configuration
Edit `appsettings.json` in both Web and Worker projects:
- Set connection strings
- Configure SharePoint credentials
- Set RabbitMQ connection

#### 4. Create Database Tables
```bash
dotnet ef database update --project src/SharePointPermissionSync.Data
```

Or run the SQL scripts manually from the database schema section.

#### 5. Run Web Portal
```bash
cd src/SharePointPermissionSync.Web
dotnet run
```

Navigate to `https://localhost:5001`

#### 6. Run Worker Service
```bash
cd src/SharePointPermissionSync.Worker
dotnet run
```

### Development Workflow

1. **Upload CSV** via web portal
2. **Portal validates** CSV and creates job in database
3. **Portal publishes** messages to RabbitMQ queue
4. **Worker consumes** messages one at a time
5. **Worker processes** using SharePoint broker or direct CSOM
6. **Worker updates** database with progress/results
7. **SignalR pushes** real-time updates to web UI
8. **User monitors** progress on dashboard

## Next Steps

1. Review the existing `Tecala.SMO.SharePoint` broker methods
2. Identify which operations you want to expose first
3. Create the database tables for job tracking
4. Set up RabbitMQ locally
5. Build the web portal (start simple - just CSV upload)
6. Build the worker service (start with one operation type)
7. Test end-to-end with a small CSV file
8. Add SignalR for real-time monitoring
9. Deploy to UAT environment
10. Production rollout

## Questions to Answer

1. **Which environment should we target first?** (DEV, UAT, or PROD)
2. **What's the priority order for operations?**
   - Interaction permission sync?
   - Interaction creation?
   - Remove unique permissions?
3. **Where should we host the worker service?**
   - Windows Server with Windows Service?
   - Docker container?
   - Same server as web app?
4. **How many concurrent workers** should we allow? (Start with 1, scale to 2-3?)
5. **Retention policy** for job history? (30 days? 90 days?)

---

**Ready to start building?** Let me know which operation you want to tackle first, and I'll help you create the detailed implementation for that specific workflow.