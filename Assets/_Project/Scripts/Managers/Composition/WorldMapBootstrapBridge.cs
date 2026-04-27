using Project.Gameplay.World;
using UnityEngine;

namespace Project.Managers.Composition
{
    /// <summary>
    /// DeepLightTestBedBootstrapper와 WorldMapRuntimeInstaller 사이의 선택적 연결 브릿지.
    ///
    /// 책임:
    /// - TestBedBootstrapper의 playerTransform을 WorldMapRuntimeInstaller에 전달
    /// - Installer가 Gameplay 모드일 때 SetGameplayTarget() 호출
    /// - Installer가 Debug 모드일 경우 아무것도 하지 않음 (디버그 경로 유지)
    ///
    /// 사용법:
    /// 1. DeepLightTestBedBootstrapper가 있는 GameObject에 이 컴포넌트를 추가
    /// 2. 인스펙터에서 worldMapInstaller에 WorldMapRuntimeInstaller 참조 할당
    /// 3. Bootstrapper의 Awake() 또는 Start()에서 bridge.Initialize(playerTransform) 호출
    ///
    /// 또는 Bootstrapper가 직접 WorldMapRuntimeInstaller.SetGameplayTarget()을 호출해도 됨.
    /// 이 브릿지는 명시적인 hook point 예시를 제공하기 위한 것.
    /// </summary>
    public class WorldMapBootstrapBridge : MonoBehaviour
    {
        [Header("World Map Installer")]
        [SerializeField] private WorldMapRuntimeInstaller worldMapInstaller;

        /// <summary>Installer 참조 설정 (코드에서 할당)</summary>
        public void SetInstaller(WorldMapRuntimeInstaller installer)
        {
            worldMapInstaller = installer;
        }

        /// <summary>
        /// 플레이어 Transform을 WorldMapRuntimeInstaller에 전달.
        /// Bootstrapper의 Start()에서 호출할 것.
        /// </summary>
        /// <param name="playerTransform">플레이어/잠수함 Transform</param>
        public void Initialize(Transform playerTransform)
        {
            if (worldMapInstaller == null)
            {
                // Installer를 찾을 수 없으면 씬에서 검색
                worldMapInstaller = FindFirstObjectByType<WorldMapRuntimeInstaller>();
                if (worldMapInstaller == null)
                {
                    UnityEngine.Debug.LogWarning("[WorldMapBootstrapBridge] No WorldMapRuntimeInstaller found in scene. World Map will not be initialized.");
                    return;
                }
            }

            if (playerTransform == null)
            {
                UnityEngine.Debug.LogWarning("[WorldMapBootstrapBridge] playerTransform is null. World Map target will not be set.");
                return;
            }

            // Installer에 gameplay target 전달
            worldMapInstaller.SetGameplayTarget(playerTransform);

            UnityEngine.Debug.Log($"[WorldMapBootstrapBridge] WorldMap target set to: {playerTransform.name} (Installer mode: {worldMapInstaller.Mode})");
        }
    }
}
