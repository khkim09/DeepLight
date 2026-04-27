using UnityEngine;

namespace Project.Gameplay.World
{
    /// <summary>
    /// 디버그 HUD가 WorldMapRuntimeInstaller로부터 서비스를 주입받기 위한 인터페이스.
    /// 리플렉션 기반 주입을 대체하여 타입 안전성을 확보한다.
    ///
    /// WorldMapDebugHUDController가 이 인터페이스를 구현한다.
    /// WorldMapRuntimeInstaller는 이 인터페이스를 통해 직접 주입한다.
    /// </summary>
    public interface IDebugHUDServiceReceiver
    {
        /// <summary>Installer로부터 IWorldMapService와 추적 Transform을 주입받는다.</summary>
        /// <param name="service">공유 IWorldMapService 인스턴스</param>
        /// <param name="trackedTransform">현재 추적 대상 Transform</param>
        void SetService(IWorldMapService service, Transform trackedTransform);
    }
}
