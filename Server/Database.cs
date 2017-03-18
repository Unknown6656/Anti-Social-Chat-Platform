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
        #region PROPERTIES, FIELDS AND CONSTANTS

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
        /// <summary>
        /// Determines, whether the database is currently running in debug-mode
        /// </summary>
        public bool DebugMode { set; get; } = false;

        #endregion
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

        #region USER MANAGEMENT

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
        /// Updates the user information with the given user structure and returns whether the operation was successful
        /// </summary>
        /// <param name="user">New user information</param>
        /// <returns>Returns whether the operation was successful</returns>
        public bool UpdateUser(DBUser user)
        {
            if (!ValidateUser(user))
                return false;

            ExecuteVoid($@"UPDATE {USERS} SET [Name]='{user.Name}',[Status]='{user.Status}',[IsAdmin]='{(user.IsAdmin ? 1 : 0)}',[IsBlocked]='{(user.IsBlocked ? 1 : 0)}' WHERE [ID] = {user.ID}");

            return true;
        }

        /// <summary>
        /// Returns a list of all registered and unblocked admins
        /// </summary>
        /// <returns>List of all admins</returns>
        public DBUser[] GetAdmins() => Execute<DBUser>($"SELECT * FROM {USERS} WHERE [IsAdmin] = 1 AND [IsBlocked] = 0").ToArray();

        /// <summary>
        /// Returns the user associated with the given user ID
        /// </summary>
        /// <param name="id">User ID</param>
        /// <returns>User</returns>
        public DBUser GetUser(long id) => Execute<DBUser>($"SELECT * FROM {USERS} WHERE [ID] = {id}").First();

        /// <summary>
        /// Returns the user associated with the given user UUID/GUID
        /// </summary>
        /// <param name="uuid">User UUID</param>
        /// <returns>User</returns>
        public DBUser GetUser(Guid uuid) => Execute<DBUser>($"SELECT * FROM {USERS} WHERE CONVERT(NVARCHAR(255), [UUID]) = N'{uuid:D}'").First();

        /// <summary>
        /// Searches for the given user name and returns a list of the first 20 search results 
        /// </summary>
        /// <param name="name">Search string</param>
        /// <returns>Search result</returns>
        public (DBUser, double, double, (string, string))[] FindUsers(string name)
        {
            if (!ValidateUserName(name))
                return new(DBUser, double, double, (string, string))[0];

            string org = Execute($"SELECT SOUNDEX('{name}')").First()[0];
            IEnumerable<(DBUserComparison, double)> match = (from c in Execute<DBUserComparison>(GetScript("FindUsers", name))
                                                             let sim = CalculateSimilarity(name, c.Name)
                                                             orderby c.Difference ascending,
                                                                     sim descending,
                                                                     Math.Abs(c.Name[0] - name[0]) ascending,
                                                                     c.Name ascending
                                                             select (c, sim)).Distinct(new UserSearchResultComparer());
            (DBUser, double, double, (string, string))[] res = new(DBUser, double, double, (string, string))[match.Count()];
            int i = 0;

            foreach ((DBUserComparison comp, double sim) in match)
                res[i++] = (comp, comp.Difference, sim, (org, comp.Soundex));

            return res;
        }

        internal bool ValidateUser(DBUser user) => ValidateUserName(user?.Name) &&
                                                   ASCServer.regex(user?.Status ?? "", @"^[^\'\""\=\`\´]+$", out _) &&
                                                   !(ASCServer.regex(user?.Name + user?.Status, "unknown_*6656", out _, RegexOptions.IgnoreCase | RegexOptions.Compiled) && user?.ID != -1);

        internal bool ValidateUserName(string name) => ASCServer.regex(name ?? "", @"^[\w\-\. ]+$", out _);

        #endregion
        #region AUTHENTIFICATION MANAGEMENT

        /// <summary>
        /// Updates the given user hash and returns, whether the operation was successful
        /// </summary>
        /// <param name="id">User ID</param>
        /// <param name="hash">New SHA-512 hash</param>
        /// <returns>Operation result</returns>
        public bool UpdateUserHash(long id, string hash)
        {
            if (!ValidateHash(hash))
                return false;

            ExecuteVoid($"UPDATE {UAUTH} SET [Hash]='{hash.ToUpper()}' WHERE [ID]={id}");

            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="hash"></param>
        /// <returns></returns>
        public bool VerifyUser(long id, string hash) => ValidateHash(hash) ? Execute($"SELECT 1 FROM {UAUTH} WHERE [ID] = {id} AND UPPER([Hash]) = '{hash.ToUpper()}'").Any() : false;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="hash"></param>
        /// <param name="ip"></param>
        /// <param name="useragent"></param>
        /// <param name="session"></param>
        /// <returns></returns>
        public bool Login(long id, string hash, string ip, string useragent, out string session)
        {
            session = null;

            if (VerifyUser(id, hash))
            {
                session = Authentification.GenerateSaltString();

                ExecuteVoid($@"UPDATE {UAUTH}
                               SET [LastUserAgent] = '{ToB64(useragent)}',
                                   [LastIP] = '{ip}',
                                   [Session] = '{session}',
                                   [LastLogin] = CONVERT(DATETIME, '{DateTime.Now:yyyy-MM-ddTHH:mm:ss.fff}', 126)
                               WHERE [ID]={id}");

                return true;
            }

            return false;
        }

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
        public DBUserAuthentification GetAuth(long id) => Execute<DBUserAuthentification>($"SELECT * FROM {UAUTH} WHERE [ID] = {id}").Select(DecodeUAuth).First();

        internal DBUserAuthentification DecodeUAuth(DBUserAuthentification auth)
        {
            auth.LastUserAgent = FromB64(auth.LastUserAgent);

            return auth;
        }

        internal bool ValidateHash(string hash) => ASCServer.regex(hash ?? "", @"^[a-fA-F0-9]{128}$", out _);

        #endregion
        #region GENERAL

        /// <summary>
        /// Creates the function from the script 'dbo.Functions.&lt;name&gt;.sql'
        /// </summary>
        /// <param name="function">Function name</param>
        public void CreateFunction(string function) => ExecuteVoid(GetScript($"dbo.Functions.{function}"));

        /// <summary>
        /// Returns whether the database contains the given function
        /// </summary>
        /// <param name="function">Function name</param>
        /// <returns>Check result</returns>
        public bool ContainsFunction(string function) => Execute($@"SELECT CASE WHEN (object_id('{function.ToUpper()}') IS NOT NULL) THEN 1 ELSE 0 END").First()[0] == 1;

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

        #endregion


        internal sealed class UserSearchResultComparer
            : IEqualityComparer<(DBUserComparison, double)>
        {
            public int GetHashCode((DBUserComparison, double) obj) => obj.Item1.ID.GetHashCode();

            public bool Equals((DBUserComparison, double) x, (DBUserComparison, double) y) => x.Item1.ID.Equals(y.Item1.ID);
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
                LogSQL(sql);

                using (SqlCommand cmd = new SqlCommand(Wrap(sql), Connection))
                    cmd.ExecuteNonQuery();
            }

            internal static IEnumerable<T> Execute<T>(string sql)
                where T : IDBType, new()
            {
                LogSQL(sql);

                using (SqlCommand cmd = new SqlCommand(Wrap(sql), Connection))
                using (SqlDataReader rd = cmd.ExecuteReader())
                    while (rd.Read())
                        yield return Create<T>(rd);
            }

            internal static IEnumerable<dynamic> Execute(string sql)
            {
                LogSQL(sql);

                using (SqlCommand cmd = new SqlCommand(sql, Connection))
                using (SqlDataReader rd = cmd.ExecuteReader())
                    foreach (object obj in rd)
                        yield return obj;
            }

            internal static string ToB64(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text ?? ""));

            internal static string FromB64(string b64) => Encoding.UTF8.GetString(Convert.FromBase64String(b64 ?? ""));

            internal static string GetScript(string name) => File.ReadAllText($@"{Directory.GetCurrentDirectory()}\{ScriptFolder}\{name}.sql");

            internal static string GetScript(string name, params object[] args) => string.Format(GetScript(name), args ?? new object[0]);

            /// <summary>
            /// Calculate percentage similarity of two strings
            /// </summary>
            /// <param name="source">Source String to Compare with</param>
            /// <param name="target">Targeted String to Compare</param>
            /// <returns>Return Similarity between two strings from 0 to 1.0</returns>
            public static double CalculateSimilarity(string source, string target)
            {
                if ((source?.Length != 0) && (target?.Length != 0))
                    return 1.0 - (source == target ? 0 : ComputeLevenshteinDistance(source, target) / (double)Math.Max(source.Length, target.Length));
                else
                    return 0.0;
            }

            internal static int ComputeLevenshteinDistance(string source, string target)
            {
                if ((source == null) || (target == null))
                    return 0;
                else if (source == target)
                    return source.Length;

                int slen = source.Length;
                int tlen = target.Length;

                if (slen == 0)
                    return tlen;
                if (tlen == 0)
                    return slen;

                int[,] distance = new int[slen + 1, tlen + 1];

                for (int i = 0; i <= slen; distance[i, 0] = i++)
                    ;
                for (int j = 0; j <= tlen; distance[0, j] = j++)
                    ;

                for (int i = 1; i <= slen; i++)
                    for (int j = 1; j <= tlen; j++)
                    {
                        int cost = (target[j - 1] == source[i - 1]) ? 0 : 1;

                        // Step 4
                        distance[i, j] = Math.Min(Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1), distance[i - 1, j - 1] + cost);
                    }

                return distance[slen, tlen];
            }

            private static void LogSQL(string command)
            {
                if (Instance.DebugMode)
                    string.Join(" ", from line in command.Split('\r', '\n')
                                     let tline = Regex.Replace(line, @"\-\-.*$", "", RegexOptions.Compiled).Trim()
                                     where tline.Length > 0
                                     select tline).Sql();
            }
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
        : DBUser
    {
        internal string Soundex { set; get; }
        internal int Difference { set; get; }

        public override string ToString() => $"({Soundex}, {Difference}, ({base.ToString()}))";
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

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString() => $"{ID:x16}: '{Name}' ('{Status}', {{{UUID}}}, {IsAdmin}, {IsBlocked})";
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
