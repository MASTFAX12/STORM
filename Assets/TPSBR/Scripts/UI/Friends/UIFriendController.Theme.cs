#if ENABLE_PLAYFAB
namespace TPSBR.UI
{
	using TMPro;
	using UnityEngine;
	using UnityEngine.UI;

	/// <summary>
	/// Theme, responsive layout, and item theming logic.
	/// Shared utility methods (StyleButton, ApplyCardFrame, etc.) live in Helpers.
	/// </summary>
	public partial class UIFriendController
	{
		private void ApplyResponsiveLayoutAndTheme()
		{
			var panelRect = GetComponent<RectTransform>();
			if (panelRect == null)
				return;

			float scale = GetUiScale();

			panelRect.anchorMin = new Vector2(0.03f, 0.04f);
			panelRect.anchorMax = new Vector2(0.97f, 0.92f);
			panelRect.offsetMin = Vector2.zero;
			panelRect.offsetMax = Vector2.zero;

			var panelImage = GetComponent<Image>();
			if (panelImage != null)
				panelImage.color = PANEL_BG;
			ApplyPanelFrame(panelRect, scale);

			float profileTop = Mathf.Round(16f * scale);
			float avatarSize = Mathf.Round(96f * scale);
			float nameHeight = Mathf.Round(38f * scale);
			float idHeight = Mathf.Round(26f * scale);
			float rowGap = Mathf.Round(4f * scale);
			float tabsTop = profileTop + avatarSize + Mathf.Round(10f * scale);
			float tabHeight = Mathf.Round(52f * scale);
			float inputTop = tabsTop + tabHeight + Mathf.Round(10f * scale);
			float inputHeight = Mathf.Round(46f * scale);
			float myProfileHeight = idHeight + Mathf.Round(8f * scale);

			SetTopFixed(transform.Find("MyAvatarImage") as RectTransform, Mathf.Round(18f * scale), profileTop, avatarSize, avatarSize);
			SetTopStretch(transform.Find("MyNameText") as RectTransform, 0.14f, 0.58f, profileTop, nameHeight);
			SetTopStretch(transform.Find("MyIdText") as RectTransform, 0.14f, 0.52f, profileTop + nameHeight + rowGap, idHeight);
			SetTopStretch(transform.Find("CopyIdButton") as RectTransform, 0.53f, 0.66f, profileTop + nameHeight + rowGap, idHeight);
			SetTopStretch(transform.Find("MyProfileButton") as RectTransform, 0.67f, 0.92f, profileTop + nameHeight + rowGap - Mathf.Round(4f * scale), myProfileHeight);

			bool showRequests = _requestsTabButton != null && _requestsTabButton.gameObject.activeSelf;
			if (showRequests)
			{
				SetTopStretch(transform.Find("FriendsTab") as RectTransform, 0f, 0.33f, tabsTop, tabHeight);
				SetTopStretch(transform.Find("RequestsTab") as RectTransform, 0.34f, 0.66f, tabsTop, tabHeight);
				SetTopStretch(transform.Find("DiscoverTab") as RectTransform, 0.67f, 1f, tabsTop, tabHeight);
			}
			else
			{
				SetTopStretch(transform.Find("FriendsTab") as RectTransform, 0f, 0.49f, tabsTop, tabHeight);
				SetTopStretch(transform.Find("DiscoverTab") as RectTransform, 0.51f, 1f, tabsTop, tabHeight);
			}

			SetTopStretch(transform.Find("FriendInput") as RectTransform, 0f, 0.68f, inputTop, inputHeight);
			SetTopStretch(GetOptionalRect("Paste Button", "PasteButton"), 0.69f, 0.83f, inputTop, inputHeight);
			SetTopStretch(transform.Find("AddButton") as RectTransform, 0.84f, 1f, inputTop, inputHeight);

			RectTransform listRect = transform.Find("FriendsList") as RectTransform;
			if (listRect != null)
			{
				listRect.anchorMin = new Vector2(0f, 0f);
				listRect.anchorMax = new Vector2(1f, 1f);
				listRect.offsetMin = new Vector2(0f, Mathf.Round(58f * scale));
				listRect.offsetMax = new Vector2(0f, -Mathf.Round((inputTop + inputHeight + 10f * scale)));
			}

			SetBottomStretch(transform.Find("RefreshButton") as RectTransform, 0f, 0.30f, Mathf.Round(8f * scale), Mathf.Round(44f * scale));
			SetBottomStretch(transform.Find("StatusText") as RectTransform, 0.31f, 1f, Mathf.Round(8f * scale), Mathf.Round(44f * scale));

			StyleButton(_addButton, BTN_SUCCESS);
			StyleButton(_pasteButton, BTN_PRIMARY);
			StyleButton(_refreshButton, BTN_PRIMARY);
			StyleButton(_copyIdButton, BTN_PRIMARY);
			StyleButton(_myProfileButton, TAB_ON);
			ApplyProfileOverlayLayout(scale);

			if (_myProfileButton != null)
			{
				var profileText = _myProfileButton.GetComponentInChildren<TMP_Text>(true);
				if (profileText != null)
					profileText.fontSize = Mathf.Round(15f * scale);			}

			if (_myNameText != null)
			{
				_myNameText.fontSize = Mathf.Round(32f * scale);
				_myNameText.textWrappingMode = TextWrappingModes.NoWrap;
				_myNameText.overflowMode = TextOverflowModes.Ellipsis;
			}

			if (_myIdText != null)
			{
				_myIdText.fontSize = Mathf.Round(18f * scale);
				_myIdText.color = new Color(0.72f, 0.86f, 1f, 1f);
			}

			if (_statusText != null)
				_statusText.fontSize = Mathf.Round(16f * scale);
		}

