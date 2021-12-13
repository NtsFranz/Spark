using NVIDIA;
using System;
using System.Linq;
using EchoVRAPI;

namespace Spark
{
	internal static class HighlightsHelper
	{
		public static bool isNVHighlightsEnabled = false;
		public static bool didHighlightsInit;
		public static bool isNVHighlightsSupported = true;
		private static HighlightLevel ClientHighlightScope => (HighlightLevel) SparkSettings.instance.clientHighlightScope;
		private static readonly Highlights.EmptyCallbackDelegate videoCallback = NVSetVideoCallback;
		private static readonly Highlights.EmptyCallbackDelegate openSummaryCallback = Highlights.DefaultOpenSummaryCallback;
		private static readonly Highlights.EmptyCallbackDelegate closeGroupCallback = NVCloseGroupCallback;
		private static readonly Highlights.GetNumberOfHighlightsCallbackDelegate getNumOfHighlightsCallback = NVGetNumberOfHighlightsCallback;
		private static readonly Highlights.EmptyCallbackDelegate configStepCallback = NVConfigCallback;

		public static int nvHighlightClipCount;


		private enum HighlightLevel : int
		{
			CLIENT_ONLY,
			CLIENT_TEAM,
			ALL
		};

		internal static bool SaveHighlightMaybe(Player player, Frame frame, string id)
		{
			string highlightGroupName = IsPlayerHighlightEnabled(player, frame);
			if (highlightGroupName.Length > 0)
			{
				Highlights.VideoHighlightParams vhp = new Highlights.VideoHighlightParams
				{
					groupId = highlightGroupName,
					highlightId = id,
					startDelta = -(int)(SparkSettings.instance.nvHighlightsSecondsBefore * 1000),
					endDelta = (int)(SparkSettings.instance.nvHighlightsSecondsAfter * 1000)
				};
				Highlights.SetVideoHighlight(vhp, videoCallback);
				return true;
			}
			else return false;
		}

		internal static bool SaveHighlightMaybe(string player, Frame frame, string id)
		{
			string highlightGroupName = IsPlayerHighlightEnabled(player, frame);
			if (highlightGroupName.Length <= 0) return false;
			
			Highlights.VideoHighlightParams vhp = new()
			{
				groupId = highlightGroupName,
				highlightId = id,
				startDelta = -(int)(SparkSettings.instance.nvHighlightsSecondsBefore * 1000),
				endDelta = (int)(SparkSettings.instance.nvHighlightsSecondsAfter * 1000)
			};
			Highlights.SetVideoHighlight(vhp, videoCallback);
			return true;

		}

		public static void CloseNVHighlights(bool wasDisableNVHCall = false)
		{
			try
			{
				if (!didHighlightsInit) return;
				
				if (SparkSettings.instance.clearHighlightsOnExit && !wasDisableNVHCall)
				{
					ClearUnsavedNVHighlights(false);

				}
				Highlights.ReleaseHighlightsSDK();
			}
			catch (Exception e)
			{
				Logger.LogRow(Logger.LogType.Error, $"Failed during CloseNVHighlights\n{e}");
			}
		}

		public static int InitHighlightsSDK(bool isCheck)
		{
			try
			{
				Highlights.HighlightScope[] RequiredScopes =
				{
					Highlights.HighlightScope.Highlights,
					Highlights.HighlightScope.HighlightsRecordVideo,
					Highlights.HighlightScope.HighlightsRecordScreenshot
				};
				if (Highlights.CreateHighlightsSDK("EchoVR", RequiredScopes) != Highlights.ReturnCode.SUCCESS)
				{
					Logger.LogRow(Logger.LogType.Error, "Failed to initialize Highlights");
					didHighlightsInit = false;
					isNVHighlightsSupported = false;
					return -1;
				}

				if (isCheck)
				{
					Highlights.ReleaseHighlightsSDK();
					isNVHighlightsSupported = true;
					return 1;
				}

				didHighlightsInit = true;
				return 1;
			}
			catch (Exception e)
			{
				Logger.LogRow(Logger.LogType.Error, $"Failed to initialize Highlights: {e}");
				didHighlightsInit = false;
				isNVHighlightsSupported = false;
				return -1;
			}
		}

