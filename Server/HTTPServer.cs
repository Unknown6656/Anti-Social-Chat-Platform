using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Threading;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System;

namespace ASC.Server
{
    public sealed class HTTPServer
        : IDisposable
    {
        private readonly Func<HttpListenerRequest, HttpListenerResponse, HTTPResponse> _rfunc;
        private readonly HttpListener _listener = new HttpListener();


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

        public HTTPServer(Func<HttpListenerRequest, HttpListenerResponse, HTTPResponse> method, params string[] prefixes)
            : this(prefixes, method)
        {
        }

        public void Dispose() => Stop();

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

                ctx.Response.ContentLength64 = resp.Bytes.Length;
                ctx.Response.OutputStream.Write(resp.Bytes, 0, resp.Bytes.Length);

                $"Response sent to '{ctx.Request.RemoteEndPoint}'".Msg();
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

        public void Stop()
        {
            if (_listener?.IsListening ?? false)
                _listener?.Stop();

            _listener?.Close();
        }
    }

    public class HTTPResponse
    {
        public byte[] Bytes { private set; get; }

        public static implicit operator HTTPResponse(byte[] bytes) => new HTTPResponse { Bytes = bytes };
        public static implicit operator HTTPResponse(string text) => Encoding.UTF8.GetBytes(text);
    }
}
