using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Threading;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System;

using Newtonsoft.Json;

using static ASC.Server.Program;

namespace ASC.Server
{
    /// <summary>
    /// Represents a specialized HTTP request handler delegate method
    /// </summary>
    /// <param name="request">HTTP request data</param>
    /// <param name="response">HTTP response data</param>
    /// <param name="geoip">GeoIP task</param>
    public delegate HTTPResponse HTTPRequestHandler(HttpListenerRequest request, HttpListenerResponse response, Task<GeoIPResult> geoip);

    /// <summary>
    /// Represents an HTTP server
    /// </summary>
    public sealed class HTTPServer
        : IDisposable
    {
        private readonly HTTPRequestHandler _rfunc;
        private readonly HttpListener _listener = new HttpListener();


        /// <summary>
        /// Creates a new instance
        /// </summary>
        /// <param name="prefixes">HTTP(S) listening prefixes</param>
        /// <param name="func">Listening handling function</param>
        public HTTPServer(string[] prefixes, HTTPRequestHandler func)
        {
            if (!HttpListener.IsSupported)
            {
                "Needs Windows XP SP2, Server 2003 or later.".Err();

                throw new NotSupportedException();
            }

            if ((prefixes?.Length ?? 0) == 0)
                throw new ArgumentException("prefixes");

            foreach (string s in prefixes)
            {
                _listener.Prefixes.Add(s);

                $"Listening on '{s}' ...".Msg();
            }

            _rfunc = func ?? throw new ArgumentException(nameof(func));
            _listener.Start();
        }

        /// <summary>
        /// Creates a new instance
        /// </summary>
        /// <param name="method">Listening handling function</param>
        /// <param name="prefixes">HTTP(S) listening prefixes</param>
        public HTTPServer(HTTPRequestHandler method, params string[] prefixes)
            : this(prefixes, method)
        {
        }

        /// <summary>
        /// Disposes the current HTTP server and releases all underlying resources
        /// </summary>
        public void Dispose() => Stop();

        /// <summary>
        /// Stopps the current HTTP server
        /// </summary>
        public void Stop()
        {
            try
            {
                if (_listener?.IsListening ?? false)
                    _listener?.Stop();

                _listener?.Close();
            }
            finally
            {
                acceptconnections = false;
            }
        }

        /// <summary>
        /// Starts the current HTTP server
        /// </summary>
        public void Start() => ThreadPool.QueueUserWorkItem(delegate
        {
            "Webserver running...".Ok();

            try
            {
                Task.Factory.StartNew(async () =>
                {
                    while (_listener.IsListening)
                        await Listen(_listener);
                }, TaskCreationOptions.LongRunning);
            }
            catch (Exception ex)
            {
                (!(ex is ForcedShutdown) ? ex : throw ex).Err();
            }
        });

        internal async Task Listen(HttpListener listener)
        {
            HttpListenerContext ctx = await listener.GetContextAsync();

            try
            {
                $"'{ctx.Request.RemoteEndPoint}' requests '{ctx.Request.LocalEndPoint}{ctx.Request.RawUrl}' ...".Conn();

                Task<GeoIPResult> geoip = Task<GeoIPResult>.Run(() => GetGeoIPResult(ctx.Request.RemoteEndPoint));
                HTTPResponse resp = _rfunc(ctx.Request, ctx.Response, geoip);

                ctx.Response.ContentEncoding = HTTPResponse.Codepage;
                ctx.Response.ContentLength64 = resp.Length;
                ctx.Response.OutputStream.Write(resp.Bytes ?? new byte[0], 0, resp.Length);

                $"Response sent to '{ctx.Request.RemoteEndPoint}' with the status code '{ctx.Response?.StatusCode ?? 500} - {ctx.Response?.StatusDescription ?? "Internal error"}'".Msg();

                TaskKiller(geoip);
            }
            catch (Exception ex)
            {
                if (ex is ForcedShutdown)
                {
                    Stop();

                    throw;
                }
                else
                    ex.Err();
            }
            finally
            {
                ctx.Response.OutputStream.Close();
            }
        }

        internal static GeoIPResult GetGeoIPResult(IPEndPoint endpoint)
        {
            string ip = endpoint.Address.ToString();
            bool isloopback = false;
            string res;

            try
            {
                var host = Dns.GetHostEntry(endpoint.Address);

                isloopback = host.AddressList.Any(_ => _ == IPAddress.IPv6Loopback || _ == IPAddress.Loopback) ||
                             HostNames.Any(_ => _.Equals(ip, StringComparison.InvariantCultureIgnoreCase));
            }
            catch
            {
            }

            if (regex(ip, @"^\[(?<ip>[\:0-9a-f]+)\](\:[0-9]+)?$", out Match m)) // strip '[' and ']'
                ip = m.Groups[nameof(ip)].ToString();

            ip = isloopback ? "" : ip.ToUpper();

            using (WebClient wc = new WebClient())
                try
                {
                    res = wc.DownloadString($"https://www.geoip-db.com/jsonp/{ip}");
                }
                catch
                {
                    return null;
                }

            $"Location of '{endpoint}' resloved to '{res.Replace("\r", "").Replace("\n", "")}'.".Conn();

            if (regex(res, @"^callback\s*\((?<content>.+)\)", out m))
                res = m.Groups["content"].ToString();

            return JsonConvert.DeserializeObject<GeoIPResult>(res);
        }

        // a task will be launched to wait for the termination of an existing async task .... now that's meta.
        internal static void TaskKiller(Task<GeoIPResult> task) => Task.Factory.StartNew(() =>
        {
            using (task)
                if (!(task.IsCompleted || task.IsFaulted || task.IsCanceled))
                {
                    $"Waiting for the termination of task 0x{task.Id:x8}...".Msg();

                    task.Wait();

                    $"Task 0x{task.Id:x8} terminated.".Msg();
                }
        });
    }

    /// <summary>
    /// Represents a simple http-response
    /// </summary>
    public class HTTPResponse
    {
        public static Encoding Codepage { get; } = Encoding.UTF8; // Unicode or UTF8 or GetEncoding(1252) ?

        /// <summary>
        /// The response bytes
        /// </summary>
        public byte[] Bytes { private set; get; }
        /// <summary>
        /// Returns the response's length (in bytes)
        /// </summary>
        public int Length => Bytes?.Length ?? 0;
        /// <summary>
        /// Returns the response's textual representation
        /// </summary>
        public string Text => Codepage.GetString(Bytes);

        /// <summary>
        /// Converts the given byte array to an HTTP-response
        /// </summary>
        /// <param name="bytes">Byte array</param>
        public static implicit operator HTTPResponse(byte[] bytes) => new HTTPResponse { Bytes = bytes };
        /// <summary>
        /// Converts the given UTF-16 string to an UTF-8 encoded HTTP-response
        /// </summary>
        /// <param name="text">UTF-16 strings</param>
        public static implicit operator HTTPResponse(string text) => Codepage.GetBytes(text);
    }
}