		public static int SetupNVHighlights()
		{
			if (InitHighlightsSDK(false) < 0)
			{
				return -1;
			}

			Highlights.RequestPermissions(configStepCallback);
			// Configure Highlights
			Highlights.HighlightDefinition[] highlightDefinitions =
			{
				new()
				{
					Id = "SAVE",
					HighlightTags = Highlights.HighlightType.Achievement,
					Significance = Highlights.HighlightSignificance.Good,
					UserDefaultInterest = true,
					NameTranslationTable = new[] {new Highlights.TranslationEntry("en-US", "Save!"),}
				},
				new()
				{
					Id = "SCORE",
					HighlightTags = Highlights.HighlightType.Achievement,
					Significance = Highlights.HighlightSignificance.Good,
					UserDefaultInterest = true,
					NameTranslationTable = new[] {new Highlights.TranslationEntry("en-US", "Goal!"),}
				},
				new()
				{
					Id = "INTERCEPTION",
					HighlightTags = Highlights.HighlightType.Achievement,
					Significance = Highlights.HighlightSignificance.Good,
					UserDefaultInterest = false,
					NameTranslationTable = new[] {new Highlights.TranslationEntry("en-US", "Interception!"),}
				},
				new()
				{
					Id = "STEAL_SAVE",
					HighlightTags = Highlights.HighlightType.Achievement,
					Significance = Highlights.HighlightSignificance.Good,
					UserDefaultInterest = true,
					NameTranslationTable = new[] {new Highlights.TranslationEntry("en-US", "Steal counts as Save!"),}
				},
				new()
				{
					Id = "ASSIST",
					HighlightTags = Highlights.HighlightType.Achievement,
					Significance = Highlights.HighlightSignificance.Good,
					UserDefaultInterest = true,
					NameTranslationTable = new[] {new Highlights.TranslationEntry("en-US", "Scoring Assist!"),}
				},
				new()
				{
					Id = "BIG_BOOST",
					HighlightTags = Highlights.HighlightType.Achievement,
					Significance = Highlights.HighlightSignificance.Good,
					UserDefaultInterest = false,
					NameTranslationTable = new[] {new Highlights.TranslationEntry("en-US", "Big boost!"),}
				},
			};

			Highlights.ConfigureHighlights(highlightDefinitions, "en-US", configStepCallback);

			// Open Groups
			Highlights.OpenGroup(new Highlights.OpenGroupParams
			{
				Id = "PERSONAL_HIGHLIGHT_GROUP",
				GroupDescriptionTable =
					new[] {new Highlights.TranslationEntry("en-US", "Personal Highlight Group"),}
			}, configStepCallback);

			Highlights.OpenGroup(new Highlights.OpenGroupParams
			{
				Id = "PERSONAL_TEAM_HIGHLIGHT_GROUP",
				GroupDescriptionTable = new[]
				{
					new Highlights.TranslationEntry("en-US", "Personal Team Highlight Group"),
				}
			}, configStepCallback);

			Highlights.OpenGroup(new Highlights.OpenGroupParams
			{
				Id = "OPPOSING_TEAM_HIGHLIGHT_GROUP",
				GroupDescriptionTable = new[]
				{
					new Highlights.TranslationEntry("en-US", "Opposing Team Highlight Group"),
				}
			}, configStepCallback);

			GetNVHighlightsCount();
			return 1;
		}

		public static bool DoNVClipsExist()
		{
			return nvHighlightClipCount > 0;
		}

		public static void ShowNVHighlights()
		{
			if (!didHighlightsInit) return;
			
			Highlights.GroupView[] gViews = new Highlights.GroupView[3];
			gViews[0] = new Highlights.GroupView
			{
				GroupId = "PERSONAL_HIGHLIGHT_GROUP",
				SignificanceFilter = Highlights.HighlightSignificance.Good,
				TagFilter = Highlights.HighlightType.Achievement
			};
			gViews[1] = new Highlights.GroupView
			{
				GroupId = "PERSONAL_TEAM_HIGHLIGHT_GROUP",
				SignificanceFilter = Highlights.HighlightSignificance.Good,
				TagFilter = Highlights.HighlightType.Achievement
			};
			gViews[2] = new Highlights.GroupView
			{
				GroupId = "OPPOSING_TEAM_HIGHLIGHT_GROUP",
				SignificanceFilter = Highlights.HighlightSignificance.Good,
				TagFilter = Highlights.HighlightType.Achievement
			};

			Highlights.OpenSummary(gViews, openSummaryCallback);
		}

		private static void GetNVHighlightsCount()
		{
			if (!didHighlightsInit) return;
			
			nvHighlightClipCount = 0;
			Highlights.GroupView groupView = new() {GroupId = "PERSONAL_HIGHLIGHT_GROUP"};
			Highlights.GetNumberOfHighlights(groupView, getNumOfHighlightsCallback);

			Highlights.GroupView groupView2 = new() {GroupId = "PERSONAL_TEAM_HIGHLIGHT_GROUP"};
			Highlights.GetNumberOfHighlights(groupView2, getNumOfHighlightsCallback);

			Highlights.GroupView groupView3 = new() {GroupId = "OPPOSING_TEAM_HIGHLIGHT_GROUP"};
			Highlights.GetNumberOfHighlights(groupView3, getNumOfHighlightsCallback);
		}

