using UnityEngine;

namespace TowerDefense.Visuals
{
    /// <summary>
    /// Build-slot presentation: the plot sprite hides while a tower stands on
    /// it (TowerPlacement.AnySlotChanged) and comes back when the tower is sold.
    /// </summary>
    public sealed class SlotVisual : MonoBehaviour
    {
        [SerializeField] private TowerPlacement slot;
        [SerializeField] private SpriteRenderer plot;

        internal void Setup(TowerPlacement owner, SpriteRenderer plotRenderer)
        {
            slot = owner;
            plot = plotRenderer;
        }

        private void OnEnable()
        {
            TowerPlacement.AnySlotChanged += OnSlotChanged;
            if (plot != null) plot.sortingOrder = SortingOrders.ForY(SortingOrders.Slots, transform.position.y);
            Refresh();
        }

        private void OnDisable() => TowerPlacement.AnySlotChanged -= OnSlotChanged;

        private void OnSlotChanged(TowerPlacement changed)
        {
            if (changed == slot) Refresh();
        }

        private void Refresh()
        {
            if (slot != null && plot != null) plot.enabled = !slot.IsOccupied;
        }
    }
}
