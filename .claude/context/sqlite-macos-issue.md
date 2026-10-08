# SQLite Database Issue on macOS

## Problem Description

When testing the macOS build, the application failed to initialize the SQLite database, preventing all download functionality from working.

### Symptoms
- "Download Now" button does nothing when clicked
- "More" and "Download Later" buttons also non-functional
- Log shows: `[DEBUG] AppDB.Init result: False`

### Root Cause

**System.Data.SQLite.Core** package cannot load its native library (`SQLite.Interop.dll`) on macOS when using single-file publish:

```
System.DllNotFoundException: Unable to load shared library 'SQLite.Interop.dll' or one of its dependencies.
dlopen(/Applications/XDM.app/Contents/MacOS/SQLite.Interop.dll.dylib, 0x0001): tried: '/Applications/XDM.app/Contents/MacOS/SQLite.Interop.dll.dylib' (no such file)
```

The System.Data.SQLite.Core package is Windows-focused and expects Windows DLL naming conventions, which don't work correctly on macOS with single-file publish mode.

### Failed Attempt

Adding `<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>` to the project file did not resolve the issue.

## Solution

Switch from **System.Data.SQLite.Core** to **Microsoft.Data.Sqlite** package, which has proper cross-platform support including macOS.

### Implementation Strategy

Use conditional compilation with type aliases to maintain compatibility across platforms:

```csharp
#if OSX
using Microsoft.Data.Sqlite;
using SQLiteConnection = Microsoft.Data.Sqlite.SqliteConnection;
using SQLiteCommand = Microsoft.Data.Sqlite.SqliteCommand;
using SQLiteDataReader = Microsoft.Data.Sqlite.SqliteDataReader;
using SQLiteParameter = Microsoft.Data.Sqlite.SqliteParameter;
using SQLiteParameterCollection = Microsoft.Data.Sqlite.SqliteParameterCollection;
#else
using System.Data.SQLite;
#endif
```

### Files Modified

All DataAccess layer files in `XDM.Core/DataAccess/`:

1. **AppDB.cs** - Database initialization with different connection strings
2. **DownloadList.cs** - Changed from DbConnection to SQLiteConnection throughout
3. **DataReaderExtensions.cs** - Updated SQLiteDataReader usage
4. **SchemaInitializer.cs** - Changed from DbConnection to SQLiteConnection
5. **DataImportExport.cs** - Platform-specific backup implementation

### API Differences

**Microsoft.Data.Sqlite** doesn't have `BackupDatabase()` API. On macOS, database backup uses ATTACH/DETACH pattern:

```csharp
#if OSX
    using var cmd = new SQLiteCommand($"ATTACH '{file}' as backup", sql);
    cmd.ExecuteNonQuery();
    using var copyCmd = new SQLiteCommand("CREATE TABLE backup.downloads AS SELECT * FROM downloads", sql);
    copyCmd.ExecuteNonQuery();
    using var detachCmd = new SQLiteCommand("DETACH backup", sql);
    detachCmd.ExecuteNonQuery();
#else
    sql.BackupDatabase(dest, "main", "main", -1, null, 0);
#endif
```

### Connection String Differences

```csharp
// macOS (Microsoft.Data.Sqlite)
string cs = $"Data Source={file}";
db = new SqliteConnection(cs);

// Windows/Linux (System.Data.SQLite)
string cs = $"URI=file:{file}";
if (!File.Exists(file))
{
    SQLiteConnection.CreateFile(file);
}
db = new SQLiteConnection(cs);
```

## Status

**RESOLVED** - Successfully implemented and tested.

The migration to Microsoft.Data.Sqlite is complete:
- All compilation errors fixed
- Database initialization succeeds (`AppDB.Init result: True`)
- Database file created successfully at `~/Library/Application Support/XDM/downloads.db`
- Application can now create and manage downloads on macOS

## References

- NuGet: Microsoft.Data.Sqlite 9.0.0
- Related files: All files in `app/XDM/XDM.Core/DataAccess/`
- Project file: `app/XDM/XDM.Gtk.UI.macOS/XDM.Gtk.UI.macOS.csproj`
