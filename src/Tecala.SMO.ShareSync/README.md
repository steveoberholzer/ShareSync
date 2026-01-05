# Tecala ShareSync K2 Broker

A K2 Service Broker that provides SharePoint permission synchronization and interaction management capabilities directly within K2 Forms and Workflows.

## Overview

This broker exposes the same functionality available in the SharePointPermissionSync Web application as K2 SmartObjects, allowing you to:

- **Sync Interaction Permissions** - Update SharePoint permissions for existing interactions
- **Create Interactions** - Create new interaction folders with proper permissions
- **Monitor Job Status** - Check the status of queued operations

All operations are queued to RabbitMQ and processed asynchronously by the SharePointPermissionSync Worker service.

## Architecture

```
K2 Form/Workflow
    ↓
ShareSync K2 Broker (this project)
    ↓
RabbitMQ Queue
    ↓
SharePointPermissionSync Worker
    ↓
SharePoint Online
```

## Project Structure

```
Tecala.SMO.ShareSync/
├── ShareSyncBroker.cs              # Main K2 broker class
├── Services/
│   ├── ShareSyncService.cs         # Service object with K2 methods
│   ├── DatabaseService.cs          # Database operations
│   ├── QueueService.cs             # RabbitMQ operations
│   ├── Logger.cs                   # Event log logging
│   ├── ILogger.cs                  # Logging interface
│   └── ErrorNumberResolver.cs      # Error code management
├── Properties/
│   └── AssemblyInfo.cs
├── packages.config                 # NuGet packages
└── Tecala.SMO.ShareSync.csproj

Tecala.SMO.ShareSync.TestHarness/   # Test console application
├── Program.cs
├── App.config
└── Tecala.SMO.ShareSync.TestHarness.csproj
```

## Prerequisites

### Required Software

1. **.NET Framework 4.6.2** or higher
2. **K2 Host Server** with ServiceSDK installed
3. **SQL Server** with ScyneShareDEV database
4. **RabbitMQ** server running
5. **SharePointPermissionSync Worker** service

### Required Database Tables

The broker requires these tables in the `ScyneShare` schema:

- `ProcessingJobs` - Job tracking
- `ProcessingJobItems` - Individual item tracking
- `ProcessingJobLogs` - Processing logs

These are created by the SharePointPermissionSync migrations.

## Configuration

### K2 Broker Configuration

When registering the broker in K2, configure these settings:

| Setting | Description | Example |
|---------|-------------|---------|
| SQL Connection String | Database connection | `Server=localhost;Database=ScyneShareDEV;Trusted_Connection=True;TrustServerCertificate=True;` |
| RabbitMQ Host | RabbitMQ server hostname | `localhost` |
| RabbitMQ Port | RabbitMQ port number | `5672` |
| RabbitMQ Username | RabbitMQ username | `guest` |
| RabbitMQ Password | RabbitMQ password | `guest` |
| RabbitMQ VirtualHost | RabbitMQ virtual host | `/` |
| Queue InteractionPermissions | Permission sync queue | `sharepoint.interaction.permissions` |
| Queue InteractionCreation | Interaction creation queue | `sharepoint.interaction.creation` |
| Queue RemovePermissions | Permission removal queue | `sharepoint.remove.permissions` |

## Building the Project

### Using MSBuild (Recommended)

```powershell
# Restore NuGet packages
nuget restore src\Tecala.SMO.ShareSync\packages.config -PackagesDirectory packages

# Build the broker
msbuild src\Tecala.SMO.ShareSync\Tecala.SMO.ShareSync.csproj /p:Configuration=Release

# Build the test harness
msbuild src\Tecala.SMO.ShareSync.TestHarness\Tecala.SMO.ShareSync.TestHarness.csproj /p:Configuration=Release
```

### Post-Build

The project is configured to automatically copy the compiled DLL to:
```
C:\Program Files (x86)\K2\ServiceBroker\
```

You can modify this in the `.csproj` file's `<PostBuildEvent>` section.

