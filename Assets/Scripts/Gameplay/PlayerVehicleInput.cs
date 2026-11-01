using UnityEngine;
using UnityEngine.InputSystem;

namespace Vigilante8.Gameplay
{
    [RequireComponent(typeof(VehicleDrive))]
    public sealed class PlayerVehicleInput : MonoBehaviour
    {
        private VehicleDrive vehicle;
        private void Awake() => vehicle = GetComponent<VehicleDrive>();

        private void Update()
        {
            if (!vehicle.IsAlive) return;
            float gas = 0f, steer = 0f, brake = 0f;
            bool fire = false;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) gas += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) gas -= 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) steer -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) steer += 1f;
                brake = keyboard.leftShiftKey.isPressed ? 1f : 0f;
                fire = keyboard.spaceKey.isPressed;
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                float stick = gamepad.leftStick.ReadValue().x;
                if (Mathf.Abs(stick) > 0.15f) steer = stick;
                float triggers = gamepad.rightTrigger.ReadValue() -
                                 gamepad.leftTrigger.ReadValue();
                if (Mathf.Abs(triggers) > 0.1f) gas = triggers;
                brake = Mathf.Max(brake, gamepad.buttonSouth.isPressed ? 1f : 0f);
                fire |= gamepad.rightShoulder.isPressed;
            }
            vehicle.SetControls(gas, steer, brake, fire);
        }
    }
}
