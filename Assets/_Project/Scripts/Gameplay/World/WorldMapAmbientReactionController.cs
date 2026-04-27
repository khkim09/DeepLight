using Project.Core.Events;
using Project.Data.World;
using UnityEngine;

namespace Project.Gameplay.World
{
    /// <summary>
    /// 월드맵 존 변경 이벤트를 수신하여 존 기반 분위기(Ambient) 반응을 조정하는 컨트롤러.
    ///
    /// 역할:
    /// 1. ZoneChangedEvent / CurrentZoneClearedEvent 구독
    /// 2. 현재 존의 ZoneDataSO에서 ZoneAmbientProfileSO를 resolve
    /// 3. resolve된 프로필을 IWorldAmbientApplier에 전달하여 실제 씬에 적용
    /// 4. Out-of-bounds 시 fallback 프로필 적용
    /// 5. 중복 적용 방지 (같은 프로필이면 스킵)
    ///
    /// 설계 의도:
    /// - IWorldAmbientApplier를 통해 실제 적용 로직과 분리되어 있음
    /// - Applier가 null이어도 안전하게 동작 (아무 일도 일어나지 않음)
    /// - 기존 메인 게임플레이 HUD / UIRoot에 의존하지 않음
    /// - WorldMapRuntimeTest와 독립적으로 동작 가능 (서비스만 있으면 OK)
    /// </summary>
    public class WorldMapAmbientReactionController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private IWorldMapService worldMapService; // 월드맵 서비스 (존 데이터 resolve용)
        [SerializeField] private IWorldAmbientApplier ambientApplier; // 실제 분위기 적용자

        [Header("Fallback Profile")]
        [SerializeField] private ZoneAmbientProfileSO outOfBoundsProfile; // 경계 밖 fallback 프로필 (null이면 중립 기본값)

        // 내부 상태
        private bool _isInitialized;
        private string _lastAppliedProfileId = ""; // 마지막으로 적용한 프로필 ID (중복 적용 방지)

        /// <summary>컨트롤러 초기화 (런타임 주입용)</summary>
        public void Initialize(IWorldMapService service, IWorldAmbientApplier applier, ZoneAmbientProfileSO fallbackProfile = null)
        {
            worldMapService = service;
            ambientApplier = applier;
            outOfBoundsProfile = fallbackProfile;
        }

        private void OnEnable()
        {
            // EventBus 구독
            EventBus.Subscribe<ZoneChangedEvent>(OnZoneChanged);
            EventBus.Subscribe<CurrentZoneClearedEvent>(OnZoneCleared);
        }

        private void OnDisable()
        {
            // EventBus 구독 해제
            EventBus.Unsubscribe<ZoneChangedEvent>(OnZoneChanged);
            EventBus.Unsubscribe<CurrentZoneClearedEvent>(OnZoneCleared);
        }

        private void Start()
        {
            // Applier 초기화
            if (ambientApplier != null)
            {
                ambientApplier.Initialize();
            }

            // 서비스가 이미 초기화되어 있으면 현재 존 기준으로 즉시 적용
            if (worldMapService != null && worldMapService.HasCurrentZone)
            {
                ApplyCurrentZoneAmbient();
            }

            _isInitialized = true;
        }

        /// <summary>ZoneChangedEvent 핸들러 — 존이 변경되면 해당 존의 AmbientProfile 적용</summary>
        private void OnZoneChanged(ZoneChangedEvent evt)
        {
            if (!_isInitialized || ambientApplier == null || worldMapService == null)
                return;

            // 현재 존의 ZoneData에서 AmbientProfile resolve
            ApplyCurrentZoneAmbient();
        }

        /// <summary>CurrentZoneClearedEvent 핸들러 — 경계 밖이면 fallback 적용</summary>
        private void OnZoneCleared(CurrentZoneClearedEvent evt)
        {
            if (!_isInitialized || ambientApplier == null)
                return;

            // Out-of-bounds fallback 적용
            if (outOfBoundsProfile != null && outOfBoundsProfile.IsValid())
            {
                ambientApplier.ApplyProfile(outOfBoundsProfile);
            }
            else
            {
                // fallback 프로필이 없으면 Applier의 기본 fallback 사용
                ambientApplier.ApplyOutOfBoundsFallback();
            }

            _lastAppliedProfileId = ambientApplier.GetCurrentProfileId();
        }

        /// <summary>현재 존의 AmbientProfile을 resolve하여 Applier에 전달</summary>
        private void ApplyCurrentZoneAmbient()
        {
            if (worldMapService == null || !worldMapService.HasCurrentZone)
                return;

            // 현재 존의 ZoneRuntimeState를 통해 ZoneDataSO 획득
            if (!worldMapService.TryGetCurrentZoneState(out ZoneRuntimeState currentState))
                return;

            ZoneDataSO zoneData = currentState.ZoneData;
            if (zoneData == null)
                return;

            // ZoneDataSO에서 AmbientProfile 획득
            ZoneAmbientProfileSO profile = zoneData.AmbientProfile;

            if (profile != null && profile.IsValid())
            {
                // 유효한 프로필이면 적용
                ambientApplier.ApplyProfile(profile);
            }
            else
            {
                // 프로필이 없으면 fallback
                if (outOfBoundsProfile != null && outOfBoundsProfile.IsValid())
                {
                    ambientApplier.ApplyProfile(outOfBoundsProfile);
                }
                else
                {
                    ambientApplier.ApplyOutOfBoundsFallback();
                }
            }

            _lastAppliedProfileId = ambientApplier.GetCurrentProfileId();
        }

        /// <summary>현재 적용된 AmbientProfile ID 반환 (디버그 HUD 연동용)</summary>
        public string GetCurrentAmbientProfileId()
        {
            return _lastAppliedProfileId;
        }

        /// <summary>Applier 참조 반환 (디버그 HUD 연동용)</summary>
        public IWorldAmbientApplier GetAmbientApplier()
        {
            return ambientApplier;
        }
    }
}
