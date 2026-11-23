using System.Collections;
using EvilSanta.Gameplay;
using UnityEngine;

namespace EvilSanta.Player
{
    /// <summary>
    /// Hold the grab button to pick up the nearest Grabbable in front of you and carry
    /// it on a soft spring (so it wobbles), release to drop, press throw to launch it
    /// where the camera is facing. In Week 2 TryGrab becomes a request to the host.
    /// </summary>
    [RequireComponent(typeof(ElfMotor))]
    public class ElfGrabber : MonoBehaviour
    {
        ElfMotor motor;
        ElfInput input;
        Collider[] ownColliders;
        readonly Collider[] overlap = new Collider[24];
        float nextGrabTime;
        bool waitForGrabRelease;

        public Grabbable Held { get; private set; }

        ElfTuning Tuning => motor.Tuning;

        void Awake()
        {
            motor = GetComponent<ElfMotor>();
            input = GetComponent<ElfInput>();
            ownColliders = GetComponentsInChildren<Collider>();
        }

        void FixedUpdate()
        {
            ElfCommand cmd = input.Command;
            bool throwPressed = input.ConsumeThrow();
            if (!cmd.GrabHeld) waitForGrabRelease = false;

            if (Held == null)
            {
                // Holding grab keeps trying, so you can walk into a present and scoop it up.
                if (cmd.GrabHeld && !waitForGrabRelease && Time.time >= nextGrabTime) TryGrab();
                return;
            }

            if (!cmd.GrabHeld) { Release(); return; }
            if (throwPressed) { Throw(); return; }
            HoldStep();
        }

        void TryGrab()
        {
            ElfTuning t = Tuning;
            Vector3 forward = transform.forward;
            Vector3 center = transform.position + Vector3.up * (t.holdHeight * 0.6f) + forward * (t.grabReach * 0.5f);
            int count = Physics.OverlapSphereNonAlloc(center, t.grabRadius + t.grabReach * 0.5f, overlap,
                                                      Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Grabbable best = null;
            float bestScore = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Rigidbody rb = overlap[i].attachedRigidbody;
                if (rb == null || rb == motor.Body) continue;
                if (!rb.TryGetComponent(out Grabbable g) || g.IsHeld || rb.mass > t.maxGrabMass) continue;

                Vector3 to = rb.worldCenterOfMass - transform.position;
                to.y = 0f;
                float facing = to.sqrMagnitude > 0.0001f ? Vector3.Dot(forward, to.normalized) : 1f;
                if (facing < -0.2f) continue; // behind us
                float score = to.magnitude - facing; // prefer close and in front
                if (score < bestScore) { bestScore = score; best = g; }
            }
            if (best != null) Grab(best);
        }

        void Grab(Grabbable g)
        {
            Held = g;
            g.OnGrabbed(this);
            SetIgnoreCollisions(g, true);
            motor.SpeedMultiplier = Mathf.Max(Tuning.minCarrySpeedMultiplier, 1f - Tuning.carrySlowdownPerKg * g.Body.mass);
        }

        void HoldStep()
        {
            ElfTuning t = Tuning;
            Rigidbody rb = Held.Body;
            float reach = t.holdDistance + Held.HoldRadius;
            Vector3 holdPoint = transform.position + Vector3.up * t.holdHeight + transform.forward * reach;
            Vector3 toHold = holdPoint - rb.worldCenterOfMass;

            if (toHold.magnitude > t.holdBreakDistance) { Release(); return; }

            // Mass-independent spring so tuning works for every present, but heavy ones lag behind more.
            float heaviness = Mathf.Lerp(1f, 0.45f, rb.mass / Mathf.Max(0.01f, t.maxGrabMass));
            Vector3 relativeVelocity = rb.linearVelocity - motor.Body.linearVelocity;
            Vector3 accel = (toHold * t.holdSpring - relativeVelocity * t.holdDamping) * heaviness;
            accel -= Physics.gravity * t.holdGravityCompensation;
            rb.AddForce(accel, ForceMode.Acceleration);
        }

        void Throw()
        {
            ElfTuning t = Tuning;
            Grabbable g = Held;
            Rigidbody rb = g.Body;

            Vector3 aim = motor.CameraRig != null ? motor.CameraRig.FlatForward : transform.forward;
            motor.FaceDirection(aim);
            Vector3 direction = (aim + Vector3.up * t.throwUpward).normalized;
            float speed = t.throwSpeed * g.ThrowMultiplier / (1f + t.throwMassFalloff * rb.mass);

            ReleaseInternal(t.ignoreThrowerTime);
            rb.linearVelocity = motor.Body.linearVelocity * 0.5f + direction * speed;
            rb.angularVelocity = Random.insideUnitSphere * t.throwSpin;
            waitForGrabRelease = true; // don't instantly re-grab while the trigger is still held
        }

        /// <summary>Drop whatever is held. Safe to call when holding nothing.</summary>
        public void Release()
        {
            if (Held == null) return;
            ReleaseInternal(0.1f);
        }

        void ReleaseInternal(float ignoreFor)
        {
            Grabbable g = Held;
            Held = null;
            motor.SpeedMultiplier = 1f;
            if (g == null) return; // destroyed while held (scene unload)
            nextGrabTime = Time.time + Tuning.regrabCooldown;
            g.OnReleased();
            if (isActiveAndEnabled) StartCoroutine(RestoreCollisionsAfter(g, ignoreFor));
            else SetIgnoreCollisions(g, false);
        }

        IEnumerator RestoreCollisionsAfter(Grabbable g, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (g != null && g != Held) SetIgnoreCollisions(g, false);
        }

        void SetIgnoreCollisions(Grabbable g, bool ignore)
        {
            foreach (Collider mine in ownColliders)
            foreach (Collider theirs in g.Colliders)
                if (mine != null && theirs != null) Physics.IgnoreCollision(mine, theirs, ignore);
        }

        void OnDisable() => Release();
    }
}
