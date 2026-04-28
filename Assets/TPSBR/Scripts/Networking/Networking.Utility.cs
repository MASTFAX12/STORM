namespace TPSBR
{
	using System.Collections.Generic;
	using UnityEngine;
	using UnityEngine.InputSystem;

	public partial class Networking
	{
		private void UpdatePeerSwitch(GamePeer[] peers)
		{
			int  newID      = -1;
			bool showOthers = false;

			bool canSwitchPeer = Application.isEditor == true ? true : Keyboard.current.leftCtrlKey.isPressed == true && Keyboard.current.leftShiftKey.isPressed == true;
			if (canSwitchPeer == true)
			{
				if (Keyboard.current.numpad1Key.wasPressedThisFrame == true)
				{
					newID = 0;
				}
				else if (Keyboard.current.numpad2Key.wasPressedThisFrame == true)
				{
					newID = 1;
				}
				else if (Keyboard.current.numpad3Key.wasPressedThisFrame == true)
				{
					newID = 2;
				}
				else if (Keyboard.current.numpad4Key.wasPressedThisFrame == true)
				{
					newID = 0;
					showOthers = true;
				}
				else if (Keyboard.current.numpad5Key.wasPressedThisFrame == true)
				{
					newID = 1;
					showOthers = true;
				}
				else if (Keyboard.current.numpad6Key.wasPressedThisFrame == true)
				{
					newID = 2;
					showOthers = true;
				}
			}

			if (newID >= 0 && newID < peers.Length)
			{
				for (int i = 0; i < peers.Length; i++)
				{
					GamePeer peer = peers[i];

					peer.Context.HasInput = peer.ID == newID;
					peer.Context.IsVisible = peer.ID == newID || showOthers == true;
				}
			}
		}

		private void ValidateMultiPeers(GamePeer[] peers)
		{
			if (peers.SafeCount() <= 0)
				return;

			int inputPeer = -1;
			int visibilityPeer = -1;

			for (int i = 0; i < peers.Length; i++)
			{
				GamePeer peer = peers[i];

				if (peer.Context == null)
					continue;

				if (peer.Context.HasInput)
				{
					if (inputPeer >= 0)
					{
						Debug.Log($"Multiple peers with input is not allowed, turning off input for peer {peer.ID}");
						peer.Context.HasInput = false;
					}
					else
					{
						inputPeer = peer.ID;
					}
				}

				if (peer.Context.IsVisible == true && visibilityPeer < 0)
				{
					visibilityPeer = peer.ID;
				}
			}

			if (peers[0].Context != null)
			{
				if (inputPeer < 0)
				{
					Debug.Log($"No input peer, turning on input for peer {peers[0].ID}");
					peers[0].Context.HasInput = true;
				}

				if (visibilityPeer < 0)
				{
					Debug.Log($"No visible peer, turning on visibility for peer {peers[0].ID}");
					peers[0].Context.IsVisible = true;
				}
			}
		}

		private Dictionary<string, Fusion.SessionProperty> CreateSessionProperties(SessionRequest request)
		{
			var dictionary = new Dictionary<string, Fusion.SessionProperty>();

			dictionary[DISPLAY_NAME_KEY] = request.DisplayName;
			dictionary[MAP_KEY]          = Global.Settings.Map.GetMapIndexFromScenePath(request.ScenePath);
			dictionary[TYPE_KEY]         = (int)request.GameplayType;
			dictionary[MODE_KEY]         = (int)request.GameMode;

			return dictionary;
		}

		[System.Diagnostics.Conditional("ENABLE_LOGS")]
		private void Log(string message)
		{
			Debug.Log($"[{Time.realtimeSinceStartup:F3}][{Time.frameCount}] Networking({GetInstanceID()}): {message}");
		}

		private static string StringToLabel(string myString)
		{
			var label = System.Text.RegularExpressions.Regex.Replace(myString, "(?<=[A-Z])(?=[A-Z][a-z])", " ");
			label = System.Text.RegularExpressions.Regex.Replace(label, "(?<=[^A-Z])(?=[A-Z])", " ");

			return label;
		}
	}
}
