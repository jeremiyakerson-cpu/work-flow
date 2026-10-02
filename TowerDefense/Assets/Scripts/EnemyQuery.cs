using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Allocation-free "which enemies are near this point" queries shared by
/// towers, heroes, barricades and projectiles. Uses one cached layer mask,
/// Physics2D.OverlapCircle with a ContactFilter2D and reused buffers.
/// If the "Enemy" layer is missing from the project it falls back to all
/// layers (filtered by the Enemy component) and warns once, so the game still
/// works before ProjectSettings are in place.
/// </summary>
public static class EnemyQuery
{
    public const string EnemyLayerName = "Enemy";
    /// <summary>Max colliders read per query. Extra colliders in one circle are ignored.</summary>
    public const int MaxColliders = 128;
    /// <summary>Size of buffers handed out by RentBuffer.</summary>
    public const int BufferSize = 64;

    private static readonly Collider2D[] colliders = new Collider2D[MaxColliders];
    private static readonly Stack<Enemy[]> bufferPool = new Stack<Enemy[]>();
    private static ContactFilter2D filter;
    private static bool filterReady;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        filterReady = false;
        bufferPool.Clear();
    }

    /// <summary>Layer mask for the "Enemy" layer, or Physics2D.AllLayers if that layer doesn't exist.</summary>
    public static int EnemyLayerMask
    {
        get
        {
            EnsureFilter();
            return filter.useLayerMask ? (int)filter.layerMask : Physics2D.AllLayers;
        }
    }

    /// <summary>
    /// Fill results with distinct living enemies whose colliders overlap the circle.
    /// Returns the count written (at most results.Length).
    /// </summary>
    public static int OverlapEnemies(Vector2 center, float radius, Enemy[] results)
    {
        if (results == null || results.Length == 0 || !(radius > 0f)) return 0;
        EnsureFilter();

        int hits = Physics2D.OverlapCircle(center, radius, filter, colliders);
        int count = 0;
        for (int i = 0; i < hits && count < results.Length; i++)
        {
            Collider2D col = colliders[i];
            colliders[i] = null; // don't keep destroyed objects alive in the static buffer
            if (col == null) continue;
            if (!col.TryGetComponent(out Enemy e)) e = col.GetComponentInParent<Enemy>();
            if (e == null || e.IsDead) continue;
            if (Contains(results, count, e)) continue; // enemies with several colliders count once
            results[count++] = e;
        }
        return count;
    }

    /// <summary>
    /// Borrow an Enemy buffer for a query whose results you iterate while
    /// dealing damage (deaths can trigger callbacks that query again, so a
    /// single shared buffer isn't safe). Always pair with ReturnBuffer.
    /// </summary>
    public static Enemy[] RentBuffer() => bufferPool.Count > 0 ? bufferPool.Pop() : new Enemy[BufferSize];

    public static void ReturnBuffer(Enemy[] buffer)
    {
        if (buffer == null) return;
        System.Array.Clear(buffer, 0, buffer.Length);
        bufferPool.Push(buffer);
    }

    private static bool Contains(Enemy[] arr, int count, Enemy e)
    {
        for (int i = 0; i < count; i++) if (arr[i] == e) return true;
        return false;
    }

    private static void EnsureFilter()
    {
        if (filterReady) return;
        filterReady = true;
        int mask = LayerMask.GetMask(EnemyLayerName);
        filter = new ContactFilter2D { useTriggers = true };
        if (mask != 0)
        {
            filter.useLayerMask = true;
            filter.layerMask = mask;
        }
        else
        {
            filter.useLayerMask = false;
            Debug.LogWarning($"EnemyQuery: no \"{EnemyLayerName}\" layer defined; querying all layers instead.");
        }
    }
}
