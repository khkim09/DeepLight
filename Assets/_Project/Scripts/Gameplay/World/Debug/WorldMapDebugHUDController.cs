using Project.Data.World;
using Project.Gameplay.World;
using UnityEngine;

namespace Project.Gameplay.World.Debug
{
    /// <summary>월드맵 디버그 HUD의 Controller 계층 (서비스 읽기 → View 업데이트)</summary>
    public class WorldMapDebugHUDController : MonoBehaviour, IDebugHUDServiceReceiver
    {
        [Header("References")]
        [SerializeField] private WorldMapDebugHUDView hudView;
        [SerializeField] private WorldMapRuntimeTest runtimeTestSource;

        [Header("Settings")]
        [SerializeField] private float updateInterval = 0.5f;
        [SerializeField] private bool updateEveryFrame;

        [Header("Test Zone Query")]
        [SerializeField] private string testZoneIdText = "I6";

        [Header("Ambient Reaction (Phase 6)")]
        [SerializeField] private WorldMapAmbientReactionController ambientReactionController; // Ambient 반응 컨트롤러 (옵션)

        // 내부 상태
        private IWorldMapService _service;
        private Transform _trackedTransform;
        private float _lastUpdateTime;
        private bool _lastZoneChanged;
        private bool _hasInitializedService;
        private ZoneId? _lastZoneId; // 이전 프레임의 존 ID (변경 감지용)

        /// <summary>Controller 초기화 (런타임 설치자용)</summary>
        public void Initialize(WorldMapDebugHUDView view, WorldMapRuntimeTest runtimeTest, float interval, bool everyFrame, string testZoneId)
        {
            hudView = view;
            runtimeTestSource = runtimeTest;
            updateInterval = interval;
            updateEveryFrame = everyFrame;
            testZoneIdText = testZoneId;
        }

        /// <summary>HUD가 사용할 IWorldMapService 설정 (런타임 주입용)</summary>
        public void SetService(IWorldMapService service, Transform trackedTransform)
        {
            _service = service;
            _trackedTransform = trackedTransform;
            _hasInitializedService = service != null;
        }

        private void Start()
        {
            // RuntimeTestSource가 설정되어 있으면 자동 연결
            if (runtimeTestSource != null)
            {
                TryConnectFromRuntimeTest();
            }
        }

        private void Update()
        {
            if (!_hasInitializedService || _service == null)
            {
                // 아직 서비스가 없으면 RuntimeTestSource 재시도
                if (runtimeTestSource != null && !_hasInitializedService)
                {
                    TryConnectFromRuntimeTest();
                }

                if (!_hasInitializedService)
                {
                    hudView?.ShowUninitialized();
                    return;
                }
            }

            // 업데이트 주기 체크
            if (!updateEveryFrame)
            {
                if (Time.unscaledTime - _lastUpdateTime < updateInterval)
                    return;
            }
            _lastUpdateTime = Time.unscaledTime;

            RefreshDisplay();
        }

        /// <summary>RuntimeTestSource에서 서비스 연결 시도</summary>
        private void TryConnectFromRuntimeTest()
        {
            if (runtimeTestSource == null) return;

            IWorldMapService service = runtimeTestSource.GetWorldMapService();
            Transform tracked = runtimeTestSource.GetTrackedTransform();

            if (service != null)
            {
                SetService(service, tracked);
                UnityEngine.Debug.Log("[WorldMapDebugHUDController] Connected to WorldMapRuntimeTest service.");
            }
        }

        /// <summary>디스플레이 갱신 (Context Menu에서 수동 호출 가능)</summary>
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

            // 존 상태 조회
            if (!_service.TryGetCurrentZoneState(out ZoneRuntimeState state))
            {
                hudView?.ShowUninitialized();
                return;
            }

            // 존 변경 감지: 이전 존 ID와 비교
            if (_lastZoneId.HasValue)
            {
                _lastZoneChanged = !_lastZoneId.Value.Equals(currentZoneId);
            }
            _lastZoneId = currentZoneId;

            // 테스트 존 조회 — 현재 존의 상태를 표시 (고정 I6 대신 동적)
            string testQueryResult = GetTestZoneQueryResult();

            // Ambient Profile 정보 수집 (Phase 6)
            string ambientProfileId = null;
            string ambientDisplayName = null;
            string bgmStateKey = null;
            bool dangerWarningActive = false;
            float riskOverlayIntensity = 0f;

            if (ambientReactionController != null)
            {
                // AmbientReactionController에서 현재 Ambient 정보 조회
                IWorldAmbientApplier applier = ambientReactionController.GetAmbientApplier();
                if (applier != null)
                {
                    ambientProfileId = applier.GetCurrentProfileId();
                }

                // ZoneData에서 AmbientProfile 직접 읽기 (더 상세한 정보)
                if (state.ZoneData != null)
                {
                    ZoneAmbientProfileSO profile = state.ZoneData.AmbientProfile;
                    if (profile != null)
                    {
                        ambientDisplayName = profile.DisplayName;
                        bgmStateKey = profile.BgmStateKey;
                        dangerWarningActive = profile.ShowDangerWarning;
                        riskOverlayIntensity = profile.RiskOverlayIntensity;
                    }
                }
            }

            // View 업데이트 (Ambient 정보 포함)
            hudView?.ShowCurrentZoneState(
                currentZoneId,
                currentRegionId,
                state,
                trackedPos,
                _lastZoneChanged,
                testQueryResult,
                ambientProfileId,
                ambientDisplayName,
                bgmStateKey,
                dangerWarningActive,
                riskOverlayIntensity);
        }

        /// <summary>현재 존의 상세 상태 문자열 생성 (GetDebugStateString)</summary>
        private string GetTestZoneQueryResult()
        {
            if (_service == null || !_service.HasCurrentZone)
                return null;

            // 현재 존의 상세 상태를 표시
            if (_service.TryGetCurrentZoneState(out ZoneRuntimeState currentState))
            {
                return currentState.GetDebugStateString();
            }

            return null;
        }

        /// <summary>존 변경 플래그 설정 (WorldMapRuntimeTest에서 호출)</summary>
        public void NotifyZoneChanged(bool changed)
        {
            _lastZoneChanged = changed;
        }

        /// <summary>RuntimeTestSource 강제 재연결</summary>
        [ContextMenu("Reconnect From RuntimeTest")]
        private void ReconnectFromRuntimeTest()
        {
            _hasInitializedService = false;
            _service = null;
            _trackedTransform = null;

            if (runtimeTestSource != null)
            {
                TryConnectFromRuntimeTest();
            }

            if (!_hasInitializedService)
            {
                UnityEngine.Debug.LogWarning("[WorldMapDebugHUDController] Failed to reconnect. Is WorldMapRuntimeTest initialized?");
            }
        }
    }
}
