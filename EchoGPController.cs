using EchoVRAPI;

namespace Spark
{
	public class EchoGPController
	{
		public EchoGPController()
		{
			Program.NewFrame += ProcessFrame;
		}

		private static void ProcessFrame(Frame f)
		{
			if (!f.InCombat) return;

			switch (f.map_name)
			{
				case "mpl_surge_a":
					break;
				case "mpl_dyson_a":
					break;
				case "mpl_combustion_a":
					break;
			}
		}
	}
}