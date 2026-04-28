namespace TPSBR
{
	using UnityEngine;
	using UnityEngine.SceneManagement;
	using UnityScene = UnityEngine.SceneManagement.Scene;

	/// <summary>
	/// Creates hidden physical walls around the playable land so players cannot enter the sea or fall off-map.
	/// </summary>
	public sealed class MapBoundaryBootstrap : MonoBehaviour
	{
		[SerializeField] private float _height = 420f;
		[SerializeField] private float _thickness = 28f;
		[SerializeField] private float _verticalCenter = 140f;
		[SerializeField] private float _edgeInset = 4f;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static void Initialize()
		{
			UnityScene scene = SceneManager.GetActiveScene();
			if (!scene.isLoaded)
				return;

			MapPlayArea playArea = Object.FindFirstObjectByType<MapPlayArea>();
			if (playArea == null)
				return;

			if (Object.FindFirstObjectByType<MapBoundaryBootstrap>() != null)
				return;

			GameObject root = new GameObject("RuntimeMapBoundaries");
			var bootstrap = root.AddComponent<MapBoundaryBootstrap>();
			bootstrap.Build(playArea);
		}

		private void Build(MapPlayArea playArea)
		{
			if (playArea == null)
				return;

			Bounds bounds = playArea.PlayBounds;
			bounds.Expand(new Vector3(-_edgeInset * 2f, 0f, -_edgeInset * 2f));
			transform.position = Vector3.zero;

			CreateWall("North", new Vector3(bounds.center.x, _verticalCenter, bounds.max.z + _thickness * 0.5f), new Vector3(bounds.size.x + _thickness * 2f, _height, _thickness));
			CreateWall("South", new Vector3(bounds.center.x, _verticalCenter, bounds.min.z - _thickness * 0.5f), new Vector3(bounds.size.x + _thickness * 2f, _height, _thickness));
			CreateWall("East",  new Vector3(bounds.max.x + _thickness * 0.5f, _verticalCenter, bounds.center.z), new Vector3(_thickness, _height, bounds.size.z + _thickness * 2f));
			CreateWall("West",  new Vector3(bounds.min.x - _thickness * 0.5f, _verticalCenter, bounds.center.z), new Vector3(_thickness, _height, bounds.size.z + _thickness * 2f));
		}

		private void CreateWall(string wallName, Vector3 position, Vector3 size)
		{
			GameObject wall = new GameObject($"Boundary_{wallName}");
			wall.transform.SetParent(transform, false);
			wall.transform.position = position;
			wall.layer = 0;

			BoxCollider collider = wall.AddComponent<BoxCollider>();
			collider.size = size;
			collider.isTrigger = false;
		}
	}
}
