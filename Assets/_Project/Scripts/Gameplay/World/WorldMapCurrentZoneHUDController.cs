using Project.Core.Events;
using Project.Data.World;
using UnityEngine;

namespace Project.Gameplay.World
{
    /// <summary>
    /// 플레이어용 현재 존 HUD Controller.
    /// IWorldMapService를 주입받아 ZoneChangedEvent를 구독하고 View를 업데이트한다.
    ///
    /// 디버그 HUD와 독립적이며, Installer가 직접 주입한다.
    /// EventBus 기반 업데이트로 폴링 최소화.
    ///
    /// Phase 9: 사용자용 최소 HUD (full map 아님)
    /// </summary>
    public class WorldMapCurrentZoneHUDController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WorldMapCurrentZoneHUDView hudView;

        [Header("Settings")]
        [SerializeField] private float updateInterval = 0.5f; // 폴링 fallback 간격
        [SerializeField] private bool updateEveryFrame;       // 매 프레임 강제 업데이트

        // 내부 상태
        private IWorldMapService _service;
        private Transform _trackedTransform;
        private float _lastUpdateTime;
        private bool _hasInitializedService;

        /// <summary>Controller 초기화 (Installer가 호출)</summary>
        /// <param name="service">공유 IWorldMapService 인스턴스</param>
        /// <param name="trackedTransform">추적 대상 Transform</param>
        public void Initialize(IWorldMapService service, Transform trackedTransform)
        {
            _service = service;
            _trackedTransform = trackedTransform;
            _hasInitializedService = service != null;

            if (hudView != null)
            {
                hudView.Initialize();
            }

            if (_hasInitializedService)
            {
                // EventBus 구독: 존 변경 시 즉시 업데이트
                EventBus.Subscribe<ZoneChangedEvent>(OnZoneChanged);
                EventBus.Subscribe<CurrentZoneStateChangedEvent>(OnZoneStateChanged);
                EventBus.Subscribe<CurrentZoneClearedEvent>(OnZoneCleared);

                UnityEngine.Debug.Log("[WorldMapCurrentZoneHUDController] Initialized with shared service. Subscribed to zone events.");
            }
        }

        private void OnDestroy()
        {
            // EventBus 구독 해제
            if (_hasInitializedService)
            {
                EventBus.Unsubscribe<ZoneChangedEvent>(OnZoneChanged);
                EventBus.Unsubscribe<CurrentZoneStateChangedEvent>(OnZoneStateChanged);
                EventBus.Unsubscribe<CurrentZoneClearedEvent>(OnZoneCleared);
            }
        }

        private void Update()
        {
            if (!_hasInitializedService || _service == null)
            {
                hudView?.ShowUninitialized();
                return;
            }

            // 매 프레임 업데이트 모드
            if (updateEveryFrame)
            {
                RefreshDisplay();
                return;
            }

            // 주기적 폴링 (EventBus fallback)
            if (Time.unscaledTime - _lastUpdateTime >= updateInterval)
            {
                _lastUpdateTime = Time.unscaledTime;
                RefreshDisplay();
            }
        }

        /// <summary>ZoneChangedEvent 핸들러 (존 변경 시 즉시 업데이트)</summary>
        private void OnZoneChanged(ZoneChangedEvent evt)
        {
            RefreshDisplay();
        }

        /// <summary>CurrentZoneStateChangedEvent 핸들러 (같은 존 내 상태 변화)</summary>
        private void OnZoneStateChanged(CurrentZoneStateChangedEvent evt)
        {
            RefreshDisplay();
        }

        /// <summary>CurrentZoneClearedEvent 핸들러 (존 이탈)</summary>
        private void OnZoneCleared(CurrentZoneClearedEvent evt)
        {
            Vector3 pos = _trackedTransform != null ? _trackedTransform.position : Vector3.zero;
            hudView?.ShowOutOfBounds(pos);
        }

        /// <summary>디스플레이 갱신</summary>
        [ContextMenu("Refresh Display")]
        public void RefreshDisplay()
        {
            if (!_hasInitializedService || _service == null)
            {
                hudView?.ShowUninitialized();
                return;
            }

            if (!_service.HasCurrentZone)
            {
                Vector3 pos = _trackedTransform != null ? _trackedTransform.position : Vector3.zero;
                hudView?.ShowOutOfBounds(pos);
                return;
            }

            // 현재 존 상태 읽기
            ZoneId currentZoneId = _service.CurrentZoneId;
            RegionId currentRegionId = _service.CurrentRegionId;
            Vector3 trackedPos = _trackedTransform != null ? _trackedTransform.position : Vector3.zero;

            if (!_service.TryGetCurrentZoneState(out ZoneRuntimeState state))
            {
                hudView?.ShowUninitialized();
                return;
            }

            // View 업데이트
            hudView?.ShowCurrentZone(currentZoneId, currentRegionId, state, trackedPos);
        }
    }
}
