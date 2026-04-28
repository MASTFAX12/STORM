#if ENABLE_PLAYFAB
namespace TPSBR
{
	using UnityEngine;
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Text.RegularExpressions;
	using PlayFab;
	using PlayFab.ClientModels;
	using Fusion.Photon.Realtime;

	/// <summary>
	/// Manages PlayFab login, friend system, online status, and session tracking.
	/// Singleton — attach to a persistent GameObject in the Menu scene.
	/// </summary>
	public class PlayFabManager : MonoBehaviour
	{
		public static PlayFabManager Instance { get; private set; }

		// Events
		public event Action OnLoginSuccess;
		public event Action<string> OnLoginFailure;
		public event Action<List<FriendInfo>> OnFriendsUpdated;
		public event Action<List<PlayerLeaderboardEntry>> OnPlayersDiscovered;
		public event Action<string> OnStatusMessage; // For UI feedback

		// Public State
		[Header("My Info")]
		public string MyPlayFabId;
		public string MyDisplayName;
		public string MyAvatarUrl;

		[Header("Session Tracking")]
		public string CurrentSessionName;
		public string CurrentRegion;
		public string CurrentLobbyName;
		public string CurrentScenePath;
		public EGameplayType CurrentGameplayType;
		public bool IsInGame;

		// Cached friends
		private List<FriendInfo> _cachedFriends = new List<FriendInfo>();
		public List<FriendInfo> CachedFriends => _cachedFriends;

		private struct FriendStatusCache
		{
			public bool IsOnline;
			public string SessionName;
			public DateTime LastSeen;
			public float CachedAt;
		}

		public struct FriendJoinInfo
		{
			public bool IsOnline;
			public string SessionName;
			public DateTime LastSeen;
			public string Region;
			public string LobbyName;
			public string ScenePath;
			public EGameplayType GameplayType;
		}

		private readonly Dictionary<string, FriendStatusCache> _friendStatusCache = new Dictionary<string, FriendStatusCache>();
		private const float FRIEND_STATUS_CACHE_SECONDS = 15f;
		private const int MIN_DISPLAY_NAME_LENGTH = 2;
		private const int MAX_DISPLAY_NAME_LENGTH = 25;
		private static readonly Regex PLAYFAB_ID_REGEX = new Regex(@"[A-Fa-f0-9]{16,32}", RegexOptions.Compiled);
		private const int DISCOVER_FETCH_PAGE_SIZE = 30;
		private const int DISCOVER_MAX_PAGES_PER_CALL = 6;
		private const float GET_FRIENDS_MIN_INTERVAL = 1.5f;
		private const float GET_FRIENDS_THROTTLE_RETRY_BASE = 2.0f;
		private const float GET_FRIENDS_THROTTLE_RETRY_MAX = 12.0f;
		private const float ONLINE_STALE_SECONDS = 180f;

		private int _discoverCursor;
		private bool _discoverInProgress;
		private int _queuedDiscoverCount = -1;
		private bool _queuedDiscoverReset;
		private bool _getFriendsInProgress;
		private bool _getFriendsPending;
		private float _lastGetFriendsRequestTime = -999f;
		private float _getFriendsThrottleRetryDelay;
		private Coroutine _getFriendsRetryCoroutine;

		// Status update interval
		private float _statusUpdateTimer;
		private const float STATUS_UPDATE_INTERVAL = 30f;

		private void Awake()
		{
			if (Instance == null)
			{
				Instance = this;
				DontDestroyOnLoad(gameObject);
			}
			else
			{
				Destroy(gameObject);
				return;
			}
		}

		private void Start()
		{
			Login();
		}

		private void Update()
		{
			// Periodically refresh online status
			if (!string.IsNullOrEmpty(MyPlayFabId))
			{
				_statusUpdateTimer += Time.unscaledDeltaTime;
				if (_statusUpdateTimer >= STATUS_UPDATE_INTERVAL)
				{
					_statusUpdateTimer = 0f;
					UpdateOnlineStatus();
				}
			}
		}

		private void OnApplicationQuit()
		{
			SetOffline();
		}

		private void OnApplicationPause(bool paused)
		{
			if (paused)
				SetOffline();
			else if (!string.IsNullOrEmpty(MyPlayFabId))
				UpdateOnlineStatus();
		}

