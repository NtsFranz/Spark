using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EchoVRAPI;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;

namespace Spark
{
    public class OverlayServer
    {
        private IHost server;
        private readonly SemaphoreSlim restartLock = new SemaphoreSlim(1, 1);


        public static string StaticOverlayFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IgniteVR", "Spark", "Overlays");

        public OverlayServer()
        {
            // Initial start
            _ = RestartServer();

            DiscordOAuth.AccessCodeChanged += (code) =>
            {
                Console.WriteLine("Access code changed");
                _ = RestartServer();
            };
        }

        private async Task RestartServer()
        {
            // Prevent multiple concurrent restarts
            if (!await restartLock.WaitAsync(0))
            {
                // If we can't get the lock immediately, it means a restart is already in progress.
                // We can ignore this request as the current restart will pick up the latest state eventually,
                // or simply preventing overlap is sufficient.
                return;
            }

            try
            {


                // Fetch data needed for the server
                await OverlaysCustom.FetchOverlayData();

                if (server != null)
                {
                    await server.StopAsync();
                    server.Dispose();
                    server = null;
                }

                // Create and start the server
                server = Host
                    .CreateDefaultBuilder()
                    .ConfigureWebHostDefaults(webBuilder =>
                    {
                        webBuilder.UseKestrel(x => { x.ListenAnyIP(6724); })
                                  .ConfigureLogging((logging) => { /* Configure logging if needed, or keep silent to save perf */ })
                                  .UseStartup<Routes>();
                    })
                    .Build();

                await server.StartAsync();
            }
            catch (Exception e)
            {
                Logger.LogRow(Logger.LogType.Error, $"Error when restarting server: {e}");
            }
            finally
            {

                restartLock.Release();
            }
        }

        public void Stop()
        {
            if (server != null)
            {
                // Fire and forget stop
                _ = server.StopAsync();
            }
        }

        public class Routes
        {
            // Cache the resource names to avoid reflection on every request setup
            private static readonly string[] ResourceNames = Assembly.GetExecutingAssembly().GetManifestResourceNames();

            public void ConfigureServices(IServiceCollection services)
            {
                services.AddCors(o => o.AddPolicy("CorsPolicy", builder =>
                {
                    builder.AllowAnyOrigin()
                           .AllowAnyMethod()
                           .AllowAnyHeader();
                }));
                services.AddRouting();
            }

