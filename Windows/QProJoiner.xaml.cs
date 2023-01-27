using System;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Spark
{
	/// <summary>
	/// Interaction logic for ChooseJoinTypeDialog.xaml
	/// </summary>
	public partial class QProJoiner
	{
		private static string sessionId;
		private bool? sessionDataFound;
		private IWebHost server;

		public QProJoiner(string session_id)
		{
			InitializeComponent();

			sessionId = session_id;

			SetupServer();
		}

		private void SetupServer()
		{
			MainMessage.Text = $"http://{QuestIPFetching.GetLocalIP()}:6726";

			// restart the server
			server = WebHost
				.CreateDefaultBuilder()
				.UseKestrel(x => { x.ListenAnyIP(6726); })
				.UseStartup<Routes>()
				.Build();

			_ = server.RunAsync();
		}


		private void CloseButtonClicked(object sender, EventArgs e)
		{
			_ = server.StopAsync();
			Program.Quit();
		}


		public class Routes
		{
			public void ConfigureServices(IServiceCollection services)
			{
				services.AddCors(o => o.AddPolicy("CorsPolicy", builder =>
				{
					builder.WithOrigins("*")
						.AllowAnyMethod()
						.AllowAnyHeader();
				}));
			}

			public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ICorsService corsService, ICorsPolicyProvider corsPolicyProvider)
			{
				app.UseCors("CorsPolicy");
				app.UseCors(x => x.SetIsOriginAllowed(origin => true));
				app.UseRouting();

				app.UseEndpoints(endpoints =>
				{
					endpoints.MapGet("/", async context =>
					{
						await context.Response.WriteAsync($@"
<!DOCTYPE html>
<html lang='en'>

<head>
	<meta charset='UTF-8'>
	<meta http-equiv='X-UA-Compatible' content='IE=edge'>
	<meta name='viewport' content='width=device-width, initial-scale=1.0'>
	<title>Quest Pro Controllers Match Joiner</title>
	<style>
	    body {{
			font-family: 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, 'Fira Sans', 'Droid Sans', 'Helvetica Neue', Helvetica, Arial, sans-serif;
			background-color: #222;
			color: #eee;
	    }}

		h1 {{
			margin-bottom: 0;
			text-align: center;
		}}

	    h2 {{
			opacity: .7;
			margin-top: 10px;
			text-align: center;
	    }}

	    .buttons {{
			display: flex;
	        flex-direction: column;
	        width: 200px;
	        margin: auto;
	        gap: 10px;
	    }}

	    button {{
			font-size: 20px;
			padding: 16px;
			background-color: #444;
			border-radius: 10px;
			border: none;
			color: #aaa;
			border: 1px solid #555;
	    }}

	    button:hover {{
			opacity: .5;
	    }}

	    button.random {{
			background-color: #444;
	    }}

	    button.orange {{
			background-color: rgb(146, 92, 37);
	    }}

	    button.blue {{
			background-color: rgb(33, 112, 192);
	    }}

	    button.spectator {{
			background-color: rgb(101, 100, 98);
	    }}

	    #messages {{
			margin: 30px auto;
	        max-width: 400px;
	        color: #999;
	        text-align: center;
	    }}
	</style>
</head>

<body>
	<h1>Quest Match Joiner</h1>
	<h2>For Quests using Quest Pro Controllers</h2>
	<div class='buttons'>
		<button class='random' onclick='join(3)'>Random Team</button>
		<button class='orange' onclick='join(1)'>Orange Team</button>
		<button class='blue' onclick='join(0)'>Blue Team</button>
		<button class='spectator' onclick='join(-1)'>Spectator</button>
	</div>

	<div id='messages'></div>

	<script>
		function join(team_idx) {{
			messages.innerHTML = 'Joining... if you are not redirected in a few seconds, please refresh this window and try again.<br>You need to be in a lobby/match for this to work.';

			const data = {{
				'session_id': '{sessionId}',
				'team_idx': team_idx
			}};
			console.log(data);
			for (let i = 0; i < 256; i++) {{
				let url = `http://192.168.43.${{i}}:6721/join_session`;
				fetch(url, {{
					mode: 'no-cors',
					method: 'POST',
					headers: {{
						'Content-Type': 'application/json',
					}},
					body: JSON.stringify(data),
				}})
			}}
			return false;
		}}
	</script>
</body>

</html>
");
					});
				});
			}
		}
	}
}