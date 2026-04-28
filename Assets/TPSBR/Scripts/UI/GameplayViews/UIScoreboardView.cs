namespace TPSBR.UI
{
	using UnityEngine;
	using UnityEngine.InputSystem;

	public class UIScoreboardView : UIView
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private UIGameInfo  _gameInfo;
		[SerializeField]
		private CanvasGroup _fader;
		[SerializeField]
		private float       _fadeSpeed = 5f;

		private UIScoreboard _board;
		private float _targetAlpha;
		private bool _isShowing;

		// PUBLIC METHODS

		public void Show()
		{
			_isShowing = true;
			_board.SetActive(true);
			_targetAlpha = 1f;

			if (Context.Runner != null)
			{
				_gameInfo.UpdateInfo(Context.Runner, true);
			}
		}

		public void Hide(bool immediately = false)
		{
			_isShowing = false;
			_targetAlpha = 0f;
			_board.SetActive(false);

			if (immediately == true)
			{
				_fader.alpha = _targetAlpha;
			}
		}

		public void Toggle()
		{
			if (_isShowing)
				Hide();
			else
				Show();
		}

		// UIView INTERFACE

		protected override void OnInitialize()
		{
			base.OnInitialize();

			_board = GetComponentInChildren<UIScoreboard>(true);
		}

		protected override void OnOpen()
		{
			base.OnOpen();

			Hide(true);
		}

		protected override void OnTick()
		{
			base.OnTick();

			// Toggle on Tab key press (was hold before)
			if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame == true && IsTopView(true) == true)
			{
				Toggle();
			}

			// Toggle on Gamepad Select/View button (left of Xbox button)
			if (Gamepad.current != null && Gamepad.current.selectButton.wasPressedThisFrame == true && IsTopView(true) == true)
			{
				Toggle();
			}

			_fader.alpha = Mathf.Lerp(_fader.alpha, _targetAlpha, Time.deltaTime * _fadeSpeed);

			if (_targetAlpha <= 0.0f || Context.Runner == null)
				return;

			_gameInfo.UpdateInfo(Context.Runner);
		}
	}
}

