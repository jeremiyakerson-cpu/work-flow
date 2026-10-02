using TowerDefense.Visuals.Pure;
using UnityEngine;

namespace TowerDefense.Visuals
{
    /// <summary>
    /// One-shot camera framing for a level: orthographic, centred on the play
    /// area, sized so the whole world rect fits any aspect (iPhone 19.5:9 is
    /// height-bound, iPad 4:3 width-bound), with the theme background colour.
    /// No per-frame logic: the UI/Input workstream owns pan and zoom afterwards.
    /// </summary>
    public static class CameraSetup
    {
        /// <summary>Default fractional padding around the world rect.</summary>
        public const float DefaultPadding = 0.02f;

        /// <summary>Orthographic size that fits <paramref name="worldSize"/> on a screen of <paramref name="aspect"/> (width/height).</summary>
        public static float OrthoSizeToFit(Vector2 worldSize, float aspect, float padding = DefaultPadding) =>
            CameraFit.OrthoSizeToFit(worldSize.x, worldSize.y, aspect, padding);

        /// <summary>Frame the camera on the play area (0,0)-(worldSize) and set the background.</summary>
        public static Camera Frame(Camera camera, Vector2 worldSize) => Frame(camera, worldSize, DefaultPadding, Palette.Background);

        public static Camera Frame(Camera camera, Vector2 worldSize, float padding, Color background)
        {
            if (camera == null) return null;
            if (worldSize.x <= 0f || worldSize.y <= 0f) worldSize = new Vector2(32f, 18f);
            float aspect = camera.aspect > 0f ? camera.aspect
                         : (Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f);
            camera.orthographic = true;
            camera.orthographicSize = OrthoSizeToFit(worldSize, aspect, padding);
            camera.transform.position = new Vector3(worldSize.x * 0.5f, worldSize.y * 0.5f, -10f);
            camera.transform.rotation = Quaternion.identity;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            return camera;
        }

        /// <summary>Camera.main, or a new tagged "MainCamera" if the scene has none.</summary>
        public static Camera EnsureMainCamera()
        {
            Camera cam = Camera.main;
            if (cam != null) return cam;
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = go.AddComponent<Camera>();
            return cam;
        }
    }
}
