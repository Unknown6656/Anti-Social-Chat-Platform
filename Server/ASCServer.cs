using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Drawing.Imaging;
using System.Net.Sockets;
using System.Diagnostics;
using System.Reflection;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System;

using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

using Resources = ASC.Server.Properties.Resources;

namespace ASC.Server
{
    /// <summary>
    /// Represents an ASC-server
    /// </summary>
    public sealed unsafe class ASCServer
        : IDisposable
    {
        internal static Dictionary<string, Dictionary<string, string>> LanguagePacks { get; }
        internal static Dictionary<string, ASCOperation> Operations { get; }
        internal static Dictionary<StatusCode, string> StatusCodes { get; } = new Dictionary<StatusCode, string>
        {
            [StatusCode._200] = "OK",
            [StatusCode._400] = "Invalid operation",
            [StatusCode._403] = "Forbidden",
            [StatusCode._404] = "Not found",
            [StatusCode._420] = "420/Weed",
            [StatusCode._500] = "Server Error",
        };
        internal (int HTTP, int HTTPS) Ports { get; }
        internal Func<int, bool> SSL { get; }
        internal string ServerString { get; }
        internal Database tSQL { get; set; }
        internal HTTPServer Server { get; }

        private readonly bool* acceptconnections;


        static ASCServer()
        {
            LanguagePacks = (from f in Directory.GetFiles($@"{Directory.GetCurrentDirectory()}\Languages", "*.json", SearchOption.AllDirectories)
                             let nfo = new FileInfo(f)
                             let code = nfo.Name.Replace(nfo.Extension, "")
                             select (code, BuildLanguageDictionary(f))).ToDictionary(_ => _.Item1, _ => _.Item2);

            Operations = new Dictionary<string, ASCOperation>
            {
                ["user_by_name"] = new ASCOperation((req, res, vals, db) => db.FindUsers(vals["name"]), false, "name"),
                ["user_by_id"] = new ASCOperation((req, res, vals, db) => db.GetUser(long.Parse(vals["id"])), false, "id"),
                ["user_by_guid"] = new ASCOperation((req, res, vals, db) => db.GetUser(Guid.Parse(vals["guid"])), false, "guid"),
                ["auth_login"] = new ASCOperation((req, res, vals, db) => new
                {
                    Success = verify(req, db, vals["id"], vals["hash"], out string session),
                    Session = session ?? ""
                }, false, "id", "hash"),
                ["auth_salt"] = new ASCOperation((req, res, vals, db) => db.GetUserSalt(long.Parse(vals["id"])), false, "id"),
                ["auth_update"] = new ASCOperation((req, res, vals, db) => db.UpdateUserHash(long.Parse(vals["id"]), vals["new"]), true, "new"),
                ["available_lang"] = new ASCOperation((req, res, vals, db) => LanguagePacks.Keys.ToArray()),

                ["auth_test"] = new ASCOperation(null, true) // TESTING ONLY
            };
        }

        /// <summary>
        /// Creates a new instance
        /// </summary>
        /// <param name="port">The HTTP port</param>
        /// <param name="accept">A pointer to an boolean value, which indicates whether the server shall accept incomming requests</param>
        /// <param name="tsql">The underlying Transact-SQL [tSQL] database</param>
        public ASCServer(int port, bool* accept, Database tsql)
        {
            acceptconnections = accept;

            foreach (KeyValuePair<string, Dictionary<string, string>> dic in LanguagePacks)
                $"Language dictionary '{dic.Value["lang_name"]} ({dic.Key})' loaded.".Ok();

            ServerString = $"ASC Server/{Assembly.GetExecutingAssembly().GetName().Version} Unknown6656/420.1337.14.88";

            tSQL = tsql;
            Ports = (port, port + 1);
            SSL = p => p == port + 1;

            Server = new HTTPServer(SendResponse, $"http://*:{port}/", $"https://*:{port + 1}/");
            Server.Start();
        }

        /// <summary>
        /// Builds and returns the language dictionary from the given file
        /// </summary>
        /// <param name="path">Language dictionary file path</param>
        /// <returns>Language dictionary</returns>
        public static Dictionary<string, string> BuildLanguageDictionary(string path)
        {
            $"Building language dictionary for '{path}' ...".Msg();

            Dictionary<string, string> dic = new Dictionary<string, string>();
            string json = File.ReadAllText(path);
            JToken token = JToken.Parse(json);

            Flatten(dic, token, "");

            void Flatten(Dictionary<string, string> table, JToken tk, string pfx)
            {
                switch (tk.Type)
                {
                    case JTokenType.Object:
                        foreach (JProperty prop in tk.Children<JProperty>())
                            Flatten(table, prop.Value, n(prop.Name));

                        return;
                    case JTokenType.Array:
                        int index = 0;

                        foreach (JToken value in token.Children())
                        {
                            Flatten(table, value, n(index.ToString()));

                            index++;
                        }

                        return;
                    default:
                        table[pfx] = (tk as JValue)?.Value?.ToString() ?? "";

                        return;
                }

                string n(string str) => (pfx ?? "").Length == 0 ? str : $"{pfx}_{str}";
            }

            return dic;
        }

        /// <summary>
        /// Fetches the resource associated with the given object name and formats it using the given string dictionary and format parameters
        /// </summary>
        /// <param name="obj">Resource name</param>
        /// <param name="variables">String table</param>
        /// <param name="args">Format parameters</param>
        /// <returns>Formatted resource</returns>
        public string FetchResource(string obj, Dictionary<string, object> variables, params object[] args)
        {
            const string pat_dic = @"\§(?<key>\w+)(\:(?<format>[^§]+))?\§";
            const string pat_par = @"\§([0-9]+)\:([^§]+)§";
            const string pat_cnt = @"\§([0-9]+)";
            List<object> values = new List<object>();
            int rcount = 0;

            obj = obj.Replace("{", "{{");
            obj = obj.Replace("}", "}}");

            rcount += Regex.Matches(obj, pat_par).Count;
            rcount += Regex.Matches(obj, pat_cnt).Count;

            obj = Regex.Replace(obj, pat_par, "{$1:$2}");
            obj = Regex.Replace(obj, pat_cnt, "{$1}");

            while (regex(obj, pat_dic, out Match m))
            {
                string head = obj.Substring(0, m.Index);
                string tail = obj.Substring(m.Index + m.Length);
                string key = m.Groups["key"].ToString();
                string format = m.Groups["format"]?.ToString() ?? "";

                if (format.Length > 0)
                    format = ':' + format;

                values.Add(variables?.ContainsKey(key) ?? false ? variables?[key] : null);

                obj = $"{head}{{{rcount++}{format}}}{tail}";
            }

            return string.Format(obj, args.Concat(values).ToArray());
        }

        /// <summary>
        /// Processes the given HTTP listener request and writes the processing result into the given HTTP listener response
        /// </summary>
        /// <param name="request">HTTP listener request</param>
        /// <param name="response">HTTP listener response</param>
        /// <returns>HTTP response data</returns>
        public HTTPResponse SendResponse(HttpListenerRequest request, HttpListenerResponse response)
        {
            #region INIT

            Dictionary<string, object> vars = new Dictionary<string, object>();
            int port = request.LocalEndPoint.Port;
            string url = request.RawUrl;
            DateTime now = DateTime.Now;

            response.Headers[HttpResponseHeader.Server] = ServerString;

            Stopwatch sw = new Stopwatch();

            sw.Start();

            while (!*acceptconnections)
                if (sw.ElapsedMilliseconds > 30000)
                    return SendError(request, response, vars, StatusCode._500, "");

            #endregion
            #region RESOURCE HANDLING

            if (Regex.IsMatch(url, @"[\\\/]?favicon\.ico$", RegexOptions.IgnoreCase))
                url = "res~favicon~image/x-icon";

            if (regex(url, @"res\~(?<res>.+)\~(?<type>[\w\-\/\-\+]+)\b?", out Match m))
            {
                string resource = m.Groups["res"].ToString();

                try
                {
                    SetStatusCode(response, StatusCode._200);

                    response.ContentType = m.Groups["type"].ToString();

                    $"Processing resource '{resource}' with MIME-type '{response.ContentType}'...".Msg();

                    foreach (Func<HTTPResponse> f in new Func<HTTPResponse>[] {
                        () => File.ReadAllBytes($"{Directory.GetCurrentDirectory()}\\Resources\\{resource}"),
                        () => Resources.ResourceManager.GetString(resource),
                        () => Resources.ResourceManager.GetStream(resource).ToBytes(),
                        () =>
                        {
                            object obj = typeof(Resources).GetProperty(resource, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null);

                            switch (obj)
                            {
                                case Icon ico:
                                    using (MemoryStream ms = new MemoryStream())
                                    {
                                        ico.Save(ms);

                                        return ms.ToArray();
                                    }
                                case Image i:
                                    using (MemoryStream ms = new MemoryStream())
                                    {
                                        i.Save(ms, ImageFormat.Png);

                                        return ms.ToArray();
                                    }
                                case Stream s:
                                    return s.ToBytes();
                                default:
                                    return obj as byte[];
                            }
                        },
                        () => Assembly.GetExecutingAssembly().GetManifestResourceStream($"Resources.{resource}").ToBytes(),
                    })
                        try
                        {
                            return f();
                        }
                        catch
                        {
                        }
                }
                catch
                {
                }

                $"Resource '{resource}' not found.".Warn();

                return SendError(request, response, vars, StatusCode._404, $"The resource '{resource}' could not be found.");
            }
            #endregion
            #region 'REGULAR' REQUESTS
            else
            {
                string session = null;

                SetStatusCode(response, StatusCode._200);

                if (contains(request, "lang", out string lang))
                    lang = lang?.ToLower();
                else
                {
                    Cookie langc = request.Cookies["_lang"];

                    if (langc?.Value?.Length > 0)
                        lang = langc.Value;
                }

                if (!LanguagePacks.ContainsKey(lang))
                    lang = (from lraw in request.UserLanguages
                            let lng = lraw.Contains(';') ? lraw.Split(';')[0] : lraw
                            let lcd = (lng.Contains('-') ? lng.Split('-')[0] : lng).ToLower().Trim()
                            where LanguagePacks.ContainsKey(lcd)
                            select lcd).FirstOrDefault();

                foreach (KeyValuePair<string, string> kvp in LanguagePacks[lang ?? "en"])
                    vars[kvp.Key] = kvp.Value;

                vars["url"] = url;
                vars["ssl"] = SSL(port).ToString().ToLower(); // JavaScript being bitchy
                vars["time"] = now;
                vars["port"] = port;
                vars["port_http"] = Ports.HTTP;
                vars["port_https"] = Ports.HTTPS;
                vars["protocol"] = request.Url.Scheme;
                vars["host"] = request.Url.Host;
                vars["addr"] = request.LocalEndPoint.Address;
                vars["user_agent"] = request.UserAgent;
                vars["user_host"] = request.UserHostName;
                vars["user_addr"] = request.UserHostAddress;
                vars["pre_script"] = FetchResource(Resources.pre_script, vars);
                vars["post_script"] = FetchResource(Resources.post_script, vars);
                vars["style"] = FetchResource(Resources.style, vars);

                if (regex(request.Url.LocalPath, @"^[\\\/]?api\.json$", out _))
                {
#if DEBUG
                    $"API access: {url}".Info();
#endif
                    if (contains(request, "operation", out string op))
                        if (Operations.ContainsKey(op = op.ToLower()))
                        {
                            ASCOperation ascop = Operations[op];
                            Dictionary<string, string> values = new Dictionary<string, string>();

                            foreach (string key in request.QueryString.AllKeys)
                                values[key] = request.QueryString[key];

                            IEnumerable<string> missing = ascop.Keys.Except(values.Keys);

                            if (missing.Any())
                                return error($"A value for '{missing.First()}' is required when using the operation '{op}'.");

                            try
                            {
                                if (ascop.NeedsAuthentification)
                                {
                                    bool res = contains(request, "id", out string sid);

                                    if (contains(request, "hash", out string hash))
                                        res &= verify(request, tSQL, sid, hash, out session);
                                    else if (contains(request, "session", out session))
                                        res &= tSQL.VerifySession(session); // TODO
                                    else
                                    {
                                        Cookie sessc = request.Cookies["_sess"];

                                        res &= sessc == null ? false : tSQL.VerifySession(session = sessc.Value); // TODO
                                    }

                                    if (!res)
                                        return error("", StatusCode._403);
                                }

                                vars["user_session"] = session;

                                return ToJSON(new
                                {
                                    Success = true,
                                    Session = session,
                                    Data = ascop.Handler(request, response, values, tSQL)
                                });
                            }
                            catch (Exception ex)
                            {
                                return error($"At least one parameter was mal-formatted or the operation was invalid.<br/><pre class=\"code\">{ex.Message}:\n{ex.StackTrace}</pre>");
                            }
                        }
                        else
                            return error($"The operation '{op}' is unknown.");
                    else
                        return error("An operation must be specified.");
                }

                if (session == null)
                    vars["inner"] = FetchResource(Resources.login, vars);

                return FetchResource(Resources.frame, vars);

                HTTPResponse error(string msg, StatusCode code = StatusCode._400)
                {
                    SetStatusCode(response, code);

                    return ToJSON(new
                    {
                        Success = false,
                        Session = session,
                        Data = msg
                    });
                }
            }

            #endregion
        }

        internal HTTPResponse SendError(HttpListenerRequest request, HttpListenerResponse response, Dictionary<string, object> vars, StatusCode code, string msg = null)
        {
            SetStatusCode(response, code);

            vars["error_code"] = ((int)code).ToString();
            vars["error_message"] = vars[$"error{code}"].ToString();
            vars["error_submessage"] = msg ?? "";
            vars["inner"] = FetchResource(Resources.error, vars);

            return FetchResource(Resources.frame, vars);
        }

        internal void SetStatusCode(HttpListenerResponse resp, StatusCode code)
        {
            resp.StatusDescription = StatusCodes[code];
            resp.StatusCode = (int)code;
        }

        /// <summary>
        /// Disposes the current ASC server and releases all underlying resources
        /// </summary>
        public void Dispose() => Server.Dispose();

        internal static bool contains(HttpListenerRequest request, string key, out string value)
        {
            bool ret;

            (value, ret) = request?.QueryString?.AllKeys?.Contains(key) ?? false ? (request.QueryString[key], true) : (null, false);

            return ret;
        }

        internal static bool verify(HttpListenerRequest req, Database db, string id, string hash, out string session)
        {
            session = null;

            return long.TryParse(id, out long l) && db.Login(l, hash, req.RemoteEndPoint.ToString(), req.UserAgent, out session);
        }

        internal static bool regex(string input, string pattern, out Match m, RegexOptions opt = RegexOptions.IgnoreCase) => (m = Regex.Match(input, pattern, opt)).Success;

        internal static HTTPResponse ToJSON<T>(T obj) => JsonConvert.SerializeObject(obj);
    }

