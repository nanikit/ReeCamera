using System.Collections.Generic;
using ReeCamera.Spout;
using UnityEngine;

namespace ReeCamera {
    public class ReeSceneController : MonoBehaviour {
        #region Instantiate

        public static ReeSceneController Instantiate(Transform parent, AbstractSceneConfig config, SpoutSenderManager spoutManager) {
            var go = new GameObject("ReeSceneController");
            go.transform.SetParent(parent, false);

            var component = go.AddComponent<ReeSceneController>();
            component.Construct(config, spoutManager);
            return component;
        }

        #endregion

        #region Construct / Init / Dispose

        public AbstractSceneConfig Config { get; private set; }
        private SpoutSenderManager _spoutManager;

        private void Construct(AbstractSceneConfig config, SpoutSenderManager spoutManager) {
            Config = config;
            _spoutManager = spoutManager;
        }

        private void Start() {
            Config.LayoutConfigs.AddStateListener(OnLayoutsChanged);
        }

        private void OnDestroy() {
            Config.LayoutConfigs.RemoveStateListener(OnLayoutsChanged);
            DisposeLayouts();
        }

        #endregion

        #region Events

        private readonly List<ReeLayoutController> _layoutControllers = new List<ReeLayoutController>();

        private void OnLayoutsChanged(IReadOnlyList<SceneLayoutConfig> configs) {
            DisposeLayouts();

            foreach (var config in configs) {
                var controller = ReeLayoutController.Instantiate(transform, config, _spoutManager);
                _layoutControllers.Add(controller);
            }
        }

        private void DisposeLayouts() {
            foreach (var layoutController in _layoutControllers) {
                DestroyImmediate(layoutController.gameObject);
            }

            _layoutControllers.Clear();
        }

        #endregion
    }
}