#if ENABLE_PLAYFAB
namespace TPSBR.UI
{
	using UnityEngine;
	using UnityEngine.UI;

	/// <summary>
	/// Friends view for the menu system. Inherits UICloseView for proper back navigation.
	/// Requires a UIFriendController component on a child panel.
	/// </summary>
	public class UIFriendsView : UICloseView
	{
		// UIView INTERFACE

		protected override void OnOpen()
		{
			base.OnOpen();

			EnsureCanvas();
			ApplyShellLayout();

			// Refresh friends when opening
			if (PlayFabManager.Instance != null)
			{
				PlayFabManager.Instance.GetFriends();
			}
		}

		protected override void OnClose()
		{
			base.OnClose();
		}

		private void EnsureCanvas()
		{
			var canvas = GetComponent<Canvas>();
			if (canvas == null)
			{
				canvas = gameObject.AddComponent<Canvas>();
				gameObject.AddComponent<GraphicRaycaster>();
			}

			var scaler = GetComponent<CanvasScaler>();
			if (scaler == null)
				scaler = gameObject.AddComponent<CanvasScaler>();

			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			scaler.referenceResolution = new Vector2(1920f, 1080f);
			scaler.matchWidthOrHeight = 0.5f;

			var root = transform as RectTransform;
			if (root != null)
			{
				root.anchorMin = Vector2.zero;
				root.anchorMax = Vector2.one;
				root.offsetMin = Vector2.zero;
				root.offsetMax = Vector2.zero;
			}

			canvas.overrideSorting = true;
			canvas.sortingOrder = 100;
		}

		private void ApplyShellLayout()
		{
			SetTopCenterFixed(transform.Find("TitleText") as RectTransform, -16f, 22f, 400f, 58f);
			SetTopRightFixed(transform.Find("CloseButton") as RectTransform, 10f, 16f, 74f, 74f);
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
