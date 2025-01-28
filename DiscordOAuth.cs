using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web;
using Spark.Properties;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Spark
{
    /// <summary>
    /// 🔑
    /// </summary>
    public class DiscordOAuth
    {
        /// <summary>
        /// Called when the program gets authenticated with Discord
        /// </summary>
        public static Action Authenticated;

        private const string REDIRECT_URI = "http://localhost:6722/oauth_login";

        private static WebServer2 webServer;

        static readonly HttpClient client = new HttpClient();

        public static string oauthToken = "";

        public static Dictionary<string, string> discordUserData;
        public static string DiscordUsername => discordUserData?["username"];
        public static string DiscordUserID => discordUserData?["id"];
        public static string DiscordPFPURL => $"https://cdn.discordapp.com/avatars/{discordUserData["id"]}/{discordUserData["avatar"]}";

        public static string igniteUploadKey = string.Empty;
        public static string firebaseCred;

        public static List<AccessCodeKey> availableAccessCodes = new List<AccessCodeKey>();
        private static readonly AccessCodeKey personalAccessCode = new AccessCodeKey
        {
            username = "Personal",
            series_name = "personal"
        };

        public class AccessCodeKey
        {
            public string series_name;
            public string username;
        }

        public static AccessCodeKey AccessCode
        {
            get => accessCode;
            set
            {
                if (value.series_name != accessCode.series_name)
                {
                    SparkSettings.instance.accessCode = SecretKeys.Hash(value.series_name);
                    SparkSettings.instance.Save();
                    accessCode = value;
                    AccessCodeChanged?.Invoke(value);
                }
                accessCode = value;
            }
        }

        public static Action<AccessCodeKey> AccessCodeChanged;
        private static AccessCodeKey accessCode = personalAccessCode;
        public static bool Personal => AccessCode.series_name == "personal" || AccessCode == null;

        public static bool IsLoggedIn { get => oauthToken != string.Empty; }

        public static int GetAccessCodeIndexByHash(string hash)
        {
            for (int i = 0; i < availableAccessCodes.Count; i++)
            {
                if (SecretKeys.Hash(availableAccessCodes[i].series_name) == hash)
                {
                    return i;
                }
            }
            return 0;
        }

        public static void SetAccessCodeByUsername(string username)
        {
            foreach (AccessCodeKey key in availableAccessCodes)
            {
                if (key.username == username)
                {
                    AccessCode = key;
                    return;
                }
            }
            Logger.LogRow(Logger.LogType.Error, "Chosen access code is not available. " + username);
        }

        public static void SetAccessCodeByHash(string hash)
        {
            foreach (AccessCodeKey key in availableAccessCodes)
            {
                if (SecretKeys.Hash(key.series_name) == hash)
                {
                    AccessCode = key;
                    return;
                }
            }
            RevertToPersonal();
            Logger.LogRow(Logger.LogType.Error, "Chosen access code is not available. " + hash);
        }

        public static void OAuthLogin(bool force = false)
        {
            try
            {
                string token = SparkSettings.instance.discordOAuthRefreshToken;
                if (string.IsNullOrEmpty(token) || force)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = SecretKeys.OAuthURL,
                        UseShellExecute = true
                    });

                    webServer = new WebServer2(OAuthResponse, "http://localhost:6722/");
                    webServer.Run();
                }
                else
                {
                    OAuthLoginRefresh(token);
                }
            }
            catch (Exception ex)
            {
                Logger.LogRow(Logger.LogType.Error, $"Error starting OAuth login process: {ex.Message}");
                // Handle the error appropriately
            }
        }

        internal static void Unlink()
        {
            oauthToken = string.Empty;
            discordUserData = null;
            availableAccessCodes.Clear();
            SparkSettings.instance.discordOAuthRefreshToken = string.Empty;
            SparkSettings.instance.Save();
        }

        private static string OAuthResponse(HttpListenerRequest request)
        {
            NameValueCollection queryStrings = HttpUtility.ParseQueryString(request.Url.Query);
            if (queryStrings["code"] != null)
            {
                OAuthLoginResponse(queryStrings["code"]);
                return "<html><head></head><body onload=\"javascript: close(); \" style=\"background-color: #333;\"><div style=\"margin: 8em auto;width: max-content;font-family: arial, sans-serif;color: #ddd;\">Login successful. You can close this window.</div></body></html>";
            }
            else
            {
                return "<html><body onload=\"javascript: close(); \">There was an error. Close this window and try again.</body></html>";
            }
        }

        public static async void OAuthLoginRefresh(string refresh_token)
        {
            Dictionary<string, string> postDataDict = new Dictionary<string, string>
            {
                { "client_id", SecretKeys.CLIENT_ID },
                { "client_secret", SecretKeys.CLIENT_SECRET },
                { "grant_type", "refresh_token" },
                { "refresh_token", refresh_token },
                { "redirect_uri", REDIRECT_URI },
                { "scope", "identify" }
            };

            try
            {
                HttpResponseMessage response = await client.PostAsync("https://discord.com/api/v6/oauth2/token", new FormUrlEncodedContent(postDataDict));

                if (!response.IsSuccessStatusCode)
                {
                    RevertToPersonal();
                    Logger.LogRow(Logger.LogType.Error, $"OAuth token refresh failed. Status code: {response.StatusCode}");
                }
                else
                {
                    if (webServer == null)
                    {
                        webServer = new WebServer2(OAuthResponse, "http://localhost:6722/");
                        webServer.Run();
                    }

                    string responseString = await response.Content.ReadAsStringAsync();
                    Dictionary<string, string> data = JsonConvert.DeserializeObject<Dictionary<string, string>>(responseString);
                    ProcessResponse(data);
                }
            }
            catch (HttpRequestException ex)
            {
                RevertToPersonal();
                Logger.LogRow(Logger.LogType.Error, $"Error during OAuth token refresh: {ex.Message}");
                new MessageBox(Resources.cant_connect_to_internet_for_discord, Resources.Error).Show();
            }
            catch (Exception ex)
            {
                Logger.LogRow(Logger.LogType.Error, $"Unexpected error during OAuth token refresh: {ex.Message}");
                // Handle the error appropriately
            }
        }

        public static async void OAuthLoginResponse(string code)
        {
            Dictionary<string, string> postDataDict = new Dictionary<string, string>
            {
                { "client_id", SecretKeys.CLIENT_ID },
                { "client_secret", SecretKeys.CLIENT_SECRET },
                { "grant_type", "authorization_code" },
                { "code", code },
                { "redirect_uri", REDIRECT_URI },
                { "scope", "identify" }
            };

            try
            {
                HttpResponseMessage response = await client.PostAsync("https://discord.com/api/v6/oauth2/token", new FormUrlEncodedContent(postDataDict));
                string responseString = await response.Content.ReadAsStringAsync();
                Dictionary<string, string> data = JsonConvert.DeserializeObject<Dictionary<string, string>>(responseString);
                ProcessResponse(data);
            }
            catch (HttpRequestException ex)
            {
                Logger.LogRow(Logger.LogType.Error, $"Error during OAuth login response: {ex.Message}");
                // Handle the error appropriately
            }
            catch (Exception ex)
            {
                Logger.LogRow(Logger.LogType.Error, $"Unexpected error during OAuth login response: {ex.Message}");
                // Handle the error appropriately
            }
        }

        public static async void ProcessResponse(Dictionary<string, string> response)
        {
            if (response == null)
            {
                Logger.LogRow(Logger.LogType.Error, "Received null response from OAuth server.");
                RevertToPersonal();
                return;
            }

            if (response.ContainsKey("error"))
            {
                RevertToPersonal();
                SparkSettings.instance.discordOAuthRefreshToken = string.Empty;
                oauthToken = string.Empty;
                return;
            }

            SparkSettings.instance.discordOAuthRefreshToken = response["refresh_token"];
            SparkSettings.instance.Save();
            oauthToken = response["access_token"];

            webServer?.Stop();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", oauthToken);
            FetchUserDetails();
        }

        private static async void FetchUserDetails()
        {
            try
            {
                HttpResponseMessage userResponse = await client.GetAsync("https://discord.com/api/v6/users/@me");
                string responseString = await userResponse.Content.ReadAsStringAsync();
                Dictionary<string, string> data = JsonConvert.DeserializeObject<Dictionary<string, string>>(responseString);

                if (data == null || !data.ContainsKey("username"))
                {
                    Logger.LogRow(Logger.LogType.Error, "Invalid user data received from OAuth server.");
                    return;
                }

                discordUserData = data;
                FetchAccessCodes();
            }
            catch (Exception ex)
            {
                Logger.LogRow(Logger.LogType.Error, $"Error fetching user details: {ex.Message}");
                // Handle the error appropriately
            }
        }

        private static async void FetchAccessCodes()
        {
            try
            {
                string url = $"{Program.APIURL}/auth/token/{oauthToken}?u={SparkSettings.instance.client_name}&v={Program.AppVersionString()}";
                string accessCodesResponseString = await FetchUtils.GetRequestAsync(url, null);

                Dictionary<string, JToken> accessCodesResponseData = JsonConvert.DeserializeObject<Dictionary<string, JToken>>(accessCodesResponseString);
                if (accessCodesResponseData == null)
                {
                    Logger.LogRow(Logger.LogType.Error, "Ignite login response data is null");
                    return;
                }

                List<AccessCodeKey> newAccessCodes = accessCodesResponseData["keys"].ToObject<List<AccessCodeKey>>();
                igniteUploadKey = accessCodesResponseData["write"]?.ToObject<string>() ?? string.Empty;
                firebaseCred = accessCodesResponseData["firebase_cred"]?.ToObject<string>() ?? string.Empty;

                if (newAccessCodes == null)
                {
                    Logger.LogRow(Logger.LogType.Error, "Ignite login response doesn't contain valid data.");
                    return;
                }

                newAccessCodes.Insert(0, personalAccessCode);
                availableAccessCodes = newAccessCodes;
                SetAccessCodeByHash(SparkSettings.instance.accessCode);

                FetchUtils.client.DefaultRequestHeaders.Remove("x-api-key");
                FetchUtils.client.DefaultRequestHeaders.Add("x-api-key", igniteUploadKey);

                Authenticated?.Invoke();
            }
            catch (Exception ex)
            {
                Logger.LogRow(Logger.LogType.Error, $"Error connecting to login server. {ex}");
                // Handle the error appropriately
            }
        }

        public static void RevertToPersonal()
        {
            AccessCode = personalAccessCode;
        }
    }
}
