using System.Collections.Generic;
using Project.Core.Events;
using Project.Data.World;
using UnityEngine;

namespace Project.Gameplay.World
{
    /// <summary>
    /// 월드맵 존 기반 콘텐츠 활성화 컨트롤러 (Phase 10).
    ///
    /// 책임:
    /// 1. 씬 내 WorldMapZoneContentBinding 컴포넌트를 수집/관리
    /// 2. ZoneChangedEvent / CurrentZoneClearedEvent / CurrentZoneStateChangedEvent 구독
    /// 3. 각 바인딩의 규칙에 따라 활성화/비활성화 결정
    /// 4. 중복 SetActive 호출 방지 (Binding.ApplyActiveState 경유)
    /// 5. 수동 Refresh / ValidateBindings 컨텍스트 메뉴 지원
    ///
    /// [설계 의도]
    /// - WorldMapAmbientReactionController와 유사한 EventBus 구독 패턴 사용
    /// - IWorldMapService에 의존하여 존 상태 질의
    /// - WorldMapConfigSO에 의존하여 이웃 계산 (ZoneNeighborHelper 경유)
    /// - WorldMapRuntimeInstaller가 Initialize()를 호출하여 서비스 주입
    /// - FindObjectsOfType<WorldMapZoneContentBinding>()로 바인딩 자동 수집
    /// </summary>
    public class WorldMapZoneContentController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WorldMapConfigSO worldMapConfig; // 월드맵 설정 (이웃 계산용)

        [Header("Settings")]
        [SerializeField] private bool disableAllWhenNoCurrentZone = true; // 현재 존이 없으면 모든 바인딩 비활성화
        [SerializeField] private bool debugLogging = true; // 활성화 상태 변경 시 로그 출력

        // ===== 내부 상태 =====

        /// <summary>월드맵 서비스 (런타임 주입 또는 자동 탐색)</summary>
        private IWorldMapService _worldMapService;

        /// <summary>수집된 모든 존 콘텐츠 바인딩</summary>
        private List<WorldMapZoneContentBinding> _bindings = new();

        /// <summary>초기화 완료 여부</summary>
        private bool _isInitialized;

        /// <summary>현재 존 ID 캐시 (중복 평가 방지)</summary>
        private ZoneId _lastEvaluatedCurrentZoneId;

        /// <summary>현재 존 유효 여부 캐시</summary>
        private bool _lastHadCurrentZone;

        /// <summary>컨트롤러 초기화 (런타임 주입용)</summary>
        public void Initialize(IWorldMapService service, WorldMapConfigSO config)
        {
            _worldMapService = service;
            worldMapConfig = config;

            // 씬 내 모든 바인딩 자동 수집
            RefreshBindings();

            _isInitialized = true;

            if (debugLogging)
            {
                UnityEngine.Debug.Log($"[ZoneContentController] Initialized with {_bindings.Count} bindings.");
            }

            // 서비스가 이미 초기화되어 있으면 즉시 평가
            if (_worldMapService != null && _worldMapService.HasCurrentZone)
            {
                EvaluateAllBindings();
            }
            else if (disableAllWhenNoCurrentZone)
            {
                // 현재 존이 없으면 모든 바인딩 비활성화
                DeactivateAllBindings("No current zone on initialize");
            }
        }

        private void OnEnable()
        {
            // EventBus 구독
            EventBus.Subscribe<ZoneChangedEvent>(OnZoneChanged);
            EventBus.Subscribe<CurrentZoneClearedEvent>(OnZoneCleared);
            EventBus.Subscribe<CurrentZoneStateChangedEvent>(OnZoneStateChanged);
        }

        private void OnDisable()
        {
            // EventBus 구독 해제
            EventBus.Unsubscribe<ZoneChangedEvent>(OnZoneChanged);
            EventBus.Unsubscribe<CurrentZoneClearedEvent>(OnZoneCleared);
            EventBus.Unsubscribe<CurrentZoneStateChangedEvent>(OnZoneStateChanged);
        }

