using UnityEngine;
using UnityEngine.InputSystem;

namespace EvilSanta.Player
{
    /// <summary>
    /// What a player wants to do this tick. Gameplay code reads this struct and never
    /// touches devices, so in Week 2 the same scripts can run from networked input.
    /// </summary>
    public struct ElfCommand
    {
        public Vector2 Move;
        public bool Sprint;
        public bool JumpHeld;
        public bool GrabHeld;
    }

    /// <summary>
    /// Local gamepad + keyboard/mouse input. Actions are built in code so the project
    /// needs no generated input asset; they still support runtime rebinding later.
    /// Gamepad: left stick move, right stick look, A/Cross jump, RT grab (hold),
    /// RB or X/Square throw, LT sprint, D-pad down respawn.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class ElfInput : MonoBehaviour
    {
        [SerializeField, Tooltip("Degrees per second at full right-stick tilt.")]
        float stickLookSpeed = 200f;
        [SerializeField, Tooltip("Degrees per pixel of mouse movement.")]
        float mouseLookSpeed = 0.1f;
        [SerializeField] bool invertY;

        InputAction move, lookStick, lookMouse, jump, sprint, grab, throwAction, respawn, releaseCursor;
        bool jumpQueued, throwQueued, respawnQueued;

        public ElfCommand Command { get; private set; }
        /// <summary>Camera yaw/pitch change this frame, in degrees.</summary>
        public Vector2 LookDelta { get; private set; }
        public bool UsingGamepad { get; private set; }

        void Awake()
        {
            move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            move.AddBinding("<Gamepad>/leftStick").WithProcessor("stickDeadzone");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");

            lookStick = new InputAction("LookStick", InputActionType.Value, binding: "<Gamepad>/rightStick", processors: "stickDeadzone", expectedControlType: "Vector2");
            lookMouse = new InputAction("LookMouse", InputActionType.PassThrough, binding: "<Mouse>/delta", expectedControlType: "Vector2");

            jump = Button("Jump", "<Gamepad>/buttonSouth", "<Keyboard>/space");
            sprint = Button("Sprint", "<Gamepad>/leftTrigger", "<Keyboard>/leftShift");
            grab = Button("Grab", "<Gamepad>/rightTrigger", "<Mouse>/leftButton", "<Keyboard>/e");
            throwAction = Button("Throw", "<Gamepad>/rightShoulder", "<Gamepad>/buttonWest", "<Mouse>/rightButton", "<Keyboard>/f");
            respawn = Button("Respawn", "<Gamepad>/dpad/down", "<Keyboard>/r");
            releaseCursor = Button("ReleaseCursor", "<Keyboard>/escape");
        }

        static InputAction Button(string name, params string[] paths)
        {
            var a = new InputAction(name, InputActionType.Button);
            foreach (var p in paths) a.AddBinding(p);
            return a;
        }

        InputAction[] All => new[] { move, lookStick, lookMouse, jump, sprint, grab, throwAction, respawn, releaseCursor };

        void OnEnable() { foreach (var a in All) a.Enable(); }
        void OnDisable()
        {
            foreach (var a in All) a.Disable();
            SetCursorLocked(false);
        }
        void OnDestroy() { foreach (var a in All) a.Dispose(); }

        void Start() => SetCursorLocked(true);

        void Update()
        {
            TrackDevice();

            // Cursor: Esc frees it, clicking the game view grabs it again.
            if (releaseCursor.WasPressedThisFrame()) SetCursorLocked(false);
            else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
                SetCursorLocked(true);

            if (jump.WasPressedThisFrame()) jumpQueued = true;
            if (throwAction.WasPressedThisFrame()) throwQueued = true;
            if (respawn.WasPressedThisFrame()) respawnQueued = true;

            Vector2 look = lookStick.ReadValue<Vector2>() * (stickLookSpeed * Time.deltaTime);
            if (Cursor.lockState == CursorLockMode.Locked)
                look += lookMouse.ReadValue<Vector2>() * mouseLookSpeed;
            if (invertY) look.y = -look.y;
            LookDelta = look;

            Command = new ElfCommand
            {
                Move = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f),
                Sprint = sprint.IsPressed(),
                JumpHeld = jump.IsPressed(),
                GrabHeld = grab.IsPressed(),
            };
        }

        // One-shot presses are queued in Update and consumed in FixedUpdate so none get lost.
        public bool ConsumeJump() { bool v = jumpQueued; jumpQueued = false; return v; }
        public bool ConsumeThrow() { bool v = throwQueued; throwQueued = false; return v; }
        public bool ConsumeRespawn() { bool v = respawnQueued; respawnQueued = false; return v; }

        void TrackDevice()
        {
            var pad = Gamepad.current;
            if (pad != null && (pad.leftStick.ReadValue().sqrMagnitude > 0.04f ||
                                pad.rightStick.ReadValue().sqrMagnitude > 0.04f ||
                                pad.buttonSouth.isPressed || pad.rightTrigger.isPressed))
                UsingGamepad = true;
            else if ((Keyboard.current != null && Keyboard.current.anyKey.isPressed) ||
                     (Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > 4f))
                UsingGamepad = false;
        }

        static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
