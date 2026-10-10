using UnityEngine;

namespace ReeCamera {
    public class CameraPreviewMask : MonoBehaviour {
        private CameraHandleController _handle;
        private RenderTexture _texture;
        private Material _material;

        public static CameraPreviewMask Attach(Camera camera, CameraHandleController handle) {
            var component = camera.gameObject.AddComponent<CameraPreviewMask>();
            component._handle = handle;
            component.enabled = false;
            return component;
        }

        private void OnPostRender() {
            // Post-processing uses an intermediate target whose MSAA can differ from camera.targetTexture.
            var source = RenderTexture.active;
            if (source == null) return;
            if (_texture == null || _texture.width != source.width || _texture.height != source.height ||
                _texture.antiAliasing != source.antiAliasing) {
                Release();
                _texture = new RenderTexture(source.width, source.height, 0, RenderTextureFormat.R8) {
                    name = "ReeCameraPreviewMask",
                    antiAliasing = source.antiAliasing,
                    filterMode = FilterMode.Bilinear
                };
                _texture.Create();
                _material = new Material(BundleLoader.PreviewShader);
                _handle.SetPreviewMask(_texture);
            }

            // Reuse the rendered depth; the far-plane triangle passes only where a nearer surface exists.
            Graphics.SetRenderTarget(_texture.colorBuffer, source.depthBuffer);
            GL.Clear(false, true, Color.black);
            _material.SetPass(1);
            Graphics.DrawProceduralNow(MeshTopology.Triangles, 3);
            RenderTexture.active = source;
        }

        private void OnDisable() {
            Release();
        }

        private void Release() {
            if (_handle != null) _handle.SetPreviewMask(null);
            if (_texture != null) {
                _texture.Release();
                Destroy(_texture);
                _texture = null;
            }
            if (_material != null) {
                Destroy(_material);
                _material = null;
            }
        }
    }
}
