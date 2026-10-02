using UnityEngine;

/// <summary>
/// Attach to each buildable grid slot in the scene (empty GameObject with a
/// Collider2D, e.g. BoxCollider2D). Handles selecting a tower to build (from
/// a UI button) and placing it here. OnMouseDown works for both mouse clicks
/// and mobile touch as long as the object has a Collider2D and there's a
/// Physics2DRaycaster on the camera (or an EventSystem set up for UI-over-world
/// taps) - for pure mobile you may prefer routing this through Input.touches
/// + Physics2D.Raycast instead; swap the trigger method, the logic below stays.
/// </summary>
public class TowerPlacement : MonoBehaviour
{
    private GameObject builtTower;

    public void TryBuild(TowerData towerData, GameObject towerPrefab)
    {
        if (builtTower != null) return; // slot occupied
        if (!GameManager.Instance.SpendGold(towerData.baseCost)) return;

        builtTower = Instantiate(towerPrefab, transform.position, Quaternion.identity, transform);
        Tower t = builtTower.GetComponent<Tower>();
        if (t != null) t.data = towerData;
    }

    public Tower GetBuiltTower()
    {
        return builtTower != null ? builtTower.GetComponent<Tower>() : null;
    }

    private void OnMouseDown()
    {
        if (TowerShopUI.SelectedData != null && TowerShopUI.SelectedPrefab != null)
        {
            TryBuild(TowerShopUI.SelectedData, TowerShopUI.SelectedPrefab);
            TowerShopUI.ClearSelection();
        }
        else
        {
            // Slot already built - open your upgrade panel here instead of
            // auto-upgrading. Left simple for now:
            Tower t = GetBuiltTower();
            if (t != null && t.upgradeLevel == 1)
                t.Upgrade();
        }
    }
}

/// <summary>
/// Minimal static holder for "which tower is the player about to place".
/// Replace with a proper UI shop panel; this keeps the wiring simple to start.
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
