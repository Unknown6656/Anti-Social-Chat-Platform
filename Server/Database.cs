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
    /// <summary>
    /// Represents the underlying database
    /// </summary>
    public sealed class Database
        : IDisposable
    {
        internal const string MESSG = "[Messages]";
        internal const string CHATS = "[Chats]";
        internal const string USERS = "[Users]";
        internal const string DB = "[dbo]";

        private static Database instance;
        private bool disposed = false;


        /// <summary>
        /// Returns an instance of the currently used database
        /// </summary>
        public static Database Instance => instance = instance ?? new Database();
        /// <summary>
        /// Returns the number of sent messages
        /// </summary>
        public long MessageCount => Count(MESSG);
        /// <summary>
        /// Returns the number of (currently used) chats/groups
        /// </summary>
        public long ChatCount => Count(CHATS);
        /// <summary>
        /// Returns the number of registered users
        /// </summary>
        public long UserCount => Count(USERS);
        /// <summary>
        /// Returns the underlying T-SQL connection
        /// </summary>
        public SqlConnection Connection => DatabaseHelper.Connection;

        // TODO : connection stuff, query stuff etc.

        private Database()
        {
            if ((instance != null) || !(instance?.disposed ?? true))
                throw new InvalidOperationException($"An instance of this database already exists in the current application. It is accessible via the member `{typeof(Database).FullName}.{nameof(Instance)}`");
        }

        /// <summary>
        /// Disposes the current database instance and releases all underlying resources
        /// </summary>
        public void Dispose()
        {
            disposed = true;

            Disconnect();
        }

        /// <summary>
        /// Creates a new predefined table associated with the given table name
        /// </summary>
        /// <param name="tablename">Table name</param>
        public void CreateNew(string tablename) => ExecuteVoid(GetScript($"dbo.{tablename}"));

        /// <summary>
        /// Returns, whether the table associated with the given table name exists
        /// </summary>
        /// <param name="tablename">Table name</param>
        /// <returns>Existence information</returns>
        public bool Exists(string tablename)
        {
            int res = (int)Execute(GetScript("CheckExistence", tablename)).First()[0];

            return res != 0;
        }

        /// <summary>
        /// Clears the internal database and deletes all tables
        /// </summary>
        /// <param name="confirmation">Optional confirmation handler</param>
        public void ClearAll(Func<bool> confirmation = null)
        {
            if ((confirmation ?? (() => true))())
                ExecuteVoid($"DROP DATABASE {DB};");
        }

        private long Count(string table) => (long)Execute($"SELECT COUNT([ID]) FROM {table}").First()[0];


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

    /// <summary>
    /// 
    /// </summary>
    [Serializable]
    public sealed class DBUser
    {
        /// <summary>
        /// 
        /// </summary>
        public long ID { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string Status { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public bool IsAdmin { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public bool IsBlocked { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    [Serializable]
    public sealed class DBMessage
    {
        /// <summary>
        /// 
        /// </summary>
        public long ID { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public long SenderID { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public DateTime Date { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public bool IsSecured { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public bool IsDelivered { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public bool IsRead { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string SenderIP { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string Content { get; set; }
    }
}
