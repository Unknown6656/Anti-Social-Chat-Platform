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
        internal static readonly Regex REGEX_MOBILEVERSION = new Regex(@"1207|6310|6590|3gso|4thp|50[1-6]i|770s|802s|a wa|abac|ac(er|oo|s\-)|ai(ko|rn)|al(av|ca|co)|amoi|an(ex|ny|yw)|aptu|ar(ch|go)|as(te|us)|attw|au(di|\-m|r |s )|avan|be(ck|ll|nq)|bi(lb|rd)|bl(ac|az)|br(e|v)w|bumb|bw\-(n|u)|c55\/|capi|ccwa|cdm\-|cell|chtm|cldc|cmd\-|co(mp|nd)|craw|da(it|ll|ng)|dbte|dc\-s|devi|dica|dmob|do(c|p)o|ds(12|\-d)|el(49|ai)|em(l2|ul)|er(ic|k0)|esl8|ez([4-7]0|os|wa|ze)|fetc|fly(\-|_)|g1 u|g560|gene|gf\-5|g\-mo|go(\.w|od)|gr(ad|un)|haie|hcit|hd\-(m|p|t)|hei\-|hi(pt|ta)|hp( i|ip)|hs\-c|ht(c(\-| |_|a|g|p|s|t)|tp)|hu(aw|tc)|i\-(20|go|ma)|i230|iac( |\-|\/)|ibro|idea|ig01|ikom|im1k|inno|ipaq|iris|ja(t|v)a|jbro|jemu|jigs|kddi|keji|kgt( |\/)|klon|kpt |kwc\-|kyo(c|k)|le(no|xi)|lg( g|\/(k|l|u)|50|54|\-[a-w])|libw|lynx|m1\-w|m3ga|m50\/|ma(te|ui|xo)|mc(01|21|ca)|m\-cr|me(rc|ri)|mi(o8|oa|ts)|mmef|mo(01|02|bi|de|do|t(\-| |o|v)|zz)|mt(50|p1|v )|mwbp|mywa|n10[0-2]|n20[2-3]|n30(0|2)|n50(0|2|5)|n7(0(0|1)|10)|ne((c|m)\-|on|tf|wf|wg|wt)|nok(6|i)|nzph|o2im|op(ti|wv)|oran|owg1|p800|pan(a|d|t)|pdxg|pg(13|\-([1-8]|c))|phil|pire|pl(ay|uc)|pn\-2|po(ck|rt|se)|prox|psio|pt\-g|qa\-a|qc(07|12|21|32|60|\-[2-7]|i\-)|qtek|r380|r600|raks|rim9|ro(ve|zo)|s55\/|sa(ge|ma|mm|ms|ny|va)|sc(01|h\-|oo|p\-)|sdk\/|se(c(\-|0|1)|47|mc|nd|ri)|sgh\-|shar|sie(\-|m)|sk\-0|sl(45|id)|sm(al|ar|b3|it|t5)|so(ft|ny)|sp(01|h\-|v\-|v )|sy(01|mb)|t2(18|50)|t6(00|10|18)|ta(gt|lk)|tcl\-|tdg\-|tel(i|m)|tim\-|t\-mo|to(pl|sh)|ts(70|m\-|m3|m5)|tx\-9|up(\.b|g1|si)|utst|v400|v750|veri|vi(rg|te)|vk(40|5[0-3]|\-v)|vm40|voda|vulc|vx(52|53|60|61|70|80|81|83|85|98)|w3c(\-| )|webc|whit|wi(g |nc|nw)|wmlb|wonu|x700|yas\-|your|zeto|zte\-", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);
        internal static readonly Regex REGEX_MOBILE = new Regex(@"android|(android|bb\d+|meego).+mobile|avantgo|bada\/|blackberry|blazer|compal|elaine|fennec|hiptop|iemobile|ip(hone|od)|iris|kindle|lge |maemo|midp|mmp|mobile.+firefox|netfront|opera m(ob|in)i|palm( os)?|phone|p(ixi|re)\/|plucker|pocket|psp|series(4|6)0|symbian|treo|up\.(browser|link)|vodafone|wap|windows (ce|phone)|xda|xiino", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

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
                ["lang_pack"] = new ASCOperation((req, res, vals, db) => LanguagePacks[vals["code"].ToLower()], keys: "lang"),
                ["lang_info"] = new ASCOperation((req, res, vals, db) =>
                {
                    Dictionary<string, string> pack = LanguagePacks[vals["code"].ToLower()];

                    return new {
                        Name = pack["lang_name"],
                        EnglishName = pack["lang_iname"],
                        IsBeta = bool.Parse(pack["lang_beta"].ToLower()),
                    };
                }, keys: "lang"),
                ["user_by_name"] = new ASCOperation((req, res, vals, db) => db.FindUsers(vals["name"]), keys: "name"),
                ["user_by_id"] = new ASCOperation((req, res, vals, db) => db.GetUser(long.Parse(vals["id"])), keys: "id"),
                ["user_by_guid"] = new ASCOperation((req, res, vals, db) => db.GetUser(Guid.Parse(vals["guid"])), keys: "guid"),
                ["auth_login"] = new ASCOperation((req, res, vals, db) => new
                {
                    Success = verify(req, db, vals["id"], vals["hash"], out string session),
                    Session = session ?? ""
                }, ASCOperationPrivilege.Regular, "id", "hash"),
                ["auth_salt"] = new ASCOperation((req, res, vals, db) => db.GetUserSalt(long.Parse(vals["id"])), keys: "id"),
                ["auth_update"] = new ASCOperation((req, res, vals, db) => db.UpdateUserHash(long.Parse(vals["id"]), vals["new"]), keys: "new"),
                ["available_lang"] = new ASCOperation((req, res, vals, db) => LanguagePacks.Keys.ToArray()),
                ["auth_test"] = new ASCOperation(null, ASCOperationPrivilege.User), // TESTING ONLY
                ["raw_sql"] = new ASCOperation((req, res, vals, db) => JsonConvert.DeserializeObject(Database.DatabaseHelper.ExecuteToJSON(vals["cmd"])), ASCOperationPrivilege.Administrator, "cmd"),
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
            #region REQUESTS HANDLING

            if (Regex.IsMatch(url, @"[\\\/]?favicon\.ico$", RegexOptions.IgnoreCase))
                url = "res~favicon~image/x-icon";
            
            bool mobile = IsMobile(request);
            string session = null;
            DBUser user;

            SetStatusCode(response, StatusCode._200);

            if (contains(request, "lang", out string lang))
                lang = lang?.ToLower();
            else
            {
                Cookie langc = request.Cookies["_lang"];

                if (langc?.Value?.Length > 0)
                    lang = langc.Value;
            }

            lang = lang ?? "en";

            if (!LanguagePacks.ContainsKey(lang))
                lang = (from lraw in request.UserLanguages
                        let lng = lraw.Contains(';') ? lraw.Split(';')[0] : lraw
                        let lcd = (lng.Contains('-') ? lng.Split('-')[0] : lng).ToLower().Trim()
                        where LanguagePacks.ContainsKey(lcd)
                        select lcd).FirstOrDefault();

            foreach (KeyValuePair<string, string> kvp in LanguagePacks[lang ?? "en"])
                vars[kvp.Key] = kvp.Value;

            vars["lang_avail"] = string.Join(", ", from lp in LanguagePacks.Keys select $"\"{lp}\"");
            vars["mobile"] = ToJSbool(mobile);
            vars["url"] = url;
            vars["ssl"] = ToJSbool(SSL(port)); 
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
                            if (ascop.Privilege > ASCOperationPrivilege.Regular)
                            {
                                bool res = contains(request, "id", out string sid);

                                if (contains(request, "hash", out string hash))
                                    res &= verify(request, tSQL, sid, hash, out session);
                                else if (contains(request, "session", out session))
                                    res &= tSQL.VerifySession(session); // TODO
                                else
                                    res &= ((user = getsessionuser()) != null)
                                        & (ascop.Privilege == ASCOperationPrivilege.Administrator ? user.IsAdmin : true);

                                if (!res)
                                    return error($"An {ascop.Privilege.ToString().ToLower()} account is required to perform this operation", StatusCode._403);
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

            if ((user = getsessionuser()) != null)
            {
                DBUserAuthentification auth = tSQL.GetAuth(user.ID);

                tSQL.Login(user.ID, auth.Hash, request.UserHostAddress, request.UserAgent, out session); // update login

                auth.Session = session;

                vars["user"] = JsonConvert.SerializeObject(user, Formatting.Indented);
                vars["user_auth"] = JsonConvert.SerializeObject(auth, Formatting.Indented);
                vars["user_session"] = session;
                vars["inner"] = FetchResource(Resources.chat, vars);

                response.SetCookie(new Cookie("_sess", session));
            }
            else
            {
                vars["user"] =
                vars["user_auth"] = "undefined";
                vars["inner"] = FetchResource(Resources.login, vars);
            }

            if (mobile)
            {
                vars["style_desktop"] = "";
                vars["style_mobile"] = FetchResource(Resources.style_mobile, vars);
            }
            else
            {
                vars["style_desktop"] = FetchResource(Resources.style_desktop, vars);
                vars["style_mobile"] = "";
            }

            vars["pre_script"] = FetchResource(Resources.pre_script, vars);
            vars["post_script"] = FetchResource(Resources.post_script, vars);
            vars["style"] = FetchResource(Resources.style, vars);

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
            else
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

            DBUser getsessionuser()
            {
                Cookie sessc = request.Cookies["_sess"];

                return tSQL.VerifySession(session = sessc?.Value ?? "") ? tSQL.GetUserBySession(session) : null;
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

        internal static string ToJSbool(bool? val) => (val ?? false).ToString().ToLower(); // JavaScript being bitchy

        internal static bool IsMobile(HttpListenerRequest req) =>
            (req.UserAgent.Length < 4) ||
            REGEX_MOBILE.IsMatch(req.UserAgent) ||
            REGEX_MOBILEVERSION.IsMatch(req.UserAgent.Substring(0, 4));

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
        /// Returns the required privilege
        /// </summary>
        public ASCOperationPrivilege Privilege { get; }

        /// <summary>
        /// Creates a new instance
        /// </summary>
        /// <param name="handler">The operation's handler</param>
        /// <param name="priv">The operation's required privilege</param>
        /// <param name="keys">The operation's required query keys</param>
        public ASCOperation(Func<HttpListenerRequest, HttpListenerResponse, Dictionary<string, string>, Database, dynamic> handler, ASCOperationPrivilege priv = ASCOperationPrivilege.Regular, params string[] keys)
        {
            this.Keys = keys ?? new string[0];
            this.Privilege = priv;
            this.Handler = handler ?? ((req, res, k, db) => null);
        }
    }

    /// <summary>
    /// An enumeration of possible required ASC operation privileges
    /// </summary>
    public enum ASCOperationPrivilege
        : byte
    {
        /// <summary>
        /// No privileges required (public operation)
        /// </summary>
        Regular = 0,
        /// <summary>
        /// User privilege required
        /// </summary>
        User = 1,
        /// <summary>
        /// Administrative privilege required
        /// </summary>
        Administrator = 2
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
