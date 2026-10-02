using UnityEngine;

namespace TowerDefense.Visuals
{
    /// <summary>Constant spin (portal swirl). Uses scaled time so it freezes with the game.</summary>
    public sealed class Spinner : MonoBehaviour
    {
        [SerializeField] private float degreesPerSecond = -90f;

        public void SetSpeed(float speed) => degreesPerSecond = speed;

        private void Update() => transform.Rotate(0f, 0f, degreesPerSecond * Time.deltaTime);
    }
}
