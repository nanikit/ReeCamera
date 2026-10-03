using System;
using System.Diagnostics;
using System.IO;

namespace ReeCamera {
    internal sealed class PresetFileWatcher : IDisposable {
        private readonly FileSystemWatcher _watcher;
        private readonly object _gate = new object();
        private readonly Stopwatch _quietPeriod = new Stopwatch();
        private bool _disposed;

        internal PresetFileWatcher(string directoryPath) {
            Directory.CreateDirectory(directoryPath);
            _watcher = new FileSystemWatcher(directoryPath, "*.json") {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
            };
            _watcher.Changed += OnPresetChanged;
            _watcher.Created += OnPresetChanged;
            _watcher.Deleted += OnPresetChanged;
            _watcher.Renamed += OnPresetChanged;
            _watcher.EnableRaisingEvents = true;
        }

        internal bool TryConsumeReloadRequest() {
            lock (_gate) {
                if (!_quietPeriod.IsRunning || _quietPeriod.ElapsedMilliseconds < 300) return false;
                _quietPeriod.Reset();
                return true;
            }
        }

        public void Dispose() {
            lock (_gate) {
                _disposed = true;
                _quietPeriod.Reset();
            }
            _watcher.Dispose();
        }

        private void OnPresetChanged(object sender, FileSystemEventArgs args) {
            lock (_gate) {
                if (!_disposed) _quietPeriod.Restart();
            }
        }
    }
}