        private void Start()
        {
            // Initialize()가 호출되지 않았으면 자동 초기화 시도
            if (!_isInitialized)
            {
                // config가 인스펙터에서 할당되었는지 확인
                if (worldMapConfig != null)
                {
                    // WorldMapRuntimeInstaller를 찾아서 서비스 획득 시도
                    var installer = FindObjectOfType<WorldMapRuntimeInstaller>();
                    if (installer != null && installer.WorldMapService != null)
                    {
                        Initialize(installer.WorldMapService, worldMapConfig);
                    }
                    else
                    {
                        UnityEngine.Debug.LogWarning("[ZoneContentController] WorldMapRuntimeInstaller not found or not initialized yet. " +
                            "Call Initialize() manually or ensure installer exists in the scene.");
                    }
                }
                else
                {
                    UnityEngine.Debug.LogWarning("[ZoneContentController] WorldMapConfigSO not assigned. " +
                        "Assign in Inspector or call Initialize() with config.");
                }
            }
        }

        // ===== EventBus 핸들러 =====

        /// <summary>ZoneChangedEvent 핸들러 — 존이 변경되면 모든 바인딩 재평가</summary>
        private void OnZoneChanged(ZoneChangedEvent evt)
        {
            if (!_isInitialized || _worldMapService == null)
                return;

            if (debugLogging)
            {
                UnityEngine.Debug.Log($"[ZoneContentController] Zone changed: '{evt.PreviousZoneId}' -> '{evt.CurrentZoneId}'. Re-evaluating bindings.");
            }

            EvaluateAllBindings();
        }

        /// <summary>CurrentZoneClearedEvent 핸들러 — 경계 밖이면 모든 바인딩 비활성화</summary>
        private void OnZoneCleared(CurrentZoneClearedEvent evt)
        {
            if (!_isInitialized)
                return;

            if (debugLogging)
            {
                UnityEngine.Debug.Log($"[ZoneContentController] Zone cleared: '{evt.LastZoneId}'. Reason: {evt.Reason}");
            }

            if (disableAllWhenNoCurrentZone)
            {
                DeactivateAllBindings($"Zone cleared: {evt.Reason}");
            }
        }

        /// <summary>CurrentZoneStateChangedEvent 핸들러 — 존 상태가 변경되면 재평가</summary>
        private void OnZoneStateChanged(CurrentZoneStateChangedEvent evt)
        {
            if (!_isInitialized || _worldMapService == null)
                return;

            // 발견/해금 상태가 변경되었을 때만 재평가 (위험도 변경은 콘텐츠 활성화에 영향 없음)
            if (evt.PreviousIsDiscovered != evt.CurrentIsDiscovered ||
                evt.PreviousAccessibility != evt.CurrentAccessibility)
            {
                if (debugLogging)
                {
                    UnityEngine.Debug.Log($"[ZoneContentController] Zone state changed: '{evt.ZoneId}'. " +
                        $"Discovered: {evt.PreviousIsDiscovered}->{evt.CurrentIsDiscovered}, " +
                        $"Accessibility: {evt.PreviousAccessibility}->{evt.CurrentAccessibility}. Re-evaluating.");
                }

                EvaluateAllBindings();
            }
        }

        // ===== 바인딩 평가 =====

        /// <summary>모든 바인딩을 현재 존 상태에 따라 재평가</summary>
        private void EvaluateAllBindings()
        {
            if (_worldMapService == null || worldMapConfig == null)
                return;

            bool hasCurrentZone = _worldMapService.HasCurrentZone;
            ZoneId currentZoneId = _worldMapService.CurrentZoneId;

            // 캐시 업데이트
            _lastHadCurrentZone = hasCurrentZone;
            _lastEvaluatedCurrentZoneId = currentZoneId;

            int activatedCount = 0;
            int deactivatedCount = 0;
            int skippedCount = 0;

            foreach (var binding in _bindings)
            {
                if (binding == null)
                {
                    skippedCount++;
                    continue;
                }

                bool shouldBeActive = EvaluateBinding(binding, hasCurrentZone, currentZoneId);

                // 중복 SetActive 방지
                if (binding.ApplyActiveState(shouldBeActive))
                {
                    if (shouldBeActive)
                        activatedCount++;
                    else
                        deactivatedCount++;

                    if (debugLogging)
                    {
                        UnityEngine.Debug.Log($"[ZoneContentController] Binding '{binding.name}' ({binding.PrimaryZoneId}) " +
                            $"{(shouldBeActive ? "ACTIVATED" : "DEACTIVATED")}. " +
                            $"Category: {binding.Category}, Label: {binding.DebugLabel}");
                    }
                }
            }

            if (debugLogging && (activatedCount > 0 || deactivatedCount > 0))
            {
                UnityEngine.Debug.Log($"[ZoneContentController] Evaluation complete: {activatedCount} activated, {deactivatedCount} deactivated, {skippedCount} skipped.");
            }
        }

