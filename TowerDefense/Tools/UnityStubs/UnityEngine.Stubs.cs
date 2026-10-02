// Compile-only stand-ins for the UnityEngine API surface this project uses.
// They let `dotnet build` type-check Assets/Scripts outside the Unity Editor.
// Nothing here runs real engine behaviour: members return defaults.
// Every type is partial so a workstream can add members in its own file
// (Tools/UnityStubs/<Area>.Stubs.cs) instead of editing this one.
// Signatures must match the real Unity 6 API - never invent members.
#pragma warning disable CS0067, CS0108, CS0114, CS0660, CS0661, CS1591
using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityEngine
{
    // ------------------------------------------------------------------ attributes
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)] public partial class HeaderAttribute : Attribute { public HeaderAttribute(string header) { } }
    [AttributeUsage(AttributeTargets.Field)] public partial class TooltipAttribute : Attribute { public TooltipAttribute(string tooltip) { } }
    [AttributeUsage(AttributeTargets.Field)] public partial class SerializeField : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public partial class SerializeReference : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public partial class HideInInspector : Attribute { }
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)] public partial class SpaceAttribute : Attribute { public SpaceAttribute() { } public SpaceAttribute(float height) { } }
    [AttributeUsage(AttributeTargets.Field)] public partial class RangeAttribute : Attribute { public RangeAttribute(float min, float max) { } }
    [AttributeUsage(AttributeTargets.Field)] public partial class MinAttribute : Attribute { public MinAttribute(float min) { } }
    [AttributeUsage(AttributeTargets.Field)] public partial class TextAreaAttribute : Attribute { public TextAreaAttribute() { } public TextAreaAttribute(int minLines, int maxLines) { } }
    [AttributeUsage(AttributeTargets.Field)] public partial class MultilineAttribute : Attribute { public MultilineAttribute() { } public MultilineAttribute(int lines) { } }
    [AttributeUsage(AttributeTargets.Class)] public partial class CreateAssetMenuAttribute : Attribute { public string fileName; public string menuName; public int order; }
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)] public partial class RequireComponent : Attribute { public RequireComponent(Type t) { } public RequireComponent(Type t, Type t2) { } public RequireComponent(Type t, Type t2, Type t3) { } }
    [AttributeUsage(AttributeTargets.Class)] public partial class DisallowMultipleComponent : Attribute { }
    [AttributeUsage(AttributeTargets.Class)] public partial class AddComponentMenu : Attribute { public AddComponentMenu(string menuName) { } public AddComponentMenu(string menuName, int order) { } }
    [AttributeUsage(AttributeTargets.Class)] public partial class DefaultExecutionOrder : Attribute { public DefaultExecutionOrder(int order) { } public int order => 0; }
    [AttributeUsage(AttributeTargets.Class)] public partial class ExecuteAlways : Attribute { }
    [AttributeUsage(AttributeTargets.Class)] public partial class ExecuteInEditMode : Attribute { }
    [AttributeUsage(AttributeTargets.Class)] public partial class HelpURLAttribute : Attribute { public HelpURLAttribute(string url) { } }
    [AttributeUsage(AttributeTargets.Field)] public partial class ContextMenuItemAttribute : Attribute { public ContextMenuItemAttribute(string name, string function) { } }
    [AttributeUsage(AttributeTargets.Method)] public partial class ContextMenu : Attribute { public ContextMenu(string itemName) { } }
    public enum RuntimeInitializeLoadType { AfterSceneLoad, BeforeSceneLoad, AfterAssembliesLoaded, BeforeSplashScreen, SubsystemRegistration }
    [AttributeUsage(AttributeTargets.Method)] public partial class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute() { } public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType loadType) { } }
    public partial class PropertyAttribute : Attribute { }

    // ------------------------------------------------------------------ math
    public partial struct Vector2 : IEquatable<Vector2>
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => default; public static Vector2 one => new Vector2(1, 1);
        public static Vector2 up => new Vector2(0, 1); public static Vector2 down => new Vector2(0, -1);
        public static Vector2 left => new Vector2(-1, 0); public static Vector2 right => new Vector2(1, 0);
        public float magnitude => 0; public float sqrMagnitude => 0; public Vector2 normalized => default;
        public float this[int i] { get => 0; set { } }
        public void Normalize() { }
        public void Set(float x, float y) { }
        public static float Distance(Vector2 a, Vector2 b) => 0;
        public static float Dot(Vector2 a, Vector2 b) => 0;
        public static float Angle(Vector2 from, Vector2 to) => 0;
        public static float SignedAngle(Vector2 from, Vector2 to) => 0;
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) => default;
        public static Vector2 LerpUnclamped(Vector2 a, Vector2 b, float t) => default;
        public static Vector2 MoveTowards(Vector2 current, Vector2 target, float maxDistanceDelta) => default;
        public static Vector2 ClampMagnitude(Vector2 v, float maxLength) => default;
        public static Vector2 Perpendicular(Vector2 v) => default;
        public static Vector2 Scale(Vector2 a, Vector2 b) => default;
        public static Vector2 Min(Vector2 a, Vector2 b) => default; public static Vector2 Max(Vector2 a, Vector2 b) => default;
        public static Vector2 operator +(Vector2 a, Vector2 b) => default; public static Vector2 operator -(Vector2 a, Vector2 b) => default;
        public static Vector2 operator -(Vector2 a) => default;
        public static Vector2 operator *(Vector2 a, float d) => default; public static Vector2 operator *(float d, Vector2 a) => default;
        public static Vector2 operator *(Vector2 a, Vector2 b) => default; public static Vector2 operator /(Vector2 a, float d) => default;
        public static bool operator ==(Vector2 a, Vector2 b) => true; public static bool operator !=(Vector2 a, Vector2 b) => false;
        public static implicit operator Vector2(Vector3 v) => default; public static implicit operator Vector3(Vector2 v) => default;
        public bool Equals(Vector2 other) => true; public override bool Equals(object o) => true; public override int GetHashCode() => 0;
    }

    public partial struct Vector3 : IEquatable<Vector3>
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public Vector3(float x, float y) { this.x = x; this.y = y; z = 0; }
        public static Vector3 zero => default; public static Vector3 one => default;
        public static Vector3 up => default; public static Vector3 down => default; public static Vector3 left => default;
        public static Vector3 right => default; public static Vector3 forward => default; public static Vector3 back => default;
        public float magnitude => 0; public float sqrMagnitude => 0; public Vector3 normalized => default;
        public float this[int i] { get => 0; set { } }
        public void Normalize() { }
        public void Set(float x, float y, float z) { }
        public static float Distance(Vector3 a, Vector3 b) => 0;
        public static float Dot(Vector3 a, Vector3 b) => 0;
        public static Vector3 Cross(Vector3 a, Vector3 b) => default;
        public static float Angle(Vector3 from, Vector3 to) => 0;
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => default;
        public static Vector3 LerpUnclamped(Vector3 a, Vector3 b, float t) => default;
        public static Vector3 Slerp(Vector3 a, Vector3 b, float t) => default;
        public static Vector3 MoveTowards(Vector3 current, Vector3 target, float maxDistanceDelta) => default;
        public static Vector3 SmoothDamp(Vector3 current, Vector3 target, ref Vector3 currentVelocity, float smoothTime) => default;
        public static Vector3 ClampMagnitude(Vector3 v, float maxLength) => default;
        public static Vector3 Scale(Vector3 a, Vector3 b) => default;
        public static Vector3 Min(Vector3 a, Vector3 b) => default; public static Vector3 Max(Vector3 a, Vector3 b) => default;
        public static Vector3 operator +(Vector3 a, Vector3 b) => default; public static Vector3 operator -(Vector3 a, Vector3 b) => default;
        public static Vector3 operator -(Vector3 a) => default;
        public static Vector3 operator *(Vector3 a, float d) => default; public static Vector3 operator *(float d, Vector3 a) => default;
        public static Vector3 operator /(Vector3 a, float d) => default;
        public static bool operator ==(Vector3 a, Vector3 b) => true; public static bool operator !=(Vector3 a, Vector3 b) => false;
        public bool Equals(Vector3 other) => true; public override bool Equals(object o) => true; public override int GetHashCode() => 0;
    }

    public partial struct Vector4 { public float x, y, z, w; public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; } public static Vector4 zero => default; public static Vector4 one => default; }

    public partial struct Vector2Int : IEquatable<Vector2Int>
    {
        public int x { get; set; } public int y { get; set; }
        public Vector2Int(int x, int y) { this.x = x; this.y = y; }
        public static Vector2Int zero => default; public static Vector2Int one => default;
        public static Vector2Int up => default; public static Vector2Int down => default; public static Vector2Int left => default; public static Vector2Int right => default;
        public static float Distance(Vector2Int a, Vector2Int b) => 0;
        public static Vector2Int operator +(Vector2Int a, Vector2Int b) => default; public static Vector2Int operator -(Vector2Int a, Vector2Int b) => default;
        public static Vector2Int operator *(Vector2Int a, int b) => default;
        public static bool operator ==(Vector2Int a, Vector2Int b) => true; public static bool operator !=(Vector2Int a, Vector2Int b) => false;
        public static implicit operator Vector2(Vector2Int v) => default;
        public bool Equals(Vector2Int other) => true; public override bool Equals(object o) => true; public override int GetHashCode() => 0;
    }

    public partial struct Vector3Int { public int x { get; set; } public int y { get; set; } public int z { get; set; } public Vector3Int(int x, int y, int z) { this.x = x; this.y = y; this.z = z; } }

    public partial struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Quaternion identity => default;
        public Vector3 eulerAngles { get => default; set { } }
        public static Quaternion Euler(float x, float y, float z) => default;
        public static Quaternion Euler(Vector3 euler) => default;
        public static Quaternion AngleAxis(float angle, Vector3 axis) => default;
        public static Quaternion LookRotation(Vector3 forward) => default;
        public static Quaternion LookRotation(Vector3 forward, Vector3 upwards) => default;
        public static Quaternion Lerp(Quaternion a, Quaternion b, float t) => default;
        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) => default;
        public static Quaternion RotateTowards(Quaternion from, Quaternion to, float maxDegreesDelta) => default;
        public static Quaternion operator *(Quaternion a, Quaternion b) => default;
        public static Vector3 operator *(Quaternion r, Vector3 p) => default;
    }

    public partial struct Rect
    {
        public Rect(float x, float y, float width, float height) { }
        public Rect(Vector2 position, Vector2 size) { }
        public float x { get; set; } public float y { get; set; } public float width { get; set; } public float height { get; set; }
        public float xMin { get; set; } public float yMin { get; set; } public float xMax { get; set; } public float yMax { get; set; }
        public Vector2 position { get; set; } public Vector2 size { get; set; } public Vector2 center { get; set; }
        public Vector2 min { get; set; } public Vector2 max { get; set; }
        public bool Contains(Vector2 point) => false; public bool Contains(Vector3 point) => false;
        public bool Overlaps(Rect other) => false;
        public static Rect zero => default;
    }

    public partial struct Bounds
    {
        public Bounds(Vector3 center, Vector3 size) { }
        public Vector3 center { get; set; } public Vector3 size { get; set; } public Vector3 extents { get; set; }
        public Vector3 min { get; set; } public Vector3 max { get; set; }
        public bool Contains(Vector3 point) => false; public void Encapsulate(Vector3 point) { } public void Encapsulate(Bounds b) { }
        public Vector3 ClosestPoint(Vector3 point) => default;
    }

    public partial struct Matrix4x4 { public static Matrix4x4 identity => default; }

    public static partial class Mathf
    {
        public const float PI = 3.14159274f; public const float Infinity = float.PositiveInfinity; public const float NegativeInfinity = float.NegativeInfinity;
        public const float Deg2Rad = 0.0174532924f; public const float Rad2Deg = 57.29578f; public static readonly float Epsilon = float.Epsilon;
        public static float Sin(float f) => 0; public static float Cos(float f) => 0; public static float Tan(float f) => 0;
        public static float Asin(float f) => 0; public static float Acos(float f) => 0; public static float Atan(float f) => 0; public static float Atan2(float y, float x) => 0;
        public static float Sqrt(float f) => 0; public static float Abs(float f) => 0; public static int Abs(int v) => 0;
        public static float Min(float a, float b) => 0; public static float Min(params float[] values) => 0; public static int Min(int a, int b) => 0; public static int Min(params int[] values) => 0;
        public static float Max(float a, float b) => 0; public static float Max(params float[] values) => 0; public static int Max(int a, int b) => 0; public static int Max(params int[] values) => 0;
        public static float Pow(float f, float p) => 0; public static float Exp(float p) => 0; public static float Log(float f) => 0; public static float Log(float f, float p) => 0; public static float Log10(float f) => 0;
        public static float Ceil(float f) => 0; public static float Floor(float f) => 0; public static float Round(float f) => 0;
        public static int CeilToInt(float f) => 0; public static int FloorToInt(float f) => 0; public static int RoundToInt(float f) => 0;
        public static float Sign(float f) => 0;
        public static float Clamp(float value, float min, float max) => 0; public static int Clamp(int value, int min, int max) => 0; public static float Clamp01(float value) => 0;
        public static float Lerp(float a, float b, float t) => 0; public static float LerpUnclamped(float a, float b, float t) => 0; public static float InverseLerp(float a, float b, float value) => 0;
        public static float LerpAngle(float a, float b, float t) => 0; public static float MoveTowards(float current, float target, float maxDelta) => 0;
        public static float MoveTowardsAngle(float current, float target, float maxDelta) => 0; public static float DeltaAngle(float current, float target) => 0;
        public static float SmoothStep(float from, float to, float t) => 0;
        public static float SmoothDamp(float current, float target, ref float currentVelocity, float smoothTime) => 0;
        public static float Repeat(float t, float length) => 0; public static float PingPong(float t, float length) => 0;
        public static bool Approximately(float a, float b) => false; public static float PerlinNoise(float x, float y) => 0;
        public static int NextPowerOfTwo(int value) => 0; public static bool IsPowerOfTwo(int value) => false;
    }

    public static partial class Random
    {
        public static float value => 0; public static int seed { get; set; }
        public static Vector2 insideUnitCircle => default; public static Vector3 insideUnitSphere => default; public static Vector3 onUnitSphere => default;
        public static float Range(float minInclusive, float maxInclusive) => 0; public static int Range(int minInclusive, int maxExclusive) => 0;
        public static void InitState(int seed) { }
        public static Color ColorHSV() => default; public static Color ColorHSV(float hueMin, float hueMax) => default;
    }

    public partial struct Color : IEquatable<Color>
    {
        public float r, g, b, a;
        public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; a = 1; }
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white => default; public static Color black => default; public static Color red => default; public static Color green => default;
        public static Color blue => default; public static Color yellow => default; public static Color cyan => default; public static Color magenta => default;
        public static Color gray => default; public static Color grey => default; public static Color clear => default;
        public float grayscale => 0; public Color linear => default; public Color gamma => default;
        public static Color Lerp(Color a, Color b, float t) => default; public static Color LerpUnclamped(Color a, Color b, float t) => default;
        public static Color HSVToRGB(float h, float s, float v) => default; public static void RGBToHSV(Color rgb, out float h, out float s, out float v) { h = s = v = 0; }
        public static Color operator *(Color a, float b) => default; public static Color operator *(Color a, Color b) => default; public static Color operator +(Color a, Color b) => default;
        public static Color operator -(Color a, Color b) => default; public static Color operator /(Color a, float b) => default;
        public static bool operator ==(Color a, Color b) => true; public static bool operator !=(Color a, Color b) => false;
        public static implicit operator Color(Color32 c) => default; public static implicit operator Vector4(Color c) => default;
        public bool Equals(Color other) => true; public override bool Equals(object o) => true; public override int GetHashCode() => 0;
    }

    public partial struct Color32 { public byte r, g, b, a; public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; } public static implicit operator Color32(Color c) => default; }

    public static partial class ColorUtility { public static bool TryParseHtmlString(string htmlString, out Color color) { color = default; return false; } public static string ToHtmlStringRGB(Color c) => ""; public static string ToHtmlStringRGBA(Color c) => ""; }

    public partial class AnimationCurve
    {
        public AnimationCurve() { } public AnimationCurve(params Keyframe[] keys) { }
        public Keyframe[] keys { get; set; } public int length => 0;
        public float Evaluate(float time) => 0; public int AddKey(float time, float value) => 0;
        public static AnimationCurve Linear(float timeStart, float valueStart, float timeEnd, float valueEnd) => null;
        public static AnimationCurve EaseInOut(float timeStart, float valueStart, float timeEnd, float valueEnd) => null;
        public static AnimationCurve Constant(float timeStart, float timeEnd, float value) => null;
    }
    public partial struct Keyframe { public Keyframe(float time, float value) { } public Keyframe(float time, float value, float inTangent, float outTangent) { } public float time { get; set; } public float value { get; set; } }

    public partial class Gradient
    {
        public GradientColorKey[] colorKeys { get; set; } public GradientAlphaKey[] alphaKeys { get; set; }
        public Color Evaluate(float time) => default; public void SetKeys(GradientColorKey[] colorKeys, GradientAlphaKey[] alphaKeys) { }
    }
    public partial struct GradientColorKey { public Color color; public float time; public GradientColorKey(Color col, float time) { color = col; this.time = time; } }
    public partial struct GradientAlphaKey { public float alpha; public float time; public GradientAlphaKey(float alpha, float time) { this.alpha = alpha; this.time = time; } }

    public partial struct LayerMask
    {
        public int value { get; set; }
        public static int GetMask(params string[] layerNames) => 0; public static int NameToLayer(string layerName) => 0; public static string LayerToName(int layer) => "";
        public static implicit operator int(LayerMask mask) => 0; public static implicit operator LayerMask(int intVal) => default;
    }

    // ------------------------------------------------------------------ object model
    public enum HideFlags { None = 0, HideInHierarchy = 1, HideInInspector = 2, DontSaveInEditor = 4, NotEditable = 8, DontSaveInBuild = 16, DontUnloadUnusedAsset = 32, DontSave = 52, HideAndDontSave = 61 }
    public enum FindObjectsInactive { Exclude, Include }
    public enum FindObjectsSortMode { None, InstanceID }

    public partial class Object
    {
        public string name { get; set; }
        public HideFlags hideFlags { get; set; }
        public int GetInstanceID() => 0;
        public static T Instantiate<T>(T original) where T : Object => null;
        public static T Instantiate<T>(T original, Transform parent) where T : Object => null;
        public static T Instantiate<T>(T original, Transform parent, bool worldPositionStays) where T : Object => null;
        public static T Instantiate<T>(T original, Vector3 position, Quaternion rotation) where T : Object => null;
        public static T Instantiate<T>(T original, Vector3 position, Quaternion rotation, Transform parent) where T : Object => null;
        public static Object Instantiate(Object original) => null;
        public static Object Instantiate(Object original, Vector3 position, Quaternion rotation) => null;
        public static void Destroy(Object obj) { } public static void Destroy(Object obj, float t) { }
        public static void DestroyImmediate(Object obj) { }
        public static void DontDestroyOnLoad(Object target) { }
        public static T FindFirstObjectByType<T>() where T : Object => null;
        public static T FindFirstObjectByType<T>(FindObjectsInactive findObjectsInactive) where T : Object => null;
        public static T FindAnyObjectByType<T>() where T : Object => null;
        public static T FindAnyObjectByType<T>(FindObjectsInactive findObjectsInactive) where T : Object => null;
        public static T[] FindObjectsByType<T>(FindObjectsSortMode sortMode) where T : Object => null;
        public static T[] FindObjectsByType<T>(FindObjectsInactive findObjectsInactive, FindObjectsSortMode sortMode) where T : Object => null;
        [Obsolete("Use FindFirstObjectByType or FindAnyObjectByType")] public static T FindObjectOfType<T>() where T : Object => null;
        public static bool operator ==(Object x, Object y) => ReferenceEquals(x, y);
        public static bool operator !=(Object x, Object y) => !ReferenceEquals(x, y);
        public static implicit operator bool(Object exists) => !ReferenceEquals(exists, null);
        public override string ToString() => name;
    }

    public partial class Component : Object
    {
        public Transform transform => null; public GameObject gameObject => null;
        public string tag { get; set; }
        public T GetComponent<T>() => default; public Component GetComponent(Type type) => null;
        public bool TryGetComponent<T>(out T component) { component = default; return false; }
        public T GetComponentInChildren<T>() => default; public T GetComponentInChildren<T>(bool includeInactive) => default;
        public T[] GetComponentsInChildren<T>() => null; public T[] GetComponentsInChildren<T>(bool includeInactive) => null;
        public T GetComponentInParent<T>() => default; public T[] GetComponents<T>() => null;
        public bool CompareTag(string tag) => false;
        public void SendMessage(string methodName) { } public void BroadcastMessage(string methodName) { }
    }

    public partial class Behaviour : Component { public bool enabled { get; set; } public bool isActiveAndEnabled => false; }

    public partial class Coroutine : YieldInstruction { }
    public partial class YieldInstruction { }
    public partial class WaitForSeconds : YieldInstruction { public WaitForSeconds(float seconds) { } }
    public partial class WaitForSecondsRealtime : CustomYieldInstruction { public WaitForSecondsRealtime(float time) { } public override bool keepWaiting => false; public float waitTime { get; set; } }
    public partial class WaitForEndOfFrame : YieldInstruction { }
    public partial class WaitForFixedUpdate : YieldInstruction { }
    public abstract partial class CustomYieldInstruction : IEnumerator { public abstract bool keepWaiting { get; } public object Current => null; public bool MoveNext() => keepWaiting; public void Reset() { } }
    public sealed partial class WaitUntil : CustomYieldInstruction { public WaitUntil(Func<bool> predicate) { } public override bool keepWaiting => false; }
    public sealed partial class WaitWhile : CustomYieldInstruction { public WaitWhile(Func<bool> predicate) { } public override bool keepWaiting => false; }

    public partial class MonoBehaviour : Behaviour
    {
        public Coroutine StartCoroutine(IEnumerator routine) => null;
        public Coroutine StartCoroutine(string methodName) => null;
        public void StopCoroutine(IEnumerator routine) { } public void StopCoroutine(Coroutine routine) { } public void StopCoroutine(string methodName) { }
        public void StopAllCoroutines() { }
        public void Invoke(string methodName, float time) { } public void InvokeRepeating(string methodName, float time, float repeatRate) { }
        public void CancelInvoke() { } public void CancelInvoke(string methodName) { } public bool IsInvoking(string methodName) => false;
        public bool useGUILayout { get; set; }
        public static void print(object message) { }
    }

    public partial class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject => null;
        public static ScriptableObject CreateInstance(Type type) => null;
        public static ScriptableObject CreateInstance(string className) => null;
    }

    public enum Space { World, Self }

    public partial class Transform : Component, IEnumerable
    {
        public Vector3 position { get; set; } public Vector3 localPosition { get; set; }
        public Quaternion rotation { get; set; } public Quaternion localRotation { get; set; }
        public Vector3 eulerAngles { get; set; } public Vector3 localEulerAngles { get; set; }
        public Vector3 localScale { get; set; } public Vector3 lossyScale => default;
        public Vector3 right { get; set; } public Vector3 up { get; set; } public Vector3 forward { get; set; }
        public Transform parent { get; set; } public Transform root => null; public int childCount => 0;
        public void SetParent(Transform parent) { } public void SetParent(Transform parent, bool worldPositionStays) { }
        public void SetPositionAndRotation(Vector3 position, Quaternion rotation) { }
        public void SetAsFirstSibling() { } public void SetAsLastSibling() { } public void SetSiblingIndex(int index) { } public int GetSiblingIndex() => 0;
        public Transform GetChild(int index) => null; public Transform Find(string n) => null;
        public void Translate(Vector3 translation) { } public void Translate(Vector3 translation, Space relativeTo) { } public void Translate(float x, float y, float z) { }
        public void Rotate(Vector3 eulers) { } public void Rotate(float xAngle, float yAngle, float zAngle) { } public void Rotate(Vector3 axis, float angle) { }
        public void LookAt(Transform target) { } public void LookAt(Vector3 worldPosition) { }
        public Vector3 TransformPoint(Vector3 position) => default; public Vector3 InverseTransformPoint(Vector3 position) => default;
        public Vector3 TransformDirection(Vector3 direction) => default; public Vector3 InverseTransformDirection(Vector3 direction) => default;
        public void DetachChildren() { } public bool IsChildOf(Transform parent) => false;
        public IEnumerator GetEnumerator() => null;
    }

    public partial class RectTransform : Transform
    {
        public Vector2 anchorMin { get; set; } public Vector2 anchorMax { get; set; } public Vector2 pivot { get; set; }
        public Vector2 anchoredPosition { get; set; } public Vector3 anchoredPosition3D { get; set; } public Vector2 sizeDelta { get; set; }
        public Vector2 offsetMin { get; set; } public Vector2 offsetMax { get; set; } public Rect rect => default;
        public enum Axis { Horizontal, Vertical }
        public enum Edge { Left, Right, Top, Bottom }
        public void SetSizeWithCurrentAnchors(Axis axis, float size) { }
        public void SetInsetAndSizeFromParentEdge(Edge edge, float inset, float size) { }
        public void GetWorldCorners(Vector3[] fourCornersArray) { } public void GetLocalCorners(Vector3[] fourCornersArray) { }
        public void ForceUpdateRectTransforms() { }
    }

    public sealed partial class GameObject : Object
    {
        public GameObject() { } public GameObject(string name) { } public GameObject(string name, params Type[] components) { }
        public Transform transform => null; public GameObject gameObject => this;
        public int layer { get; set; } public string tag { get; set; } public bool isStatic { get; set; }
        public bool activeSelf => false; public bool activeInHierarchy => false;
        public void SetActive(bool value) { }
        public T AddComponent<T>() where T : Component => null; public Component AddComponent(Type componentType) => null;
        public T GetComponent<T>() => default; public Component GetComponent(Type type) => null;
        public bool TryGetComponent<T>(out T component) { component = default; return false; }
        public T GetComponentInChildren<T>() => default; public T GetComponentInChildren<T>(bool includeInactive) => default;
        public T[] GetComponentsInChildren<T>() => null; public T[] GetComponentsInChildren<T>(bool includeInactive) => null;
        public T GetComponentInParent<T>() => default; public T[] GetComponents<T>() => null;
        public bool CompareTag(string tag) => false;
        public static GameObject Find(string name) => null; public static GameObject FindWithTag(string tag) => null; public static GameObject FindGameObjectWithTag(string tag) => null;
        public static GameObject[] FindGameObjectsWithTag(string tag) => null;
        public static GameObject CreatePrimitive(PrimitiveType type) => null;
        public void SendMessage(string methodName) { }
    }
    public enum PrimitiveType { Sphere, Capsule, Cylinder, Cube, Plane, Quad }

    // ------------------------------------------------------------------ time / debug / app
    public static partial class Time
    {
        public static float time => 0; public static float unscaledTime => 0; public static float realtimeSinceStartup => 0; public static double timeAsDouble => 0;
        public static float deltaTime => 0; public static float unscaledDeltaTime => 0; public static float smoothDeltaTime => 0;
        public static float fixedDeltaTime { get; set; } public static float fixedUnscaledDeltaTime => 0; public static float maximumDeltaTime { get; set; }
        public static float timeScale { get; set; } public static int frameCount => 0; public static float timeSinceLevelLoad => 0;
    }

    public enum LogType { Error, Assert, Warning, Log, Exception }
    public static partial class Debug
    {
        public static void Log(object message) { } public static void Log(object message, Object context) { }
        public static void LogWarning(object message) { } public static void LogWarning(object message, Object context) { }
        public static void LogError(object message) { } public static void LogError(object message, Object context) { }
        public static void LogException(Exception exception) { } public static void LogException(Exception exception, Object context) { }
        public static void LogFormat(string format, params object[] args) { } public static void LogWarningFormat(string format, params object[] args) { } public static void LogErrorFormat(string format, params object[] args) { }
        public static void Assert(bool condition) { } public static void Assert(bool condition, object message) { }
        public static void DrawLine(Vector3 start, Vector3 end) { } public static void DrawLine(Vector3 start, Vector3 end, Color color) { } public static void DrawLine(Vector3 start, Vector3 end, Color color, float duration) { }
        public static void DrawRay(Vector3 start, Vector3 dir) { } public static void DrawRay(Vector3 start, Vector3 dir, Color color) { }
        public static bool isDebugBuild => false;
    }

    public static partial class Gizmos
    {
        public static Color color { get; set; } public static Matrix4x4 matrix { get; set; }
        public static void DrawLine(Vector3 from, Vector3 to) { } public static void DrawWireSphere(Vector3 center, float radius) { } public static void DrawSphere(Vector3 center, float radius) { }
        public static void DrawWireCube(Vector3 center, Vector3 size) { } public static void DrawCube(Vector3 center, Vector3 size) { } public static void DrawRay(Vector3 from, Vector3 direction) { }
        public static void DrawIcon(Vector3 center, string name) { }
    }

    public enum RuntimePlatform { OSXEditor = 0, OSXPlayer = 1, WindowsPlayer = 2, WindowsEditor = 7, IPhonePlayer = 8, Android = 11, LinuxPlayer = 13, LinuxEditor = 16, WebGLPlayer = 17 }
    public enum NetworkReachability { NotReachable, ReachableViaCarrierDataNetwork, ReachableViaLocalAreaNetwork }
    public enum SystemLanguage { English = 10, Unknown = 43 }
    public enum ApplicationInstallMode { Unknown, Store, DeveloperBuild, Adhoc, Enterprise, Editor }

    public static partial class Application
    {
        public static string persistentDataPath => ""; public static string dataPath => ""; public static string streamingAssetsPath => ""; public static string temporaryCachePath => "";
        public static int targetFrameRate { get; set; } public static bool isPlaying => false; public static bool isEditor => false; public static bool isMobilePlatform => false;
        public static bool isFocused => false; public static bool runInBackground { get; set; }
        public static RuntimePlatform platform => default; public static string version => ""; public static string productName => ""; public static string identifier => ""; public static string companyName => ""; public static string unityVersion => "";
        public static NetworkReachability internetReachability => default; public static SystemLanguage systemLanguage => default; public static ApplicationInstallMode installMode => default;
        public static void Quit() { } public static void Quit(int exitCode) { } public static void OpenURL(string url) { }
        public delegate void LogCallback(string condition, string stackTrace, LogType type);
        public static event LogCallback logMessageReceived;
        public static event Action quitting; public static event Action<bool> focusChanged; public static event Action lowMemory;
    }

    public enum DeviceType { Unknown, Handheld, Console, Desktop }
    public static partial class SystemInfo
    {
        public static string deviceModel => ""; public static string deviceName => ""; public static string deviceUniqueIdentifier => ""; public static string operatingSystem => "";
        public static DeviceType deviceType => default; public static int systemMemorySize => 0; public static int graphicsMemorySize => 0; public static int processorCount => 0;
        public static bool supportsVibration => false; public static int batteryLevelInt => 0; public static float batteryLevel => 0;
    }

    public static partial class Handheld { public static void Vibrate() { } }

    public static partial class PlayerPrefs
    {
        public static void SetInt(string key, int value) { } public static int GetInt(string key) => 0; public static int GetInt(string key, int defaultValue) => 0;
        public static void SetFloat(string key, float value) { } public static float GetFloat(string key) => 0; public static float GetFloat(string key, float defaultValue) => 0;
        public static void SetString(string key, string value) { } public static string GetString(string key) => ""; public static string GetString(string key, string defaultValue) => "";
        public static bool HasKey(string key) => false; public static void DeleteKey(string key) { } public static void DeleteAll() { } public static void Save() { }
    }

    public static partial class JsonUtility
    {
        public static string ToJson(object obj) => ""; public static string ToJson(object obj, bool prettyPrint) => "";
        public static T FromJson<T>(string json) => default; public static object FromJson(string json, Type type) => null;
        public static void FromJsonOverwrite(string json, object objectToOverwrite) { }
    }

    public partial class ResourceRequest : AsyncOperation { public Object asset => null; }
    public partial class AsyncOperation : YieldInstruction { public bool isDone => false; public float progress => 0; public bool allowSceneActivation { get; set; } public event Action<AsyncOperation> completed; }
    public static partial class Resources
    {
        public static T Load<T>(string path) where T : Object => null; public static Object Load(string path) => null;
        public static T[] LoadAll<T>(string path) where T : Object => null;
        public static ResourceRequest LoadAsync<T>(string path) where T : Object => null;
        public static T GetBuiltinResource<T>(string path) where T : Object => null;
        public static void UnloadAsset(Object assetToUnload) { } public static AsyncOperation UnloadUnusedAssets() => null;
    }

    public partial class TextAsset : Object { public TextAsset() { } public TextAsset(string text) { } public string text => ""; public byte[] bytes => null; }

    // ------------------------------------------------------------------ screen / input
    public enum ScreenOrientation { Portrait = 1, PortraitUpsideDown = 2, LandscapeLeft = 3, LandscapeRight = 4, AutoRotation = 5, Landscape = 3 }
    public partial struct Resolution { public int width { get; set; } public int height { get; set; } }
    public static partial class SleepTimeout { public const int NeverSleep = -1; public const int SystemSetting = -2; }
    public static partial class Screen
    {
        public static int width => 0; public static int height => 0; public static float dpi => 0; public static Rect safeArea => default;
        public static Rect[] cutouts => null;
        public static ScreenOrientation orientation { get; set; } public static int sleepTimeout { get; set; } public static bool fullScreen { get; set; }
        public static bool autorotateToPortrait { get; set; } public static bool autorotateToPortraitUpsideDown { get; set; }
        public static bool autorotateToLandscapeLeft { get; set; } public static bool autorotateToLandscapeRight { get; set; }
        public static Resolution currentResolution => default;
    }

    public enum TouchPhase { Began, Moved, Stationary, Ended, Canceled }
    public enum TouchType { Direct, Indirect, Stylus }
    public partial struct Touch
    {
        public int fingerId { get; set; } public Vector2 position { get; set; } public Vector2 rawPosition { get; set; } public Vector2 deltaPosition { get; set; }
        public float deltaTime { get; set; } public int tapCount { get; set; } public TouchPhase phase { get; set; } public TouchType type { get; set; }
        public float pressure { get; set; } public float radius { get; set; }
    }
    public enum KeyCode { None = 0, Backspace = 8, Tab = 9, Return = 13, Escape = 27, Space = 32, Alpha0 = 48, Alpha1, Alpha2, Alpha3, Alpha4, Alpha5, Alpha6, Alpha7, Alpha8, Alpha9, A = 97, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z, Mouse0 = 323, Mouse1, Mouse2 }
    public static partial class Input
    {
        public static int touchCount => 0; public static Touch GetTouch(int index) => default; public static Touch[] touches => null; public static bool touchSupported => false;
        public static bool multiTouchEnabled { get; set; } public static bool simulateMouseWithTouches { get; set; }
        public static Vector3 mousePosition => default; public static Vector2 mouseScrollDelta => default; public static bool mousePresent => false;
        public static bool GetMouseButton(int button) => false; public static bool GetMouseButtonDown(int button) => false; public static bool GetMouseButtonUp(int button) => false;
        public static bool GetKey(KeyCode key) => false; public static bool GetKeyDown(KeyCode key) => false; public static bool GetKeyUp(KeyCode key) => false;
        public static bool anyKeyDown => false; public static float GetAxis(string axisName) => 0;
    }

    // ------------------------------------------------------------------ camera / rendering
    public enum CameraClearFlags { Skybox = 1, Color = 2, SolidColor = 2, Depth = 3, Nothing = 4 }
    public partial class Camera : Behaviour
    {
        public static Camera main => null;
        public bool orthographic { get; set; } public float orthographicSize { get; set; } public float fieldOfView { get; set; } public float aspect { get; set; }
        public float nearClipPlane { get; set; } public float farClipPlane { get; set; } public float depth { get; set; }
        public Color backgroundColor { get; set; } public CameraClearFlags clearFlags { get; set; } public int cullingMask { get; set; }
        public Rect rect { get; set; } public Rect pixelRect { get; set; } public int pixelWidth => 0; public int pixelHeight => 0;
        public Vector3 ScreenToWorldPoint(Vector3 position) => default; public Vector3 WorldToScreenPoint(Vector3 position) => default;
        public Vector3 ViewportToWorldPoint(Vector3 position) => default; public Vector3 WorldToViewportPoint(Vector3 position) => default;
        public Vector3 ScreenToViewportPoint(Vector3 position) => default; public Vector3 ViewportToScreenPoint(Vector3 position) => default;
        public Ray ScreenPointToRay(Vector3 pos) => default;
    }
    public partial struct Ray { public Ray(Vector3 origin, Vector3 direction) { } public Vector3 origin { get; set; } public Vector3 direction { get; set; } public Vector3 GetPoint(float distance) => default; }

    public partial class Renderer : Component
    {
        public bool enabled { get; set; } public Material material { get; set; } public Material sharedMaterial { get; set; } public Material[] materials { get; set; }
        public int sortingOrder { get; set; } public string sortingLayerName { get; set; } public int sortingLayerID { get; set; } public Bounds bounds => default; public bool isVisible => false;
    }
    public enum SpriteDrawMode { Simple, Sliced, Tiled }
    public enum SpriteMeshType { FullRect, Tight }
    public partial class SpriteRenderer : Renderer
    {
        public Sprite sprite { get; set; } public Color color { get; set; } public bool flipX { get; set; } public bool flipY { get; set; }
        public SpriteDrawMode drawMode { get; set; } public Vector2 size { get; set; }
    }
    public partial class Sprite : Object
    {
        public Rect rect => default; public Texture2D texture => null; public Bounds bounds => default; public Vector2 pivot => default; public float pixelsPerUnit => 0; public Vector4 border => default;
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot) => null;
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit) => null;
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit, uint extrude, SpriteMeshType meshType, Vector4 border) => null;
    }
    public enum TextureFormat { Alpha8 = 1, RGB24 = 3, RGBA32 = 4, ARGB32 = 5 }
    public enum FilterMode { Point, Bilinear, Trilinear }
    public enum TextureWrapMode { Repeat, Clamp, Mirror, MirrorOnce }
    public partial class Texture : Object { public int width { get; set; } public int height { get; set; } public FilterMode filterMode { get; set; } public TextureWrapMode wrapMode { get; set; } }
    public partial class Texture2D : Texture
    {
        public Texture2D(int width, int height) { } public Texture2D(int width, int height, TextureFormat textureFormat, bool mipChain) { }
        public static Texture2D whiteTexture => null; public static Texture2D blackTexture => null;
        public void SetPixel(int x, int y, Color color) { } public void SetPixels(Color[] colors) { } public void SetPixels32(Color32[] colors) { }
        public Color GetPixel(int x, int y) => default; public Color[] GetPixels() => null;
        public void Apply() { } public void Apply(bool updateMipmaps) { } public void Apply(bool updateMipmaps, bool makeNoLongerReadable) { }
        public byte[] EncodeToPNG() => null; public bool LoadImage(byte[] data) => false;
    }
    public partial class Shader : Object { public static Shader Find(string name) => null; public static int PropertyToID(string name) => 0; }
    public partial class Material : Object
    {
        public Material(Shader shader) { } public Material(Material source) { }
        public Color color { get; set; } public Shader shader { get; set; } public Texture mainTexture { get; set; } public int renderQueue { get; set; }
        public void SetColor(string name, Color value) { } public void SetColor(int nameID, Color value) { } public void SetFloat(string name, float value) { } public void SetFloat(int nameID, float value) { }
        public void SetTexture(string name, Texture value) { } public Color GetColor(string name) => default; public float GetFloat(string name) => 0;
    }
    public partial class MaterialPropertyBlock { public void SetColor(string name, Color value) { } public void SetFloat(string name, float value) { } public void Clear() { } }
    public enum LineTextureMode { Stretch, Tile, DistributePerSegment, RepeatPerSegment }
    public enum LineAlignment { View, TransformZ }
    public partial class LineRenderer : Renderer
    {
        public int positionCount { get; set; } public bool useWorldSpace { get; set; } public bool loop { get; set; }
        public float startWidth { get; set; } public float endWidth { get; set; } public float widthMultiplier { get; set; } public AnimationCurve widthCurve { get; set; }
        public Color startColor { get; set; } public Color endColor { get; set; } public Gradient colorGradient { get; set; }
        public int numCornerVertices { get; set; } public int numCapVertices { get; set; } public LineTextureMode textureMode { get; set; } public LineAlignment alignment { get; set; }
        public void SetPosition(int index, Vector3 position) { } public void SetPositions(Vector3[] positions) { } public Vector3 GetPosition(int index) => default;
    }
    public partial class TrailRenderer : Renderer { public float time { get; set; } public float startWidth { get; set; } public float endWidth { get; set; } public Color startColor { get; set; } public Color endColor { get; set; } public void Clear() { } }
    public partial class Font : Object { public Font() { } public Font(string name) { } public static Font CreateDynamicFontFromOSFont(string fontname, int size) => null; public static string[] GetOSInstalledFontNames() => null; }
    public enum FontStyle { Normal, Bold, Italic, BoldAndItalic }
    public enum TextAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight }
    public enum HorizontalWrapMode { Wrap, Overflow }
    public enum VerticalWrapMode { Truncate, Overflow }

    // ------------------------------------------------------------------ 2D physics
    public enum RigidbodyType2D { Dynamic, Kinematic, Static }
    public enum CollisionDetectionMode2D { Discrete, Continuous }
    public enum RigidbodyInterpolation2D { None, Interpolate, Extrapolate }
    public partial class Rigidbody2D : Component
    {
        public RigidbodyType2D bodyType { get; set; } public bool isKinematic { get; set; } public float gravityScale { get; set; } public bool simulated { get; set; }
        public Vector2 position { get; set; } public float rotation { get; set; } public Vector2 linearVelocity { get; set; } public float mass { get; set; }
        public bool useFullKinematicContacts { get; set; } public RigidbodyInterpolation2D interpolation { get; set; } public CollisionDetectionMode2D collisionDetectionMode { get; set; }
        public void MovePosition(Vector2 position) { } public void MoveRotation(float angle) { } public void AddForce(Vector2 force) { }
    }
    public partial class Collider2D : Behaviour
    {
        public bool isTrigger { get; set; } public Vector2 offset { get; set; } public Bounds bounds => default; public Rigidbody2D attachedRigidbody => null;
        public bool OverlapPoint(Vector2 point) => false; public Vector2 ClosestPoint(Vector2 position) => default;
    }
    public partial class BoxCollider2D : Collider2D { public Vector2 size { get; set; } }
    public partial class CircleCollider2D : Collider2D { public float radius { get; set; } }
    public partial class CapsuleCollider2D : Collider2D { public Vector2 size { get; set; } }
    public partial class PolygonCollider2D : Collider2D { public Vector2[] points { get; set; } public int pathCount { get; set; } public void SetPath(int index, Vector2[] points) { } }
    public partial class EdgeCollider2D : Collider2D { public Vector2[] points { get; set; } public float edgeRadius { get; set; } }
    public partial struct RaycastHit2D { public Collider2D collider => null; public Transform transform => null; public Vector2 point { get; set; } public Vector2 normal { get; set; } public float distance { get; set; } public static implicit operator bool(RaycastHit2D hit) => false; }
    public partial struct ContactFilter2D { public bool useTriggers; public bool useLayerMask; public LayerMask layerMask; public void SetLayerMask(LayerMask layerMask) { } public ContactFilter2D NoFilter() => default; }
    public partial class Collision2D { public Collider2D collider => null; public GameObject gameObject => null; public Transform transform => null; public Rigidbody2D rigidbody => null; }
    public static partial class Physics2D
    {
        public const int DefaultRaycastLayers = -5; public const int AllLayers = -1;
        public static bool queriesHitTriggers { get; set; } public static Vector2 gravity { get; set; }
        public static Collider2D[] OverlapCircleAll(Vector2 point, float radius) => null;
        public static Collider2D[] OverlapCircleAll(Vector2 point, float radius, int layerMask) => null;
        public static Collider2D OverlapCircle(Vector2 point, float radius) => null;
        public static Collider2D OverlapCircle(Vector2 point, float radius, int layerMask) => null;
        public static int OverlapCircle(Vector2 point, float radius, ContactFilter2D contactFilter, Collider2D[] results) => 0;
        [Obsolete("Use OverlapCircle with ContactFilter2D")] public static int OverlapCircleNonAlloc(Vector2 point, float radius, Collider2D[] results, int layerMask) => 0;
        public static Collider2D OverlapPoint(Vector2 point) => null; public static Collider2D OverlapPoint(Vector2 point, int layerMask) => null;
        public static Collider2D[] OverlapPointAll(Vector2 point) => null; public static Collider2D[] OverlapPointAll(Vector2 point, int layerMask) => null;
        public static Collider2D OverlapBox(Vector2 point, Vector2 size, float angle) => null; public static Collider2D OverlapBox(Vector2 point, Vector2 size, float angle, int layerMask) => null;
        public static RaycastHit2D Raycast(Vector2 origin, Vector2 direction) => default;
        public static RaycastHit2D Raycast(Vector2 origin, Vector2 direction, float distance) => default;
        public static RaycastHit2D Raycast(Vector2 origin, Vector2 direction, float distance, int layerMask) => default;
        public static RaycastHit2D[] RaycastAll(Vector2 origin, Vector2 direction, float distance, int layerMask) => null;
        public static RaycastHit2D GetRayIntersection(Ray ray) => default; public static RaycastHit2D GetRayIntersection(Ray ray, float distance, int layerMask) => default;
        public static void IgnoreLayerCollision(int layer1, int layer2, bool ignore) { }
    }

    // ------------------------------------------------------------------ audio
    public enum AudioRolloffMode { Logarithmic, Linear, Custom }
    public partial class AudioClip : Object
    {
        public float length => 0; public int samples => 0; public int channels => 0; public int frequency => 0;
        public static AudioClip Create(string name, int lengthSamples, int channels, int frequency, bool stream) => null;
        public bool SetData(float[] data, int offsetSamples) => false; public bool GetData(float[] data, int offsetSamples) => false;
    }
    public partial class AudioSource : Behaviour
    {
        public AudioClip clip { get; set; } public float volume { get; set; } public float pitch { get; set; } public bool loop { get; set; } public bool mute { get; set; }
        public bool playOnAwake { get; set; } public bool isPlaying => false; public float spatialBlend { get; set; } public float time { get; set; } public int priority { get; set; }
        public bool ignoreListenerPause { get; set; } public AudioRolloffMode rolloffMode { get; set; }
        public void Play() { } public void Stop() { } public void Pause() { } public void UnPause() { } public void PlayOneShot(AudioClip clip) { } public void PlayOneShot(AudioClip clip, float volumeScale) { }
        public static void PlayClipAtPoint(AudioClip clip, Vector3 position) { }
    }
    public partial class AudioListener : Behaviour { public static float volume { get; set; } public static bool pause { get; set; } }

    // ------------------------------------------------------------------ UI base types (uGUI lives in UnityEngine.UI below)
    public enum RenderMode { ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace }
    public enum AdditionalCanvasShaderChannels { None = 0, TexCoord1 = 1, TexCoord2 = 2, TexCoord3 = 4, Normal = 8, Tangent = 16 }
    public sealed partial class Canvas : Behaviour
    {
        public RenderMode renderMode { get; set; } public Camera worldCamera { get; set; } public float planeDistance { get; set; } public int sortingOrder { get; set; } public string sortingLayerName { get; set; }
        public bool overrideSorting { get; set; } public bool pixelPerfect { get; set; } public float scaleFactor { get; set; } public bool isRootCanvas => false; public Canvas rootCanvas => null;
        public AdditionalCanvasShaderChannels additionalShaderChannels { get; set; }
        public static void ForceUpdateCanvases() { }
    }
    public sealed partial class CanvasGroup : Behaviour { public float alpha { get; set; } public bool interactable { get; set; } public bool blocksRaycasts { get; set; } public bool ignoreParentGroups { get; set; } }
    public sealed partial class CanvasRenderer : Component { public void SetAlpha(float alpha) { } public void SetColor(Color color) { } public bool cull { get; set; } }
    public static partial class RectTransformUtility
    {
        public static bool ScreenPointToLocalPointInRectangle(RectTransform rect, Vector2 screenPoint, Camera cam, out Vector2 localPoint) { localPoint = default; return false; }
        public static bool RectangleContainsScreenPoint(RectTransform rect, Vector2 screenPoint) => false;
        public static bool RectangleContainsScreenPoint(RectTransform rect, Vector2 screenPoint, Camera cam) => false;
        public static Vector2 WorldToScreenPoint(Camera cam, Vector3 worldPoint) => default;
    }

    public partial class GUIStyle { }
    public static partial class GUI { public static Color color { get; set; } public static bool Button(Rect position, string text) => false; public static void Label(Rect position, string text) { } public static void Box(Rect position, string text) { } }
}

