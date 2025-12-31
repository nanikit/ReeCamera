using JetBrains.Annotations;
using ReeCamera.Spout;
using Zenject;

namespace ReeCamera {
    [UsedImplicitly]
    public class OnAppInstaller : Installer<OnAppInstaller> {
        public override void InstallBindings() {
            Container.Bind<PluginStateManager>().FromNewComponentOnNewGameObject().AsSingle().NonLazy();
            Container.Bind<InteropCamerasManager>().FromNewComponentOnNewGameObject().AsSingle().NonLazy();
            Container.Bind<SpoutSenderManager>().FromNewComponentOnNewGameObject().AsSingle().NonLazy();
        }
    }
}