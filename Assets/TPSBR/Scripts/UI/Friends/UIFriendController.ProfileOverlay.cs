#if ENABLE_PLAYFAB
namespace TPSBR.UI
{
	using System;
	using System.Collections;
	using TMPro;
	using UnityEngine;
	using UnityEngine.Networking;
	using UnityEngine.UI;

	/// <summary>
	/// Profile overlay: create/show/hide the "My Profile" popup and handle its buttons.
	/// </summary>
	public partial class UIFriendController
	{
		private void EnsureMyProfileButton()
		{
			if (_myProfileButton != null)
				return;

			var root = transform as RectTransform;
			if (root == null)
				return;

			var buttonObject = new GameObject("MyProfileButton", typeof(RectTransform), typeof(Image), typeof(UIButton));
			buttonObject.layer = gameObject.layer;

			var buttonRect = buttonObject.GetComponent<RectTransform>();
			buttonRect.SetParent(root, false);
			buttonRect.anchorMin = new Vector2(0f, 1f);
			buttonRect.anchorMax = new Vector2(0f, 1f);
			buttonRect.pivot = new Vector2(0f, 1f);
			buttonRect.anchoredPosition = new Vector2(0f, 0f);
			buttonRect.sizeDelta = new Vector2(180f, 30f);

			var image = buttonObject.GetComponent<Image>();
			var button = buttonObject.GetComponent<UIButton>();
			button.targetGraphic = image;
			button.transition = Selectable.Transition.ColorTint;
			button.navigation = new Navigation { mode = Navigation.Mode.None };

			if (_copyIdButton != null)
			{
				var copyImage = _copyIdButton.GetComponent<Image>();
				if (copyImage != null)
				{
					image.sprite = copyImage.sprite;
					image.type = copyImage.type;
					image.material = copyImage.material;
				}

				button.colors = _copyIdButton.colors;
			}

			var textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
			textObject.layer = buttonObject.layer;
			var textRect = textObject.GetComponent<RectTransform>();
			textRect.SetParent(buttonRect, false);
			textRect.anchorMin = Vector2.zero;
			textRect.anchorMax = Vector2.one;
			textRect.offsetMin = Vector2.zero;
			textRect.offsetMax = Vector2.zero;

			var text = textObject.GetComponent<TextMeshProUGUI>();
			text.text = "MY PROFILE";
			text.alignment = TextAlignmentOptions.Center;
			text.fontSize = 15f;
			text.fontStyle = FontStyles.Bold;
			text.color = Color.white;
			text.raycastTarget = false;

			if (_copyIdButton != null)
			{
				var copyText = _copyIdButton.GetComponentInChildren<TMP_Text>(true);
				if (copyText != null)
					text.font = copyText.font;
			}

			_myProfileButton = button;
		}

		private void EnsureProfileOverlay()
		{
			if (_profileOverlayRoot != null)
				return;

			var root = transform as RectTransform;
			if (root == null)
				return;

			_profileOverlayRoot = CreateRect("MyProfileOverlay", root);
			_profileOverlayRoot.anchorMin = Vector2.zero;
			_profileOverlayRoot.anchorMax = Vector2.one;
			_profileOverlayRoot.offsetMin = Vector2.zero;
			_profileOverlayRoot.offsetMax = Vector2.zero;
			_profileOverlayRoot.SetAsLastSibling();

			var overlayImage = _profileOverlayRoot.gameObject.AddComponent<Image>();
			overlayImage.color = new Color(0f, 0f, 0f, 0.62f);

			var overlayButton = _profileOverlayRoot.gameObject.AddComponent<Button>();
			overlayButton.targetGraphic = overlayImage;
			overlayButton.transition = Selectable.Transition.None;

			_profileCardRect = CreateRect("Card", _profileOverlayRoot);
			_profileCardRect.anchorMin = new Vector2(0.5f, 0.5f);
			_profileCardRect.anchorMax = new Vector2(0.5f, 0.5f);
			_profileCardRect.pivot = new Vector2(0.5f, 0.5f);
			_profileCardRect.sizeDelta = new Vector2(680f, 390f);

			var cardImage = _profileCardRect.gameObject.AddComponent<Image>();
			cardImage.color = new Color(0.06f, 0.10f, 0.16f, 0.98f);
			ApplyCardFrame(_profileCardRect.gameObject, 1f);

			_profileCloseButton = CreateOverlayButton(_profileCardRect, "CloseButton", "X");
			_profileCopyButton = CreateOverlayButton(_profileCardRect, "CopyIdButton", "COPY ID");
			_profileSyncNameButton = CreateOverlayButton(_profileCardRect, "SyncNameButton", "SYNC NAME");
			_profileNewAvatarButton = CreateOverlayButton(_profileCardRect, "NewAvatarButton", "NEW AVATAR");
			_profileRefreshButton = CreateOverlayButton(_profileCardRect, "RefreshButton", "REFRESH");

			_profileAvatarImage = CreateRect("Avatar", _profileCardRect).gameObject.AddComponent<RawImage>();
			_profileAvatarImage.color = new Color(0.58f, 0.70f, 0.83f, 0.36f);
			ApplyAvatarFrame(_profileAvatarImage.gameObject, 1f);

			_profileTitleText = CreateOverlayText(_profileCardRect, "Title", "MY PROFILE", 32f, FontStyles.Bold);
			_profileInfoText = CreateOverlayText(_profileCardRect, "Info", string.Empty, 20f, FontStyles.Normal);
			_profileHintText = CreateOverlayText(_profileCardRect, "Hint", string.Empty, 16f, FontStyles.Italic);
			_profileHintText.color = new Color(0.72f, 0.86f, 1f, 1f);

			_profileCloseButton.onClick.AddListener(HideProfileOverlay);
			_profileCopyButton.onClick.AddListener(OnProfileCopyIdClicked);
			_profileSyncNameButton.onClick.AddListener(OnProfileSyncNameClicked);
			_profileNewAvatarButton.onClick.AddListener(OnProfileNewAvatarClicked);
			_profileRefreshButton.onClick.AddListener(OnProfileRefreshClicked);

			_profileOverlayRoot.gameObject.SetActive(false);
		}

