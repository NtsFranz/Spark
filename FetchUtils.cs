using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Spark
{
	public static class FetchUtils
	{
		/// <summary>
		/// Client used for one-off requests
		/// </summary>
		public static readonly HttpClient client = new HttpClient();

		/// <summary>
		/// Generic method for getting data from a web url
		/// </summary>
		/// <param name="headers">Key-value pairs for headers. Leave null if none.</param>
		public static void GetRequestCallback(string uri, Dictionary<string, string> headers, Action<string> callback)
		{
			Task.Run(async () =>
			{
				string resp = await GetRequestAsync(uri, headers);
				callback(resp);
			});
		}

		/// <summary>
		/// Generic method for getting data from a web url
		/// </summary>
		/// <param name="uri">The URL to GET</param>
		/// <param name="headers">Key-value pairs for headers. Leave null if none.</param>
		public static async Task<string> GetRequestAsync(string uri, Dictionary<string, string> headers = null)
		{
			try
			{
				using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);

				if (headers != null)
				{
					foreach ((string key, string value) in headers)
					{
						request.Headers.Add(key, value);
					}
				}

				HttpResponseMessage response = await client.SendAsync(request);

				response.EnsureSuccessStatusCode();

				return await response.Content.ReadAsStringAsync();
			}
			catch (Exception)
			{
				return string.Empty;
			}
		}
		
		/// <summary>
		/// Generic method for getting data from a web url. Returns a Stream
		/// </summary>
		/// <param name="uri">The URL to GET</param>
		/// <param name="headers">Key-value pairs for headers. Leave null if none.</param>
		public static async Task<Stream> GetRequestAsyncStream(string uri, Dictionary<string, string> headers)
		{
			try
			{
				HttpWebRequest request = (HttpWebRequest) WebRequest.Create(uri);
				if (headers != null)
				{
					foreach ((string key, string value) in headers)
					{
						request.Headers[key] = value;
					}
				}

				request.UserAgent = $"Spark/{Program.AppVersionString()}";
				using WebResponse response = await request.GetResponseAsync();
				return response.GetResponseStream();
			}
			catch (Exception e)
			{
				Console.WriteLine($"Can't get data\n{e}");
			}
			
			return null;
		}

		/// <summary>
		/// Generic method for posting data to a web url
		/// </summary>
		/// <param name="headers">Key-value pairs for headers. Leave null if none.</param>
		public static void PostRequestCallback(string uri, Dictionary<string, string> headers, string body, Action<string> callback)
		{
			Task.Run(async () =>
			{
				string resp = await PostRequestAsync(uri, headers, body);
				callback?.Invoke(resp);
			});
		}

		/// <summary>
		/// Generic method for posting data to a web url
		/// </summary>
		/// <param name="headers">Key-value pairs for headers. Leave null if none.</param>
		public static async Task<string> PostRequestAsync(string uri, Dictionary<string, string> headers, string body, bool readResponse = true)
		{
			try
			{
				if (headers != null)
				{
					foreach (KeyValuePair<string, string> header in headers)
					{
						client.DefaultRequestHeaders.Remove(header.Key);
						client.DefaultRequestHeaders.Add(header.Key, header.Value);
					}
				}
				StringContent content = new StringContent(body, Encoding.UTF8, "application/json");
				HttpResponseMessage response = await client.PostAsync(uri, content);
				if (readResponse)
				{
					return await response.Content.ReadAsStringAsync();
				}

				return string.Empty;
			}
			catch (Exception e)
			{
				Console.WriteLine($"Can't get data\n{e}");
				return string.Empty;
			}
		}
	}
}