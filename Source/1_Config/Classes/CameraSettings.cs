using Newtonsoft.Json;
using UnityEngine;

namespace ReeCamera {
    public struct CameraSettings {
        public HandleVisibility HandleVisibility;
        public bool IgnoreCameraUtils;
        public float FieldOfView;
        public float NearClipPlane;
        public float FarClipPlane;
        public bool Orthographic;
        public float OrthographicSize;
        public Vector2 CenterOffset;

        [JsonConstructor]
        public CameraSettings(int _) {
            HandleVisibility = HandleVisibility.HmdOnly;
            IgnoreCameraUtils = false;
            FieldOfView = 90.0f;
            NearClipPlane = 0.05f;
            FarClipPlane = 1000f;
            Orthographic = false;
            OrthographicSize = 2.0f;
            CenterOffset = Vector2.zero;
        }

        public static CameraSettings Default => new CameraSettings(0);
    }
}
