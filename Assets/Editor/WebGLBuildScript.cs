using System.IO;
using UnityEditor;

public static class WebGLBuildScript
{
    public static void Build()
    {
        string outputPath = Path.Combine(Directory.GetCurrentDirectory(), "WebGLBuild");

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/SampleScene.unity" },
            locationPathName = outputPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        BuildPipeline.BuildPlayer(options);
    }
}
