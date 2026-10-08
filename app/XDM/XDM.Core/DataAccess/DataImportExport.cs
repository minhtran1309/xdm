using System;
#if OSX
using Microsoft.Data.Sqlite;
using SQLiteConnection = Microsoft.Data.Sqlite.SqliteConnection;
using SQLiteCommand = Microsoft.Data.Sqlite.SqliteCommand;
#else
using System.Data.SQLite;
#endif
using System.IO;
using TraceLog;

namespace XDM.Core.DataAccess
{
    public static class DataImportExport
    {
        public static bool CopyToFile(SQLiteConnection sql, string file)
        {
            try
            {
#if OSX
                // Microsoft.Data.Sqlite doesn't have BackupDatabase, use ATTACH/DETACH instead
                using var cmd = new SQLiteCommand($"ATTACH '{file}' as backup", sql);
                cmd.ExecuteNonQuery();
                using var copyCmd = new SQLiteCommand("CREATE TABLE backup.downloads AS SELECT * FROM downloads", sql);
                copyCmd.ExecuteNonQuery();
                using var detachCmd = new SQLiteCommand("DETACH backup", sql);
                detachCmd.ExecuteNonQuery();
#else
                var cs = $"URI=file:{file}";
                if (!File.Exists(file))
                {
                    SQLiteConnection.CreateFile(file);
                }
                using var dest = new SQLiteConnection(cs);
                dest.Open();
                sql.BackupDatabase(dest, "main", "main", -1, null, 0);
#endif
                return true;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, ex.Message);
                return false;
            }
        }

        public static bool CopyFromFile(SQLiteConnection sql, string file)
        {
            try
            {
                using var attachCmd = new SQLiteCommand($"ATTACH '{file}' as db", sql);
                attachCmd.ExecuteNonQuery();
                var tx = sql.BeginTransaction();
                try
                {
                    using var mergeCmd = new SQLiteCommand($"INSERT OR IGNORE INTO downloads SELECT * FROM db.downloads", sql);
                    mergeCmd.ExecuteNonQuery();
                    tx.Commit();
                }
                catch (Exception ex)
                {
                    tx.Rollback();
                    Log.Debug("Error during merge insert, performing rollback!!");
                    Log.Debug(ex, ex.Message);
                }
                using var detachCmd = new SQLiteCommand($"DETACH db", sql);
                detachCmd.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, ex.Message);
                return false;
            }
        }

        //public bool CopyDB(SQLiteConnection sourceDB, SQLiteConnection targetDB, )
        //{
        //    try
        //    {
        //        using var attachCmd = new SQLiteCommand($"ATTACH '{file}' as db", db);
        //        attachCmd.ExecuteNonQuery();
        //        var tx = db.BeginTransaction();
        //        try
        //        {
        //            using var mergeCmd = new SQLiteCommand($"INSERT OR IGNORE INTO downloads SELECT * FROM db.downloads", db);
        //            mergeCmd.ExecuteNonQuery();
        //            tx.Commit();
        //        }
        //        catch (Exception ex)
        //        {
        //            tx.Rollback();
        //            Log.Debug("Error during merge insert, performing rollback!!");
        //            Log.Debug(ex, ex.Message);
        //        }
        //        using var detachCmd = new SQLiteCommand($"DETACH db", db);
        //        detachCmd.ExecuteNonQuery();
        //        return true;
        //    }
        //    catch (Exception ex)
        //    {
        //        Log.Debug(ex, ex.Message);
        //        return false;
        //    }
        //}
    }
}
