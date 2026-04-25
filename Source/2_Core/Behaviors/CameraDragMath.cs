using UnityEngine;

namespace ReeCamera {
    public static class CameraDragMath {
        public static MovementConfig SolveOffsets(
            in MovementConfig current,
            in ReeTransform basePoseLocal,
            in ReeTransform dropPoseLocal
        ) {
            var result = current;
            var rotOffsetQ = Quaternion.Inverse(basePoseLocal.Rotation) * dropPoseLocal.Rotation;

            switch (current.OffsetType) {
                case OffsetType.Local: {
                    result.PositionOffset = Quaternion.Inverse(dropPoseLocal.Rotation) *
                                            (dropPoseLocal.Position - basePoseLocal.Position);
                    result.RotationOffset = NormalizeEuler(rotOffsetQ.eulerAngles);
                    break;
                }
                case OffsetType.Global:
                default: {
                    result.PositionOffset = dropPoseLocal.Position - basePoseLocal.Position;
                    result.RotationOffset = NormalizeEuler(rotOffsetQ.eulerAngles);
                    break;
                }
            }

            return result;
        }

        internal static Vector3 NormalizeEuler(in Vector3 euler) {
            return new Vector3(NormalizeAngle(euler.x), NormalizeAngle(euler.y), NormalizeAngle(euler.z));
        }

        internal static float NormalizeAngle(float degrees) {
            var a = degrees % 360f;
            if (a > 180f) a -= 360f;
            else if (a < -180f) a += 360f;
            return a;
        }

        public static ReeTransform WorldToLocal(in ReeTransform parent, in Vector3 worldPosition, in Quaternion worldRotation) {
            return new ReeTransform(
                parent.WorldToLocalPosition(worldPosition),
                parent.WorldToLocalRotation(worldRotation)
            );
        }
    }
}
