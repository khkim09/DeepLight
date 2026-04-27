using Project.Data.World;
using UnityEngine;

namespace Project.Gameplay.World
{
    /// <summary>
    /// 월드맵 시스템 간단 런타임 테스트.
    /// 씬 재생 후 콘솔에서 결과를 확인할 수 있습니다.
    ///
    /// 사용법:
    /// 1. Tools > World Map > Quick Setup All 실행 (Phase 1-7)
    /// 2. WorldMap_RuntimeRoot 아래 WorldMapRuntimeInstaller 오브젝트 선택
    /// 3. 인스펙터에서 TrackedTransform = Submarine (Player 태그) 확인
    /// 4. 씬 재생
    /// 5. F9 키로 Debug HUD 토글
    /// 6. Submarine을 움직여 Zone 변경 확인
    /// </summary>
    public class WorldMapQuickTest : MonoBehaviour
    {
        [Header("Test Configuration")]
        [SerializeField] private WorldMapConfigSO worldMapConfig;
        [SerializeField] private Transform trackedTransform;

        [Header("Test Zone Coordinates")]
        [SerializeField] private Vector3 hubPosition = new Vector3(-1400, 0, -1400);   // E5
        [SerializeField] private Vector3 researchPosition = new Vector3(-600, 0, -600); // I6
        [SerializeField] private Vector3 sealedPosition = new Vector3(600, 0, 1400);    // F10
        [SerializeField] private Vector3 outOfBoundsPosition = new Vector3(5000, 0, 5000); // 경계 밖

        private IZoneResolver _zoneResolver;
        private IWorldMapService _worldMapService;

        private void Start()
        {
            if (worldMapConfig == null)
            {
                UnityEngine.Debug.LogError("[WorldMapQuickTest] WorldMapConfig is not assigned.");
                return;
            }

            // 서비스 그래프 생성
            _zoneResolver = new ZoneResolver(worldMapConfig);
            var zoneRepository = new ZoneRepository(worldMapConfig, null);
            var conditionEvaluator = new UnlockConditionEvaluator();
            var zoneStateEvaluator = new ZoneStateEvaluator(conditionEvaluator);
            var mockProgress = new MockWorldProgressQuery();
            mockProgress.AddUpgrade("upgrade_battery_mk2");
            mockProgress.AddLog("log_intro_01");
            mockProgress.AddNarrativeFlag("mara_research_analysis_done");

            _worldMapService = new WorldMapService(
                _zoneResolver,
                zoneRepository,
                zoneStateEvaluator,
                mockProgress);

            // 테스트 실행
            RunAllTests();
        }

        /// <summary>모든 테스트 실행</summary>
        [ContextMenu("Run All Tests")]
        public void RunAllTests()
        {
            UnityEngine.Debug.Log("========================================");
            UnityEngine.Debug.Log("  WorldMap Quick Test Suite");
            UnityEngine.Debug.Log("========================================");

            TestZoneResolution();
            TestZoneChangeDetection();
            TestOutOfBounds();
            TestZoneStateEvaluation();

            UnityEngine.Debug.Log("========================================");
            UnityEngine.Debug.Log("  All tests completed. Check results above.");
            UnityEngine.Debug.Log("========================================");
        }

        /// <summary>존 해석 테스트</summary>
        [ContextMenu("Test Zone Resolution")]
        public void TestZoneResolution()
        {
            UnityEngine.Debug.Log("--- [Test] Zone Resolution ---");

            TestResolvePosition(hubPosition, "Hub (E5)");
            TestResolvePosition(researchPosition, "Research (I6)");
            TestResolvePosition(sealedPosition, "Sealed (F10)");

            UnityEngine.Debug.Log("--- [Test] Zone Resolution Complete ---");
        }

        private void TestResolvePosition(Vector3 position, string label)
        {
            if (_zoneResolver.TryGetZoneIdFromWorldPosition(position, out ZoneId zoneId))
            {
                UnityEngine.Debug.Log($"  [{label}] Pos ({position.x:F0}, {position.z:F0}) → Zone: {zoneId}");
            }
            else
            {
                UnityEngine.Debug.LogWarning($"  [{label}] Pos ({position.x:F0}, {position.z:F0}) → FAILED to resolve");
            }
        }

