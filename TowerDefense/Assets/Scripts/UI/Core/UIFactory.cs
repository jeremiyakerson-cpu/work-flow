using System;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense.UI
{
    /// <summary>
    /// Code-only uGUI builders: rects, panels, labels, buttons with press
    /// feedback, progress bars, radial cooldowns, sliders and toggles, all styled
    /// from <see cref="UITheme"/> and <see cref="UISprites"/>.
    /// </summary>
    public static class UIFactory
    {
        public const int UILayer = 5;

        // ------------------------------------------------------------------ layout primitives

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = UILayer;
            var rt = (RectTransform)go.transform;
            if (parent != null) rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Stretch to fill the parent, optionally inset on all sides.</summary>
        public static RectTransform Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        /// <summary>Anchor at a single normalised point with the given pivot, offset and size.</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
            return rt;
        }

        public static RectTransform Center(RectTransform rt, Vector2 position, Vector2 size) =>
            Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);

        public static CanvasGroup Group(GameObject go)
        {
            var g = go.GetComponent<CanvasGroup>();
            return g != null ? g : go.AddComponent<CanvasGroup>();
        }

        public static VerticalLayoutGroup VerticalLayout(GameObject go, float spacing, RectOffset padding = null,
                                                         TextAnchor align = TextAnchor.UpperCenter)
        {
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = padding ?? new RectOffset(0, 0, 0, 0);
            v.childAlignment = align;
            v.childControlWidth = false;
            v.childControlHeight = false;
            v.childForceExpandWidth = false;
            v.childForceExpandHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup HorizontalLayout(GameObject go, float spacing, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = new RectOffset(0, 0, 0, 0);
            h.childAlignment = align;
            h.childControlWidth = false;
            h.childControlHeight = false;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            return h;
        }

        // ------------------------------------------------------------------ graphics

        public static Image Image(Transform parent, string name, Sprite sprite, Color color, bool raycast = false)
        {
            RectTransform rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        /// <summary>Rounded, 9-sliced panel. Blocks raycasts by default so taps don't reach the world.</summary>
        public static Image Panel(Transform parent, string name, Color color, float radius = UITheme.Radius, bool raycast = true)
        {
            Image img = Image(parent, name, UISprites.RoundedRect, color, raycast);
            SetRadius(img, radius);
            return img;
        }

        /// <summary>Configure a rounded-rect Image as sliced with the given corner radius (canvas units).</summary>
        public static void SetRadius(Image img, float radius)
        {
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.fillCenter = true;
            img.pixelsPerUnitMultiplier = UISprites.SliceRadius / Mathf.Max(1f, radius);
        }

        /// <summary>Thin rounded outline (selection highlight) stretched over a parent.</summary>
        public static Image Outline(Transform parent, Color color, float radius = UITheme.Radius, float outset = 6f)
        {
            Image img = Image(parent, "Outline", UISprites.RoundedOutline, color);
            SetRadius(img, radius);
            Stretch(img.rectTransform, -outset);
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color color,
                                 TextAnchor align = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal,
                                 bool shadow = true)
        {
            RectTransform rt = Rect("Label", parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = UITheme.Font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = align;
            t.supportRichText = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            if (shadow)
            {
                var s = rt.gameObject.AddComponent<Shadow>();
                s.effectColor = UITheme.Shadow;
                s.effectDistance = new Vector2(2f, -3f);
            }
            return t;
        }

        /// <summary>Label that fills its parent (inset) and shrinks long text to fit.</summary>
        public static Text FitLabel(Transform parent, string text, int size, Color color, float inset = 8f)
        {
            Text t = Label(parent, text, size, color);
            Stretch(t.rectTransform, inset);
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = Mathf.Max(14, Mathf.RoundToInt(size * 0.55f));
            t.resizeTextMaxSize = size;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            return t;
        }

        // ------------------------------------------------------------------ buttons

        /// <summary>Rounded text button with depth, colour-tint states and press-scale feedback.</summary>
        public static UIButtonView Button(Transform parent, string label, Action onClick, Color color, Vector2 size,
                                          int fontSize = UITheme.FontButton, float radius = UITheme.Radius)
        {
            UIButtonView view = BuildButton(parent, "Button_" + label, UISprites.RoundedRect, color, size, radius, onClick);
            if (!string.IsNullOrEmpty(label))
                view.Label = FitLabel(view.Face.transform, label, fontSize, UITheme.Text, 12f);
            return view;
        }

        /// <summary>Circular (or rounded-square) icon button.</summary>
        public static UIButtonView IconButton(Transform parent, Sprite icon, Action onClick, Color color, float size,
                                              bool circle = true, float iconScale = 0.5f)
        {
            UIButtonView view = BuildButton(parent, "IconButton", circle ? UISprites.Circle : UISprites.RoundedRect, color,
                                            new Vector2(size, size), UITheme.Radius, onClick);
            if (icon != null)
            {
                view.Icon = Image(view.Face.transform, "Icon", icon, UITheme.Text);
                view.Icon.preserveAspect = true;
                Center(view.Icon.rectTransform, Vector2.zero, new Vector2(size * iconScale, size * iconScale));
            }
            return view;
        }

        private static UIButtonView BuildButton(Transform parent, string name, Sprite sprite, Color color, Vector2 size,
                                                float radius, Action onClick)
        {
            RectTransform root = Rect(name, parent);
            root.sizeDelta = size;

            // Darker base gives depth; the face sits on top, raised by a few units.
            var shade = root.gameObject.AddComponent<Image>();
            shade.sprite = sprite;
            shade.color = UITheme.Shade(color, 0.6f);
            shade.raycastTarget = true;
            bool sliced = sprite == UISprites.RoundedRect;
            if (sliced) SetRadius(shade, radius);

            Image face = Image(root, "Face", sprite, color);
            if (sliced) SetRadius(face, radius);
            else face.preserveAspect = true; // keep circles round inside the raised rect
            Stretch(face.rectTransform);
            float depth = Mathf.Clamp(size.y * 0.07f, 4f, 10f);
            face.rectTransform.offsetMin = new Vector2(0f, depth);

            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock cb = ColorBlock.defaultColorBlock;
            cb.normalColor = UnityEngine.Color.white;
            cb.highlightedColor = UnityEngine.Color.white;
            cb.selectedColor = UnityEngine.Color.white;
            cb.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            cb.disabledColor = new Color(0.5f, 0.5f, 0.52f, 1f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.08f;
            button.colors = cb;
            var nav = new Navigation();
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            button.onClick.AddListener(() =>
            {
                UIRoot.NotifyButtonClick();
                onClick?.Invoke();
            });

            root.gameObject.AddComponent<PressScale>();

            var view = root.gameObject.AddComponent<UIButtonView>();
            view.Button = button;
            view.Shade = shade;
            view.Face = face;
            view.BaseColor = color;
            return view;
        }

        // ------------------------------------------------------------------ indicators

        public static ProgressBar ProgressBar(Transform parent, Vector2 size, Color fillColor, Color backColor)
        {
            RectTransform root = Rect("ProgressBar", parent);
            root.sizeDelta = size;
            float radius = size.y * 0.5f;

            var bg = root.gameObject.AddComponent<Image>();
            bg.sprite = UISprites.RoundedRect;
            bg.color = backColor;
            bg.raycastTarget = false;
            SetRadius(bg, radius);

            Image fill = Image(root, "Fill", UISprites.RoundedRect, fillColor);
            SetRadius(fill, radius);
            RectTransform frt = fill.rectTransform;
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(3f, 3f);
            frt.offsetMax = new Vector2(-3f, -3f);

            var bar = root.gameObject.AddComponent<ProgressBar>();
            bar.Background = bg;
            bar.Fill = frt;
            bar.FillImage = fill;
            bar.SetValue(1f);
            return bar;
        }

        /// <summary>Radial cooldown shade stretched over <paramref name="parent"/> (a circular button face).</summary>
        public static RadialCooldown RadialShade(Transform parent, Color shadeColor, Sprite shape = null)
        {
            Image shade = Image(parent, "Cooldown", shape != null ? shape : UISprites.Circle, shadeColor);
            Stretch(shade.rectTransform);
            shade.type = UnityEngine.UI.Image.Type.Filled;
            shade.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
            shade.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top;
            shade.fillClockwise = false; // wedge shrinks clockwise as the cooldown ends
            shade.fillAmount = 0f;
            shade.enabled = false;
            var rc = shade.gameObject.AddComponent<RadialCooldown>();
            rc.Shade = shade;
            return rc;
        }

        // ------------------------------------------------------------------ inputs

        /// <summary>Horizontal slider (0..1) with a large thumb for touch.</summary>
        public static Slider Slider(Transform parent, Vector2 size, float value, Action<float> onChanged)
        {
            RectTransform root = Rect("Slider", parent);
            root.sizeDelta = size;
            float handle = size.y;

            Image bg = Image(root, "Background", UISprites.RoundedRect, UITheme.Shade(UITheme.Panel, 0.6f), true);
            SetRadius(bg, size.y * 0.2f);
            RectTransform bgRt = bg.rectTransform;
            bgRt.anchorMin = new Vector2(0f, 0.32f);
            bgRt.anchorMax = new Vector2(1f, 0.68f);
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            RectTransform fillArea = Rect("Fill Area", root);
            fillArea.anchorMin = new Vector2(0f, 0.32f);
            fillArea.anchorMax = new Vector2(1f, 0.68f);
            fillArea.offsetMin = new Vector2(handle * 0.5f, 0f);
            fillArea.offsetMax = new Vector2(-handle * 0.5f, 0f);
            Image fill = Image(fillArea, "Fill", UISprites.RoundedRect, UITheme.Gold);
            SetRadius(fill, size.y * 0.2f);
            fill.rectTransform.sizeDelta = new Vector2(handle, 0f);

            RectTransform handleArea = Rect("Handle Slide Area", root);
            Stretch(handleArea);
            handleArea.offsetMin = new Vector2(handle * 0.5f, 0f);
            handleArea.offsetMax = new Vector2(-handle * 0.5f, 0f);
            Image knob = Image(handleArea, "Handle", UISprites.Circle, UITheme.Text, true);
            knob.rectTransform.sizeDelta = new Vector2(handle, 0f);

            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = knob.rectTransform;
            slider.targetGraphic = knob;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            var nav = new Navigation();
            nav.mode = Navigation.Mode.None;
            slider.navigation = nav;
            slider.SetValueWithoutNotify(Mathf.Clamp01(value));
            if (onChanged != null) slider.onValueChanged.AddListener(v => onChanged(v));
            return slider;
        }

        /// <summary>Checkbox-style toggle (rounded box with a check).</summary>
        public static Toggle Toggle(Transform parent, float size, bool isOn, Action<bool> onChanged)
        {
            RectTransform root = Rect("Toggle", parent);
            root.sizeDelta = new Vector2(size, size);
            Image box = root.gameObject.AddComponent<Image>();
            box.sprite = UISprites.RoundedRect;
            box.color = UITheme.Shade(UITheme.Panel, 0.6f);
            SetRadius(box, size * 0.22f);
            box.raycastTarget = true;

            Image check = Image(root, "Check", UISprites.Check, UITheme.Gold);
            Center(check.rectTransform, Vector2.zero, new Vector2(size * 0.7f, size * 0.7f));

            var toggle = root.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = box;
            toggle.graphic = check;
            var nav = new Navigation();
            nav.mode = Navigation.Mode.None;
            toggle.navigation = nav;
            toggle.SetIsOnWithoutNotify(isOn);
            check.canvasRenderer.SetAlpha(isOn ? 1f : 0f);
            toggle.onValueChanged.AddListener(v =>
            {
                UIRoot.NotifyButtonClick();
                onChanged?.Invoke(v);
            });
            root.gameObject.AddComponent<PressScale>();
            return toggle;
        }

        // ------------------------------------------------------------------ composites

        /// <summary>Coin + number pill (costs, rewards). Returns the number label.</summary>
        public static Text CostTag(Transform parent, int amount, float height = 52f)
        {
            Image pill = Panel(parent, "CostTag", UITheme.Shade(UITheme.Panel, 0.8f), height * 0.5f, false);
            pill.rectTransform.sizeDelta = new Vector2(height * 2.6f, height);
            Image coin = Image(pill.transform, "Coin", UISprites.Coin, UnityEngine.Color.white);
            Place(coin.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6f, 0f),
                  new Vector2(height - 10f, height - 10f));
            Text t = Label(pill.transform, NumberCache.Get(amount), Mathf.RoundToInt(height * 0.62f), UITheme.Gold,
                           TextAnchor.MiddleCenter, FontStyle.Bold);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            Stretch(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(height - 4f, 0f);
            return t;
        }

        /// <summary>A row of five-point stars, <paramref name="filled"/> of them lit.</summary>
        public static Image[] Stars(Transform parent, int count, int filled, float size, float spacing)
        {
            RectTransform row = Rect("Stars", parent);
            row.sizeDelta = new Vector2(count * size + (count - 1) * spacing, size);
            var stars = new Image[count];
            for (int i = 0; i < count; i++)
            {
                Image s = Image(row, "Star" + i, UISprites.Star, i < filled ? UITheme.Gold : UITheme.StarOff);
                Place(s.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(i * (size + spacing), 0f),
                      new Vector2(size, size));
                stars[i] = s;
            }
            return stars;
        }
    }
}