        /// <summary>
        /// 단일 바인딩의 활성화 여부를 결정한다.
        /// 규칙 우선순위:
        ///   1. 현재 존 없음 → disableAllWhenNoCurrentZone에 따라 처리
        ///   2. activeOnlyWhenCurrentZone → 현재 존이 primaryZoneId와 일치해야 활성화
        ///   3. activeWhenCurrentOrNeighbor → 현재 존이 primaryZoneId 또는 8-이웃이어야 활성화
        ///   4. requireZoneDiscovered → 대상 존이 발견되어야 활성화
        ///   5. requireZoneUnlocked → 대상 존이 해금되어야 활성화
        /// </summary>
        private bool EvaluateBinding(WorldMapZoneContentBinding binding, bool hasCurrentZone, ZoneId currentZoneId)
        {
            ZoneId targetZoneId = binding.PrimaryZoneId;

            // ===== 규칙 1: 현재 존 없음 =====
            if (!hasCurrentZone)
            {
                return !disableAllWhenNoCurrentZone;
            }

            // ===== 규칙 2: 현재 존 일치 확인 =====
            bool isCurrentZone = currentZoneId.Equals(targetZoneId);

            // ===== 규칙 3: 이웃 존 확인 =====
            bool isNeighborZone = false;
            if (binding.ActiveWhenCurrentOrNeighbor && !isCurrentZone)
            {
                isNeighborZone = ZoneNeighborHelper.IsNeighborOf(targetZoneId, currentZoneId, worldMapConfig);
            }

            // ===== 위치 조건 평가 =====
            bool locationMatch = false;

            if (binding.ActiveOnlyWhenCurrentZone)
            {
                // 현재 존일 때만 활성화
                locationMatch = isCurrentZone;
            }
            else if (binding.ActiveWhenCurrentOrNeighbor)
            {
                // 현재 존 또는 이웃 존일 때 활성화
                locationMatch = isCurrentZone || isNeighborZone;
            }
            else
            {
                // 위치 조건 없음 → 항상 위치는 통과
                locationMatch = true;
            }

            // 위치 조건을 통과하지 못하면 즉시 비활성화
            if (!locationMatch)
                return false;

            // ===== 규칙 4: 발견 조건 =====
            if (binding.RequireZoneDiscovered)
            {
                ZoneRuntimeState targetState = _worldMapService.GetZoneStateOrEvaluate(targetZoneId);
                if (!targetState.IsDiscovered)
                    return false;
            }

            // ===== 규칙 5: 해금 조건 =====
            if (binding.RequireZoneUnlocked)
            {
                ZoneRuntimeState targetState = _worldMapService.GetZoneStateOrEvaluate(targetZoneId);
                if (!targetState.IsUnlocked)
                    return false;
            }

            // 모든 조건 통과 → 활성화
            return true;
        }

        /// <summary>모든 바인딩 비활성화 (존 클리어 등)</summary>
        private void DeactivateAllBindings(string reason)
        {
            int deactivatedCount = 0;

            foreach (var binding in _bindings)
            {
                if (binding == null)
                    continue;

                if (binding.ApplyActiveState(false))
                {
                    deactivatedCount++;

                    if (debugLogging)
                    {
                        UnityEngine.Debug.Log($"[ZoneContentController] Binding '{binding.name}' DEACTIVATED. Reason: {reason}");
                    }
                }
            }

            if (debugLogging && deactivatedCount > 0)
            {
                UnityEngine.Debug.Log($"[ZoneContentController] All bindings deactivated: {deactivatedCount} total. Reason: {reason}");
            }
        }

