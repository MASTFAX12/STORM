#if ENABLE_PLAYFAB
namespace TPSBR.UI
{
	using System;
	using System.Collections;
	using UnityEngine;
	using Fusion;
	using Fusion.Photon.Realtime;

	/// <summary>
	/// Join-friend-game flow: pre-check status, connect, timeout handling.
	/// </summary>
	public partial class UIFriendController
	{
		private void JoinFriendGame(string sessionName)
		{
			if (string.IsNullOrEmpty(sessionName))
			{
				ShowStatus("Friend is not in a game.");
				return;
			}

			var networking = Global.Networking;
			if (networking == null)
			{
				ShowStatus("Networking is unavailable.");
				return;
			}

			if (networking.IsConnected || networking.IsConnecting)
			{
				ShowStatus("Leave current game first.");
				return;
			}

			ShowStatus("Joining friend's game...");
			var request = new SessionRequest
			{
				GameMode = Fusion.GameMode.Client,
				SessionName = sessionName
			};
			networking.StartGame(request);
		}

		private void BeginJoinFriendGame(string friendPlayFabId, string cachedSessionName, UIFriendItemView sourceView)
		{
			if (string.IsNullOrEmpty(friendPlayFabId))
			{
				ShowStatus("Invalid friend.");
				return;
			}

			if (_joinFlowCoroutine != null)
			{
				ShowStatus("Join is already in progress...");
				return;
			}

			var networking = Global.Networking;
			if (networking == null)
			{
				ShowStatus("Networking is unavailable.");
				return;
			}

			if (networking.IsConnected || networking.IsConnecting)
			{
				ShowStatus("Leave current game first.");
				return;
			}

			_joinFlowCoroutine = StartCoroutine(JoinFriendFlow(friendPlayFabId, cachedSessionName, sourceView));
		}

		private IEnumerator JoinFriendFlow(string friendPlayFabId, string cachedSessionName, UIFriendItemView sourceView)
		{
			SetJoinButtonBusy(sourceView, "CHECK");
			ShowStatus("Checking friend's game...");

			bool responseReceived = false;
			PlayFabManager.FriendJoinInfo joinInfo = default;
			joinInfo.SessionName = cachedSessionName;

			if (PlayFabManager.Instance != null)
			{
				PlayFabManager.Instance.GetFriendJoinInfoFresh(friendPlayFabId, info =>
				{
					responseReceived = true;
					joinInfo = info;
					if (string.IsNullOrEmpty(joinInfo.SessionName))
					{
						joinInfo.SessionName = cachedSessionName;
					}
				});
			}

			float precheckDeadline = Time.unscaledTime + JOIN_PRECHECK_TIMEOUT;
			while (responseReceived == false && Time.unscaledTime < precheckDeadline)
				yield return null;

			if (responseReceived == false)
			{
				ShowStatus("Could not verify friend status. Try again.");
				FinishJoinFlow(friendPlayFabId, sourceView);
				yield break;
			}

			if (joinInfo.IsOnline == false || string.IsNullOrEmpty(joinInfo.SessionName))
			{
				string last = joinInfo.LastSeen > DateTime.MinValue ? (" Last seen " + FormatTimeAgo(joinInfo.LastSeen) + ".") : string.Empty;
				ShowStatus("Friend is not in joinable match." + last);
				RefreshFriendStatus(friendPlayFabId, sourceView);
				FinishJoinFlow(friendPlayFabId, sourceView);
				yield break;
			}

			SetJoinButtonBusy(sourceView, "JOIN...");
			ShowStatus("Joining " + joinInfo.SessionName + "...");

			var networking = Global.Networking;
			if (networking == null)
			{
				ShowStatus("Networking is unavailable.");
				FinishJoinFlow(friendPlayFabId, sourceView);
				yield break;
			}

			ApplyFriendRegion(joinInfo);
			networking.ClearErrorStatus();
			networking.StartGame(BuildJoinRequest(joinInfo));

			float connectDeadline = Time.unscaledTime + JOIN_CONNECT_TIMEOUT;
			while (Time.unscaledTime < connectDeadline)
			{
				if (networking == null)
				{
					ShowStatus("Join cancelled: networking closed.");
					FinishJoinFlow(friendPlayFabId, sourceView);
					yield break;
				}

				if (networking.IsConnected)
				{
					ShowStatus("Connected to friend.");
					FinishJoinFlow(friendPlayFabId, sourceView);
					yield break;
				}

				if (string.IsNullOrEmpty(networking.ErrorStatus) == false)
				{
					ShowStatus("Join failed: " + networking.ErrorStatus);
					FinishJoinFlow(friendPlayFabId, sourceView);
					yield break;
				}

				yield return null;
			}

			string reason = networking != null && string.IsNullOrEmpty(networking.StatusDescription) == false ? networking.StatusDescription : "Timed out.";
			ShowStatus("Join timeout: " + reason);

			if (networking != null && networking.IsConnecting)
				networking.StopGame();

			FinishJoinFlow(friendPlayFabId, sourceView);
		}

