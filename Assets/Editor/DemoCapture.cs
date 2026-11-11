using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DontForgetMe.Editor
{
    // Runs the scripted walkthrough in Play Mode. From a terminal:
    //   Unity -batchmode -projectPath . -executeMethod DontForgetMe.Editor.DemoCapture.Run
    // with DFM_SHOTS set to the folder that should receive the pictures and log.
    public static class DemoCapture
    {
        private const string Flag = "DontForgetMe.Capture";

        [MenuItem("Dont Forget Me/Validate/Run Scripted Walkthrough")]
        public static void Run()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DFM_SHOTS")))
                Environment.SetEnvironmentVariable("DFM_SHOTS", System.IO.Path.GetFullPath("Logs/Walkthrough"));
            EditorSceneManager.OpenScene("Assets/Scenes/Opening.unity");
            SessionState.SetBool(Flag, true);
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Watch()
        {
            if (!SessionState.GetBool(Flag, false)) return;
            double started = EditorApplication.timeSinceStartup;
            EditorApplication.update += () =>
            {
                if (!SessionState.GetBool(Flag, false)) return;
                bool timedOut = EditorApplication.timeSinceStartup - started > 420;
                if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode
                    && EditorApplication.timeSinceStartup - started > 30)
                    timedOut = true;
                if (!DemoAutopilot.Finished && !timedOut) return;
                SessionState.SetBool(Flag, false);
                Environment.SetEnvironmentVariable("DFM_SHOTS", null);
                int code = DemoAutopilot.Finished && DemoAutopilot.Problems == 0 ? 0 : 1;
                if (timedOut) Debug.LogError("Scripted walkthrough did not finish in time.");
                if (Application.isBatchMode) EditorApplication.Exit(code);
                else EditorApplication.ExitPlaymode();
            };
        }
    }
}
