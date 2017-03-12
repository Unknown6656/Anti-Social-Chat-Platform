using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Reflection;
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
        internal const string UAUTH = "[UserAuthentifications]";
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
        /// Returns the number of registered administrators
        /// </summary>
        public long AdminCount => GetAdmins().Length;
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
        /// Adds the given user to the database
        /// </summary>
        /// <param name="user">ASC user</param>
        public DBUserAuthentification AddUser(ref DBUser user)
        {
            user.ID = NextID(USERS);

            DBUserAuthentification auth = new DBUserAuthentification
            {
                ID = user.ID,
                Salt = Authentification.GenerateSaltString(),
            };

            if (!ValidateUser(user))
                return null;

            ExecuteVoid($"INSERT INTO {DB}.{USERS} ([ID], [Name], [Status], [IsAdmin], [IsBlocked], [UUID]) VALUES ({user.ID}, '{user.Name}', '{user.Status}', {(user.IsAdmin ? 1 : 0)}, {(user.IsBlocked ? 1 : 0)}, NEWID())");
            ExecuteVoid($"INSERT INTO {DB}.{UAUTH} ([ID], [Hash], [Salt], [Session], [LastIP], [LastLogin], [LastUserAgent]) VALUES ({user.ID}, '', '{auth.Salt}', NULL, NULL, NULL, NULL)");

            user = GetUser(user.ID); // update user

            return auth;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public bool UpdateUser(DBUser user)
        {
            if (!ValidateUser(user))
                return false;

            ExecuteVoid($@"UPDATE {USERS} SET [Name]='{user.Name}',[Status]='{user.Status}',[IsAdmin]='{(user.IsAdmin ? 1 : 0)}',[IsBlocked]='{(user.IsBlocked ? 1 : 0)}'");

            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="hash"></param>
        /// <returns></returns>
        public bool UpdateUserHash(long id, string hash)
        {
            if (!ASCServer.regex(hash, @"^[a-fA-F0-9]+$", out _))
                return false;

            ExecuteVoid($"UPDATE {UAUTH} SET [Hash]='{hash}' WHERE [ID]={id}");

            return true;
        }

        /// <summary>
        /// Returns a list of all registered and unblocked admins
        /// </summary>
        /// <returns>List of all admins</returns>
        public DBUser[] GetAdmins() => Execute<DBUser>($"SELECT * FROM {USERS} WHERE [IsAdmin] = 1 AND [IsBlocked] = 0").ToArray();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="hash"></param>
        /// <returns></returns>
        public bool VerifyUser(long id, string hash) => ASCServer.regex(hash, @"^[a-fA-F0-9]+$", out _) ? Execute($"SELECT 1 FROM {UAUTH} WHERE [ID] = {id} AND [Hash] = '{hash}'").Any() : false;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public string GetUserSalt(long id) => Execute($"SELECT [Salt] FROM {UAUTH} WHERE [ID] = {id}").First()[0] as string;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public DBUserAuthentification GetAuth(long id) => Execute<DBUserAuthentification>($"SELECT * FROM {UAUTH} WHERE [ID] = {id}").First();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public DBUser GetUser(long id) => Execute<DBUser>($"SELECT * FROM {USERS} WHERE [ID] = {id}").First();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        public DBUser GetUser(Guid guid) => Execute<DBUser>($"SELECT * FROM {USERS} WHERE CONVERT(NVARCHAR(255), [UUID]) = N'{guid:D}'").First();

        public (DBUser, double)[] FindUsers(string name)
        {
            var match = Execute<DBUserComparison>($"SELECT u.*, SOUNDEX('{name}', u.[Name]) AS Soundex, DIFFERENCE('{name}', u.[Name]) as Difference FROM {USERS} AS u WHERE Difference < 3");
            var c = match.Count();
            
            return null;
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

        internal bool ValidateUser(DBUser user) => ASCServer.regex(user.Name ?? "", @"^[\w\-\. ]+$", out _) &&
                                                   ASCServer.regex(user.Status ?? "", @"^[^\'\""\=\`\´]+$", out _);

        internal long NextID(string table) => (long)Execute(GetScript("NextID", table)).First()[0];

        private long Count(string table) => (long)Execute($"SELECT COUNT([ID]) FROM {table}").First()[0];

        /// <summary>
        /// Creates a generic ASC data type instance from the given data record
        /// </summary>
        /// <typeparam name="T">ASC data type</typeparam>
        /// <param name="record">Data record</param>
        /// <returns>ASC data type instance</returns>
        public static T Create<T>(IDataRecord record)
            where T : IDBType, new()
        {
            Type t = typeof(T);
            T res = new T();

            List<PropertyInfo> props = new List<PropertyInfo>();

            while ((t != null) && (t != typeof(object)))
            {
                props.AddRange(t.GetProperties(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public));
                t = t.BaseType;
            }

            foreach (PropertyInfo nfo in props)
                nfo.SetValue(res, record[nfo.Name]);

            return res;
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
                        conn?.State != ConnectionState.Connecting ||
                        conn == null)
                    {
                        Disconnect();

                        string path = $@"{Directory.GetCurrentDirectory()}\{Name}";

                        if (!File.Exists(path))
                        {
                            "**CRITICAL ERROR:** The required database could not be found. It will be re-created.".Err();

                            CreateSqlDatabase(path);
                        }

                        conn = new SqlConnection($@"Data Source=(LocalDB)\MSSQLLocalDB;
                                                AttachDbFilename={path};
                                                Integrated Security=True;
                                                Connect Timeout=30;");
                        conn.Open();
                    }

                    return conn;
                }
            }

            internal static void CreateSqlDatabase(string path)
            {
                string name = Path.GetFileNameWithoutExtension(path);

                $"Creating database '{path}' ...".Msg();

                using (SqlConnection conn = new SqlConnection(@"Data Source=(LocalDB)\MSSQLLocalDB;
                                                                Initial Catalog=tempdb;
                                                                Integrated Security=True;
                                                                Connect Timeout=30;"))
                {
                    conn.Open();

                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = $"CREATE DATABASE [dbo] ON PRIMARY (NAME='dbo', FILENAME='{path}')";
                        cmd.ExecuteNonQuery();

                        cmd.CommandText = $"EXEC sp_detach_db 'dbo', 'true'";
                        cmd.ExecuteNonQuery();
                    }
                }

                $"Database '{name}' created.".Ok();
            }

            internal static void Disconnect()
            {
                conn?.Close();
                conn?.Dispose();
                conn = null;
            }

            internal static string Wrap(string sql) => $"BEGIN TRANSACTION; {sql}; COMMIT TRANSACTION;";

            internal static void ExecuteVoid(string sql)
            {
                using (SqlCommand cmd = new SqlCommand(Wrap(sql), Connection))
                    cmd.ExecuteNonQuery();
            }

            internal static IEnumerable<T> Execute<T>(string sql)
                where T : IDBType, new()
            {
                using (SqlCommand cmd = new SqlCommand(Wrap(sql), Connection))
                using (SqlDataReader rd = cmd.ExecuteReader())
                    while (rd.Read())
                        yield return Create<T>(rd);
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
    /// Provides basic authentification functions
    /// </summary>
    public static unsafe class Authentification
    {
        internal const int SALT_SIZE = 0x40;
        private static RNGCryptoServiceProvider rng;


        /// <summary>
        /// Starts the underlying RNG service provider
        /// </summary>
        public static void Start()
        {
            Stop();

            rng = new RNGCryptoServiceProvider();

            "Started the cryprographic RNG serive provider".Ok();
        }

        /// <summary>
        /// Stops the underlying RNG service provider
        /// </summary>
        public static void Stop()
        {
            if (rng != null)
            {
                rng.Dispose();
                rng = null;

                "Stopped the cryprographic RNG serive provider".Ok();
            }
        }

        /// <summary>
        /// Generates a 512-Bit salt
        /// </summary>
        /// <returns>512-Bit salt (64 Bytes)</returns>
        public static byte[] GenerateSalt()
        {
            byte[] bytes = new byte[SALT_SIZE];

            rng.GetBytes(bytes);

            $"Salt '{string.Join("", from b in bytes select b.ToString("x2"))}' generated.".Msg();

            return bytes;
        }

        /// <summary>
        /// Generates a 128-character hexadecimal salt string
        /// </summary>
        /// <returns>Salt string</returns>
        public static string GenerateSaltString()
        {
            StringBuilder res = new StringBuilder();
            byte[] salt = GenerateSalt();

            fixed (byte* ptr = salt)
                for (int i = 0; i < SALT_SIZE / sizeof(ulong); i++)
                    res.Append($"{*((ulong*)ptr + i):x16}");

            return res.ToString();
        }
    }

    /// <summary>
    /// Represents an ASC data type
    /// </summary>
    public interface IDBType
    {
    }

    [Serializable]
    internal sealed class DBUserComparison
        : IDBType
    {
        internal string Soundex { set; get; }
        internal int Difference { set; get; }
    }

    /// <summary>
    /// Represents an ASC user
    /// </summary>
    [Serializable]
    public class DBUser
        : IDBType
    {
        /// <summary>
        /// The user's ID
        /// </summary>
        public long ID { get; set; } = 0L;
        /// <summary>
        /// The user's Name
        /// </summary>
        public string Name { get; set; } = "user";
        /// <summary>
        /// The user's Status
        /// </summary>
        public string Status { get; set; } = "status";
        /// <summary>
        /// Determines, whether the User is an admin
        /// </summary>
        public bool IsAdmin { get; set; } = false;
        /// <summary>
        /// Determines, whether the User is blocked
        /// </summary>
        public bool IsBlocked { get; set; } = true;
        /// <summary>
        /// The user's GUID
        /// </summary>
        public Guid UUID { get; set; } = Guid.Empty;
    }

    /// <summary>
    /// Represents an ASC message
    /// </summary>
    [Serializable]
    public sealed class DBMessage
        : IDBType
    {
        /// <summary>
        /// The message's ID
        /// </summary>
        public long ID { get; set; }
        /// <summary>
        /// The message's sender ID
        /// </summary>
        public long SenderID { get; set; }
        /// <summary>
        /// The message's timestamp
        /// </summary>
        public DateTime Date { get; set; }
        /// <summary>
        /// Determines, whether the message is secured
        /// </summary>
        public bool IsSecured { get; set; }
        /// <summary>
        /// Determines, whether the message has been delivered
        /// </summary>
        public bool IsDelivered { get; set; }
        /// <summary>
        /// Determines, whether the message has been read
        /// </summary>
        public bool IsRead { get; set; }
        /// <summary>
        /// The message's sender IP
        /// </summary>
        public string SenderIP { get; set; }
        /// <summary>
        /// The message's content
        /// </summary>
        public string Content { get; set; }
    }

    /// <summary>
    /// Represents an ASC user authentification information set
    /// </summary>
    [Serializable]
    public sealed class DBUserAuthentification
        : IDBType
    {
        /// <summary>
        /// The user's ID
        /// </summary>
        public long ID { get; set; }
        /// <summary>
        /// The user's Hash (SHA-512)
        /// </summary>
        public string Hash { get; set; }
        /// <summary>
        /// The user's salt
        /// </summary>
        public string Salt { get; set; }
        /// <summary>
        /// The user's session ID/string
        /// </summary>
        public string Session { get; set; }
        /// <summary>
        /// The user's last login IP
        /// </summary>
        public string LastIP { get; set; }
        /// <summary>
        /// The user's last login date
        /// </summary>
        public DateTime? LastLogin { get; set; }
        /// <summary>
        /// The user's last login user agent
        /// </summary>
        public string LastUserAgent { get; set; }
    }
}
