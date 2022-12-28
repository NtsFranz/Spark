using System;
using System.Collections.Generic;
using System.Linq;
using EchoVRAPI;
using Newtonsoft.Json;

namespace Spark
{
	/// <summary>
	/// Object containing a teams basic data and MatchPlayer for the corresponding team.
	/// </summary>
	public class TeamData
	{
		public string vrmlTeamName = "";
		public string vrmlTeamLogo = "";
		
		public void FindTeamNamesFromPlayerList(Team team)
		{
			//if (frame.private_match)
			{
				if (team.players.Count > 0)
				{
					FetchUtils.GetRequestCallback($"{Program.APIURL}/vrml/get_team_name_from_list?player_list=[{string.Join(',', team.player_names.Select(name => $"\"{name}\""))}]",
						new Dictionary<string, string> { { "x-api-key", DiscordOAuth.igniteUploadKey } },
						returnJSON =>
						{
							try
							{
								if (!string.IsNullOrEmpty(returnJSON))
								{
									Dictionary<string, string> data = JsonConvert.DeserializeObject<Dictionary<string, string>>(returnJSON);
									if (data != null)
									{
										// if there are at least 2 players from that team
										if (data.ContainsKey("count") && int.Parse(data["count"]) >= 2)
										{
											vrmlTeamName = data["team_name"];
											vrmlTeamLogo = data["team_logo"];
										}
										else // reset the names if people leave
										{
											vrmlTeamName = string.Empty;
											vrmlTeamLogo = string.Empty;
										}
									}
									else
									{
										Logger.LogRow(Logger.LogType.Error, $"Null team detection response.");
									}
								}
							}
							catch (Exception ex)
							{
								Logger.LogRow(Logger.LogType.Error, $"Can't parse get_team_name_from_list response: {ex}");
							}
						}
					);
				}
			}
		}

	}

}
