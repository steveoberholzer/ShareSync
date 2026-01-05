# ShareSync Deployment Guide

## Overview

ShareSync is a SharePoint permission synchronization system consisting of two main components:

1. **Web Application** - ASP.NET Core MVC application for uploading permission sync jobs
2. **Worker Service** - Background Windows service that processes jobs via RabbitMQ priority queues

Both applications are built on .NET 8 and deployed as self-contained applications, meaning they include the .NET runtime and do not require .NET 8 to be installed on the target server.

## Architecture

```
[Web UI] → [Upload CSV] → [RabbitMQ Priority Queue] → [Worker Service] → [SharePoint]
                              ↓
                         [SQL Server]
                    (Job tracking & state)
```

**Key Features:**
- Priority-based processing (High=10, Medium=5, Low=1)
- Durable message queues with dead-letter handling
- Real-time progress updates via SignalR
- Comprehensive logging with Serilog
- Production-tested SharePoint integration (Tecala.SMO.SharePoint library)

## Prerequisites

### On K2 Server

- Windows Server 2016 or later
- IIS 10.0 or later with ASP.NET Core Module
- SQL Server access (SQL Server 2016 or later)
- RabbitMQ Server 4.0 or later running and accessible
- Service account with appropriate permissions:
  - SharePoint site collection admin rights
  - SQL Server database access
  - Local administrator (for service installation)

### Published Artifacts

The self-contained publish creates approximately 350-400 MB total:
- **Worker**: ~100-150 MB (includes .NET 8 runtime)
- **Web**: ~200-250 MB (includes .NET 8 runtime)

## Pre-Deployment Steps

### 1. Database Setup

The application uses the `ScyneShareDEV` database (or `ScyneShare` for production).

#### Apply Database Migration

Run the Priority column migration:

```sql
-- Execute from: src/SharePointPermissionSync.Data/Migrations/20250105_AddPriorityColumn.sql

USE ScyneShareDEV;
GO

-- Adds Priority column if it doesn't exist
ALTER TABLE [ScyneShare].[ProcessingJobs]
ADD [Priority] NVARCHAR(10) NOT NULL DEFAULT 'Medium';

-- Creates index for efficient filtering
CREATE NONCLUSTERED INDEX [IX_ProcessingJobs_Priority]
ON [ScyneShare].[ProcessingJobs]([Priority]);
GO
```

#### Verify Database Tables

Ensure these tables exist:
- `ScyneShare.ProcessingJobs` (with Priority column)
- `ScyneShare.ProcessingJobItems`
- Existing business tables: `Engagement`, `Project`, `Interaction`, `InteractionMembership`

### 2. RabbitMQ Setup

#### Install RabbitMQ Management Plugin

```powershell
# Enable management plugin for queue monitoring
rabbitmq-plugins enable rabbitmq_management
```

#### Configure RabbitMQ

- Default credentials: `guest/guest` (change for production!)
- Management UI: `http://localhost:15672`
- AMQP Port: `5672`

#### Priority Queues Setup

The Worker service automatically creates these queues on first startup:
- `sharepoint.interaction.creation` (with x-max-priority: 10)
- `sharepoint.interaction.permissions` (with x-max-priority: 10)
- Dead-letter queues for failed messages

**Important**: If upgrading from a non-priority version, delete existing queues first:

```bash
# Delete old queues (RabbitMQ must be running)
curl -u guest:guest -X DELETE http://localhost:15672/api/queues/%2F/sharepoint.interaction.creation
curl -u guest:guest -X DELETE http://localhost:15672/api/queues/%2F/sharepoint.interaction.permissions
```

## Deployment Instructions

### Step 1: Copy Files to Target Server

From your build/publish machine:

```powershell
# Copy Worker Service
xcopy "C:\Publish\ShareSync\Worker\*" "\\K2SERVER\C$\Apps\ShareSync\Worker\" /E /I /Y

# Copy Web Application
xcopy "C:\Publish\ShareSync\Web\*" "\\K2SERVER\C$\Apps\ShareSync\Web\" /E /I /Y
```

