using UnityEngine;
using UnityEngine.EventSystems;

namespace FietsApp
{
    /// <summary>Knop die meldt wanneer hij ingedrukt en weer losgelaten wordt (voor de rem).</summary>
    public sealed class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public event System.Action<bool> Changed;
        bool _pressed;

        public void OnPointerDown(PointerEventData eventData) => Set(true);
        public void OnPointerUp(PointerEventData eventData) => Set(false);
        public void OnPointerExit(PointerEventData eventData) => Set(false);

        void OnDisable() => Set(false);

        void Set(bool pressed)
        {
            if (_pressed == pressed) return;
            _pressed = pressed;
            Changed?.Invoke(pressed);
        }
    }
}