		private void ApplyProfileOverlayLayout(float scale)
		{
			if (_profileOverlayRoot == null || _profileCardRect == null)
				return;

			float cardScale = Mathf.Clamp(scale, 0.92f, 1.24f);
			_profileCardRect.sizeDelta = new Vector2(Mathf.Round(700f * cardScale), Mathf.Round(400f * cardScale));

			SetStretch(_profileCloseButton != null ? _profileCloseButton.transform as RectTransform : null, 0.915f, 0.985f, 0.86f, 0.96f);
			SetStretch(_profileAvatarImage != null ? _profileAvatarImage.rectTransform : null, 0.05f, 0.24f, 0.57f, 0.86f);
			SetStretch(_profileTitleText != null ? _profileTitleText.transform as RectTransform : null, 0.28f, 0.88f, 0.79f, 0.92f);
			SetStretch(_profileInfoText != null ? _profileInfoText.transform as RectTransform : null, 0.28f, 0.93f, 0.33f, 0.76f);
			SetStretch(_profileHintText != null ? _profileHintText.transform as RectTransform : null, 0.05f, 0.93f, 0.26f, 0.31f);

			SetStretch(_profileCopyButton != null ? _profileCopyButton.transform as RectTransform : null, 0.05f, 0.27f, 0.08f, 0.21f);
			SetStretch(_profileSyncNameButton != null ? _profileSyncNameButton.transform as RectTransform : null, 0.29f, 0.51f, 0.08f, 0.21f);
			SetStretch(_profileNewAvatarButton != null ? _profileNewAvatarButton.transform as RectTransform : null, 0.53f, 0.75f, 0.08f, 0.21f);
			SetStretch(_profileRefreshButton != null ? _profileRefreshButton.transform as RectTransform : null, 0.77f, 0.95f, 0.08f, 0.21f);

			StyleButton(_profileCloseButton, BTN_DANGER);
			StyleButton(_profileCopyButton, BTN_PRIMARY);
			StyleButton(_profileSyncNameButton, TAB_ON);
			StyleButton(_profileNewAvatarButton, BTN_SUCCESS);
			StyleButton(_profileRefreshButton, BTN_PRIMARY);

			ApplyTextStyle(_profileTitleText, Mathf.Round(26f * cardScale), FontStyles.Bold, Color.white);
			ApplyTextStyle(_profileInfoText, Mathf.Round(17f * cardScale), FontStyles.Normal, new Color(0.92f, 0.94f, 0.98f, 1f));
			ApplyTextStyle(_profileHintText, Mathf.Round(14f * cardScale), FontStyles.Italic, new Color(0.72f, 0.86f, 1f, 1f));
		}

		private void ApplyItemTheme(UIFriendItemView view)
		{
			float itemScale = GetItemScale();

			var rootImage = view.GetComponent<Image>();
			if (rootImage != null)
				rootImage.color = ITEM_BG;
			ApplyCardFrame(view.gameObject, itemScale);

			var layoutElement = view.GetComponent<LayoutElement>();
			if (layoutElement != null)
			{
				layoutElement.minHeight = Mathf.Round(104f * itemScale);
				layoutElement.preferredHeight = Mathf.Round(112f * itemScale);
				layoutElement.flexibleHeight = 0f;
			}

			var horizontalLayout = view.GetComponent<HorizontalLayoutGroup>();
			if (horizontalLayout != null)
			{
				int sidePadding = Mathf.RoundToInt(14f * itemScale);
				int verticalPadding = Mathf.RoundToInt(10f * itemScale);
				horizontalLayout.spacing = Mathf.Round(12f * itemScale);
				horizontalLayout.padding = new RectOffset(sidePadding, sidePadding, verticalPadding, verticalPadding);
				horizontalLayout.childAlignment = TextAnchor.MiddleLeft;
				horizontalLayout.childControlWidth = true;
				horizontalLayout.childForceExpandWidth = false;
				horizontalLayout.childControlHeight = true;
				horizontalLayout.childForceExpandHeight = true;
			}

			if (view.NameText != null)
			{
				view.NameText.fontSize = Mathf.Round(25f * itemScale);
				view.NameText.textWrappingMode = TextWrappingModes.NoWrap;
				view.NameText.overflowMode = TextOverflowModes.Ellipsis;
				view.NameText.raycastTarget = false;
			}

			if (view.StatusText != null)
			{
				view.StatusText.fontSize = Mathf.Round(16f * itemScale);
				view.StatusText.color = new Color(0.90f, 0.90f, 0.88f, 1f);
				view.StatusText.fontStyle = FontStyles.Normal;
				view.StatusText.textWrappingMode = TextWrappingModes.Normal;
				view.StatusText.overflowMode = TextOverflowModes.Truncate;
				view.StatusText.raycastTarget = false;
			}

			if (view.AvatarImage != null && view.AvatarImage.texture == null)
				view.AvatarImage.color = new Color(0.7f, 0.75f, 0.82f, 0.45f);
			if (view.AvatarImage != null)
				ApplyAvatarFrame(view.AvatarImage.gameObject, itemScale);

			EnsureItemStructure(view, itemScale);
		}
	}
}
#endif