Or if deploying locally on K2 server:
- Worker: `C:\Apps\ShareSync\Worker\`
- Web: `C:\Apps\ShareSync\Web\`

### Step 2: Configure Application Settings

#### Worker Service Configuration

Edit `C:\Apps\ShareSync\Worker\appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SQL_SERVER;Database=ScyneShare;Integrated Security=true;TrustServerCertificate=true;"
  },
  "RabbitMQ": {
    "HostName": "localhost",
    "Port": 5672,
    "UserName": "guest",
    "Password": "guest",
    "VirtualHost": "/"
  },
  "SharePoint": {
    "SiteUrl": "https://yourtenant.sharepoint.com/sites/yoursite",
    "ClientId": "your-app-client-id",
    "ClientSecret": "your-client-secret",
    "TenantId": "your-tenant-id"
  },
  "Serilog": {
    "WriteTo": [
      {
        "Name": "File",
        "Args": {
          "path": "C:\\Apps\\ShareSync\\Worker\\Logs\\worker-.log",
          "rollingInterval": "Day"
        }
      }
    ]
  }
}
```

#### Web Application Configuration

Edit `C:\Apps\ShareSync\Web\appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SQL_SERVER;Database=ScyneShare;Integrated Security=true;TrustServerCertificate=true;"
  },
  "RabbitMQ": {
    "HostName": "localhost",
    "Port": 5672,
    "UserName": "guest",
    "Password": "guest",
    "VirtualHost": "/"
  },
  "Serilog": {
    "WriteTo": [
      {
        "Name": "File",
        "Args": {
          "path": "C:\\Apps\\ShareSync\\Web\\Logs\\web-.log",
          "rollingInterval": "Day"
        }
      }
    ]
  }
}
```

### Step 3: Install Worker as Windows Service

Choose one of the following methods:

#### Option A: Using sc.exe (Built-in)

```cmd
REM Run as Administrator
sc create ShareSyncWorker ^
  binPath="C:\Apps\ShareSync\Worker\SharePointPermissionSync.Worker.exe" ^
  DisplayName="ShareSync Worker Service" ^
  start=auto

sc description ShareSyncWorker "SharePoint Permission Sync Worker - Processes RabbitMQ priority queue messages"

sc start ShareSyncWorker
```

#### Option B: Using NSSM (Recommended)

Download NSSM from https://nssm.cc/download

```cmd
REM Run as Administrator
nssm install ShareSyncWorker "C:\Apps\ShareSync\Worker\SharePointPermissionSync.Worker.exe"
nssm set ShareSyncWorker AppDirectory "C:\Apps\ShareSync\Worker"
nssm set ShareSyncWorker Description "SharePoint Permission Sync Worker - Priority Queue Processing"
nssm set ShareSyncWorker Start SERVICE_AUTO_START
nssm start ShareSyncWorker
```

**NSSM Advantages:**
- Better logging and crash recovery
- GUI for service management
- Automatic restart on failure
- Environment variable support

### Step 4: Deploy Web Application to IIS

#### 4.1 Install ASP.NET Core Hosting Bundle

If not already installed:
1. Download: https://dotnet.microsoft.com/download/dotnet/8.0
2. Install: ASP.NET Core Runtime 8.x Hosting Bundle
3. Run: `iisreset` after installation

#### 4.2 Create Application Pool

```powershell
# PowerShell as Administrator
Import-Module WebAdministration

New-WebAppPool -Name "ShareSyncAppPool"
Set-ItemProperty -Path "IIS:\AppPools\ShareSyncAppPool" -Name "managedRuntimeVersion" -Value ""
Set-ItemProperty -Path "IIS:\AppPools\ShareSyncAppPool" -Name "managedPipelineMode" -Value "Integrated"
```

Or via IIS Manager:
1. Open IIS Manager
2. Right-click "Application Pools" → "Add Application Pool"
3. Name: `ShareSyncAppPool`
4. .NET CLR Version: `No Managed Code`
5. Managed Pipeline Mode: `Integrated`

#### 4.3 Create Website

```powershell
# PowerShell as Administrator
New-Website -Name "ShareSync" `
  -PhysicalPath "C:\Apps\ShareSync\Web" `
  -ApplicationPool "ShareSyncAppPool" `
  -Port 8080
```

