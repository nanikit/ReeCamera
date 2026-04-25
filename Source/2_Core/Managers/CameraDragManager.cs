using UnityEngine;

namespace ReeCamera {
    public class CameraDragManager : MonoBehaviour {
        private const float TriggerReleaseThreshold = 0.5f;
        private const float SnapTolerance = 4f;
        private const float SnapStep = 45f;

        private static CameraDragManager _instance;

        public static CameraDragManager EnsureInstance() {
            if (_instance != null) return _instance;

            var go = new GameObject("ReeCameraDragManager");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<CameraDragManager>();
            return _instance;
        }

        private ICameraDragTarget _target;
        private VRController _controller;
        private Vector3 _grabLocalPosition;
        private Quaternion _grabLocalRotation;

        public void BeginDrag(ICameraDragTarget target, VRController controller) {
            if (target == null || controller == null) return;
            if (_target != null) Cancel();

            _target = target;
            _controller = controller;

            var camTransform = target.CameraTransform;
            var inverseControllerRot = Quaternion.Inverse(controller.rotation);
            _grabLocalPosition = inverseControllerRot * (camTransform.position - controller.position);
            _grabLocalRotation = inverseControllerRot * camTransform.rotation;

            target.OnDragBegin();
        }

        private void Update() {
            if (_target == null) return;

            if (_controller == null || !_controller.isActiveAndEnabled) {
                Cancel();
                return;
            }

            var pos = _controller.position + _controller.rotation * _grabLocalPosition;
            var rot = ApplySnap(_controller.rotation * _grabLocalRotation);

            _target.OnDragUpdate(pos, rot);

            if (_controller.triggerValue <= TriggerReleaseThreshold) {
                Finish(pos, rot);
            }
        }

        private void Cancel() {
            if (_target == null) return;

            var camTransform = _target.CameraTransform;
            var pos = camTransform != null ? camTransform.position : Vector3.zero;
            var rot = camTransform != null ? camTransform.rotation : Quaternion.identity;
            Finish(pos, rot);
        }

        private void Finish(in Vector3 worldPosition, in Quaternion worldRotation) {
            var target = _target;
            _target = null;
            _controller = null;
            target?.OnDragEnd(worldPosition, worldRotation);
        }

        private static Quaternion ApplySnap(in Quaternion rotation) {
            var euler = rotation.eulerAngles;
            SnapAxis(ref euler.x);
            SnapAxis(ref euler.y);
            SnapAxis(ref euler.z);
            return Quaternion.Euler(euler);
        }

        private static void SnapAxis(ref float angle) {
            var l = angle % SnapStep;
            if (l <= SnapTolerance) angle -= l;
            else if (l >= SnapStep - SnapTolerance) angle += SnapStep - l;
        }
    }
}
