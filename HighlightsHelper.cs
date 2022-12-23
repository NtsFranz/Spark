using NVIDIA;
using System;
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
		internal static bool SaveHighlightMaybe(string player, Frame frame, string id)
		{
			string highlightGroupName = IsPlayerHighlightEnabled(player, frame);
			if (highlightGroupName.Length <= 0) return false;
			
			SaveHighlight(highlightGroupName, id);
			return true;

		}

		public static void SaveHighlight(string highlightGroupName, string id, bool onlyAfter = false)
		{
			LoggerEvents.Log(Program.lastFrame, $"Saving NVIDIA Highlights clip: {id}");
			Highlights.VideoHighlightParams vhp = new Highlights.VideoHighlightParams
			{
				groupId = highlightGroupName,
				highlightId = id
			};

			if (onlyAfter)
			{
				vhp.startDelta = -(int)((SparkSettings.instance.nvHighlightsSecondsBefore + SparkSettings.instance.nvHighlightsSecondsAfter) * 1000);
				vhp.endDelta = 0;	
			}
			else
			{
				vhp.startDelta = -(int)(SparkSettings.instance.nvHighlightsSecondsBefore * 1000);
				vhp.endDelta = (int)(SparkSettings.instance.nvHighlightsSecondsAfter * 1000);
			}
			
			Highlights.SetVideoHighlight(vhp, videoCallback);
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
				Highlights.ReturnCode create = Highlights.CreateHighlightsSDK("Echo VR", RequiredScopes);
				if (create != Highlights.ReturnCode.SUCCESS)
				{
					Logger.LogRow(Logger.LogType.Error, "Failed to initialize Highlights on first try: " + create);
					create = Highlights.CreateHighlightsSDK("Echo VR", RequiredScopes);
					if (create != Highlights.ReturnCode.SUCCESS)
					{
						Logger.LogRow(Logger.LogType.Error, "Failed to initialize Highlights: " + create);
						didHighlightsInit = false;
						isNVHighlightsSupported = false;
						return -1;
					}
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
				new()
				{
					Id = "EMOTE",
					HighlightTags = Highlights.HighlightType.Achievement,
					Significance = Highlights.HighlightSignificance.Good,
					UserDefaultInterest = true,
					NameTranslationTable = new[] {new Highlights.TranslationEntry("en-US", "Emote activation"),}
				},
				new()
				{
					Id = "MANUAL",
					HighlightTags = Highlights.HighlightType.Achievement,
					Significance = Highlights.HighlightSignificance.Good,
					UserDefaultInterest = true,
					NameTranslationTable = new[] {new Highlights.TranslationEntry("en-US", "Manual clip"),}
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

		private static string IsPlayerHighlightEnabled(string playerName, Frame frame)
		{
			if (playerName == "[INVALID]") return "";
			Player targetPlayer = frame.GetPlayer(playerName);
			Player clientPlayer = frame.GetPlayer(frame.client_name);
			
			try
			{
				if (targetPlayer == null)
				{
					Logger.LogRow(Logger.LogType.Error, "HIGHLIGHTS: Target player not found");
					return "";
				}
				if (clientPlayer == null)
				{
					Logger.LogRow(Logger.LogType.Error, "HIGHLIGHTS: Client player not found");
					return "";
				}
				
				if (frame.teams == null || !didHighlightsInit || !isNVHighlightsEnabled) return "";

				if (targetPlayer.name == frame.client_name)
				{
					return "PERSONAL_HIGHLIGHT_GROUP";
				}
				else if (ClientHighlightScope != HighlightLevel.CLIENT_ONLY && targetPlayer.team_color == clientPlayer.team_color)
				{
					return "PERSONAL_TEAM_HIGHLIGHT_GROUP";
				}
				else if (ClientHighlightScope == HighlightLevel.ALL || (clientPlayer.team_color == Team.TeamColor.spectator &&
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

		/// <summary>
		/// Clears ALL NVidia Highlight clips that were not saved by the user via the NVidia UI.
		/// </summary>
		public static void ClearUnsavedNVHighlights(bool reopenGroup = false)
		{
			if (!didHighlightsInit) return;
			
			Highlights.CloseGroupParams cgp = new Highlights.CloseGroupParams
			{
				id = "PERSONAL_HIGHLIGHT_GROUP", 
				destroyHighlights = true
			};
			Highlights.CloseGroup(cgp, closeGroupCallback);
			cgp = new Highlights.CloseGroupParams
			{
				id = "PERSONAL_TEAM_HIGHLIGHT_GROUP", 
				destroyHighlights = true
			};
			Highlights.CloseGroup(cgp, closeGroupCallback);
			cgp = new Highlights.CloseGroupParams
			{
				id = "OPPOSING_TEAM_HIGHLIGHT_GROUP", 
				destroyHighlights = true
			};
			Highlights.CloseGroup(cgp, closeGroupCallback);
			
			if (!reopenGroup) return;
			
			Highlights.OpenGroupParams ogp1 = new Highlights.OpenGroupParams
			{
				Id = "PERSONAL_HIGHLIGHT_GROUP",
				GroupDescriptionTable = new[]
				{
					new Highlights.TranslationEntry("en-US", "Personal Highlight Group"),
				}
			};
			Highlights.OpenGroup(ogp1, configStepCallback);
			ogp1.Id = "PERSONAL_TEAM_HIGHLIGHT_GROUP";
			ogp1.GroupDescriptionTable = new[]
			{
				new Highlights.TranslationEntry("en-US", "Personal Team Highlight Group"),
			};
			Highlights.OpenGroup(ogp1, configStepCallback);
			ogp1.Id = "OPPOSING_TEAM_HIGHLIGHT_GROUP";
			ogp1.GroupDescriptionTable = new[]
			{
				new Highlights.TranslationEntry("en-US", "Opposing Team Highlight Group"),
			};
			Highlights.OpenGroup(ogp1, configStepCallback);
		}
	}
}
