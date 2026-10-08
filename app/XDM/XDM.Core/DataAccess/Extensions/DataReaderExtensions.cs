using System;
using System.Collections.Generic;
#if OSX
using Microsoft.Data.Sqlite;
using SQLiteDataReader = Microsoft.Data.Sqlite.SqliteDataReader;
#else
using System.Data.SQLite;
#endif
using System.Linq;
using System.Text;

namespace XDM.Core.DataAccess.Extensions
{
    public static class DataReaderExtensions
    {
        public static string GetSafeString(this SQLiteDataReader r, int index)
        {
            if (!r.IsDBNull(index))
            {
                return r.GetString(index);
            }
#pragma warning disable CS8603 // Possible null reference return.
            return null;
#pragma warning restore CS8603 // Possible null reference return.
        }
    }
}
