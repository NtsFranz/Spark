using IgniteBot2.Properties;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;

namespace IgniteBot2
{
	/// <summary>
	/// 🔑
	/// </summary>
	public class OAuth
	{
		const string REDIRECT_URI = "http://localhost:6722/oauth_login";

		static HTTPServer httpServer;

		static HttpClient client = new HttpClient();

		public static string oauthToken = "";


		public static void OAuthLogin(bool force = false)
		{
			string token = Settings.Default.discordOAuthRefreshToken;
			if (string.IsNullOrEmpty(token) || force)
			{
				Process.Start(new ProcessStartInfo
				{
					FileName = SecretKeys.OAuthURL,
					UseShellExecute = true
				});

				//create server with auto assigned port
				httpServer = new HTTPServer("localhost", 6722);
				httpServer.Start();
			}
			else
			{
				OAuthLoginRefresh(token);
			}
		}

		public static async void OAuthLoginRefresh(string refresh_token)
		{
			Dictionary<string, string> postDataDict = new Dictionary<string, string>()
			{
				{ "client_id", SecretKeys.CLIENT_ID },
				{ "client_secret", SecretKeys.CLIENT_SECRET },
				{ "grant_type", "refresh_token" },
				{ "refresh_token", refresh_token },
				{ "redirect_uri", REDIRECT_URI },
				{ "scope", "identify" }
			};

			HttpResponseMessage response = await client.PostAsync("https://discord.com/api/v6/oauth2/token", new FormUrlEncodedContent(postDataDict));

			if (!response.IsSuccessStatusCode)
			{
				Process.Start(new ProcessStartInfo
				{
					FileName = SecretKeys.OAuthURL,
					UseShellExecute = true
				});

				//create server with auto assigned port
				httpServer = new HTTPServer("localhost", 6722);
				httpServer.Start();
			}
			else
			{
				string responseString = await response.Content.ReadAsStringAsync();
				Dictionary<string, string> data = JsonConvert.DeserializeObject<Dictionary<string, string>>(responseString);
				ProcessResponse(data);
			}

		}

		public static async void OAuthLoginResponse(string code)
		{

			Dictionary<string, string> postDataDict = new Dictionary<string, string>()
			{
				{ "client_id", SecretKeys.CLIENT_ID },
				{ "client_secret", SecretKeys.CLIENT_SECRET },
				{ "grant_type", "authorization_code" },
				{ "code", code },
				{ "redirect_uri", REDIRECT_URI },
				{ "scope", "identify" }
			};

			HttpResponseMessage response = await client.PostAsync("https://discord.com/api/v6/oauth2/token", new FormUrlEncodedContent(postDataDict));
			string responseString = await response.Content.ReadAsStringAsync();
			Dictionary<string, string> data = JsonConvert.DeserializeObject<Dictionary<string, string>>(responseString);
			ProcessResponse(data);
		}

		public static void ProcessResponse(Dictionary<string, string> response)
		{

			Settings.Default.discordOAuthRefreshToken = response["refresh_token"];
			Settings.Default.Save();
			oauthToken = response["access_token"];
			GetDiscordUsername();
		}

		public static async void GetDiscordUsername()
		{
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Program.oauthToken);
			HttpResponseMessage response = await client.GetAsync("https://discord.com/api/v6/users/@me");

			string responseString = await response.Content.ReadAsStringAsync();

			Dictionary<string, string> data = JsonConvert.DeserializeObject<Dictionary<string, string>>(responseString);
			if (data.ContainsKey("username"))
			{
				Program.discordUserData = data;
				Program.discordUsername = data["username"];
			}
			else
			{
				Console.WriteLine("Not Authorized");
			}
		}
	}
}
