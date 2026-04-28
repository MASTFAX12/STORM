namespace TPSBR
{
	using UnityEngine;

	public static class GroundSnapBootstrap
	{
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void RegisterSceneCallbacks()
		{
			UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
			UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static void SnapLoadedScenes()
		{
			for (int i = 0, count = UnityEngine.SceneManagement.SceneManager.sceneCount; i < count; ++i)
			{
				SnapScene(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i));
			}
		}

		private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode)
		{
			SnapScene(scene);
		}

		private static void SnapScene(UnityEngine.SceneManagement.Scene scene)
		{
			int snappedCount = GroundSnapUtility.SnapSceneObjectsInScene(scene);
			if (snappedCount > 0)
			{
				Debug.Log($"[GroundSnap] Snapped {snappedCount} gameplay objects in scene '{scene.name}'.");
			}
		}
	}
}
