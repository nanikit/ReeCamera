using CameraUtils.Core;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ReeCamera.Tests {
    public class CameraHandleSettingsTests {
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData((1 << 3) | (1 << 5) | (1 << 29))]
        public void CameraOutputsExcludeHeadsetHandlesAndPreserveOtherPresetLayers(int mask) {
            var actual = CameraHandleController.GetOutputCullingMask(mask);
            var headsetOnly = 1 << (int)VisibilityLayer.HmdOnly;

            Assert.Equal(mask & ~headsetOnly, actual);
        }

        [Fact]
        public void ExistingPresetsKeepDefaultPreviewsAndPerCameraChoicesSurviveSavingAndCopying() {
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
            AssertPreviewSettings(saved, 0.15f, true, 0.15f, true);
            ConfigureLegacySharedPreview(saved);
            Assert.True(ScenePresetV1.TryDeserialize(saved, out var edited, out failure), failure);
            AssertVisibility(edited, "HmdOnly", "Hidden", "HmdOnly");

            var copied = CopyPresetCameras(edited);
            Assert.True(ScenePresetV1.TryDeserialize(copied.Serialize(), out var reloaded, out failure), failure);
            AssertVisibility(reloaded, "HmdOnly", "Hidden", "HmdOnly");
            AssertPreviewSettings(new ScenePresetV1(reloaded.LayoutConfigs).Serialize(), 0.3f, false, 0.1f, true);
            Assert.Equal(75f, reloaded.LayoutConfigs[0].MainCamera.CameraSettingsOV.Value.FieldOfView);
        }

        private static void ConfigureLegacySharedPreview(JObject saved) {
            var layoutJson = saved["Layouts"][0];
            layoutJson["MainCamera"]["CameraSettings"]["HandleVisibility"] = "HmdAndDesktop";
            layoutJson["MainCamera"]["CameraSettings"]["PreviewViewportHeight"] = 0.3f;
            layoutJson["MainCamera"]["CameraSettings"]["ShowPreviewBackground"] = false;
            layoutJson["SecondaryCameras"][0]["CameraSettings"]["HandleVisibility"] = "Hidden";
            layoutJson["SecondaryCameras"][0]["CameraSettings"]["PreviewViewportHeight"] = 0.1f;
        }

        private static ScenePresetV1 CopyPresetCameras(IScenePreset preset) {
            var layout = preset.LayoutConfigs[0];
            var main = MainCameraConfig.Default;
            main.CopyFrom(layout.MainCamera);
            var hidden = new SecondaryCameraConfig();
            hidden.CopyFrom(layout.SecondaryCameras.Items[0]);
            var headsetOnly = new SecondaryCameraConfig();
            headsetOnly.CopyFrom(layout.SecondaryCameras.Items[1]);
            return new ScenePresetV1(new[] {
                new SceneLayoutConfig(main, new[] { hidden, headsetOnly })
            });
        }

        private static void AssertPreviewSettings(JObject preset, float mainSize, bool mainBackground, float secondarySize, bool secondaryBackground) {
            var layout = preset["Layouts"][0];
            var main = layout["MainCamera"]["CameraSettings"];
            var secondary = layout["SecondaryCameras"][0]["CameraSettings"];
            Assert.Equal(mainSize, (float?)main["PreviewViewportHeight"]);
            Assert.Equal(mainBackground, (bool?)main["ShowPreviewBackground"]);
            Assert.Equal(secondarySize, (float?)secondary["PreviewViewportHeight"]);
            Assert.Equal(secondaryBackground, (bool?)secondary["ShowPreviewBackground"]);
        }

        private static void AssertVisibility(IScenePreset preset, string main, string firstSecondary, string secondSecondary) {
            var layout = new ScenePresetV1(preset.LayoutConfigs).Serialize()["Layouts"][0];
            Assert.Equal(main, (string)layout["MainCamera"]["CameraSettings"]["HandleVisibility"]);
            Assert.Equal(firstSecondary, (string)layout["SecondaryCameras"][0]["CameraSettings"]["HandleVisibility"]);
            Assert.Equal(secondSecondary, (string)layout["SecondaryCameras"][1]["CameraSettings"]["HandleVisibility"]);
        }
    }
}
