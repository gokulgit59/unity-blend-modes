using UnityEngine;
using UnityEngine.EventSystems;

namespace BlendMode.Showcase
{
    // Reports when a UI element is held down, for live "pressed" states.
    public class PressRelay : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public System.Action<bool> onChange;

        public void OnPointerDown(PointerEventData e) => onChange?.Invoke(true);
        public void OnPointerUp(PointerEventData e) => onChange?.Invoke(false);
        public void OnPointerExit(PointerEventData e) => onChange?.Invoke(false);
    }
}