Or via IIS Manager:
1. Right-click "Sites" → "Add Website"
2. Site name: `ShareSync`
3. Application pool: `ShareSyncAppPool`
4. Physical path: `C:\Apps\ShareSync\Web`
5. Binding:
   - Type: `http`
   - Port: `8080` (or your preferred port)
   - Host name: (optional) `sharesync.yourdomain.com`

#### 4.4 Set Permissions

```powershell
# Grant IIS_IUSRS read access
icacls "C:\Apps\ShareSync\Web" /grant "IIS_IUSRS:(OI)(CI)RX" /T

# Grant App Pool identity access (if using specific identity)
icacls "C:\Apps\ShareSync\Web" /grant "IIS APPPOOL\ShareSyncAppPool:(OI)(CI)RX" /T
```

#### 4.5 Configure Application Settings

In IIS Manager:
1. Select ShareSync website
2. Open "Configuration Editor"
3. Section: `system.webServer/aspNetCore`
4. Verify `processPath` points to `SharePointPermissionSync.Web.exe`
5. Verify `stdoutLogEnabled` is `false` (or `true` for debugging)

#### 4.6 Start Website

```powershell
Start-Website -Name "ShareSync"
```

## Post-Deployment Verification

### 1. Verify Worker Service

```powershell
# Check service status
Get-Service ShareSyncWorker

# Check logs
Get-Content "C:\Apps\ShareSync\Worker\Logs\worker-*.log" -Tail 50
```

Expected log entries:
- RabbitMQ connection established
- Priority queues declared (x-max-priority: 10)
- Worker started and listening

### 2. Verify Web Application

Browse to: `http://localhost:8080` (or configured port)

Expected:
- Home page loads
- No errors in browser console
- "Upload Permissions" page accessible

Check logs:
```powershell
Get-Content "C:\Apps\ShareSync\Web\Logs\web-*.log" -Tail 50
```

### 3. Verify RabbitMQ Queues

1. Open RabbitMQ Management: `http://localhost:15672`
2. Login with credentials
3. Navigate to "Queues" tab
4. Verify queues exist:
   - `sharepoint.interaction.creation` (Features: Pri=10)
   - `sharepoint.interaction.permissions` (Features: Pri=10)
   - Dead-letter queues

### 4. Verify Database Connection

```sql
-- Check recent jobs
SELECT TOP 10
    JobId,
    JobType,
    Priority,
    Status,
    CreatedDate
FROM ScyneShare.ProcessingJobs
ORDER BY CreatedDate DESC;
```

### 5. End-to-End Test

1. Navigate to Web UI: `http://localhost:8080/Operations/InteractionPermissions`
2. Upload test CSV file
3. Verify:
   - Job appears in queue view
   - Worker picks up job
   - Database records updated
   - Success message displayed

## Priority Queue System

### Priority Levels

| Level  | RabbitMQ Priority | Use Case                          |
|--------|-------------------|-----------------------------------|
| High   | 10                | Urgent permission updates         |
| Medium | 5                 | Standard operations (default)     |
| Low    | 1                 | Bulk operations, maintenance jobs |

### How Priorities Work

1. Web app publishes messages with priority value (1-10)
2. RabbitMQ orders queue by priority (highest first)
3. Worker consumes messages in priority order
4. Within same priority, uses FIFO

### Setting Priority

Via Web UI:
- Select priority from dropdown when uploading CSV
- Default: Medium

Via API:
```csharp
await queueService.PublishAsync(message, priority: "High");
```

## Updating the Application

### 1. Stop Services

```powershell
# Stop Worker
Stop-Service ShareSyncWorker

# Stop Web
Stop-Website -Name "ShareSync"
```

