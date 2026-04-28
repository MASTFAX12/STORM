using UnityEngine;
using UnityEngine.Audio;

namespace TPSBR
{
	public class SceneAudio : SceneService 
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private AudioMixer _masterMixer;

		// PUBLIC METHODS

		public void UpdateVolume()
		{
			if (_masterMixer == null)
				return;

			_masterMixer.SetFloat("MusicVolume", ToDecibel(Context.RuntimeSettings.MusicVolume));
			_masterMixer.SetFloat("EffectsVolume", ToDecibel(Context.RuntimeSettings.EffectsVolume));
		}

		private static float ToDecibel(float linear)
		{
			if (linear <= 0.0001f)
				return -80f;

			return Mathf.Log10(linear) * 20f;
		}

		// GameService INTERFACE

		protected override void OnActivate()
		{
			base.OnActivate();

			UpdateVolume();
		}
	}
}
