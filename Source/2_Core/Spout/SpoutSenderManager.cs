using System;
using System.Collections.Generic;
using System.Threading;
using JetBrains.Annotations;
using UnityEngine;

namespace ReeCamera.Spout {
    public class SpoutSenderManager : MonoBehaviour {
        private const float DisposeDelay = 1.0f;
        private const string ClearAlphaKeyword = "CLEAR_ALPHA";

        private readonly Dictionary<string, ManagedSpoutSender> _senders = new();
        private readonly List<string> _toRemove = new();

        #region Public API

        public void Acquire(string channelName, int width, int height) {
            if (_senders.TryGetValue(channelName, out var existing)) {
                existing.CancelScheduledDispose();
                existing.RefCount++;

                if (existing.Width != width || existing.Height != height) {
                    existing.Reinitialize(channelName, width, height);
                }
                return;
            }

            var sender = new ManagedSpoutSender();
            sender.Initialize(channelName, width, height, BundleLoader.Materials.spoutBlitMaterial.shader);
            sender.RefCount = 1;
            _senders[channelName] = sender;
        }

        public void SendTexture(string channelName, RenderTexture texture) {
            if (!_senders.TryGetValue(channelName, out var sender)) return;
            if (sender.IsDisposeScheduled) return;

            sender.SendTexture(texture);
        }

        public void Release(string channelName) {
            if (!_senders.TryGetValue(channelName, out var sender)) return;

            sender.RefCount--;
            if (sender.RefCount <= 0) {
                sender.ScheduleDispose(Time.time + DisposeDelay);
            }
        }

        #endregion

        #region MonoBehaviour

        private void Update() {
            _toRemove.Clear();

            Util.BeginBatch();
            foreach (var kvp in _senders) {
                var sender = kvp.Value;

                if (sender.IsDisposeScheduled && Time.time >= sender.DisposeTime) {
                    sender.Dispose();
                    _toRemove.Add(kvp.Key);
                    continue;
                }

                sender.IssueUpdateEvent();
            }
            Util.ExecuteBatch();

            foreach (var key in _toRemove) {
                _senders.Remove(key);
            }
        }

        private void OnDestroy() {
            foreach (var sender in _senders.Values) {
                sender.Dispose();
            }
            _senders.Clear();
        }

        #endregion

        #region ManagedSpoutSender

        private class ManagedSpoutSender {
            public int RefCount;
            public int Width { get; private set; }
            public int Height { get; private set; }
            public bool IsDisposeScheduled { get; private set; }
            public float DisposeTime { get; private set; }

            private IntPtr _plugin;
            private Texture2D _sharedTexture;
            private Material _blitMaterial;
            private Shader _blitShader;
            private bool _pluginInitialized;
            private bool _sharedTextureInitialized;
            private EventWaitHandle _syncEvent;

            public void Initialize(string channelName, int width, int height, Shader blitShader) {
                Width = width;
                Height = height;
                _blitShader = blitShader;

                _plugin = PluginEntry.CreateSender(channelName, width, height);
                if (_plugin == IntPtr.Zero) return;

                _pluginInitialized = true;
                InitializeSharedTexture();
                InitializeBlitMaterial();
                InitializeSyncEvent(channelName);
            }

            public void Reinitialize(string channelName, int width, int height) {
                DisposeNative();
                Initialize(channelName, width, height, _blitShader);
            }

            public void SendTexture(RenderTexture source) {
                if (!_pluginInitialized) return;

                // Lazy initialization - Spout may not be ready immediately
                InitializeSharedTexture();
                if (!_sharedTextureInitialized) return;

                _blitMaterial.EnableKeyword(ClearAlphaKeyword);

                var tempRT = RenderTexture.GetTemporary(_sharedTexture.width, _sharedTexture.height);
                Graphics.Blit(source, tempRT, _blitMaterial, 0);
                Graphics.CopyTexture(tempRT, _sharedTexture);
                RenderTexture.ReleaseTemporary(tempRT);

                _syncEvent?.Set();
            }

            public void IssueUpdateEvent() {
                if (_pluginInitialized) {
                    Util.IssuePluginEvent(PluginEntry.Event.Update, _plugin);
                }
            }

            public void ScheduleDispose(float time) {
                IsDisposeScheduled = true;
                DisposeTime = time;
            }

            public void CancelScheduledDispose() {
                IsDisposeScheduled = false;
            }

            public void Dispose() {
                DisposeNative();
                Util.Destroy(_blitMaterial);
                _blitMaterial = null;
            }

            private void DisposeNative() {
                if (_pluginInitialized) {
                    Util.IssuePluginEvent(PluginEntry.Event.Dispose, _plugin);
                    _plugin = IntPtr.Zero;
                    _pluginInitialized = false;
                }

                _syncEvent?.Dispose();
                _syncEvent = null;

                Util.Destroy(_sharedTexture);
                _sharedTexture = null;
                _sharedTextureInitialized = false;
            }

            private void InitializeSharedTexture() {
                if (_sharedTextureInitialized) return;

                var ptr = PluginEntry.GetTexturePointer(_plugin);
                if (ptr == IntPtr.Zero) return;

                _sharedTexture = Texture2D.CreateExternalTexture(
                    PluginEntry.GetTextureWidth(_plugin),
                    PluginEntry.GetTextureHeight(_plugin),
                    TextureFormat.ARGB32, false, false, ptr
                );
                _sharedTexture.hideFlags = HideFlags.DontSave;
                _sharedTextureInitialized = true;
            }

            private void InitializeBlitMaterial() {
                if (_blitMaterial != null) return;

                _blitMaterial = new Material(_blitShader) {
                    hideFlags = HideFlags.DontSave
                };
            }

            private void InitializeSyncEvent(string channelName) {
                var eventName = $"{channelName}_Sync_Event";
                _syncEvent = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
            }
        }

        #endregion
    }
}