		// ==================== LOGIN ====================

		public void Login()
		{
			var request = new LoginWithCustomIDRequest
			{
				CustomId = SystemInfo.deviceUniqueIdentifier,
				CreateAccount = true,
				TitleId = PlayFabSettings.TitleId,
				InfoRequestParameters = new GetPlayerCombinedInfoRequestParams
				{
					GetPlayerProfile = true,
					ProfileConstraints = new PlayerProfileViewConstraints
					{
						ShowDisplayName = true,
						ShowAvatarUrl = true
					}
				}
			};

			PlayFabClientAPI.LoginWithCustomID(request, HandleLoginSuccess, HandleLoginError);
		}

		private void HandleLoginSuccess(LoginResult result)
		{
			Debug.Log("PlayFab Login Successful!");
			MyPlayFabId = result.PlayFabId;

			// Get display name
			if (result.InfoResultPayload?.PlayerProfile?.DisplayName != null)
			{
				MyDisplayName = result.InfoResultPayload.PlayerProfile.DisplayName;
			}
			else
			{
				MyDisplayName = "Player_" + MyPlayFabId.Substring(0, 6);
				SetDisplayName(MyDisplayName);
			}

			// Get or set default avatar
			if (!string.IsNullOrEmpty(result.InfoResultPayload?.PlayerProfile?.AvatarUrl))
			{
				MyAvatarUrl = result.InfoResultPayload.PlayerProfile.AvatarUrl;
			}
			else
			{
				// Set a default avatar using DiceBear API (generates unique avatar from ID)
				string defaultAvatar = $"https://api.dicebear.com/7.x/bottts-neutral/png?seed={MyPlayFabId}";
				SetAvatarUrl(defaultAvatar);
			}

			UpdateOnlineStatus();
			UpdateLoginStat();
			OnLoginSuccess?.Invoke();
			GetFriends(true);
		}

		private void HandleLoginError(PlayFabError error)
		{
			Debug.LogError("PlayFab Login Failed: " + error.GenerateErrorReport());
			OnLoginFailure?.Invoke(error.ErrorMessage);
		}

		// ==================== DISPLAY NAME ====================

		public void SetDisplayName(string name)
		{
			PlayFabClientAPI.UpdateUserTitleDisplayName(
				new UpdateUserTitleDisplayNameRequest { DisplayName = name },
				result =>
				{
					MyDisplayName = result.DisplayName;
					Debug.Log("Display name set: " + MyDisplayName);
					OnStatusMessage?.Invoke("Display name updated!");
				},
				error => Debug.LogError("SetDisplayName error: " + error.GenerateErrorReport())
			);
		}

		// ==================== AVATAR ====================

		public void SetAvatarUrl(string url)
		{
			MyAvatarUrl = url;
			PlayFabClientAPI.UpdateAvatarUrl(
				new UpdateAvatarUrlRequest { ImageUrl = url },
				result =>
				{
					Debug.Log("Avatar URL set: " + url);
				},
				error => Debug.LogError("SetAvatarUrl error: " + error.GenerateErrorReport())
			);
		}

		// ==================== ONLINE STATUS ====================

		public void UpdateOnlineStatus()
		{
			var data = new Dictionary<string, string>
			{
				{ "Status", "Online" },
				{ "LastSeen", DateTime.UtcNow.ToString("o") },
				{ "SessionName", CurrentSessionName ?? "" },
				{ "Region", CurrentRegion ?? "" },
				{ "LobbyName", CurrentLobbyName ?? "" },
				{ "ScenePath", CurrentScenePath ?? "" },
				{ "GameplayType", ((int)CurrentGameplayType).ToString() },
				{ "IsInGame", IsInGame.ToString() }
			};

			PlayFabClientAPI.UpdateUserData(
				new UpdateUserDataRequest
				{
					Data = data,
					Permission = UserDataPermission.Public
				},
				result => { },
				error => Debug.LogError("Status update error: " + error.GenerateErrorReport())
			);
		}

		public void SetInGame(string sessionName)
		{
			SetInGame(sessionName, GetResolvedRegion(), GetResolvedLobbyName(), CurrentScenePath, CurrentGameplayType);
		}

