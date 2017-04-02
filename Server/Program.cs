// #define USE_SERVICEHOST

using System.Security.Cryptography.X509Certificates;
using System.Runtime.InteropServices;
using System.ServiceModel.Security;
using System.Collections.Generic;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Net.Security;
using System.ServiceModel;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System;

using NetFwTypeLib;
using System.Net.NetworkInformation;

namespace ASC.Server
{
    /// <summary>
    /// The application's main program class
    /// </summary>
    [ServiceContract(Namespace = Win32.NAMESPACE_URI), Guid(Win32.GUID)]
    public unsafe class Program
    {
        internal const string ARG_IGNMTX = "--ignore-mutex";
        internal const string ARG_DELFWL = "--delete-firewall-entries";
        internal const string ARG_OFFLINE = "--offline-mode";
        internal const string ARG_NOLOG = "--no-log-save";

        internal static string recaptcha_private = null;
        private static bool accptconnections = false;
        private static Database database;

        /// <summary>
        /// Installs the given certificate to the given .X509-store
        /// </summary>
        /// <param name="path">Path to the certificate's .cer-file</param>
        /// <param name="name">.X509 store name</param>
        public static void InstallCertificate(string path, StoreName name)
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

        /// <summary>
        /// Returns, whether the given ip:port need any service binding
        /// </summary>
        /// <param name="ip">IP Address</param>
        /// <param name="port">Port</param>
        /// <returns>Service binding requirement</returns>
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

        /// <summary>
        /// Binds the given certificate from the given store to the given ip:port and returns the newly created service host
        /// </summary>
        /// <param name="ip">IP Address</param>
        /// <param name="port">Port</param>
        /// <param name="store">.X509 certificate store</param>
        /// <param name="name">.X509 certificate name</param>
        /// <returns>Service host</returns>
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

        /// <summary>
        /// The application's main entry point
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <returns>Return code</returns>
        public static int Main(string[] args)
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);

            $"Application started with the following {args.Length} argument(s):".Msg();

            for (int i = 0; i < args.Length; i++)
                $"    [{i}]: {args[i].Trim()}".Msg();

            if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
            {
                "This application is not running as administrator and will therefore have privilege and authorisation problems. Please restart it with elevated privilege.".Err();

                return -1;
            }
            else
                "Running as administrator. Perfect.".Ok();

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

            if (File.Exists(Win32.RECAPTCHA_SECRET) && ((recaptcha_private = File.ReadAllText(Win32.RECAPTCHA_SECRET)?.Trim() ?? recaptcha_private ?? "").Length > 0))
                $"Loaded the ReCaptcha private key '{recaptcha_private}'".Ok();
            else
                "No ReCaptcha private key could be found, meaning that the register-function will not be available.".Err();

            using (Mutex m = new Mutex(false, Win32.MUTEX))
                try
                {
                    if (m.WaitOne(0, false) || containsarg(ARG_IGNMTX))
                    {
                        InstallCertificate($@"{dir}\{nameof(Properties.Resources.Unknown6656)}.cer", StoreName.Root);
                        InstallCertificate($@"{dir}\{nameof(Properties.Resources.ASC)}.cer", StoreName.TrustedPublisher);

                        "Setting firewall rules ...".Msg();

                        foreach (int port in new int[] { Win32.PORT, Win32.PORT + 1})
                        {
                            if (FirewallUtils.IsPortOpen(port))
                            {
                                $"A serive is already running on port {port}. It will be shut down ...".Warn();

                                FirewallUtils.ClosePort(port);
                            }

                            FirewallUtils.OpenPort(port, "ASC Server");

                            $"Port {port} was successfully registered.".Ok();
                        }

                        "Firewall rules set.".Ok();

                        if (containsarg(ARG_OFFLINE))
                            "Server running in offline mode. No internet connection checks will be performed.".Warn();
                        else
                            if (!TestConnection())
                                "No connection to the outside internet could be made. Many of the server's features will only work partially.".Err();

                        fixed (bool* bptr = &accptconnections)
                            using (ServiceHost sh = BindCertificatePort(IPAddress.Any.ToString(), Win32.PORT, StoreName.TrustedPublisher, nameof(Properties.Resources.ASC)))
                            using (ASCServer ws = new ASCServer(Win32.PORT, bptr, null))
                                new Program().Inner(Win32.PORT, dir, ws);
                    }
                    else
                        "Cannot start the server, as an other instance of this application is already running.".Warn();
                }
                catch (Exception ex)
                {
                    ex.Err();

                    "Application-forced shutdown ...".Err();

                    retcode = -1;
                }
                finally
                {
                    if (containsarg(ARG_DELFWL))
                    {
                        "Removing previously set firewall rules ...".Msg();

                        foreach (int port in new int[] { Win32.PORT, Win32.PORT + 1 })
                        {
                            FirewallUtils.ClosePort(port);

                            $"Port {port} was successfully un-registered".Ok();
                        }
                    }

                    "Server shut down".Ok();

                    m.Close();

                    "Logging service shutting down.".Info();

                    if (!containsarg(ARG_NOLOG))
                        Logger.Save(Directory.GetCurrentDirectory());

                    if (Debugger.IsAttached | (Win32.GetConsoleWindow() != IntPtr.Zero))
                    {
                        "Press any key ...".Msg();

                        Console.ReadKey(true);
                    }
                }

