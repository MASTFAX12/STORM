namespace TPSBR
{
	using UnityEngine;
	using UnityEngine.InputSystem;
	using Fusion.Addons.KCC;

	public sealed partial class AgentInput
	{
		partial void ProcessGamepadInput(bool isInputPoll)
		{
			// Standard gamepad layout (matches most shooters like Fortnite, CoD, etc.)

			Gamepad gamepad = Gamepad.current;
			if (gamepad == null)
				return;

			// Left Stick = Movement
			Vector2 moveDirection = gamepad.leftStick.ReadValue();
			if (moveDirection.IsAlmostZero(0.1f) == false)
			{
				_renderInput.MoveDirection = moveDirection;
			}
			else
			{
				moveDirection = default;
			}

			// Right Stick = Camera/Look
			Vector2 lookRotationDelta = gamepad.rightStick.ReadValue();
			if (lookRotationDelta.IsAlmostZero() == false)
			{
				lookRotationDelta = new Vector2(-lookRotationDelta.y, lookRotationDelta.x);
				_renderInput.LookRotationDelta = InputUtility.GetSmoothLookRotationDelta(_smoothLookRotationDelta, lookRotationDelta, Global.RuntimeSettings.Sensitivity, _lookResponsivity);
			}

			// A = Jump + Thrust (standard jump button)
			_renderInput.Jump          |= gamepad.aButton.isPressed;
			_renderInput.Thrust        |= gamepad.aButton.isPressed;

			// Left Trigger (LT) = Aim
			_renderInput.Aim           |= gamepad.leftTrigger.isPressed;

			// Right Trigger (RT) = Fire/Attack
			_renderInput.Attack        |= gamepad.rightTrigger.isPressed;

			// X = Reload
			_renderInput.Reload        |= gamepad.xButton.isPressed;

			// B = Interact
			_renderInput.Interact      |= gamepad.bButton.isPressed;

			// Left Bumper (LB) = Toggle Jetpack
			_renderInput.ToggleJetpack |= gamepad.leftShoulder.isPressed;

			// Left Stick Press (L3) = Sprint
			_renderInput.Sprint        |= gamepad.leftStickButton.isPressed && moveDirection.IsAlmostZero(0.1f) == false;

			// Right Bumper (RB) = Grenade Cycle
			if (gamepad.rightShoulder.wasPressedThisFrame == true)
			{
				int pendingWeapon = _agent.Weapons.PendingWeaponSlot;

				// Toggle: If already holding grenade (>=4), switch back to previous weapon
				if (pendingWeapon >= 4)
				{
					int target = _agent.Weapons.PreviousWeaponSlot;
					if (target < 0 || target >= 4) target = 0; // Default to slot 0
					_renderInput.Weapon = (byte)(target + 1);
				}
				else
				{
					// Switch to best available grenade
					int grenadeToSwitch = _agent.Weapons.GetNextWeaponSlot(4, 4);
					
					if (grenadeToSwitch > 0)
					{
						_renderInput.Weapon = (byte)(grenadeToSwitch + 1);
					}
				}
			}

			// Y = Next Weapon (cycles guns normally, cycles grenades if holding grenade)
			if (gamepad.yButton.wasPressedThisFrame == true)
			{
				int currentSlot = _agent.Weapons.CurrentWeaponSlot;

				if (currentSlot >= 4)
				{
					// Currently holding grenade — cycle to next grenade
					int nextGrenade = _agent.Weapons.GetNextWeaponSlot(currentSlot, 4);
					if (nextGrenade > 0)
					{
						_renderInput.Weapon = (byte)(nextGrenade + 1);
					}
				}
				else
				{
					// Standard weapon cycling (slots 1-3)
					int nextSlot = _agent.Weapons.GetNextWeaponSlot(currentSlot);
					if (nextSlot > 0 && nextSlot <= 3)
					{
						_renderInput.Weapon = (byte)(nextSlot + 1);
					}
				}
			}

			// D-Pad Up = Toggle Camera Side
			_renderInput.ToggleSide |= gamepad.dpad.up.isPressed;

			// D-Pad Left = Previous Weapon (fast switch)
			if (gamepad.dpad.left.wasPressedThisFrame == true)
			{
				_renderInput.Weapon = (byte)(_agent.Weapons.PreviousWeaponSlot + 1);
			}
		}
	}
}
