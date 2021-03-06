﻿using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Drawing.Imaging;
using System.Net.Sockets;
using System.Diagnostics;
using System.Reflection;
using System.Drawing;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System;

using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

using Resources = ASC.Server.Properties.Resources;

using static ASC.Server.StatusCode;
using static ASC.Server.Program;

namespace ASC.Server
{
    /// <summary>
    /// Represents an ASC-server
    /// </summary>
    public sealed class ASCServer
        : IDisposable
    {
        private delegate HTTPResponse ErrorDelegate(string msg, StatusCode code = _400, params object[] args);

        internal static readonly Regex REGEX_MOBILEVERSION = new Regex(@"1207|6310|6590|3gso|4thp|50[1-6]i|770s|802s|a wa|abac|ac(er|oo|s\-)|ai(ko|rn)|al(av|ca|co)|amoi|an(ex|ny|yw)|aptu|ar(ch|go)|as(te|us)|attw|au(di|\-m|r |s )|avan|be(ck|ll|nq)|bi(lb|rd)|bl(ac|az)|br(e|v)w|bumb|bw\-(n|u)|c55\/|capi|ccwa|cdm\-|cell|chtm|cldc|cmd\-|co(mp|nd)|craw|da(it|ll|ng)|dbte|dc\-s|devi|dica|dmob|do(c|p)o|ds(12|\-d)|el(49|ai)|em(l2|ul)|er(ic|k0)|esl8|ez([4-7]0|os|wa|ze)|fetc|fly(\-|_)|g1 u|g560|gene|gf\-5|g\-mo|go(\.w|od)|gr(ad|un)|haie|hcit|hd\-(m|p|t)|hei\-|hi(pt|ta)|hp( i|ip)|hs\-c|ht(c(\-| |_|a|g|p|s|t)|tp)|hu(aw|tc)|i\-(20|go|ma)|i230|iac( |\-|\/)|ibro|idea|ig01|ikom|im1k|inno|ipaq|iris|ja(t|v)a|jbro|jemu|jigs|kddi|keji|kgt( |\/)|klon|kpt |kwc\-|kyo(c|k)|le(no|xi)|lg( g|\/(k|l|u)|50|54|\-[a-w])|libw|lynx|m1\-w|m3ga|m50\/|ma(te|ui|xo)|mc(01|21|ca)|m\-cr|me(rc|ri)|mi(o8|oa|ts)|mmef|mo(01|02|bi|de|do|t(\-| |o|v)|zz)|mt(50|p1|v )|mwbp|mywa|n10[0-2]|n20[2-3]|n30(0|2)|n50(0|2|5)|n7(0(0|1)|10)|ne((c|m)\-|on|tf|wf|wg|wt)|nok(6|i)|nzph|o2im|op(ti|wv)|oran|owg1|p800|pan(a|d|t)|pdxg|pg(13|\-([1-8]|c))|phil|pire|pl(ay|uc)|pn\-2|po(ck|rt|se)|prox|psio|pt\-g|qa\-a|qc(07|12|21|32|60|\-[2-7]|i\-)|qtek|r380|r600|raks|rim9|ro(ve|zo)|s55\/|sa(ge|ma|mm|ms|ny|va)|sc(01|h\-|oo|p\-)|sdk\/|se(c(\-|0|1)|47|mc|nd|ri)|sgh\-|shar|sie(\-|m)|sk\-0|sl(45|id)|sm(al|ar|b3|it|t5)|so(ft|ny)|sp(01|h\-|v\-|v )|sy(01|mb)|t2(18|50)|t6(00|10|18)|ta(gt|lk)|tcl\-|tdg\-|tel(i|m)|tim\-|t\-mo|to(pl|sh)|ts(70|m\-|m3|m5)|tx\-9|up(\.b|g1|si)|utst|v400|v750|veri|vi(rg|te)|vk(40|5[0-3]|\-v)|vm40|voda|vulc|vx(52|53|60|61|70|80|81|83|85|98)|w3c(\-| )|webc|whit|wi(g |nc|nw)|wmlb|wonu|x700|yas\-|your|zeto|zte\-", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);
        internal static readonly Regex REGEX_MOBILE = new Regex(@"android|(android|bb\d+|meego).+mobile|avantgo|bada\/|blackberry|blazer|compal|elaine|fennec|hiptop|iemobile|ip(hone|od)|iris|kindle|lge |maemo|midp|mmp|mobile.+firefox|netfront|opera m(ob|in)i|palm( os)?|phone|p(ixi|re)\/|plucker|pocket|psp|series(4|6)0|symbian|treo|up\.(browser|link)|vodafone|wap|windows (ce|phone)|xda|xiino", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);
        internal static readonly IPHostEntry LOCALHOST = Dns.GetHostEntry(IPAddress.IPv6Loopback);

        internal static dynamic Unit { get; } = new { };

        internal static string NonExistantPath { get; set; } // only used for client-side cookies
        internal static Dictionary<long, DBUser> TemporaryUsers { get; } = new Dictionary<long, DBUser>();
        internal static Dictionary<string, Dictionary<string, string>> LanguagePacks { get; }
        internal static Dictionary<StatusCode, string> StatusCodes { get; } = new Dictionary<StatusCode, string>
        {
            [_200] = "OK",
            [_400] = "Invalid operation",
            [_403] = "Forbidden",
            [_404] = "Not found",
            [_420] = "420/Weed",
            [_500] = "Server Error",
        };
        internal Dictionary<string, ASCOperation> Operations { get; }
        internal (int HTTP, int HTTPS) Ports { get; }
        internal Func<int, bool> SSL { get; }
        internal string ServerString { get; }
        internal Database tSQL { get; set; }
        internal HTTPServer Server { get; }

        private readonly unsafe bool* acceptconnections;


        static ASCServer() => LanguagePacks = (from f in Directory.GetFiles($@"{Directory.GetCurrentDirectory()}\Languages", "*.json", SearchOption.AllDirectories)
                                               let nfo = new FileInfo(f)
                                               let code = nfo.Name.Replace(nfo.Extension, "")
                                               select (code, BuildLanguageDictionary(f))).ToDictionary(_ => _.Item1, _ => _.Item2);

        /// <summary>
        /// Creates a new instance
        /// </summary>
        /// <param name="port">The HTTP port</param>
        /// <param name="accept">A pointer to an boolean value, which indicates whether the server shall accept incomming requests</param>
        /// <param name="tsql">The underlying Transact-SQL [tSQL] database</param>
        public unsafe ASCServer(int port, bool* accept, Database tsql)
        {
            acceptconnections = accept;

            foreach (KeyValuePair<string, Dictionary<string, string>> dic in LanguagePacks)
                $"Language dictionary '{dic.Value["lang_name"]} ({dic.Key})' loaded.".Ok();

            ServerString = $"ASC Server/{Assembly.GetExecutingAssembly().GetName().Version} Unknown6656/420.1337.14.88";
            Operations = new Dictionary<string, ASCOperation>
            {
                #region PUBLIC TOOLS

                ["server_info"] = new ASCOperation(delegate
                {
                    return new
                    {
                        ServerString = this.ServerString,
                        ServerTime = DateTime.Now,
                        IsOnline = true,
                        ServerGUID = Win32.GUID,
                        SupportedLanguages = LanguagePacks.Keys.ToArray(),
                        HostNames = Program.HostNames,
                        HTTPPort = port,
                        HTTPSPort = port + 1,
                    };
                }),
                ["available_lang"] = new ASCOperation((req, res, vals, db) => LanguagePacks.Keys.ToArray()),
                ["lang_pack"] = new ASCOperation((req, res, vals, db) => LanguagePacks[vals["code"].ToLower()], keys: "lang"),
                ["lang_info"] = new ASCOperation((req, res, vals, db) =>
                {
                    Dictionary<string, string> pack = LanguagePacks[vals["code"].ToLower()];

                    return new
                    {
                        Name = pack["lang_name"],
                        EnglishName = pack["lang_iname"],
                        IsBeta = bool.Parse(pack["lang_beta"].ToLower()),
                    };
                }, keys: "lang"),
                ["user_by_name"] = new ASCOperation((req, res, vals, db) => db.FindUsers(vals["name"]), keys: "name"),
                ["user_by_id"] = new ASCOperation((req, res, vals, db) => db.GetUser(long.Parse(vals["id"])), keys: "id"),
                ["user_by_guid"] = new ASCOperation((req, res, vals, db) => db.GetUser(Guid.Parse(vals["guid"])), keys: "guid"),
                ["can_use_name"] = new ASCOperation((req, res, vals, db) => db.ValidateUserName(vals["name"]) && db.CanChangeName(vals["name"]), keys: "name"),

                #endregion
                #region PRIVATE / AUTHENTIFICATION TOOLS

                ["auth_register"] = new ASCOperation((req, res, vals, db) => {
                    ReCaptchaResult result = null;

                    using (WebClient wc = new WebClient())
                        result = JsonConvert.DeserializeObject<ReCaptchaResult>(Encoding.UTF8.GetString(wc.UploadValues("https://www.google.com/recaptcha/api/siteverify", new NameValueCollection
                        {
                            ["secret"] = Program.recaptcha_private ?? "",
                            ["response"] = vals["g-recaptcha-response"],
                            // ["remoteip"] = vars["user_addr"],
                        })));

                    if (result?.success ?? false /* && (result.hostname.ToLower() != req.UserHostName.ToLower()) */)
                    {
                        DBUser user = new DBUser
                        {
                            IsBlocked = false,
                            Name = vals["name"],
                        };
                        DBUserAuthentification auth = db.AddUser(ref user);

                        lock (TemporaryUsers)
                            TemporaryUsers[user.ID] = user;

                        return new
                        {
                            ID = user.ID,
                            UUID = user.UUID,
                            Salt = auth.Salt,
                            Hash = auth.Hash,
                        };
                    }
                    //else
                    //    DeleteTemporaryUser(??, db);

                    throw null;
                }, ASCOperationPrivilege.Regular, "name", "g-recaptcha-response"),
                ["auth_verify_sesion"] = new ASCOperation((req, res, vals, db) => db.VerifySession(vals["session"]), keys: "session"),
                ["auth_salt"] = new ASCOperation((req, res, vals, db) => db.GetUserSalt(long.Parse(vals["id"])), keys: "id"),
                ["auth_change_pw"] = new ASCOperation((req, res, vals, db) => db.UpdateUserHash(long.Parse(vals["id"]), vals["newhash"]), ASCOperationPrivilege.User, "newhash"),
                ["auth_login"] = new ASCOperation((req, res, vals, db) => new
                {
                    Success = verify(req, db, vals["id"], vals["hash"], vals["location"], out string session),
                    Session = session ?? ""
                }, ASCOperationPrivilege.Regular, "id", "hash")
                { RequiresLocation = true },
                ["auth_refr_session"] = new ASCOperation((req, res, vals, db) => Unit, ASCOperationPrivilege.User, keys: "session"),
                ["auth_update"] = new ASCOperation((req, res, vals, db) => db.UpdateUserHash(long.Parse(vals["id"]), vals["new"]), keys: "new") { RequiresLocation = true },
                ["delete_tmp"] = new ASCOperation((req, res, vals, db) => {
                    lock (TemporaryUsers)
                        return DeleteTemporaryUser(long.Parse(vals["id"]), db);
                }, ASCOperationPrivilege.Regular, "id", "hash"),

                #endregion
                #region ADMINISTRATIVE TOOLS

                ["raw_sql"] = new ASCOperation((req, res, vals, db) => JsonConvert.DeserializeObject(Database.DatabaseHelper.ExecuteToJSON(vals["cmd"])), ASCOperationPrivilege.Administrator, "cmd"),
                ["save_log"] = new ASCOperation(delegate {
                    ConsoleLogger.Save(Directory.GetCurrentDirectory());

                    return Unit;
                }, ASCOperationPrivilege.Administrator),
                ["shutdown"] = new ASCOperation(delegate {
                    throw new ForcedShutdown();
                }, ASCOperationPrivilege.Administrator),
                ["display"] = new ASCOperation((req, res, vals, db) => vals[vals["_key"]], ASCOperationPrivilege.Administrator, keys : "_key"),
                ["clear_tmp"] = new ASCOperation((req, res, vals, db) => {
                    DeleteTemporaryUsers(db);

                    return Unit;
                }, ASCOperationPrivilege.Administrator),

                #endregion
            };

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
            // conditional pattern:     §{cond_var?true_var:format|false_var:format}§
            // variable pattern:        §variable:format§
            // parameter pattern:       §0:format§
            //                          §0

            const string pat_cond = @"\§\{(?<cond>\w+)\?(?<key1>(\w+|\-))(\:(?<format1>[^§]+))?\|(?<key2>(\w+|\-))(\:(?<format2>[^§]+))?\}\§";
            const string pat_dic = @"\§(?<key>\w+)(\:(?<format>[^§]+))?\§";
            const string pat_par = @"\§(?<num>[0-9]+)(\:(?<format>[^§]+)§)?";
            List<object> values = new List<object>();
            IEnumerable<Match> matches;
            int rcount = 0;

            obj = obj.Replace("{", "{{");
            obj = obj.Replace("}", "}}");

            rcount = (matches = Regex.Matches(obj, pat_par).Cast<Match>()
                                                           .OrderByDescending(m => m.Index))
                                                           .Select(m => int.Parse(m.Groups["num"].ToString()))
                                                           .Union(new int[] { 0 })
                                                           .Distinct()
                                                           .Max();

            foreach (Match m in matches)
            {
                string repl = $"{{{m.Groups["num"]}}}";

                if (m.Groups["format"].Length > 0)
                    repl = $"{{{m.Groups["num"]}:{m.Groups["format"]}}}";

                obj = Replace(obj, m.Index, m.Length, repl);
            }

            if (matches.Any())
                ++rcount;

            while (regex(obj, pat_cond, out Match m))
                SplitMerge(m, delegate
                {
                    string cond = m.Groups["cond"].ToString();
                    bool bcond = variables?.ContainsKey(cond) ?? false ? bool.TryParse(variables?[cond].ToString(), out bcond) : false;
                    int index = bcond ? 1 : 2;

                    return (m.Groups["key" + index].ToString(), m.Groups["format" + index]?.ToString() ?? "");
                });

            while (regex(obj, pat_dic, out Match m))
                SplitMerge(m, delegate
                {
                    string key = m.Groups["key"].ToString();
                    string format = m.Groups["format"]?.ToString() ?? "";

                    return (key, format);
                });

            args = args.Concat(values).ToArray();

            return string.Format(obj, args);

            void SplitMerge(Match m, Func<(string key, string format)> callback)
            {
                string head = obj.Substring(0, m.Index);
                string tail = obj.Substring(m.Index + m.Length);
                (string key, string format) = callback();

                if (format.Length > 0)
                    format = ':' + format;

                values.Add(variables?.ContainsKey(key) ?? false ? variables?[key] : format = "");

                obj = $"{head}{{{rcount++}{format}}}{tail}";
            }
            string Replace(string s, int index, int length, string replacement) => new StringBuilder()
                                                                                   .Append(s.Substring(0, index))
                                                                                   .Append(replacement)
                                                                                   .Append(s.Substring(index + length))
                                                                                   .ToString();
        }

        /// <summary>
        /// Processes the given HTTP listener request and writes the processing result into the given HTTP listener response
        /// </summary>
        /// <param name="request">HTTP listener request</param>
        /// <param name="clientdata">The data send by the client</param>
        /// <param name="response">HTTP listener response</param>
        /// <param name="geoip">GeoIP fetcher task</param>
        /// <returns>HTTP response data</returns>
        public HTTPResponse SendResponse(HttpListenerRequest request, byte[] clientdata, HttpListenerResponse response, Task<GeoIPResult> geoip)
        {
            #region INIT

            DateTime dt_req = DateTime.Now;
            Dictionary<string, object> vars = new Dictionary<string, object>
            {
                ["nexpath"] = NonExistantPath
            };
            int port = request.LocalEndPoint.Port;
            string path = request.Url.LocalPath;
            string url = request.RawUrl;
            var json_timestamp = new
            {
                Raw = dt_req,
                SinceUnix = new DateTimeOffset(dt_req).ToUnixTimeMilliseconds(),
            };
            bool _hasinitlang = false;

            response.Headers[HttpResponseHeader.Server] = ServerString;
            response.Headers[HttpResponseHeader.Connection] = "keep-alive"; // upgrade
            // response.Headers[HttpRequestHeader.Upgrade] = "h2c";

            Stopwatch sw = new Stopwatch();

            sw.Start();

            unsafe
            {
                while (!*acceptconnections)
                    if (sw.ElapsedMilliseconds > 30000)
                        return SendError(request, response, vars, _500, "");
            }

            bool mobile = IsMobile(request);

            SetStatusCode(response, _200);

            #endregion
            #region ENVIRONMENT VARIABLES

            vars["location"] = "unknown";
            vars["lang_avail"] = string.Join(", ", from lp in LanguagePacks.Keys select $"\"{lp}\"");
            vars["mobile"] = ToJSbool(mobile);
            vars["url_path"] = path;
            vars["url"] = url;
            vars["ssl"] = ToJSbool(SSL(port));
            vars["time"] = dt_req;
            vars["port"] = port;
            vars["port_http"] = Ports.HTTP;
            vars["port_https"] = Ports.HTTPS;
            vars["protocol"] = request.Url.Scheme;
            vars["host"] = request.Url.Host;
            vars["addr"] = request.LocalEndPoint.Address;
            vars["user"] = "undefined";
            vars["user_agent"] = request.UserAgent;
            vars["user_host"] = request.UserHostName;
            vars["user_addr"] = request.UserHostAddress;
            vars["main_page"] = "false";
            vars["timestamp"] = json_timestamp;
            vars["compact"] = contains(request, "_display", out string comp) && (comp.ToLower().Trim() == "compact");

            #endregion
            #region RESOURCE REQUEST

            if (Regex.IsMatch(path, @"[\\\/]?favicon\.ico$", RegexOptions.IgnoreCase))
                url = "res~favicon~image/x-icon";

            initlang();

            if (regex(path, @"(.*[\\\/])?res\~(?<res>.+)\~(?<type>[\w\-\/\-\+]+)", out Match m))
            {
                string resource = m.Groups["res"].ToString();

                try
                {
                    SetStatusCode(response, _200);

                    response.ContentType = m.Groups["type"].ToString();

                    $"Processing resource '{resource}' with MIME-type '{response.ContentType}'...".Msg();

                    foreach (Func<HTTPResponse> f in new Func<HTTPResponse>[] {
                        () => {
                            string dir = Directory.GetCurrentDirectory() + "\\";
                            string respath = $"{dir}\\Resources\\{resource}";

                            if (regex(resource, @"userimage\:\{?(?<guid>[^\{\}]+)\}?", out m))
                            {
                                respath = $"{dir}{DIR_PROFILEIMAGES}\\{{{m.Groups["guid"]}}}.png";

                                if (!File.Exists(respath))
                                    return (Resources.profile_default, ImageFormat.Png);
                            }
                            else if (regex(resource, @"media\:(?<id>.+)", out m))
                                ; // TODO

                            return File.ReadAllBytes(respath);
                        },
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
                                    return (i, ImageFormat.Png);
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
                            HTTPResponse resp = f();

                            if (resp.Length > 0)
                                return resp;
                        }
                        catch
                        {
                        }
                }
                catch
                {
                    $"Resource '{resource}' not found.".Warn();

                    setstyles();

                    return SendError(request, response, vars, _404, $"The resource '{resource}' could not be found.");
                }
            }

            #endregion
            #region OPERATION REQUEST

            string session = null;
            DBUser user = null;

            if (regex(path, @"^[\\\/]?api\.json$", out _))
            {
                HTTPResponse resp = SendOperationResponse(error, vars, request, response, geoip, ref session, out user);
#if DEBUG
                $"API response: {resp.Text}".Info();
#endif
                return resp;
            }

            #endregion
            #region GEOIP HANDLING

            InitLocation(geoip, ref vars);
            setstyles();

            #endregion
            #region SESSION + AUTHENTIFICATION VARIABLES

            bool successful_login = false;

            if (contains(request, "hash", out string hash) && contains(request, "id", out string id))
                verify(request, tSQL, id, hash, vars["location"].ToString(), out session);

            if ((user = getsessionuser()) != null)
            {
                DBUserAuthentification auth = tSQL.GetAuth(user.ID);

                successful_login = tSQL.Login(user.ID, auth.Hash, request.UserHostAddress, request.UserAgent, vars["location"].ToString(), out session); // update login

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
                vars["main_page"] = "true";
            }

            #endregion
            #region USER PROFILE

            if (regex(path.Replace('\\', '/'), @"^\/?user\/(?<name>\w+)\b?", out m))
                if (successful_login)
                {
                    DBUser puser = null;

                    try
                    {
                        puser = tSQL.GetUser(m.Groups["name"].ToString());
                    }
                    catch
                    {
                    }

                    if (puser == null)
                        return SendError(request, response, vars, _403, vars["error_userprofile_notfound"].ToString());

                    vars["inner"] = FetchResource(Resources.user, vars, puser.UUID, puser.Name, puser.MemberSince, puser.Status); // TODO
                }
                else
                    return SendError(request, response, vars, _403, vars["error_userprofile_denied"].ToString());

            #endregion

            setstyles(); // environment variables may be updated

            return FetchResource((bool)vars["compact"] ? Resources.frame_compact : Resources.frame, vars);


            HTTPResponse error(string msg, StatusCode code = _400, params object[] args)
            {
                SetStatusCode(response, code);

                return ToJSON(new
                {
                    Success = false,
                    Session = session,
                    Data = (args ?? new object[0]).Length > 0 ? string.Format(vars[msg].ToString(), args) : vars[msg],
                    TimeStamp = json_timestamp,
                });
            }
            DBUser getsessionuser()
            {
                string sessc = request.Cookies["_sess"]?.Value ?? "";

                if (sessc.Length > 0)
                    session = sessc;

                return tSQL.VerifySession(session) ? tSQL.GetUserBySession(session) : null;
            }
            void setstyles()
            {
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
            }
            void initlang()
            {
                if (_hasinitlang)
                    return;
                else
                    _hasinitlang = true;

                if (contains(request, "lang", out string lang))
                    lang = lang?.ToLower();
                else
                {
                    Cookie langc = request.Cookies["_lang"];

                    if (langc?.Value?.Length > 0)
                        lang = langc.Value;
                    else
                        lang = request.UserLanguages.Select(_ => _.ToLower()).Intersect(LanguagePacks.Keys).FirstOrDefault();
                }

                lang = (lang ?? "en").ToLower();

                if (!LanguagePacks.ContainsKey(lang))
                    lang = (from lraw in request.UserLanguages
                            let lng = lraw.Contains(';') ? lraw.Split(';')[0] : lraw
                            let lcd = (lng.Contains('-') ? lng.Split('-')[0] : lng).ToLower().Trim()
                            where LanguagePacks.ContainsKey(lcd)
                            select lcd).FirstOrDefault();

                foreach (KeyValuePair<string, string> kvp in LanguagePacks[lang ?? "en"])
                    vars[kvp.Key] = kvp.Value;
            }
        }

        private HTTPResponse SendOperationResponse(ErrorDelegate error, Dictionary<string, object> vars, HttpListenerRequest request, HttpListenerResponse response, Task<GeoIPResult> geoip, ref string session, out DBUser user)
        {
            user = null;
#if DEBUG
            $"API access: {vars["url"]}".Info();
#endif
            if (!contains(request, "operation", out string op))
                return error("error_api_missingop");
            else if (!Operations.ContainsKey(op = op.ToLower()))
                return error("error_api_unknownop", args: op);
            else
            {
                ASCOperation ascop = Operations[op];
                Dictionary<string, string> values = new Dictionary<string, string>();

                foreach (string key in request.QueryString.AllKeys)
                    if (key != null)
                        values[key] = request.QueryString[key];

                IEnumerable<string> missing = ascop.Keys.Except(values.Keys);

                if (missing.Any())
                    return error("error_api_valuerequired", _400, missing.First(), op);

                foreach (string key in vars.Keys)
                    if (!values.ContainsKey(key))
                        values[key] = vars[key].ToString();

                try
                {
                    if (ascop.RequiresLocation || (ascop.Privilege > ASCOperationPrivilege.Regular))
                        InitLocation(geoip, ref vars);

                    string loc = values["location"] = vars["location"].ToString();

                    if (ascop.Privilege > ASCOperationPrivilege.Regular)
                    {
                        bool res = contains(request, "id", out string sid);

                        user = default(DBUser);
                        session = null;

                        if (contains(request, "hash", out string hash))
                            res &= verify(request, tSQL, sid, hash, loc, out session);

                        if (session == null)
                            if (contains(request, "session", out session))
                            {
                                res |= tSQL.VerifySession(session);

                                if (res)
                                {
                                    DBUser temp = tSQL.GetUserBySession(session);

                                    tSQL.AutoLogin(temp.ID, request.RemoteEndPoint.ToString(), request.UserAgent, loc, out session);

                                    user = temp; // copy after login due to possible failure
                                }
                            }
                            else
                            {
                                Cookie sessc = request.Cookies["_sess"];

                                res &= tSQL.VerifySession(session = sessc?.Value ?? "");
                                user = res ? tSQL.GetUserBySession(session) : null;
                            }
                        else if (long.TryParse(sid, out long id))
                            user = tSQL.GetUser(id);

                        res &= ascop.Privilege == ASCOperationPrivilege.Administrator ? user?.IsAdmin ?? false : true;

                        if (!res)
                            return error(ascop.Privilege == ASCOperationPrivilege.Administrator ? "error_api_needsadmin" : "error_api_needsuser", _403);
                    }

                    vars["user_session"] = session;

                    return ToJSON(new
                    {
                        Success = true,
                        Session = session,
                        Data = ascop.Handler(request, response, values, tSQL),
                        TimeStamp = vars["timestamp"],
                    });
                }
                catch (ForcedShutdown)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    return error("error_api_malformatted", _400, ex.Message, ex.StackTrace);
                }
            }
        }

        private HTTPResponse SendError(HttpListenerRequest request, HttpListenerResponse response, Dictionary<string, object> vars, StatusCode code, string msg = null)
        {
            SetStatusCode(response, code);

            vars["error_code"] = ((int)code).ToString();
            vars["error_message"] = vars[$"error{code}"].ToString();
            vars["error_submessage"] = msg ?? "";
            vars["inner"] = FetchResource(Resources.error, vars);
            vars["main_page"] = "true"; // prevent redirect

            vars["pre_script"] = FetchResource(Resources.pre_script, vars); // update
            vars["post_script"] = FetchResource(Resources.post_script, vars); // update

            return FetchResource(Resources.frame, vars);
        }

        private void InitLocation(Task<GeoIPResult> geoip, ref Dictionary<string, object> vars)
        {
            geoip.Wait();

            vars["location"] = geoip.Result?.ToString() ?? "unknown";
        }

        private void SetStatusCode(HttpListenerResponse resp, StatusCode code)
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

        internal static bool verify(HttpListenerRequest req, Database db, string id, string hash, string loc, out string session)
        {
            session = null;

            bool res = long.TryParse(id, out long l) && db.Login(l, hash, req.RemoteEndPoint.ToString(), req.UserAgent, loc, out session);

            if (res && TemporaryUsers.ContainsKey(l))
                TemporaryUsers.Remove(l);

            return res;
        }

        internal static HTTPResponse ToJSON<T>(T obj)
        {
            return JsonConvert.SerializeObject(convert(obj));

            dynamic convert(dynamic o)
            {
                if (o is null)
                    return null;

                Type t = o.GetType();

                if (isanonymous(t))
                {
                    Dictionary<string, dynamic> copy = new Dictionary<string, dynamic>();
                    ConstructorInfo ctor = t.GetConstructors().Single();

                    foreach (PropertyInfo prop in t.GetRuntimeProperties())
                    {
                        object pval = prop.GetValue(o);

                        copy[prop.Name] = pval is string s ? ConvertEncoding(s, Encoding.Unicode, HTTPResponse.Codepage) : convert(pval);
                    }

                    dynamic[] @params = ctor.GetParameters().Select(p => copy[p.Name]).ToArray();

                    return ctor.Invoke(@params);
                }
                else
                    return o;
            }

            bool isanonymous(Type type) => Attribute.IsDefined(type, typeof(CompilerGeneratedAttribute), false)
                                        && type.IsGenericType
                                        && type.Name.Contains("AnonymousType")
                                        && (type.Name.StartsWith("<>") || type.Name.StartsWith("VB$"))
                                        && type.Attributes.HasFlag(TypeAttributes.NotPublic);
        }

        /// <summary>
        /// Converts the given string from the source to the destination encoding
        /// </summary>
        /// <param name="text">Input text</param>
        /// <param name="src">Source Encoding</param>
        /// <param name="dst">Destination Encoding</param>
        /// <returns>Converted text</returns>
        public static string ConvertEncoding(string text, Encoding src, Encoding dst)
        {
            byte[] dat = src.GetBytes(text);
            byte[] res = Encoding.Convert(src, dst, dat);

            return dst.GetString(res);
        }

        internal static bool DeleteTemporaryUsers(Database db)
        {
            bool result = false;

            lock (TemporaryUsers)
                foreach (long id in TemporaryUsers.Keys)
                    result |= DeleteTemporaryUser(id, db);

            return result;
        }

        internal static bool DeleteTemporaryUser(long id, Database db)
        {
            DBUser user = TemporaryUsers[id];
            bool result;

            if (result = db?.HasUser(id) ?? false)
            {
                db?.DeleteUser(id);

                $"Temporary user {{{user.UUID}}} ({user.Name}) deleted.".Ok();
            }

            return result;
        }


        // TODO:  private abstract class ServerResponseHandler ....
        // TODO:  private struct ServerResponseContext ...
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
        /// Defines, whether the current operation needs location information about the requested IP address
        /// </summary>
        public bool RequiresLocation { set; get; }

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

    /// <summary>
    /// Represents a GeoIP query result
    /// </summary>
    public sealed class GeoIPResult
    {
        /// <summary>
        /// Returns the default GeoIP query result
        /// </summary>
        public static GeoIPResult Default { get; } = new GeoIPResult
        {
            country_code = "--",
            country_name = "[unknown]",
            city = "[unknown]",
            postal = "-----",
            latitude = null,
            longitude = null,
            IPv4 = "---.---.---.---",
            state = "[unknown]",
        };

        /// <summary>
        /// The country code
        /// </summary>
        public string country_code { set; get; }
        /// <summary>
        /// The country name
        /// </summary>
        public string country_name { set; get; }
        /// <summary>
        /// The city name
        /// </summary>
        public string city { set; get; }
        /// <summary>
        /// The city postal code
        /// </summary>
        public string postal { set; get; }
        /// <summary>
        /// The latitude (°N)
        /// </summary>
        public decimal? latitude { set; get; }
        /// <summary>
        /// The longitude (°E)
        /// </summary>
        public decimal? longitude { set; get; }
        /// <summary>
        /// The IPv4 address
        /// </summary>
        public string IPv4 { set; get; }
        /// <summary>
        /// The state name
        /// </summary>
        public string state { set; get; }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString() => $"{postal} - {city},\n{state},\n{country_code} - {country_name}\n({latitude}, {longitude})";

        internal string ToShortString() => ToString().Replace('\n', ' ');
    }

    /// <summary>
    /// Represents a ReCaptcha query result
    /// </summary>
    public sealed class ReCaptchaResult
    {
        /// <summary>
        /// Success state
        /// </summary>
        public bool success { set; get; }
        /// <summary>
        /// The challenge timestamp
        /// </summary>
        public string challenge_ts { set; get; }
        /// <summary>
        /// The host name
        /// </summary>
        public string hostname { set; get; }
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