namespace UnityEngine.Serialization
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)] public partial class FormerlySerializedAsAttribute : Attribute { public FormerlySerializedAsAttribute(string oldName) { } }
}

namespace UnityEngine.Scripting
{
    [AttributeUsage(AttributeTargets.All)] public partial class PreserveAttribute : Attribute { }
}

namespace UnityEngine.Events
{
    public delegate void UnityAction(); public delegate void UnityAction<T0>(T0 arg0); public delegate void UnityAction<T0, T1>(T0 arg0, T1 arg1); public delegate void UnityAction<T0, T1, T2>(T0 arg0, T1 arg1, T2 arg2);
    public abstract partial class UnityEventBase { public void RemoveAllListeners() { } public int GetPersistentEventCount() => 0; }
    [Serializable] public partial class UnityEvent : UnityEventBase { public void AddListener(UnityAction call) { } public void RemoveListener(UnityAction call) { } public void Invoke() { } }
    [Serializable] public partial class UnityEvent<T0> : UnityEventBase { public void AddListener(UnityAction<T0> call) { } public void RemoveListener(UnityAction<T0> call) { } public void Invoke(T0 arg0) { } }
    [Serializable] public partial class UnityEvent<T0, T1> : UnityEventBase { public void AddListener(UnityAction<T0, T1> call) { } public void RemoveListener(UnityAction<T0, T1> call) { } public void Invoke(T0 arg0, T1 arg1) { } }
}