## Deployment to K2

### 1. Register the Service Type

1. Open **K2 Management Console**
2. Navigate to **Integration** → **Service Types**
3. Click **New Service Type**
4. Browse to `Tecala.SMO.ShareSync.dll`
5. Click **OK**

### 2. Create Service Instance

1. Navigate to **Integration** → **Service Instances**
2. Click **New Service Instance**
3. Select **Tecala SharePoint Permission Sync** service type
4. Configure all settings (see Configuration section)
5. Click **OK**

### 3. Verify Registration

1. Check that the service instance shows as **Running**
2. Expand the service instance to see available SmartObjects
3. You should see the **ShareSync** SmartObject with methods

## Available Methods

### 1. SyncInteractionPermissions

Synchronize permissions for an existing SharePoint interaction folder.

**Method Type:** Execute

**Input Parameters:**
- `InteractionId` (Number, Required) - Database interaction ID
- `ProjectId` (Number, Required) - Database project ID
- `EngagementId` (Number, Required) - Database engagement ID
- `Environment` (Text, Required) - Target environment (DEV/UAT/PROD)
- `SiteUrl` (Text, Required) - SharePoint site URL
- `SharePointFolderId` (Number, Optional) - SharePoint folder ID if known
- `InternalPermission` (Text, Optional) - Permission level for internal users (default: Read)
- `InternalUserEmails` (Memo, Optional) - Semicolon-separated internal user emails
- `ExternalPermission` (Text, Optional) - Permission level for external users
- `ExternalUserEmails` (Memo, Optional) - Semicolon-separated external user emails
- `Priority` (Text, Optional) - Job priority: Low, Medium (default), High, Critical

**Output Parameters:**
- `ErrorNumber` (Number) - 0 for success, non-zero for error
- `ErrorMessage` (Text) - Error description if failed
- `JobId` (Text) - GUID of the created job
- `MessageId` (Text) - GUID of the queued message

**Example K2 SmartObject Call:**
```
Execute ShareSync.SyncInteractionPermissions
  InteractionId = 12345
  ProjectId = 100
  EngagementId = 50
  Environment = "DEV"
  SiteUrl = "https://yourtenant.sharepoint.com/sites/engagements"
  InternalPermission = "Contribute"
  InternalUserEmails = "user1@company.com;user2@company.com"
  Priority = "High"
```

### 2. CreateInteraction

Create a new interaction folder in SharePoint with permissions.

**Method Type:** Create

**Input Parameters:**
- `InteractionName` (Text, Required) - Name of the interaction to create
- `ProjectId` (Number, Required) - Database project ID
- `EngagementId` (Number, Required) - Database engagement ID
- `Environment` (Text, Required) - Target environment (DEV/UAT/PROD)
- `SiteUrl` (Text, Required) - SharePoint site URL
- `ProjectSubfolder` (Text, Optional) - Project subfolder path
- `InternalPermission` (Text, Optional) - Permission level for internal users (default: Read)
- `InternalUserEmails` (Memo, Optional) - Semicolon-separated internal user emails
- `ExternalPermission` (Text, Optional) - Permission level for external users
- `ExternalUserEmails` (Memo, Optional) - Semicolon-separated external user emails
- `Priority` (Text, Optional) - Job priority: Low, Medium (default), High, Critical

**Output Parameters:**
- `ErrorNumber` (Number) - 0 for success, non-zero for error
- `ErrorMessage` (Text) - Error description if failed
- `JobId` (Text) - GUID of the created job
- `MessageId` (Text) - GUID of the queued message

**Example K2 SmartObject Call:**
```
Execute ShareSync.CreateInteraction
  InteractionName = "Q1 2026 Financial Review"
  ProjectId = 100
  EngagementId = 50
  Environment = "PROD"
  SiteUrl = "https://yourtenant.sharepoint.com/sites/engagements"
  InternalPermission = "Edit"
  InternalUserEmails = "partner@company.com;manager@company.com"
  Priority = "Critical"
```

