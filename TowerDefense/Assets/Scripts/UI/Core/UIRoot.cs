using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TowerDefense.UI
{
    /// <summary>
    /// The single overlay canvas and everything that hangs off it: layers, the
    /// screen/modal stack with Esc/back routing, toasts, floating world text and
    /// world-to-canvas helpers. Create once with <see cref="Create"/>; it survives
    /// level teardown (DontDestroyOnLoad) and also guarantees an EventSystem.
    ///
    /// Layers, bottom to top: Hud (safe area), World (full screen, radial menus),
    /// Screens (full), Modals (full), Toasts (safe area).
    /// </summary>
    public sealed class UIRoot : MonoBehaviour
    {
        public static UIRoot Instance { get; private set; }

        /// <summary>Optional click sound / haptic hook for every factory button.</summary>
        public static IUiFeedback Feedback { get; set; }

        /// <summary>Raised on every factory button / toggle click.</summary>
        public static event Action ButtonClicked;

        public Canvas Canvas { get; private set; }
        public CanvasScaler Scaler { get; private set; }
        public RectTransform CanvasRect { get; private set; }
        public RectTransform HudLayer { get; private set; }
        public RectTransform WorldLayer { get; private set; }
        public RectTransform ScreenLayer { get; private set; }
        public RectTransform ModalLayer { get; private set; }
        public RectTransform ToastLayer { get; private set; }

        private readonly List<UIScreen> screens = new List<UIScreen>();
        private readonly List<UIScreen> modals = new List<UIScreen>();
        private readonly List<Func<bool>> backHandlers = new List<Func<bool>>();

        private CanvasGroup toastGroup;
        private Text toastLabel;
        private RectTransform toastRect;
        private readonly List<Text> floatPool = new List<Text>();

        /// <summary>Topmost open (non-closing) screen, modals first. Null if none.</summary>
        public UIScreen Top => TopOf(modals) ?? TopOf(screens);
        public bool HasOpenScreens => TopOf(modals) != null || TopOf(screens) != null;

        // ------------------------------------------------------------------ lifecycle

        /// <summary>Create (or return) the UI root canvas. Safe to call repeatedly.</summary>
        public static UIRoot Create(int sortingOrder = 100)
        {
            if (Instance != null) return Instance;

            var go = new GameObject("[UI]", typeof(RectTransform));
            DontDestroyOnLoad(go);
            go.layer = UIFactory.UILayer;
            var root = go.AddComponent<UIRoot>();
            root.Build(sortingOrder);
            return root;
        }

        /// <summary>Destroy the whole UI (app shutdown / full reset).</summary>
        public static void DestroyRoot()
        {
            if (Instance != null) Destroy(Instance.gameObject);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Build(int sortingOrder)
        {
            CanvasRect = (RectTransform)transform;

            Canvas = gameObject.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.sortingOrder = sortingOrder;
            Canvas.pixelPerfect = false;

            Scaler = gameObject.AddComponent<CanvasScaler>();
            Scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            Scaler.referenceResolution = new Vector2(1920f, 1080f);
            Scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            Scaler.matchWidthOrHeight = 0.5f;
            Scaler.referencePixelsPerUnit = 100f;

            gameObject.AddComponent<GraphicRaycaster>();

            HudLayer = SafeLayer("Hud");
            WorldLayer = FullLayer("World");
            ScreenLayer = FullLayer("Screens");
            ModalLayer = FullLayer("Modals");
            ToastLayer = SafeLayer("Toasts");

            BuildToast();
            EnsureEventSystem(transform);
        }

        private RectTransform FullLayer(string name) => UIFactory.Stretch(UIFactory.Rect(name, transform));

        private RectTransform SafeLayer(string name)
        {
            RectTransform full = FullLayer(name);
            full.gameObject.AddComponent<SafeAreaFitter>();
            return full;
        }

        /// <summary>Make sure an EventSystem with a StandaloneInputModule exists.</summary>
        public static EventSystem EnsureEventSystem(Transform parent = null)
        {
            EventSystem es = EventSystem.current;
            if (es == null) es = FindFirstObjectByType<EventSystem>();
            if (es == null)
            {
                var go = new GameObject("[EventSystem]");
                if (parent != null) go.transform.SetParent(parent, false);
                else DontDestroyOnLoad(go);
                es = go.AddComponent<EventSystem>();
                go.AddComponent<StandaloneInputModule>();
            }
            else if (es.GetComponent<BaseInputModule>() == null)
            {
                es.gameObject.AddComponent<StandaloneInputModule>();
            }
            return es;
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape)) HandleBack();
        }

        internal static void NotifyButtonClick()
        {
            ButtonClicked?.Invoke();
            Feedback?.OnButtonClick();
        }

        // ------------------------------------------------------------------ screens

        /// <summary>
        /// Create and push an empty screen. Build into <see cref="UIScreen.Content"/>
        /// (safe area). Modals go on the modal layer above every screen.
        /// </summary>
        public UIScreen CreateScreen(string name, ScreenBackground background, bool modal = false, bool animate = true)
        {
            RectTransform rt = UIFactory.Stretch(UIFactory.Rect(name, modal ? ModalLayer : ScreenLayer));
            var screen = rt.gameObject.AddComponent<UIScreen>();
            screen.Rect = rt;
            screen.IsModal = modal;
            screen.IsOpaque = background == ScreenBackground.Opaque;
            screen.Group = UIFactory.Group(rt.gameObject);

            Color bg = background == ScreenBackground.Opaque ? UITheme.ScreenBackground
                     : background == ScreenBackground.Dim ? UITheme.Backdrop
                     : new Color(0f, 0f, 0f, 0f);
            screen.Background = UIFactory.Image(rt, "Backdrop", null, bg, true);
            UIFactory.Stretch(screen.Background.rectTransform);

            RectTransform content = UIFactory.Stretch(UIFactory.Rect("Content", rt));
            content.gameObject.AddComponent<SafeAreaFitter>();
            screen.Content = content;

            (modal ? modals : screens).Add(screen);

            if (animate)
            {
                screen.Group.alpha = 0f;
                UITween.Fade(screen.Group, 0f, 1f, UITheme.NormalAnim, Ease.OutQuad, 0f, RefreshVisibility);
                UITween.Scale(content, Vector3.one * (modal ? 0.9f : 0.97f), Vector3.one, UITheme.NormalAnim + 0.08f,
                              modal ? Ease.OutBack : Ease.OutCubic);
            }
            else
            {
                RefreshVisibility();
            }
            return screen;
        }

        /// <summary>Remove from the stack, animate out and destroy.</summary>
        public void CloseScreen(UIScreen screen, bool animate = true)
        {
            if (screen == null || screen.IsClosing) return;
            screen.IsClosing = true;
            screens.Remove(screen);
            modals.Remove(screen);
            RefreshVisibility();
            screen.RaiseClosed();

            if (screen.Group != null)
            {
                screen.Group.interactable = false;
                screen.Group.blocksRaycasts = false;
            }

            if (!animate || screen.Group == null || !screen.gameObject.activeInHierarchy)
            {
                Destroy(screen.gameObject);
                return;
            }
            GameObject go = screen.gameObject;
            UITween.Fade(screen.Group, screen.Group.alpha, 0f, UITheme.FastAnim + 0.03f, Ease.InQuad, 0f, () =>
            {
                if (go != null) Destroy(go);
            });
        }

        public void CloseAllScreens(bool animate = false)
        {
            for (int i = screens.Count - 1; i >= 0; i--) CloseScreen(screens[i], animate);
        }

        public void CloseAllModals(bool animate = false)
        {
            for (int i = modals.Count - 1; i >= 0; i--) CloseScreen(modals[i], animate);
        }

        internal void Forget(UIScreen screen)
        {
            if (screens.Remove(screen) | modals.Remove(screen)) RefreshVisibility();
        }

        /// <summary>Deactivate screens hidden behind an opaque screen; reactivate the rest.</summary>
        private void RefreshVisibility()
        {
            bool covered = false;
            for (int i = screens.Count - 1; i >= 0; i--)
            {
                UIScreen s = screens[i];
                if (s == null) continue;
                bool visible = !covered;
                if (s.gameObject.activeSelf != visible) s.gameObject.SetActive(visible);
                // An opaque screen only covers once it has finished fading in.
                if (s.IsOpaque && (s.Group == null || s.Group.alpha >= 0.99f)) covered = true;
            }
            // The HUD is pointless (and costs fill rate) behind an opaque screen.
            if (HudLayer != null && HudLayer.gameObject.activeSelf == covered) HudLayer.gameObject.SetActive(!covered);
            if (WorldLayer != null && WorldLayer.gameObject.activeSelf == covered) WorldLayer.gameObject.SetActive(!covered);
        }

        private static UIScreen TopOf(List<UIScreen> list)
        {
            for (int i = list.Count - 1; i >= 0; i--)
                if (list[i] != null && !list[i].IsClosing) return list[i];
            return null;
        }

        // ------------------------------------------------------------------ back / Esc

        /// <summary>Register a back handler (checked after screens). Return true if it consumed the press.</summary>
        public void PushBackHandler(Func<bool> handler)
        {
            if (handler != null && !backHandlers.Contains(handler)) backHandlers.Add(handler);
        }

        public void RemoveBackHandler(Func<bool> handler) => backHandlers.Remove(handler);

        /// <summary>Route a back/Esc press: top modal, then top screen, then registered handlers.</summary>
        public bool HandleBack()
        {
            UIScreen top = Top;
            if (top != null)
            {
                top.OnBack?.Invoke();
                return true;
            }
            for (int i = backHandlers.Count - 1; i >= 0; i--)
                if (backHandlers[i]()) return true;
            return false;
        }

        // ------------------------------------------------------------------ dialogs

        /// <summary>Yes/no dialog. Back or a backdrop tap counts as cancel.</summary>
        public UIScreen Confirm(string title, string message, string confirmLabel, string cancelLabel,
                                Action onConfirm, Action onCancel = null, bool destructive = false)
        {
            UIScreen s = CreateScreen("Confirm", ScreenBackground.Dim, true);
            Image panel = UIFactory.Panel(s.Content, "Panel", UITheme.Panel, 36f);
            UIFactory.Center(panel.rectTransform, Vector2.zero, new Vector2(1000f, 560f));
            UIFactory.Outline(panel.transform, UITheme.PanelBorder, 36f, 0f);

            Text t = UIFactory.Label(panel.transform, title, UITheme.FontLarge, UITheme.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(900f, 90f));
            Text m = UIFactory.Label(panel.transform, message, UITheme.FontBody, UITheme.Text);
            UIFactory.Place(m.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(880f, 200f));

            bool done = false;
            Action cancel = () =>
            {
                if (done) return;
                done = true;
                s.Close();
                onCancel?.Invoke();
            };
            Action confirm = () =>
            {
                if (done) return;
                done = true;
                s.Close();
                onConfirm?.Invoke();
            };

            UIButtonView no = UIFactory.Button(panel.transform, cancelLabel, cancel, UITheme.Neutral, new Vector2(400f, UITheme.ButtonHeight));
            UIFactory.Place(no.Rect, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-20f, 40f), new Vector2(400f, UITheme.ButtonHeight));
            UIButtonView yes = UIFactory.Button(panel.transform, confirmLabel, confirm, destructive ? UITheme.Danger : UITheme.Primary,
                                                new Vector2(400f, UITheme.ButtonHeight));
            UIFactory.Place(yes.Rect, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(20f, 40f), new Vector2(400f, UITheme.ButtonHeight));

            s.OnBack = cancel;
            s.SetBackdropAction(cancel);
            return s;
        }

        // ------------------------------------------------------------------ toast / floating text

        private void BuildToast()
        {
            Image panel = UIFactory.Panel(ToastLayer, "Toast", UITheme.WithAlpha(UITheme.Panel, 0.92f), 40f, false);
            toastRect = panel.rectTransform;
            UIFactory.Place(toastRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 220f), new Vector2(900f, 100f));
            toastLabel = UIFactory.Label(panel.transform, "", UITheme.FontBody, UITheme.Text);
            UIFactory.Stretch(toastLabel.rectTransform, 16f);
            toastGroup = UIFactory.Group(panel.gameObject);
            toastGroup.blocksRaycasts = false;
            toastGroup.interactable = false;
            toastGroup.alpha = 0f;
            panel.gameObject.SetActive(false);
        }

        /// <summary>Short non-blocking message near the bottom of the screen.</summary>
        public void ShowToast(string message, float seconds = 1.8f)
        {
            if (toastGroup == null) return;
            toastLabel.text = message;
            float w = Mathf.Clamp(toastLabel.preferredWidth + 80f, 360f, 1400f);
            toastRect.sizeDelta = new Vector2(w, 100f);
            toastGroup.gameObject.SetActive(true);
            UITween.Kill(toastGroup);
            UITween.Fade(toastGroup, toastGroup.alpha, 1f, UITheme.FastAnim, Ease.OutQuad, 0f, () =>
                UITween.Fade(toastGroup, 1f, 0f, UITheme.NormalAnim, Ease.InQuad, seconds, () =>
                {
                    if (toastGroup != null) toastGroup.gameObject.SetActive(false);
                }));
            UITween.Move(toastRect, new Vector2(0f, 190f), new Vector2(0f, 220f), UITheme.NormalAnim, Ease.OutBack);
        }

        /// <summary>Text that rises and fades from a world position (gold gains, warnings).</summary>
        public void FloatText(Vector3 world, string text, Color color, Camera cam = null)
        {
            if (cam == null) cam = Camera.main;
            if (cam == null || !WorldToLayer(WorldLayer, world, cam, out Vector2 local)) return;

            Text label = null;
            for (int i = 0; i < floatPool.Count; i++)
                if (floatPool[i] != null && !floatPool[i].gameObject.activeSelf) { label = floatPool[i]; break; }
            if (label == null)
            {
                label = UIFactory.Label(WorldLayer, "", UITheme.FontLarge, color, TextAnchor.MiddleCenter, FontStyle.Bold);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.rectTransform.sizeDelta = new Vector2(400f, 80f);
                floatPool.Add(label);
            }

            RectTransform rt = label.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = local;
            label.text = text;
            label.color = color;
            label.gameObject.SetActive(true);
            label.transform.SetAsLastSibling();
            Text l = label;
            Vector2 start = local;
            UITween.Kill(l);
            UITween.To(l, 1.1f, v =>
            {
                if (l == null) return;
                l.rectTransform.anchoredPosition = start + new Vector2(0f, 110f * v);
                l.color = UITheme.WithAlpha(color, v < 0.6f ? 1f : 1f - (v - 0.6f) / 0.4f);
            }, Ease.OutQuad, 0f, () => { if (l != null) l.gameObject.SetActive(false); });
            UITween.Punch(rt, 0.3f, 0.25f);
        }

        // ------------------------------------------------------------------ coordinates

        /// <summary>World position to a local point in <paramref name="layer"/> (overlay canvas).</summary>
        public static bool WorldToLayer(RectTransform layer, Vector3 world, Camera cam, out Vector2 local)
        {
            local = Vector2.zero;
            if (cam == null || layer == null) return false;
            Vector3 sp = cam.WorldToScreenPoint(world);
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, new Vector2(sp.x, sp.y), null, out local);
        }

        /// <summary>Screen.safeArea expressed in <paramref name="layer"/>'s local coordinates.</summary>
        public static Rect SafeRectIn(RectTransform layer)
        {
            Rect sa = SafeAreaFitter.CurrentSafeArea;
            if (sa.width <= 0f || sa.height <= 0f) sa = new Rect(0f, 0f, Screen.width, Screen.height);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, new Vector2(sa.xMin, sa.yMin), null, out Vector2 min);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, new Vector2(sa.xMax, sa.yMax), null, out Vector2 max);
            return new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
        }
    }
}
