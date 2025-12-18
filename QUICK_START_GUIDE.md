# SharePoint Permission Sync - Quick Start Guide

**Last Updated:** December 18, 2024
**Status:** Worker Service Running ✅ | Web UI Ready to Start ⏳

---

## 🚀 **Quick Start: Get Running in 5 Minutes**

### **Current Status**
- ✅ Worker Service is already running in background
- ✅ RabbitMQ is operational
- ✅ Database is ready
- ✅ Test CSV file created
- ⏳ Web UI needs manual start (see below)

---

## **Step 1: Start the Web Application**

### **Option A: Using PowerShell (Recommended)**

1. **Open a new PowerShell window**
   - Press `Win + X` and select "Windows PowerShell" or "Terminal"

2. **Navigate to the Web project**
   ```powershell
   cd C:\DEV\ShareSync\src\SharePointPermissionSync.Web
   ```

3. **Run the application**
   ```powershell
   dotnet watch run
   ```

4. **Wait for startup messages**
   You should see:
   ```
   [INFO] SharePoint Permission Sync Web Portal starting...
   [INFO] Now listening on: https://localhost:7242
   [INFO] Now listening on: http://localhost:5157
   [INFO] Application started. Press Ctrl+C to shut down.
   ```

5. **Note the HTTPS URL** (usually `https://localhost:7242`)

---

### **Option B: Using Command Prompt**

1. **Open Command Prompt**
   - Press `Win + R`, type `cmd`, press Enter

2. **Navigate and run**
   ```cmd
   cd C:\DEV\ShareSync\src\SharePointPermissionSync.Web
   dotnet run
   ```

---

### **If you see an HTTPS certificate warning:**

The first time you run, you might see:
```
HTTPS development certificate not found. Generating certificate...
Trust the certificate? [y/n]
```

**Type `y` and press Enter** to trust the development certificate.

---

## **Step 2: Open the Web Portal**

1. **Open your browser** (Chrome, Edge, Firefox)

2. **Navigate to:** `https://localhost:7242`

3. **If you see a security warning:**
   - Chrome: Click "Advanced" → "Proceed to localhost"
   - Edge: Click "Advanced" → "Continue to localhost"
   - Firefox: Click "Advanced" → "Accept the Risk"

4. **You should see the Jobs page!**
   - Title: "SharePoint Permission Sync"
   - Navigation: Jobs, Operations
   - Empty job list (initially)

---

## **Step 3: Upload Test CSV**

### **Using the Test File Already Created**

1. **In the Web UI, click "Upload Permissions"** (top-right button)

2. **Fill in the form:**
   - **Environment:** Select `DEV`
   - **Site URL:** Enter: `https://scyneadvisory.sharepoint.com/sites/TestSite`
   - **CSV File:** Click "Choose File" and select:
     ```
     C:\DEV\ShareSync\test-permissions.csv
     ```

3. **Click "Upload and Process"**

4. **You'll be redirected to the Job Details page**

---

## **Step 4: Watch Real-Time Processing**

### **What You'll See:**

1. **Job Information Card**
   - Job ID, Type, Status
   - Environment: DEV
   - Site URL
   - Uploaded by: [Your username]

2. **Progress Card**
   - Progress bar updating every 5 seconds
   - Total Items: 3
   - Completed: [increasing]
   - Failed: [hopefully 0]

3. **Job Items Table**
   - Individual rows for each CSV item
   - Status changing: Pending → Processing → Completed (or Failed)
   - Retry counts
   - Error messages (if any)

### **Expected Timeline:**
- Page refreshes every 5 seconds automatically
- Worker processes ~500ms per item (with throttling)
- 3 items should complete in ~10-15 seconds

---

## **Step 5: Verify Everything Worked**

### **In the Web UI:**

1. **Check Job Status**
   - Should change from "Queued" → "Processing" → "Completed"
   - Progress bar should reach 100%

2. **Check Item Details**
   - All 3 items should show "Completed" (green badge)
   - No error messages

### **In the Database:**

```sql
-- Check the job
SELECT
    JobId, JobType, Status, TotalItems, ProcessedItems, FailedItems,
    CreatedAt, CompletedAt
FROM ScyneShare.ProcessingJobs
ORDER BY CreatedAt DESC;

-- Check the items
SELECT
    ItemIdentifier, Status, RetryCount, ErrorMessage, ProcessedAt
FROM ScyneShare.ProcessingJobItems
WHERE JobId = '[YourJobId]';
```

### **In RabbitMQ Management UI:**

1. Open: http://localhost:15672
2. Login: `guest` / `guest`
3. Click "Queues" tab
4. You should see:
   - `sharepoint.interaction.permissions` - 0 messages (processed)
   - `sharepoint.interaction.creation` - 0 messages
   - `sharepoint.deadletter` - 0 messages (no failures)

---

## **Troubleshooting**

### **Problem: Web app won't start**

**Solution:**
```powershell
# Check if port is already in use
netstat -ano | findstr :7242

# If something is using it, either:
# 1. Kill that process, or
# 2. Change the port in launchSettings.json
```

---

### **Problem: "Cannot connect to database"**

**Check database connection:**
```powershell
sqlcmd -S localhost -d ScyneShareDEV -E -Q "SELECT 1"
```

**If fails:** Check that SQL Server is running
```powershell
Get-Service | Where-Object {$_.Name -like '*SQL*'}
```

---

### **Problem: Jobs stay in "Queued" status**

**Cause:** Worker service might have stopped

**Solution:**
```powershell
# Check if Worker is still running
# Look for the PowerShell window where it's running

# Or restart it:
cd C:\DEV\ShareSync\src\SharePointPermissionSync.Worker
dotnet run
```

**You should see:**
```
[INFO] Queue Processor Worker is ready to process messages from all queues
```

