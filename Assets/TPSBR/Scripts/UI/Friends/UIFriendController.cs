#if ENABLE_PLAYFAB
namespace TPSBR.UI
{
	using PlayFab.ClientModels;
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using TMPro;
	using UnityEngine;
	using UnityEngine.Networking;
	using UnityEngine.UI;

	/// <summary>
	/// Friend panel controller with stable bindings and responsive layout rules.
	/// </summary>
	public partial class UIFriendController : MonoBehaviour
	{
		[Header("My Info")]
		[SerializeField] private TMP_Text _myIdText;
		[SerializeField] private TMP_Text _myNameText;
		[SerializeField] private RawImage _myAvatarImage;
		[SerializeField] private Button _copyIdButton;
		[SerializeField] private Button _myProfileButton;

		[Header("Input")]
		[SerializeField] private TMP_InputField _inputField;
		[SerializeField] private Button _addButton;
		[SerializeField] private Button _pasteButton;

		[Header("Tabs")]
		[SerializeField] private Button _friendsTabButton;
		[SerializeField] private Button _requestsTabButton;
		[SerializeField] private Button _discoverTabButton;

		[Header("Lists")]
		[SerializeField] private Transform _friendListContent;
		[SerializeField] private GameObject _friendItemPrefab;
		[SerializeField] private Button _refreshButton;

		[Header("Status Feedback")]
		[SerializeField] private TMP_Text _statusText;

		private enum Tab { Friends, Requests, Discover }
		private enum ItemMode { Friend, Request, Discover }

		private struct FriendPresence
		{
			public bool IsOnline;
			public string SessionName;
			public DateTime LastSeen;
			public float CachedAt;
		}

		private static readonly Color PANEL_BG = new Color(0.04f, 0.07f, 0.11f, 0.96f);
		private static readonly Color ITEM_BG = new Color(0.09f, 0.12f, 0.17f, 0.96f);
		private static readonly Color TAB_OFF = new Color(0.12f, 0.16f, 0.22f, 1f);
		private static readonly Color TAB_ON = new Color(0.10f, 0.54f, 0.75f, 1f);
		private static readonly Color BTN_PRIMARY = new Color(0.11f, 0.15f, 0.22f, 1f);
		private static readonly Color BTN_SUCCESS = new Color(0.12f, 0.68f, 0.46f, 1f);
		private static readonly Color BTN_DANGER = new Color(0.95f, 0.19f, 0.19f, 1f);
		private static readonly Color BTN_WARNING = new Color(0.90f, 0.42f, 0.17f, 1f);
		private static readonly Color ONLINE_COLOR = new Color(0.19f, 0.84f, 0.54f, 1f);
		private static readonly Color OFFLINE_COLOR = new Color(0.56f, 0.56f, 0.56f, 1f);

		private const float PRESENCE_CACHE_SECONDS = 15f;
		private const float PRESENCE_POLL_INTERVAL = 4f;
		private const int PRESENCE_POLL_BATCH_SIZE = 2;
		private const float REMOVE_CONFIRM_SECONDS = 3f;
		private const float JOIN_PRECHECK_TIMEOUT = 5f;
		private const float JOIN_CONNECT_TIMEOUT = 18f;

