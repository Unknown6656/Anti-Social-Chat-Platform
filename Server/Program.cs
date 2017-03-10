// #define USE_SERVICEHOST

using System.Security.Cryptography.X509Certificates;
using System.Runtime.InteropServices;
using System.ServiceModel.Security;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Net.Security;
using System.ServiceModel;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Data;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System;

namespace ASC.Server
{
    internal static class Win32
    {
        internal const string GUID = "208b519d-d6b8-44df-9bba-a5abfddb773a";
        internal const string MUTEX = "ASC_server_" + GUID;
        internal const string NAMESPACE_URI = "http://0.0.0.0:8081";
        internal const int PORT = 8080;

        internal static readonly string sys32 = $@"{Environment.GetFolderPath(Environment.SpecialFolder.Windows)}\system32";

        [DllImport("user32.dll")]
        internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("kernel32.dll")]
        internal static extern IntPtr GetConsoleWindow();
    }

    [ServiceContract(Namespace = Win32.NAMESPACE_URI), Guid(Win32.GUID)]
    public unsafe class Program
    {
        private static bool accptconnections = false;

        public static void InstallCertificates(string path, StoreName name)
        {
            using (X509Certificate cert = X509Certificate.CreateFromCertFile(path))
            {
                $"Adding certificate '{path}' ({cert.Issuer}, {cert.GetCertHashString()}) to the certificate store '{name}' ...".Msg();

                using (X509Store store = new X509Store(name, StoreLocation.LocalMachine))
                {
                    store.Open(OpenFlags.ReadWrite);

                    foreach (X509Certificate2 c in store.Certificates)
                        if (c.GetCertHashString() == cert.GetCertHashString())
                            return;

                    store.Add(new X509Certificate2(cert));
                }
            }
        }

        public static bool NeedsBinding(string ip, int port)
        {
            using (Process nstat = Process.Start(new ProcessStartInfo()
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                WorkingDirectory = Win32.sys32,
                FileName = $@"{Win32.sys32}\cmd.exe",
                Verb = "runas",
                Arguments = $@"/c ""netstat -o -n -a | find /c ""{ip}:{port}""""",
                WindowStyle = ProcessWindowStyle.Hidden,
            }))
            using (StreamReader cout = nstat.StandardOutput)
            {
                nstat.WaitForExit();

                return int.TryParse(cout.ReadToEnd(), out int val) ? val == 0 : true;
            }
        }

        public static ServiceHost BindCertificatePort(string ip, int port, StoreName store, string name)
        {
            if (NeedsBinding(ip, port))
            {
                $"Binding certificates to port {port + 1} ...".Msg();
#if USE_SERVICEHOST
                WSHttpBinding b = new WSHttpBinding();

                b.Security.Mode = SecurityMode.Transport;
                b.Security.Transport.ClientCredentialType = HttpClientCredentialType.Certificate;

                ServiceHost sh = new ServiceHost(typeof(Program), new Uri[]
                {
                    new Uri($"http://{ip}:{port}"),
                    new Uri($"https://{ip}:{port + 1}")
                });

                sh.AddServiceEndpoint(typeof(Program), b, "ASC");
                sh.Credentials.ServiceCertificate.SetCertificate(StoreLocation.LocalMachine, store, X509FindType.FindBySubjectName, name);
                sh.Credentials.ClientCertificate.Authentication.CertificateValidationMode = X509CertificateValidationMode.PeerOrChainTrust; // optional
                sh.Open();
#else
                using (X509Certificate cert = X509Certificate.CreateFromCertFile($@".\{nameof(Properties.Resources.ASC)}.cer"))
                using (Process proc = Process.Start(new ProcessStartInfo
                {
                    Verb = "runas",
                    UseShellExecute = false,
                    WorkingDirectory = Win32.sys32,
                    FileName = $@"{Win32.sys32}\netsh.exe",
                    Arguments = $@"http add sslcert ipport=""{ip}:{port + 1}"" certhash={cert.GetCertHashString()} appid={{{Win32.GUID}}}", //  clientcertnegotiation=enable
                    WindowStyle = ProcessWindowStyle.Hidden,
                }))
                    proc.WaitForExit();
#endif
                "Binding successful".Ok();
#if USE_SERVICEHOST
                return sh;
#endif
            }
            else
                $"Port {port + 1} is already bound to the certificate.".Ok();

            return null;
        }

        public static int Main(string[] args)
        {
            string dir = new FileInfo(Assembly.GetExecutingAssembly().Location).Directory.FullName;
            int retcode = 0;
#if DEBUG
            AppDomain.MonitoringIsEnabled = true;
#endif
            try
            {
                Win32.ShowWindow(Process.GetCurrentProcess().MainWindowHandle, 3);
            }
            catch
            {
                "Unable to resize console window ... aren't you running in GUI mode?".Warn();
            }

            Directory.SetCurrentDirectory(dir);

            $"Running from '{dir}'".Info();

            using (Mutex m = new Mutex(false, Win32.MUTEX))
                try
                {
                    if (m.WaitOne(0, false))
                    {
                        InstallCertificates($@"{dir}\{nameof(Properties.Resources.Unknown6656)}.cer", StoreName.Root);
                        InstallCertificates($@"{dir}\{nameof(Properties.Resources.ASC)}.cer", StoreName.TrustedPublisher);

                        fixed (bool* bptr = &accptconnections)
                            using (ServiceHost sh = BindCertificatePort(IPAddress.Any.ToString(), Win32.PORT, StoreName.TrustedPublisher, nameof(Properties.Resources.ASC)))
                            using (ASCServer ws = new ASCServer(Win32.PORT, bptr))
                                new Program().Inner(Win32.PORT, dir);
                    }
                    else
                        "Cannot start the server, as an other instance of this application is already running...".Warn();
                }
                catch (Exception ex)
                {
                    ex.Err();

                    "Application-forced shutdown ...".Err();

                    retcode = -1;
                }
                finally
                {
                    "Server shut down".Ok();

                    m.Close();

                    if (Debugger.IsAttached | (Win32.GetConsoleWindow() != IntPtr.Zero))
                    {
                        "Press any key ...".Msg();

                        Console.ReadKey(true);
                    }
                }

            return retcode;
        }

        [OperationContract]
        internal void Inner(int port, string dir)
        {
            try
            {
                "Connecting to the internal database ...".Msg();
                $"Connected to the database with the connection ID {{{Database.Connection.ClientConnectionId}}}".Ok();
                $"{Database.Execute("SELECT COUNT([ID]) FROM [Users]").First()[0]} registered useres have been found inside the database".Msg();
                $"Runninng on port {port}. Press `ESC` to exit.".Info();

                accptconnections = true;

                do
                    while (!Console.KeyAvailable)
                        ;
                while (Console.ReadKey(true).Key != ConsoleKey.Escape);

                accptconnections = false;

                "User-forced shutdown ...".Warn();
            }
            finally
            {
                Database.Disconnect();

                accptconnections = false;
            }
        }
    }

    internal static class Logger
    {
        private static readonly StringBuilder log = new StringBuilder();
        private static readonly Queue<Action> actions = new Queue<Action>();


        internal static string Log => log.ToString();

        static Logger() => ThreadPool.QueueUserWorkItem(delegate
        {
            while (true)
                if (actions.Count > 0)
                    actions.Dequeue()();
        });

        private static void PrintColored(this string str, string prefix, ConsoleColor col) => actions.Enqueue(delegate
        {
            DateTime now = DateTime.Now;

            Console.ForegroundColor = ConsoleColor.White;
            Console.Write('[');
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write($"{now:HH:mm:ss:ffffff}");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("][");
            Console.ForegroundColor = col;
            Console.Write(prefix);
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("] ");
            Console.ForegroundColor = col;
            Console.WriteLine(str);
            Console.ForegroundColor = ConsoleColor.White;

            log.AppendLine($"[{now:YYYY-MM-dd HH:mm:ss:ffffff}] [{prefix}] {str}");
        });

        internal static void Clear() => actions.Enqueue(() => log.Clear());

        internal static void Err(this Exception ex)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"An {ex.GetType().FullName} occuered:");

            while (ex != null)
            {
                sb.AppendLine($"{ex.Message}:\n{ex.StackTrace}");

                ex = ex.InnerException;
            }

            Err(sb.ToString().TrimEnd());
        }

        internal static void Err(this string str) => PrintColored(str, "ERR.", ConsoleColor.Red);

        internal static void Msg(this string str) => PrintColored(str, "    ", ConsoleColor.White);

        internal static void Ok(this string str) => PrintColored(str, " OK ", ConsoleColor.Green);

        internal static void Warn(this string str) => PrintColored(str, "WARN", ConsoleColor.Yellow);

        internal static void Info(this string str) => PrintColored(str, "INFO", ConsoleColor.Magenta);
    }

    internal static class Database
    {
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
                                                AttachDbFilename={Directory.GetCurrentDirectory()}\database.mdf;
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

        // TODO : connection stuff, query stuff etc.
    }
}
