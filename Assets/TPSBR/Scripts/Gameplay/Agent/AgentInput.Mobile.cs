namespace TPSBR
{
	using UnityEngine;
	using TPSBR.UI;

	public sealed partial class AgentInput
	{
		partial void ProcessMobileInput(bool isInputPoll)
		{
			Vector2 moveDirection;
			Vector2 lookRotationDelta;
			const float mobileSprintThreshold = 0.82f;

			if (_mobileInputView == null)
			{
				if (Context != null && Context.UI != null)
				{
					_mobileInputView = Context.UI.Get<UIMobileInputView>();
				}

				return;
			}

			const float mobileSensitivityMultiplier = 32.0f;

			moveDirection     = Vector2.ClampMagnitude(_mobileInputView.Move, 1.0f);
			lookRotationDelta = InputUtility.GetSmoothLookRotationDelta(_smoothLookRotationDelta, new Vector2(-_mobileInputView.Look.y, _mobileInputView.Look.x) * mobileSensitivityMultiplier, Global.RuntimeSettings.Sensitivity, _lookResponsivity);

			_mobileInputView.Look = default;

			if (_agent.Character.CharacterController.FixedData.Aim == true)
			{
				lookRotationDelta *= Global.RuntimeSettings.AimSensitivity;
			}

			_renderInput.MoveDirection     = moveDirection;
			_renderInput.LookRotationDelta = lookRotationDelta;
			_renderInput.Jump              = _mobileInputView.Jump;
			_renderInput.Attack            = _mobileInputView.Fire;
			_renderInput.Interact          = _mobileInputView.Interact;
			_renderInput.Aim               = _mobileInputView.Aim;
			_renderInput.Reload            = _mobileInputView.Reload;
			// WeaponNext: If holding grenade, cycle grenades instead of guns
			if (_mobileInputView.Weapon != 0 && _agent.Weapons.CurrentWeaponSlot >= 4)
			{
				int currentSlot = _agent.Weapons.CurrentWeaponSlot;
				int nextGrenade = _agent.Weapons.GetNextWeaponSlot(currentSlot, 4);
				if (nextGrenade > 0)
				{
					_renderInput.Weapon = (byte)(nextGrenade + 1);
				}
				_mobileInputView.Weapon = 0; // Consume
			}
			else
			{
			_renderInput.Weapon = _mobileInputView.Weapon;
			}
			_renderInput.ToggleJetpack     = _mobileInputView.ToggleJetpack;
			_renderInput.Thrust            = _mobileInputView.Jump; // Same as PC (Space) and Gamepad (A button)
			_renderInput.Sprint            = moveDirection.sqrMagnitude >= mobileSprintThreshold * mobileSprintThreshold;

			// ToggleSide — one-shot toggle, consume after reading
			_renderInput.ToggleSide = _mobileInputView.ToggleSide;
			_mobileInputView.ToggleSide = false;

			// Grenade cycling (same logic as PC G key)
			if (_mobileInputView.Grenade == true)
			{
				_mobileInputView.Grenade = false; // Consume the input

				int pendingWeapon = _agent.Weapons.PendingWeaponSlot;

				// Toggle: If already holding grenade (>=4), switch back to previous weapon
				if (pendingWeapon >= 4)
				{
					int target = _agent.Weapons.PreviousWeaponSlot;
					if (target < 0 || target >= 4) target = 0; // Default to slot 0 (Rifle) if previous invalid
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
		}
	}
}