namespace UnityEngine.SceneManagement
{
    public enum LoadSceneMode { Single, Additive }
    public partial struct Scene { public string name => ""; public int buildIndex => 0; public bool isLoaded => false; public string path => ""; public bool IsValid() => false; public GameObject[] GetRootGameObjects() => null; }
    public static partial class SceneManager
    {
        public static int sceneCount => 0; public static int sceneCountInBuildSettings => 0;
        public static Scene GetActiveScene() => default; public static bool SetActiveScene(Scene scene) => false;
        public static void LoadScene(int sceneBuildIndex) { } public static void LoadScene(string sceneName) { } public static void LoadScene(string sceneName, LoadSceneMode mode) { }
        public static AsyncOperation LoadSceneAsync(int sceneBuildIndex) => null; public static AsyncOperation LoadSceneAsync(string sceneName) => null;
        public static Scene CreateScene(string sceneName) => default;
        public static event UnityEngine.Events.UnityAction<Scene, LoadSceneMode> sceneLoaded; public static event UnityEngine.Events.UnityAction<Scene> sceneUnloaded;
    }
}

namespace UnityEngine.EventSystems
{
    public partial class UIBehaviour : MonoBehaviour { public bool IsActive() => false; }
    public partial class EventSystem : UIBehaviour
    {
        public static EventSystem current { get; set; } public GameObject currentSelectedGameObject => null; public bool sendNavigationEvents { get; set; } public int pixelDragThreshold { get; set; }
        public bool IsPointerOverGameObject() => false; public bool IsPointerOverGameObject(int pointerId) => false;
        public void SetSelectedGameObject(GameObject selected) { } public void RaycastAll(PointerEventData eventData, List<RaycastResult> raycastResults) { }
    }
    public abstract partial class BaseInputModule : UIBehaviour { }
    public partial class StandaloneInputModule : BaseInputModule { }
    public abstract partial class BaseRaycaster : UIBehaviour { }
    public partial class Physics2DRaycaster : BaseRaycaster { public LayerMask eventMask { get; set; } }
    public partial struct RaycastResult { public GameObject gameObject { get; set; } public float distance; public int depth; public Vector2 screenPosition; }
    public abstract partial class AbstractEventData { public bool used => false; public virtual void Use() { } }
    public partial class BaseEventData : AbstractEventData { public BaseEventData(EventSystem eventSystem) { } public GameObject selectedObject { get; set; } }
    public partial class PointerEventData : BaseEventData
    {
        public enum InputButton { Left, Right, Middle }
        public PointerEventData(EventSystem eventSystem) : base(eventSystem) { }
        public Vector2 position { get; set; } public Vector2 delta { get; set; } public Vector2 pressPosition { get; set; } public int pointerId { get; set; } public int clickCount { get; set; }
        public InputButton button { get; set; } public bool dragging { get; set; } public GameObject pointerPress { get; set; } public GameObject pointerEnter { get; set; }
        public RaycastResult pointerCurrentRaycast { get; set; } public RaycastResult pointerPressRaycast { get; set; } public Camera pressEventCamera => null; public Camera enterEventCamera => null;
    }
    public interface IEventSystemHandler { }
    public interface IPointerClickHandler : IEventSystemHandler { void OnPointerClick(PointerEventData eventData); }
    public interface IPointerDownHandler : IEventSystemHandler { void OnPointerDown(PointerEventData eventData); }
    public interface IPointerUpHandler : IEventSystemHandler { void OnPointerUp(PointerEventData eventData); }
    public interface IPointerEnterHandler : IEventSystemHandler { void OnPointerEnter(PointerEventData eventData); }
    public interface IPointerExitHandler : IEventSystemHandler { void OnPointerExit(PointerEventData eventData); }
    public interface IBeginDragHandler : IEventSystemHandler { void OnBeginDrag(PointerEventData eventData); }
    public interface IDragHandler : IEventSystemHandler { void OnDrag(PointerEventData eventData); }
    public interface IEndDragHandler : IEventSystemHandler { void OnEndDrag(PointerEventData eventData); }
    public interface IScrollHandler : IEventSystemHandler { void OnScroll(PointerEventData eventData); }
}