		public void SetInGame(string sessionName, string region, string lobbyName, string scenePath, EGameplayType gameplayType)
		{
			IsInGame = true;
			CurrentSessionName = sessionName ?? string.Empty;
			CurrentRegion = region ?? string.Empty;
			CurrentLobbyName = lobbyName ?? string.Empty;
			CurrentScenePath = scenePath ?? string.Empty;
			CurrentGameplayType = gameplayType;
			UpdateOnlineStatus();
		}

		public void SetInMenu()
		{
			IsInGame = false;
			CurrentSessionName = "";
			CurrentRegion = "";
			CurrentLobbyName = "";
			CurrentScenePath = "";
			CurrentGameplayType = EGameplayType.None;
			UpdateOnlineStatus();
		}

		private void SetOffline()
		{
			if (string.IsNullOrEmpty(MyPlayFabId)) return;

			var data = new Dictionary<string, string>
			{
				{ "Status", "Offline" },
				{ "LastSeen", DateTime.UtcNow.ToString("o") },
				{ "SessionName", "" },
				{ "Region", "" },
				{ "LobbyName", "" },
				{ "ScenePath", "" },
				{ "GameplayType", "0" },
				{ "IsInGame", "false" }
			};

			PlayFabClientAPI.UpdateUserData(
				new UpdateUserDataRequest
				{
					Data = data,
					Permission = UserDataPermission.Public
				},
				result => { },
				error => { }
			);
		}

		// ==================== FRIENDS ====================

		public void GetFriends(bool force = false)
		{
			if (string.IsNullOrEmpty(MyPlayFabId))
				return;

			float elapsed = Time.unscaledTime - _lastGetFriendsRequestTime;
			if (force == false && elapsed < GET_FRIENDS_MIN_INTERVAL)
			{
				_getFriendsPending = true;
				ScheduleGetFriendsRetry(GET_FRIENDS_MIN_INTERVAL - elapsed);
				if (_cachedFriends.Count > 0)
					OnFriendsUpdated?.Invoke(_cachedFriends);
				return;
			}

			if (_getFriendsInProgress)
			{
				_getFriendsPending = true;
				return;
			}

			RequestGetFriends();
		}

		private void RequestGetFriends()
		{
			_getFriendsInProgress = true;
			_lastGetFriendsRequestTime = Time.unscaledTime;

			var request = new GetFriendsListRequest
			{
				ProfileConstraints = new PlayerProfileViewConstraints
				{
					ShowDisplayName = true,
					ShowAvatarUrl = true,
					ShowLastLogin = true
				}
			};

			PlayFabClientAPI.GetFriendsList(request, result =>
			{
				_getFriendsInProgress = false;
				_getFriendsThrottleRetryDelay = 0f;

				_cachedFriends = result.Friends ?? new List<FriendInfo>();
				_friendStatusCache.Clear();
				_discoverCursor = 0;
				if (_discoverInProgress)
					_queuedDiscoverReset = true;

				OnFriendsUpdated?.Invoke(_cachedFriends);
				ProcessPendingGetFriends();
			}, error =>
			{
				_getFriendsInProgress = false;

				if (IsRequestRateLimited(error))
				{
					_getFriendsPending = true;
					_getFriendsThrottleRetryDelay = _getFriendsThrottleRetryDelay <= 0f
						? GET_FRIENDS_THROTTLE_RETRY_BASE
						: Mathf.Min(_getFriendsThrottleRetryDelay * 1.8f, GET_FRIENDS_THROTTLE_RETRY_MAX);

					ScheduleGetFriendsRetry(_getFriendsThrottleRetryDelay);
					OnStatusMessage?.Invoke("Too many requests. Retrying friends list...");
					return;
				}

				HandleError(error);
				ProcessPendingGetFriends();
			});
		}

		private void ProcessPendingGetFriends()
		{
			if (_getFriendsPending == false)
				return;

			_getFriendsPending = false;
			GetFriends();
		}