    /// <summary>
    /// Represents a simple ASC server operation
    /// </summary>
    public sealed class ASCOperation
    {
        /// <summary>
        /// Returns the operation's handler
        /// </summary>
        public Func<HttpListenerRequest, HttpListenerResponse, Dictionary<string, string>, Database, dynamic> Handler { get; }
        /// <summary>
        /// Returns an enumeration of required query keys
        /// </summary>
        public string[] Keys { get; }
        /// <summary>
        /// Returns whether the ASC operation requires an user authentification
        /// </summary>
        public bool NeedsAuthentification { get; }

        /// <summary>
        /// Creates a new instance
        /// </summary>
        /// <param name="handler">The operation's handler</param>
        /// <param name="elevated">Determines whether the operation needs elevated privileges in form of user authentification</param>
        /// <param name="keys">The operation's required query keys</param>
        public ASCOperation(Func<HttpListenerRequest, HttpListenerResponse, Dictionary<string, string>, Database, dynamic> handler, bool elevated = false, params string[] keys)
        {
            this.Keys = keys ?? new string[0];
            this.NeedsAuthentification = elevated;
            this.Handler = handler ?? ((req, res, k, db) => null);
        }
    }

    internal enum StatusCode
    {
        _200 = 200,
        _400 = 400,
        _403 = 403,
        _404 = 404,
        _420 = 420,
        _500 = 500,
    }
}
