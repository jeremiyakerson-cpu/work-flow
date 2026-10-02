using System;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense.UI
{
    /// <summary>How a screen covers what is behind it.</summary>
    public enum ScreenBackground
    {
        /// <summary>Solid background; screens below are deactivated once it is open.</summary>
        Opaque,
        /// <summary>Semi-transparent dim backdrop (modals, pause).</summary>
        Dim,
        /// <summary>Invisible backdrop that still blocks touches.</summary>
        Clear,
    }

    /// <summary>
    /// One entry in UIRoot's screen/modal stack. Full-screen rect with a backdrop
    /// and a safe-area <see cref="Content"/> child to build into. Close() animates
    /// out and destroys it; <see cref="OnBack"/> handles Esc/back (null = ignore).
    /// </summary>
    public sealed class UIScreen : MonoBehaviour
    {
        public RectTransform Rect;
        /// <summary>Safe-area child: put screen content here.</summary>
        public RectTransform Content;
        public CanvasGroup Group;
        public Image Background;
        public bool IsOpaque;
        public bool IsModal;

        /// <summary>Back/Esc action. Null means back is swallowed while this screen is on top.</summary>
        public Action OnBack;

        public bool IsClosing { get; internal set; }

        /// <summary>Raised once when the screen starts closing.</summary>
        public event Action Closed;

        /// <summary>Make a tap on the backdrop run <paramref name="action"/> (e.g. dismiss).</summary>
        public void SetBackdropAction(Action action)
        {
            if (Background == null) return;
            var b = Background.GetComponent<Button>();
            if (b == null)
            {
                b = Background.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                var nav = new Navigation();
                nav.mode = Navigation.Mode.None;
                b.navigation = nav;
            }
            b.onClick.RemoveAllListeners();
            if (action != null) b.onClick.AddListener(() => action());
        }

        public void Close(bool animate = true)
        {
            if (UIRoot.Instance != null) UIRoot.Instance.CloseScreen(this, animate);
            else Destroy(gameObject);
        }

        internal void RaiseClosed()
        {
            Action c = Closed;
            Closed = null;
            c?.Invoke();
        }

        private void OnDestroy()
        {
            if (UIRoot.Instance != null) UIRoot.Instance.Forget(this);
        }
    }
}
