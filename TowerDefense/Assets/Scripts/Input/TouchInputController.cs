using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TowerDefense.Input
{
    /// <summary>
    /// Unified touch + mouse controller for the battlefield. Owns camera pan/zoom
    /// (one-finger drag, pinch, inertia, clamped to level bounds) and turns taps
    /// into world actions: build slots (TowerPlacement.HandleTap), hero selection
    /// and move orders (tap hero then road, or drag the hero onto the road), spell
    /// targeting, and "empty ground" taps that close menus.
    /// Touches that start over uGUI are ignored for their whole lifetime.
    /// Desktop fallbacks: left click/drag, right-drag pan, mouse wheel zoom
    /// (Esc is routed through UIRoot's back stack, see CancelModes()).
    /// Uses unscaled time so the camera still works at timeScale 0..3.
    /// Note: requires the legacy Input Manager ("Both" or "Input Manager (Old)").
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class TouchInputController : MonoBehaviour
    {
        public static TouchInputController Instance { get; private set; }

        [Header("Gestures")]
        [Tooltip("Finger travel (points) before a press becomes a drag instead of a tap.")]
        public float dragThresholdPoints = 10f;
        [Tooltip("Presses held longer than this are not taps.")]
        public float maxTapSeconds = 0.8f;
        [Tooltip("Exponential decay rate of pan inertia (1/s).")]
        public float inertiaDamping = 5f;
        public float minInertiaSpeed = 0.2f;
        [Tooltip("Zoom change per mouse wheel notch (fraction of orthographic size).")]
        public float wheelZoomStep = 0.12f;

        [Header("Tap targets")]
        [Tooltip("Taps within this many points of the hero select it even if they miss its collider.")]
        public float heroTapRadiusPoints = 30f;
        [Tooltip("Max distance (world units) from a path for hero move orders and spell placement.")]
        public float pathTolerance = 0.9f;

        /// <summary>Camera to drive. Defaults to Camera.main.</summary>
        public Camera TargetCamera { get; set; }
        /// <summary>When false, world taps are ignored (camera still pans if CameraControlEnabled).</summary>
        public bool InputEnabled { get; set; } = true;
        public bool CameraControlEnabled { get; set; } = true;

        /// <summary>A pan, pinch or wheel zoom began (menus should close).</summary>
        public event Action CameraGestureStarted;
        /// <summary>The controller moved or zoomed the camera this frame.</summary>
        public event Action CameraMoved;
        /// <summary>Any accepted world tap (world position), before it is resolved.</summary>
        public event Action<Vector3> WorldTapped;
        /// <summary>A tap hit nothing interactive (close menus, deselect).</summary>
        public event Action<Vector3> EmptyTapped;
        public event Action<bool> HeroSelectionChanged;
        /// <summary>The selected hero was ordered to this point on the path.</summary>
        public event Action<Vector3> HeroMoveOrdered;
        /// <summary>A hero move or spell target landed off the path (show feedback).</summary>
        public event Action<Vector3> InvalidTap;
        public event Action<bool> TargetingChanged;

        public HeroUnit Hero => hero;
        public bool IsHeroSelected => heroSelected && hero != null;
        public bool IsTargeting => targeting;
        public bool HasBounds => hasBounds;
        public Rect Bounds => bounds;
        public float MinZoom => minZoom;
        public float MaxZoom => maxZoom;

        private const int NoFinger = int.MinValue;
        private const int MouseLeftId = -100;
        private const int MouseRightId = -101;

        private bool hasBounds;
        private Rect bounds;
        private float minZoom = 2f;
        private float maxZoom = 50f;

        private HeroUnit hero;
        private bool heroSelected;

        private bool targeting;
        private float targetingTolerance;
        private Action<Vector3> onTargetConfirm;
        private Action onTargetCancel;

        // Gesture state (one primary finger, optional second finger for pinch).
        private int primaryId = NoFinger;
        private int secondaryId = NoFinger;
        private Vector2 primaryStart, primaryLast, secondaryLast;
        private float primaryStartTime, lastMoveTime;
        private bool dragging, multiTouch, tapAllowed, gestureAnnounced, pinchPrimed;
        private bool heroDragCandidate, heroDragging;
        private readonly List<int> ignoredFingers = new List<int>(10);
        private Vector2 velocity;
        private int lastTouchFrame = -100;
        private bool movedThisFrame;

        private PointerEventData pointerData;
        private EventSystem pointerDataOwner;
        private readonly List<RaycastResult> raycastResults = new List<RaycastResult>(16);

        /// <summary>Create (or return) the single controller.</summary>
        public static TouchInputController Create(Camera camera = null)
        {
            if (Instance == null)
                new GameObject("[TouchInput]").AddComponent<TouchInputController>();
            if (camera != null) Instance.TargetCamera = camera;
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            UnityEngine.Input.multiTouchEnabled = true;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnDisable()
        {
            ResetGesture();
            velocity = Vector2.zero;
        }

        // ------------------------------------------------------------------ public API

        public Camera Cam => TargetCamera != null ? TargetCamera : Camera.main;

        /// <summary>Screen pixels per iOS point (approx.). 1 when the dpi is unknown.</summary>
        public static float PixelsPerPoint => Screen.dpi > 0f ? Screen.dpi / 160f : 1f;

        /// <summary>
        /// Constrain the camera to <paramref name="worldBounds"/> with orthographic
        /// size between min and max zoom. If the view is larger than the bounds on an
        /// axis, the camera centres on that axis instead.
        /// </summary>
        public void SetBounds(Rect worldBounds, float minZoomSize, float maxZoomSize)
        {
            bounds = worldBounds;
            minZoom = Mathf.Max(0.5f, Mathf.Min(minZoomSize, maxZoomSize));
            maxZoom = Mathf.Max(minZoom, Mathf.Max(minZoomSize, maxZoomSize));
            hasBounds = true;
            velocity = Vector2.zero;
            Camera cam = Cam;
            if (cam != null && cam.orthographic)
            {
                cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minZoom, maxZoom);
                ClampCamera(cam);
            }
        }

        public void ClearBounds() => hasBounds = false;

        /// <summary>Hero used for selection and move orders (null for hero-less levels).</summary>
        public void SetHero(HeroUnit newHero)
        {
            if (hero != newHero) DeselectHero();
            hero = newHero;
        }

        public bool SelectHero()
        {
            if (hero == null || hero.IsDead) return false;
            CancelTargeting();
            if (!heroSelected)
            {
                heroSelected = true;
                HeroSelectionChanged?.Invoke(true);
            }
            return true;
        }

        public void DeselectHero()
        {
            if (!heroSelected) return;
            heroSelected = false;
            HeroSelectionChanged?.Invoke(false);
        }

        public void ToggleHeroSelection()
        {
            if (IsHeroSelected) DeselectHero(); else SelectHero();
        }

        /// <summary>
        /// Spell targeting: the next world tap within <paramref name="tolerance"/> of
        /// a path calls <paramref name="onConfirm"/> with the projected path point.
        /// A tap elsewhere cancels (InvalidTap fires, then <paramref name="onCancel"/>).
        /// </summary>
        public void BeginTargeting(float tolerance, Action<Vector3> onConfirm, Action onCancel = null)
        {
            DeselectHero();
            if (targeting) CancelTargeting();
            targeting = true;
            targetingTolerance = tolerance > 0f ? tolerance : pathTolerance;
            onTargetConfirm = onConfirm;
            onTargetCancel = onCancel;
            TargetingChanged?.Invoke(true);
        }

        public void CancelTargeting()
        {
            if (!targeting) return;
            Action cancel = onTargetCancel;
            EndTargeting();
            cancel?.Invoke();
        }

        /// <summary>Back/Esc: cancel targeting or hero selection. True if something was cancelled.</summary>
        public bool CancelModes()
        {
            if (targeting) { CancelTargeting(); return true; }
            if (IsHeroSelected) { DeselectHero(); return true; }
            return false;
        }

        /// <summary>Clear per-level state before a level is torn down.</summary>
        public void ResetLevelState()
        {
            CancelTargeting();
            DeselectHero();
            hero = null;
            hasBounds = false;
            velocity = Vector2.zero;
            ResetGesture();
        }

        /// <summary>Kill pan inertia.</summary>
        public void StopCamera() => velocity = Vector2.zero;

        /// <summary>Centre the camera on a world point (clamped to bounds).</summary>
        public void FocusOn(Vector2 world)
        {
            Camera cam = Cam;
            if (cam == null) return;
            Vector3 p = cam.transform.position;
            cam.transform.position = new Vector3(world.x, world.y, p.z);
            ClampCamera(cam);
            movedThisFrame = true;
        }

        public void SetZoom(float orthographicSize)
        {
            Camera cam = Cam;
            if (cam == null || !cam.orthographic) return;
            cam.orthographicSize = hasBounds ? Mathf.Clamp(orthographicSize, minZoom, maxZoom) : orthographicSize;
            ClampCamera(cam);
            movedThisFrame = true;
        }

        public Vector3 ScreenToWorld(Vector2 screen)
        {
            Camera cam = Cam;
            if (cam == null) return Vector3.zero;
            Vector3 w = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            w.z = 0f;
            return w;
        }

        /// <summary>World units per screen pixel for the orthographic camera.</summary>
        public float WorldPerPixel(Camera cam)
        {
            if (cam == null || cam.pixelHeight <= 0) return 0.01f;
            return 2f * cam.orthographicSize / cam.pixelHeight;
        }

        // ------------------------------------------------------------------ frame loop

        private void Update()
        {
            movedThisFrame = false;
            Camera cam = Cam;

            if (hero == null || hero.IsDead) DeselectHero();

            int touchCount = UnityEngine.Input.touchCount;
            if (touchCount > 0)
            {
                // Touch wins over (simulated) mouse; drop any mouse gesture in flight.
                if (primaryId == MouseLeftId || primaryId == MouseRightId) ResetGesture();
                lastTouchFrame = Time.frameCount;
                ProcessTouches(touchCount, cam);
            }
            else
            {
                // Touches vanished without an Ended phase (app interruption): drop the gesture.
                if (primaryId >= 0) ResetGesture();
                if (Time.frameCount - lastTouchFrame > 2) ProcessMouse(cam);
            }

            if (primaryId == NoFinger) ApplyInertia(cam);

            if (movedThisFrame) CameraMoved?.Invoke();
        }

        private void ProcessTouches(int count, Camera cam)
        {
            for (int i = 0; i < count; i++)
            {
                Touch t = UnityEngine.Input.GetTouch(i);
                if (t.phase == TouchPhase.Began) FingerDown(t.fingerId, t.position, true);
                else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                    FingerUp(t.fingerId, t.position, t.phase == TouchPhase.Canceled);
            }

            if (primaryId == NoFinger) return;

            bool hasP = false, hasS = false;
            Vector2 p1 = default, p2 = default;
            for (int i = 0; i < count; i++)
            {
                Touch t = UnityEngine.Input.GetTouch(i);
                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) continue;
                if (t.fingerId == primaryId) { p1 = t.position; hasP = true; }
                else if (t.fingerId == secondaryId) { p2 = t.position; hasS = true; }
            }

            if (!hasS) secondaryId = NoFinger;
            if (hasP && hasS) Pinch(p1, p2, cam);
            else if (hasP) Drag(p1, cam);
            else ResetGesture(); // primary touch lost without an Ended phase
        }

        private void ProcessMouse(Camera cam)
        {
            if (!UnityEngine.Input.mousePresent) return;
            Vector2 mp = UnityEngine.Input.mousePosition;

            float wheel = UnityEngine.Input.mouseScrollDelta.y;
            if (wheel > 0.01f || wheel < -0.01f)
            {
                if (CameraControlEnabled && cam != null && cam.orthographic && !IsOverUI(mp))
                {
                    AnnounceGesture();
                    ZoomAround(cam, mp, cam.orthographicSize * Mathf.Pow(1f - wheelZoomStep, wheel));
                }
            }

            if (UnityEngine.Input.GetMouseButtonDown(0) && primaryId == NoFinger) FingerDown(MouseLeftId, mp, true);
            else if (UnityEngine.Input.GetMouseButtonDown(1) && primaryId == NoFinger) FingerDown(MouseRightId, mp, false);

            if (UnityEngine.Input.GetMouseButtonUp(0)) FingerUp(MouseLeftId, mp, false);
            if (UnityEngine.Input.GetMouseButtonUp(1)) FingerUp(MouseRightId, mp, false);

            if (primaryId == MouseLeftId || primaryId == MouseRightId) Drag(mp, cam);
        }

        // ------------------------------------------------------------------ gesture handling

        private void FingerDown(int id, Vector2 pos, bool canTap)
        {
            if (IsOverUI(pos))
            {
                if (!ignoredFingers.Contains(id)) ignoredFingers.Add(id);
                return;
            }

            if (primaryId == NoFinger)
            {
                primaryId = id;
                primaryStart = primaryLast = pos;
                primaryStartTime = lastMoveTime = Time.unscaledTime;
                dragging = false;
                multiTouch = false;
                gestureAnnounced = false;
                tapAllowed = canTap;
                velocity = Vector2.zero;
                heroDragging = false;
                // A press that starts on the hero can drag it to a new spot instead of panning.
                heroDragCandidate = canTap && InputEnabled && !targeting && !IsGameBlocked() && IsNearHero(pos, Cam);
            }
            else if (secondaryId == NoFinger && id >= 0 && primaryId >= 0)
            {
                secondaryId = id;
                secondaryLast = pos;
                multiTouch = true;
                pinchPrimed = false;
                velocity = Vector2.zero;
                heroDragCandidate = false;
                if (heroDragging) { heroDragging = false; DeselectHero(); }
                AnnounceGesture();
            }
        }

        private void FingerUp(int id, Vector2 pos, bool canceled)
        {
            if (ignoredFingers.Remove(id)) return;

            if (id == primaryId && heroDragging)
            {
                heroDragging = false;
                heroDragCandidate = false;
                primaryId = NoFinger;
                velocity = Vector2.zero;
                if (canceled || !InputEnabled || IsGameBlocked()) DeselectHero();
                else OrderHeroTo(ScreenToWorld(pos));
                return;
            }

            if (id == primaryId)
            {
                bool isTap = tapAllowed && !dragging && !multiTouch && !canceled &&
                             Time.unscaledTime - primaryStartTime <= maxTapSeconds;

                if (secondaryId != NoFinger)
                {
                    // Promote the remaining finger: keeps panning, never becomes a tap.
                    primaryId = secondaryId;
                    primaryLast = secondaryLast;
                    secondaryId = NoFinger;
                    dragging = true;
                }
                else
                {
                    primaryId = NoFinger;
                    if (Time.unscaledTime - lastMoveTime > 0.08f) velocity = Vector2.zero;
                }

                if (isTap) HandleTap(pos);
            }
            else if (id == secondaryId)
            {
                secondaryId = NoFinger;
                dragging = true;
                velocity = Vector2.zero;
            }
        }

        private void Drag(Vector2 pos, Camera cam)
        {
            if (!dragging)
            {
                float threshold = Mathf.Max(6f, dragThresholdPoints * PixelsPerPoint);
                if ((pos - primaryStart).sqrMagnitude < threshold * threshold) return;
                dragging = true;
                if (heroDragCandidate && SelectHero())
                {
                    heroDragging = true;
                    return;
                }
                AnnounceGesture();
            }
            if (heroDragging) { primaryLast = pos; return; }

            Vector2 delta = pos - primaryLast;
            primaryLast = pos;
            if (!CameraControlEnabled || cam == null || !cam.orthographic) return;
            if (delta.sqrMagnitude < 0.0001f) return;

            float wpp = WorldPerPixel(cam);
            Vector2 worldDelta = -delta * wpp;
            Vector3 before = cam.transform.position;
            cam.transform.position = new Vector3(before.x + worldDelta.x, before.y + worldDelta.y, before.z);
            ClampCamera(cam);
            movedThisFrame = true;

            float dt = Time.unscaledDeltaTime;
            if (dt > 0.0001f)
            {
                Vector3 after = cam.transform.position;
                Vector2 v = new Vector2((after.x - before.x) / dt, (after.y - before.y) / dt);
                velocity = Vector2.Lerp(velocity, v, 0.5f);
            }
            lastMoveTime = Time.unscaledTime;
        }

        private void Pinch(Vector2 p1, Vector2 p2, Camera cam)
        {
            if (!pinchPrimed)
            {
                // First frame with both fingers: just record positions so nothing jumps.
                primaryLast = p1;
                secondaryLast = p2;
                pinchPrimed = true;
                dragging = true;
                return;
            }

            Vector2 prevMid = (primaryLast + secondaryLast) * 0.5f;
            float prevDist = Vector2.Distance(primaryLast, secondaryLast);
            Vector2 mid = (p1 + p2) * 0.5f;
            float dist = Vector2.Distance(p1, p2);
            primaryLast = p1;
            secondaryLast = p2;
            dragging = true;

            if (!CameraControlEnabled || cam == null || !cam.orthographic) return;
            if (prevDist < 1f || dist < 1f) return;

            // Keep the world point under the previous midpoint under the new midpoint.
            Vector2 anchor = ScreenToWorldOrtho(cam, prevMid);
            float size = cam.orthographicSize * (prevDist / dist);
            if (hasBounds) size = Mathf.Clamp(size, minZoom, maxZoom);
            cam.orthographicSize = size;
            PlaceSoScreenPointShowsWorld(cam, mid, anchor);
            ClampCamera(cam);
            movedThisFrame = true;
            velocity = Vector2.zero;
        }

        private void ZoomAround(Camera cam, Vector2 screen, float newSize)
        {
            Vector2 anchor = ScreenToWorldOrtho(cam, screen);
            cam.orthographicSize = hasBounds ? Mathf.Clamp(newSize, minZoom, maxZoom) : Mathf.Max(0.5f, newSize);
            PlaceSoScreenPointShowsWorld(cam, screen, anchor);
            ClampCamera(cam);
            movedThisFrame = true;
        }

        private void ApplyInertia(Camera cam)
        {
            if (cam == null || !CameraControlEnabled) { velocity = Vector2.zero; return; }
            if (velocity.sqrMagnitude < minInertiaSpeed * minInertiaSpeed) { velocity = Vector2.zero; return; }

            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            Vector3 p = cam.transform.position;
            Vector3 wanted = new Vector3(p.x + velocity.x * dt, p.y + velocity.y * dt, p.z);
            cam.transform.position = wanted;
            ClampCamera(cam);
            Vector3 got = cam.transform.position;
            // Stop along an axis that hit the bounds.
            if (Mathf.Abs(got.x - wanted.x) > 0.0001f) velocity.x = 0f;
            if (Mathf.Abs(got.y - wanted.y) > 0.0001f) velocity.y = 0f;
            velocity *= Mathf.Exp(-inertiaDamping * dt);
            movedThisFrame = true;
        }

        private void AnnounceGesture()
        {
            if (gestureAnnounced && primaryId != NoFinger) return;
            gestureAnnounced = true;
            CameraGestureStarted?.Invoke();
        }

        private void ResetGesture()
        {
            if (heroDragging) DeselectHero();
            heroDragging = false;
            heroDragCandidate = false;
            primaryId = NoFinger;
            secondaryId = NoFinger;
            dragging = false;
            multiTouch = false;
            gestureAnnounced = false;
            ignoredFingers.Clear();
        }

        // ------------------------------------------------------------------ taps

        private void HandleTap(Vector2 screenPos)
        {
            if (!InputEnabled || IsGameBlocked()) return;
            Camera cam = Cam;
            if (cam == null) return;

            Vector3 world = ScreenToWorld(screenPos);
            Vector2 p = new Vector2(world.x, world.y);
            WorldTapped?.Invoke(world);

            if (targeting)
            {
                if (TryProjectOntoPath(p, targetingTolerance, out Vector3 target))
                {
                    Action<Vector3> confirm = onTargetConfirm;
                    EndTargeting();
                    confirm?.Invoke(target);
                }
                else
                {
                    InvalidTap?.Invoke(world);
                    CancelTargeting();
                }
                return;
            }

            // Taps are rare: OverlapPointAll's allocation is fine here.
            Collider2D[] hits = Physics2D.OverlapPointAll(p);
            TowerPlacement slot = null;
            bool heroColliderHit = false;
            if (hits != null)
            {
                for (int i = 0; i < hits.Length; i++)
                {
                    Collider2D c = hits[i];
                    if (c == null) continue;
                    if (hero != null && (c.transform == hero.transform || c.transform.IsChildOf(hero.transform)))
                    {
                        heroColliderHit = true;
                        continue;
                    }
                    if (slot == null) slot = c.GetComponentInParent<TowerPlacement>();
                }
            }

            bool heroAvailable = hero != null && !hero.IsDead;
            bool heroNear = heroAvailable && !heroColliderHit && slot == null &&
                            Vector2.Distance(new Vector2(hero.transform.position.x, hero.transform.position.y), p) <=
                            heroTapRadiusPoints * PixelsPerPoint * WorldPerPixel(cam);

            if (IsHeroSelected)
            {
                if (heroColliderHit || heroNear) { DeselectHero(); return; }
                if (slot != null) { DeselectHero(); slot.HandleTap(); return; }

                OrderHeroTo(world);
                return;
            }

            if (heroAvailable && heroColliderHit) { SelectHero(); return; }
            if (slot != null) { slot.HandleTap(); return; }
            if (heroNear) { SelectHero(); return; }
            EmptyTapped?.Invoke(world);
        }

        /// <summary>Send the selected hero to the nearest path point, or flag an invalid spot.</summary>
        private void OrderHeroTo(Vector3 world)
        {
            if (hero != null && !hero.IsDead &&
                TryProjectOntoPath(new Vector2(world.x, world.y), pathTolerance, out Vector3 dest))
            {
                hero.MoveTo(dest);
                DeselectHero();
                HeroMoveOrdered?.Invoke(dest);
            }
            else
            {
                DeselectHero();
                InvalidTap?.Invoke(world);
            }
        }

        private bool IsNearHero(Vector2 screenPos, Camera cam)
        {
            if (hero == null || hero.IsDead || cam == null) return false;
            Vector3 w = ScreenToWorld(screenPos);
            Vector3 h = hero.transform.position;
            float radius = Mathf.Max(0.5f, heroTapRadiusPoints * PixelsPerPoint * WorldPerPixel(cam));
            float dx = w.x - h.x, dy = w.y - h.y;
            return dx * dx + dy * dy <= radius * radius;
        }

        private static bool TryProjectOntoPath(Vector2 p, float tolerance, out Vector3 result)
        {
            WaveManager wm = WaveManager.Instance;
            if (wm == null || wm.Paths == null || wm.Paths.Count == 0)
            {
                // No world-space paths registered: nothing to validate against.
                result = new Vector3(p.x, p.y, 0f);
                return true;
            }
            return PathUtility.TryProjectOntoPaths(wm.Paths, p, tolerance, out result);
        }

        private void EndTargeting()
        {
            targeting = false;
            onTargetConfirm = null;
            onTargetCancel = null;
            TargetingChanged?.Invoke(false);
        }

        private static bool IsGameBlocked()
        {
            GameManager gm = GameManager.Instance;
            return gm != null && (gm.IsPaused || gm.IsGameOver || gm.IsVictory);
        }

        /// <summary>True if a uGUI element (RectTransform graphic) is under the screen point.</summary>
        public bool IsOverUI(Vector2 screenPos)
        {
            EventSystem es = EventSystem.current;
            if (es == null) return false;
            if (pointerData == null || pointerDataOwner != es)
            {
                pointerData = new PointerEventData(es);
                pointerDataOwner = es;
            }
            pointerData.position = screenPos;
            es.RaycastAll(pointerData, raycastResults);
            bool over = false;
            for (int i = 0; i < raycastResults.Count; i++)
            {
                GameObject go = raycastResults[i].gameObject;
                // Ignore Physics2DRaycaster hits on world sprites: only UI has RectTransforms.
                if (go != null && go.transform is RectTransform) { over = true; break; }
            }
            raycastResults.Clear();
            return over;
        }

        // ------------------------------------------------------------------ camera math

        private static Vector2 ScreenToWorldOrtho(Camera cam, Vector2 screen)
        {
            Rect pr = cam.pixelRect;
            float wpp = 2f * cam.orthographicSize / Mathf.Max(1f, pr.height);
            Vector3 c = cam.transform.position;
            return new Vector2(c.x + (screen.x - pr.center.x) * wpp, c.y + (screen.y - pr.center.y) * wpp);
        }

        private static void PlaceSoScreenPointShowsWorld(Camera cam, Vector2 screen, Vector2 world)
        {
            Rect pr = cam.pixelRect;
            float wpp = 2f * cam.orthographicSize / Mathf.Max(1f, pr.height);
            Vector3 c = cam.transform.position;
            cam.transform.position = new Vector3(world.x - (screen.x - pr.center.x) * wpp,
                                                 world.y - (screen.y - pr.center.y) * wpp, c.z);
        }

        private void ClampCamera(Camera cam)
        {
            if (!hasBounds || cam == null || !cam.orthographic) return;
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            Vector3 p = cam.transform.position;
            float x = halfW * 2f >= bounds.width ? bounds.center.x : Mathf.Clamp(p.x, bounds.xMin + halfW, bounds.xMax - halfW);
            float y = halfH * 2f >= bounds.height ? bounds.center.y : Mathf.Clamp(p.y, bounds.yMin + halfH, bounds.yMax - halfH);
            cam.transform.position = new Vector3(x, y, p.z);
        }
    }
}