		public static void NVSetVideoCallback(Highlights.ReturnCode ret, int id)
		{
			if (ret == Highlights.ReturnCode.SUCCESS)
			{
				nvHighlightClipCount++;
				Console.WriteLine($"SetVideoCallback {id} returns success");
			}
			else
			{
				Console.WriteLine($"SetVideoCallback {id} returns unsuccess");
			}
		}
		public static void NVCloseGroupCallback(Highlights.ReturnCode ret, int id)
		{
			if (ret == Highlights.ReturnCode.SUCCESS)
			{
				nvHighlightClipCount = 0;
				Console.WriteLine($"CloseGroupCallback {id} returns success");
			}
			else
			{
				Console.WriteLine($"CloseGroupCallback {id} returns unsuccess");
			}
		}

		private static void NVConfigCallback(Highlights.ReturnCode ret, int id)
		{
			if (ret == Highlights.ReturnCode.SUCCESS)
			{
				nvHighlightClipCount = 0;
				Console.WriteLine($"ConfigStep {id} returns success");
			}
			else
			{
				Console.WriteLine($"ConfigStep {id} returns unsuccess");
			}
		}

		private static void NVGetNumberOfHighlightsCallback(Highlights.ReturnCode ret, int number, int id)
		{
			if (ret == Highlights.ReturnCode.SUCCESS)
			{
				nvHighlightClipCount += number;
				Console.WriteLine($"GetNumberOfHighlightsCallback {id} returns " + number);
			}
			else
			{
				Console.WriteLine($"GetNumberOfHighlightsCallback {id} returns unsuccess");
			}
		}


		private static string IsPlayerHighlightEnabled(Player player, Frame frame)
		{
			try
			{
				if (player == null || frame.teams == null || !didHighlightsInit || !isNVHighlightsEnabled) return "";

				Team.TeamColor clientTeam = frame.teams.FirstOrDefault(t => t.players.Exists(p => p.name == frame.client_name)).color;
				if (player.name == frame.client_name)
				{
					return "PERSONAL_HIGHLIGHT_GROUP";
				}
				else if (ClientHighlightScope != HighlightLevel.CLIENT_ONLY && player.team.color == clientTeam)
				{
					return "PERSONAL_TEAM_HIGHLIGHT_GROUP";
				}
				else if (ClientHighlightScope == HighlightLevel.ALL || (clientTeam == Team.TeamColor.spectator &&
				                                                        SparkSettings.instance.nvHighlightsSpectatorRecord))
				{
					return "OPPOSING_TEAM_HIGHLIGHT_GROUP";
				}
			}
			catch (Exception ex)
			{
				Logger.LogRow(Logger.LogType.Error, $"Something broke while checking if player highlights is enabled\n{ex}");
			}
			
			return "";
		}

		private static string IsPlayerHighlightEnabled(string playerName, Frame frame)
		{
			if (playerName == "[INVALID]") return "";
			Player highlightPlayer = frame.GetPlayer(playerName);
			return IsPlayerHighlightEnabled(highlightPlayer, frame);
		}

		/// <summary>
		/// Clears ALL NVidia Highlight clips that were not saved by the user via the NVidia UI.
		/// </summary>
		public static void ClearUnsavedNVHighlights(bool reopenGroup = false)
		{
			if (!didHighlightsInit) return;
			
			Highlights.CloseGroupParams cgp = new() { id = "PERSONAL_HIGHLIGHT_GROUP", destroyHighlights = true };
			Highlights.CloseGroup(cgp, closeGroupCallback);
			cgp = new Highlights.CloseGroupParams { id = "PERSONAL_TEAM_HIGHLIGHT_GROUP", destroyHighlights = true };
			Highlights.CloseGroup(cgp, closeGroupCallback);
			cgp = new Highlights.CloseGroupParams { id = "OPPOSING_TEAM_HIGHLIGHT_GROUP", destroyHighlights = true };
			Highlights.CloseGroup(cgp, closeGroupCallback);
			
			if (!reopenGroup) return;
			
			Highlights.OpenGroupParams ogp1 = new();
			ogp1.Id = "PERSONAL_HIGHLIGHT_GROUP";
			ogp1.GroupDescriptionTable = new[] { new Highlights.TranslationEntry("en-US", "Personal Highlight Group"), };
			Highlights.OpenGroup(ogp1, configStepCallback);
			ogp1.Id = "PERSONAL_TEAM_HIGHLIGHT_GROUP";
			ogp1.GroupDescriptionTable = new[] { new Highlights.TranslationEntry("en-US", "Personal Team Highlight Group"), };
			Highlights.OpenGroup(ogp1, configStepCallback);
			ogp1.Id = "OPPOSING_TEAM_HIGHLIGHT_GROUP";
			ogp1.GroupDescriptionTable = new[] { new Highlights.TranslationEntry("en-US", "Opposing Team Highlight Group"), };
			Highlights.OpenGroup(ogp1, configStepCallback);
		}
	}
}
