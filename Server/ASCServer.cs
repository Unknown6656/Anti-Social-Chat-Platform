﻿using System.Text.RegularExpressions;
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
        internal Func<int, bool> SSL { get; }
        internal string ServerString { get; }
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
                ["login"] = new ASCOperation((req, res, vals) => {

                    return null;
                }, "id", "hash"),
            };
        }

        /// <summary>
        /// Creates a new instance
        /// </summary>
        /// <param name="port">The HTTP port</param>
        /// <param name="accept">A pointer to an boolean value, which indicates whether the server shall accept incomming requests</param>
        public ASCServer(int port, bool* accept)
        {
            acceptconnections = accept;

            foreach (KeyValuePair<string, Dictionary<string, string>> dic in LanguagePacks)
                $"Language dictionary '{dic.Value["lang_name"]} ({dic.Key})' loaded.".Ok();

            ServerString = $"ASC Server/{Assembly.GetExecutingAssembly().GetName().Version} Unknown6656/420.1337.14.88";

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

            if (regex(url, @"res\~(?<res>.+)\~(?<type>[\w\-\/]+)\b?", out Match m))
            {
                string resource = m.Groups["res"].ToString();

                try
                {
                    SetStatusCode(response, StatusCode._200);

                    response.ContentType = m.Groups["type"].ToString();

                    $"Processing resource '{resource}' with MIME-type '{response.ContentType}'...".Msg();

                    foreach (Func<HTTPResponse> f in new Func<HTTPResponse>[] {
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
                SetStatusCode(response, StatusCode._200);

                if (contains(request, "lang", out string lang))
                {
                    lang = lang?.ToLower();

                    if (!LanguagePacks.ContainsKey(lang))
                        lang = (from lraw in request.UserLanguages
                                let lng = lraw.Contains(';') ? lraw.Split(';')[0] : lraw
                                let lcd = (lng.Contains('-') ? lng.Split('-')[0] : lng).ToLower().Trim()
                                where LanguagePacks.ContainsKey(lcd)
                                select lcd).FirstOrDefault();
                }

                foreach (KeyValuePair<string, string> kvp in LanguagePacks[lang ?? "en"])
                    vars[kvp.Key] = kvp.Value;

                vars["time"] = now;
                vars["port"] = port;
                vars["ssl"] = SSL(port);
                vars["user_agent"] = request.UserAgent;
                vars["user_host"] = request.UserHostName;
                vars["user_addr"] = request.UserHostAddress;
                vars["script"] = FetchResource(Resources.script, vars);
                vars["style"] = FetchResource(Resources.style, vars);

                if (contains(request, "operation", out string op))
                    if (Operations.ContainsKey(op = op.ToLower()))
                    {
                        ASCOperation ascop = Operations[op];
                        string[] values = new string[ascop.Keys.Length];
                        int indx = 0;

                        foreach (string key in ascop.Keys)
                            if (contains(request, key, out string val))
                                values[indx++] = val ?? "";
                            else
                                return SendError(request, response, vars, StatusCode._400, $"A value for '{key}' is required when using the operation '{op}'.");

                        return ascop.Handler(request, response, values);
                    }
                    else
                        return SendError(request, response, vars, StatusCode._400, $"The operation '{op}' is unknown.");

                return FetchResource(Resources.frame, vars);
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

        internal static bool regex(string input, string pattern, out Match m, RegexOptions opt = RegexOptions.IgnoreCase) => (m = Regex.Match(input, pattern, opt)).Success;
    }

    /// <summary>
    /// 
    /// </summary>
    public sealed class ASCOperation
    {
        /// <summary>
        /// 
        /// </summary>
        public Func<HttpListenerRequest, HttpListenerResponse, string[], HTTPResponse> Handler { get; }
        /// <summary>
        /// 
        /// </summary>
        public string[] Keys { get; }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="keys"></param>
        public ASCOperation(Func<HttpListenerRequest, HttpListenerResponse, string[], HTTPResponse> handler, params string[] keys)
        {
            this.Keys = keys ?? new string[0];
            this.Handler = handler ?? ((req, res, k) => new byte[0]);
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
