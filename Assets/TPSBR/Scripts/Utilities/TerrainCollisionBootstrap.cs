namespace TPSBR
{
	using UnityEngine;

	/// <summary>
	/// Fixes terrain collision for KCC by generating a MeshCollider from the terrain heightmap.
	/// The KCC (Kinematic Character Controller) uses capsule overlaps which don't always
	/// interact correctly with TerrainCollider. MeshCollider works reliably.
	/// Attach this to each Terrain GameObject that has collision issues (e.g. LandTerrain).
	/// </summary>
	[RequireComponent(typeof(Terrain))]
	[RequireComponent(typeof(TerrainCollider))]
	public sealed class TerrainCollisionBootstrap : MonoBehaviour
	{
		[Header("Mesh Collider Settings")]
		[Tooltip("Resolution of the generated mesh collider (lower = faster, higher = more accurate). 64-128 recommended.")]
		[SerializeField] private int _meshResolution = 64;

		[Tooltip("Disable the original TerrainCollider after adding MeshCollider")]
		[SerializeField] private bool _disableTerrainCollider = true;

		[Header("Safety")]
		[Tooltip("Enable player rescue system")]
		[SerializeField] private bool _enablePlayerRescue = true;

		[Tooltip("How far below terrain surface before rescue triggers")]
		[SerializeField] private float _rescueThreshold = -5f;

		[Tooltip("Height above terrain to teleport rescued player")]
		[SerializeField] private float _rescueHeight = 3f;

		private Terrain _terrain;
		private TerrainCollider _terrainCollider;
		private MeshCollider _meshCollider;

		private void Awake()
		{
			_terrain = GetComponent<Terrain>();
			_terrainCollider = GetComponent<TerrainCollider>();

			if (_terrain == null || _terrainCollider == null)
				return;

			GenerateTerrainMeshCollider();

			Debug.Log($"[TerrainSafety] {gameObject.name}: MeshCollider generated from terrain heightmap ({_meshResolution}x{_meshResolution} resolution).");
		}

		private void GenerateTerrainMeshCollider()
		{
			TerrainData terrainData = _terrain.terrainData;
			Vector3 terrainSize = terrainData.size;
			Vector3 terrainPos = transform.position;

			int res = Mathf.Clamp(_meshResolution, 8, 256);

			// Generate mesh vertices from terrain heightmap
			Vector3[] vertices = new Vector3[(res + 1) * (res + 1)];
			int[] triangles = new int[res * res * 6];

			for (int z = 0; z <= res; z++)
			{
				for (int x = 0; x <= res; x++)
				{
					float normalizedX = (float)x / res;
					float normalizedZ = (float)z / res;

					// Sample terrain height at this normalized position
					float height = terrainData.GetInterpolatedHeight(normalizedX, normalizedZ);

					// Vertex position in LOCAL space relative to terrain origin
					vertices[z * (res + 1) + x] = new Vector3(
						normalizedX * terrainSize.x,
						height,
						normalizedZ * terrainSize.z
					);
				}
			}

			// Generate triangle indices
			int triIndex = 0;
			for (int z = 0; z < res; z++)
			{
				for (int x = 0; x < res; x++)
				{
					int bottomLeft = z * (res + 1) + x;
					int bottomRight = bottomLeft + 1;
					int topLeft = (z + 1) * (res + 1) + x;
					int topRight = topLeft + 1;

					// First triangle
					triangles[triIndex++] = bottomLeft;
					triangles[triIndex++] = topLeft;
					triangles[triIndex++] = topRight;

					// Second triangle
					triangles[triIndex++] = bottomLeft;
					triangles[triIndex++] = topRight;
					triangles[triIndex++] = bottomRight;
				}
			}

			// Create mesh
			Mesh terrainMesh = new Mesh();
			terrainMesh.name = $"{gameObject.name}_CollisionMesh";

			// Use 32-bit indices if vertex count exceeds 16-bit limit
			if (vertices.Length > 65535)
				terrainMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

			terrainMesh.vertices = vertices;
			terrainMesh.triangles = triangles;
			terrainMesh.RecalculateNormals();
			terrainMesh.RecalculateBounds();

			// Add MeshCollider
			_meshCollider = gameObject.AddComponent<MeshCollider>();
			_meshCollider.sharedMesh = terrainMesh;
			_meshCollider.convex = false; // Must be non-convex for terrain

			// Optionally disable the original TerrainCollider
			if (_disableTerrainCollider)
			{
				_terrainCollider.enabled = false;
				Debug.Log($"[TerrainSafety] {gameObject.name}: Original TerrainCollider disabled, using MeshCollider instead.");
			}
		}

		private void OnDestroy()
		{
			if (_meshCollider != null && _meshCollider.sharedMesh != null)
			{
				Destroy(_meshCollider.sharedMesh);
			}
		}
	}
}
