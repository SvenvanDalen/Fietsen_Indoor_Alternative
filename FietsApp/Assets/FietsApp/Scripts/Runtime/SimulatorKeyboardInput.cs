using UnityEngine;
using UnityEngine.InputSystem;

namespace FietsApp
{
    /// <summary>
    /// Toetsenbordbesturing voor de simulator (story 1.6):
    /// pijl omhoog/omlaag = vermogen ±10 W (met Shift ±50 W),
    /// Page Up/Page Down = helling ±1%, 0 = vermogen naar nul.
    /// Spatiebalk ingedrukt = remmen (story 2.4); dat werkt ook met een echte trainer.
    /// </summary>
    [RequireComponent(typeof(RideController))]
    public sealed class SimulatorKeyboardInput : MonoBehaviour
    {
        RideController _ride;

        void Awake() => _ride = GetComponent<RideController>();

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            _ride.Brake = kb.spaceKey.isPressed ? 1f : 0f;
            if (_ride.Trainer != _ride.Simulator) return;

            float step = kb.shiftKey.isPressed ? 50f : 10f;
            var sim = _ride.Simulator;

            if (kb.upArrowKey.wasPressedThisFrame) sim.TargetPowerWatts += step;
            if (kb.downArrowKey.wasPressedThisFrame) sim.TargetPowerWatts -= step;
            if (kb.digit0Key.wasPressedThisFrame) sim.TargetPowerWatts = 0f;

            if (kb.pageUpKey.wasPressedThisFrame) _ride.GradePercent += 1f;
            if (kb.pageDownKey.wasPressedThisFrame) _ride.GradePercent -= 1f;
        }
    }
}
