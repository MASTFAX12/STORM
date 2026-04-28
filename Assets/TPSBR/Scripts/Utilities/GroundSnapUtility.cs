namespace TPSBR
{
	using System;
	using System.Collections.Generic;
	using UnityEngine;

	public static class GroundSnapUtility
	{
		private const float RaycastStartHeight = 256.0f;
		private const float RaycastDistance = 1024.0f;
		private const float SurfaceClearance = 0.02f;
		private const float MinHoverDistance = 0.25f;
		private const float DefaultSearchRadiusStep = 4.0f;
		private const int DefaultSearchSamplesPerRing = 10;

		private static readonly RaycastHit[] _raycastHits = new RaycastHit[64];
		private static readonly List<Transform> _sceneTransforms = new List<Transform>(256);

		public static int SnapSceneObjectsInScene(UnityEngine.SceneManagement.Scene scene)
		{
			if (scene.IsValid() == false || scene.isLoaded == false)
				return 0;

			_sceneTransforms.Clear();

			GameObject[] rootObjects = scene.GetRootGameObjects();
			for (int i = 0; i < rootObjects.Length; ++i)
			{
				CollectTransforms(rootObjects[i].transform, _sceneTransforms);
			}

			int snappedCount = 0;

			for (int i = 0; i < _sceneTransforms.Count; ++i)
			{
				Transform transform = _sceneTransforms[i];
				if (ShouldSnapInScene(transform) == false)
					continue;

				if (TrySnapToGround(transform, true) == true)
				{
					++snappedCount;
				}
			}

			return snappedCount;
		}

		public static bool TryGetGroundPosition(Vector3 targetPosition, out Vector3 groundedPosition, float clearance = SurfaceClearance)
		{
			MapPlayArea playArea = UnityEngine.Object.FindFirstObjectByType<MapPlayArea>();
			if (playArea != null && playArea.TryGetSafeGroundPosition(targetPosition, out groundedPosition, 0f, clearance))
				return true;

			Vector3 rayOrigin = targetPosition + Vector3.up * RaycastStartHeight;
			int hitCount = Physics.RaycastNonAlloc(rayOrigin, Vector3.down, _raycastHits, RaycastDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
			if (hitCount <= 0)
			{
				groundedPosition = default;
				return false;
			}

			Array.Sort(_raycastHits, 0, hitCount, RaycastHitDistanceComparer.Instance);

			for (int i = 0; i < hitCount; ++i)
			{
				RaycastHit hit = _raycastHits[i];
				if (hit.collider == null)
					continue;

				groundedPosition = hit.point + hit.normal * clearance;
				return true;
			}

			groundedPosition = default;
			return false;
		}

		public static bool TrySnapToGround(Transform target, bool alignToSurface)
		{
			if (target == null)
				return false;

			Vector3 rayOrigin = target.position + Vector3.up * RaycastStartHeight;
			int hitCount = Physics.RaycastNonAlloc(rayOrigin, Vector3.down, _raycastHits, RaycastDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
			if (hitCount <= 0)
				return false;

			Array.Sort(_raycastHits, 0, hitCount, RaycastHitDistanceComparer.Instance);

			for (int i = 0; i < hitCount; ++i)
			{
				RaycastHit hit = _raycastHits[i];
				Collider collider = hit.collider;

				if (collider == null)
					continue;

				if (collider.transform == target || collider.transform.IsChildOf(target))
					continue;

				Quaternion snappedRotation = alignToSurface == true ? GetAlignedRotation(target, hit.normal) : target.rotation;

				Vector3 originalPosition = target.position;
				Quaternion originalRotation = target.rotation;

				target.SetPositionAndRotation(hit.point, snappedRotation);

				if (TryGetBottomOffset(target, out float bottomOffset) == true)
				{
					target.position += Vector3.up * (bottomOffset + SurfaceClearance);
				}
				else
				{
					target.position += hit.normal * SurfaceClearance;
				}

				bool changed = Vector3.Distance(originalPosition, target.position) > 0.001f ||
				               Quaternion.Angle(originalRotation, target.rotation) > 0.1f;

				return changed;
			}

			return false;
		}

		public static bool TryGetNonOverlappingGroundPosition(Vector3 targetPosition, IReadOnlyList<Vector3> occupiedPositions, float minSpacing, out Vector3 groundedPosition)
		{
			if (TryGetGroundPosition(targetPosition, out groundedPosition) == true && IsFarEnoughFromOccupied(groundedPosition, occupiedPositions, minSpacing) == true)
				return true;

			float stepRadius = Mathf.Max(DefaultSearchRadiusStep, minSpacing * 0.75f);

			for (int ring = 1; ring <= 8; ring++)
			{
				float radius = ring * stepRadius;
				for (int sample = 0; sample < DefaultSearchSamplesPerRing; sample++)
				{
					float angle = sample / (float)DefaultSearchSamplesPerRing * Mathf.PI * 2f;
					Vector3 candidate = targetPosition + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

					if (TryGetGroundPosition(candidate, out groundedPosition) == false)
						continue;

					if (IsFarEnoughFromOccupied(groundedPosition, occupiedPositions, minSpacing) == false)
						continue;

					return true;
				}
			}

			groundedPosition = default;
			return false;
		}

		private static bool ShouldSnapInScene(Transform transform)
		{
			if (transform == null || transform.gameObject.activeInHierarchy == false)
				return false;

			if (transform.GetComponent<ItemBox>() != null)
				return true;

			if (transform.GetComponent<StaticPickup>() != null)
				return true;

			if (transform.name.StartsWith("ItemBox", StringComparison.Ordinal) == true)
				return true;

			return false;
		}

		public static bool ShouldSnapAtRuntime(Transform transform)
		{
			if (transform == null || transform.gameObject.activeInHierarchy == false)
				return false;

			if (transform.GetComponent<ItemBox>() == null && transform.GetComponent<StaticPickup>() == null)
				return false;

			if (TryGetGroundPosition(transform.position, out Vector3 groundedPosition) == false)
				return false;

			return transform.position.y - groundedPosition.y > MinHoverDistance;
		}

		private static bool IsFarEnoughFromOccupied(Vector3 candidate, IReadOnlyList<Vector3> occupiedPositions, float minSpacing)
		{
			if (occupiedPositions == null || occupiedPositions.Count == 0)
				return true;

			float minSqrSpacing = minSpacing * minSpacing;
			for (int i = 0; i < occupiedPositions.Count; i++)
			{
				Vector3 occupied = occupiedPositions[i];
				Vector2 candidateXZ = new Vector2(candidate.x, candidate.z);
				Vector2 occupiedXZ = new Vector2(occupied.x, occupied.z);

				if ((candidateXZ - occupiedXZ).sqrMagnitude < minSqrSpacing)
					return false;
			}

			return true;
		}

		private static void CollectTransforms(Transform root, List<Transform> transforms)
		{
			transforms.Add(root);

			for (int i = 0, count = root.childCount; i < count; ++i)
			{
				CollectTransforms(root.GetChild(i), transforms);
			}
		}

		private static Quaternion GetAlignedRotation(Transform target, Vector3 normal)
		{
			Vector3 forwardOnPlane = Vector3.ProjectOnPlane(target.forward, normal);
			if (forwardOnPlane.sqrMagnitude < 0.0001f)
			{
				forwardOnPlane = Vector3.ProjectOnPlane(target.right, normal);
			}

			if (forwardOnPlane.sqrMagnitude < 0.0001f)
			{
				forwardOnPlane = Vector3.Cross(normal, Vector3.right);
			}

			if (forwardOnPlane.sqrMagnitude < 0.0001f)
			{
				forwardOnPlane = Vector3.forward;
			}

			return Quaternion.LookRotation(forwardOnPlane.normalized, normal);
		}

		private static bool TryGetBottomOffset(Transform target, out float bottomOffset)
		{
			bottomOffset = 0.0f;

			Collider[] colliders = target.GetComponentsInChildren<Collider>(false);
			Bounds bounds = default;
			bool hasBounds = false;

			for (int i = 0; i < colliders.Length; ++i)
			{
				Collider collider = colliders[i];
				if (collider == null || collider.enabled == false)
					continue;

				if (hasBounds == false)
				{
					bounds = collider.bounds;
					hasBounds = true;
				}
				else
				{
					bounds.Encapsulate(collider.bounds);
				}
			}

			if (hasBounds == false)
			{
				Renderer[] renderers = target.GetComponentsInChildren<Renderer>(false);
				for (int i = 0; i < renderers.Length; ++i)
				{
					Renderer renderer = renderers[i];
					if (renderer == null || renderer.enabled == false)
						continue;

					if (hasBounds == false)
					{
						bounds = renderer.bounds;
						hasBounds = true;
					}
					else
					{
						bounds.Encapsulate(renderer.bounds);
					}
				}
			}

			if (hasBounds == false)
				return false;

			bottomOffset = target.position.y - bounds.min.y;
			return true;
		}

		private sealed class RaycastHitDistanceComparer : IComparer<RaycastHit>
		{
			public static readonly RaycastHitDistanceComparer Instance = new RaycastHitDistanceComparer();

			public int Compare(RaycastHit x, RaycastHit y)
			{
				return x.distance.CompareTo(y.distance);
			}
		}
	}
}
