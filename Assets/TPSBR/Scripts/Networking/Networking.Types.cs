namespace TPSBR
{
	using System.Collections.Generic;
	using Fusion;
	using UnityEngine;
	using UnityScene = UnityEngine.SceneManagement.Scene;

	public struct SessionRequest
	{
		public string        UserID;
		public GameMode      GameMode;
		public string        DisplayName;
		public string        SessionName;
		public string        ScenePath;
		public EGameplayType GameplayType;
		public int           MaxPlayers;
		public int           ExtraPeers;
		public string        CustomLobby;
		public string        IPAddress;
		public ushort        Port;
	}

	public partial class Networking
	{
		private sealed class GamePeer
		{
			public int                         ID;
			public NetworkSceneInfo            Scene;
			public SceneContext                Context;
			public GameMode                    GameMode;
			public NetworkRunner               Runner;
			public NetworkSceneManager         SceneManager;
			public UnityScene                  LoadedScene;
			public string                      UserID;
			public SessionRequest              Request;
			public int                         ConnectionTries   = 4;
			public int                         ReconnectionTries = 2;

			public bool                        Loaded;
			public bool                        WasConnected;
			public bool                        CanConnect => WasConnected == true ? ReconnectionTries > 0 : ConnectionTries > 0;

			public bool IsConnected
			{
				get
				{
					if (Runner == null)
						return false;

					if (Request.GameMode == GameMode.Single)
						return true;

					if (Runner.IsCloudReady == false || Runner.IsRunning == false)
						return false;

					return GameMode == GameMode.Client ? Runner.IsConnectedToServer : true;
				}
			}

			public GamePeer(int id)
			{
				ID = id;
			}
		}

		private class Session
		{
			public bool       ConnectionRequested;
			public GamePeer[] GamePeers;

			public bool IsConnected
			{
				get
				{
					if (GamePeers.SafeCount() == 0)
						return false;

					for (int i = 0; i < GamePeers.Length; i++)
					{
						if (GamePeers[i].IsConnected == false)
							return false;
					}

					return true;
				}
			}
		}
	}
}
