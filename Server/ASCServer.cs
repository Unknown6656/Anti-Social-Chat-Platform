using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Reflection;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System;

using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace ASC.Server
{
    public sealed class ASCServer
        : IDisposable
    {
        internal static Dictionary<string, Dictionary<string, string>> LanguagePacks { get; }
        internal static Dictionary<StatusCode, string> StatusCodes { get; } = new Dictionary<StatusCode, string>
        {
            [StatusCode._200] = "OK",
            [StatusCode._403] = "Forbidden",
            [StatusCode._404] = "Not found",
            [StatusCode._420] = "420/Weed",
            [StatusCode._500] = "Server Error",
        };
        internal Func<int, bool> SSL { get; }
        internal string ServerString { get; }
        internal HTTPServer Server { get; }


        static ASCServer() => LanguagePacks = (from f in Directory.GetFiles($@"{Directory.GetCurrentDirectory()}\Languages", "*.json", SearchOption.AllDirectories)
                                               let nfo = new FileInfo(f)
                                               let code = nfo.Name.Replace(nfo.Extension, "")
                                               select (code, BuildLanguageDictionary(f))).ToDictionary(_ => _.Item1, _ => _.Item2);

        public ASCServer(int port)
        {
            foreach (KeyValuePair<string, Dictionary<string, string>> dic in LanguagePacks)
                $"Language dictionary '{dic.Value["lang_name"]} ({dic.Key})' loaded.".Ok();

            ServerString = $"ASC Server/{Assembly.GetExecutingAssembly().GetName().Version}";

            SSL = p => p == port + 1;

            Server = new HTTPServer(SendResponse, $"http://*:{port}/", $"https://*:{port + 1}/");
            Server.Start();
        }

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

        public string FetchResource(string obj, Dictionary<string, object> variables, params object[] args)
        {
            const string pat_dic = @"\§(?<key>\w+)(\:(?<format>[^§]+))?\§";
            const string pat_par = @"\§([0-9]+)\:([^§]+)§";
            const string pat_cnt = @"\§([0-9]+)";
            List<object> values = new List<object>();
            int rcount = 0;
            Match m;

            obj = obj.Replace("{", "{{");
            obj = obj.Replace("}", "}}");

            rcount += Regex.Matches(obj, pat_par).Count;
            rcount += Regex.Matches(obj, pat_cnt).Count;

            obj = Regex.Replace(obj, pat_par, "{$1:$2}");
            obj = Regex.Replace(obj, pat_cnt, "{$1}");

            while ((m = Regex.Match(obj, pat_dic)).Success)
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

        public HTTPResponse SendResponse(HttpListenerRequest request, HttpListenerResponse response)
        {
            Dictionary<string, object> vars = new Dictionary<string, object>();
            string url = request.RawUrl;
            DateTime now = DateTime.Now;
            string lang = "en";
            int port = request.LocalEndPoint.Port;

            response.Headers[HttpResponseHeader.Server] = ServerString;

            if (Regex.IsMatch(url, @"[\\\/]?favicon\.ico$", RegexOptions.IgnoreCase))
            {
                SetStatusCode(response, StatusCode._200);

                response.ContentType = "image/x-icon";

                using (MemoryStream ms = new MemoryStream())
                {
                    Properties.Resources.favicon.Save(ms);

                    return ms.ToArray();
                }
            }
            else
            {
                SetStatusCode(response, StatusCode._200);

                if (request.QueryString.AllKeys.Contains("lang"))
                {
                    lang = request.QueryString["lang"].ToLower();

                    if (!LanguagePacks.ContainsKey(lang))
                        lang = "en";
                }

                foreach (KeyValuePair<string, string> kvp in LanguagePacks[lang])
                    vars[kvp.Key] = kvp.Value;

                vars["time"] = now;
                vars["port"] = port;
                vars["ssl"] = SSL(port);
                vars["script"] = "// script";
                vars["style"] = FetchResource(Properties.Resources.style, vars);

                return FetchResource(Properties.Resources.frame, vars);
            }
        }

        internal void SetStatusCode(HttpListenerResponse resp, StatusCode code)
        {
            resp.StatusDescription = StatusCodes[code];
            resp.StatusCode = (int)code;
        }

        public void Dispose() => Server.Dispose();
    }

    internal enum StatusCode
    {
        _200 = 200,
        _403 = 403,
        _404 = 404,
        _420 = 420,
        _500 = 500,
    }
}
