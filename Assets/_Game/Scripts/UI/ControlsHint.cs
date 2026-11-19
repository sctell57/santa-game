using EvilSanta.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EvilSanta.UI
{
    /// <summary>
    /// Playtest helper: shows the controls for whichever device you last touched.
    /// Toggle with Tab or the gamepad View/Select button. Scales with screen height
    /// so it stays readable on a TV. Throwaway, not the real UI.
    /// </summary>
    public class ControlsHint : MonoBehaviour
    {
        [SerializeField] bool visible = true;
        ElfInput input;
        GUIStyle style;

        const string Pad =
            "Left stick  Move        LT  Sprint\n" +
            "Right stick Look        A / Cross  Jump\n" +
            "Hold RT     Grab        RB or X / Square  Throw\n" +
            "D-pad down  Respawn     View / Select  Hide this";
        const string Keys =
            "WASD  Move        Mouse  Look       Shift  Sprint\n" +
            "Space  Jump       Hold LMB or E  Grab\n" +
            "RMB or F  Throw   R  Respawn        Esc  Free cursor\n" +
            "Tab  Hide this";

        void Update()
        {
            if ((Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame) ||
                (Gamepad.current != null && Gamepad.current.selectButton.wasPressedThisFrame))
                visible = !visible;
            if (input == null) input = FindAnyObjectByType<ElfInput>();
        }

        void OnGUI()
        {
            if (!visible) return;
            style ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 22, padding = new RectOffset(16, 16, 12, 12) };
            float scale = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            string text = input != null && input.UsingGamepad ? Pad : Keys;
            GUI.Box(new Rect(24, 1080 - 24 - 150, 640, 150), text, style);
        }
    }
}
