using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DontForgetMe.Editor
{
    [InitializeOnLoad]
    public static class DemoBuild
    {
        private const string ScenePath = "Assets/Scenes/Opening.unity";
        private const string SandboxPath = "Assets/Scenes/Sandbox.unity";

        static DemoBuild()
        {
            EditorApplication.delayCall += ConfigureProject;
        }

        private static void ConfigureProject()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(SandboxPath, true)
            };
            PlayerSettings.companyName = "DontForgetMe";
            PlayerSettings.productName = "Dont Forget Me - Opening";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 800;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.runInBackground = false;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneOSX, true);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, true);

            Shader standard = Shader.Find("Standard");
            if (standard == null)
                throw new InvalidOperationException("The Demo needs the Built-in Render Pipeline Standard shader.");
            UnityEngine.Object[] graphics = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (graphics.Length == 0)
                throw new InvalidOperationException("GraphicsSettings.asset is not available. Reopen the project before building.");
            SerializedObject settings = new SerializedObject(graphics[0]);
            SerializedProperty shaders = settings.FindProperty("m_AlwaysIncludedShaders");
            if (shaders == null)
                throw new InvalidOperationException("Cannot configure the required Standard shader.");
            bool included = false;
            for (int i = 0; i < shaders.arraySize; i++)
                if (shaders.GetArrayElementAtIndex(i).objectReferenceValue == standard) included = true;
            if (!included)
            {
                shaders.InsertArrayElementAtIndex(shaders.arraySize);
                shaders.GetArrayElementAtIndex(shaders.arraySize - 1).objectReferenceValue = standard;
                settings.ApplyModifiedPropertiesWithoutUndo();
            }
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Dont Forget Me/Open Opening Scene")]
        public static void OpenScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Dont Forget Me/Open Sandbox Scene")]
        public static void OpenSandbox()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(SandboxPath);
        }

        [MenuItem("Dont Forget Me/Build/macOS")]
        public static void BuildMac()
        {
            Build(BuildTarget.StandaloneOSX, "Builds/macOS/DontForgetMe.app");
        }

        [MenuItem("Dont Forget Me/Build/Windows x64")]
        public static void BuildWindows()
        {
            Build(BuildTarget.StandaloneWindows64, "Builds/Windows/DontForgetMe.exe");
        }

        [MenuItem("Dont Forget Me/Build Sandbox/Windows x64")]
        public static void BuildSandboxWindows()
        {
            Build(BuildTarget.StandaloneWindows64, "Builds/SandboxWindows/DontForgetMeSandbox.exe", true);
        }

        [MenuItem("Dont Forget Me/Build Sandbox/macOS")]
        public static void BuildSandboxMac()
        {
            Build(BuildTarget.StandaloneOSX, "Builds/SandboxMac/DontForgetMeSandbox.app", true);
        }

        private static void Build(BuildTarget target, string path, bool sandboxFirst = false)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
                throw new InvalidOperationException("Install the " + target + " Build Support module in Unity Hub first.");
            ConfigureProject();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = sandboxFirst ? new[] { SandboxPath, ScenePath } : new[] { ScenePath, SandboxPath },
                locationPathName = path,
                target = target,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Demo build failed: " + report.summary.result);
            Debug.Log("Demo built successfully: " + Path.GetFullPath(path));
        }
    }
}
