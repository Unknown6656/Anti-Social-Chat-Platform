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

namespace ASC.Server
{
    /// <summary>
    /// Represents an HTTP server
    /// </summary>
    public sealed class HTTPServer
        : IDisposable
    {
        private readonly Func<HttpListenerRequest, HttpListenerResponse, HTTPResponse> _rfunc;
        private readonly HttpListener _listener = new HttpListener();


        /// <summary>
        /// Creates a new instance
        /// </summary>
        /// <param name="prefixes">HTTP(S) listening prefixes</param>
        /// <param name="func">Listening handling function</param>
        public HTTPServer(string[] prefixes, Func<HttpListenerRequest, HttpListenerResponse, HTTPResponse> func)
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
        public HTTPServer(Func<HttpListenerRequest, HttpListenerResponse, HTTPResponse> method, params string[] prefixes)
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
            if (_listener?.IsListening ?? false)
                _listener?.Stop();

            _listener?.Close();
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
                ex.Err();
            }
        });

        internal async Task Listen(HttpListener listener)
        {
            HttpListenerContext ctx = await listener.GetContextAsync();

            try
            {
                $"'{ctx.Request.RemoteEndPoint}' requests '{ctx.Request.LocalEndPoint}{ctx.Request.RawUrl}' ...".Msg();

                HTTPResponse resp = _rfunc(ctx.Request, ctx.Response);

                ctx.Response.ContentLength64 = resp.Length;
                ctx.Response.OutputStream.Write(resp.Bytes ?? new byte[0], 0, resp.Length);

                $"Response sent to '{ctx.Request.RemoteEndPoint}' with the status code '{ctx.Response?.StatusCode ?? 500} - {ctx.Response?.StatusDescription ?? "Internal error"}'".Msg();
            }
            catch (Exception ex)
            {
                ex.Err();
            }
            finally
            {
                ctx.Response.OutputStream.Close();
            }
        }
    }

    /// <summary>
    /// Represents a simple http-response
    /// </summary>
    public class HTTPResponse
    {
        /// <summary>
        /// The response bytes
        /// </summary>
        public byte[] Bytes { private set; get; }
        /// <summary>
        /// Returns the response's length (in bytes)
        /// </summary>
        public int Length => Bytes?.Length ?? 0;

        /// <summary>
        /// Converts the given byte array to an HTTP-response
        /// </summary>
        /// <param name="bytes">Byte array</param>
        public static implicit operator HTTPResponse(byte[] bytes) => new HTTPResponse { Bytes = bytes };
        /// <summary>
        /// Converts the given UTF-16 string to an UTF-8 encoded HTTP-response
        /// </summary>
        /// <param name="text">UTF-16 strings</param>
        public static implicit operator HTTPResponse(string text) => Encoding.UTF8.GetBytes(text);
    }
}
