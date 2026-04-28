#if UNITY_EDITOR
namespace TPSBR
{
	using UnityEngine;
	using UnityEditor;
	using TPSBR.UI;

	/// <summary>
	/// Tools > Apply Mobile Layout
	/// "Professional" Mobile HUD Layout - Designed for 3/4-finger claw or 2-thumb play.
	/// Based on competitive mobile shooter standards (PUBG Mobile / CODM).
	/// </summary>
	public class MobileButtonLayoutEditor : EditorWindow
	{
		[MenuItem("Tools/Apply Mobile Layout (Professional Class)")]
		public static void ApplyLayout()
		{
			var mobileInput = FindObjectOfType<UIMobileInputView>(true);

			if (mobileInput == null)
			{
				string[] guids = AssetDatabase.FindAssets("t:Prefab UIMobileInputView");
				if (guids.Length > 0)
				{
					string path = AssetDatabase.GUIDToAssetPath(guids[0]);
					GameObject prefab = PrefabUtility.LoadPrefabContents(path);
					mobileInput = prefab.GetComponent<UIMobileInputView>();

					if (mobileInput != null)
					{
						ApplyToMobileInput(mobileInput);
						PrefabUtility.SaveAsPrefabAsset(prefab, path);
						Debug.Log("[MobileLayout] Saved prefab: " + path);
					}

					PrefabUtility.UnloadPrefabContents(prefab);
					return;
				}
				return;
			}

			ApplyToMobileInput(mobileInput);
			EditorUtility.SetDirty(mobileInput.gameObject);
			Debug.Log("[MobileLayout] Applied Professional Layout.");
		}

		private static void ApplyToMobileInput(UIMobileInputView mobileInput)
		{
			var so = new SerializedObject(mobileInput);

			// References
			RectTransform fire       = so.FindProperty("_fire")?.objectReferenceValue as RectTransform;
			RectTransform jump       = so.FindProperty("_jump")?.objectReferenceValue as RectTransform;
			RectTransform interact   = so.FindProperty("_interact")?.objectReferenceValue as RectTransform;
			RectTransform aim        = so.FindProperty("_aim")?.objectReferenceValue as RectTransform;
			RectTransform reload     = so.FindProperty("_reload")?.objectReferenceValue as RectTransform;
			RectTransform weaponNext = so.FindProperty("_weaponNext")?.objectReferenceValue as RectTransform;
			RectTransform grenade    = so.FindProperty("_grenade")?.objectReferenceValue as RectTransform;
			RectTransform toggleSide = so.FindProperty("_toggleSide")?.objectReferenceValue as RectTransform;
			RectTransform toggleJetpack = so.FindProperty("_toggleJetpack")?.objectReferenceValue as RectTransform;
			RectTransform joystickOrigin = so.FindProperty("_joystickOrigin")?.objectReferenceValue as RectTransform;

			// ===========================================================
			// PROFESSIONAL HUD LAYOUT (Reference: 1920x1080)
			// ===========================================================
			//
			//  [Settings] [ToggleSide]                               [WeaponNext]
			//                                                        [Grenade]
			//
			//
			//                         [Reload]    [Jump]
			//          [Interact]     [Aim]       [FIRE]
			//
			// [Joystick]              [Jetpack]
			//
			// ===========================================================

			// 1. JOYSTICK: Bottom-Left (Solid, slightly offset from edge)
			if (joystickOrigin != null)
			{
				Undo.RecordObject(joystickOrigin, "Layout Joystick");
				joystickOrigin.anchorMin = new Vector2(0, 0);
				joystickOrigin.anchorMax = new Vector2(0, 0);
				joystickOrigin.pivot = new Vector2(0.5f, 0.5f);
				// Position: 220, 220 is the sweet spot for 1080p
				joystickOrigin.anchoredPosition = new Vector2(220, 220); 
				EditorUtility.SetDirty(joystickOrigin);
			}

			// 2. FIRE: The Anchor of the Right Hand
			// Large, but not obscuring. Bottom-Right, offset for thumb reach.
			SetBtn(fire,
				anchor: new Vector2(1, 0), pos: new Vector2(-120, 120),
				size: 110); // Standard Pro Size

			// 3. COMBAT CLUSTER (Orbiting Fire)
			
			// JUMP: Top-Right of Fire (Natural thumb extension)
			SetBtn(jump,
				anchor: new Vector2(1, 0), pos: new Vector2(-40, 260),
				size: 80);

			// AIM (ADS): Immediate Left of Fire (Quick scope access)
			SetBtn(aim,
				anchor: new Vector2(1, 0), pos: new Vector2(-260, 100),
				size: 85);

			// RELOAD: Top-Left of cluster (Secondary action)
			SetBtn(reload,
				anchor: new Vector2(1, 0), pos: new Vector2(-240, 240),
				size: 70);

			// JETPACK: Below Aim, rarely used but needed
			SetBtn(toggleJetpack,
				anchor: new Vector2(1, 0), pos: new Vector2(-260, -10),
				size: 60);

			// 4. WEAPON MANAGEMENT (Right Thumb - Top Quadrant)
			// Swapping weapons is a deliberate action, moved to top-right.
			
			// WEAPON SWAP
			SetBtn(weaponNext,
				anchor: new Vector2(1, 1), pos: new Vector2(-100, -50),
				size: 75);

			// GRENADE: Below weapon swap
			SetBtn(grenade,
				anchor: new Vector2(1, 1), pos: new Vector2(-100, -150),
				size: 70);

			// 5. CONTEXTUAL & UTILITY

			// INTERACT: Center-Rightish. Distinct.
			// Needs to be reachable but not accidental.
			SetBtn(interact,
				anchor: new Vector2(1, 0), pos: new Vector2(-450, 150),
				size: 80);

			// TOGGLE SIDE: Top-Left (Non-combat utility)
			// Beside Settings (Settings is usually at ~10-50 padding)
			SetBtn(toggleSide,
				anchor: new Vector2(0, 1), pos: new Vector2(180, -50),
				size: 60);
		}

		private static void SetBtn(RectTransform btn, Vector2 anchor, Vector2 pos, float size)
		{
			if (btn == null) return;
			Undo.RecordObject(btn, "Pro Layout");
			
			btn.anchorMin = anchor;
			btn.anchorMax = anchor;
			btn.pivot = new Vector2(0.5f, 0.5f); // Center pivot for consistent sizing
			btn.anchoredPosition = pos;
			btn.sizeDelta = new Vector2(size, size);
			
			EditorUtility.SetDirty(btn);
		}
	}
}
#endif
