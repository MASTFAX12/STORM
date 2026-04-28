#if UNITY_EDITOR && ENABLE_PLAYFAB
namespace TPSBR.Editor
{
	using TMPro;
	using UnityEditor;
	using UnityEngine;
	using UnityEngine.UI;
	using TPSBR.UI;

	/// <summary>
	/// Rebuilds Friends UI with responsive layout and button visuals copied from existing game button prefabs.
	/// </summary>
	public static class FriendUIBuilder
	{
		private const string FriendsViewPrefabPath = "Assets/TPSBR/UI/Prefabs/GeneralViews/UIFriendsView.prefab";
		private const string FriendItemPrefabPath = "Assets/TPSBR/UI/Prefabs/FriendItem.prefab";

		private const string PrimaryButtonTemplatePath = "Assets/TPSBR/UI/Prefabs/Buttons/PrimaryButton.prefab";
		private const string SecondaryButtonTemplatePath = "Assets/TPSBR/UI/Prefabs/Buttons/SecondaryButton.prefab";
		private const string CloseButtonTemplatePath = "Assets/TPSBR/UI/Prefabs/Buttons/CloseButton.prefab";
		private const string InputFieldTemplatePath = "Assets/TPSBR/UI/Prefabs/Buttons/InputField.prefab";

		private static readonly Color PanelBg = new Color(0.05f, 0.05f, 0.07f, 0.96f);
		private static readonly Color ItemBg = new Color(0.10f, 0.09f, 0.08f, 0.96f);
		private static readonly Color TabOff = new Color(0.16f, 0.16f, 0.16f, 1f);
		private static readonly Color TabOn = new Color(0.78f, 0.58f, 0.20f, 1f);
		private static readonly Color BtnBlue = new Color(0.14f, 0.14f, 0.14f, 1f);
		private static readonly Color BtnGreen = new Color(0.58f, 0.66f, 0.20f, 1f);
		private static readonly Color BtnRed = new Color(0.95f, 0.19f, 0.19f, 1f);

		private struct ButtonVisualTemplate
		{
			public Sprite BackgroundSprite;
			public Image.Type BackgroundType;
			public Material BackgroundMaterial;
			public Color BackgroundColor;
			public ColorBlock ButtonColors;
			public TMP_FontAsset LabelFont;
			public FontStyles LabelStyle;
			public Color LabelColor;
			public float LabelSize;
		}

		private struct InputVisualTemplate
		{
			public Sprite BackgroundSprite;
			public Image.Type BackgroundType;
			public Material BackgroundMaterial;
			public Color BackgroundColor;
			public TMP_FontAsset TextFont;
			public float TextSize;
			public Color TextColor;
			public Color PlaceholderColor;
			public Color CaretColor;
			public Color SelectionColor;
		}

		[MenuItem("Tools/Friend System/Rebuild Friends UI (Responsive)")]
		public static void RebuildFriendsUi()
		{
			GameObject viewRoot = null;
			GameObject itemRoot = null;

			try
			{
				ButtonVisualTemplate primaryTemplate = LoadButtonTemplate(PrimaryButtonTemplatePath);
				ButtonVisualTemplate secondaryTemplate = LoadButtonTemplate(SecondaryButtonTemplatePath);
				ButtonVisualTemplate closeTemplate = LoadButtonTemplate(CloseButtonTemplatePath);
				InputVisualTemplate inputTemplate = LoadInputTemplate(InputFieldTemplatePath);

				viewRoot = PrefabUtility.LoadPrefabContents(FriendsViewPrefabPath);
				itemRoot = PrefabUtility.LoadPrefabContents(FriendItemPrefabPath);

				UpgradeFriendItem(itemRoot, primaryTemplate, closeTemplate, secondaryTemplate);
				PrefabUtility.SaveAsPrefabAsset(itemRoot, FriendItemPrefabPath);

				UpgradeFriendsView(viewRoot, primaryTemplate, secondaryTemplate, closeTemplate, inputTemplate);
				PrefabUtility.SaveAsPrefabAsset(viewRoot, FriendsViewPrefabPath);

				AssetDatabase.SaveAssets();
				AssetDatabase.Refresh();
				EditorUtility.DisplayDialog("Friend UI", "Friends UI visuals updated from game button styles.", "OK");
			}
			finally
			{
				if (viewRoot != null)
					PrefabUtility.UnloadPrefabContents(viewRoot);
				if (itemRoot != null)
					PrefabUtility.UnloadPrefabContents(itemRoot);
			}
		}