		private void ScheduleGetFriendsRetry(float delaySeconds)
		{
			delaySeconds = Mathf.Max(0.1f, delaySeconds);

			if (_getFriendsRetryCoroutine != null)
				StopCoroutine(_getFriendsRetryCoroutine);

			_getFriendsRetryCoroutine = StartCoroutine(GetFriendsRetryCoroutine(delaySeconds));
		}

		private IEnumerator GetFriendsRetryCoroutine(float delaySeconds)
		{
			yield return new WaitForSecondsRealtime(delaySeconds);
			_getFriendsRetryCoroutine = null;

			if (_getFriendsPending)
			{
				_getFriendsPending = false;
				GetFriends();
			}
		}

		private static bool IsRequestRateLimited(PlayFabError error)
		{
			if (error == null)
				return false;

			return error.HttpCode == 429 ||
			       error.Error == PlayFabErrorCode.APIClientRequestRateLimitExceeded ||
			       error.Error == PlayFabErrorCode.PartyTooManyRequests ||
			       error.Error == PlayFabErrorCode.XboxServiceTooManyRequests;
		}

		public void AddFriend(string friendInput, Action onSuccess = null)
		{
			string normalizedInput = NormalizeFriendInput(friendInput);
			if (string.IsNullOrEmpty(normalizedInput))
			{
				OnStatusMessage?.Invoke("Enter PlayFab ID or display name.");
				return;
			}

			if (string.Equals(normalizedInput, MyPlayFabId, StringComparison.OrdinalIgnoreCase))
			{
				OnStatusMessage?.Invoke("You cannot add yourself.");
				return;
			}

			if (TryExtractPlayFabId(normalizedInput, out string playFabId) == true)
			{
				AddFriendByPlayFabId(playFabId, normalizedInput, onSuccess);
				return;
			}

			if (IsValidDisplayName(normalizedInput) == false)
			{
				OnStatusMessage?.Invoke("Invalid name. Use 2-25 chars.");
				return;
			}

			AddFriendByDisplayName(normalizedInput, onSuccess);
		}

		private void AddFriendByPlayFabId(string friendPlayFabId, string fallbackDisplayName, Action onSuccess = null)
		{
			var request = new AddFriendRequest
			{
				FriendPlayFabId = friendPlayFabId
			};

			PlayFabClientAPI.AddFriend(request, result =>
			{
				Debug.Log("Friend added!");
				OnStatusMessage?.Invoke("Friend added!");
				onSuccess?.Invoke();
				GetFriends(true);
			}, error =>
			{
				if (error.Error == PlayFabErrorCode.AccountNotFound && IsValidDisplayName(fallbackDisplayName))
				{
					AddFriendByDisplayName(fallbackDisplayName, onSuccess);
				}
				else
				{
					Debug.LogError("AddFriend error: " + error.GenerateErrorReport());
					OnStatusMessage?.Invoke("Error adding friend.");
				}
			});
		}

		private void AddFriendByDisplayName(string displayName, Action onSuccess = null)
		{
			displayName = NormalizeFriendInput(displayName);
			if (IsValidDisplayName(displayName) == false)
			{
				OnStatusMessage?.Invoke("Invalid name. Use 2-25 chars.");
				return;
			}

			var request = new AddFriendRequest
			{
				FriendTitleDisplayName = displayName
			};

			PlayFabClientAPI.AddFriend(request, result =>
			{
				Debug.Log("Friend added by display name!");
				OnStatusMessage?.Invoke("Friend added!");
				onSuccess?.Invoke();
				GetFriends(true);
			}, error =>
			{
				if (error.Error == PlayFabErrorCode.AccountNotFound)
				{
					Debug.LogWarning("Player not found: " + displayName);
					OnStatusMessage?.Invoke("Player '" + displayName + "' not found.");
				}
				else
				{
					Debug.LogError("AddFriend error: " + error.GenerateErrorReport());
					OnStatusMessage?.Invoke("Error adding friend.");
				}
			});
		}

		public void RemoveFriend(string friendPlayFabId)
		{
			var request = new RemoveFriendRequest
			{
				FriendPlayFabId = friendPlayFabId
			};

			PlayFabClientAPI.RemoveFriend(request, result =>
			{
				_friendStatusCache.Remove(friendPlayFabId);
				OnStatusMessage?.Invoke("Friend removed.");
				GetFriends(true);
			}, HandleError);
		}

