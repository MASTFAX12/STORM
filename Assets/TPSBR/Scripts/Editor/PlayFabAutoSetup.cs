#if UNITY_EDITOR
namespace TPSBR.Editor
{
	using UnityEngine;
	using UnityEditor;
	using System;
	using System.Linq;
	using System.Reflection;

	/// <summary>
	/// Automatically adds ENABLE_PLAYFAB to Scripting Define Symbols
	/// if the PlayFab SDK is detected in the project.
	/// </summary>
	[InitializeOnLoad]
	public class PlayFabAutoSetup
	{
		static PlayFabAutoSetup()
		{
			// Check if we already have the symbol
			string definesString = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
			if (definesString.Contains("ENABLE_PLAYFAB"))
				return;

			// Check if PlayFab SDK is present by looking for one of its types
			// We iterate through all assemblies to find "PlayFab.PlayFabSettings"
			bool playFabExists = AppDomain.CurrentDomain.GetAssemblies()
				.SelectMany(assembly => assembly.GetTypes())
				.Any(type => type.FullName == "PlayFab.PlayFabSettings");

			if (playFabExists)
			{
				Debug.Log("[PlayFabAutoSetup] PlayFab SDK detected! Adding ENABLE_PLAYFAB to defines...");
				
				definesString += ";ENABLE_PLAYFAB";
				PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, definesString);
			}
		}
	}
}
#endif
