using System;
using System.IO;
using UnityEditor;
using UnityEngine.Rendering;

public static class BuildPreviewBundle {
    public static void Build() {
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
        // Keep the VR variant when building without an XR loader or headset.
#pragma warning disable CS0618
        PlayerSettings.virtualRealitySupported = true;
#pragma warning restore CS0618
        PlayerSettings.stereoRenderingPath = StereoRenderingPath.Instancing;
        Directory.CreateDirectory("Build");
        var manifest = BuildPipeline.BuildAssetBundles("Build", new[] {
            new AssetBundleBuild {
                assetBundleName = "camera_preview",
                assetNames = new[] { "Assets/Shaders/CameraPreview.shader" }
            }
        }, BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode | BuildAssetBundleOptions.ForceRebuildAssetBundle,
            BuildTarget.StandaloneWindows64);
        if (manifest == null) throw new InvalidOperationException("Preview bundle build failed.");

        PreviewRenderingTests.ValidateBundleFile("Build/camera_preview");
        File.Copy("Build/camera_preview", "../Source/9_Resources/AssetBundles/camera_preview", true);
    }
}
