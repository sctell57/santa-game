using UnityEngine;

namespace EvilSanta.Gameplay
{
    /// <summary>
    /// Put on a collider to change how elves move on it. Ice uses low traction
    /// (pair it with a low-friction Physics Material so presents slide too).
    /// </summary>
    public class SurfaceProperties : MonoBehaviour
    {
        [SerializeField, Range(0.02f, 1f), Tooltip("1 = normal ground, 0.1 = very slippery ice.")]
        float traction = 1f;

        public float Traction
        {
            get => traction;
            set => traction = Mathf.Clamp(value, 0.02f, 1f);
        }
    }
}
