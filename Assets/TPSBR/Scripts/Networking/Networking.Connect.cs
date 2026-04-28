namespace TPSBR
{
	using System;
	using System.Collections;
	using System.Threading.Tasks;
	using UnityEngine;
	using UnityEngine.EventSystems;
	using UnityEngine.SceneManagement;
	using Fusion;
	using Fusion.Photon.Realtime;
	using Fusion.Sockets;
	using UnityScene = UnityEngine.SceneManagement.Scene;

	public partial class Networking
	{
		public void UpdateCurrentSession()
		{
			if (_currentSession == null)
			{
				Status = string.Empty;
				StatusDescription = string.Empty;
				return;
			}

			if (_coroutine != null)
				return;

			var peers = _currentSession.GamePeers;

			if (_stopGameOnDisconnect == true)
			{
				for (int i = 0; i < peers.Length; i++)
				{
					if (_currentSession.ConnectionRequested == true && peers[i].IsConnected == false)
					{
						Log($"Stopping game after disconnect");
						_stopGameOnDisconnect = false;
						StopGame();
						return;
					}
				}
			}

			for (int i = 0; i < peers.Length; i++)
			{
				var peer = peers[i];
				bool isConnected = peer.IsConnected;

				if (_currentSession.ConnectionRequested == true && peer.Loaded == false && isConnected == false && peer.CanConnect == true)
				{
					// First connect or reconnect after failed connect

					Status = peer.WasConnected == false ? "Starting" : "Reconnecting";
					Log($"Starting ConnectPeerCoroutine() - {Status} - Peer {peer.ID}");
					_coroutine = StartCoroutine(ConnectPeerCoroutine(peer));
					return;
				}
				else if (_currentSession.ConnectionRequested == false && (isConnected == true || peer.Loaded == true))
				{
					// Disconnect requested

					Status = "Quitting";
					Log($"Starting DisconnectPeerCoroutine() - {Status} - Peer {peer.ID}");
					_coroutine = StartCoroutine(DisconnectPeerCoroutine(peer));
					return;
				}
				else if (peer.Loaded == true && isConnected == false)
				{
					// Connection lost

					Status = "Connection Lost";
					Log($"Starting DisconnectPeerCoroutine() - {Status} - Peer {peer.ID}");
					_coroutine = StartCoroutine(DisconnectPeerCoroutine(peer));
					return;
				}
			}

			UpdatePeerSwitch(_currentSession.GamePeers);
			ValidateMultiPeers(_currentSession.GamePeers);
		}

		private IEnumerator ConnectPeerCoroutine(GamePeer peer, float connectionTimeout = 20f, float loadTimeout = 90f)
		{
			peer.Loaded = true;

			if (peer.WasConnected == true)
			{
				peer.ReconnectionTries--;
			}
			else
			{
				peer.ConnectionTries--;
			}

			StatusDescription = "Unloading current scene";

			UnityScene activeScene = SceneManager.GetActiveScene();

			if (IsSameScene(activeScene.path, peer.Request.ScenePath) == false && activeScene.name != _loadingScene)
			{
				Log($"Show loading scene");
				yield return ShowLoadingSceneCoroutine(true);

				bool unloadScene = true;

				for (int i = 0; i < _currentSession.GamePeers.Length; ++i)
				{
					if (activeScene == _currentSession.GamePeers[i].LoadedScene)
					{
						unloadScene = false;
						break;
					}
				}

				if (unloadScene == true)
				{
					Scene currentScene = activeScene.GetComponent<Scene>();
					if (currentScene != null)
					{
						Log($"Deinitializing Scene");
						currentScene.Deinitialize();
					}

					Log($"Unloading scene {activeScene.name}");
					yield return SceneManager.UnloadSceneAsync(activeScene);
					yield return null;
				}
			}

			float  baseTime  = Time.realtimeSinceStartup;
			float  limitTime = baseTime + connectionTimeout;
			string peerName  = $"{peer.GameMode}#{peer.ID}";

			Debug.LogWarning($"Starting {peerName} ...");
			StatusDescription = "Starting network connection";

			yield return null;

			NetworkObjectPool pool = new NetworkObjectPool();

			NetworkRunner runner = Instantiate(Global.Settings.RunnerPrefab);
			runner.name = peerName;

			runner.EnableVisibilityExtension();

			peer.Runner       = runner;
			peer.SceneManager = runner.GetComponent<NetworkSceneManager>();
			peer.LoadedScene  = default;

			StartGameArgs startGameArgs = new StartGameArgs();
			startGameArgs.GameMode                    = peer.GameMode;
			startGameArgs.SessionName                 = peer.Request.SessionName;
			startGameArgs.Scene                       = peer.Scene;
			startGameArgs.OnGameStarted               = OnGamePeerInitialized;
			startGameArgs.ObjectProvider              = pool;
			startGameArgs.CustomLobbyName             = peer.Request.CustomLobby;
			startGameArgs.SceneManager                = peer.SceneManager;
			startGameArgs.EnableClientSessionCreation = false;

			if (peer.Request.MaxPlayers > 0)
			{
				startGameArgs.PlayerCount = peer.Request.MaxPlayers;
			}

			if (peer.GameMode == GameMode.Server || peer.GameMode == GameMode.Host)
			{
				startGameArgs.SessionProperties = CreateSessionProperties(peer.Request);
			}

			if (peer.Request.IPAddress.HasValue() == true)
			{
				startGameArgs.Address = NetAddress.CreateFromIpPort(peer.Request.IPAddress, peer.Request.Port);
			}
			else if (peer.Request.Port > 0)
			{
				startGameArgs.Address = NetAddress.Any(peer.Request.Port);
			}

			Log($"NetworkRunner.StartGame()");
			var startGameTask = runner.StartGame(startGameArgs);

			while (startGameTask.IsCompleted == false)
			{
				yield return null;

				if (Time.realtimeSinceStartup >= limitTime)
				{
					Debug.LogError($"{peerName} start timeout! IsCompleted: {startGameTask.IsCompleted} IsCanceled: {startGameTask.IsCanceled} IsFaulted: {startGameTask.IsFaulted}");
					break;
				}

				if (_currentSession.ConnectionRequested == false)
				{
					Log($"Stopping coroutine (requested by user)");
					// Stop requested by user
					break;
				}
			}

			if (startGameTask.IsCanceled == true || startGameTask.IsFaulted == true || startGameTask.IsCompleted == false)
			{
				Debug.LogError($"{peerName} failed to start!");

				Log($"Starting DisconnectPeerCoroutine() - Peer {peer.ID}");
				yield return DisconnectPeerCoroutine(peer);

				_coroutine = null;
				yield break;
			}

			var result = startGameTask.Result;

			Log($"StartGame() Result: {result.ToString()} - Peer {peer.ID}");

			if (result.Ok == false)
			{
				Debug.LogError($"{peerName} failed to start! Result: {result}");

				// Probably incorrect start game parameters, go back to menu immediately
				if (Application.isBatchMode == false)
				{
					StopGame();
				}

				if (peer.WasConnected == true && result.ShutdownReason == ShutdownReason.GameNotFound)
				{
					ErrorStatus = STATUS_SERVER_CLOSED;
				}
				else
				{
					ErrorStatus = StringToLabel(result.ShutdownReason.ToString());
				}

				Log($"Starting DisconnectPeerCoroutine() - Peer {peer.ID}");
				yield return DisconnectPeerCoroutine(peer);

				_coroutine = null;
				yield break;
			}

			limitTime += loadTimeout;

			Log($"Waiting for connection - Peer {peer.ID}");
			StatusDescription = "Waiting for server connection";

			while (peer.IsConnected == false)
			{
				yield return null;

				if (Time.realtimeSinceStartup >= limitTime)
				{
					Debug.LogError($"{peerName} start timeout! IsCloudReady: {runner.IsCloudReady} IsRunning: {runner.IsRunning}");

					Log($"Starting DisconnectPeerCoroutine() - Peer {peer.ID}");
					yield return DisconnectPeerCoroutine(peer);

					_coroutine = null;
					yield break;
				}
			}

			Log($"Loading gameplay scene - Peer {peer.ID}");
			StatusDescription = "Loading gameplay scene";

			while (runner.SimulationUnityScene.IsValid() == false || runner.SimulationUnityScene.isLoaded == false)
			{
				Log($"Waiting for NetworkRunner.SimulationUnityScene - Peer {peer.ID}");
				yield return null;

				if (Time.realtimeSinceStartup >= limitTime)
				{
					Debug.LogError($"{peerName} scene load timeout!");

					Log($"Starting DisconnectPeerCoroutine() - Peer {peer.ID}");
					yield return DisconnectPeerCoroutine(peer);

					_coroutine = null;
					yield break;
				}
			}

			Debug.LogWarning($"{peerName} started in {(Time.realtimeSinceStartup - baseTime):0.00}s");

			peer.LoadedScene = runner.SimulationUnityScene;

			if (peer.ID == 0)
			{
				SceneManager.SetActiveScene(peer.LoadedScene);
			}

			StatusDescription = "Waiting for gameplay scene load";

			var scene = peer.SceneManager.GameplayScene;
			while (scene == null)
			{
				Log($"Waiting for GameplayScene - Peer {peer.ID}");

				yield return null;

				scene = peer.SceneManager.GameplayScene;

				if (Time.realtimeSinceStartup >= limitTime)
				{
					Debug.LogError($"{peerName} GameplayScene query timeout!");

					Log($"Starting DisconnectPeerCoroutine() - Peer {peer.ID}");
					yield return DisconnectPeerCoroutine(peer);

					_coroutine = null;
					yield break;
				}
			}

			Log($"Scene.PrepareContext() - Peer {peer.ID}");
			scene.PrepareContext();

			var sceneContext = scene.Context;
			sceneContext.IsVisible  = peer.ID == 0;
			sceneContext.HasInput   = peer.ID == 0;
			sceneContext.Runner     = peer.Runner;
			sceneContext.PeerUserID = peer.UserID;

			peer.Context = sceneContext;
			pool.Context = sceneContext;

			StatusDescription = "Waiting for networked game";

			var networkGame = scene.GetComponentInChildren<NetworkGame>(true);

			while (networkGame.Object == null)
			{
				Log($"Waiting for NetworkGame - Peer {peer.ID}");

				yield return null;

				if (Time.realtimeSinceStartup >= limitTime)
				{
					Debug.LogError($"{peerName} start timeout! Network game not started properly.");

					Log($"Starting DisconnectPeerCoroutine() - Peer {peer.ID}");
					yield return DisconnectPeerCoroutine(peer);

					_coroutine = null;
					yield break;
				}

				if (_currentSession.ConnectionRequested == false)
				{
					// Stop requested by user
					Log($"Starting DisconnectPeerCoroutine() - Connection is not requested anymore - Peer {peer.ID}");
					yield return DisconnectPeerCoroutine(peer);

					_coroutine = null;
					yield break;
				}
			}

			StatusDescription = "Waiting for gameplay load";

			Log($"NetworkGame.Initialize() - Peer {peer.ID}");
			networkGame.Initialize(peer.Request.GameplayType);

			while (scene.Context.GameplayMode == null)
			{
				Log($"Waiting for GameplayMode - Peer {peer.ID}");

				yield return null;

				if (Time.realtimeSinceStartup >= limitTime)
				{
					Debug.LogError($"{peerName} start timeout! Gameplay mode not started properly.");

					Log($"Starting DisconnectPeerCoroutine() - Peer {peer.ID}");
					yield return DisconnectPeerCoroutine(peer);

					_coroutine = null;
					yield break;
				}
			}

			StatusDescription = "Activating scene";

			Log($"Scene.Initialize() - Peer {peer.ID}");
			scene.Initialize();

			Log($"Scene.Activate() - Peer {peer.ID}");
			yield return scene.Activate();

			StatusDescription = "Activating network game";

			Log($"NetworkGame.Activate() - Peer {peer.ID}");
			networkGame.Activate();

			if (SceneManager.GetSceneByName(_loadingScene).IsValid() == true)
			{
				// Wait a little bit for scene activation before showing it
				yield return new WaitForSeconds(1f);

				Log($"Hide loading scene");
				yield return ShowLoadingSceneCoroutine(false);
			}

		#if ENABLE_PLAYFAB
			if (PlayFabManager.Instance != null && peer.Runner != null && peer.Runner.SessionInfo.IsValid)
			{
				string actualSessionName = peer.Runner.SessionInfo.Name;
				Log($"Updating PlayFab Status: In Game ({actualSessionName})");
				PlayFabManager.Instance.SetInGame(actualSessionName, PhotonAppSettings.Global.AppSettings.FixedRegion, peer.Request.CustomLobby, peer.Request.ScenePath, peer.Request.GameplayType);
			}
		#endif

			if (peer.WasConnected == true)
			{
				peer.ReconnectionTries++;
			}

			peer.WasConnected = true;

			_coroutine = null;

			Log($"ConnectPeerCoroutine() finished");
		}

		private IEnumerator DisconnectPeerCoroutine(GamePeer peer)
		{
			StatusDescription = "Disconnecting from server";

#if ENABLE_PLAYFAB
			if (PlayFabManager.Instance != null)
			{
				PlayFabManager.Instance.SetInMenu();
			}
#endif

			UnityScene gameplayScene = default;

			try
			{
				if (peer.Runner != null)
				{
					// Possible exception when runner tries to read config
					gameplayScene = peer.Runner.SimulationUnityScene;

					// Close and hide the room
					if (peer.Runner.IsServer == true && peer.Runner.SessionInfo != null)
					{
						Log($"Closing the room");
						peer.Runner.SessionInfo.IsOpen = false;
						peer.Runner.SessionInfo.IsVisible = false;
					}
				}
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
			}

			if (gameplayScene.IsValid() == false)
			{
				gameplayScene = peer.LoadedScene;
			}

			if (gameplayScene.IsValid() == true)
			{
				Scene scene = gameplayScene.GetComponent<Scene>(true);
				if (scene != null)
				{
					try
					{
						Log($"Deinitializing Scene");
						scene.Deinitialize();
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
					}
				}
			}

			Task shutdownTask = null;

			if (peer.Runner != null)
			{
				Debug.LogWarning($"Shutdown {peer.Runner.name} ...");

				try
				{
					shutdownTask = peer.Runner.Shutdown(true);
				}
				catch (Exception exception)
				{
					Debug.LogException(exception);
				}
			}

			Log($"Show loading scene");
			yield return ShowLoadingSceneCoroutine(true);

			if (shutdownTask != null)
			{
				float operationTimeout = 10.0f;
				while (operationTimeout > 0.0f && shutdownTask.IsCompleted == false)
				{
					yield return null;
					operationTimeout -= Time.unscaledDeltaTime;
				}
			}

			StatusDescription = "Unloading gameplay scene";

			yield return null;

			if (gameplayScene.IsValid() == true)
			{
				Debug.LogWarning($"Unloading scene {gameplayScene.name}");

				yield return SceneManager.UnloadSceneAsync(gameplayScene);
				yield return null;
			}

			peer.Loaded       = default;
			peer.Runner       = default;
			peer.SceneManager = default;
			peer.LoadedScene  = default;

			_coroutine = null;

			Log($"DisconnectPeerCoroutine() finished");
		}

		private void OnGamePeerInitialized(NetworkRunner runner)
		{
			if (NetworkProjectConfig.Global.PeerMode != NetworkProjectConfig.PeerModes.Multiple)
				return;

			Camera camera = runner.SimulationUnityScene.FindMainCamera();
			if (camera != null)
			{
				camera.gameObject.SetActive(false);
			}

			EventSystem eventSystem = runner.SimulationUnityScene.GetComponent<EventSystem>(true);
			if (eventSystem != null)
			{
				eventSystem.gameObject.SetActive(false);
			}
		}
	}
}