            public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
            {
                // Ensure directory exists
                if (!Directory.Exists(StaticOverlayFolder))
                {
                    Directory.CreateDirectory(StaticOverlayFolder);
                }

                var provider = new FileExtensionContentTypeProvider
                {
                    Mappings = { [".yaml"] = "application/x-yaml" }
                };

                // Common CORS headers setup for static files
                Action<StaticFileResponseContext> onPrepareResponse = ctx =>
                {
                    ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
                    ctx.Context.Response.Headers.Append("Access-Control-Allow-Headers", "Content-Type, Accept, X-Requested-With");
                };

                // Global CORS policy - applied before static files
                app.UseCors("CorsPolicy");

                // Svelte overlay build output. "build" is what `npm run build` in /Overlay produces;
                // build_arena / build_combat are kept for older packaged layouts.
                string overlayRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Overlay");
                List<string> overlayFolders = new List<string> { StaticOverlayFolder };
                overlayFolders.AddRange(new[] { "build", "build_arena", "build_combat" }
                    .Select(f => Path.Combine(overlayRoot, f))
                    .Where(Directory.Exists));

                if (overlayFolders.Count == 1)
                {
                    Logger.LogRow(Logger.LogType.Error, $"Overlay build not found in {overlayRoot}. Run `npm run build` in the Overlay folder.");
                }

                // Map extension-less URLs onto the prerendered files, accepting both build layouts:
                //   /overlays  or /overlays/  ->  overlays/index.html  or  overlays.html
                app.Use(async (context, next) =>
                {
                    string path = context.Request.Path.Value ?? "/";
                    if (path != "/" && !Path.HasExtension(path))
                    {
                        string trimmed = path.Trim('/');
                        foreach (string folder in overlayFolders)
                        {
                            if (File.Exists(Path.Combine(folder, trimmed, "index.html")))
                            {
                                context.Request.Path = "/" + trimmed + "/index.html";
                                break;
                            }

                            if (File.Exists(Path.Combine(folder, trimmed + ".html")))
                            {
                                context.Request.Path = "/" + trimmed + ".html";
                                break;
                            }
                        }
                    }
                    await next();
                });

                foreach (string folder in overlayFolders)
                {
                    app.UseFileServer(new FileServerOptions
                    {
                        FileProvider = new PhysicalFileProvider(folder),
                        RequestPath = "",
                        EnableDefaultFiles = true,
                        StaticFileOptions =
                        {
                            ServeUnknownFileTypes = true,
                            ContentTypeProvider = provider,
                            OnPrepareResponse = onPrepareResponse
                        }
                    });
                }

                app.UseRouting();

                app.UseEndpoints(endpoints =>
                {
                    SparkAPI.MapRoutes(endpoints);
                    EchoVRAPIPassthrough.MapRoutes(endpoints);

                    // API Endpoints
                    endpoints.MapGet("/spark_info", async context =>
                    {
                        await WriteJsonAsync(context, new Dictionary<string, object>
                        {
                            { "version", Program.AppVersionString() },
                            { "windows_store", Program.IsWindowsStore() },
                            { "ess_version", Program.InstalledSpeakerSystemVersion },
                        });
                    });

                    endpoints.MapGet("/stats", async context =>
                    {
                        await WriteJsonAsync(context, GetStatsResponse());
                    });

                    endpoints.MapGet("/overlay_info", async context =>
                    {
                        try
                        {
                            Dictionary<string, object> response = new Dictionary<string, object>
                            {
                                ["stats"] = GetStatsResponse(),
                                ["visibility"] = new Dictionary<string, object>
                                {
                                    { "minimap", true },
                                    { "main_banner", true },
                                    { "neutral_jousts", true },
                                    { "defensive_jousts", true },
                                    { "event_log", true },
                                    { "playspace", true },
                                    { "player_speed", true },
                                    { "disc_speed", true },
                                },
                                ["caster_prefs"] = SparkSettings.instance.casterPrefs
                            };

                            if (Program.InGame)
                            {
                                response["session"] = Program.lastJSON;
                            }

                            await WriteJsonAsync(context, response);
                        }
                        catch (Exception e)
                        {
                            Logger.LogRow(Logger.LogType.Error, e.ToString());
                            context.Response.StatusCode = 500;
                        }
                    });

                    endpoints.MapGet("/scoreboard", async context =>
                    {
                        await ServeScoreboard(context);
                    });

                    endpoints.MapGet("/disc_positions", async context =>
                    {
                        var positions = await GetDiscPositions();
                        await WriteJsonAsync(context, positions);
                    });

                    endpoints.MapGet("/disc_position_heatmap", async context =>
                    {
                        context.Response.ContentType = "text/html";
                        await context.Response.WriteAsync(ReadResource("disc_position_heatmap.html"));
                    });

                    endpoints.MapGet("/get_player_speed", async context =>
                    {
                        float speed = Program.lastFrame?.GetPlayer(Program.lastFrame.client_name)?.velocity.ToVector3().Length() ?? -1;
                        await WriteJsonAsync(context, new { speed });
                    });

                    endpoints.MapGet("/get_disc_speed", async context =>
                    {
                        float speed = Program.lastFrame?.disc.velocity.ToVector3().Length() ?? -1;
                        await WriteJsonAsync(context, new { speed });
                    });

                    // Speedometer pages
                    MapHtmlReplacement(endpoints, "/speedometer/player", "speedometer.html", "FETCH_URL", "/get_player_speed");
                    MapHtmlReplacement(endpoints, "/speedometer/lone_echo_1", "speedometer.html", "FETCH_URL", "http://127.0.0.1:6723/le1/speed/");
                    MapHtmlReplacement(endpoints, "/speedometer/lone_echo_2", "speedometer.html", "FETCH_URL", "http://127.0.0.1:6723/le2/speed/");
                    MapHtmlReplacement(endpoints, "/speedometer/disc", "speedometer.html", "FETCH_URL", "/get_disc_speed");

                    // Map embedded wwwroot_resources
                    MapEmbeddedResources(endpoints);
                });
            }

            private static async Task WriteJsonAsync(HttpContext context, object data)
            {
                context.Response.ContentType = "application/json";
                context.Response.Headers["Access-Control-Allow-Origin"] = "*";
                await context.Response.WriteAsync(JsonConvert.SerializeObject(data));
            }