namespace UnityEngine.UI
{
    using UnityEngine.EventSystems;
    using UnityEngine.Events;

    public abstract partial class Graphic : UIBehaviour
    {
        public Color color { get; set; } public bool raycastTarget { get; set; } public Material material { get; set; } public RectTransform rectTransform => null; public Canvas canvas => null;
        public CanvasRenderer canvasRenderer => null;
        public virtual void SetAllDirty() { } public virtual void SetNativeSize() { } public void CrossFadeAlpha(float alpha, float duration, bool ignoreTimeScale) { } public void CrossFadeColor(Color targetColor, float duration, bool ignoreTimeScale, bool useAlpha) { }
    }
    public abstract partial class MaskableGraphic : Graphic { public bool maskable { get; set; } }
    public partial class Image : MaskableGraphic
    {
        public enum Type { Simple, Sliced, Tiled, Filled }
        public enum FillMethod { Horizontal, Vertical, Radial90, Radial180, Radial360 }
        public enum OriginHorizontal { Left, Right }
        public enum OriginVertical { Bottom, Top }
        public enum Origin360 { Bottom, Right, Top, Left }
        public Sprite sprite { get; set; } public Sprite overrideSprite { get; set; } public Type type { get; set; } public bool preserveAspect { get; set; }
        public FillMethod fillMethod { get; set; } public float fillAmount { get; set; } public bool fillClockwise { get; set; } public int fillOrigin { get; set; } public bool fillCenter { get; set; }
        public float pixelsPerUnitMultiplier { get; set; }
    }
    public partial class RawImage : MaskableGraphic { public Texture texture { get; set; } public Rect uvRect { get; set; } }
    public partial class Text : MaskableGraphic
    {
        public string text { get; set; } public Font font { get; set; } public int fontSize { get; set; } public FontStyle fontStyle { get; set; } public TextAnchor alignment { get; set; }
        public bool supportRichText { get; set; } public bool resizeTextForBestFit { get; set; } public int resizeTextMinSize { get; set; } public int resizeTextMaxSize { get; set; }
        public HorizontalWrapMode horizontalOverflow { get; set; } public VerticalWrapMode verticalOverflow { get; set; } public float lineSpacing { get; set; }
        public float preferredWidth => 0; public float preferredHeight => 0;
    }
    public partial struct ColorBlock
    {
        public Color normalColor { get; set; } public Color highlightedColor { get; set; } public Color pressedColor { get; set; } public Color selectedColor { get; set; } public Color disabledColor { get; set; }
        public float colorMultiplier { get; set; } public float fadeDuration { get; set; } public static ColorBlock defaultColorBlock => default;
    }
    public partial struct Navigation { public enum Mode { None = 0, Horizontal = 1, Vertical = 2, Automatic = 3, Explicit = 4 } public Mode mode { get; set; } public static Navigation defaultNavigation => default; }
    public partial class Selectable : UIBehaviour
    {
        public enum Transition { None, ColorTint, SpriteSwap, Animation }
        public bool interactable { get; set; } public Graphic targetGraphic { get; set; } public ColorBlock colors { get; set; } public Transition transition { get; set; } public Navigation navigation { get; set; }
        public Image image { get; set; } public bool IsInteractable() => false; public virtual void Select() { }
    }
    public partial class Button : Selectable, IPointerClickHandler
    {
        public partial class ButtonClickedEvent : UnityEvent { }
        public ButtonClickedEvent onClick { get; set; }
        public virtual void OnPointerClick(PointerEventData eventData) { }
    }
    public partial class Toggle : Selectable
    {
        public partial class ToggleEvent : UnityEvent<bool> { }
        public bool isOn { get; set; } public ToggleEvent onValueChanged { get; set; } public Graphic graphic { get; set; } public ToggleGroup group { get; set; }
        public void SetIsOnWithoutNotify(bool value) { }
    }
    public partial class ToggleGroup : UIBehaviour { public bool allowSwitchOff { get; set; } }
    public partial class Slider : Selectable
    {
        public enum Direction { LeftToRight, RightToLeft, BottomToTop, TopToBottom }
        public partial class SliderEvent : UnityEvent<float> { }
        public float value { get; set; } public float minValue { get; set; } public float maxValue { get; set; } public bool wholeNumbers { get; set; } public float normalizedValue { get; set; }
        public SliderEvent onValueChanged { get; set; } public RectTransform fillRect { get; set; } public RectTransform handleRect { get; set; } public Direction direction { get; set; }
        public void SetValueWithoutNotify(float input) { }
    }
    public partial class ScrollRect : UIBehaviour
    {
        public enum MovementType { Unrestricted, Elastic, Clamped }
        public RectTransform content { get; set; } public RectTransform viewport { get; set; } public bool horizontal { get; set; } public bool vertical { get; set; }
        public MovementType movementType { get; set; } public float scrollSensitivity { get; set; } public Vector2 normalizedPosition { get; set; } public float verticalNormalizedPosition { get; set; } public float horizontalNormalizedPosition { get; set; }
        public bool inertia { get; set; } public float decelerationRate { get; set; }
    }
    public partial class Mask : UIBehaviour { public bool showMaskGraphic { get; set; } }
    public partial class RectMask2D : UIBehaviour { }
    public partial class CanvasScaler : UIBehaviour
    {
        public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize }
        public enum ScreenMatchMode { MatchWidthOrHeight, Expand, Shrink }
        public ScaleMode uiScaleMode { get; set; } public Vector2 referenceResolution { get; set; } public ScreenMatchMode screenMatchMode { get; set; } public float matchWidthOrHeight { get; set; }
        public float scaleFactor { get; set; } public float referencePixelsPerUnit { get; set; }
    }
    public partial class GraphicRaycaster : BaseRaycaster { public bool ignoreReversedGraphics { get; set; } }
    public abstract partial class LayoutGroup : UIBehaviour { public RectOffset padding { get; set; } public TextAnchor childAlignment { get; set; } }
    public abstract partial class HorizontalOrVerticalLayoutGroup : LayoutGroup
    {
        public float spacing { get; set; } public bool childForceExpandWidth { get; set; } public bool childForceExpandHeight { get; set; }
        public bool childControlWidth { get; set; } public bool childControlHeight { get; set; } public bool childScaleWidth { get; set; } public bool childScaleHeight { get; set; } public bool reverseArrangement { get; set; }
    }
    public partial class HorizontalLayoutGroup : HorizontalOrVerticalLayoutGroup { }
    public partial class VerticalLayoutGroup : HorizontalOrVerticalLayoutGroup { }
    public partial class GridLayoutGroup : LayoutGroup
    {
        public enum Constraint { Flexible, FixedColumnCount, FixedRowCount }
        public Vector2 cellSize { get; set; } public Vector2 spacing { get; set; } public Constraint constraint { get; set; } public int constraintCount { get; set; }
    }
    public partial class LayoutElement : UIBehaviour
    {
        public bool ignoreLayout { get; set; } public float minWidth { get; set; } public float minHeight { get; set; } public float preferredWidth { get; set; } public float preferredHeight { get; set; }
        public float flexibleWidth { get; set; } public float flexibleHeight { get; set; }
    }
    public partial class ContentSizeFitter : UIBehaviour
    {
        public enum FitMode { Unconstrained, MinSize, PreferredSize }
        public FitMode horizontalFit { get; set; } public FitMode verticalFit { get; set; }
    }
    public partial class AspectRatioFitter : UIBehaviour { public enum AspectMode { None, WidthControlsHeight, HeightControlsWidth, FitInParent, EnvelopeParent } public AspectMode aspectMode { get; set; } public float aspectRatio { get; set; } }
    public partial class Outline : Shadow { }
    public partial class Shadow : UIBehaviour { public Color effectColor { get; set; } public Vector2 effectDistance { get; set; } public bool useGraphicAlpha { get; set; } }
    public static partial class LayoutRebuilder { public static void ForceRebuildLayoutImmediate(RectTransform layoutRoot) { } public static void MarkLayoutForRebuild(RectTransform rect) { } }
}

namespace UnityEngine
{
    public partial class RectOffset { public RectOffset() { } public RectOffset(int left, int right, int top, int bottom) { } public int left { get; set; } public int right { get; set; } public int top { get; set; } public int bottom { get; set; } public int horizontal => 0; public int vertical => 0; }
}