		// ==================== FRIEND STATUS ====================

		/// <summary>
		/// Gets the online status and session info for a specific friend.
		/// </summary>
		public void GetFriendStatus(string friendPlayFabId, Action<bool, string, DateTime> onResult)
		{
			GetFriendStatusInternal(friendPlayFabId, onResult, false);
		}

		/// <summary>
		/// Gets fresh online status and session info for a specific friend, bypassing local cache.
		/// </summary>
		public void GetFriendStatusFresh(string friendPlayFabId, Action<bool, string, DateTime> onResult)
		{
			GetFriendStatusInternal(friendPlayFabId, onResult, true);
		}

		public void GetFriendJoinInfoFresh(string friendPlayFabId, Action<FriendJoinInfo> onResult)
		{
			GetFriendJoinInfoInternal(friendPlayFabId, onResult, true);
		}

		private void GetFriendStatusInternal(string friendPlayFabId, Action<bool, string, DateTime> onResult, bool forceRefresh)
		{
			if (string.IsNullOrEmpty(friendPlayFabId))
			{
				onResult?.Invoke(false, "", DateTime.MinValue);
				return;
			}

			if (forceRefresh == false && _friendStatusCache.TryGetValue(friendPlayFabId, out FriendStatusCache cached) == true)
			{
				if (Time.unscaledTime - cached.CachedAt < FRIEND_STATUS_CACHE_SECONDS)
				{
					onResult?.Invoke(cached.IsOnline, cached.SessionName, cached.LastSeen);
					return;
				}
			}

			PlayFabClientAPI.GetUserData(
				new GetUserDataRequest
				{
					PlayFabId = friendPlayFabId,
					Keys = new List<string> { "Status", "SessionName", "LastSeen", "IsInGame", "Region", "LobbyName", "ScenePath", "GameplayType" }
				},
				result =>
				{
					FriendJoinInfo joinInfo = ParseFriendJoinInfo(result);

					_friendStatusCache[friendPlayFabId] = new FriendStatusCache
					{
						IsOnline = joinInfo.IsOnline,
						SessionName = joinInfo.SessionName,
						LastSeen = joinInfo.LastSeen,
						CachedAt = Time.unscaledTime
					};

					onResult?.Invoke(joinInfo.IsOnline, joinInfo.SessionName, joinInfo.LastSeen);
				},
				error =>
				{
					Debug.LogError("GetFriendStatus error: " + error.GenerateErrorReport());
					onResult?.Invoke(false, "", DateTime.MinValue);
				}
			);
		}

		// ==================== DISCOVER PLAYERS ====================

		private void UpdateLoginStat()
		{
			PlayFabClientAPI.UpdatePlayerStatistics(
				new UpdatePlayerStatisticsRequest
				{
					Statistics = new List<StatisticUpdate>
					{
						new StatisticUpdate { StatisticName = "LoginCount", Value = 1 }
					}
				},
				result => { },
				error => { } // Silently fail, stat might not exist yet
			);
		}

		/// <summary>
		/// Discovers players using paged leaderboard scan (stable and less random than single random window).
		/// Requires "LoginCount" statistic to be created in PlayFab Dashboard.
		/// </summary>
		public void DiscoverPlayers(int count = 10, bool resetCursor = false)
		{
			count = Mathf.Clamp(count, 1, 30);

			if (resetCursor)
				_discoverCursor = 0;

			if (_discoverInProgress)
			{
				_queuedDiscoverCount = count;
				_queuedDiscoverReset |= resetCursor;
				return;
			}

			var friendIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			for (int i = 0; i < _cachedFriends.Count; ++i)
			{
				var friend = _cachedFriends[i];
				if (friend == null || string.IsNullOrEmpty(friend.FriendPlayFabId))
					continue;

				friendIds.Add(friend.FriendPlayFabId);
			}

			_discoverInProgress = true;
			var collected = new List<PlayerLeaderboardEntry>(count);
			var batchIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			DiscoverPlayersPage(count, _discoverCursor, 0, false, collected, batchIds, friendIds);
		}

