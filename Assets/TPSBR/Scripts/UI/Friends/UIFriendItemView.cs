#if ENABLE_PLAYFAB
namespace TPSBR.UI
{
	using TMPro;
	using UnityEngine;
	using UnityEngine.UI;

	/// <summary>
	/// Stable bindings for friend list item to avoid relying on transform/text order.
	/// </summary>
	public class UIFriendItemView : MonoBehaviour
	{
		[SerializeField] private TMP_Text _nameText;
		[SerializeField] private TMP_Text _statusText;
		[SerializeField] private Image _statusIndicator;
		[SerializeField] private RawImage _avatarImage;
		[SerializeField] private Button _joinButton;
		[SerializeField] private Button _removeButton;
		[SerializeField] private TMP_Text _joinButtonText;
		[SerializeField] private TMP_Text _removeButtonText;

		public TMP_Text NameText => _nameText;
		public TMP_Text StatusText => _statusText;
		public Image StatusIndicator => _statusIndicator;
		public RawImage AvatarImage => _avatarImage;
		public Button JoinButton => _joinButton;
		public Button RemoveButton => _removeButton;
		public TMP_Text JoinButtonText => _joinButtonText;
		public TMP_Text RemoveButtonText => _removeButtonText;

		public void EnsureBindings()
		{
			if (_nameText == null)
			{
				var nameTransform = transform.Find("TextContainer/NameText");
				if (nameTransform != null)
					_nameText = nameTransform.GetComponent<TMP_Text>();
			}

			if (_statusText == null)
			{
				var statusTransform = transform.Find("TextContainer/StatusText");
				if (statusTransform != null)
					_statusText = statusTransform.GetComponent<TMP_Text>();
			}

			if (_statusIndicator == null)
			{
				var indicatorTransform = transform.Find("StatusIndicator");
				if (indicatorTransform != null)
					_statusIndicator = indicatorTransform.GetComponent<Image>();
			}

			if (_avatarImage == null)
			{
				var avatarTransform = transform.Find("AvatarImage");
				if (avatarTransform != null)
					_avatarImage = avatarTransform.GetComponent<RawImage>();
			}

			if (_joinButton == null)
			{
				var joinTransform = transform.Find("JoinButton");
				if (joinTransform != null)
					_joinButton = joinTransform.GetComponent<Button>();
			}

			if (_removeButton == null)
			{
				var removeTransform = transform.Find("RemoveButton");
				if (removeTransform != null)
					_removeButton = removeTransform.GetComponent<Button>();
			}

			if (_joinButtonText == null && _joinButton != null)
				_joinButtonText = _joinButton.GetComponentInChildren<TMP_Text>(true);

			if (_removeButtonText == null && _removeButton != null)
				_removeButtonText = _removeButton.GetComponentInChildren<TMP_Text>(true);
		}
	}
}
#endif