            return retcode;

            bool containsarg(string arg) => args.Any(_ => _.ToLower().Trim() == arg.ToLower().Trim());
        }

        [OperationContract]
        internal void Inner(int port, string dir, ASCServer ascws)
        {
            if (ascws == null) // running from service
            {
                "Please avoid running the ASC server from service due to possibly incorrect initialization".Warn();

                Main(new string[] { ARG_IGNMTX });

                return;
            }

            "Connecting to the internal database ...".Msg();

            using (database = Database.Instance)
                try
                {
                    ascws.tSQL = database;
#if DEBUG
                    database.DebugMode = true;
#endif
                    $"Connected to the database with the connection ID {{{database.Connection.ClientConnectionId}}}".Ok();

                    foreach (string table in new string[] { "ChatMembers", "ChatMessages", "Chats", "Messages", "Users", "UserAuthentifications" })
                    {
                        if (!database.Exists(table))
                        {
                            $"Table '{table}' could not be found. It will be re-created ...".Warn();

                            database.CreateNew(table);
                        }

                        $"Table '{table}' loaded.".Ok();
                    }

                    foreach (string function in new string[] { "Trim" })
                    {
                        if (!database.ContainsFunction(function))
                        {
                            $"Function '{function}' could not be found. It will be re-created ...".Warn();

                            database.CreateFunction(function);
                        }

                        $"Function '{function}' loaded.".Ok();
                    }

                    $"{database.UserCount} registered user(s) have been found inside the database.".Msg();
                    $"{database.AdminCount} registered administrator(s) have been found inside the database.".Msg();
                    $"{database.MessageCount} sent message(s) have been found inside the database.".Msg();
                    $"{database.ChatCount} chat(s)/group(s) have been found inside the database.".Msg();

                    Authentification.Start();

                    $"Runninng on port {port}. Press `ESC` to exit.".Info();

                    {
                        // testing
                        foreach (string s in @"foo bar baz test anon topkek".Split())
                        {
                            DBUser user = new DBUser {
                                IsAdmin = s.Length % 4 == 3,
                                Name = s,
                                Status = s
                            };

                            database.AddUser(ref user);
                        }
                    }

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
                    accptconnections = false;

                    Authentification.Stop();

                    lock (ASCServer.TemporaryUsers)
                        foreach (long id in ASCServer.TemporaryUsers.Keys)
                            database?.DeleteUser(id);

                    "Disconnicting from the database ...".Msg();
                }
        }

        internal static bool TestConnection()
        {
            object mutex = new object();
            bool res = false;

            Parallel.ForEach(new string[] { "8.8.8.8", "8.8.4.4", "google.com", "wikipedia.org", "4chan.org", "yahoo.com", "microsoft.com", "apple.com" }, (dns, local) => {
                try
                {
                    using (Ping p = new Ping())
                        if (p.Send(dns, 3000).Status == IPStatus.Success)
                            lock (mutex)
                            {
                                res |= true;
                                local.Stop();
                            }
                }
                catch
                {
                }
            });

            return res;
        }
    }

    internal static class FirewallUtils
    {
        private static INetFwProfile sm_fwProfile = null;
        private static Dictionary<string, string> TypeGUIDs { get; } = new Dictionary<string, string>
        {
            ["INetFwMgr"] = "{304CE942-6E39-40D8-943A-B913C40C9CD4}",
            ["INetAuthApp"] = "{EC9846B3-2762-4A6B-A214-6ACB603462D2}",
            ["INetOpenPort"] = "{0CA545C6-37AD-4A6C-BF92-9F7610067EF5}",
        };


        internal static bool IsPortOpen(int port)
        {
            EnsureSetup();

            Type progID = Type.GetTypeFromProgID("HNetCfg.FwMgr");
            INetFwMgr firewall = Activator.CreateInstance(progID) as INetFwMgr;
            INetFwOpenPorts ports = firewall.LocalPolicy.CurrentProfile.GloballyOpenPorts;
            IEnumerator iter = ports.GetEnumerator();

            while (iter.MoveNext())
                if ((iter.Current as INetFwOpenPort).Port == port)
                    return true;

            return false;
        }

        internal static void OpenPort(int port, string applicationName)
        {
            EnsureSetup();

            if (IsPortOpen(port))
                return;

            INetFwOpenPort fwport = GetInstance("INetOpenPort") as INetFwOpenPort;

            fwport.Port = port;
            fwport.Enabled = true;
            fwport.Name = applicationName;
            fwport.Scope = NET_FW_SCOPE_.NET_FW_SCOPE_ALL;
            fwport.Protocol = NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_TCP;
            fwport.IpVersion = NET_FW_IP_VERSION_.NET_FW_IP_VERSION_ANY;

            sm_fwProfile.GloballyOpenPorts.Add(fwport);
        }

        internal static void ClosePort(int port)
        {
            EnsureSetup();

            if (!IsPortOpen(port))
                return;

            INetFwOpenPorts ports = sm_fwProfile.GloballyOpenPorts;
            ports.Remove(port, NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_TCP);
        }

        private static void EnsureSetup()
        {
            if (sm_fwProfile == null)
            {
                INetFwMgr fwMgr = GetInstance("INetFwMgr") as INetFwMgr;
                sm_fwProfile = fwMgr.LocalPolicy.CurrentProfile;
            }
        }

        private static object GetInstance(string typeName) => Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid(TypeGUIDs[typeName])));
    }

    internal static class Win32
    {
        internal const string RECAPTCHA_SECRET = "./recaptcha.key";
        internal const string GUID = "208b519d-d6b8-44df-9bba-a5abfddb773a";
        internal const string MUTEX = "ASC_server_" + GUID;
        internal const string NAMESPACE_URI = "http://0.0.0.0:8081";
        internal const int PORT = 8080;

        internal static readonly string sys32 = $@"{Environment.GetFolderPath(Environment.SpecialFolder.Windows)}\system32";

        [DllImport("user32.dll")]
        internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("kernel32.dll")]
        internal static extern IntPtr GetConsoleWindow();


        /// <summary>
        /// Converts the given stream to a byte array
        /// </summary>
        /// <param name="s">Stream</param>
        /// <returns>Byte array</returns>
        public static byte[] ToBytes(this Stream s)
        {
            if (s == null)
                return new byte[0];
            else
            {
                byte[] arr = new byte[s.Length];

                s.Position = 0;
                s.Read(arr, 0, arr.Length);

                return arr;
            }
        }
    }

    internal static class Logger
    {
        private static readonly StringBuilder log = new StringBuilder();
        private static readonly Queue<Action> actions = new Queue<Action>();
        private static readonly DateTime startup = DateTime.Now;


        internal static string Log => log.ToString();

        static Logger()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                while (true)
                    if (actions.Count > 0)
                        actions.Dequeue()();
            });

            $"Logger service started at {startup:yyyy-MM-dd HH:mm:ss.ffffff}".Info();
        }

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
            Console.WriteLine(str ?? "");
            Console.ForegroundColor = ConsoleColor.White;

            log.AppendLine($"[{now:yyyy-MM-dd HH:mm:ss:ffffff}] [{prefix}] {str ?? ""}");
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

        internal static void Sql(this string str) => PrintColored(str, "tSQL", ConsoleColor.Cyan);

        internal static void Save(this string dir)
        {
            while (actions.Count > 0)
                ;

            File.AppendAllText($"{dir}\\output-{startup:yyyy-MM-dd-HH-mm-ss-ffffff}.log", Log);
        }
    }
}
