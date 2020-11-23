// Modified from: https://gist.github.com/aksakalli/9191056

using System;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Threading;

namespace IgniteBot2
{
	/// <summary>
	/// Http server for local use, similar to how the EchoVR API works.
	/// </summary>
	class HTTPServer
	{
		private Thread thread;
		private volatile bool threadActive;

		private HttpListener listener;
		private readonly string ip;
		private readonly int port;

		public HTTPServer(string ip, int port)
		{
			this.ip = ip;
			this.port = port;
		}

		public void Start()
		{
			if (thread != null) throw new Exception("WebServer already active. (Call stop first)");
			thread = new Thread(Listen);
			thread.Start();
		}

		public void Stop()
		{
			// stop thread and listener
			threadActive = false;
			try
			{
				if (listener != null && listener.IsListening) listener.Stop();
			}
			catch
			{

			}

			// wait for thread to finish
			if (thread != null)
			{
				thread.Join();
				thread = null;
			}

			// finish closing listener
			if (listener != null)
			{
				listener.Close();
				listener = null;
			}
		}

		private void Listen()
		{
			threadActive = true;

			// start listener
			try
			{
				listener = new HttpListener();
				listener.Prefixes.Add(string.Format("http://{0}:{1}/", ip, port));
				listener.Start();
			}
			catch (Exception e)
			{
				Console.WriteLine("ERROR: " + e.Message);
				threadActive = false;
				return;
			}

			// wait for requests
			while (threadActive)
			{
				try
				{
					var context = listener.GetContext();
					if (!threadActive) break;
					ProcessContext(context);
				}
				catch (HttpListenerException e)
				{
					if (e.ErrorCode != 995) Console.WriteLine("ERROR: " + e.Message);
					threadActive = false;
				}
				catch (Exception e)
				{
					Console.WriteLine("ERROR: " + e.Message);
					threadActive = false;
				}
			}
		}

		private void ProcessContext(HttpListenerContext context)
		{
			// this is an oauth request
			if (context.Request.Url.AbsolutePath == "/oauth_login")
			{
				using (MemoryStream memStream = new MemoryStream())
				{
					StreamWriter sw = new StreamWriter(memStream);
					sw.WriteLine("<body onload=\"javascript: close(); \"></body>");
					context.Response.ContentLength64 = sw.BaseStream.Length;
					memStream.Flush();
					memStream.CopyTo(context.Response.OutputStream);
					context.Response.OutputStream.Flush();
				}
				context.Response.StatusCode = (int)HttpStatusCode.OK;
				context.Response.OutputStream.Close();
				Stop();

				DiscordOAuth.OAuthLoginResponse(System.Web.HttpUtility.ParseQueryString(context.Request.Url.Query)["code"]);

				return;
			}

			//context.Response.ContentType = "application/json";
			//context.Response.AddHeader("Access-Control-Allow-Origin", "*");

			//string data = "{}";
			//if (Program.lastJSON != null && Program.lastJSON != "")     // TODO add locks to lastJSON
			//{
			//	data = Program.lastJSON;
			//}

			//byte[] buffer = System.Text.Encoding.ASCII.GetBytes(data);
			//context.Response.ContentLength64 = buffer.Length;
			//Stream output = context.Response.OutputStream;
			//output.Write(buffer, 0, buffer.Length);
			//output.Close();
		}
	}
}