            private static void MapHtmlReplacement(Microsoft.AspNetCore.Routing.IEndpointRouteBuilder endpoints, string route, string resourceName, string placeholder, string replacement)
            {
                endpoints.MapGet(route, async context =>
                {
                    string file = ReadResource(resourceName);
                    file = file.Replace(placeholder, replacement);
                    context.Response.ContentType = "text/html";
                    await context.Response.WriteAsync(file);
                });
            }

            private static void MapEmbeddedResources(Microsoft.AspNetCore.Routing.IEndpointRouteBuilder endpoints)
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                string sparkPath = Path.GetDirectoryName(assembly.Location);

                foreach (string str in ResourceNames)
                {
                    string[] pieces = str.Split('.');
                    if (pieces.Length > 2 && pieces?[1] == "wwwroot_resources")
                    {
                        // Logic to map resource name to URL path
                        List<string> folderPieces = pieces.Skip(2).SkipLast(2).Append(string.Join('.', pieces.TakeLast(2))).ToList();
                        
                        if (folderPieces.Count > 1 && folderPieces[^1].Contains("min."))
                        {
                            folderPieces[^2] = string.Join('.', folderPieces.TakeLast(2));
                            folderPieces.RemoveAt(folderPieces.Count - 1);
                        }

                        string url;
                        string fileName = folderPieces[^1];

                        if (fileName == "index.html")
                        {
                            url = "/" + string.Join('/', folderPieces.SkipLast(1));
                        }
                        else if (fileName.EndsWith(".html"))
                        {
                            url = "/" + string.Join('/', folderPieces)[..^5];
                        }
                        else
                        {
                            url = "/" + string.Join('/', folderPieces);
                        }

                        endpoints.MapGet(url, async context =>
                        {
                            string ext = fileName.Split('.').Last();
                            string contentType = ext switch
                            {
                                "js" => "application/javascript",
                                "css" => "text/css",
                                "png" => "image/png",
                                "jpg" => "image/jpeg",
                                "html" => "text/html",
                                _ => "application/octet-stream"
                            };

                            context.Response.Headers.Append("content-type", contentType);
                            // Prefer loading from disk if available (for dev/modding), fall back to resource
                            if (sparkPath != null)
                            {
                                string filePath = Path.Combine(sparkPath, "wwwroot_resources", Path.Combine(folderPieces.ToArray()));
                                if (File.Exists(filePath))
                                {
                                    await context.Response.SendFileAsync(filePath);
                                    return;
                                }
                            }
                            
                            // Fallback to embedded stream
                            using Stream stream = assembly.GetManifestResourceStream(str);
                            if (stream != null)
                            {
                                await stream.CopyToAsync(context.Response.Body);
                            }
                        });
                    }
                }
            }

            private static async Task ServeScoreboard(HttpContext context)
            {
                string file = ReadResource("default_scoreboard.html");
                string[] columns = { "player_name", "points", "assists", "saves", "stuns" };
                var matchStats = GetMatchStats();

                string overlayOrangeTeamName = "ORANGE TEAM";
                string overlayBlueTeamName = "BLUE TEAM";

                switch (SparkSettings.instance.overlaysTeamSource)
                {
                    case 0:
                        overlayOrangeTeamName = SparkSettings.instance.overlaysManualTeamNameOrange;
                        overlayBlueTeamName = SparkSettings.instance.overlaysManualTeamNameBlue;
                        break;
                    case 1:
                        if (Program.CurrentRound != null)
                        {
                            overlayOrangeTeamName = Program.CurrentRound.teams[Team.TeamColor.orange].vrmlTeamName;
                            overlayBlueTeamName = Program.CurrentRound.teams[Team.TeamColor.blue].vrmlTeamName;
                        }
                        break;
                }

                if (string.IsNullOrWhiteSpace(overlayOrangeTeamName)) overlayOrangeTeamName = "ORANGE TEAM";
                if (string.IsNullOrWhiteSpace(overlayBlueTeamName)) overlayBlueTeamName = "BLUE TEAM";

                string[] teamHTMLs = new string[2];
                for (int i = 0; i < 2; i++)
                {
                    StringBuilder html = new StringBuilder();
                    html.Append("<thead>");
                    foreach (string column in columns)
                    {
                        html.Append("<th>");
                        if (column == "player_name")
                            html.Append(i == 0 ? overlayBlueTeamName : overlayOrangeTeamName);
                        else
                            html.Append(column);
                        html.Append("</th>");
                    }
                    html.Append("</thead><tbody>");

                    if (matchStats[i].Count >= 8) Logger.LogRow(Logger.LogType.Error, "8 or more players on a team.");

                    for (int playerIndex = 0; playerIndex < matchStats[i].Count && playerIndex < 8; playerIndex++)
                    {
                        Dictionary<string, object> player = matchStats[i][playerIndex];
                        html.Append("<tr>");
                        foreach (string column in columns)
                        {
                            html.Append("<td>");
                            html.Append(player.ContainsKey(column) ? player[column] : "");
                            html.Append("</td>");
                        }
                        html.Append("</tr>");
                    }
                    html.Append("</tbody>");
                    teamHTMLs[i] = html.ToString();
                }

                file = file.Replace("{{ BLUE_TEAM }}", teamHTMLs[0]);
                file = file.Replace("{{ ORANGE_TEAM }}", teamHTMLs[1]);

                context.Response.ContentType = "text/html";
                await context.Response.WriteAsync(file);
            }