		[MenuItem("Tools/Friend System/Validate Friends UI")]
		public static void ValidateFriendsUi()
		{
			var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FriendsViewPrefabPath);
			if (prefab == null)
			{
				EditorUtility.DisplayDialog("Friend UI", "UIFriendsView prefab not found.", "OK");
				return;
			}

			var controller = prefab.GetComponentInChildren<UIFriendController>(true);
			if (controller == null)
			{
				EditorUtility.DisplayDialog("Friend UI", "UIFriendController is missing.", "OK");
				return;
			}

			var so = new SerializedObject(controller);
			bool ok = true;

			ok &= RequireReference(so, "_myIdText");
			ok &= RequireReference(so, "_myNameText");
			ok &= RequireReference(so, "_myAvatarImage");
			ok &= RequireReference(so, "_copyIdButton");
			ok &= RequireReference(so, "_inputField");
			ok &= RequireReference(so, "_addButton");
			ok &= RequireReference(so, "_friendsTabButton");
			ok &= RequireReference(so, "_discoverTabButton");
			ok &= RequireReference(so, "_friendListContent");
			ok &= RequireReference(so, "_friendItemPrefab");
			ok &= RequireReference(so, "_refreshButton");
			ok &= RequireReference(so, "_statusText");

			EditorUtility.DisplayDialog("Friend UI", ok ? "Validation passed." : "Validation failed. Rebuild the UI.", "OK");
		}

		private static bool RequireReference(SerializedObject so, string propertyName)
		{
			var prop = so.FindProperty(propertyName);
			return prop != null && prop.objectReferenceValue != null;
		}

		private static ButtonVisualTemplate LoadButtonTemplate(string templatePath)
		{
			var template = new ButtonVisualTemplate
			{
				BackgroundType = Image.Type.Sliced,
				BackgroundColor = Color.white,
				ButtonColors = ColorBlock.defaultColorBlock,
				LabelColor = Color.white,
				LabelStyle = FontStyles.Bold,
				LabelSize = 20f,
			};

			GameObject root = null;
			try
			{
				root = PrefabUtility.LoadPrefabContents(templatePath);
				var sourceButton = root != null ? root.GetComponent<Button>() : null;
				var sourceBg = FindDeepChild(root != null ? root.transform : null, "BG");
				var bgImage = sourceBg != null ? sourceBg.GetComponent<Image>() : null;
				if (bgImage == null && root != null)
					bgImage = root.GetComponent<Image>();

				var labelTransform = FindDeepChild(root != null ? root.transform : null, "Label");
				var label = labelTransform != null ? labelTransform.GetComponent<TMP_Text>() : null;
				if (label == null && root != null)
					label = root.GetComponentInChildren<TMP_Text>(true);

				if (bgImage != null)
				{
					template.BackgroundSprite = bgImage.sprite;
					template.BackgroundType = bgImage.type;
					template.BackgroundMaterial = bgImage.material;
					template.BackgroundColor = bgImage.color;
				}

				if (sourceButton != null)
					template.ButtonColors = sourceButton.colors;

				if (label != null)
				{
					template.LabelFont = label.font;
					template.LabelStyle = label.fontStyle;
					template.LabelColor = label.color;
					template.LabelSize = label.fontSize;
				}
			}
			finally
			{
				if (root != null)
					PrefabUtility.UnloadPrefabContents(root);
			}

			return template;
		}

		private static InputVisualTemplate LoadInputTemplate(string templatePath)
		{
			var template = new InputVisualTemplate
			{
				BackgroundType = Image.Type.Sliced,
				BackgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f),
				TextSize = 20f,
				TextColor = Color.white,
				PlaceholderColor = new Color(1f, 1f, 1f, 0.5f),
				CaretColor = new Color(0.2f, 0.2f, 0.2f, 1f),
				SelectionColor = new Color(0f, 0.9f, 0.62f, 1f)
			};

