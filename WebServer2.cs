using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Spark
{
	public class WebServer2
	{
		private readonly HttpListener _listener = new HttpListener();
		private readonly Func<HttpListenerRequest, string> _responderMethod;
		private bool running = false;

		public WebServer2(IReadOnlyCollection<string> prefixes, Func<HttpListenerRequest, string> method)
		{
			if (!HttpListener.IsSupported)
			{
				throw new NotSupportedException("Needs Windows XP SP2, Server 2003 or later.");
			}

			// URI prefixes are required eg: "http://localhost:8080/test/"
			if (prefixes == null || prefixes.Count == 0)
			{
				throw new ArgumentException("URI prefixes are required");
			}

			if (method == null)
			{
				throw new ArgumentException("responder method required");
			}

			foreach (string s in prefixes)
			{
				_listener.Prefixes.Add(s);
			}

			running = true;

			_responderMethod = method;
			_listener.Start();
		}

		public WebServer2(Func<HttpListenerRequest, string> method, params string[] prefixes)
		   : this(prefixes, method)
		{
		}

		public void Run()
		{
			ThreadPool.QueueUserWorkItem(o =>
			{
				Console.WriteLine("Webserver running...");
				try
				{
					while (_listener.IsListening && running)
					{
						ThreadPool.QueueUserWorkItem(c =>
						{
							HttpListenerContext ctx = c as HttpListenerContext;
							try
							{
								if (ctx == null)
								{
									return;
								}

								string rstr = _responderMethod(ctx.Request);
								byte[] buf = Encoding.UTF8.GetBytes(rstr);
								ctx.Response.ContentLength64 = buf.Length;
								ctx.Response.OutputStream.Write(buf, 0, buf.Length);
							}
							catch
							{
								// ignored
							}
							finally
							{
								// always close the stream
								if (ctx != null)
								{
									ctx.Response.OutputStream.Close();
								}
							}
						}, _listener.GetContext());
					}
				}
				catch (Exception)
				{
					// ignored
				}
			});
		}

		public void Stop()
		{
			running = false;

			Task.Run(() =>
			{
				Task.Delay(1000);
				_listener.Stop();
				_listener.Close();
			});
		}
	}
}
