namespace TPSBR.UI
{
	using UnityEngine;
	using UnityEngine.UI;

	public class UIMinimap : UIWidget
	{
		// CONSTANTS

		private static readonly int     ID_CIRCLE_RADIUS = Shader.PropertyToID("_CircleRadius");
		private static readonly int     ID_CIRCLE_CENTER = Shader.PropertyToID("_CircleCenter");

		private static readonly int     ID_CUTOUT_RADIUS = Shader.PropertyToID("_CutoutRadius");
		private static readonly int     ID_CUTOUT_CENTER = Shader.PropertyToID("_CutoutCenter");

		private static readonly Vector3 MAP_CENTER_SHIFT = new Vector3(0.5f, 0f, 0.5f);

		// PRIVATE MEMBERS

		[SerializeField]
		private RawImage      _mapImage;
		[SerializeField]
		private Image         _currentShrinkArea;
		[SerializeField]
		private Image         _nextShrinkArea;

		[SerializeField]
		private RectTransform _localPlayer;
		[SerializeField]
		private RectTransform _airplane;

		private Material      _currentAreaMaterial;
		private Material      _nextAreaMaterial;

		// UIWidget INTERFACE

		protected override void OnInitialize()
		{
			base.OnInitialize();

			_currentAreaMaterial = Instantiate(_currentShrinkArea.material);
			_currentShrinkArea.material = _currentAreaMaterial;

			_nextAreaMaterial = Instantiate(_nextShrinkArea.material);
			_nextShrinkArea.material = _nextAreaMaterial;
		}

		protected override void OnDeinitialize()
		{
			if (_currentAreaMaterial != null)
			{
				Destroy(_currentAreaMaterial);
			}

			if (_nextAreaMaterial != null)
			{
				Destroy(_nextAreaMaterial);
			}
		}

		protected override void OnTick()
		{
			if (Context.Runner.Exists(Context.GameplayMode.Object) == false)
				return;

			var map        = Context.Map;
			var shrinkArea = Context.GameplayMode.ShrinkingArea;

			if (map.MapTexture == null)
				return;

			_mapImage.texture = map.MapTexture;
			_mapImage.uvRect = map.UvRect;
			var mapSize       = Mathf.Max(map.WorldDimensions.x, map.WorldDimensions.y);
			var mapCenter     = map.transform.position;
			var mapUvRect     = map.UvRect;

			if (shrinkArea != null)
			{
				_currentShrinkArea.SetActive(true);

				var currentCenter = RemapToDisplayedMap((shrinkArea.Center - mapCenter) / mapSize + MAP_CENTER_SHIFT, mapUvRect);

				if (shrinkArea.IsAnnounced == true)
				{
					_nextShrinkArea.SetActive(true);

					var nextCenter = RemapToDisplayedMap((shrinkArea.ShrinkCenter - mapCenter) / mapSize + MAP_CENTER_SHIFT, mapUvRect);

					_nextAreaMaterial.SetFloat(ID_CIRCLE_RADIUS, (shrinkArea.ShrinkRadius / mapSize) / Mathf.Max(0.0001f, mapUvRect.width));
					_nextAreaMaterial.SetVector(ID_CIRCLE_CENTER, new Vector4(nextCenter.x, nextCenter.z, 0f, 0f));

					_nextAreaMaterial.SetFloat(ID_CUTOUT_RADIUS, (shrinkArea.Radius / mapSize) / Mathf.Max(0.0001f, mapUvRect.width));
					_nextAreaMaterial.SetVector(ID_CUTOUT_CENTER, new Vector4(currentCenter.x, currentCenter.z, 0f, 0f));

				}
				else
				{
					_nextShrinkArea.SetActive(false);
				}

				_currentAreaMaterial.SetFloat(ID_CIRCLE_RADIUS, (shrinkArea.Radius / mapSize) / Mathf.Max(0.0001f, mapUvRect.width));
				_currentAreaMaterial.SetVector(ID_CIRCLE_CENTER, new Vector4(currentCenter.x, currentCenter.z, 0f, 0f));
			}
			else
			{
				_currentShrinkArea.SetActive(false);
				_nextShrinkArea.SetActive(false);
			}

			var playerTransform = Context.ObservedAgent != null ? Context.ObservedAgent.transform : Context.WaitingAgentTransform;
			if (playerTransform != null)
			{
				_localPlayer.SetActive(true);
				UpdateMinimapObject(_localPlayer, playerTransform);
			}
			else
			{
				_localPlayer.SetActive(false);
			}

			if (Context.GameplayMode is BattleRoyaleGameplayMode battleRoyale && battleRoyale.Airplane != null)
			{
				_airplane.SetActive(true);
				UpdateMinimapObject(_airplane, battleRoyale.Airplane.transform);
			}
			else
			{
				_airplane.SetActive(false);
			}
		}

		// PRIVATE METHODS

		private void UpdateMinimapObject(RectTransform minimapObject, Transform objectTransform)
		{
			var map = Context.Map;
			int mapSize = Mathf.Max(map.WorldDimensions.x, map.WorldDimensions.y);

			var objectPosition = RemapToDisplayedMap((objectTransform.position - map.transform.position) / mapSize + MAP_CENTER_SHIFT, map.UvRect) - MAP_CENTER_SHIFT;

			minimapObject.localPosition = new Vector2(objectPosition.x * RectTransform.sizeDelta.x, objectPosition.z * RectTransform.sizeDelta.y);
			minimapObject.rotation  = Quaternion.Euler(0f, 0f, -objectTransform.rotation.eulerAngles.y);
		}

		private Vector3 RemapToDisplayedMap(Vector3 normalizedPosition, Rect uvRect)
		{
			float width = Mathf.Max(0.0001f, uvRect.width);
			float height = Mathf.Max(0.0001f, uvRect.height);

			return new Vector3(
				(normalizedPosition.x - uvRect.xMin) / width,
				normalizedPosition.y,
				(normalizedPosition.z - uvRect.yMin) / height
			);
		}
	}
}