			GameObject root = null;
			try
			{
				root = PrefabUtility.LoadPrefabContents(templatePath);
				var bg = FindDeepChild(root != null ? root.transform : null, "Background");
				var bgImage = bg != null ? bg.GetComponent<Image>() : null;
				var text = FindDeepChild(root != null ? root.transform : null, "Text");
				var placeholder = FindDeepChild(root != null ? root.transform : null, "Placeholder");
				var inputField = root != null ? root.GetComponent<TMP_InputField>() : null;

				if (bgImage != null)
				{
					template.BackgroundSprite = bgImage.sprite;
					template.BackgroundType = bgImage.type;
					template.BackgroundMaterial = bgImage.material;
					template.BackgroundColor = bgImage.color;
				}

				if (text != null)
				{
					var textTmp = text.GetComponent<TMP_Text>();
					if (textTmp != null)
					{
						template.TextFont = textTmp.font;
						template.TextSize = textTmp.fontSize;
						template.TextColor = textTmp.color;
					}
				}

				if (placeholder != null)
				{
					var placeholderTmp = placeholder.GetComponent<TMP_Text>();
					if (placeholderTmp != null)
						template.PlaceholderColor = placeholderTmp.color;
				}

				if (inputField != null)
				{
					template.CaretColor = inputField.caretColor;
					template.SelectionColor = inputField.selectionColor;
				}
			}
			finally
			{
				if (root != null)
					PrefabUtility.UnloadPrefabContents(root);
			}

