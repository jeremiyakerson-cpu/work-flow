using System;
using UnityEngine;

/// <summary>
/// Attach to each buildable grid slot in the scene (empty GameObject with a
/// Collider2D, e.g. BoxCollider2D). Owns the tower built here.
/// Input is routed in from outside: the touch/mouse input controller calls
/// HandleTap() for a tap that lands on this slot's collider (so camera pans and
/// drags never count as taps). HandleTap raises SlotTapped; the build/upgrade
/// menu listens and calls TryBuild / Sell / the Tower upgrade methods.
/// </summary>
public class TowerPlacement : MonoBehaviour
{
    /// <summary>A slot was tapped (empty or built). UI opens the build or upgrade menu.</summary>
    public static event Action<TowerPlacement> SlotTapped;
    /// <summary>A tower was built or sold on any slot.</summary>
    public static event Action<TowerPlacement> AnySlotChanged;

    private GameObject builtTower;

    public bool IsOccupied => builtTower != null;

    /// <summary>Build using the TowerData's own towerPrefab.</summary>
    public bool TryBuild(TowerData towerData)
    {
        return towerData != null && TryBuild(towerData, towerData.towerPrefab);
    }

    public bool TryBuild(TowerData towerData, GameObject towerPrefab)
    {
        if (builtTower != null) return false; // slot occupied
        if (towerData == null || towerPrefab == null) return false;
        // No GameManager (sandbox scenes) = free building, like Tower upgrades.
        if (GameManager.Instance != null && !GameManager.Instance.SpendGold(towerData.baseCost)) return false;

        builtTower = Instantiate(towerPrefab, transform.position, Quaternion.identity, transform);
        // Runtime templates are active objects, but a prefab saved inactive must still come out live.
        if (!builtTower.activeSelf) builtTower.SetActive(true);
        Tower t = builtTower.GetComponent<Tower>();
        if (t != null) t.Init(towerData);
        AnySlotChanged?.Invoke(this);
        return true;
    }

    public Tower GetBuiltTower()
    {
        return builtTower != null ? builtTower.GetComponent<Tower>() : null;
    }

    /// <summary>Sell the tower here for its refund value. Returns gold gained (0 if empty).</summary>
    public int Sell()
    {
        if (builtTower == null) return 0;
        Tower t = GetBuiltTower();
        int refund = t != null ? t.SellValue() : 0;
        // Deactivate now: Destroy is deferred to end of frame and the tower must not fire again.
        builtTower.SetActive(false);
        Destroy(builtTower);
        builtTower = null;
        if (refund > 0 && GameManager.Instance != null) GameManager.Instance.AddGold(refund);
        AnySlotChanged?.Invoke(this);
        return refund;
    }

    /// <summary>Called by the input controller when a tap lands on this slot.</summary>
    public void HandleTap()
    {
        // Legacy shop flow: a tower picked in TowerShopUI is built on the next tapped slot.
        if (!IsOccupied && TowerShopUI.SelectedData != null)
        {
            TryBuild(TowerShopUI.SelectedData, TowerShopUI.SelectedPrefab != null ? TowerShopUI.SelectedPrefab : TowerShopUI.SelectedData.towerPrefab);
            TowerShopUI.ClearSelection();
            return;
        }

        SlotTapped?.Invoke(this);
    }
}

/// <summary>
/// Minimal static holder for "which tower is the player about to place".
/// The radial build menu builds directly via TowerPlacement.TryBuild; this
/// stays for simple shop-button wiring.
/// </summary>
public static class TowerShopUI
{
    public static TowerData SelectedData;
    public static GameObject SelectedPrefab;

    public static void SelectTower(TowerData data, GameObject prefab)
    {
        SelectedData = data;
        SelectedPrefab = prefab;
    }

    public static void ClearSelection()
    {
        SelectedData = null;
        SelectedPrefab = null;
    }
}
