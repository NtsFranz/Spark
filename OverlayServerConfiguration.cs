using System.IO;
using Grapevine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Spark
{
	class OverlayServerConfiguration
	{
        public IConfiguration Configuration { get; private set; }

        private int _serverPort = 6724;

        public OverlayServerConfiguration(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.ClearProviders();
            });
        }

        public void ConfigureServer(IRestServer server)
        {
			// The path to your static content
			string folderPath = Path.Combine(Directory.GetCurrentDirectory(), "html");

			server.ContentFolders.Add(folderPath);
			server.UseContentFolders();

			server.Prefixes.Add($"http://localhost:{_serverPort}/");

			/* Configure Router Options (if supported by your router implementation) */
			server.Router.Options.SendExceptionMessages = true;

			var headers = new System.Net.WebHeaderCollection
			{
				{ "Access-Control-Allow-Origin", "*" }
			};
			server.ApplyGlobalResponseHeaders(headers);
		}
    }
}