            private static Dictionary<string, object> GetStatsResponse()
            {
                lock (Program.gameStateLock)
                {
                    List<AccumulatedFrame> selectedMatches = GetPreviousRounds();
                    List<List<Dictionary<string, object>>> matchStats = GetMatchStats();

                    string overlayOrangeTeamName = "";
                    string overlayBlueTeamName = "";
                    string overlayOrangeTeamLogo = "";
                    string overlayBlueTeamLogo = "";

                    switch (SparkSettings.instance.overlaysTeamSource)
                    {
                        case 0:
                            overlayOrangeTeamName = SparkSettings.instance.overlaysManualTeamNameOrange;
                            overlayBlueTeamName = SparkSettings.instance.overlaysManualTeamNameBlue;
                            overlayOrangeTeamLogo = SparkSettings.instance.overlaysManualTeamLogoOrange;
                            overlayBlueTeamLogo = SparkSettings.instance.overlaysManualTeamLogoBlue;
                            break;
                        case 1:
                            var orangeTeam = selectedMatches?.LastOrDefault()?.teams[Team.TeamColor.orange];
                            var blueTeam = selectedMatches?.LastOrDefault()?.teams[Team.TeamColor.blue];
                            overlayOrangeTeamName = orangeTeam?.vrmlTeamName ?? "";
                            overlayBlueTeamName = blueTeam?.vrmlTeamName ?? "";
                            overlayOrangeTeamLogo = orangeTeam?.vrmlTeamLogo ?? "";
                            overlayBlueTeamLogo = blueTeam?.vrmlTeamLogo ?? "";
                            break;
                    }

                    return new Dictionary<string, object>
                    {
                        {
                            "teams", new[]
                            {
                                new Dictionary<string, object>
                                {
                                    { "vrml_team_name", selectedMatches?.LastOrDefault()?.teams[Team.TeamColor.blue].vrmlTeamName ?? "" },
                                    { "vrml_team_logo", selectedMatches?.LastOrDefault()?.teams[Team.TeamColor.blue].vrmlTeamLogo ?? "" },
                                    { "team_name", overlayBlueTeamName },
                                    { "team_logo", overlayBlueTeamLogo },
                                    { "players", matchStats?[0] }
                                },
                                new Dictionary<string, object>
                                {
                                    { "vrml_team_name", selectedMatches?.LastOrDefault()?.teams[Team.TeamColor.orange].vrmlTeamName ?? "" },
                                    { "vrml_team_logo", selectedMatches?.LastOrDefault()?.teams[Team.TeamColor.orange].vrmlTeamLogo ?? "" },
                                    { "team_name", overlayOrangeTeamName },
                                    { "team_logo", overlayOrangeTeamLogo },
                                    { "players", matchStats?[1] }
                                }
                            }
                        },
                        {
                            "joust_events", selectedMatches?
                                .SelectMany(m => m.events)
                                .Where(e => e.eventType is EventContainer.EventType.joust_speed or EventContainer.EventType.defensive_joust)
                                .Select(e => e.ToDict())
                        },
                        { "goals", selectedMatches?.SelectMany(m => m.goals).Select(e => e.ToDict()) }
                    };
                }
            }
        }

        public static List<AccumulatedFrame> GetPreviousRounds()
        {
            List<AccumulatedFrame> selectedMatches = new List<AccumulatedFrame>();
            if (Program.CurrentRound == null) return selectedMatches;

            selectedMatches.Add(Program.CurrentRound);
            AccumulatedFrame current = Program.CurrentRound;
            
            // Limit the depth to prevent infinite loops or excessive memory usage
            int depth = 0;
            while (current.lastRound != null && depth < 20)
            {
                selectedMatches.Add(current.lastRound);
                current = current.lastRound;
                depth++;
            }

            selectedMatches.Reverse();
            return selectedMatches;
        }

