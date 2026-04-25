using System;
using System.Linq;
using CameraUtils.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VRUIControls;

namespace ReeCamera {
    public interface ICameraDragTarget {
        Transform CameraTransform { get; }
        RenderTexture PreviewTexture { get; }
        void OnDragBegin();
        void OnDragUpdate(in Vector3 worldPosition, in Quaternion worldRotation);
        void OnDragEnd(in Vector3 worldPosition, in Quaternion worldRotation);
    }

    public class CameraHandleController : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler {
        // Resolve the installed CameraUtils enum; compiled constants otherwise retain the build version's layer.
        private static readonly int HmdOnlyLayer = (int)Enum.Parse(typeof(VisibilityLayer), nameof(VisibilityLayer.HmdOnly));
        private const float DesiredSubtendedAngleDeg = 15f;
        private const float MinPreviewHeight = 0.05f;
        private const float MaxPreviewHeight = 0.50f;
        private const float BodyVerticalRadius = 0.035f;
        private const float PreviewGap = 0.01f;

        private static Material _bodyMaterial;
        private static Material _bodyHoverMaterial;
        private static Material _previewMaterial;

        public ICameraDragTarget Target { get; private set; }

        public static CameraHandleController Attach(Transform parent, ICameraDragTarget target) {
            var go = new GameObject("ReeCameraHandle");
            go.transform.SetParent(parent, false);

            var component = go.AddComponent<CameraHandleController>();
            component.Target = target;
            component.Build();
            return component;
        }

        private MeshRenderer _bodyRenderer;
        private RawImage _previewImage;
        private Canvas _previewCanvas;
        private RectTransform _previewRect;

        private void Build() {
            EnsureSharedMaterials();

            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Body";
            body.transform.SetParent(transform, false);
            body.transform.localScale = new Vector3(0.04f, 0.05f, 0.07f);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            body.transform.localPosition = new Vector3(0f, 0f, -0.05f);

            _bodyRenderer = body.GetComponent<MeshRenderer>();
            _bodyRenderer.sharedMaterial = _bodyMaterial;

            BuildPreview();
            SetLayerRecursively(transform, HmdOnlyLayer);
        }

        private static void SetLayerRecursively(Transform target, int layer) {
            target.gameObject.layer = layer;
            for (var i = 0; i < target.childCount; i++) {
                SetLayerRecursively(target.GetChild(i), layer);
            }
        }

        private void BuildPreview() {
            var canvasGo = new GameObject("Preview");
            canvasGo.transform.SetParent(transform, false);
            canvasGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            _previewCanvas = canvasGo.AddComponent<Canvas>();
            _previewCanvas.renderMode = RenderMode.WorldSpace;
            // BeatLeader UI can use ZTest Always, so depth alone cannot hide it behind the preview.
            // Prioritize the preview over UI; its shader still depth-tests against opaque scene objects.
            _previewCanvas.sortingOrder = short.MaxValue;

            _previewRect = _previewCanvas.GetComponent<RectTransform>();
            _previewRect.sizeDelta = new Vector2(0.32f, 0.18f);
            _previewRect.localScale = Vector3.one;
            UpdatePreviewPosition(_previewRect.sizeDelta.y);

            var imageGo = new GameObject("Image");
            imageGo.transform.SetParent(canvasGo.transform, false);
            _previewImage = imageGo.AddComponent<RawImage>();
            _previewImage.material = _previewMaterial;
            var imageRect = _previewImage.rectTransform;
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;

            _previewImage.color = Color.white;
            _previewImage.raycastTarget = false;
        }

        private static void EnsureSharedMaterials() {
            if (_bodyMaterial == null || _bodyHoverMaterial == null) {
                // Beat Saber's bundled glowing shader is loaded but not registered with Shader.Find.
                var shader = Resources.FindObjectsOfTypeAll<Shader>().First(value => value.name == "Custom/Glowing");
                _bodyMaterial = new Material(shader) { color = new Color(1f, 0.95f, 0.55f) };
                _bodyHoverMaterial = new Material(shader) { color = new Color(0.55f, 1f, 0.55f) };
            }

            if (_previewMaterial == null) {
                _previewMaterial = new Material(BundleLoader.PreviewShader) { name = "ReeCameraPreviewMaterial" };
            }
        }

        private void Update() {
            UpdatePreviewTexture();
            UpdatePreviewAspect();
        }

        private void UpdatePreviewTexture() {
            var texture = Target?.PreviewTexture;
            if (_previewImage == null) return;

            if (_previewImage.texture != texture) {
                _previewImage.texture = texture;
                _previewImage.enabled = texture != null;
            }
        }

        private void UpdatePreviewAspect() {
            if (_previewImage == null || _previewImage.texture == null) return;

            var texWidth = _previewImage.texture.width;
            var texHeight = _previewImage.texture.height;
            if (texWidth <= 0 || texHeight <= 0) return;

            var aspect = (float)texWidth / texHeight;
            var previewHeight = CalculatePreviewHeight();
            _previewRect.sizeDelta = new Vector2(previewHeight * aspect, previewHeight);
            UpdatePreviewPosition(previewHeight);
        }

        private void UpdatePreviewPosition(float previewHeight) {
            if (_previewRect == null) return;

            var centerY = BodyVerticalRadius + PreviewGap + previewHeight * 0.5f;
            _previewRect.localPosition = new Vector3(0f, centerY, -0.025f);
        }

        private float CalculatePreviewHeight() {
            var hmdPosition = PluginState.FirstPersonPoseOV.Value.Position;
            var distance = Vector3.Distance(hmdPosition, transform.position);
            var halfAngleRad = DesiredSubtendedAngleDeg * 0.5f * Mathf.Deg2Rad;
            var height = 2f * distance * Mathf.Tan(halfAngleRad);
            return Mathf.Clamp(height, MinPreviewHeight, MaxPreviewHeight);
        }

        public void OnPointerClick(PointerEventData eventData) {
            if (Target == null) return;
            if (eventData.currentInputModule is not VRInputModule vrModule) return;

            var controller = vrModule._vrPointer != null ? vrModule._vrPointer.lastSelectedVrController : null;
            if (controller == null) return;

            CameraDragManager.EnsureInstance().BeginDrag(Target, controller);
        }

        public void OnPointerEnter(PointerEventData eventData) {
            if (_bodyRenderer != null) _bodyRenderer.sharedMaterial = _bodyHoverMaterial;
        }

        public void OnPointerExit(PointerEventData eventData) {
            if (_bodyRenderer != null) _bodyRenderer.sharedMaterial = _bodyMaterial;
        }
    }
}
