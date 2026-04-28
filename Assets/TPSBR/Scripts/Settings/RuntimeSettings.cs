using UnityEngine;

namespace TPSBR
{
	public class RuntimeSettings
	{
		// CONSTANTS

		private const string OPTIONS_PREFIX       = "Options.V3.";
		public const string KEY_MUSIC_VOLUME     = "MusicVolume";
		public const string KEY_EFFECTS_VOLUME   = "EffectsVolume";
		public const string KEY_WINDOWED         = "Windowed";
		public const string KEY_RESOLUTION       = "Resolution";
		public const string KEY_GRAPHICS_QUALITY = "GraphicsQuality";
		public const string KEY_LIMIT_FPS        = "LimitFPS";
		public const string KEY_TARGET_FPS       = "TargetFPS";
		public const string KEY_REGION           = "Region";
		public const string KEY_SENSITIVITY      = "Sensitivity";
		public const string KEY_AIM_SENSITIVITY  = "AimSensitivity";
		public const string KEY_VSYNC            = "VSync";
		public const string KEY_CAMERA_DISTANCE  = "CameraDistance";

		// PUBLIC MEMBERS

		public Options Options => _options;

		public float  MusicVolume    { get { return _options.GetFloat(KEY_MUSIC_VOLUME); }     set { _options.Set(KEY_MUSIC_VOLUME, value, false); } }
		public float  EffectsVolume  { get { return _options.GetFloat(KEY_EFFECTS_VOLUME); }   set { _options.Set(KEY_EFFECTS_VOLUME, value, false); } }

		public bool   Windowed        { get { return _options.GetBool(KEY_WINDOWED); }         set { _options.Set(KEY_WINDOWED, value, false); } }
		public int    Resolution      { get { return _options.GetInt(KEY_RESOLUTION); }        set { _options.Set(KEY_RESOLUTION, value, false); } }
		public int    GraphicsQuality { get { return _options.GetInt(KEY_GRAPHICS_QUALITY); }  set { _options.Set(KEY_GRAPHICS_QUALITY, value, false); } }
		public bool   VSync           { get { return _options.GetBool(KEY_VSYNC); }            set { _options.Set(KEY_VSYNC, value, false); } }
		public bool   LimitFPS        { get { return _options.GetBool(KEY_LIMIT_FPS); }        set { _options.Set(KEY_LIMIT_FPS, value, false); } }
		public int    TargetFPS       { get { return _options.GetInt(KEY_TARGET_FPS); }        set { _options.Set(KEY_TARGET_FPS, value, false); } }
		public float  Sensitivity     { get { return _options.GetFloat(KEY_SENSITIVITY); }     set { _options.Set(KEY_SENSITIVITY, value, false); } }
		public float  AimSensitivity  { get { return _options.GetFloat(KEY_AIM_SENSITIVITY); } set { _options.Set(KEY_AIM_SENSITIVITY, value, false); } }
		public float  CameraDistance  { get { return _options.GetFloat(KEY_CAMERA_DISTANCE); } set { _options.Set(KEY_CAMERA_DISTANCE, value, false); } }

		public string Region          { get { return _options.GetString(KEY_REGION); }         set { _options.Set(KEY_REGION, value, true); } }


		// PRIVATE MEMBERS

		private Options _options = new Options();

		// PUBLIC METHODS

		public void Initialize(GlobalSettings settings)
		{
			_options.Initialize(settings.DefaultOptions, true, OPTIONS_PREFIX);

			// First run only: default music to silent unless player already has saved preference.
			if (PlayerPrefs.HasKey(OPTIONS_PREFIX + KEY_MUSIC_VOLUME) == false)
			{
				MusicVolume = 0f;
			}

			// Register CameraDistance with default 1.0 (range 0.5 - 2.0)
			var cameraDistanceDefault = new OptionsValue(KEY_CAMERA_DISTANCE, EOptionsValueType.Float);
			cameraDistanceDefault.FloatValue = new OptionsValueFloat { Value = 1.0f, MinValue = 0.3f, MaxValue = 5.0f };
			_options.AddDefaultValue(cameraDistanceDefault);

			Windowed = Screen.fullScreen == false;
			GraphicsQuality = QualitySettings.GetQualityLevel();
			Resolution = GetCurrentResolutionIndex();

			QualitySettings.vSyncCount = VSync == true ? 1 : 0;
			Application.targetFrameRate = LimitFPS == true ? TargetFPS : -1;

			_options.SaveChanges();
		}

		// PRIVATE MEMBERS

		private int GetCurrentResolutionIndex()
		{
			var resolutions = Screen.resolutions;
			if (resolutions == null || resolutions.Length == 0)
				return -1;

			int currentWidth = Mathf.RoundToInt(Screen.width);
			int currentHeight = Mathf.RoundToInt(Screen.height);
			int defaultRefreshRate = Mathf.RoundToInt((float)resolutions[^1].refreshRateRatio.value);

			for (int i = 0; i < resolutions.Length; i++)
			{
				var resolution = resolutions[i];

				if (resolution.width == currentWidth && resolution.height == currentHeight && Mathf.RoundToInt((float)resolution.refreshRateRatio.value) == defaultRefreshRate)
					return i;
			}

			return -1;
		}
	}
}
