# Database Setup Scripts

This folder contains SQL scripts for setting up the SharePoint Permission Sync database.

## Overview

The system uses two types of tables:

### 1. **Business Tables** (EXISTING in Production)
- `Engagement`
- `Project`
- `Interaction`
- `InteractionMembership`

**⚠️ These tables are EXCLUDED from EF Core migrations**
- They already exist in production
- The system only reads from them
- EF Core migrations will NOT modify these tables

### 2. **Queue System Tables** (NEW)
- `ProcessingJobs` - Tracks CSV upload jobs
- `ProcessingJobItems` - Tracks individual items being processed

**✅ These tables are INCLUDED in EF Core migrations**
- Will be created when you run migrations
- Safe to deploy to any environment

---

## Setup Instructions

### Option A: Using EF Core Migrations (Recommended)

This is the safest approach as it will only create the queue tables.

```bash
# From the solution root directory
cd C:\DEV\ShareSync

# Apply migrations to create ProcessingJobs and ProcessingJobItems
dotnet ef database update --project src/SharePointPermissionSync.Data --startup-project src/SharePointPermissionSync.Web --connection "Server=localhost;Database=ScyneShare;Trusted_Connection=True;TrustServerCertificate=True;"
```

### Option B: Using SQL Scripts Manually

#### For DEV Environment (Local Testing)

If you need to create ALL tables in a fresh DEV database:

```bash
# 1. Create business tables (if not present)
sqlcmd -S localhost -d ScyneShareDEV -E -i scripts/CreateBusinessTables.sql

# 2. Create queue tables
sqlcmd -S localhost -d ScyneShareDEV -E -i scripts/CreateQueueTables.sql
```

#### For PRODUCTION (Deploying to existing database)

**Only run the queue tables script!** The business tables already exist.

```bash
# Only create the new queue tables
sqlcmd -S YOUR_PROD_SERVER -d ScyneShare -U your_user -P your_password -i scripts/CreateQueueTables.sql
```

---

## Testing Against Local ScyneShare Database

You mentioned you have a local ScyneShare database with the business tables. Perfect!

### Step 1: Verify Connection String

Update `appsettings.json` in the Web project:

```json
{
  "ConnectionStrings": {
    "ScyneShare": "Server=localhost;Database=ScyneShare;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

### Step 2: Run Migration

Since your local ScyneShare database already has the business tables, the migration will:
- ✅ Skip Engagement, Project, Interaction, InteractionMembership (they're excluded)
- ✅ Only create ProcessingJobs and ProcessingJobItems

```bash
# This is safe to run against your local ScyneShare database
dotnet ef database update --project src/SharePointPermissionSync.Data --startup-project src/SharePointPermissionSync.Web
```

### Step 3: Verify Tables Created

```sql
-- Check that only the queue tables were created
SELECT TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'ScyneShare'
  AND TABLE_NAME IN ('ProcessingJobs', 'ProcessingJobItems')
ORDER BY TABLE_NAME;

-- Should return:
-- ProcessingJobs
-- ProcessingJobItems
```

---

## File Descriptions

| File | Purpose |
|------|---------|
| `CreateBusinessTables.sql` | Creates Engagement, Project, Interaction, InteractionMembership tables (DEV only) |
| `CreateQueueTables.sql` | Creates ProcessingJobs and ProcessingJobItems tables (can be used instead of migrations) |
| `README.md` | This file |

---

## Migration Files

Location: `src/SharePointPermissionSync.Data/Migrations/`

| File | Description |
|------|-------------|
| `20241218_InitialQueueTables.cs` | EF Core migration that creates only the queue tables |

---

## Safety Checks

Before running any scripts in production:

1. ✅ Verify you're connected to the correct database
   ```sql
   SELECT DB_NAME(); -- Should return 'ScyneShare'
   ```

2. ✅ Check existing tables
   ```sql
   SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES
   WHERE TABLE_SCHEMA = 'ScyneShare'
   ORDER BY TABLE_NAME;
   ```

3. ✅ Backup the database first (production)
   ```sql
   BACKUP DATABASE ScyneShare TO DISK = 'C:\Backups\ScyneShare_BeforeMigration.bak';
   ```

4. ✅ Test in DEV/UAT first!

---

## Troubleshooting

### "Table already exists" error
- The scripts use `IF NOT EXISTS` checks
- This is expected and safe
- The script will skip creating existing tables

### EF Core can't find DbContext
Make sure you specify the startup project:
```bash
dotnet ef database update --project src/SharePointPermissionSync.Data --startup-project src/SharePointPermissionSync.Web
```

### Connection string issues
Add the connection string as a parameter:
```bash
dotnet ef database update --project src/SharePointPermissionSync.Data --connection "Server=localhost;Database=ScyneShare;Trusted_Connection=True;TrustServerCertificate=True;"
```

---

## Next Steps

After setting up the database:

1. ✅ Verify tables exist
2. ✅ Test connection from Web application
3. ✅ Test connection from Worker service
4. ✅ Start building the CSV upload functionality