### 2. Backup Current Version

```powershell
Copy-Item "C:\Apps\ShareSync" "C:\Apps\ShareSync.Backup.$(Get-Date -Format 'yyyyMMdd-HHmmss')" -Recurse
```

### 3. Deploy New Files

```powershell
# Copy new files (overwrite existing)
xcopy "C:\Publish\ShareSync\Worker\*" "C:\Apps\ShareSync\Worker\" /E /Y
xcopy "C:\Publish\ShareSync\Web\*" "C:\Apps\ShareSync\Web\" /E /Y
```

### 4. Update Configuration

Merge any new settings from `appsettings.json` while preserving environment-specific values.

### 5. Apply Database Migrations

Check for new migration scripts in `src\SharePointPermissionSync.Data\Migrations\` and apply as needed.

### 6. Restart Services

```powershell
# Start Worker
Start-Service ShareSyncWorker

# Start Web
Start-Website -Name "ShareSync"
```

## Troubleshooting

### Worker Service Won't Start

**Check Event Viewer:**
```powershell
Get-EventLog -LogName Application -Source ShareSyncWorker -Newest 10
```

**Common Issues:**
1. **Database connection failed**
   - Verify connection string in `appsettings.json`
   - Test SQL Server connectivity: `Test-NetConnection YOUR_SQL_SERVER -Port 1433`
   - Check SQL Server authentication

2. **RabbitMQ connection failed**
   - Verify RabbitMQ is running: `Get-Service RabbitMQ`
   - Check hostname/port in configuration
   - Verify credentials

3. **Queue argument mismatch**
   - Delete existing queues and let Worker recreate them with priority support
   - See "RabbitMQ Setup" section above

**Check Worker Logs:**
```powershell
Get-Content "C:\Apps\ShareSync\Worker\Logs\worker-*.log" -Wait
```

### Web Application Shows Error

**Check IIS Logs:**
- Location: `C:\inetpub\logs\LogFiles\W3SVC*\`
- Or: `C:\Apps\ShareSync\Web\Logs\web-*.log`

**Common Issues:**
1. **500.30 In-Process Handler Load Failure**
   - ASP.NET Core Hosting Bundle not installed
   - Install from: https://dotnet.microsoft.com/download/dotnet/8.0
   - Run `iisreset` after installation

2. **500.19 Configuration Error**
   - Missing web.config or corrupted
   - Verify `web.config` exists in publish folder
   - Check file permissions

3. **Database errors on startup**
   - Verify Priority column exists
   - Run migration script: `20250105_AddPriorityColumn.sql`
   - Check connection string

**Enable Detailed Errors (Development Only):**

Edit `web.config`:
```xml
<aspNetCore processPath="dotnet"
            arguments=".\SharePointPermissionSync.Web.dll"
            stdoutLogEnabled="true"
            stdoutLogFile=".\logs\stdout" />
```

### Jobs Not Processing

1. **Check Worker is running:**
   ```powershell
   Get-Service ShareSyncWorker
   ```

2. **Check RabbitMQ queue depth:**
   - Open RabbitMQ Management UI
   - Check message count in queues
   - Verify consumer is connected

3. **Check for errors in Worker logs:**
   ```powershell
   Select-String -Path "C:\Apps\ShareSync\Worker\Logs\*.log" -Pattern "Error|Exception"
   ```

4. **Verify SharePoint credentials:**
   - Check ClientId/ClientSecret in `appsettings.json`
   - Verify app has permissions in SharePoint

### Database Connection Issues

```powershell
# Test SQL connectivity
Test-NetConnection YOUR_SQL_SERVER -Port 1433