		private void ShowProfileOverlay()
		{
			EnsureProfileOverlay();
			if (_profileOverlayRoot == null)
			{
				ShowStatus("Profile UI failed to initialize.");
				return;
			}

			_profileOverlayRoot.gameObject.SetActive(true);
			_profileOverlayRoot.SetAsLastSibling();
			RefreshProfileOverlay(true);
		}

		private void HideProfileOverlay()
		{
			if (_profileOverlayRoot == null)
				return;

			_profileOverlayRoot.gameObject.SetActive(false);
		}

		private void OnProfileCopyIdClicked()
		{
			if (PlayFabManager.Instance == null || string.IsNullOrEmpty(PlayFabManager.Instance.MyPlayFabId))
			{
				SetProfileHint("PlayFab is not ready.");
				return;
			}

			GUIUtility.systemCopyBuffer = PlayFabManager.Instance.MyPlayFabId;
			SetProfileHint("ID copied.");
		}

		private void OnProfileSyncNameClicked()
		{
			if (PlayFabManager.Instance == null)
			{
				SetProfileHint("PlayFab is not ready.");
				return;
			}

			string nickname = GetLocalNickname();
			if (string.IsNullOrWhiteSpace(nickname))
			{
				SetProfileHint("Set nickname first.");
				return;
			}

			nickname = nickname.Trim();
			if (nickname.Length < 2)
			{
				SetProfileHint("Nickname must be at least 2 chars.");
				return;
			}

			if (nickname.Length > 25)
				nickname = nickname.Substring(0, 25);

			PlayFabManager.Instance.SetDisplayName(nickname);
			RefreshMyHeader();

			SetProfileHint("Display name synced.");
			RefreshProfileOverlay(false);
		}

		private void OnProfileNewAvatarClicked()
		{
			if (PlayFabManager.Instance == null || string.IsNullOrEmpty(PlayFabManager.Instance.MyPlayFabId))
			{
				SetProfileHint("PlayFab is not ready.");
				return;
			}

			string seed = PlayFabManager.Instance.MyPlayFabId + "_" + UnityEngine.Random.Range(1000, 9999);
			string avatarUrl = "https://api.dicebear.com/7.x/bottts-neutral/png?seed=" + UnityWebRequest.EscapeURL(seed);
			PlayFabManager.Instance.SetAvatarUrl(avatarUrl);
			RefreshMyHeader();
			SetProfileHint("Avatar updated.");
			RefreshProfileOverlay(true);
		}

		private void OnProfileRefreshClicked()
		{
			PlayFabManager.Instance?.UpdateOnlineStatus();
			RefreshProfileOverlay(true);
			SetProfileHint("Profile refreshed.");
		}

