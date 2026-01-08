# Windows Service Setup Guide

This guide explains how to deploy the SharePoint Permission Sync Worker as a Windows Service.

## Prerequisites

- .NET 8.0 Runtime installed on the target machine
- Administrator privileges
- RabbitMQ installed and running
- SQL Server accessible with the correct connection string

## Step 1: Publish the Application

Open a command prompt and navigate to the solution root directory, then run:

```powershell
dotnet publish src\SharePointPermissionSync.Worker -c Release -o C:\Services\SharePointPermissionSync
```

This will compile the application in Release mode and output all files to `C:\Services\SharePointPermissionSync`.

> You can change the output path to any location you prefer.

## Step 2: Update Configuration

Before creating the service, ensure the configuration file is properly set up:

1. Navigate to the publish directory: `C:\Services\SharePointPermissionSync`
2. Edit `appsettings.json` to configure:
   - Database connection string
   - RabbitMQ settings
   - Processing settings
   - Any other environment-specific settings

## Step 3: Create the Windows Service

Open a command prompt **as Administrator** and run:

```powershell
sc create "SharePoint Permission Sync Worker" binPath="C:\Services\SharePointPermissionSync\SharePointPermissionSync.Worker.exe"
```

### Optional: Configure Service Settings

Set the service to start automatically:

```powershell
sc config "SharePoint Permission Sync Worker" start=auto
```

Set service description:

```powershell
sc description "SharePoint Permission Sync Worker" "Processes SharePoint permission synchronization tasks from RabbitMQ"
```

Configure service to restart on failure:

```powershell
sc failure "SharePoint Permission Sync Worker" reset=86400 actions=restart/60000/restart/60000/restart/60000
```

## Step 4: Start the Service

```powershell
sc start "SharePoint Permission Sync Worker"
```

Or use the Services management console (services.msc).

## Managing the Service

### Check Service Status

```powershell
sc query "SharePoint Permission Sync Worker"
```

### Stop the Service

```powershell
sc stop "SharePoint Permission Sync Worker"
```

### Restart the Service

```powershell
sc stop "SharePoint Permission Sync Worker"
sc start "SharePoint Permission Sync Worker"
```

### Uninstall the Service

First, stop the service:

```powershell
sc stop "SharePoint Permission Sync Worker"
```

Then delete it:

```powershell
sc delete "SharePoint Permission Sync Worker"
```

## Updating the Service

To update the service after making code changes:

1. Stop the service:
   ```powershell
   sc stop "SharePoint Permission Sync Worker"
   ```

2. Publish the new version to the same directory:
   ```powershell
   dotnet publish src\SharePointPermissionSync.Worker -c Release -o C:\Services\SharePointPermissionSync
   ```

3. Start the service:
   ```powershell
   sc start "SharePoint Permission Sync Worker"
   ```

## Troubleshooting

### Viewing Logs

Logs are written to the `logs` subdirectory in the service installation folder:
- Location: `C:\Services\SharePointPermissionSync\logs\`
- Files are named: `worker-YYYYMMDD.log`

### Common Issues

**Service fails to start:**
- Check Event Viewer (Windows Logs > Application) for error details
- Verify the service account has permissions to access the installation directory
- Ensure all dependencies (RabbitMQ, SQL Server) are accessible
- Check the log files in the logs directory

**Service starts but doesn't process messages:**
- Verify RabbitMQ is running and accessible
- Check database connection string in appsettings.json
- Review the startup validation messages in the log files

**Permission Issues:**
- The service runs under the Local System account by default
- If you need different permissions, configure the service to run under a specific account:
  ```powershell
  sc config "SharePoint Permission Sync Worker" obj="DOMAIN\Username" password="Password"
  ```

## Running in Development Mode

The application can still run as a console application during development:

```powershell
dotnet run --project src\SharePointPermissionSync.Worker
```

The application automatically detects whether it's running as a Windows Service or console application.