# Verify SQL Server accepts connections
# On SQL Server, check SQL Server Configuration Manager:
# - TCP/IP enabled
# - Named Pipes enabled (if using)
```

**Check Windows Authentication:**
- Verify service account has database access
- Grant permissions:
  ```sql
  USE ScyneShare;
  GO
  CREATE USER [DOMAIN\ServiceAccount] FOR LOGIN [DOMAIN\ServiceAccount];
  ALTER ROLE db_datareader ADD MEMBER [DOMAIN\ServiceAccount];
  ALTER ROLE db_datawriter ADD MEMBER [DOMAIN\ServiceAccount];
  GO
  ```

## Rollback Procedure

If deployment fails:

```powershell
# Stop services
Stop-Service ShareSyncWorker
Stop-Website -Name "ShareSync"

# Remove failed deployment
Remove-Item "C:\Apps\ShareSync" -Recurse -Force

# Restore backup (use most recent backup)
$backupDir = Get-ChildItem "C:\Apps" -Filter "ShareSync.Backup.*" | Sort-Object Name -Descending | Select-Object -First 1
Copy-Item $backupDir.FullName "C:\Apps\ShareSync" -Recurse

# Start services
Start-Service ShareSyncWorker
Start-Website -Name "ShareSync"
```

## Security Considerations

### Production Hardening

1. **RabbitMQ:**
   - Change default `guest/guest` credentials
   - Enable SSL/TLS for AMQP connections
   - Restrict management UI access

2. **SQL Server:**
   - Use least-privilege service accounts
   - Enable encryption (TrustServerCertificate=false with valid cert)
   - Use SQL authentication for better audit trails

3. **SharePoint:**
   - Use app-only authentication
   - Limit app permissions to required sites
   - Rotate client secrets regularly

4. **IIS:**
   - Enable HTTPS with valid SSL certificate
   - Disable directory browsing
   - Configure request filtering
   - Use dedicated app pool identity

5. **File System:**
   - Remove write permissions from published files
   - Secure log directories
   - Regular log rotation

### Network Security

- Firewall rules:
  - SQL Server: Port 1433 (internal only)
  - RabbitMQ: Port 5672 (internal only)
  - RabbitMQ Management: Port 15672 (restrict access)
  - Web: Port 80/443 (public or internal as needed)

## Monitoring and Maintenance

### Log Retention

Configure log cleanup:
```powershell
# PowerShell scheduled task to delete logs older than 30 days
$logPaths = @(
    "C:\Apps\ShareSync\Worker\Logs",
    "C:\Apps\ShareSync\Web\Logs"
)
$daysToKeep = 30

foreach ($path in $logPaths) {
    Get-ChildItem $path -Filter "*.log" |
        Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-$daysToKeep) } |
        Remove-Item -Force
}
```

### Health Checks

Create a monitoring script:
```powershell
# health-check.ps1
$errors = @()

# Check Worker service
if ((Get-Service ShareSyncWorker).Status -ne 'Running') {
    $errors += "Worker service not running"
}

# Check Web site
if ((Get-Website -Name "ShareSync").State -ne 'Started') {
    $errors += "Web site not started"
}

# Check RabbitMQ
if ((Get-Service RabbitMQ).Status -ne 'Running') {
    $errors += "RabbitMQ not running"
}

# Report results
if ($errors.Count -gt 0) {
    Write-Error ($errors -join "; ")
    # Send alert email or notification
} else {
    Write-Output "All services healthy"
}
```

### Performance Tuning

1. **RabbitMQ:**
   - Adjust prefetch count for worker consumers
   - Monitor queue depth and consumer count
   - Enable lazy queues for large backlogs

2. **SQL Server:**
   - Regular index maintenance
   - Monitor query performance
   - Archive old job records

3. **Worker Service:**
   - Adjust concurrent worker count based on load
   - Monitor memory usage
   - Scale horizontally by adding more worker instances

## Support and Documentation

- Development Guide: `DEVELOPMENT_GUIDE.md`
- Architecture: See "Priority Queue System" section above
- Code Repository: [Your Git repository URL]
- Issue Tracking: [Your issue tracker URL]

## Version History

- **2025-01-05**: Added Priority queue system, updated deployment for .NET 8 self-contained
- **2024-12-18**: Initial queue-based architecture