        // ===== 바인딩 관리 =====

        /// <summary>씬 내 모든 WorldMapZoneContentBinding 컴포넌트를 수집한다.</summary>
        public void RefreshBindings()
        {
            // 기존 리스트 초기화
            _bindings.Clear();

            // 씬 내 모든 바인딩 찾기
            WorldMapZoneContentBinding[] foundBindings = FindObjectsOfType<WorldMapZoneContentBinding>(includeInactive: true);
            _bindings.AddRange(foundBindings);

            if (debugLogging)
            {
                UnityEngine.Debug.Log($"[ZoneContentController] Refreshed bindings: found {_bindings.Count} in scene.");
            }
        }

        /// <summary>모든 바인딩의 유효성을 검사하고 로그로 출력한다.</summary>
        public void ValidateBindings()
        {
            if (_bindings.Count == 0)
            {
                UnityEngine.Debug.LogWarning("[ZoneContentController] No bindings registered. Call RefreshBindings() first or ensure WorldMapZoneContentBinding components exist in the scene.");
                return;
            }

            int validCount = 0;
            int invalidCount = 0;

            UnityEngine.Debug.Log($"[ZoneContentController] === Binding Validation ({_bindings.Count} total) ===");

            foreach (var binding in _bindings)
            {
                if (binding == null)
                {
                    invalidCount++;
                    UnityEngine.Debug.LogWarning("[ZoneContentController]   [NULL] A binding reference is null.");
                    continue;
                }

                // 유효성 검사
                bool isValid = true;
                List<string> issues = new();

                // ZoneId가 기본값인지 확인 (Column=null, Row=0)
                if (binding.PrimaryZoneId.Column == default || binding.PrimaryZoneId.Row == 0)
                {
                    isValid = false;
                    issues.Add("PrimaryZoneId is not set");
                }

                // 상호 배타적 규칙 확인
                if (binding.ActiveOnlyWhenCurrentZone && binding.ActiveWhenCurrentOrNeighbor)
                {
                    isValid = false;
                    issues.Add("Both ActiveOnlyWhenCurrentZone and ActiveWhenCurrentOrNeighbor are set. ActiveOnlyWhenCurrentZone takes priority.");
                }

                string status = isValid ? "OK" : "ISSUES";
                string issueStr = issues.Count > 0 ? $" | Issues: {string.Join("; ", issues)}" : "";

                UnityEngine.Debug.Log($"[ZoneContentController]   [{status}] '{binding.name}' -> Zone={binding.PrimaryZoneId}, " +
                    $"Category={binding.Category}, Label={binding.DebugLabel}{issueStr}");

                if (isValid)
                    validCount++;
                else
                    invalidCount++;
            }

            UnityEngine.Debug.Log($"[ZoneContentController] === Validation complete: {validCount} valid, {invalidCount} invalid ===");
        }

        // ===== 컨텍스트 메뉴 =====

        /// <summary>에디터에서 바인딩 새로고침 및 재평가</summary>
        [ContextMenu("Refresh Content Activation")]
        private void ContextRefreshContentActivation()
        {
            if (!Application.isPlaying)
            {
                UnityEngine.Debug.Log("[ZoneContentController] Refresh is only available in Play Mode.");
                return;
            }

            RefreshBindings();
            EvaluateAllBindings();

            UnityEngine.Debug.Log("[ZoneContentController] Content activation refreshed manually.");
        }

        /// <summary>에디터에서 바인딩 유효성 검사</summary>
        [ContextMenu("Validate Bindings")]
        private void ContextValidateBindings()
        {
            if (!Application.isPlaying)
            {
                // 에디터 모드에서도 바인딩 수집은 가능
                RefreshBindings();
            }

            ValidateBindings();
        }

        /// <summary>등록된 바인딩 개수 반환 (디버그용)</summary>
        public int BindingCount => _bindings.Count;

        /// <summary>초기화 여부 반환 (디버그용)</summary>
        public bool IsInitialized => _isInitialized;
    }
}
