using UnityEngine;

namespace EvilSanta.Player
{
    /// <summary>
    /// All the "feel" numbers for the elf in one asset. Edits made in Play Mode
    /// are kept (it's an asset, not a scene object), so tune live and keep what feels good.
    /// </summary>
    [CreateAssetMenu(menuName = "Evil Santa/Elf Tuning", fileName = "ElfTuning")]
    public class ElfTuning : ScriptableObject
    {
        [Header("Movement")]
        public float walkSpeed = 4.5f;
        public float sprintSpeed = 7f;
        [Tooltip("How fast the elf reaches target speed on normal ground (m/s²).")]
        public float groundAcceleration = 45f;
        [Tooltip("How fast the elf stops when there's no input on normal ground (m/s²).")]
        public float groundDeceleration = 55f;
        public float airAcceleration = 12f;
        [Tooltip("Degrees per second the elf turns to face the move direction.")]
        public float turnSpeed = 720f;

        [Header("Carrying")]
        [Tooltip("Speed lost per kg carried (0.04 = 4% per kg).")]
        public float carrySlowdownPerKg = 0.04f;
        [Range(0.1f, 1f)] public float minCarrySpeedMultiplier = 0.5f;

        [Header("Jump")]
        public float jumpHeight = 1.3f;
        [Tooltip("Seconds after walking off a ledge where jump still works.")]
        public float coyoteTime = 0.12f;
        [Tooltip("Seconds a jump press is remembered before landing.")]
        public float jumpBuffer = 0.15f;
        [Tooltip("Extra gravity while falling. Higher = snappier, less floaty.")]
        public float fallGravityMultiplier = 2f;
        [Tooltip("Extra gravity while rising with jump released. Gives short hops.")]
        public float lowJumpGravityMultiplier = 2.5f;
        public float maxFallSpeed = 25f;

        [Header("Ground check")]
        public float groundProbeDistance = 0.15f;
        [Range(0f, 89f)] public float maxWalkableSlope = 50f;

        [Header("Grab")]
        public float grabReach = 1.1f;
        public float grabRadius = 0.6f;
        [Tooltip("Heaviest thing an elf can pick up (kg).")]
        public float maxGrabMass = 20f;
        public float holdDistance = 0.75f;
        public float holdHeight = 0.85f;
        [Tooltip("Spring pulling the held object to the hands. Lower = wobblier.")]
        public float holdSpring = 220f;
        [Tooltip("Damping on the hold spring. About 2*sqrt(spring) is no wobble; lower wobbles more.")]
        public float holdDamping = 22f;
        [Range(0f, 1f)] public float holdGravityCompensation = 0.85f;
        [Tooltip("If the object gets this far from the hands (snagged on a wall), let go.")]
        public float holdBreakDistance = 2.2f;
        [Tooltip("After a release, wait this long before grabbing again.")]
        public float regrabCooldown = 0.25f;

        [Header("Throw")]
        public float throwSpeed = 11f;
        [Tooltip("Upward tilt of a throw. 0 = flat, 1 = 45 degrees.")]
        public float throwUpward = 0.35f;
        [Tooltip("Heavier things fly slower: speed / (1 + this * mass).")]
        public float throwMassFalloff = 0.06f;
        public float throwSpin = 6f;
        [Tooltip("Seconds the thrown object ignores the thrower so it doesn't bonk them.")]
        public float ignoreThrowerTime = 0.25f;
    }
}