		private Tab _activeTab = Tab.Friends;
		private readonly List<FriendInfo> _allFriends = new List<FriendInfo>();
		private readonly Dictionary<string, FriendPresence> _presenceCache = new Dictionary<string, FriendPresence>();
		private readonly Dictionary<string, UIFriendItemView> _friendViewsById = new Dictionary<string, UIFriendItemView>(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<string, float> _removeConfirmWindow = new Dictionary<string, float>();
		private readonly HashSet<string> _discoverAddInProgress = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private Coroutine _statusCoroutine;
		private Coroutine _joinFlowCoroutine;
		private float _presencePollTimer;
		private int _presencePollIndex;
		private Vector2Int _lastScreenSize;
		private RectTransform _profileOverlayRoot;
		private RectTransform _profileCardRect;
		private RawImage _profileAvatarImage;
		private TMP_Text _profileTitleText;
		private TMP_Text _profileInfoText;
		private TMP_Text _profileHintText;
		private Button _profileCloseButton;
		private Button _profileCopyButton;
		private Button _profileSyncNameButton;
		private Button _profileNewAvatarButton;
		private Button _profileRefreshButton;
		private Coroutine _profileHintCoroutine;
		private Coroutine _profileAvatarCoroutine;
		private Texture _profileAvatarTexture;
		private string _loadedProfileAvatarUrl;

		private void Start()
		{
			ResolveOptionalReferences();
			EnsureMyProfileButton();
			EnsureProfileOverlay();
			ApplyResponsiveLayoutAndTheme();
			EnsureListLayout();
			_lastScreenSize = new Vector2Int(Screen.width, Screen.height);

			if (PlayFabManager.Instance != null)
			{
				BindPlayFabEvents();
				RefreshMyHeader();
			}

			if (_addButton != null)
				_addButton.onClick.AddListener(OnAddClicked);
			if (_refreshButton != null)
				_refreshButton.onClick.AddListener(OnRefreshClicked);
			if (_copyIdButton != null)
				_copyIdButton.onClick.AddListener(OnCopyIdClicked);
			if (_myProfileButton != null)
				_myProfileButton.onClick.AddListener(OnMyProfileClicked);
			if (_pasteButton != null)
				_pasteButton.onClick.AddListener(OnPasteClicked);
			if (_friendsTabButton != null)
				_friendsTabButton.onClick.AddListener(() => SwitchTab(Tab.Friends));
			if (_requestsTabButton != null)
			{
				_requestsTabButton.onClick.AddListener(() => SwitchTab(Tab.Requests));
				_requestsTabButton.gameObject.SetActive(false);
			}
			if (_discoverTabButton != null)
				_discoverTabButton.onClick.AddListener(() => SwitchTab(Tab.Discover));

			SwitchTab(Tab.Friends);
			OnRefreshClicked();
		}

		private void OnEnable()
		{
			BindPlayFabEvents();
			RefreshMyHeader();
		}

		private void Update()
		{
			if (_lastScreenSize.x != Screen.width || _lastScreenSize.y != Screen.height)
			{
				_lastScreenSize = new Vector2Int(Screen.width, Screen.height);
				ApplyResponsiveLayoutAndTheme();
				EnsureListLayout();
			}

			TickPresenceAutoRefresh();
		}

		private void OnDestroy()
		{
			UnbindPlayFabEvents();

			if (_addButton != null)
				_addButton.onClick.RemoveListener(OnAddClicked);
			if (_refreshButton != null)
				_refreshButton.onClick.RemoveListener(OnRefreshClicked);
			if (_copyIdButton != null)
				_copyIdButton.onClick.RemoveListener(OnCopyIdClicked);
			if (_myProfileButton != null)
				_myProfileButton.onClick.RemoveListener(OnMyProfileClicked);
			if (_pasteButton != null)
				_pasteButton.onClick.RemoveListener(OnPasteClicked);

			if (_joinFlowCoroutine != null)
			{
				StopCoroutine(_joinFlowCoroutine);
				_joinFlowCoroutine = null;
			}

			_discoverAddInProgress.Clear();

			if (_profileHintCoroutine != null)
			{
				StopCoroutine(_profileHintCoroutine);
				_profileHintCoroutine = null;
			}

			if (_profileAvatarCoroutine != null)
			{
				StopCoroutine(_profileAvatarCoroutine);
				_profileAvatarCoroutine = null;
			}

			ReleaseProfileAvatarTexture();
		}

		private void OnDisable()
		{
			UnbindPlayFabEvents();
		}

		private void BindPlayFabEvents()
		{
			if (PlayFabManager.Instance == null)
				return;

			PlayFabManager.Instance.OnLoginSuccess -= OnPlayFabLoginSuccess;
			PlayFabManager.Instance.OnFriendsUpdated -= OnFriendsReceived;
			PlayFabManager.Instance.OnPlayersDiscovered -= OnPlayersDiscovered;
			PlayFabManager.Instance.OnStatusMessage -= ShowStatus;

			PlayFabManager.Instance.OnLoginSuccess += OnPlayFabLoginSuccess;
			PlayFabManager.Instance.OnFriendsUpdated += OnFriendsReceived;
			PlayFabManager.Instance.OnPlayersDiscovered += OnPlayersDiscovered;
			PlayFabManager.Instance.OnStatusMessage += ShowStatus;
		}

		private void UnbindPlayFabEvents()
		{
			if (PlayFabManager.Instance == null)
				return;

			PlayFabManager.Instance.OnLoginSuccess -= OnPlayFabLoginSuccess;
			PlayFabManager.Instance.OnFriendsUpdated -= OnFriendsReceived;
			PlayFabManager.Instance.OnPlayersDiscovered -= OnPlayersDiscovered;
			PlayFabManager.Instance.OnStatusMessage -= ShowStatus;
		}

		private void OnPlayFabLoginSuccess()
		{
			RefreshMyHeader();
		}

		private void RefreshMyHeader()
		{
			var manager = PlayFabManager.Instance;

			if (_myIdText != null)
			{
				string id = manager != null ? manager.MyPlayFabId : string.Empty;
				_myIdText.text = "ID: " + (string.IsNullOrEmpty(id) ? "-" : id);
			}

			if (_myNameText != null)
			{
				string name = manager != null ? manager.MyDisplayName : string.Empty;
				_myNameText.text = string.IsNullOrWhiteSpace(name) ? "Player" : name;
			}

			if (_myAvatarImage != null && manager != null && string.IsNullOrWhiteSpace(manager.MyAvatarUrl) == false && CanRunCoroutines())
				StartCoroutine(LoadAvatar(manager.MyAvatarUrl, _myAvatarImage));
		}

		private bool CanRunCoroutines()
		{
			return isActiveAndEnabled && gameObject.activeInHierarchy;
		}

		private void ResolveOptionalReferences()
		{
			if (_pasteButton == null)
			{
				var paste = transform.Find("Paste Button");
				if (paste == null)
					paste = transform.Find("PasteButton");
				if (paste != null)
					_pasteButton = paste.GetComponent<Button>();
			}

			if (_myProfileButton == null)
			{
				var profile = transform.Find("MyProfileButton");
				if (profile != null)
					_myProfileButton = profile.GetComponent<Button>();
			}
		}

		private void SwitchTab(Tab tab)
		{
			_activeTab = tab;
			_presencePollTimer = 0f;
			_presencePollIndex = 0;
			_discoverAddInProgress.Clear();
			SetTabActive(_friendsTabButton, tab == Tab.Friends);
			SetTabActive(_requestsTabButton, tab == Tab.Requests);
			SetTabActive(_discoverTabButton, tab == Tab.Discover);

			ClearList();

			if (tab == Tab.Friends)
			{
				ShowFriendsList();
				return;
			}

			if (tab == Tab.Requests)
			{
				ShowRequestsList();
				return;
			}

			PlayFabManager.Instance?.DiscoverPlayers(10, true);
		}

		private void SetTabActive(Button tabButton, bool active)
		{
			if (tabButton == null)
				return;

			StyleButton(tabButton, active ? TAB_ON : TAB_OFF);

			var text = tabButton.GetComponentInChildren<TMP_Text>(true);
			if (text != null)
			{
				text.color = active ? Color.white : new Color(0.8f, 0.8f, 0.78f, 1f);
				text.fontSize = Mathf.Round((active ? 19f : 18f) * GetUiScale());
				text.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
			}
		}

		private void OnFriendsReceived(List<FriendInfo> friends)
		{
			RefreshMyHeader();

			_allFriends.Clear();
			if (friends != null)
				_allFriends.AddRange(friends);

			if (CanRunCoroutines() == false)
				return;

			if (_activeTab == Tab.Friends)
				ShowFriendsList();
			else if (_activeTab == Tab.Requests)
				ShowRequestsList();
		}

		private void ShowFriendsList()
		{
			ClearList();
			int count = 0;

			for (int i = 0; i < _allFriends.Count; ++i)
			{
				var friend = _allFriends[i];
				if (GetFriendTag(friend) == "Requesting")
					continue;

				CreateFriendItem(friend, ItemMode.Friend);
				count++;
			}

			if (count == 0)
				ShowStatus("No friends yet. Use Discover to add players.");
		}

		private void ShowRequestsList()
		{
			ClearList();
			int count = 0;

			for (int i = 0; i < _allFriends.Count; ++i)
			{
				var friend = _allFriends[i];
				if (GetFriendTag(friend) != "Requesting")
					continue;

				CreateFriendItem(friend, ItemMode.Request);
				count++;
			}

			if (count == 0)
				ShowStatus("No pending requests.");
		}

		private void OnPlayersDiscovered(List<PlayerLeaderboardEntry> players)
		{
			if (CanRunCoroutines() == false)
				return;

			if (_activeTab != Tab.Discover)
				return;

			ClearList();

			if (players == null || players.Count == 0)
			{
				ShowStatus("No new players found.");
				return;
			}

			int created = 0;
			var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			for (int i = 0; i < players.Count; ++i)
			{
				var player = players[i];
				if (player == null || string.IsNullOrEmpty(player.PlayFabId))
					continue;
				if (seen.Add(player.PlayFabId) == false)
					continue;
				if (IsKnownFriendId(player.PlayFabId))
					continue;

				CreateDiscoverItem(player);
				created++;
			}

			if (created == 0)
				ShowStatus("No new players found.");
		}

		private void CreateFriendItem(FriendInfo friend, ItemMode mode)
		{
			if (_friendItemPrefab == null || _friendListContent == null)
				return;

			GameObject item = Instantiate(_friendItemPrefab, _friendListContent);
			item.SetActive(true);

			UIFriendItemView view = item.GetComponent<UIFriendItemView>();
			if (view == null)
				view = item.AddComponent<UIFriendItemView>();
			view.EnsureBindings();

			ApplyItemTheme(view);

			string displayName = GetDisplayName(friend.TitleDisplayName, friend.FriendPlayFabId);
			if (view.NameText != null)
				view.NameText.text = mode == ItemMode.Request ? (displayName + " wants to connect") : displayName;

			if (view.AvatarImage != null)
			{
				string avatarUrl = friend.Profile != null ? friend.Profile.AvatarUrl : string.Empty;
				if (string.IsNullOrEmpty(avatarUrl) == false && CanRunCoroutines())
					StartCoroutine(LoadAvatar(avatarUrl, view.AvatarImage));
			}

			if (mode == ItemMode.Friend && string.IsNullOrEmpty(friend.FriendPlayFabId) == false)
				_friendViewsById[friend.FriendPlayFabId] = view;

			ConfigureRemoveButton(view, BTN_DANGER, "X", () => ConfirmOrRemoveFriend(friend.FriendPlayFabId, view));

			if (mode == ItemMode.Request)
			{
				ConfigureJoinButton(view, BTN_SUCCESS, "ACCEPT", () =>
				{
					PlayFabManager.Instance?.AddFriend(friend.FriendPlayFabId);
					ShowStatus("Friend request accepted.");
				}, true);

				SetStatus(view, false, string.Empty, "Pending", friend.FriendPlayFabId, DateTime.MinValue);
				return;
			}

			ConfigureJoinButton(view, BTN_SUCCESS, "JOIN", null, false);
			SetStatus(view, false, string.Empty, "Checking...", friend.FriendPlayFabId, DateTime.MinValue);
			RefreshFriendStatus(friend.FriendPlayFabId, view);
		}

		private void CreateDiscoverItem(PlayerLeaderboardEntry player)
		{
			if (_friendItemPrefab == null || _friendListContent == null)
				return;
			if (player == null || string.IsNullOrEmpty(player.PlayFabId))
				return;
			if (IsKnownFriendId(player.PlayFabId))
				return;

			GameObject item = Instantiate(_friendItemPrefab, _friendListContent);
			item.SetActive(true);

			UIFriendItemView view = item.GetComponent<UIFriendItemView>();
			if (view == null)
				view = item.AddComponent<UIFriendItemView>();
			view.EnsureBindings();

			ApplyItemTheme(view);

			string displayName = GetDisplayName(player.DisplayName, player.PlayFabId);
			if (view.NameText != null)
				view.NameText.text = displayName;

			if (view.AvatarImage != null)
			{
				string avatarUrl = GetDiscoverAvatarUrl(player);
				string fallbackAvatarUrl = GetDiceBearAvatarUrl(player.PlayFabId);
				if (CanRunCoroutines())
					StartCoroutine(LoadAvatarWithFallback(avatarUrl, fallbackAvatarUrl, view.AvatarImage));
			}

			SetStatus(view, false, string.Empty, "Not Friend", player.PlayFabId, DateTime.MinValue);

			ConfigureDiscoverAddAction(view, item, player.PlayFabId);

			if (view.RemoveButton != null)
				view.RemoveButton.gameObject.SetActive(false);
		}

		private void RefreshFriendStatus(string friendPlayFabId, UIFriendItemView view)
		{
			if (PlayFabManager.Instance == null || view == null || string.IsNullOrEmpty(friendPlayFabId))
				return;

			if (_presenceCache.TryGetValue(friendPlayFabId, out FriendPresence cached) == true)
			{
				if (Time.unscaledTime - cached.CachedAt < PRESENCE_CACHE_SECONDS)
				{
					ApplyPresence(view, friendPlayFabId, cached);
					return;
				}
			}

			PlayFabManager.Instance.GetFriendStatus(friendPlayFabId, (isOnline, sessionName, lastSeen) =>
			{
				if (view == null || view.gameObject == null)
					return;

				var presence = new FriendPresence
				{
					IsOnline = isOnline,
					SessionName = sessionName,
					LastSeen = lastSeen,
					CachedAt = Time.unscaledTime
				};
				_presenceCache[friendPlayFabId] = presence;
				ApplyPresence(view, friendPlayFabId, presence);
			});
		}

		private void ApplyPresence(UIFriendItemView view, string friendPlayFabId, FriendPresence presence)
		{
			string statusLabel;
			bool canJoin = presence.IsOnline && string.IsNullOrEmpty(presence.SessionName) == false;

			if (canJoin)
				statusLabel = "In Game";
			else if (presence.IsOnline)
				statusLabel = "In Menu";
			else
				statusLabel = "Offline";

			SetStatus(view, presence.IsOnline, presence.SessionName, statusLabel, friendPlayFabId, presence.LastSeen);

			ConfigureJoinButton(view, BTN_SUCCESS, "JOIN", () =>
			{
				BeginJoinFriendGame(friendPlayFabId, presence.SessionName, view);
			}, canJoin);
		}

		private void SetStatus(UIFriendItemView view, bool online, string sessionName, string fallbackLabel, string friendPlayFabId, DateTime lastSeen)
		{
			if (view.StatusIndicator != null)
				view.StatusIndicator.color = online ? ONLINE_COLOR : OFFLINE_COLOR;

			if (view.StatusText != null)
			{
				string state = online && string.IsNullOrEmpty(sessionName) == false ? "In Game" : fallbackLabel;
				string details = BuildDetailsLine(friendPlayFabId, online, sessionName, lastSeen);
				view.StatusText.text = string.IsNullOrEmpty(details) ? state : (state + "\n" + details);
			}
		}

		private void ConfigureJoinButton(UIFriendItemView view, Color color, string label, Action onClick, bool visible)
		{
			if (view.JoinButton == null)
				return;

			view.JoinButton.gameObject.SetActive(visible);
			StyleButton(view.JoinButton, color);
			view.JoinButton.onClick.RemoveAllListeners();
			if (visible && onClick != null)
				view.JoinButton.onClick.AddListener(() => onClick.Invoke());

			if (view.JoinButtonText != null)
			{
				view.JoinButtonText.text = label;
				view.JoinButtonText.fontSize = Mathf.Round(15f * GetItemScale());
				view.JoinButtonText.raycastTarget = false;
			}
		}

		private void ConfigureRemoveButton(UIFriendItemView view, Color color, string label, Action onClick)
		{
			if (view.RemoveButton == null)
				return;

			view.RemoveButton.gameObject.SetActive(true);
			StyleButton(view.RemoveButton, color);
			view.RemoveButton.onClick.RemoveAllListeners();
			if (onClick != null)
				view.RemoveButton.onClick.AddListener(() => onClick.Invoke());

			if (view.RemoveButtonText != null)
			{
				view.RemoveButtonText.text = label;
				view.RemoveButtonText.fontSize = Mathf.Round(14f * GetItemScale());
				view.RemoveButtonText.fontStyle = FontStyles.Bold;
				view.RemoveButtonText.raycastTarget = false;
			}
		}

		private void OnAddClicked()
		{
			if (_inputField == null || string.IsNullOrWhiteSpace(_inputField.text))
				return;

			string input = _inputField.text.Trim();
			PlayFabManager.Instance?.AddFriend(input, () =>
			{
				if (_activeTab == Tab.Discover)
					PlayFabManager.Instance?.DiscoverPlayers(10, true);
			});
			_inputField.text = string.Empty;
		}

		private void OnRefreshClicked()
		{
			_presenceCache.Clear();
			_removeConfirmWindow.Clear();
			_discoverAddInProgress.Clear();
			PlayFabManager.Instance?.GetFriends();
			if (_activeTab == Tab.Discover)
				PlayFabManager.Instance?.DiscoverPlayers(10, true);
		}

		private void OnCopyIdClicked()
		{
			if (PlayFabManager.Instance == null)
				return;

			GUIUtility.systemCopyBuffer = PlayFabManager.Instance.MyPlayFabId;
			ShowStatus("ID copied.");
		}

		private void OnMyProfileClicked()
		{
			ShowProfileOverlay();
		}

		private void OnPasteClicked()
		{
			if (_inputField == null)
				return;

			string clipboard = GUIUtility.systemCopyBuffer;
			if (string.IsNullOrEmpty(clipboard))
			{
				ShowStatus("Clipboard is empty.");
				return;
			}

			_inputField.text = clipboard.Trim();
			ShowStatus("Pasted.");
		}
	}
}
#endif

