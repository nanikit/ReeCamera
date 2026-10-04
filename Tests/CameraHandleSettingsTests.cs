using CameraUtils.Core;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ReeCamera.Tests {
    public class CameraHandleSettingsTests {
        [Theory]
        [InlineData(0, false)]
        [InlineData(0, true)]
        [InlineData(-1, false)]
        [InlineData(-1, true)]
        [InlineData((1 << 3) | (1 << 5) | (1 << 29), false)]
        [InlineData((1 << 3) | (1 << 5) | (1 << 29), true)]
        public void CameraOutputsPreservePresetLayersUntilAHandleIsSharedAndAlwaysExcludeHeadsetOnlyHandles(int mask, bool hasSharedHandles) {
            var actual = CameraHandleController.GetOutputCullingMask(mask, hasSharedHandles);
            var headsetOnly = 1 << (int)VisibilityLayer.HmdOnly;
            var shared = 1 << (int)VisibilityLayer.AlwaysVisible;

            Assert.Equal(0, actual & headsetOnly);
            Assert.Equal(hasSharedHandles ? shared : mask & shared, actual & shared);
            Assert.Equal(mask & ~(headsetOnly | shared), actual & ~(headsetOnly | shared));
        }

        [Fact]
        public void ExistingPresetsKeepHeadsetOnlyHandlesAndNamedChoicesSurviveSavingAndCopying() {
            var json = JObject.Parse(@"{
                'FormatVersion': 1,
                'Layouts': [{
                    'MainCamera': { 'CameraSettings': { 'FieldOfView': 75 } },
                    'SecondaryCameras': [
                        { 'CameraSettings': {} },
                        {}
                    ]
                }]
            }");
            Assert.True(ScenePresetV1.TryDeserialize(json, out var original, out var failure), failure);
            AssertVisibility(original, "HmdOnly", "HmdOnly", "HmdOnly");

            var saved = new ScenePresetV1(original.LayoutConfigs).Serialize();
            var layoutJson = saved["Layouts"][0];
            layoutJson["MainCamera"]["CameraSettings"]["HandleVisibility"] = "HmdAndDesktop";
            layoutJson["SecondaryCameras"][0]["CameraSettings"]["HandleVisibility"] = "Hidden";
            Assert.True(ScenePresetV1.TryDeserialize(saved, out var edited, out failure), failure);
            AssertVisibility(edited, "HmdAndDesktop", "Hidden", "HmdOnly");

            var layout = edited.LayoutConfigs[0];
            var main = MainCameraConfig.Default;
            main.CopyFrom(layout.MainCamera);
            var hidden = new SecondaryCameraConfig();
            hidden.CopyFrom(layout.SecondaryCameras.Items[0]);
            var headsetOnly = new SecondaryCameraConfig();
            headsetOnly.CopyFrom(layout.SecondaryCameras.Items[1]);
            var copied = new ScenePresetV1(new[] {
                new SceneLayoutConfig(main, new[] { hidden, headsetOnly })
            });

            Assert.True(ScenePresetV1.TryDeserialize(copied.Serialize(), out var reloaded, out failure), failure);
            AssertVisibility(reloaded, "HmdAndDesktop", "Hidden", "HmdOnly");
            Assert.Equal(75f, reloaded.LayoutConfigs[0].MainCamera.CameraSettingsOV.Value.FieldOfView);
        }

        private static void AssertVisibility(IScenePreset preset, string main, string firstSecondary, string secondSecondary) {
            var layout = new ScenePresetV1(preset.LayoutConfigs).Serialize()["Layouts"][0];
            Assert.Equal(main, (string)layout["MainCamera"]["CameraSettings"]["HandleVisibility"]);
            Assert.Equal(firstSecondary, (string)layout["SecondaryCameras"][0]["CameraSettings"]["HandleVisibility"]);
            Assert.Equal(secondSecondary, (string)layout["SecondaryCameras"][1]["CameraSettings"]["HandleVisibility"]);
        }
    }
}
