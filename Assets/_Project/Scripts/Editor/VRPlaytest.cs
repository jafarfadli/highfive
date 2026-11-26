using UnityEditor;

namespace SeoulFirstWeek.EditorTools
{
    /// <summary>
    /// The play-test in a pretend headset: the Mock HMD loader stands in for the Quest (stereo rendering, a head
    /// that doesn't move, no controllers), and <see cref="AutoPlaytest"/> plays the parts as usual. Start the editor
    /// with <c>-vr</c> (and <c>-vr-multipass</c> for two-pass stereo):
    /// <c>Unity -projectPath . -executeMethod SeoulFirstWeek.EditorTools.VRPlaytest.Run -vr</c>. Each photo is
    /// also saved as the game view, which shows both eyes side by side (<c>…-eyes.png</c>). The next start of the
    /// editor without <c>-vr</c> puts OpenXR back as the loader.
    /// </summary>
    [InitializeOnLoad]
    public static class VRPlaytest
    {
        static VRPlaytest()
        {
            if (!Core.VRMode.Wanted && VRSetup.MockHmdAssigned())
                EditorApplication.delayCall += () => VRSetup.UseMockHmd(false);
        }

        public static void Run()
        {
            bool multipass = System.Array.Exists(System.Environment.GetCommandLineArgs(), a => a == "-vr-multipass");
            VRSetup.UseMockHmd(true, singlePass: !multipass);
            ShaderUtil.allowAsyncCompilation = false;
            AutoPlaytest.RunPlayOnly();
        }
    }
}
