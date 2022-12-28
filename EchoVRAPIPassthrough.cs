using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Spark
{
	public static class EchoVRAPIPassthrough
	{
		private static void AddDefaultHeaders(HttpContext context)
		{
			context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
			context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Accept, X-Requested-With");
			context.Response.Headers.Add("Content-Type", "application/json");
		}

		public static void MapRoutes(IEndpointRouteBuilder endpoints)
		{
			endpoints.MapGet("/session", async context =>
			{
				AddDefaultHeaders(context);

				if (Program.InGame)
				{
					await context.Response.WriteAsync(Program.lastJSON);
				}
				else
				{
					context.Response.StatusCode = 404;
					await context.Response.WriteAsync("");
				}
			});

			endpoints.MapGet("/player_bones", async context =>
			{
				AddDefaultHeaders(context);

				try
				{
					string resp = await FetchUtils.client.GetStringAsync($"http://{Program.echoVRIP}:{Program.echoVRPort}/player_bones");
					await context.Response.WriteAsync(resp);
				}
				catch (Exception)
				{
					context.Response.StatusCode = 404;
					await context.Response.WriteAsync("");
				}
			});

			endpoints.MapGet("/get_rules", async context =>
			{
				AddDefaultHeaders(context);

				try
				{
					string resp = await FetchUtils.client.GetStringAsync($"http://{Program.echoVRIP}:{Program.echoVRPort}/get_rules");
					await context.Response.WriteAsync(resp);
				}
				catch (Exception)
				{
					context.Response.StatusCode = 404;
					await context.Response.WriteAsync("");
				}
			});
			endpoints.MapPost("/set_rules", async context =>
			{
				await PostProxy(context, "set_rules");
			});
			endpoints.MapPost("/ui_visibility", async context =>
			{
				await PostProxy(context, "ui_visibility");
			});
			endpoints.MapPost("/minimap_visibility", async context =>
			{
				await PostProxy(context, "minimap_visibility");
			});
			endpoints.MapPost("/team_muted", async context =>
			{
				await PostProxy(context, "team_muted");
			});
			endpoints.MapPost("/camera_transform", async context =>
			{
				await PostProxy(context, "camera_transform");
			});
			endpoints.MapPost("/camera_mode", async context =>
			{
				await PostProxy(context, "camera_mode");
			});
			endpoints.MapPost("/join_session", async context =>
			{
				await PostProxy(context, "join_session");
			});
			endpoints.MapPost("/set_ready", async context =>
			{
				await PostProxy(context, "set_ready");
			});
			endpoints.MapPost("/set_pause", async context =>
			{
				await PostProxy(context, "set_pause");
			});
			endpoints.MapPost("/restart_request", async context =>
			{
				await PostProxy(context, "restart_request");
			});
		}

		private static async Task PostProxy(HttpContext context, string endpoint)
		{
			AddDefaultHeaders(context);
			try
			{
				using StreamReader reader = new StreamReader(context.Request.Body);
				string body = await reader.ReadToEndAsync();
				StringContent content = new StringContent(body, Encoding.UTF8, "application/json");
				HttpResponseMessage resp = await FetchUtils.client.PostAsync($"http://{Program.echoVRIP}:{Program.echoVRPort}/{endpoint}", content);
				await context.Response.WriteAsync(await resp.Content.ReadAsStringAsync());
			}
			catch (Exception)
			{
				context.Response.StatusCode = 404;
				await context.Response.WriteAsync("");
			}
		}
	}
}