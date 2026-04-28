#define ENABLE_LOGS

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Fusion;
using Fusion.Plugin;
using Fusion.Sockets;

using UnityScene = UnityEngine.SceneManagement.Scene;

namespace TPSBR
{
	public partial class Networking : MonoBehaviour
	{
		// CONSTANTS

		public const string DISPLAY_NAME_KEY = "name";
		public const string MAP_KEY  = "map";
		public const string TYPE_KEY = "type";
		public const string MODE_KEY = "mode";
		public const string STATUS_SERVER_CLOSED = "Server Closed";

		// PUBLIC MEMBERS

		public string  Status             { get; private set; }
		public string  StatusDescription  { get; private set; }
		public string  ErrorStatus        { get; private set; }

		public bool    HasSession         => _pendingSession != null || _currentSession != null;
		public bool    IsConnecting       => _pendingSession != null || (_currentSession != null && _currentSession.IsConnected == false);
		public bool    IsConnected        => _currentSession != null && _pendingSession == null && _currentSession.IsConnected == true;

		public int     PeerCount          => _currentSession != null ? _currentSession.GamePeers.SafeCount() : 0;

		// PRIVATE MEMBERS

		private Session    _pendingSession;
		private Session    _currentSession;
		private bool       _stopGameOnDisconnect;
		private string     _loadingScene;
		private Coroutine  _coroutine;

		// PUBLIC METHODS

		public void StartGame(SessionRequest request)
		{
			var session = new Session();

			if (request.ExtraPeers > 0 && NetworkProjectConfig.Global.PeerMode == NetworkProjectConfig.PeerModes.Single)
			{
				Debug.LogError("Cannot start with multiple peers. PeerMode is set to Single.");
				request.ExtraPeers = 0;
			}

			SceneRef sceneRef = default;

			int sceneIndex = SceneUtility.GetBuildIndexByScenePath(request.ScenePath);
			if (sceneIndex >= 0)
			{
				sceneRef = SceneRef.FromIndex(sceneIndex);
			}

			int totalPeers = 1 + request.ExtraPeers;
			session.GamePeers = new GamePeer[totalPeers];

			NetworkSceneInfo sceneInfo = new NetworkSceneInfo();

			if (sceneRef.IsValid == true)
			{
				sceneInfo.AddSceneRef(sceneRef, LoadSceneMode.Additive, LocalPhysicsMode.None, true);
			}

			for (int i = 0; i < totalPeers; i++)
			{
				session.GamePeers[i] = new GamePeer(i)
				{
					UserID   = i == 0 ? request.UserID : $"{request.UserID}.{i}",
					Scene    = sceneInfo,
					GameMode = i == 0 ? request.GameMode : GameMode.Client,
					Request  = request,
				};
			}

			session.ConnectionRequested = true;

			_pendingSession       = session;
			_stopGameOnDisconnect = false;

			ErrorStatus = null;

			Log($"StartGame() UserID:{request.UserID} GameMode:{request.GameMode} DisplayName:{request.DisplayName} SessionName:{request.SessionName} ScenePath:{request.ScenePath} GameplayType:{request.GameplayType} MaxPlayers:{request.MaxPlayers} ExtraPeers:{request.ExtraPeers} CustomLobby:{request.CustomLobby}");

			// REMOVED: Moved to ConnectPeerCoroutine to ensure we have the correct Session Name via Runner
			#if ENABLE_PLAYFAB
			/*
			if (PlayFabManager.Instance != null && request.GameMode == GameMode.Client)
			{
				PlayFabManager.Instance.SetInGame(request.SessionName);
			}
			*/
			#endif
		}

		public void StopGame(string errorStatus = null)
		{
			Log($"StopGame()");

			_pendingSession       = null;
			_stopGameOnDisconnect = false;

			if (_currentSession != null)
			{
				_currentSession.ConnectionRequested = false;
			}

			ErrorStatus = errorStatus;

			#if ENABLE_PLAYFAB
			// Update PlayFab status when leaving a game
			if (PlayFabManager.Instance != null)
			{
				PlayFabManager.Instance.SetInMenu();
			}
			#endif
		}

		public void StopGameOnDisconnect()
		{
			Log($"StopGameOnDisconnect()");

			_stopGameOnDisconnect = true;
		}

		public void ClearErrorStatus()
		{
			ErrorStatus = null;
		}

		// MONOBEHAVIOUR

		protected void Awake()
		{
			_loadingScene = Global.Settings.LoadingScene;
		}

		protected void Update()
		{
			if (_pendingSession != null)
			{
				if (_currentSession == null)
				{
					_currentSession = _pendingSession;
					_pendingSession = null;
				}
				else
				{
					// Request end of current session
					_currentSession.ConnectionRequested = false;
				}
			}

			UpdateCurrentSession();

			// Check if current session finished
			if (_coroutine == null && _currentSession != null && _currentSession.IsConnected == false)
			{
				if (_pendingSession == null)
				{
					Log($"Starting LoadMenuCoroutine()");
					// Current session is finished and there is no pending session, let's go to menu
					_coroutine = StartCoroutine(LoadMenuCoroutine());
				}

				_currentSession = null;
			}
		}
	}
}