		private void DiscoverPlayersPage(
			int targetCount,
			int startPosition,
			int pagesScanned,
			bool hasWrapped,
			List<PlayerLeaderboardEntry> collected,
			HashSet<string> batchIds,
			HashSet<string> friendIds)
		{
			PlayFabClientAPI.GetLeaderboard(
				new GetLeaderboardRequest
				{
					StatisticName = "LoginCount",
					StartPosition = startPosition,
					MaxResultsCount = DISCOVER_FETCH_PAGE_SIZE,
					ProfileConstraints = new PlayerProfileViewConstraints
					{
						ShowDisplayName = true,
						ShowAvatarUrl = true
					}
				},
				result =>
				{
					var entries = result.Leaderboard ?? new List<PlayerLeaderboardEntry>();
					for (int i = 0; i < entries.Count; ++i)
					{
						var entry = entries[i];
						if (entry == null || string.IsNullOrEmpty(entry.PlayFabId))
							continue;
						if (string.Equals(entry.PlayFabId, MyPlayFabId, StringComparison.OrdinalIgnoreCase))
							continue;
						if (friendIds.Contains(entry.PlayFabId))
							continue;
						if (batchIds.Add(entry.PlayFabId) == false)
							continue;

						collected.Add(entry);
						if (collected.Count >= targetCount)
							break;
					}

					int nextStart = startPosition + entries.Count;
					bool pageHasMore = entries.Count >= DISCOVER_FETCH_PAGE_SIZE;
					bool canScanMorePages = pagesScanned + 1 < DISCOVER_MAX_PAGES_PER_CALL;

					if (collected.Count < targetCount && canScanMorePages)
					{
						if (pageHasMore)
						{
							DiscoverPlayersPage(targetCount, nextStart, pagesScanned + 1, hasWrapped, collected, batchIds, friendIds);
							return;
						}

						if (hasWrapped == false && startPosition > 0)
						{
							DiscoverPlayersPage(targetCount, 0, pagesScanned + 1, true, collected, batchIds, friendIds);
							return;
						}
					}

					_discoverCursor = pageHasMore ? nextStart : 0;
					CompleteDiscover(collected);
				},
				error =>
				{
					Debug.LogError("DiscoverPlayers error: " + error.GenerateErrorReport());
					CompleteDiscover(new List<PlayerLeaderboardEntry>());
				}
			);
		}

		private void GetFriendJoinInfoInternal(string friendPlayFabId, Action<FriendJoinInfo> onResult, bool forceRefresh)
		{
			if (string.IsNullOrEmpty(friendPlayFabId))
			{
				onResult?.Invoke(default);
				return;
			}

			PlayFabClientAPI.GetUserData(
				new GetUserDataRequest
				{
					PlayFabId = friendPlayFabId,
					Keys = new List<string> { "Status", "SessionName", "LastSeen", "IsInGame", "Region", "LobbyName", "ScenePath", "GameplayType" }
				},
				result =>
				{
					FriendJoinInfo joinInfo = ParseFriendJoinInfo(result);

					_friendStatusCache[friendPlayFabId] = new FriendStatusCache
					{
						IsOnline = joinInfo.IsOnline,
						SessionName = joinInfo.SessionName,
						LastSeen = joinInfo.LastSeen,
						CachedAt = Time.unscaledTime
					};

					onResult?.Invoke(joinInfo);
				},
				error =>
				{
					Debug.LogError("GetFriendJoinInfo error: " + error.GenerateErrorReport());
					onResult?.Invoke(default);
				}
			);
		}

