namespace TPSBR.Editor
{
	using UnityEditor;
	using UnityEditor.SceneManagement;
	using UnityEngine;

	public static class GroundSnapEditor
	{
		[MenuItem("BR200/Snap Loot Crates To Ground")]
		public static void SnapActiveSceneLootCrates()
		{
			UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
			int snappedCount = GroundSnapUtility.SnapSceneObjectsInScene(scene);

			if (snappedCount > 0)
			{
				EditorSceneManager.MarkSceneDirty(scene);
				EditorSceneManager.SaveScene(scene);
			}

			Debug.Log($"[GroundSnap] Snapped {snappedCount} gameplay objects in scene '{scene.name}'.");
		}

		public static void SnapGenAreaGreenSceneAndSave()
		{
			string scenePath = "Assets/TPSBR/Scenes/GenAreaGreen.unity";
			UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

			int snappedCount = GroundSnapUtility.SnapSceneObjectsInScene(scene);
			EditorSceneManager.MarkSceneDirty(scene);
			EditorSceneManager.SaveScene(scene);
			AssetDatabase.SaveAssets();

			Debug.Log($"[GroundSnap] Snapped {snappedCount} gameplay objects in scene '{scene.name}' and saved '{scenePath}'.");
		}
	}
}
