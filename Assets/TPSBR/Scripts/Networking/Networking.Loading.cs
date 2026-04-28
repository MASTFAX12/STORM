namespace TPSBR
{
	using System.Collections;
	using UnityEngine;
	using UnityEngine.SceneManagement;

	public partial class Networking
	{
		private IEnumerator ShowLoadingSceneCoroutine(bool show, float additionalTime = 1f)
		{
			var loadingScene = SceneManager.GetSceneByName(_loadingScene);

			if (loadingScene.IsValid() == false)
			{
				yield return SceneManager.LoadSceneAsync(_loadingScene, LoadSceneMode.Additive);
				loadingScene = SceneManager.GetSceneByName(_loadingScene);
			}

			if (show == false && additionalTime > 0f)
			{
				// Wait additional time till fade out starts
				yield return new WaitForSeconds(additionalTime);
			}

			yield return null;

			var loadingSceneObject = loadingScene.GetComponent<LoadingScene>();
			if (loadingSceneObject != null)
			{
				if (show == true)
				{
					loadingSceneObject.FadeIn();
				}
				else
				{
					loadingSceneObject.FadeOut();
				}

				while (loadingSceneObject.IsFading == true)
					yield return null;
			}

			if (show == true && additionalTime > 0f)
			{
				// Wait additional time after fade in
				yield return new WaitForSeconds(additionalTime);
			}

			if (show == false)
			{
				yield return SceneManager.UnloadSceneAsync(loadingScene);
			}
		}

		private IEnumerator LoadMenuCoroutine()
		{
			string menuSceneName = Global.Settings.MenuScene;

			if (SceneManager.sceneCount == 1 && SceneManager.GetSceneAt(0).name == menuSceneName)
			{
				_coroutine = null;
				yield break;
			}

			StatusDescription = "Unloading gameplay scenes";

			yield return ShowLoadingSceneCoroutine(true);

			for (int i = SceneManager.sceneCount - 1; i >= 0; --i)
			{
				var scene = SceneManager.GetSceneAt(i);

				if (scene.name != _loadingScene)
				{
					yield return SceneManager.UnloadSceneAsync(scene);
				}
			}

			StatusDescription = "Loading menu scene";
			yield return null;

			yield return SceneManager.LoadSceneAsync(menuSceneName, LoadSceneMode.Additive);
			yield return ShowLoadingSceneCoroutine(false);

			SceneManager.SetActiveScene(SceneManager.GetSceneByName(menuSceneName));

			_coroutine = null;
		}

		private static bool IsSameScene(string assetPath, string scenePath)
		{
			return assetPath == $"Assets/{scenePath}.unity";
		}
	}
}
