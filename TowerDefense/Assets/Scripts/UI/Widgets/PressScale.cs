using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TowerDefense.UI
{
    /// <summary>
    /// Tactile press feedback: shrinks the element while a pointer holds it and
    /// springs back on release (unscaled time, so it works while paused).
    /// </summary>
    public sealed class PressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public float pressedScale = 0.9f;

        private Selectable selectable;
        private bool pressed;

        private void Awake()
        {
            selectable = GetComponent<Selectable>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (selectable != null && !selectable.IsInteractable()) return;
            pressed = true;
            UITween.Scale(transform, transform.localScale, Vector3.one * pressedScale, 0.07f, Ease.OutQuad);
        }

        public void OnPointerUp(PointerEventData eventData) => Release();

        public void OnPointerExit(PointerEventData eventData) => Release();

        private void Release()
        {
            if (!pressed) return;
            pressed = false;
            UITween.Scale(transform, transform.localScale, Vector3.one, 0.22f, Ease.OutBack);
        }

        private void OnDisable()
        {
            pressed = false;
            UITween.Kill(transform);
            transform.localScale = Vector3.one;
        }
    }
}
