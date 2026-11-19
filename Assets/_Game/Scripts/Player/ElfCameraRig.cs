using UnityEngine;

namespace EvilSanta.Player
{
    /// <summary>
    /// A pivot that follows the elf and turns with look input. A Cinemachine camera
    /// with Third Person Follow tracks this pivot (and handles wall collision).
    /// It is a separate object from the elf so ragdolls in Week 3 can't spin the camera.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class ElfCameraRig : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] ElfInput input;
        [SerializeField] float pivotHeight = 1.0f;
        [SerializeField, Tooltip("Higher = follows the elf more tightly.")]
        float followSharpness = 18f;
        [SerializeField] float minPitch = -35f;
        [SerializeField] float maxPitch = 70f;
        [SerializeField] float startPitch = 15f;

        float yaw, pitch;

        /// <summary>Camera heading in degrees. Movement is relative to this.</summary>
        public float Yaw => yaw;
        public Vector3 FlatForward => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

        void Awake()
        {
            pitch = startPitch;
            if (target != null) Bind(target, input);
        }

        public void Bind(Transform newTarget, ElfInput newInput)
        {
            target = newTarget;
            input = newInput;
            yaw = target.eulerAngles.y;
            transform.SetPositionAndRotation(target.position + Vector3.up * pivotHeight,
                                             Quaternion.Euler(pitch, yaw, 0f));
        }

        void LateUpdate()
        {
            if (target == null) return;

            if (input != null)
            {
                Vector2 look = input.LookDelta;
                yaw += look.x;
                pitch = Mathf.Clamp(pitch - look.y, minPitch, maxPitch);
            }

            Vector3 goal = target.position + Vector3.up * pivotHeight;
            float t = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position, goal, t),
                                             Quaternion.Euler(pitch, yaw, 0f));
        }
    }
}
