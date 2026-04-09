using System;
using JetBrains.Annotations;
using UnityEngine;
using Zenject;

namespace ReeCamera {
    public class GameSceneManager : MonoBehaviour {
        private static readonly string[] NoodleOriginNames = {
            "NoodlePlayerTrackHead",
            "NoodlePlayerTrackRoot"
        };

        [Inject, UsedImplicitly]
        private PlayerTransforms _playerTransforms;

        [Inject, UsedImplicitly]
        private BeatmapObjectManager _beatmapObjectManager;

        [Inject, UsedImplicitly]
        private Spout.SpoutSenderManager _spoutManager;

        private Transform _noodleOrigin;

        private void Awake() {
            switch (PluginState.LaunchTypeOV.Value) {
                case LaunchType.VR: {
                    var config = MainPluginConfig.Instance.GameplayConfigVR;
                    ReeSceneController.Instantiate(null, config, _spoutManager);
                    break;
                }
                case LaunchType.FPFC:
                default: {
                    var config = MainPluginConfig.Instance.GameplayConfigFPFC;
                    FramerateManager.Instantiate(gameObject, config.FramerateSettingsOV);
                    ReeSceneController.Instantiate(_playerTransforms._originTransform, config, _spoutManager);
                    break;
                }
            }
        }

        private void OnEnable() {
            PluginState.SceneTypeOV.SetValue(SceneType.Gameplay, this);
        }

        private void Start() {
            _beatmapObjectManager.noteWasCutEvent += HandleNoteWasCut;
        }

        private void OnDestroy() {
            _beatmapObjectManager.noteWasCutEvent -= HandleNoteWasCut;
        }

        private static void HandleNoteWasCut(NoteController noteController, in NoteCutInfo noteCutInfo) {
            PluginState.NotifyNoteWasCut();
        }

        private ReeTransform GetMapMovementPose() {
            if (_noodleOrigin == null) {
                foreach (string originName in NoodleOriginNames) {
                    var go = GameObject.Find(originName);
                    if (go == null) {
                        continue;
                    }

                    _noodleOrigin = go.transform;
                    break;
                }
            }

            return _noodleOrigin == null
                ? ReeTransform.Identity
                : new ReeTransform(_noodleOrigin.localPosition, _noodleOrigin.localRotation);
        }

        private void LateUpdate() {
            PluginState.MapMovementPoseOV.SetValue(GetMapMovementPose(), this);

            var playerPose = ReeTransform.FromTransform(_playerTransforms._originTransform);
            PluginState.PlayerPoseOV.SetValue(playerPose, this);

            var fpvPose = new ReeTransform(_playerTransforms.headWorldPos, _playerTransforms.headWorldRot);
            PluginState.FirstPersonPoseOV.SetValue(fpvPose, this);
        }
    }
}