		private FriendJoinInfo ParseFriendJoinInfo(GetUserDataResult result)
		{
			FriendJoinInfo joinInfo = default;

			if (result.Data == null)
				return joinInfo;

			if (result.Data.TryGetValue("Status", out UserDataRecord statusRecord))
				joinInfo.IsOnline = statusRecord.Value == "Online";

			if (result.Data.TryGetValue("SessionName", out UserDataRecord sessionRecord))
				joinInfo.SessionName = sessionRecord.Value ?? string.Empty;

			if (result.Data.TryGetValue("Region", out UserDataRecord regionRecord))
				joinInfo.Region = regionRecord.Value ?? string.Empty;

			if (result.Data.TryGetValue("LobbyName", out UserDataRecord lobbyRecord))
				joinInfo.LobbyName = lobbyRecord.Value ?? string.Empty;

			if (result.Data.TryGetValue("ScenePath", out UserDataRecord sceneRecord))
				joinInfo.ScenePath = sceneRecord.Value ?? string.Empty;

			if (result.Data.TryGetValue("GameplayType", out UserDataRecord gameplayRecord) &&
			    int.TryParse(gameplayRecord.Value, out int gameplayTypeRaw))
			{
				joinInfo.GameplayType = (EGameplayType)gameplayTypeRaw;
			}

			if (result.Data.TryGetValue("LastSeen", out UserDataRecord lastSeenRecord))
			{
				DateTime.TryParse(
					lastSeenRecord.Value,
					CultureInfo.InvariantCulture,
					DateTimeStyles.RoundtripKind | DateTimeStyles.AllowWhiteSpaces,
					out joinInfo.LastSeen
				);
			}

			if (result.Data.TryGetValue("IsInGame", out UserDataRecord inGameRecord))
			{
				if (bool.TryParse(inGameRecord.Value, out bool isInGameFlag) && isInGameFlag == false)
					joinInfo.SessionName = string.Empty;
			}

			if (joinInfo.IsOnline && (DateTime.UtcNow - joinInfo.LastSeen).TotalSeconds > ONLINE_STALE_SECONDS)
				joinInfo.IsOnline = false;

			return joinInfo;
		}

		private static string GetResolvedRegion()
		{
			if (Global.RuntimeSettings != null && Global.RuntimeSettings.Region.HasValue() == true)
				return Global.RuntimeSettings.Region;

			if (PhotonAppSettings.Global != null && PhotonAppSettings.Global.AppSettings.FixedRegion.HasValue() == true)
				return PhotonAppSettings.Global.AppSettings.FixedRegion;

			return string.Empty;
		}

		private static string GetResolvedLobbyName()
		{
			return "FusionBR." + Application.version;
		}

		private void CompleteDiscover(List<PlayerLeaderboardEntry> players)
		{
			_discoverInProgress = false;
			OnPlayersDiscovered?.Invoke(players ?? new List<PlayerLeaderboardEntry>());

			if (_queuedDiscoverCount < 0)
				return;

			int queuedCount = _queuedDiscoverCount;
			bool queuedReset = _queuedDiscoverReset;
			_queuedDiscoverCount = -1;
			_queuedDiscoverReset = false;

			DiscoverPlayers(queuedCount, queuedReset);
		}

		// ==================== HELPERS ====================

		private static string NormalizeFriendInput(string input)
		{
			if (string.IsNullOrEmpty(input))
				return string.Empty;

			return input.Trim();
		}

		private static bool IsValidDisplayName(string displayName)
		{
			if (string.IsNullOrWhiteSpace(displayName))
				return false;

			displayName = displayName.Trim();
			return displayName.Length >= MIN_DISPLAY_NAME_LENGTH && displayName.Length <= MAX_DISPLAY_NAME_LENGTH;
		}

		private static bool TryExtractPlayFabId(string input, out string playFabId)
		{
			playFabId = string.Empty;
			if (string.IsNullOrWhiteSpace(input))
				return false;

			string trimmed = input.Trim();
			if (trimmed.StartsWith("ID:", StringComparison.OrdinalIgnoreCase))
				trimmed = trimmed.Substring(3).Trim();

			if (IsLikelyPlayFabId(trimmed))
			{
				playFabId = trimmed.ToUpperInvariant();
				return true;
			}

			var match = PLAYFAB_ID_REGEX.Match(input);
			if (match.Success == false)
				return false;

			playFabId = match.Value.ToUpperInvariant();
			return true;
		}

		private static bool IsLikelyPlayFabId(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
				return false;

			if (value.Length < 16 || value.Length > 32)
				return false;

			for (int i = 0; i < value.Length; ++i)
			{
				char c = value[i];
				bool isHex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
				if (isHex == false)
					return false;
			}

			return true;
		}

		private void HandleError(PlayFabError error)
		{
			Debug.LogError("PlayFab Error: " + error.GenerateErrorReport());
			OnStatusMessage?.Invoke("Error: " + error.ErrorMessage);
		}
	}
}
#endif
