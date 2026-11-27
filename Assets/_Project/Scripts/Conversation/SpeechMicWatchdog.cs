using UnityEngine;
using UnityEngine.XR;

namespace SeoulFirstWeek.Conversation
{
    /// <summary>
    /// Keeps the microphone alive. Taking the headset off suspends its microphone (and the game loses focus); on
    /// putting it back on, the old device handle can stay silent, so the ring buffer is opened again when focus or the
    /// wearer's presence comes back, and whenever the device stops delivering samples (<see cref="Speech.Watch"/>).
    /// </summary>
    public class SpeechMicWatchdog : MonoBehaviour
    {
        bool worn;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            var go = new GameObject("[SpeechMic]");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<SpeechMicWatchdog>();
        }

        void Update()
        {
            Speech.Watch();
            if (!Core.VRMode.Active) return;
            var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (head.isValid && head.TryGetFeatureValue(CommonUsages.userPresence, out bool present))
            {
                if (present && !worn) Speech.Reopen("the headset was put back on");
                worn = present;
            }
        }

        void OnApplicationFocus(bool focus)
        {
            if (focus) Speech.Reopen("the game has focus again");
        }

        void OnApplicationPause(bool paused)
        {
            if (!paused) Speech.Reopen("the game resumed");
        }
    }
}