        public static string ReadResource(string name)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string resourcePath = assembly.GetManifestResourceNames().FirstOrDefault(str => str.EndsWith(name));
            if (resourcePath == null) return "";

            using Stream stream = assembly.GetManifestResourceStream(resourcePath);
            if (stream == null) return "";
            using StreamReader reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        public static List<List<Dictionary<string, object>>> GetMatchStats()
        {
            List<AccumulatedFrame> selectedMatches = GetPreviousRounds();
            
            // Helper to merge players efficiently
            Dictionary<string, MatchPlayer> MergePlayers(Team.TeamColor color)
            {
                var dict = new Dictionary<string, MatchPlayer>();
                var players = selectedMatches.SelectMany(m => m.players.Values).Where(p => p.TeamColor == color);
                
                foreach (var p in players)
                {
                    if (dict.TryGetValue(p.Name, out MatchPlayer existing))
                    {
                        dict[p.Name] += p;
                    }
                    else
                    {
                        dict[p.Name] = new MatchPlayer(p);
                    }
                }
                return dict;
            }

            var bluePlayers = MergePlayers(Team.TeamColor.blue);
            var orangePlayers = MergePlayers(Team.TeamColor.orange);

            return new List<List<Dictionary<string, object>>>
            {
                bluePlayers.Values.Select(p => p.ToDict()).ToList(),
                orangePlayers.Values.Select(p => p.ToDict()).ToList(),
            };
        }

        public static async Task<List<Dictionary<string, float>>> GetDiscPositions()
        {
            if (string.IsNullOrEmpty(SparkSettings.instance.saveFolder) || !Directory.Exists(SparkSettings.instance.saveFolder))
                return new List<Dictionary<string, float>>();

            // Optimize: use EnumerateFiles and don't load everything into memory
            DirectoryInfo directory = new DirectoryInfo(SparkSettings.instance.saveFolder);
            
            // Get round times to match against
            var rounds = GetPreviousRounds();
            if (rounds.Count == 0) return new List<Dictionary<string, float>>();
            
            DateTime minTime = rounds.Select(m => m.matchTime).Min().ToUniversalTime().AddSeconds(-10);

            // Find relevant files - prefer butter, fallback to echoreplay
            // Check only recent files to save time
            var filesToCheck = directory.EnumerateFiles("rec_*.butter", SearchOption.TopDirectoryOnly)
                                        .OrderByDescending(f => f.LastWriteTime)
                                        .Take(10) // Check last 10 files max
                                        .ToList();

            if (!filesToCheck.Any())
            {
                filesToCheck = directory.EnumerateFiles("rec_*.echoreplay", SearchOption.TopDirectoryOnly)
                                        .OrderByDescending(f => f.LastWriteTime)
                                        .Take(10)
                                        .ToList();
            }

            var selectedFiles = new List<FileInfo>();
            foreach (var f in filesToCheck)
            {
                if (f.Name.Length < 23) continue; // "rec_yyyy-MM-dd_HH-mm-ss" length check
                
                if (DateTime.TryParseExact(f.Name.Substring(4, 19), "yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTime time))
                {
                    if (time.ToUniversalTime() > minTime)
                    {
                        selectedFiles.Add(f);
                    }
                }
            }

            if (selectedFiles.Count == 0) return new List<Dictionary<string, float>>();

            List<Dictionary<string, float>> positions = new List<Dictionary<string, float>>(1000);
            
            foreach (FileInfo file in selectedFiles)
            {
                ReplayFileReader reader = new ReplayFileReader();
                ReplayFile replayFile = await reader.LoadFileAsync(file.FullName, true);
                if (replayFile == null) continue;

                // Loop through every nth frame to reduce data size and processing time
                const int n = 5;
                for (int i = 0; i < replayFile.nframes; i += n)
                {
                    Frame frame = replayFile.GetFrame(i);
                    if (frame != null && frame.game_status == "playing")
                    {
                        Vector3 pos = frame.disc.position.ToVector3();
                        positions.Add(new Dictionary<string, float>
                        {
                            { "x", pos.X },
                            { "y", pos.Y },
                            { "z", pos.Z },
                        });
                    }
                }
            }

            return positions;
        }
    }
}