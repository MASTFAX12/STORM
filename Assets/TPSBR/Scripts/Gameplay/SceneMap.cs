namespace TPSBR
{
	using UnityEngine;

	public class SceneMap : SceneService
	{
		// PUBLIC MEMBERS

		public RenderTexture MapTexture      => _mapTexture;
		public Vector2Int    WorldDimensions => _worldDimensions;
		public Rect          UvRect          => _uvRect;

		// PRIVATE MEMBERS

		[SerializeField]
		private Vector2Int    _worldDimensions;
		[SerializeField]
		private int           _prefferedResolution = 1024;
		[SerializeField]
		private Camera        _camera;

		private RenderTexture _mapTexture;
		private bool          _parametersOverridden;
		private Rect          _uvRect = new Rect(0f, 0f, 1f, 1f);

		// PUBLIC METHODS

		public void OverrideParameters(Vector3 center, Vector2Int worldDimensions)
		{
			center.y = 0f;

			transform.position = center;
			_worldDimensions = worldDimensions;
			_parametersOverridden = true;

			Regenerate();
		}

		// SceneService INTERFACE

		protected override void OnInitialize()
		{
			_camera.enabled = false;
		}

		protected override void OnDeactivate()
		{
			if (_mapTexture != null)
			{
				Destroy(_mapTexture);
			}
		}

		protected override void OnActivate()
		{
			TrySyncWithMapPlayArea();
			Regenerate();
		}

		// MonoBehaviour INTERFACE

		private void OnDrawGizmosSelected()
		{
			var tmpColor = Gizmos.color;
			Gizmos.color = Color.blue;

			Gizmos.DrawWireCube(transform.position, new Vector3(_worldDimensions.x, 100f, _worldDimensions.y));

			Gizmos.color = tmpColor;
		}

		// PRIVATE MEMBERS

		private void Regenerate()
		{
			if (_worldDimensions == Vector2Int.zero)
				return;

			_uvRect = new Rect(0f, 0f, 1f, 1f);

			if (_mapTexture != null)
			{
				Destroy(_mapTexture);
				_mapTexture = null;
			}

			int squareSize = Mathf.Max(_worldDimensions.x, _worldDimensions.y);
			int resolutionPerMeter = Mathf.Max(1, Mathf.RoundToInt(_prefferedResolution / (float)squareSize));

			_mapTexture = new RenderTexture(squareSize * resolutionPerMeter, squareSize * resolutionPerMeter, 8, UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm);
			_mapTexture.name = "MapTexture";
			_camera.targetTexture = _mapTexture;
			_camera.orthographicSize = squareSize / 2f;

			if (Application.isBatchMode == false)
			{
				bool fogEnabled = RenderSettings.fog;
				RenderSettings.fog = false;

				_camera.Render();
				UpdateUvRectFromRenderedContent();

				RenderSettings.fog = fogEnabled;
			}
		}

		private void TrySyncWithMapPlayArea()
		{
			if (_parametersOverridden == true)
				return;

			MapPlayArea playArea = FindFirstObjectByType<MapPlayArea>();
			if (playArea == null)
				return;

			Bounds bounds = playArea.GetPlayableTerrainBounds();
			Vector3 center = bounds.center;
			center.y = 0f;

			int squareSize = Mathf.Max(
				Mathf.Max(1, Mathf.CeilToInt(bounds.size.x)),
				Mathf.Max(1, Mathf.CeilToInt(bounds.size.z))
			);

			transform.position = center;
			_worldDimensions = new Vector2Int(squareSize, squareSize);
		}

		private void UpdateUvRectFromRenderedContent()
		{
			if (_mapTexture == null)
				return;

			RenderTexture previous = RenderTexture.active;
			RenderTexture.active = _mapTexture;

			Texture2D readback = new Texture2D(_mapTexture.width, _mapTexture.height, TextureFormat.RGBA32, false, false);
			readback.ReadPixels(new Rect(0, 0, _mapTexture.width, _mapTexture.height), 0, 0, false);
			readback.Apply(false, false);

			Color32[] pixels = readback.GetPixels32();
			Destroy(readback);
			RenderTexture.active = previous;

			int minX = _mapTexture.width;
			int minY = _mapTexture.height;
			int maxX = -1;
			int maxY = -1;

			for (int y = 0; y < _mapTexture.height; y++)
			{
				int rowOffset = y * _mapTexture.width;
				for (int x = 0; x < _mapTexture.width; x++)
				{
					Color32 pixel = pixels[rowOffset + x];
					if (pixel.a <= 8)
						continue;

					minX = Mathf.Min(minX, x);
					minY = Mathf.Min(minY, y);
					maxX = Mathf.Max(maxX, x);
					maxY = Mathf.Max(maxY, y);
				}
			}

			if (maxX < minX || maxY < minY)
			{
				_uvRect = new Rect(0f, 0f, 1f, 1f);
				return;
			}

			const int pixelPadding = 8;
			minX = Mathf.Max(0, minX - pixelPadding);
			minY = Mathf.Max(0, minY - pixelPadding);
			maxX = Mathf.Min(_mapTexture.width - 1, maxX + pixelPadding);
			maxY = Mathf.Min(_mapTexture.height - 1, maxY + pixelPadding);

			float width = Mathf.Max(1, maxX - minX + 1);
			float height = Mathf.Max(1, maxY - minY + 1);

			_uvRect = new Rect(
				minX / (float)_mapTexture.width,
				minY / (float)_mapTexture.height,
				width / _mapTexture.width,
				height / _mapTexture.height
			);
		}
	}
}
