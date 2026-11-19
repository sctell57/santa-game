using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace EvilSanta.Setup
{
    /// <summary>
    /// Step 1 of the Week 1 setup. Lives in its own assembly with no package
    /// dependencies so this menu works before Cinemachine or Netcode are installed.
    /// </summary>
    public static class PackageInstaller
    {
        static readonly string[] Packages =
        {
            "com.unity.inputsystem",
            "com.unity.cinemachine",              // resolves to Cinemachine 3.x on Unity 6
            "com.unity.netcode.gameobjects",      // used from Week 2
            "com.unity.multiplayer.playmode",     // used from Week 2
        };

        static AddAndRemoveRequest request;

        [MenuItem("Evil Santa/1. Install Week 1 Packages", priority = 0)]
        public static void Install()
        {
            if (request != null && !request.IsCompleted)
            {
                Debug.Log("[Evil Santa] Package install already running...");
                return;
            }

            Debug.Log("[Evil Santa] Installing: " + string.Join(", ", Packages));
            request = Client.AddAndRemove(Packages, null);
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (!request.IsCompleted) return;
            EditorApplication.update -= Poll;

            if (request.Status == StatusCode.Success)
            {
                Debug.Log("[Evil Santa] Packages installed. If Unity asks to enable the new Input System backends and restart, say Yes. " +
                          "Then run Evil Santa > 2. Build Graybox Level.");
            }
            else
            {
                Debug.LogError("[Evil Santa] Package install failed: " + request.Error?.message);
            }
        }
    }
}
