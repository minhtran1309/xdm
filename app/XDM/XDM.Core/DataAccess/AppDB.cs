using System;
using System.Collections.Generic;
using System.Data.Common;
#if OSX
using Microsoft.Data.Sqlite;
#else
using System.Data.SQLite;
#endif
using System.IO;
using System.Linq;
using System.Text;
using TraceLog;
using XDM.Core;
using XDM.Core.Downloader;

namespace XDM.Core.DataAccess
{
    public class AppDB
    {
        private static object lockObj = new();
        private bool init = false;
#if OSX
        private SqliteConnection db;
#else
        private SQLiteConnection db;
#endif
        private AppDB() { }
        private DownloadList downloadsDB;
        public DownloadList Downloads => downloadsDB;
        private static AppDB instance;
        public static AppDB Instance
        {
            get
            {
                lock (lockObj)
                {
                    if (instance == null)
                    {
                        instance = new AppDB();
                    }
                }
                return instance;
            }
        }

        public bool Init(string file)
        {
            lock (this)
            {
                try
                {
#if OSX
                    string cs = $"Data Source={file}";
                    db = new SqliteConnection(cs);
#else
                    string cs = $"URI=file:{file}";
                    if (!File.Exists(file))
                    {
                        SQLiteConnection.CreateFile(file);
                    }
                    db = new SQLiteConnection(cs);
#endif
                    db.Open();
                    SchemaInitializer.Init(db);
                    this.downloadsDB = new DownloadList(db);
                    init = true;
                    return true;
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, ex.Message);
                    return false;
                }
            }
        }

        public bool Export(string file)
        {
            try
            {
                DataImportExport.CopyToFile(db, file);
                return true;
            }
            catch (Exception e)
            {
                Log.Debug(e, e.Message);
                return false;
                throw;
            }
        }

        public bool Import(string file)
        {
            try
            {
                DataImportExport.CopyFromFile(db, file);
                return true;
            }
            catch (Exception e)
            {
                Log.Debug(e, e.Message);
                return false;
                throw;
            }
        }
    }
}
