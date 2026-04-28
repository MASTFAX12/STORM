namespace TPSBR
{
	using System.Collections.Generic;
	using UnityEngine;

	/// <summary>
	/// Auto-detects the playable area bounds from Terrain components in the scene.
	/// Used by Airplane system for dynamic path generation and general map bounds checks.
	/// </summary>
	public class MapPlayArea : ContextBehaviour
	{
		[Tooltip("Manual override for map bounds. If empty (size 0), bounds are auto-calculated from active terrains.")]
		public Bounds ManualBounds;

		[Tooltip("How far out to consider valid playable area.")]
		public float PlayAreaPadding = 50f;

		[Tooltip("Extra inset used for safe player spawns and no-swim boundary walls.")]
		public float SafeInset = 30f;
		[Tooltip("If set, playable terrains under this root name are preferred over all other terrains.")]
		public string PreferredTerrainRootName = "Environment";

		private Bounds _calculatedBounds;
		private Bounds _terrainBounds;
		private Bounds _visualBounds;
		private bool _hasCalculatedBounds;

		private Terrain[] _cachedTerrains;
		private Terrain[] _playableTerrains;

		public Bounds PlayBounds
		{
			get
			{
				if (ManualBounds.size.sqrMagnitude > 0)
					return ManualBounds;

				if (!_hasCalculatedBounds)
					CalculateBounds();

				return _calculatedBounds;
			}
		}

		private void Awake()
		{
			CalculateBounds();
		}

		private void CalculateBounds()
		{
			_cachedTerrains = FindObjectsByType<Terrain>(FindObjectsSortMode.None);
			_playableTerrains = GetPlayableTerrains(_cachedTerrains);

			if (_playableTerrains.Length == 0)
			{
				_calculatedBounds = new Bounds(transform.position, new Vector3(500, 500, 500));
				_hasCalculatedBounds = true;
				return;
			}

			bool first = true;
			foreach (var terrain in _playableTerrains)
			{
				if (!terrain.isActiveAndEnabled) continue;

				Vector3 size = terrain.terrainData.size;
				Vector3 pos = terrain.transform.position;
				Bounds tb = new Bounds(pos + size * 0.5f, size);

				if (first)
				{
					_terrainBounds = tb;
					_calculatedBounds = tb;
					first = false;
				}
				else
				{
					_terrainBounds.Encapsulate(tb);
					_calculatedBounds.Encapsulate(tb);
				}
			}

			CalculateVisualBounds();

			// Add padding
			_calculatedBounds.Expand(PlayAreaPadding * 2f);
			_hasCalculatedBounds = true;
		}

		public Bounds GetPlayableVisualBounds(bool makeSquare = false)
		{
			if (_hasCalculatedBounds == false)
			{
				CalculateBounds();
			}

			Bounds bounds = _visualBounds.size.sqrMagnitude > 0f ? _visualBounds : GetPlayableTerrainBounds();
			return makeSquare == true ? ToSquareBounds(bounds) : bounds;
		}

		public float GetTerrainHeight(Vector3 position, float fallbackHeight = 0f)
		{
			return TryGetTerrainHeight(position, out float height) ? height : fallbackHeight;
		}

		public Bounds GetPlayableTerrainBounds()
		{
			if (_hasCalculatedBounds == false)
			{
				CalculateBounds();
			}

			if (ManualBounds.size.sqrMagnitude > 0f)
				return ManualBounds;

			return _terrainBounds.size.sqrMagnitude > 0f ? _terrainBounds : _calculatedBounds;
		}

		public Bounds GetSafeBounds(float extraInset = 0f)
		{
			Bounds bounds = GetPlayableTerrainBounds();
			float totalInset = Mathf.Max(0f, extraInset);

			if (ManualBounds.size.sqrMagnitude <= 0f)
			{
				totalInset += PlayAreaPadding;
			}

			totalInset += SafeInset;

			Vector3 size = bounds.size;
			size.x = Mathf.Max(10f, size.x - totalInset * 2f);
			size.z = Mathf.Max(10f, size.z - totalInset * 2f);
			bounds.size = size;

			return bounds;
		}

		public bool TryGetSafeGroundPosition(Vector3 position, out Vector3 safePosition, float extraInset = 0f, float heightOffset = 0.1f)
		{
			Bounds safeBounds = GetSafeBounds(extraInset);
			Vector3 clampedPosition = position;
			clampedPosition.x = Mathf.Clamp(clampedPosition.x, safeBounds.min.x, safeBounds.max.x);
			clampedPosition.z = Mathf.Clamp(clampedPosition.z, safeBounds.min.z, safeBounds.max.z);

			if (TryGetTerrainHeight(clampedPosition, out float height))
			{
				safePosition = new Vector3(clampedPosition.x, height + heightOffset, clampedPosition.z);
				return true;
			}

			float radiusStep = Mathf.Max(8f, SafeInset * 0.5f);
			const int samplesPerRing = 12;

			for (int ring = 1; ring <= 8; ring++)
			{
				float radius = ring * radiusStep;

				for (int sample = 0; sample < samplesPerRing; sample++)
				{
					float angle = sample / (float)samplesPerRing * Mathf.PI * 2f;
					Vector3 candidate = clampedPosition + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
					candidate.x = Mathf.Clamp(candidate.x, safeBounds.min.x, safeBounds.max.x);
					candidate.z = Mathf.Clamp(candidate.z, safeBounds.min.z, safeBounds.max.z);

					if (TryGetTerrainHeight(candidate, out height))
					{
						safePosition = new Vector3(candidate.x, height + heightOffset, candidate.z);
						return true;
					}
				}
			}

			if (TryGetTerrainHeight(safeBounds.center, out height))
			{
				safePosition = new Vector3(safeBounds.center.x, height + heightOffset, safeBounds.center.z);
				return true;
			}

			safePosition = default;
			return false;
		}

		public bool TryGetTerrainHeight(Vector3 position, out float height)
		{
			if (_cachedTerrains == null || _playableTerrains == null)
			{
				_cachedTerrains = FindObjectsByType<Terrain>(FindObjectsSortMode.None);
				_playableTerrains = GetPlayableTerrains(_cachedTerrains);
			}

			float highestPoint = float.MinValue;
			bool foundTerrain = false;

			foreach (var terrain in _playableTerrains)
			{
				if (terrain == null || !terrain.isActiveAndEnabled)
					continue;

				Vector3 tPos = terrain.transform.position;
				Vector3 tSize = terrain.terrainData.size;

				if (position.x >= tPos.x && position.x <= tPos.x + tSize.x &&
				    position.z >= tPos.z && position.z <= tPos.z + tSize.z)
				{
					float sampleHeight = terrain.SampleHeight(position) + tPos.y;
					if (sampleHeight > highestPoint)
					{
						highestPoint = sampleHeight;
						foundTerrain = true;
					}
				}
			}

			height = foundTerrain ? highestPoint : default;
			return foundTerrain;
		}

		private Terrain[] GetPlayableTerrains(Terrain[] terrains)
		{
			if (terrains == null || terrains.Length == 0)
				return System.Array.Empty<Terrain>();

			List<Terrain> preferredTerrains = new List<Terrain>(terrains.Length);
			List<Terrain> fallbackTerrains = new List<Terrain>(terrains.Length);

			for (int i = 0; i < terrains.Length; i++)
			{
				Terrain terrain = terrains[i];
				if (terrain == null || terrain.isActiveAndEnabled == false)
					continue;
				if (IsIgnoredTerrain(terrain) == true)
					continue;

				fallbackTerrains.Add(terrain);

				if (HasAncestorNamed(terrain.transform, PreferredTerrainRootName) == true)
				{
					preferredTerrains.Add(terrain);
				}
			}

			if (preferredTerrains.Count > 0)
				return preferredTerrains.ToArray();

			return fallbackTerrains.ToArray();
		}

		private void CalculateVisualBounds()
		{
			_visualBounds = default;
			bool hasVisualBounds = false;

			Transform[] transforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
			for (int i = 0; i < transforms.Length; i++)
			{
				Transform current = transforms[i];
				if (current == null || current.gameObject.activeInHierarchy == false)
					continue;
				if (HasAncestorNamed(current, PreferredTerrainRootName) == false)
					continue;
				if (IsIgnoredEnvironmentObject(current) == true)
					continue;

				Renderer renderer = current.GetComponent<Renderer>();
				if (renderer == null || renderer.enabled == false)
					continue;
				if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
					continue;

				if (hasVisualBounds == false)
				{
					_visualBounds = renderer.bounds;
					hasVisualBounds = true;
				}
				else
				{
					_visualBounds.Encapsulate(renderer.bounds);
				}
			}

			for (int i = 0; i < _playableTerrains.Length; i++)
			{
				Terrain terrain = _playableTerrains[i];
				if (terrain == null || terrain.isActiveAndEnabled == false)
					continue;

				Vector3 size = terrain.terrainData.size;
				Vector3 pos = terrain.transform.position;
				Bounds terrainBounds = new Bounds(pos + size * 0.5f, size);

				if (hasVisualBounds == false)
				{
					_visualBounds = terrainBounds;
					hasVisualBounds = true;
				}
				else
				{
					_visualBounds.Encapsulate(terrainBounds);
				}
			}
		}

		private bool IsIgnoredTerrain(Terrain terrain)
		{
			string terrainName = terrain.name.ToLowerInvariant();
			if (terrainName.Contains("water"))
				return true;

			Transform current = terrain.transform.parent;
			while (current != null)
			{
				if (current.name.ToLowerInvariant().Contains("water"))
					return true;

				current = current.parent;
			}

			return false;
		}

		private bool IsIgnoredEnvironmentObject(Transform transform)
		{
			Transform current = transform;
			while (current != null)
			{
				string name = current.name.ToLowerInvariant();
				if (name.Contains("water"))
					return true;
				if (name.Contains("particle"))
					return true;
				if (name.Contains("vfx"))
					return true;

				current = current.parent;
			}

			return false;
		}

		private Bounds ToSquareBounds(Bounds bounds)
		{
			float size = Mathf.Max(bounds.size.x, bounds.size.z);
			Vector3 squaredSize = bounds.size;
			squaredSize.x = size;
			squaredSize.z = size;
			bounds.size = squaredSize;
			return bounds;
		}

		private bool HasAncestorNamed(Transform transform, string rootName)
		{
			if (string.IsNullOrWhiteSpace(rootName) == true)
				return false;

			Transform current = transform;
			while (current != null)
			{
				if (current.name == rootName)
					return true;

				current = current.parent;
			}

			return false;
		}

		private void OnDrawGizmos()
		{
			Gizmos.color = new Color(0, 1, 0, 0.3f);
			Gizmos.DrawWireCube(PlayBounds.center, PlayBounds.size);
			
			Gizmos.color = new Color(0, 1, 0, 0.1f);
			Gizmos.DrawCube(new Vector3(PlayBounds.center.x, PlayBounds.min.y, PlayBounds.center.z), new Vector3(PlayBounds.size.x, 1f, PlayBounds.size.z));
		}
	}
}
