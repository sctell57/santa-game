using EvilSanta.Gameplay;
using UnityEngine;

namespace EvilSanta.Player
{
    /// <summary>
    /// Rigidbody capsule movement: camera-relative walking, sprint, buffered and
    /// coyote-time jumps, snappy falls, and slippery ice via SurfaceProperties.
    /// A Rigidbody (not a CharacterController) so presents, ramps and, later,
    /// the ragdoll and NetworkRigidbody all speak the same physics.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public class ElfMotor : MonoBehaviour
    {
        [SerializeField] ElfTuning tuning;
        [SerializeField] ElfInput input;
        [SerializeField, Tooltip("Optional. Found automatically if left empty.")]
        ElfCameraRig cameraRig;
        [SerializeField, Tooltip("Fall below this height and you respawn.")]
        float killY = -15f;

        Rigidbody body;
        CapsuleCollider capsule;
        ElfGrabber grabber;
        readonly RaycastHit[] hits = new RaycastHit[8];

        float lastGroundedTime = float.NegativeInfinity;
        float lastJumpPressTime = float.NegativeInfinity;
        float ignoreGroundUntil;
        Vector3 spawnPosition;
        Quaternion spawnRotation;

        public bool IsGrounded { get; private set; }
        public Vector3 GroundNormal { get; private set; } = Vector3.up;
        /// <summary>1 = normal grip, near 0 = ice.</summary>
        public float Traction { get; private set; } = 1f;
        /// <summary>Set by ElfGrabber while carrying something heavy.</summary>
        public float SpeedMultiplier { get; set; } = 1f;
        public Rigidbody Body => body;
        public ElfTuning Tuning => tuning;
        public ElfCameraRig CameraRig => cameraRig;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            capsule = GetComponent<CapsuleCollider>();
            grabber = GetComponent<ElfGrabber>();
            if (input == null) input = GetComponent<ElfInput>();

            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;

            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
        }

        void Start()
        {
            if (cameraRig == null) cameraRig = FindAnyObjectByType<ElfCameraRig>();
            if (cameraRig != null) cameraRig.Bind(transform, input);
            if (tuning == null)
            {
                Debug.LogWarning("[Evil Santa] ElfMotor has no ElfTuning asset; using defaults.", this);
                tuning = ScriptableObject.CreateInstance<ElfTuning>();
            }
        }

        void FixedUpdate()
        {
            float now = Time.time;
            float dt = Time.fixedDeltaTime;
            ElfCommand cmd = input.Command;

            if (input.ConsumeRespawn() || body.position.y < killY) { Respawn(); return; }
            if (input.ConsumeJump()) lastJumpPressTime = now;

            ProbeGround(now);
            if (IsGrounded) lastGroundedTime = now;

            // --- Horizontal movement, relative to the camera heading ---
            float yaw = cameraRig != null ? cameraRig.Yaw : transform.eulerAngles.y;
            Vector3 wish = Quaternion.Euler(0f, yaw, 0f) * new Vector3(cmd.Move.x, 0f, cmd.Move.y);
            bool hasInput = wish.sqrMagnitude > 0.0025f;

            float speed = (cmd.Sprint ? tuning.sprintSpeed : tuning.walkSpeed) * SpeedMultiplier;
            Vector3 velocity = body.linearVelocity;
            Vector3 planar = new Vector3(velocity.x, 0f, velocity.z);

            float accel = IsGrounded
                ? (hasInput ? tuning.groundAcceleration : tuning.groundDeceleration) * Traction
                : tuning.airAcceleration;
            Vector3 newPlanar = Vector3.MoveTowards(planar, wish * speed, accel * dt);
            body.AddForce(newPlanar - planar, ForceMode.VelocityChange);

            // Stand still on ramps instead of sliding down (but still slide on ice).
            if (IsGrounded && !hasInput)
            {
                Vector3 slopeGravity = Vector3.ProjectOnPlane(Physics.gravity, GroundNormal);
                body.AddForce(-slopeGravity * Traction, ForceMode.Acceleration);
            }

            // --- Jump (buffered + coyote time) ---
            bool jumpBuffered = now - lastJumpPressTime <= tuning.jumpBuffer;
            bool canJump = now - lastGroundedTime <= tuning.coyoteTime;
            if (jumpBuffered && canJump)
            {
                float jumpVelocity = Mathf.Sqrt(2f * -Physics.gravity.y * tuning.jumpHeight);
                body.AddForce(Vector3.up * (jumpVelocity - body.linearVelocity.y), ForceMode.VelocityChange);
                lastJumpPressTime = float.NegativeInfinity;
                lastGroundedTime = float.NegativeInfinity;
                ignoreGroundUntil = now + 0.1f;
                IsGrounded = false;
            }

            // --- Better gravity: heavier falls, short hops when jump is released ---
            float vy = body.linearVelocity.y;
            if (!IsGrounded)
            {
                if (vy < 0f)
                    body.AddForce(Physics.gravity * (tuning.fallGravityMultiplier - 1f), ForceMode.Acceleration);
                else if (vy > 0f && !cmd.JumpHeld)
                    body.AddForce(Physics.gravity * (tuning.lowJumpGravityMultiplier - 1f), ForceMode.Acceleration);
            }
            if (vy < -tuning.maxFallSpeed)
                body.AddForce(Vector3.up * (-tuning.maxFallSpeed - vy), ForceMode.VelocityChange);

            // --- Face the move direction ---
            if (hasInput) FaceDirection(wish, tuning.turnSpeed * dt);
        }

        /// <summary>Turn toward a flat direction. maxDegrees &lt; 0 snaps instantly.</summary>
        public void FaceDirection(Vector3 direction, float maxDegrees = -1f)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            Quaternion goal = Quaternion.LookRotation(direction.normalized, Vector3.up);
            body.MoveRotation(maxDegrees < 0f ? goal : Quaternion.RotateTowards(body.rotation, goal, maxDegrees));
        }

        public void Respawn()
        {
            if (grabber != null) grabber.Release();
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = spawnPosition;
            body.rotation = spawnRotation;
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        }

        void ProbeGround(float now)
        {
            IsGrounded = false;
            GroundNormal = Vector3.up;
            Traction = 1f;
            if (now < ignoreGroundUntil) return;

            float radius = capsule.radius * 0.9f;
            float bottomSphereY = capsule.center.y - capsule.height * 0.5f + capsule.radius;
            const float lift = 0.05f;
            Vector3 origin = body.position + Vector3.up * (bottomSphereY + lift);
            float distance = lift + (capsule.radius - radius) + tuning.groundProbeDistance;

            int count = Physics.SphereCastNonAlloc(origin, radius, Vector3.down, hits, distance,
                                                   Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Rigidbody held = grabber != null && grabber.Held != null ? grabber.Held.Body : null;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hits[i];
                Rigidbody hitBody = hit.collider.attachedRigidbody;
                if (hitBody == body || (held != null && hitBody == held)) continue;
                if (hit.distance <= 0f && hit.point == Vector3.zero) continue; // started overlapping
                if (Vector3.Angle(hit.normal, Vector3.up) > tuning.maxWalkableSlope) continue;
                if (hit.distance < best)
                {
                    best = hit.distance;
                    IsGrounded = true;
                    GroundNormal = hit.normal;
                    Traction = hit.collider.TryGetComponent(out SurfaceProperties surface) ? surface.Traction : 1f;
                }
            }
        }
    }
}