		private void RefreshProfileOverlay(bool forceAvatarRefresh)
		{
			if (_profileOverlayRoot == null || _profileOverlayRoot.gameObject.activeSelf == false)
				return;

			var manager = PlayFabManager.Instance;
			if (manager == null)
			{
				if (_profileInfoText != null)
					_profileInfoText.text = "PlayFab unavailable.";
				return;
			}

			string displayName = string.IsNullOrEmpty(manager.MyDisplayName) ? "-" : manager.MyDisplayName;
			string nickname = GetLocalNickname();
			if (string.IsNullOrEmpty(nickname))
				nickname = "-";
			string playFabId = string.IsNullOrEmpty(manager.MyPlayFabId) ? "-" : manager.MyPlayFabId;
			string status = manager.IsInGame ? "In Game" : "In Menu";
			string session = string.IsNullOrEmpty(manager.CurrentSessionName) ? "-" : manager.CurrentSessionName;
			string avatarProvider = GetAvatarProvider(manager.MyAvatarUrl);

			if (_profileTitleText != null)
				_profileTitleText.text = displayName;

			if (_profileInfoText != null)
			{
				_profileInfoText.text =
					"Nickname: " + nickname + "\n" +
					"PlayFab ID: " + playFabId + "\n" +
					"Status: " + status + "\n" +
					"Session: " + session + "\n" +
					"Avatar Provider: " + avatarProvider;
			}

			if (forceAvatarRefresh)
				_loadedProfileAvatarUrl = string.Empty;

			if (string.IsNullOrEmpty(manager.MyAvatarUrl) == false)
				LoadProfileAvatar(manager.MyAvatarUrl);
		}

		private void LoadProfileAvatar(string url)
		{
			if (_profileAvatarImage == null || string.IsNullOrEmpty(url))
				return;

			if (_loadedProfileAvatarUrl == url)
				return;

			if (_profileAvatarCoroutine != null)
			{
				StopCoroutine(_profileAvatarCoroutine);
				_profileAvatarCoroutine = null;
			}

			_profileAvatarCoroutine = StartCoroutine(LoadProfileAvatarCoroutine(url));
		}

		private IEnumerator LoadProfileAvatarCoroutine(string url)
		{
			using (var request = UnityWebRequestTexture.GetTexture(url))
			{
				yield return request.SendWebRequest();
				if (request.result != UnityWebRequest.Result.Success)
				{
					SetProfileHint("Avatar load failed.");
					_profileAvatarCoroutine = null;
					yield break;
				}

				if (_profileAvatarImage == null)
				{
					_profileAvatarCoroutine = null;
					yield break;
				}

				ReleaseProfileAvatarTexture();
				_profileAvatarTexture = DownloadHandlerTexture.GetContent(request);
				_profileAvatarImage.texture = _profileAvatarTexture;
				_profileAvatarImage.color = Color.white;
				_loadedProfileAvatarUrl = url;
			}

			_profileAvatarCoroutine = null;
		}

		private void ReleaseProfileAvatarTexture()
		{
			if (_profileAvatarTexture == null)
				return;

			Destroy(_profileAvatarTexture);
			_profileAvatarTexture = null;
		}

		private void SetProfileHint(string message)
		{
			if (_profileHintText == null)
				return;

			_profileHintText.text = message;

			if (_profileHintCoroutine != null)
				StopCoroutine(_profileHintCoroutine);
			_profileHintCoroutine = StartCoroutine(ClearProfileHintAfterDelay(2.5f));
		}

		private IEnumerator ClearProfileHintAfterDelay(float delay)
		{
			yield return new WaitForSecondsRealtime(delay);
			if (_profileHintText != null)
				_profileHintText.text = string.Empty;
			_profileHintCoroutine = null;
		}

		private static string GetAvatarProvider(string url)
		{
			if (string.IsNullOrEmpty(url))
				return "-";

			if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri) == false)
				return "custom";

			return uri.Host;
		}

		private Button CreateOverlayButton(RectTransform parent, string name, string label)
		{
			var rect = CreateRect(name, parent);
			var image = rect.gameObject.AddComponent<Image>();
			var button = rect.gameObject.AddComponent<Button>();
			button.targetGraphic = image;
			button.transition = Selectable.Transition.ColorTint;
			button.navigation = new Navigation { mode = Navigation.Mode.None };

			var text = CreateOverlayText(rect, "Text", label, 18f, FontStyles.Bold);
			text.alignment = TextAlignmentOptions.Center;
			var textRect = text.transform as RectTransform;
			textRect.anchorMin = Vector2.zero;
			textRect.anchorMax = Vector2.one;
			textRect.offsetMin = Vector2.zero;
			textRect.offsetMax = Vector2.zero;

			return button;
		}

		private TMP_Text CreateOverlayText(RectTransform parent, string name, string value, float fontSize, FontStyles style)
		{
			var rect = CreateRect(name, parent);
			var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
			text.text = value;
			text.fontSize = fontSize;
			text.fontStyle = style;
			text.color = Color.white;
			text.alignment = TextAlignmentOptions.MidlineLeft;
			text.raycastTarget = false;

			if (_myNameText != null)
				text.font = _myNameText.font;

			return text;
		}
	}
}
#endif