### 3. GetJobStatus

Retrieve the current status of a processing job.

**Method Type:** Read

**Input Parameters:**
- `JobId` (Text, Required) - GUID of the job to check

**Output Parameters:**
- `ErrorNumber` (Number) - 0 for success, non-zero for error
- `ErrorMessage` (Text) - Error description if failed
- `JobId` (Text) - GUID of the job
- `Status` (Text) - Current job status (Queued, Processing, Completed, Failed, etc.)

**Example K2 SmartObject Call:**
```
Execute ShareSync.GetJobStatus
  JobId = "12345678-1234-1234-1234-123456789abc"
```

## Testing with Test Harness

The test harness is a console application that lets you test broker methods without deploying to K2.

### Configuration

Edit `App.config` to match your environment:

```xml
<appSettings>
  <add key="SQL Connection String" value="Server=localhost;Database=ScyneShareDEV;..." />
  <add key="RabbitMQ Host" value="localhost" />
  <add key="RabbitMQ Port" value="5672" />
  <!-- ... other settings ... -->
</appSettings>
```

### Running Tests

```powershell
cd src\Tecala.SMO.ShareSync.TestHarness\bin\Debug
.\Tecala.SMO.ShareSync.TestHarness.exe
```

Follow the menu prompts to test each operation.

## Permission Levels

Supported SharePoint permission levels:

- **Read** - View items and pages
- **Contribute** - Add, edit, and delete items
- **Edit** - Same as Contribute plus manage lists
- **Full Control** - Full permissions

## Priority Levels

Job priorities affect queue processing order:

- **Low** (Priority 3) - Background tasks
- **Medium** (Priority 5) - Default priority
- **High** (Priority 7) - Important operations
- **Critical** (Priority 10) - Urgent operations

Higher priority jobs are processed first by the Worker service.

## Error Handling

All methods return:
- `ErrorNumber = 0` on success
- `ErrorNumber > 0` on failure (unique code per method)
- `ErrorMessage` with detailed error description

Check `ErrorNumber` in your K2 workflows to handle errors appropriately.

## Logging

The broker logs to Windows Event Log:
- **Source:** `Tecala.SMO.ShareSync`
- **Log:** Application
- **Event IDs:**
  - 1000: Trace
  - 1001: Information
  - 1002: Warning
  - 1003: Error

View logs in Event Viewer under Windows Logs → Application.

## Troubleshooting

### Broker won't register in K2

1. Verify K2 ServiceSDK DLL location matches the project reference
2. Check that all NuGet packages are restored
3. Ensure .NET Framework 4.6.2 is installed
4. Review K2 service logs for detailed errors

### Jobs aren't processing

1. Verify RabbitMQ is running and accessible
2. Check that SharePointPermissionSync Worker service is running
3. Verify database connection string is correct
4. Check ProcessingJobs table for job status

### Permission errors

1. Ensure SharePointPermissionSync Worker has proper SharePoint credentials
2. Verify certificate is installed and accessible
3. Check Worker service logs for detailed errors

## Development

### Adding New Methods

1. Add method to `ShareSyncService.cs` with `[Method]` attribute
2. Add required properties with `[Property]` attribute
3. Implement business logic using DatabaseService and QueueService
4. Update test harness to include new method
5. Rebuild and redeploy to K2

### Debugging

For debugging K2 broker issues:

1. Use the test harness for rapid iteration
2. Enable verbose logging in Event Viewer
3. Monitor RabbitMQ management UI for queue activity
4. Check SQL Server for job records
5. Review Worker service logs

## Support

For issues or questions:
- Check Event Log for detailed error messages
- Review RabbitMQ queue status
- Examine ProcessingJobLogs table for processing details
- Contact: ShareSync development team

## Version History

### 1.0.0 (2026-01-05)
- Initial release
- SyncInteractionPermissions method
- CreateInteraction method
- GetJobStatus method
- Test harness included

## License

Copyright © Tecala 2026. All rights reserved.
