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
        foreach (var samples in new[] { 1, 2, 4 }) {
            foreach (var orthographic in new[] { false, true }) {
                HidingThePreviewBackgroundPreservesNearbyObjectsAndRecordedVideo(shader, samples, orthographic);
            }
        }
        // Exercise the compiled bundle on the GPU, including the texture-array destination used by VR.
        foreach (var alpha in new[] { 0f, 1f }) {
            foreach (var stereo in new[] { false, true }) {
                foreach (var showBackground in new[] { false, true }) {
                    foreach (var coverage in new[] { 0f, 0.5f, 1f }) {
                        PreviewKeepsNearbyObjectsRegardlessOfSourceAlphaInEveryEye(shader, stereo, alpha, showBackground, coverage);
                    }
                }
            }
        }
        Debug.Log("Preview GPU checks passed: perspective/orthographic MSAA 1/2/4 preserves output; mono/stereo coverage 0/0.5/1 ignores source alpha and preserves scene color and bloom.");
    }

    private static void HidingThePreviewBackgroundPreservesNearbyObjectsAndRecordedVideo(Shader shader, int samples, bool orthographic) {
        var previousSamples = QualitySettings.antiAliasing;
        QualitySettings.antiAliasing = samples == 1 ? 0 : samples;
        var cameraObject = new GameObject("Preview depth test camera");
        var camera = cameraObject.AddComponent<Camera>();
        var effect = cameraObject.AddComponent<PreviewTestImageEffect>();
        camera.transform.position = new Vector3(100, 100, 100);
        camera.enabled = false;
        camera.allowMSAA = samples > 1;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.2f, 0.1f, 0.4f, 0);
        camera.cullingMask = 1 << 31;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 5f;
        camera.orthographic = orthographic;
        camera.orthographicSize = 1;
        var source = new RenderTexture(64, 64, 24) { antiAliasing = samples };
        source.Create();
        camera.targetTexture = source;
        var depth = new RenderTexture(64, 64, 0, RenderTextureFormat.R8) { antiAliasing = samples };
        depth.Create();
        var material = new Material(shader);
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.layer = 31;
        cube.transform.position = camera.transform.position + new Vector3(-0.3f, 0.2f, 2);
        cube.transform.localScale = Vector3.one * 0.4f;
        var cubeMaterial = new Material(Shader.Find("Unlit/Color")) { color = new Color(0, 1, 0, 0) };
        cube.GetComponent<Renderer>().sharedMaterial = cubeMaterial;
        var pixels = new Texture2D(64, 64, TextureFormat.RGBAFloat, false, true);
        var previousTarget = RenderTexture.active;
        try {
            camera.Render();
            RenderTexture.active = source;
            pixels.ReadPixels(new Rect(0, 0, 64, 64), 0, 0);
            var before = pixels.GetPixels();
            effect.Mask = depth;
            effect.Material = material;
            camera.Render();
            RenderTexture.active = source;
            pixels.ReadPixels(new Rect(0, 0, 64, 64), 0, 0);
            var outputChanged = !before.SequenceEqual(pixels.GetPixels());
            RenderTexture.active = depth;
            pixels.ReadPixels(new Rect(0, 0, 64, 64), 0, 0);
            var position = camera.WorldToViewportPoint(cube.transform.position);
            var foreground = pixels.GetPixel((int)(position.x * 64), (int)(position.y * 64)).r;
            var background = pixels.GetPixel(0, 0).r;
            Debug.Log($"Preview mask: MSAA={samples}, actual={depth.antiAliasing}, orthographic={orthographic}, foreground={foreground}, background={background}, outputChanged={outputChanged}");
            if (outputChanged) throw new InvalidDataException("Depth capture changed camera output.");
            if (background != 0 || foreground != 1) {
                throw new InvalidDataException($"Depth copy mismatch: MSAA={samples}, orthographic={orthographic}, foreground={foreground}, background={background}");
            }
        } finally {
            QualitySettings.antiAliasing = previousSamples;
            RenderTexture.active = previousTarget;
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(cube);
            UnityEngine.Object.DestroyImmediate(cubeMaterial);
            UnityEngine.Object.DestroyImmediate(material);
            UnityEngine.Object.DestroyImmediate(pixels);
            source.Release(); depth.Release();
            UnityEngine.Object.DestroyImmediate(source);
            UnityEngine.Object.DestroyImmediate(depth);
        }
    }

    private static void PreviewKeepsNearbyObjectsRegardlessOfSourceAlphaInEveryEye(Shader shader, bool stereo, float alpha, bool showBackground, float coverage) {
        var source = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
        source.SetPixel(0, 0, new Color(0.2f, 0.4f, 0.6f, alpha));
        source.Apply();
        var material = new Material(shader) { mainTexture = source };
        var depthTexture = new Texture2D(1, 1, TextureFormat.RFloat, false, true);
        depthTexture.SetPixel(0, 0, new Color(coverage, 0, 0));
        depthTexture.Apply();
        material.SetTexture("_PreviewMask", depthTexture);
        material.SetFloat("_ShowBackground", showBackground ? 1f : 0f);
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
                    var expected = Color.Lerp(Color.magenta, new Color(0.2f, 0.4f, 0.6f, 0), showBackground ? 1f : coverage);
                    if (Mathf.Abs(color.r - expected.r) > 0.01f || Mathf.Abs(color.g - expected.g) > 0.01f ||
                        Mathf.Abs(color.b - expected.b) > 0.01f || Mathf.Abs(color.a - expected.a) > 0.01f) {
                        throw new InvalidDataException($"Preview blend mismatch: stereo={stereo}, alpha={alpha}, coverage={coverage}, showBackground={showBackground}, eye={eye}, pixel={color}, expected={expected}");
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
            UnityEngine.Object.DestroyImmediate(depthTexture);
        }
    }
}

[ExecuteAlways]
public class PreviewTestImageEffect : MonoBehaviour {
    public RenderTexture Mask;
    public Material Material;
    private void OnPostRender() {
        if (Mask == null) return;
        var active = RenderTexture.active;
        if (Mask.antiAliasing != active.antiAliasing) {
            Mask.Release();
            Mask.antiAliasing = active.antiAliasing;
            Mask.Create();
        }
        Graphics.SetRenderTarget(Mask.colorBuffer, active.depthBuffer);
        GL.Clear(false, true, Color.black);
        Material.SetPass(1);
        Graphics.DrawProceduralNow(MeshTopology.Triangles, 3);
        RenderTexture.active = active;
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination) {
        Graphics.Blit(source, destination);
    }
}
