using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.IO;
using System;

using static ASC.Server.Database.DatabaseHelper;

namespace ASC.Server
{
    public sealed class Database
        : IDisposable
    {
        private static Database instance;
        private bool disposed = false;

        public static Database Instance => instance = instance ?? new Database();
        public long ChatCount => (long)Execute("SELECT COUNT([ID]) FROM [Chats]").First()[0];
        public long MessageCount => (long)Execute("SELECT COUNT([ID]) FROM [Messages]").First()[0];
        public long UserCount => (long)Execute("SELECT COUNT([ID]) FROM [Users]").First()[0];
        public SqlConnection Connection => DatabaseHelper.Connection;

        // TODO : connection stuff, query stuff etc.

        private Database()
        {
            if ((instance != null) || !(instance?.disposed ?? true))
                throw new InvalidOperationException($"An instance of this database already exists in the current application. It is accessible via the member `{typeof(Database).FullName}.{nameof(Instance)}`");
        }

        public void Dispose()
        {
            disposed = true;

            Disconnect();
        }

        public void CreateNew(string tablename) => ExecuteVoid(GetScript($"dbo.{tablename}"));

        public bool Exists(string tablename)
        {
            int res = (int)Execute(GetScript("CheckExistence", tablename)).First()[0];

            return res != 0;
        }

        public void ClearAll(Func<bool> confirmation = null)
        {
            if ((confirmation ?? (() => true))())
                ExecuteVoid(GetScript("DeleteAll"));
        }

        internal static class DatabaseHelper
        {
            internal const string ScriptFolder = "SQLScripts";
            internal const string Name = "database.mdf";

            private static SqlConnection conn;

            internal static SqlConnection Connection
            {
                get
                {
                    if (conn?.State != ConnectionState.Open ||
                        conn?.State != ConnectionState.Fetching ||
                        conn?.State != ConnectionState.Connecting)
                    {
                        Disconnect();

                        conn = new SqlConnection($@"Data Source=(LocalDB)\MSSQLLocalDB;
                                                AttachDbFilename={Directory.GetCurrentDirectory()}\{Name};
                                                Integrated Security=True;
                                                Connect Timeout=30");
                        conn.Open();
                    }

                    return conn;
                }
            }

            internal static void Disconnect()
            {
                conn?.Close();
                conn?.Dispose();
                conn = null;
            }

            internal static void ExecuteVoid(string sql)
            {
                using (SqlCommand cmd = new SqlCommand(sql, Connection))
                    cmd.ExecuteNonQuery();
            }

            internal static IEnumerable<dynamic> Execute(string sql)
            {
                using (SqlCommand cmd = new SqlCommand(sql, Connection))
                using (SqlDataReader rd = cmd.ExecuteReader())
                    foreach (object obj in rd)
                        yield return obj;
            }

            internal static string GetScript(string name) => File.ReadAllText($@"{Directory.GetCurrentDirectory()}\{ScriptFolder}\{name}.sql");

            internal static string GetScript(string name, params object[] args) => string.Format(GetScript(name), args ?? new object[0]);
        }
    }
}