		private void SetJoinButtonBusy(UIFriendItemView view, string label)
		{
			if (view == null || view.JoinButton == null)
				return;

			view.JoinButton.gameObject.SetActive(true);
			view.JoinButton.interactable = false;
			StyleButton(view.JoinButton, BTN_PRIMARY);

			if (view.JoinButtonText != null)
				view.JoinButtonText.text = label;
		}

		private void FinishJoinFlow(string friendPlayFabId, UIFriendItemView sourceView)
		{
			if (sourceView != null && sourceView.JoinButton != null)
				sourceView.JoinButton.interactable = true;

			if (string.IsNullOrEmpty(friendPlayFabId) == false)
				RefreshFriendStatus(friendPlayFabId, sourceView);

			_joinFlowCoroutine = null;
		}

		private SessionRequest BuildJoinRequest(PlayFabManager.FriendJoinInfo joinInfo)
		{
			return new SessionRequest
			{
				UserID = GetLocalUserId(),
				GameMode = Fusion.GameMode.Client,
				DisplayName = GetLocalDisplayName(),
				SessionName = joinInfo.SessionName,
				ScenePath = joinInfo.ScenePath,
				GameplayType = joinInfo.GameplayType,
				CustomLobby = string.IsNullOrEmpty(joinInfo.LobbyName) == false ? joinInfo.LobbyName : "FusionBR." + Application.version
			};
		}

		private static void ApplyFriendRegion(PlayFabManager.FriendJoinInfo joinInfo)
		{
			if (string.IsNullOrEmpty(joinInfo.Region))
				return;

			if (Global.RuntimeSettings != null)
			{
				Global.RuntimeSettings.Region = joinInfo.Region;
			}

			if (PhotonAppSettings.Global != null)
			{
				PhotonAppSettings.Global.AppSettings.FixedRegion = joinInfo.Region;
			}
		}

		private static string GetLocalUserId()
		{
			string userId = null;
			if (Global.PlayerService != null && Global.PlayerService.PlayerData != null)
				userId = Global.PlayerService.PlayerData.UserID;

			if (string.IsNullOrEmpty(userId) == false)
				return userId;

			if (PlayFabManager.Instance != null && string.IsNullOrEmpty(PlayFabManager.Instance.MyPlayFabId) == false)
				return PlayFabManager.Instance.MyPlayFabId;

			return SystemInfo.deviceUniqueIdentifier;
		}

		private static string GetLocalDisplayName()
		{
			string nickname = null;
			if (Global.PlayerService != null && Global.PlayerService.PlayerData != null)
				nickname = Global.PlayerService.PlayerData.Nickname;

			if (string.IsNullOrEmpty(nickname) == false)
				return nickname;

			if (PlayFabManager.Instance != null && string.IsNullOrEmpty(PlayFabManager.Instance.MyDisplayName) == false)
				return PlayFabManager.Instance.MyDisplayName;

			return "Player";
		}

		private static string GetLocalNickname()
		{
			if (Global.PlayerService != null && Global.PlayerService.PlayerData != null)
				return Global.PlayerService.PlayerData.Nickname ?? string.Empty;

			return string.Empty;
		}
	}
}
#endif
