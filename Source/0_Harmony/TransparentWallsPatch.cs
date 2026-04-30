using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;

namespace ReeCamera {
    [HarmonyPatch(typeof(StretchableObstacle), nameof(StretchableObstacle.SetAllProperties))]
    internal static class TransparentWallsPatch {
        private const int WallsLayer = 11;
        private const int WallTexturesLayer = 25;

        public static void MakeWallsOpaqueForMainCam() {
            var camera = Camera.main;
            if (camera == null) return;
            camera.cullingMask |= 1 << WallTexturesLayer;
        }

        [UsedImplicitly]
        // ReSharper disable InconsistentNaming
        private static void Postfix(
            Transform ____obstacleCore,
            ParametricBoxFakeGlowController ____obstacleFakeGlow,
            MaterialPropertyBlockController[] ____materialPropertyBlockControllers
        ) {
            if (____obstacleCore == null) return;

            if (____obstacleFakeGlow != null && ____obstacleFakeGlow.enabled) {
                ____obstacleFakeGlow.gameObject.layer = WallsLayer;

                if (____materialPropertyBlockControllers != null
                    && ____materialPropertyBlockControllers.Length > 1
                    && ____materialPropertyBlockControllers[1] != null) {
                    ____materialPropertyBlockControllers[1].gameObject.layer = WallTexturesLayer;
                }
            }

            ____obstacleCore.gameObject.layer = WallTexturesLayer;
        }
        // ReSharper restore InconsistentNaming
    }
}