---

### **Problem: All items fail with errors**

**Expected in this test!** The test CSV uses fake data that doesn't exist in SharePoint.

**What you should see:**
- Items will be marked as "Failed"
- Error message: "Interaction not found" or similar from Tecala.SMO.SharePoint

**This is OK for testing!** It proves:
- ✅ CSV upload works
- ✅ Job creation works
- ✅ Messages queued successfully
- ✅ Worker processes messages
- ✅ Database updates correctly
- ✅ Error handling works
- ✅ Retry logic works

---

## **Next Steps: Real Data Testing**

### **Create a Real CSV with Actual Data**

1. **Get real Interaction data from database:**
   ```sql
   SELECT TOP 3
       i.Id AS InteractionId,
       i.ProjectId,
       p.EngagementId,
       i.SharePointFolderID AS SharePointFolderId
   FROM ScyneShare.Interaction i
   JOIN ScyneShare.Project p ON i.ProjectId = p.Id
   WHERE i.SharePointFolderID IS NOT NULL
   AND i.SharePointFolderID > 0;
   ```

2. **Create CSV from results:**
   ```csv
   InteractionId,ProjectId,EngagementId,SharePointFolderId,InternalPermission,InternalUserEmails
   [RealId1],[RealProjectId1],[RealEngId1],[RealFolderId1],Edit,yourname@domain.com
   [RealId2],[RealProjectId2],[RealEngId2],[RealFolderId2],Read,user@domain.com
   ```

3. **Use correct site URL:**
   - For DEV: Your actual DEV SharePoint site
   - For UAT: Your UAT site
   - For PROD: Your production site (be careful!)

4. **Upload and test with real data**

---

## **Terminal Window Reference**

### **You should have TWO terminal windows running:**

#### **Terminal 1: Worker Service**
```
Location: C:\DEV\ShareSync\src\SharePointPermissionSync.Worker
Command: dotnet run
Status: Should show "Queue Processor Worker is ready to process messages"
Keep Running: YES - Leave this running
```

#### **Terminal 2: Web Application**
```
Location: C:\DEV\ShareSync\src\SharePointPermissionSync.Web
Command: dotnet watch run
Status: Should show "Now listening on: https://localhost:7242"
Keep Running: YES - Leave this running
```

### **To stop the services:**
- Press `Ctrl + C` in each terminal window
- Or close the terminal windows

---

## **Architecture Overview**

```
┌─────────────────┐
│  You (Browser)  │
└────────┬────────┘
         │ Upload CSV
         ▼
┌─────────────────────────┐
│   Web Portal (5157)     │  ← You're starting this manually
│  - Parses CSV           │
│  - Creates job in DB    │
│  - Publishes to queue   │
└──────────┬──────────────┘
           │
           ▼
    ┌─────────────┐
    │  RabbitMQ   │ ← Already running
    │   (5672)    │
    └──────┬──────┘
           │
           ▼
┌──────────────────────────┐
│  Worker Service          │ ← Already running in background!
│  - Consumes messages     │
│  - Calls SharePoint API  │
│  - Updates database      │
└──────────┬───────────────┘
           │
           ▼
    ┌─────────────┐
    │  Database   │ ← Already ready
    │ ScyneShareDEV│
    └─────────────┘
```

---

## **Success Checklist**

After following this guide, you should have:

- [ ] Worker service running in Terminal 1
- [ ] Web portal running in Terminal 2
- [ ] Web UI accessible at https://localhost:7242
- [ ] Uploaded test CSV successfully
- [ ] Seen job status change to "Processing"
- [ ] Watched progress bar update
- [ ] Seen items move from "Pending" to "Completed" or "Failed"
- [ ] Job status changed to "Completed"
- [ ] Database shows job and item records
- [ ] RabbitMQ queues processed (empty)

---

## **Common Questions**

### **Q: Do I need to restart the Worker when I upload a new CSV?**
**A:** No! The Worker stays running and automatically picks up new messages.

### **Q: Can I upload multiple CSVs at once?**
**A:** Yes! Each creates a separate job. The Worker processes them sequentially.

### **Q: What happens if I close the browser?**
**A:** The job keeps processing! Reopen the browser and go to Jobs → Details to see progress.

### **Q: Can I test in UAT or PROD?**
**A:** Yes, but change the Environment dropdown and use the correct Site URL for that environment.

### **Q: How do I stop everything?**
**A:** Press Ctrl+C in both terminal windows (Worker and Web).

---

## **Files Reference**

| File | Location | Purpose |
|------|----------|---------|
| Test CSV | `C:\DEV\ShareSync\test-permissions.csv` | Sample data for testing |
| Worker Service | `src\SharePointPermissionSync.Worker` | Background processor |
| Web Portal | `src\SharePointPermissionSync.Web` | Upload UI |
| Database Scripts | `scripts\*.sql` | Database setup |
| Code Review | `CODE_REVIEW.md` | Technical analysis |
| This Guide | `QUICK_START_GUIDE.md` | You are here! |

---

## **Getting Help**

If something doesn't work:

1. **Check the Worker terminal** - Look for error messages
2. **Check the Web terminal** - Look for exceptions
3. **Check RabbitMQ** - http://localhost:15672 (guest/guest)
4. **Check database** - Use SQL queries above
5. **Check logs** - Look in `logs/` folder in each project

---

## **Ready to Start?**

1. Open PowerShell
2. Run: `cd C:\DEV\ShareSync\src\SharePointPermissionSync.Web`
3. Run: `dotnet watch run`
4. Open browser: `https://localhost:7242`
5. Click "Upload Permissions"
6. Upload `C:\DEV\ShareSync\test-permissions.csv`
7. Watch it process!

**Good luck! 🚀**