        /// <summary>존 변경 감지 테스트</summary>
        [ContextMenu("Test Zone Change Detection")]
        public void TestZoneChangeDetection()
        {
            UnityEngine.Debug.Log("--- [Test] Zone Change Detection ---");

            // 초기화
            _worldMapService.Initialize(hubPosition);
            UnityEngine.Debug.Log($"  Initialized at Hub (E5): {_worldMapService.CurrentZoneId}");

            // 같은 존 → false
            bool sameZone = _worldMapService.RefreshCurrentZone(hubPosition);
            UnityEngine.Debug.Log($"  Same position (Hub): zoneChanged={sameZone} (expected: false)");

            // 다른 존 → true
            bool changedZone = _worldMapService.RefreshCurrentZone(researchPosition);
            UnityEngine.Debug.Log($"  Moved to Research (I6): zoneChanged={changedZone} (expected: true)");
            UnityEngine.Debug.Log($"  Current zone: {_worldMapService.CurrentZoneId}");

            // 다시 다른 존 → true
            bool changedZone2 = _worldMapService.RefreshCurrentZone(sealedPosition);
            UnityEngine.Debug.Log($"  Moved to Sealed (F10): zoneChanged={changedZone2} (expected: true)");
            UnityEngine.Debug.Log($"  Current zone: {_worldMapService.CurrentZoneId}");

            UnityEngine.Debug.Log("--- [Test] Zone Change Detection Complete ---");
        }

        /// <summary>경계 밖 테스트</summary>
        [ContextMenu("Test Out Of Bounds")]
        public void TestOutOfBounds()
        {
            UnityEngine.Debug.Log("--- [Test] Out of Bounds ---");

            _worldMapService.Initialize(hubPosition);
            UnityEngine.Debug.Log($"  Initialized at Hub: {_worldMapService.CurrentZoneId}");

            bool oobResult = _worldMapService.RefreshCurrentZone(outOfBoundsPosition);
            UnityEngine.Debug.Log($"  Moved to OOB position: zoneChanged={oobResult} (expected: false)");
            UnityEngine.Debug.Log($"  HasCurrentZone: {_worldMapService.HasCurrentZone} (expected: false)");

            UnityEngine.Debug.Log("--- [Test] Out of Bounds Complete ---");
        }

        /// <summary>존 상태 평가 테스트</summary>
        [ContextMenu("Test Zone State Evaluation")]
        public void TestZoneStateEvaluation()
        {
            UnityEngine.Debug.Log("--- [Test] Zone State Evaluation ---");

            // Hub (E5) — isAlwaysUnlocked=true → UNLOCKED
            _worldMapService.Initialize(hubPosition);
            if (_worldMapService.TryGetCurrentZoneState(out ZoneRuntimeState hubState))
            {
                UnityEngine.Debug.Log($"  Hub (E5): {hubState.GetDebugStateString()}");
                UnityEngine.Debug.Log($"    Expected: UNLOCKED | LOW RISK");
            }

            // Research (I6) — 조건: battery_mk2 + log 2개 + narrative flag
            _worldMapService.RefreshCurrentZone(researchPosition);
            if (_worldMapService.TryGetCurrentZoneState(out ZoneRuntimeState researchState))
            {
                UnityEngine.Debug.Log($"  Research (I6): {researchState.GetDebugStateString()}");
                UnityEngine.Debug.Log($"    Expected: UNLOCKED (all conditions met)");
            }

            // Sealed (F10) — 조건: pressure_hull_lv3 + logs + resonance_filter
            _worldMapService.RefreshCurrentZone(sealedPosition);
            if (_worldMapService.TryGetCurrentZoneState(out ZoneRuntimeState sealedState))
            {
                UnityEngine.Debug.Log($"  Sealed (F10): {sealedState.GetDebugStateString()}");
                UnityEngine.Debug.Log($"    Expected: LOCKED (conditions not met)");
                if (sealedState.FailedConditions.Count > 0)
                {
                    UnityEngine.Debug.Log($"    Failed conditions: {sealedState.GetFailedConditionsString()}");
                }
            }

            UnityEngine.Debug.Log("--- [Test] Zone State Evaluation Complete ---");
        }

        /// <summary>현재 위치의 존 정보 출력 (Update에서 호출 가능)</summary>
        [ContextMenu("Print Current Zone Info")]
        public void PrintCurrentZoneInfo()
        {
            if (trackedTransform == null)
            {
                UnityEngine.Debug.LogWarning("[WorldMapQuickTest] trackedTransform is null.");
                return;
            }

            Vector3 pos = trackedTransform.position;
            if (_zoneResolver.TryGetZoneIdFromWorldPosition(pos, out ZoneId zoneId))
            {
                UnityEngine.Debug.Log($"[WorldMapQuickTest] Current position: ({pos.x:F1}, {pos.y:F1}, {pos.z:F1}) → Zone: {zoneId}");
            }
            else
            {
                UnityEngine.Debug.Log($"[WorldMapQuickTest] Current position: ({pos.x:F1}, {pos.y:F1}, {pos.z:F1}) → OUT OF BOUNDS");
            }
        }
    }
}
