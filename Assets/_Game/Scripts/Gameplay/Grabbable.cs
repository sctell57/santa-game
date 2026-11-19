using EvilSanta.Player;
using UnityEngine;

namespace EvilSanta.Gameplay
{
    /// <summary>
    /// Anything an elf can pick up and throw: presents, crates, other junk.
    /// Respawns where it started if it falls out of the world.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Grabbable : MonoBehaviour
    {
        [SerializeField, Tooltip("Scale throw speed for this object (1 = normal).")]
        float throwMultiplier = 1f;
        [SerializeField] float heldAngularDamping = 4f;
        [SerializeField] float killY = -15f;

        Vector3 spawnPosition;
        Quaternion spawnRotation;
        float normalAngularDamping;

        public Rigidbody Body { get; private set; }
        public Collider[] Colliders { get; private set; }
        public ElfGrabber Holder { get; private set; }
        public bool IsHeld => Holder != null;
        public float ThrowMultiplier => throwMultiplier;
        /// <summary>Rough size, used to hold big things a bit further out.</summary>
        public float HoldRadius { get; private set; }

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Colliders = GetComponentsInChildren<Collider>();
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            normalAngularDamping = Body.angularDamping;
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;

            Bounds bounds = new Bounds(transform.position, Vector3.zero);
            foreach (Collider c in Colliders) bounds.Encapsulate(c.bounds);
            HoldRadius = Mathf.Max(bounds.extents.x, bounds.extents.z);
        }

        public void OnGrabbed(ElfGrabber holder)
        {
            Holder = holder;
            Body.angularDamping = heldAngularDamping;
            Body.WakeUp();
        }

        public void OnReleased()
        {
            Holder = null;
            Body.angularDamping = normalAngularDamping;
        }

        void FixedUpdate()
        {
            if (Body.position.y >= killY) return;
            if (Holder != null) Holder.Release();
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Body.position = spawnPosition + Vector3.up * 0.5f;
            Body.rotation = spawnRotation;
        }
    }
}