			return template;
		}

		private static void UpgradeFriendsView(GameObject viewRoot, ButtonVisualTemplate primaryTemplate, ButtonVisualTemplate secondaryTemplate, ButtonVisualTemplate closeTemplate, InputVisualTemplate inputTemplate)
		{
			if (viewRoot == null)
				return;

			EnsureComponent<CanvasGroup>(viewRoot);
			EnsureComponent<UIFriendsView>(viewRoot);

			var panel = FindDeepChild(viewRoot.transform, "FriendsPanel");
			if (panel == null)
				return;

			var panelRect = panel.GetComponent<RectTransform>();
			if (panelRect != null)
			{
				panelRect.anchorMin = new Vector2(0.03f, 0.04f);
				panelRect.anchorMax = new Vector2(0.97f, 0.92f);
				panelRect.offsetMin = Vector2.zero;
				panelRect.offsetMax = Vector2.zero;
			}

			var panelImage = EnsureComponent<Image>(panel.gameObject);
			panelImage.color = PanelBg;
			ApplyPanelFrame(panel.gameObject);

			var controller = EnsureComponent<UIFriendController>(panel.gameObject);

			var title = FindDeepChild(viewRoot.transform, "TitleText");
			var closeButton = FindDeepChild(viewRoot.transform, "CloseButton");
			var myAvatar = FindDeepChild(panel, "MyAvatarImage");
			var myName = FindDeepChild(panel, "MyNameText");
			var myId = FindDeepChild(panel, "MyIdText");
			var copyId = FindDeepChild(panel, "CopyIdButton");
			var input = FindDeepChild(panel, "FriendInput");
			var add = FindDeepChild(panel, "AddButton");
			var paste = FindDeepChild(panel, "Paste Button") ?? FindDeepChild(panel, "PasteButton");
			var friendsTab = FindDeepChild(panel, "FriendsTab");
			var requestsTab = FindDeepChild(panel, "RequestsTab");
			var discoverTab = FindDeepChild(panel, "DiscoverTab");
			var list = FindDeepChild(panel, "FriendsList");
			var refresh = FindDeepChild(panel, "RefreshButton");
			var status = FindDeepChild(panel, "StatusText");

			SetTopCenterFixed(title as RectTransform, -16f, 22f, 400f, 58f);
			SetTopRightFixed(closeButton as RectTransform, 10f, 16f, 74f, 74f);

			SetTopFixed(myAvatar as RectTransform, 18f, 16f, 96f, 96f);
			SetTopStretch(myName as RectTransform, 0.14f, 0.57f, 16f, 38f);
			SetTopStretch(myId as RectTransform, 0.14f, 0.56f, 58f, 26f);
			SetTopStretch(copyId as RectTransform, 0.47f, 0.64f, 58f, 26f);

			SetTopStretch(friendsTab as RectTransform, 0.00f, 0.49f, 122f, 52f);
			SetTopStretch(requestsTab as RectTransform, 0.34f, 0.66f, 122f, 52f);
			SetTopStretch(discoverTab as RectTransform, 0.51f, 1.00f, 122f, 52f);

			SetTopStretch(input as RectTransform, 0.00f, 0.68f, 184f, 46f);
			SetTopStretch(paste as RectTransform, 0.69f, 0.83f, 184f, 46f);
			SetTopStretch(add as RectTransform, 0.84f, 1.00f, 184f, 46f);

			var listRect = list as RectTransform;
			if (listRect != null)
			{
				listRect.anchorMin = new Vector2(0f, 0f);
				listRect.anchorMax = new Vector2(1f, 1f);
				listRect.offsetMin = new Vector2(0f, 58f);
				listRect.offsetMax = new Vector2(0f, -240f);
			}

			SetBottomStretch(refresh as RectTransform, 0.00f, 0.30f, 8f, 44f);
			SetBottomStretch(status as RectTransform, 0.31f, 1.00f, 8f, 44f);

			ApplyButtonTemplate(closeButton != null ? closeButton.GetComponent<Button>() : null, closeTemplate, BtnRed, true);
			ApplyButtonTemplate(friendsTab != null ? friendsTab.GetComponent<Button>() : null, secondaryTemplate, TabOn, false);
			ApplyButtonTemplate(requestsTab != null ? requestsTab.GetComponent<Button>() : null, secondaryTemplate, TabOff, false);
			ApplyButtonTemplate(discoverTab != null ? discoverTab.GetComponent<Button>() : null, secondaryTemplate, TabOff, false);
			ApplyButtonTemplate(copyId != null ? copyId.GetComponent<Button>() : null, secondaryTemplate, BtnBlue, true);
			ApplyButtonTemplate(add != null ? add.GetComponent<Button>() : null, primaryTemplate, BtnGreen, true);
			ApplyButtonTemplate(paste != null ? paste.GetComponent<Button>() : null, secondaryTemplate, BtnBlue, true);
			ApplyButtonTemplate(refresh != null ? refresh.GetComponent<Button>() : null, secondaryTemplate, BtnBlue, true);
			ApplyInputTemplate(input != null ? input.GetComponent<TMP_InputField>() : null, inputTemplate);

			SetTextStyle(myName, 32f, Color.white);
			SetTextStyle(myId, 18f, new Color(0.89f, 0.82f, 0.62f, 1f));
			SetTextStyle(status, 16f, Color.white);
			SetTextStyle(title, 46f, Color.white);

			Transform content = FindDeepChild(panel, "Content");
			if (content != null)
			{
				var vlg = EnsureComponent<VerticalLayoutGroup>(content.gameObject);
				vlg.spacing = 10f;
				vlg.padding = new RectOffset(6, 6, 8, 8);
				vlg.childControlWidth = true;
				vlg.childForceExpandWidth = true;
				vlg.childControlHeight = false;
				vlg.childForceExpandHeight = false;

				var fitter = EnsureComponent<ContentSizeFitter>(content.gameObject);
				fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
				fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
			}

			var so = new SerializedObject(controller);
			SetReference(so, "_myIdText", myId != null ? myId.GetComponent<TMP_Text>() : null);
			SetReference(so, "_myNameText", myName != null ? myName.GetComponent<TMP_Text>() : null);
			SetReference(so, "_myAvatarImage", myAvatar != null ? myAvatar.GetComponent<RawImage>() : null);
			SetReference(so, "_copyIdButton", copyId != null ? copyId.GetComponent<Button>() : null);
			SetReference(so, "_inputField", input != null ? input.GetComponent<TMP_InputField>() : null);
			SetReference(so, "_addButton", add != null ? add.GetComponent<Button>() : null);
			SetReference(so, "_pasteButton", paste != null ? paste.GetComponent<Button>() : null);
			SetReference(so, "_friendsTabButton", friendsTab != null ? friendsTab.GetComponent<Button>() : null);
			SetReference(so, "_requestsTabButton", requestsTab != null ? requestsTab.GetComponent<Button>() : null);
			SetReference(so, "_discoverTabButton", discoverTab != null ? discoverTab.GetComponent<Button>() : null);
			SetReference(so, "_friendListContent", content);
			SetReference(so, "_friendItemPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(FriendItemPrefabPath));
			SetReference(so, "_refreshButton", refresh != null ? refresh.GetComponent<Button>() : null);
			SetReference(so, "_statusText", status != null ? status.GetComponent<TMP_Text>() : null);
			so.ApplyModifiedPropertiesWithoutUndo();
		}

		private static void UpgradeFriendItem(GameObject itemRoot, ButtonVisualTemplate primaryTemplate, ButtonVisualTemplate closeTemplate, ButtonVisualTemplate secondaryTemplate)
		{
			if (itemRoot == null)
				return;

			var image = EnsureComponent<Image>(itemRoot);
			image.sprite = secondaryTemplate.BackgroundSprite;
			image.type = secondaryTemplate.BackgroundType;
			image.material = secondaryTemplate.BackgroundMaterial;
			image.color = ItemBg;
			ApplyCardFrame(itemRoot);

			var layout = EnsureComponent<LayoutElement>(itemRoot);
			layout.minHeight = 104f;
			layout.preferredHeight = 112f;
			layout.flexibleHeight = 0f;

			var hlg = EnsureComponent<HorizontalLayoutGroup>(itemRoot);
			hlg.spacing = 12f;
			hlg.padding = new RectOffset(14, 14, 10, 10);
			hlg.childAlignment = TextAnchor.MiddleLeft;
			hlg.childControlWidth = true;
			hlg.childForceExpandWidth = false;
			hlg.childControlHeight = true;
			hlg.childForceExpandHeight = true;

			var statusIndicator = FindDeepChild(itemRoot.transform, "StatusIndicator");
			var avatar = FindDeepChild(itemRoot.transform, "AvatarImage");
			var textContainer = FindDeepChild(itemRoot.transform, "TextContainer");
			var name = FindDeepChild(itemRoot.transform, "NameText");
			var status = FindDeepChild(itemRoot.transform, "StatusText");
			var join = FindDeepChild(itemRoot.transform, "JoinButton");
			var remove = FindDeepChild(itemRoot.transform, "RemoveButton");

			int order = 0;
			if (statusIndicator != null) statusIndicator.SetSiblingIndex(order++);
			if (avatar != null) avatar.SetSiblingIndex(order++);
			if (textContainer != null) textContainer.SetSiblingIndex(order++);
			if (join != null) join.SetSiblingIndex(order++);
			if (remove != null) remove.SetSiblingIndex(order++);

			SetFixedWidth(statusIndicator, 20f);
			SetFixedWidth(avatar, 64f);
			SetFixedWidth(join, 122f);
			SetFixedWidth(remove, 54f);
			if (avatar != null)
				ApplyAvatarFrame(avatar.gameObject);

			if (textContainer != null)
			{
				var textContainerLayout = EnsureComponent<LayoutElement>(textContainer.gameObject);
				textContainerLayout.flexibleWidth = 1f;
				textContainerLayout.minWidth = 240f;

				var textVlg = EnsureComponent<VerticalLayoutGroup>(textContainer.gameObject);
				textVlg.spacing = 2f;
				textVlg.childAlignment = TextAnchor.MiddleLeft;
				textVlg.childControlWidth = true;
				textVlg.childControlHeight = true;
				textVlg.childForceExpandWidth = true;
				textVlg.childForceExpandHeight = false;
			}

			SetTextStyle(name, 25f, Color.white);
			SetTextStyle(status, 16f, new Color(0.90f, 0.90f, 0.88f, 1f));
			if (status != null)
			{
				var statusText = status.GetComponent<TMP_Text>();
				if (statusText != null)
				{
					statusText.textWrappingMode = TextWrappingModes.Normal;
					statusText.overflowMode = TextOverflowModes.Truncate;
					statusText.fontStyle = FontStyles.Normal;
				}
			}

			if (name != null)
			{
				var nameLayout = EnsureComponent<LayoutElement>(name.gameObject);
				nameLayout.minHeight = 28f;
				nameLayout.preferredHeight = 30f;
				nameLayout.flexibleHeight = 0f;
			}

			if (status != null)
			{
				var statusLayout = EnsureComponent<LayoutElement>(status.gameObject);
				statusLayout.minHeight = 38f;
				statusLayout.preferredHeight = 40f;
				statusLayout.flexibleHeight = 0f;
			}

			ApplyButtonTemplate(join != null ? join.GetComponent<Button>() : null, primaryTemplate, BtnGreen, true);
			ApplyButtonTemplate(remove != null ? remove.GetComponent<Button>() : null, closeTemplate, BtnRed, true);

			var view = EnsureComponent<UIFriendItemView>(itemRoot);
			view.EnsureBindings();
		}

		private static void ApplyInputTemplate(TMP_InputField inputField, InputVisualTemplate template)
		{
			if (inputField == null)
				return;

			var backgroundImage = inputField.GetComponent<Image>();
			if (backgroundImage != null)
			{
				backgroundImage.sprite = template.BackgroundSprite;
				backgroundImage.type = template.BackgroundType;
				backgroundImage.material = template.BackgroundMaterial;
				backgroundImage.color = template.BackgroundColor;
			}

			if (inputField.textComponent != null)
			{
				if (template.TextFont != null)
					inputField.textComponent.font = template.TextFont;
				inputField.textComponent.fontSize = template.TextSize;
				inputField.textComponent.color = template.TextColor;
				inputField.textComponent.textWrappingMode = TextWrappingModes.NoWrap;
			}

			var placeholder = inputField.placeholder as TMP_Text;
			if (placeholder != null)
			{
				if (template.TextFont != null)
					placeholder.font = template.TextFont;
				placeholder.fontSize = template.TextSize;
				placeholder.color = template.PlaceholderColor;
				placeholder.textWrappingMode = TextWrappingModes.NoWrap;
			}

			inputField.caretColor = template.CaretColor;
			inputField.selectionColor = template.SelectionColor;
		}

		private static void ApplyButtonTemplate(Button button, ButtonVisualTemplate template, Color tint, bool compactText)
		{
			if (button == null)
				return;

			var image = button.GetComponent<Image>();
			if (image != null)
			{
				image.sprite = template.BackgroundSprite;
				image.type = template.BackgroundType;
				image.material = template.BackgroundMaterial;
				image.color = tint;
				image.raycastTarget = true;
			}

			var colors = template.ButtonColors;
			colors.normalColor = tint;
			colors.highlightedColor = Color.Lerp(tint, Color.white, 0.12f);
			colors.pressedColor = Color.Lerp(tint, Color.black, 0.18f);
			colors.selectedColor = colors.highlightedColor;
			colors.disabledColor = new Color(tint.r, tint.g, tint.b, 0.35f);
			button.transition = Selectable.Transition.ColorTint;
			button.colors = colors;

			var label = button.GetComponentInChildren<TMP_Text>(true);
			if (label != null)
			{
				if (template.LabelFont != null)
					label.font = template.LabelFont;
				label.fontStyle = template.LabelStyle;
				if (compactText == false)
					label.fontStyle = FontStyles.Bold;
				label.color = template.LabelColor;
				label.fontSize = compactText ? Mathf.Clamp(template.LabelSize - 3f, 16f, 28f) : Mathf.Clamp(template.LabelSize - 1f, 17f, 30f);
				label.textWrappingMode = TextWrappingModes.NoWrap;
				label.raycastTarget = false;
			}
		}

		private static void ApplyPanelFrame(GameObject panel)
		{
			if (panel == null)
				return;

			var outline = EnsureOutline(panel);
			outline.effectColor = new Color(0.72f, 0.55f, 0.26f, 0.34f);
			outline.effectDistance = new Vector2(1f, -1f);

			var shadow = EnsureShadow(panel);
			shadow.effectColor = new Color(0f, 0f, 0f, 0.62f);
			shadow.effectDistance = new Vector2(0f, -5f);
		}

		private static void ApplyCardFrame(GameObject card)
		{
			if (card == null)
				return;

			var outline = EnsureOutline(card);
			outline.effectColor = new Color(0.74f, 0.58f, 0.30f, 0.24f);
			outline.effectDistance = new Vector2(1f, -1f);

			var shadow = EnsureShadow(card);
			shadow.effectColor = new Color(0f, 0f, 0f, 0.56f);
			shadow.effectDistance = new Vector2(0f, -3f);
		}

		private static void ApplyAvatarFrame(GameObject avatar)
		{
			if (avatar == null)
				return;

			var outline = EnsureOutline(avatar);
			outline.effectColor = new Color(0.84f, 0.69f, 0.34f, 0.78f);
			outline.effectDistance = new Vector2(1f, -1f);
		}

		private static Outline EnsureOutline(GameObject go)
		{
			var outline = go.GetComponent<Outline>();
			if (outline == null)
				outline = go.AddComponent<Outline>();
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
				{
					shadow = shadows[i];
					break;
				}
			}

			if (shadow == null)
				shadow = go.AddComponent<Shadow>();
			shadow.useGraphicAlpha = true;
			return shadow;
		}

		private static void SetReference(SerializedObject so, string propertyName, Object value)
		{
			var prop = so.FindProperty(propertyName);
			if (prop != null)
				prop.objectReferenceValue = value;
		}

		private static T EnsureComponent<T>(GameObject go) where T : Component
		{
			var component = go.GetComponent<T>();
			if (component == null)
				component = go.AddComponent<T>();
			return component;
		}

		private static Transform FindDeepChild(Transform root, string name)
		{
			if (root == null)
				return null;

			if (root.name == name)
				return root;

			for (int i = 0; i < root.childCount; ++i)
			{
				var found = FindDeepChild(root.GetChild(i), name);
				if (found != null)
					return found;
			}

			return null;
		}

		private static void SetTextStyle(Transform target, float size, Color color)
		{
			if (target == null)
				return;

			var text = target.GetComponent<TMP_Text>();
			if (text == null)
				return;

			text.fontSize = size;
			text.color = color;
			text.textWrappingMode = TextWrappingModes.NoWrap;
			text.overflowMode = TextOverflowModes.Ellipsis;
			text.raycastTarget = false;
		}

		private static void SetFixedWidth(Transform target, float width)
		{
			if (target == null)
				return;

			var layout = EnsureComponent<LayoutElement>(target.gameObject);
			layout.minWidth = width;
			layout.preferredWidth = width;
			layout.flexibleWidth = 0f;
		}

		private static void SetTopStretch(RectTransform rect, float minX, float maxX, float top, float height)
		{
			if (rect == null)
				return;

			rect.anchorMin = new Vector2(minX, 1f);
			rect.anchorMax = new Vector2(maxX, 1f);
			rect.pivot = new Vector2(0.5f, 1f);
			rect.offsetMin = new Vector2(4f, -(top + height));
			rect.offsetMax = new Vector2(-4f, -top);
		}

		private static void SetBottomStretch(RectTransform rect, float minX, float maxX, float bottom, float height)
		{
			if (rect == null)
				return;

			rect.anchorMin = new Vector2(minX, 0f);
			rect.anchorMax = new Vector2(maxX, 0f);
			rect.pivot = new Vector2(0.5f, 0f);
			rect.offsetMin = new Vector2(4f, bottom);
			rect.offsetMax = new Vector2(-4f, bottom + height);
		}

		private static void SetTopFixed(RectTransform rect, float left, float top, float width, float height)
		{
			if (rect == null)
				return;

			rect.anchorMin = new Vector2(0f, 1f);
			rect.anchorMax = new Vector2(0f, 1f);
			rect.pivot = new Vector2(0f, 1f);
			rect.anchoredPosition = new Vector2(left, -top);
			rect.sizeDelta = new Vector2(width, height);
		}

		private static void SetTopRightFixed(RectTransform rect, float right, float top, float width, float height)
		{
			if (rect == null)
				return;

			rect.anchorMin = new Vector2(1f, 1f);
			rect.anchorMax = new Vector2(1f, 1f);
			rect.pivot = new Vector2(1f, 1f);
			rect.anchoredPosition = new Vector2(-right, -top);
			rect.sizeDelta = new Vector2(width, height);
		}

		private static void SetTopCenterFixed(RectTransform rect, float centerOffsetX, float top, float width, float height)
		{
			if (rect == null)
				return;

			rect.anchorMin = new Vector2(0.5f, 1f);
			rect.anchorMax = new Vector2(0.5f, 1f);
			rect.pivot = new Vector2(0.5f, 1f);
			rect.anchoredPosition = new Vector2(centerOffsetX, -top);
			rect.sizeDelta = new Vector2(width, height);
		}
	}
}
#endif
