using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense.Pooling
{
    /// <summary>
    /// Optional hooks for components on pooled objects (visuals resetting a hit
    /// flash, trails clearing, etc). Called on every component implementing it
    /// on the pooled GameObject (root and children, found once at creation).
    /// </summary>
    public interface IPoolable
    {
        /// <summary>After every Spawn (new or reused), after Awake/OnEnable, at the final position.</summary>
        void OnSpawnedFromPool();
        /// <summary>Just before the object is deactivated and parked in the pool.</summary>
        void OnReturnedToPool();
    }

    /// <summary>
    /// Bookkeeping component added to every instance the pool creates.
    /// Don't add it by hand.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PooledObject : MonoBehaviour
    {
        /// <summary>The prefab or runtime template this instance was cloned from (its pool key).</summary>
        public GameObject Template { get; internal set; }
        /// <summary>True while parked (inactive) in the pool.</summary>
        public bool IsInPool { get; internal set; }

        internal Vector3 templateScale;
        internal IPoolable[] poolables;
    }

    /// <summary>
    /// GameObject pool keyed by prefab / runtime template. Spawn instead of
    /// Instantiate, Despawn instead of Destroy:
    /// <code>
    /// GameObject go = GameObjectPool.Spawn(enemyData.prefab, pos, Quaternion.identity);
    /// ...
    /// GameObjectPool.Despawn(go);   // safe on non-pooled objects too: they're Destroyed
    /// </code>
    /// Lifecycle: a new instance gets Awake/OnEnable from Instantiate; a reused
    /// one only gets OnEnable (Awake never runs twice), so per-spawn state must be
    /// reset in an Init method, OnEnable, or IPoolable.OnSpawnedFromPool.
    /// Parked instances are inactive children of a "[Pool]" object in the active
    /// scene, so a scene reload destroys them and the pool starts fresh.
    /// Works with runtime templates (active objects under an inactive root): clones
    /// come out active at the scene root exactly like Instantiate.
    /// </summary>
    public static class GameObjectPool
    {
        private static readonly Dictionary<GameObject, Stack<PooledObject>> pools = new Dictionary<GameObject, Stack<PooledObject>>();
        private static readonly List<IPoolable> poolableScratch = new List<IPoolable>();
        private static Transform root;

        // Enter Play Mode without domain reload keeps statics alive: start clean each play session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            pools.Clear();
            poolableScratch.Clear();
            root = null;
        }

        /// <summary>Get an active instance of template at position/rotation (under parent if given).</summary>
        public static GameObject Spawn(GameObject template, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (template == null) return null;
            EnsureRoot();

            PooledObject po = PopAlive(template);
            if (po != null)
            {
                Transform t = po.transform;
                t.SetParent(parent, false);
                t.SetPositionAndRotation(position, rotation);
                po.IsInPool = false;
                po.gameObject.SetActive(true);
            }
            else
            {
                GameObject go = Object.Instantiate(template, position, rotation, parent);
                po = Track(go, template);
                // Instantiate copies activeSelf; a template parked inactive must still spawn active.
                if (!go.activeSelf) go.SetActive(true);
            }

            NotifySpawned(po);
            return po.gameObject;
        }

        /// <summary>Spawn and return a component of type T from the instance (null if missing).</summary>
        public static T Spawn<T>(GameObject template, Vector3 position, Quaternion rotation, Transform parent = null) where T : Component
        {
            GameObject go = Spawn(template, position, rotation, parent);
            return go != null ? go.GetComponent<T>() : null;
        }

        /// <summary>
        /// Return an instance to its pool (deactivated, reparented, scale restored).
        /// Objects that didn't come from the pool, or whose template was destroyed,
        /// are Destroyed instead. Despawning twice is a no-op.
        /// </summary>
        public static void Despawn(GameObject instance)
        {
            if (instance == null) return;
            PooledObject po = instance.GetComponent<PooledObject>();
            if (po == null || po.Template == null || !EnsureRoot())
            {
                Object.Destroy(instance);
                return;
            }
            if (po.IsInPool) return;

            po.IsInPool = true;
            if (po.poolables != null)
                foreach (var p in po.poolables)
                    if (p is Object o ? o != null : p != null) p.OnReturnedToPool();

            instance.SetActive(false);
            Transform t = instance.transform;
            t.SetParent(root, false);
            t.localScale = po.templateScale;
            GetStack(po.Template).Push(po);
        }

        /// <summary>True if this instance is managed by the pool (spawned by it).</summary>
        public static bool IsPooled(GameObject instance) =>
            instance != null && instance.TryGetComponent(out PooledObject po) && po.Template != null;

        /// <summary>Create inactive instances up front (level load) so the first wave doesn't hitch.</summary>
        public static void Prewarm(GameObject template, int count)
        {
            if (template == null || count <= 0 || !EnsureRoot()) return;
            Stack<PooledObject> stack = GetStack(template);
            for (int i = stack.Count; i < count; i++)
            {
                // Instantiated under the inactive root: no Awake/OnEnable until first spawned.
                GameObject go = Object.Instantiate(template, root, false);
                PooledObject po = Track(go, template);
                go.SetActive(false);
                po.IsInPool = true;
                stack.Push(po);
            }
        }

        /// <summary>Parked instances available for template.</summary>
        public static int CountInactive(GameObject template) =>
            template != null && pools.TryGetValue(template, out var s) ? s.Count : 0;

        /// <summary>Destroy every parked instance (active instances are untouched).</summary>
        public static void Clear()
        {
            foreach (var stack in pools.Values)
                while (stack.Count > 0)
                {
                    PooledObject po = stack.Pop();
                    if (po != null) Object.Destroy(po.gameObject);
                }
            pools.Clear();
        }

        private static PooledObject PopAlive(GameObject template)
        {
            if (!pools.TryGetValue(template, out var stack)) return null;
            while (stack.Count > 0)
            {
                PooledObject po = stack.Pop();
                if (po != null) return po; // skips instances destroyed behind the pool's back
            }
            return null;
        }

        private static PooledObject Track(GameObject go, GameObject template)
        {
            if (!go.TryGetComponent(out PooledObject po)) po = go.AddComponent<PooledObject>();
            po.Template = template;
            po.IsInPool = false;
            po.templateScale = template.transform.localScale;
            poolableScratch.Clear();
            go.GetComponentsInChildren(true, poolableScratch);
            po.poolables = poolableScratch.Count > 0 ? poolableScratch.ToArray() : null;
            poolableScratch.Clear();
            return po;
        }

        private static void NotifySpawned(PooledObject po)
        {
            if (po.poolables == null) return;
            foreach (var p in po.poolables)
                if (p is Object o ? o != null : p != null) p.OnSpawnedFromPool();
        }

        private static Stack<PooledObject> GetStack(GameObject template)
        {
            if (!pools.TryGetValue(template, out var stack))
            {
                stack = new Stack<PooledObject>();
                pools.Add(template, stack);
            }
            return stack;
        }

        // The root lives in the active scene; after a scene change it is gone and so are
        // all parked instances, so drop the stale bookkeeping and start over.
        private static bool EnsureRoot()
        {
            if (root != null) return true;
            pools.Clear();
            var go = new GameObject("[Pool]");
            go.SetActive(false);
            root = go.transform;
            return root != null;
        }
    }
}
