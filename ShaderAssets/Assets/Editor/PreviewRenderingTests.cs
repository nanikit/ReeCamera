using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

public static class PreviewRenderingTests {
    public static void ValidateBundle() {
        var arguments = Environment.GetCommandLineArgs();
        var index = Array.IndexOf(arguments, "-previewBundle");
        if (index < 0 || index + 1 == arguments.Length) throw new ArgumentException("Specify -previewBundle <path>.");
        ValidateBundleFile(arguments[index + 1]);
    }

    public static void ValidateBundleFile(string path) {
        var bundle = AssetBundle.LoadFromFile(path)
            ?? throw new InvalidOperationException("Preview bundle could not be loaded.");
        try {
            Validate(bundle.LoadAsset<Shader>("CameraPreview"));
        } finally {
            bundle.Unload(true);
        }
    }

    public static void ValidateExisting() {
        var bundle = AssetBundle.LoadFromFile("../Source/9_Resources/AssetBundles/asset_bundle");
        try {
            bundle.LoadAllAssets();
            Validate(Resources.FindObjectsOfTypeAll<Shader>().Single(shader => shader.name == "Ree/ScreenImage"));
        } finally {
            bundle.Unload(true);
        }
    }

    public static void Validate(Shader shader) {
        // Exercise the compiled bundle on the GPU, including the texture-array destination used by VR.
        foreach (var alpha in new[] { 0f, 1f }) {
            foreach (var stereo in new[] { false, true }) {
                CameraImageKeepsItsColorsAndClearsBloomAlphaInEveryEye(shader, stereo, alpha);
            }
        }
        Debug.Log("Preview GPU checks passed: mono/stereo, source alpha 0/1, RGB preserved, bloom alpha zero.");
    }

    private static void CameraImageKeepsItsColorsAndClearsBloomAlphaInEveryEye(Shader shader, bool stereo, float alpha) {
        var source = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
        source.SetPixel(0, 0, new Color(0.2f, 0.4f, 0.6f, alpha));
        source.Apply();
        var material = new Material(shader) { mainTexture = source };
        var target = new RenderTexture(32, 32, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear) {
            dimension = stereo ? TextureDimension.Tex2DArray : TextureDimension.Tex2D,
            volumeDepth = stereo ? 2 : 1
        };
        target.Create();
        var quad = new Mesh {
            vertices = new[] {
                new Vector3(-1, -1, 0.5f), new Vector3(-1, 1, 0.5f),
                new Vector3(1, 1, 0.5f), new Vector3(1, -1, 0.5f)
            },
            uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right },
            triangles = new[] { 0, 1, 2, 0, 2, 3 }
        };
        var previousTarget = RenderTexture.active;
        using var commands = new CommandBuffer();
        // UnityStereoGlobals contains 16 matrices followed by four padded vectors in UnityShaderVariables.cginc.
        // Bind it explicitly because there is no XR camera supplying built-in stereo matrices in this test.
        using var stereoGlobals = new ComputeBuffer(17, 64, ComputeBufferType.Constant);
        stereoGlobals.SetData(Enumerable.Repeat(Matrix4x4.identity, 17).ToArray());
        if (stereo) material.SetConstantBuffer("UnityStereoGlobals", stereoGlobals, 0, 17 * 64);
        try {
            commands.SetRenderTarget(target, 0, CubemapFace.Unknown, -1);
            commands.ClearRenderTarget(false, true, Color.magenta);
            commands.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
            commands.SetSinglePassStereo(stereo ? SinglePassStereoMode.Instancing : SinglePassStereoMode.None);
            if (stereo) commands.EnableShaderKeyword("STEREO_INSTANCING_ON");
            commands.SetInstanceMultiplier(stereo ? 2u : 1u);
            commands.DrawMesh(quad, Matrix4x4.identity, material, 0, 0);
            commands.SetInstanceMultiplier(1);
            commands.SetSinglePassStereo(SinglePassStereoMode.None);
            commands.DisableShaderKeyword("STEREO_INSTANCING_ON");
            Graphics.ExecuteCommandBuffer(commands);

            for (var eye = 0; eye < target.volumeDepth; eye++) {
                Graphics.SetRenderTarget(target, 0, CubemapFace.Unknown, eye);
                var pixels = new Texture2D(32, 32, TextureFormat.RGBAFloat, false, true);
                try {
                    pixels.ReadPixels(new Rect(0, 0, 32, 32), 0, 0);
                    var color = pixels.GetPixel(16, 16);
                    if (Mathf.Abs(color.r - 0.2f) > 0.01f || Mathf.Abs(color.g - 0.4f) > 0.01f ||
                        Mathf.Abs(color.b - 0.6f) > 0.01f || Mathf.Abs(color.a) > 0.01f) {
                        throw new InvalidDataException($"Preview RGB or output alpha mismatch: stereo={stereo}, alpha={alpha}, eye={eye}, pixel={color}");
                    }
                } finally {
                    UnityEngine.Object.DestroyImmediate(pixels);
                }
            }
        } finally {
            RenderTexture.active = previousTarget;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(quad);
            UnityEngine.Object.DestroyImmediate(material);
            UnityEngine.Object.DestroyImmediate(source);
        }
    }
}
