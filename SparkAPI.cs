using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Spark
{
	public static class SparkAPI
	{

		public static void MapRoutes(IEndpointRouteBuilder endpoints)
		{
				//string file = OverlayServer4.ReadResource("api_index.html");
				//await context.Response.WriteAsync(file);

			endpoints.MapGet("/api/go_to_waypoint/{index}", async context =>
			{
				if (int.TryParse(context.Request.RouteValues["index"].ToString(), out int index)) {
					CameraWrite.TryGoToWaypoint(index);
					await context.Response.WriteAsync($"Going to waypoint {index}");
				} else{
					await context.Response.WriteAsync($"Waypoint index must be an int!");
				}
			});


			endpoints.MapGet("/api/play_animation/{index}", async context =>
			{
				if (int.TryParse(context.Request.RouteValues["index"].ToString(), out int index))
				{
					Program.cameraWriteWindow.TryPlayAnim(index);
					await context.Response.WriteAsync($"Playing animation {index}");
				}
				else
				{
					await context.Response.WriteAsync($"Animation index must be an int!");
				}
			});


			endpoints.MapGet("/api/orbit_disc_enabled/{enabled}", async context =>
			{
				if (bool.TryParse(context.Request.RouteValues["enabled"].ToString(), out bool enabled))
				{
					Program.cameraWriteWindow.OrbitDisc(enabled);
					await context.Response.WriteAsync(enabled ? "Enabled disc orbit" : "Disabled disc orbit");
				}
				else
				{
					await context.Response.WriteAsync($"Must provide true or false!");
				}
			});

			// resources
		}
	}
}