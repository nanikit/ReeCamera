using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace ReeCamera.Tests {
    public class PresetFileWatcherTests : IDisposable {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "ReeCamera.Tests", Guid.NewGuid().ToString("N"));
        private string PresetsDirectory => Path.Combine(_directory, "Presets");

        [Fact]
        public async Task SavingPresetsRequestsOneReloadAfterTheLastSaveIncludingAtomicReplacement() {
            using var watcher = new PresetFileWatcher(PresetsDirectory);
            var preset = Path.Combine(PresetsDirectory, "camera.json");
            File.WriteAllText(preset, "{}");
            await ConsumeOneReload(watcher);

            for (var i = 0; i < 3; i++) {
                File.WriteAllText(preset, $"{{\"revision\":{i}}}");
                await Task.Delay(80);
                Assert.False(watcher.TryConsumeReloadRequest());
            }
            await Task.Delay(150);
            Assert.False(watcher.TryConsumeReloadRequest());
            await ConsumeOneReload(watcher);
            Assert.Equal("{\"revision\":2}", File.ReadAllText(preset));

            var temporary = Path.Combine(PresetsDirectory, "camera.tmp");
            File.WriteAllText(temporary, "{\"revision\":3}");
            File.Replace(temporary, preset, null);
            await ConsumeOneReload(watcher);
            Assert.Equal("{\"revision\":3}", File.ReadAllText(preset));
        }

        [Fact]
        public async Task RenamingOrDeletingPresetsRequestsReloadButOtherFilesDoNot() {
            using var watcher = new PresetFileWatcher(PresetsDirectory);
            File.WriteAllText(Path.Combine(_directory, "ReeCamera.json"), "{}");
            File.WriteAllText(Path.Combine(PresetsDirectory, "notes.txt"), "notes");
            var nested = Directory.CreateDirectory(Path.Combine(PresetsDirectory, "nested"));
            File.WriteAllText(Path.Combine(nested.FullName, "camera.json"), "{}");
            await AssertNoReload(watcher);

            var preset = Path.Combine(PresetsDirectory, "camera.json");
            File.WriteAllText(preset, "{}");
            await ConsumeOneReload(watcher);

            var renamed = Path.Combine(PresetsDirectory, "renamed.json");
            File.Move(preset, renamed);
            await ConsumeOneReload(watcher);

            var backup = Path.Combine(PresetsDirectory, "renamed.bak");
            File.Move(renamed, backup);
            await ConsumeOneReload(watcher);
            File.Move(backup, renamed);
            await ConsumeOneReload(watcher);

            File.Delete(renamed);
            await ConsumeOneReload(watcher);
        }

        [Fact]
        public async Task StoppingTheWatcherDiscardsPendingSavesAndIgnoresLaterSaves() {
            using var watcher = new PresetFileWatcher(PresetsDirectory);
            var preset = Path.Combine(PresetsDirectory, "camera.json");
            File.WriteAllText(preset, "{}");
            await Task.Delay(100);
            watcher.Dispose();
            await AssertNoReload(watcher);

            File.WriteAllText(preset, "{\"revision\":1}");
            await AssertNoReload(watcher);
        }

        private static async Task ConsumeOneReload(PresetFileWatcher watcher) {
            var timeout = Stopwatch.StartNew();
            while (timeout.ElapsedMilliseconds < 5000) {
                if (watcher.TryConsumeReloadRequest()) {
                    await AssertNoReload(watcher);
                    return;
                }
                await Task.Delay(20);
            }
            Assert.Fail("A saved preset did not request a reload within five seconds.");
        }

        private static async Task AssertNoReload(PresetFileWatcher watcher) {
            var duration = Stopwatch.StartNew();
            while (duration.ElapsedMilliseconds < 450) {
                Assert.False(watcher.TryConsumeReloadRequest());
                await Task.Delay(20);
            }
        }

        public void Dispose() {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }
    }
}