#if ENABLE_PLAYFAB
namespace TPSBR.UI
{
	using PlayFab.ClientModels;
	using System;
	using System.Collections;
	using TMPro;
	using UnityEngine;
	using UnityEngine.Networking;
	using UnityEngine.UI;

	/// <summary>
	/// Shared helpers: avatar loading, friend tags, layout utilities, status display,
	/// discover-add actions, removal confirmation, and presence auto-refresh.
	/// </summary>
	public partial class UIFriendController
	{
		// ===================== Avatar loading =====================

		private IEnumerator LoadAvatar(string url, RawImage target)
		{
			if (string.IsNullOrWhiteSpace(url) || target == null)
				yield break;

			using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url.Trim()))
			{
				yield return request.SendWebRequest();
				if (request.result != UnityWebRequest.Result.Success)
					yield break;

				if (target == null)
					yield break;

				target.texture = DownloadHandlerTexture.GetContent(request);
				target.color = Color.white;
			}
		}

		private IEnumerator LoadAvatarWithFallback(string primaryUrl, string fallbackUrl, RawImage target)
		{
			if (target == null)
				yield break;

			bool loaded = false;
			if (string.IsNullOrWhiteSpace(primaryUrl) == false)
			{
				using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(primaryUrl.Trim()))
				{
					yield return request.SendWebRequest();
					if (request.result == UnityWebRequest.Result.Success && target != null)
					{
						target.texture = DownloadHandlerTexture.GetContent(request);
						target.color = Color.white;
						loaded = true;
					}
				}
			}

			if (loaded || string.IsNullOrWhiteSpace(fallbackUrl))
				yield break;
			if (string.Equals(primaryUrl, fallbackUrl, StringComparison.OrdinalIgnoreCase))
				yield break;

			using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(fallbackUrl.Trim()))
			{
				yield return request.SendWebRequest();
				if (request.result != UnityWebRequest.Result.Success || target == null)
					yield break;

				target.texture = DownloadHandlerTexture.GetContent(request);
				target.color = Color.white;
			}
		}

		private static string GetDiscoverAvatarUrl(PlayerLeaderboardEntry player)
		{
			if (player == null)
				return string.Empty;

			if (player.Profile != null && string.IsNullOrWhiteSpace(player.Profile.AvatarUrl) == false)
				return NormalizeAvatarUrl(player.Profile.AvatarUrl, player.PlayFabId);

			return GetDiceBearAvatarUrl(player.PlayFabId);
		}

		private static string GetDiceBearAvatarUrl(string seed)
		{
			if (string.IsNullOrWhiteSpace(seed))
				return string.Empty;

			return "https://api.dicebear.com/7.x/bottts-neutral/png?seed=" + UnityWebRequest.EscapeURL(seed.Trim());
		}

		private static string NormalizeAvatarUrl(string url, string fallbackSeed)
		{
			if (string.IsNullOrWhiteSpace(url))
				return GetDiceBearAvatarUrl(fallbackSeed);

			string normalized = url.Trim();
			if (normalized.IndexOf("api.dicebear.com", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				if (normalized.IndexOf("/svg?", StringComparison.OrdinalIgnoreCase) >= 0)
					normalized = normalized.Replace("/svg?", "/png?");
				else if (normalized.EndsWith("/svg", StringComparison.OrdinalIgnoreCase))
					normalized = normalized.Substring(0, normalized.Length - 4) + "/png";
				else if (normalized.IndexOf("/png", StringComparison.OrdinalIgnoreCase) < 0)
				{
					int queryIndex = normalized.IndexOf('?');
					if (queryIndex >= 0)
						normalized = normalized.Insert(queryIndex, "/png");
					else
						normalized += "/png";
				}
			}

			return normalized;
		}

		// ===================== Friend tags & display =====================

		private string GetFriendTag(FriendInfo friend)
		{
			if (friend == null || friend.Tags == null || friend.Tags.Count == 0)
				return "Confirmed";

			for (int i = 0; i < friend.Tags.Count; ++i)
			{
				string tag = friend.Tags[i].ToLowerInvariant();
				if (tag.Contains("requesting"))
					return "Requesting";
				if (tag.Contains("requested"))
					return "Requested";
				if (tag.Contains("confirmed"))
					return "Confirmed";
			}

			return "Confirmed";
		}

		private static string GetDisplayName(string displayName, string fallbackId)
		{
			if (string.IsNullOrEmpty(displayName) == false)
				return displayName;

			if (string.IsNullOrEmpty(fallbackId))
				return "Player";

			return fallbackId.Length <= 8 ? fallbackId : fallbackId.Substring(0, 8);
		}

		private static string BuildDetailsLine(string friendPlayFabId, bool isOnline, string sessionName, DateTime lastSeen)
		{
			string shortId = ShortId(friendPlayFabId);

			if (isOnline && string.IsNullOrEmpty(sessionName) == false)
			{
				string room = sessionName.Length > 16 ? sessionName.Substring(0, 16) + "..." : sessionName;
				return $"ID: {shortId}  Room: {room}";
			}

			if (isOnline)
				return $"ID: {shortId}  In menu";

			if (lastSeen > DateTime.MinValue)
				return $"ID: {shortId}  Last seen: {FormatTimeAgo(lastSeen)}";

			return $"ID: {shortId}";
		}

		private static string ShortId(string playFabId)
		{
			if (string.IsNullOrEmpty(playFabId))
				return "-";

			return playFabId.Length <= 12 ? playFabId : playFabId.Substring(0, 12);
		}

		private static string FormatTimeAgo(DateTime utcTime)
		{
			if (utcTime <= DateTime.MinValue)
				return "unknown";

			var delta = DateTime.UtcNow - utcTime;
			if (delta.TotalSeconds < 60)
				return "just now";
			if (delta.TotalMinutes < 60)
				return Mathf.RoundToInt((float)delta.TotalMinutes) + "m ago";
			if (delta.TotalHours < 24)
				return Mathf.RoundToInt((float)delta.TotalHours) + "h ago";
			return Mathf.RoundToInt((float)delta.TotalDays) + "d ago";
		}

		// ===================== Known friend check =====================

		private bool IsKnownFriendId(string playFabId)
		{
			if (string.IsNullOrEmpty(playFabId))
				return false;

			for (int i = 0; i < _allFriends.Count; ++i)
			{
				var friend = _allFriends[i];
				if (friend == null || string.IsNullOrEmpty(friend.FriendPlayFabId))
					continue;
				if (string.Equals(friend.FriendPlayFabId, playFabId, StringComparison.OrdinalIgnoreCase))
					return true;
			}

			var manager = PlayFabManager.Instance;
			if (manager != null && manager.CachedFriends != null)
			{
				for (int i = 0; i < manager.CachedFriends.Count; ++i)
				{
					var friend = manager.CachedFriends[i];
					if (friend == null || string.IsNullOrEmpty(friend.FriendPlayFabId))
						continue;
					if (string.Equals(friend.FriendPlayFabId, playFabId, StringComparison.OrdinalIgnoreCase))
						return true;
				}
			}

			return false;
		}

		// ===================== Discover add / remove =====================

		private void ConfigureDiscoverAddAction(UIFriendItemView view, GameObject item, string playFabId)
		{
			ConfigureJoinButton(view, BTN_SUCCESS, "ADD", () =>
			{
				if (_discoverAddInProgress.Contains(playFabId))
					return;

				_discoverAddInProgress.Add(playFabId);
				ConfigureJoinButton(view, BTN_PRIMARY, "ADDING...", null, true);

				PlayFabManager.Instance?.AddFriend(playFabId, () =>
				{
					_discoverAddInProgress.Remove(playFabId);
					if (item != null)
						Destroy(item);
				});

				if (CanRunCoroutines())
					StartCoroutine(ResetDiscoverAddStateAfterDelay(playFabId, view, item, 8f));
			}, true);
		}

		private IEnumerator ResetDiscoverAddStateAfterDelay(string playFabId, UIFriendItemView view, GameObject item, float delay)
		{
			yield return new WaitForSecondsRealtime(delay);

			if (_discoverAddInProgress.Remove(playFabId) == false)
				yield break;

			if (view == null || view.gameObject == null)
				yield break;

			ConfigureDiscoverAddAction(view, item, playFabId);
		}

		private void ConfirmOrRemoveFriend(string friendPlayFabId, UIFriendItemView view)
		{
			if (string.IsNullOrEmpty(friendPlayFabId))
				return;

			float now = Time.unscaledTime;
			if (_removeConfirmWindow.TryGetValue(friendPlayFabId, out float validUntil) && now <= validUntil)
			{
				_removeConfirmWindow.Remove(friendPlayFabId);
				PlayFabManager.Instance?.RemoveFriend(friendPlayFabId);
				return;
			}

			_removeConfirmWindow[friendPlayFabId] = now + REMOVE_CONFIRM_SECONDS;
			ConfigureRemoveButton(view, BTN_WARNING, "OK", () => ConfirmOrRemoveFriend(friendPlayFabId, view));
			ShowStatus("Press remove again within 3 seconds to confirm.");
			StartCoroutine(ResetRemoveButtonAfterDelay(friendPlayFabId, view, REMOVE_CONFIRM_SECONDS));
		}

		private IEnumerator ResetRemoveButtonAfterDelay(string friendPlayFabId, UIFriendItemView view, float delay)
		{
			yield return new WaitForSecondsRealtime(delay);

			if (_removeConfirmWindow.TryGetValue(friendPlayFabId, out float validUntil) == false)
				yield break;

			if (Time.unscaledTime < validUntil)
				yield break;

			_removeConfirmWindow.Remove(friendPlayFabId);
			if (view != null && view.gameObject != null)
				ConfigureRemoveButton(view, BTN_DANGER, "X", () => ConfirmOrRemoveFriend(friendPlayFabId, view));
		}

		// ===================== List & status helpers =====================

		private void ClearList()
		{
			if (_friendListContent == null)
				return;

			_friendViewsById.Clear();

			for (int i = _friendListContent.childCount - 1; i >= 0; --i)
				Destroy(_friendListContent.GetChild(i).gameObject);
		}

		private void ShowStatus(string message)
		{
			if (_statusText == null)
				return;

			if (CanRunCoroutines() == false)
				return;

			_statusText.text = message;

			if (_statusCoroutine != null)
				StopCoroutine(_statusCoroutine);

			if (string.IsNullOrEmpty(message) == false)
				_statusCoroutine = StartCoroutine(HideStatusAfterDelay(3f));
		}

		private IEnumerator HideStatusAfterDelay(float delay)
		{
			yield return new WaitForSeconds(delay);
			if (_statusText != null)
				_statusText.text = string.Empty;
			_statusCoroutine = null;
		}

		// ===================== Presence auto-refresh =====================

		private void TickPresenceAutoRefresh()
		{
			if (_activeTab != Tab.Friends)
				return;
			if (_allFriends.Count == 0 || _friendViewsById.Count == 0)
				return;

			_presencePollTimer += Time.unscaledDeltaTime;
			if (_presencePollTimer < PRESENCE_POLL_INTERVAL)
				return;

			_presencePollTimer = 0f;

			int total = _allFriends.Count;
			int scanned = 0;
			int refreshed = 0;

			while (scanned < total && refreshed < PRESENCE_POLL_BATCH_SIZE)
			{
				int index = total > 0 ? (_presencePollIndex % total) : 0;
				_presencePollIndex = total > 0 ? (_presencePollIndex + 1) % total : 0;
				scanned++;

				FriendInfo friend = _allFriends[index];
				if (friend == null || string.IsNullOrEmpty(friend.FriendPlayFabId))
					continue;
				if (GetFriendTag(friend) == "Requesting")
					continue;

				if (_friendViewsById.TryGetValue(friend.FriendPlayFabId, out UIFriendItemView view) == false)
					continue;
				if (view == null || view.gameObject == null)
				{
					_friendViewsById.Remove(friend.FriendPlayFabId);
					continue;
				}

				RefreshFriendStatus(friend.FriendPlayFabId, view);
				refreshed++;
			}
		}

		// ===================== Layout helpers =====================

		private void EnsureListLayout()
		{
			if (_friendListContent == null)
				return;

			float scale = GetUiScale();

			var vlg = _friendListContent.GetComponent<VerticalLayoutGroup>();
			if (vlg == null)
				vlg = _friendListContent.gameObject.AddComponent<VerticalLayoutGroup>();

			int sidePadding = Mathf.RoundToInt(6f * scale);
			int verticalPadding = Mathf.RoundToInt(8f * scale);
			vlg.spacing = Mathf.Round(10f * scale);
			vlg.padding = new RectOffset(sidePadding, sidePadding, verticalPadding, verticalPadding);
			vlg.childControlWidth = true;
			vlg.childForceExpandWidth = true;
			vlg.childControlHeight = false;
			vlg.childForceExpandHeight = false;

			var fitter = _friendListContent.GetComponent<ContentSizeFitter>();
			if (fitter == null)
				fitter = _friendListContent.gameObject.AddComponent<ContentSizeFitter>();
			fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
			fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
		}

		private void StyleButton(Button button, Color normalColor)
		{
			if (button == null)
				return;

			var image = button.GetComponent<Image>();
			if (image != null)
				image.color = normalColor;

			var colors = button.colors;
			colors.normalColor = normalColor;
			colors.highlightedColor = Color.Lerp(normalColor, Color.white, 0.18f);
			colors.pressedColor = Color.Lerp(normalColor, Color.black, 0.15f);
			colors.selectedColor = colors.highlightedColor;
			colors.disabledColor = new Color(0.35f, 0.35f, 0.35f, 0.8f);
			button.colors = colors;
		}

		private static void ApplyPanelFrame(RectTransform panelRect, float scale)
		{
			if (panelRect == null) return;
			var panelObject = panelRect.gameObject;
			var outline = EnsureOutline(panelObject);
			outline.effectColor = new Color(0.28f, 0.62f, 0.88f, 0.34f);
			outline.effectDistance = new Vector2(1f, -1f) * Mathf.Clamp(scale, 0.8f, 1.3f);
			var shadow = EnsureShadow(panelObject);
			shadow.effectColor = new Color(0f, 0f, 0f, 0.62f);
			shadow.effectDistance = new Vector2(0f, -5f) * Mathf.Clamp(scale, 0.8f, 1.3f);
		}

		private static void ApplyCardFrame(GameObject cardObject, float scale)
		{
			if (cardObject == null) return;
			var outline = EnsureOutline(cardObject);
			outline.effectColor = new Color(0.25f, 0.54f, 0.80f, 0.24f);
			outline.effectDistance = new Vector2(1f, -1f) * Mathf.Clamp(scale, 0.8f, 1.3f);
			var shadow = EnsureShadow(cardObject);
			shadow.effectColor = new Color(0f, 0f, 0f, 0.56f);
			shadow.effectDistance = new Vector2(0f, -3f) * Mathf.Clamp(scale, 0.8f, 1.3f);
		}

		private static void ApplyAvatarFrame(GameObject avatarObject, float scale)
		{
			if (avatarObject == null) return;
			var outline = EnsureOutline(avatarObject);
			outline.effectColor = new Color(0.30f, 0.76f, 0.95f, 0.78f);
			outline.effectDistance = new Vector2(1f, -1f) * Mathf.Clamp(scale, 0.8f, 1.3f);
		}

		private static Outline EnsureOutline(GameObject go)
		{
			var outline = go.GetComponent<Outline>();
			if (outline == null) outline = go.AddComponent<Outline>();
			outline.useGraphicAlpha = true;
			return outline;
		}

		private static Shadow EnsureShadow(GameObject go)
		{
			Shadow shadow = null;
			var shadows = go.GetComponents<Shadow>();
			for (int i = 0; i < shadows.Length; ++i)
			{
				if (shadows[i] != null && shadows[i].GetType() == typeof(Shadow))
				{ shadow = shadows[i]; break; }
			}
			if (shadow == null) shadow = go.AddComponent<Shadow>();
			shadow.useGraphicAlpha = true;
			return shadow;
		}

		private float GetUiScale()
		{
			var rect = transform as RectTransform;
			float width = rect != null ? rect.rect.width : 0f;
			if (width <= 0f) width = Screen.width;
			return Mathf.Clamp(width / 1300f, 0.9f, 1.28f);
		}

		private float GetItemScale()
		{
			return Mathf.Clamp(GetUiScale(), 0.92f, 1.32f);
		}

		private void EnsureItemStructure(UIFriendItemView view, float itemScale)
		{
			if (view == null) return;

			int index = 0;
			if (view.StatusIndicator != null) view.StatusIndicator.transform.SetSiblingIndex(index++);
			if (view.AvatarImage != null) view.AvatarImage.transform.SetSiblingIndex(index++);

			Transform textContainer = view.NameText != null ? view.NameText.transform.parent : null;
			if (textContainer != null)
			{
				textContainer.SetSiblingIndex(index++);
				var textLayout = textContainer.GetComponent<LayoutElement>();
				if (textLayout == null) textLayout = textContainer.gameObject.AddComponent<LayoutElement>();
				textLayout.minWidth = Mathf.Round(240f * itemScale);
				textLayout.flexibleWidth = 1f;

				var vertical = textContainer.GetComponent<VerticalLayoutGroup>();
				if (vertical == null) vertical = textContainer.gameObject.AddComponent<VerticalLayoutGroup>();
				vertical.spacing = Mathf.RoundToInt(2f * itemScale);
				vertical.childAlignment = TextAnchor.MiddleLeft;
				vertical.childControlWidth = true;
				vertical.childControlHeight = true;
				vertical.childForceExpandWidth = true;
				vertical.childForceExpandHeight = false;
			}

			if (view.JoinButton != null) view.JoinButton.transform.SetSiblingIndex(index++);
			if (view.RemoveButton != null) view.RemoveButton.transform.SetSiblingIndex(index++);

			SetFixedWidth(view.StatusIndicator != null ? view.StatusIndicator.transform : null, Mathf.Round(20f * itemScale));
			SetFixedWidth(view.AvatarImage != null ? view.AvatarImage.transform : null, Mathf.Round(64f * itemScale));
			SetFixedWidth(view.JoinButton != null ? view.JoinButton.transform : null, Mathf.Round(122f * itemScale));
			SetFixedWidth(view.RemoveButton != null ? view.RemoveButton.transform : null, Mathf.Round(54f * itemScale));

			if (view.NameText != null)
			{
				view.NameText.color = Color.white;
				var nameLayout = view.NameText.GetComponent<LayoutElement>();
				if (nameLayout == null) nameLayout = view.NameText.gameObject.AddComponent<LayoutElement>();
				nameLayout.minHeight = Mathf.Round(28f * itemScale);
				nameLayout.preferredHeight = Mathf.Round(30f * itemScale);
				nameLayout.flexibleHeight = 0f;
			}

			if (view.StatusText != null)
			{
				var statusLayout = view.StatusText.GetComponent<LayoutElement>();
				if (statusLayout == null) statusLayout = view.StatusText.gameObject.AddComponent<LayoutElement>();
				statusLayout.minHeight = Mathf.Round(38f * itemScale);
				statusLayout.preferredHeight = Mathf.Round(40f * itemScale);
				statusLayout.flexibleHeight = 0f;
			}
		}

		private static void SetFixedWidth(Transform target, float width)
		{
			if (target == null) return;
			var layout = target.GetComponent<LayoutElement>();
			if (layout == null) layout = target.gameObject.AddComponent<LayoutElement>();
			layout.minWidth = width;
			layout.preferredWidth = width;
			layout.flexibleWidth = 0f;
		}

		private static RectTransform CreateRect(string name, RectTransform parent)
		{
			var go = new GameObject(name, typeof(RectTransform));
			go.layer = parent.gameObject.layer;
			var rect = go.GetComponent<RectTransform>();
			rect.SetParent(parent, false);
			return rect;
		}

		private RectTransform GetOptionalRect(string primary, string secondary)
		{
			var t = transform.Find(primary);
			if (t == null) t = transform.Find(secondary);
			return t as RectTransform;
		}

		private static void SetTopStretch(RectTransform rect, float minX, float maxX, float top, float height)
		{
			if (rect == null) return;
			rect.anchorMin = new Vector2(minX, 1f);
			rect.anchorMax = new Vector2(maxX, 1f);
			rect.pivot = new Vector2(0.5f, 1f);
			rect.offsetMin = new Vector2(4f, -(top + height));
			rect.offsetMax = new Vector2(-4f, -top);
		}

		private static void SetStretch(RectTransform rect, float minX, float maxX, float minY, float maxY)
		{
			if (rect == null) return;
			rect.anchorMin = new Vector2(minX, minY);
			rect.anchorMax = new Vector2(maxX, maxY);
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;
		}

		private static void ApplyTextStyle(TMP_Text text, float size, FontStyles style, Color color)
		{
			if (text == null) return;
			text.fontSize = size;
			text.fontStyle = style;
			text.color = color;
		}

		private static void SetBottomStretch(RectTransform rect, float minX, float maxX, float bottom, float height)
		{
			if (rect == null) return;
			rect.anchorMin = new Vector2(minX, 0f);
			rect.anchorMax = new Vector2(maxX, 0f);
			rect.pivot = new Vector2(0.5f, 0f);
			rect.offsetMin = new Vector2(4f, bottom);
			rect.offsetMax = new Vector2(-4f, bottom + height);
		}

		private static void SetTopFixed(RectTransform rect, float left, float top, float width, float height)
		{
			if (rect == null) return;
			rect.anchorMin = new Vector2(0f, 1f);
			rect.anchorMax = new Vector2(0f, 1f);
			rect.pivot = new Vector2(0f, 1f);
			rect.anchoredPosition = new Vector2(left, -top);
			rect.sizeDelta = new Vector2(width, height);
		}
	}
}
#endif
