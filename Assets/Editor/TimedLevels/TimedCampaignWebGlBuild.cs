using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class TimedCampaignWebGlBuild
{
    private const string OutputPath = "C:/Users/Михаил/DruggingStuff/.utmp/timed-campaign-webgl-final";

    public static void Run()
    {
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (scenes.Length == 0)
            throw new InvalidOperationException("No enabled scenes were found in build settings.");

        Directory.CreateDirectory(OutputPath);
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = OutputPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.Development
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);

        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException($"WebGL build failed: {report.summary.result}, {report.summary.totalErrors} errors.");

        Debug.Log($"TIMED_CAMPAIGN_WEBGL_BUILD_PASS: {report.summary.totalSize} bytes, {report.summary.totalWarnings} warnings");
    }
}
