using System.Collections.Generic;
using System.Text;
using Project.Data.World;
using UnityEngine;

namespace Project.Gameplay.World
{
    /// <summary>월드맵 시스템 테스트 컴포넌트</summary>
    public class WorldMapSystemTest : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private WorldMapConfigSO worldMapConfig;
        [SerializeField] private ZoneDataSO testZoneData;

        [Header("Test Position")]
        [SerializeField] private Transform testPositionTransform;
        [SerializeField] private Vector3 testWorldPosition = new Vector3(0, 0, 0);

        [Header("Mock Progress")]
        [SerializeField] private bool useMockProgress = true;
        [SerializeField] private string testUpgradeId = "upgrade_sensor_2";
        [SerializeField] private string testLogId = "log_abyssal_1";
        [SerializeField] private string testNarrativeFlag = "story_chapter2_complete";
        [SerializeField] private int mockHullTier = 2;
        [SerializeField] private int mockDepthLevel = 3;
        [SerializeField] private float mockSensorAccuracy = 0.8f;
        [SerializeField] private int mockLogCount = 5;

        private IZoneResolver _zoneResolver;
        private IZoneStateEvaluator _zoneStateEvaluator;
        private MockWorldProgressQuery _mockProgressQuery;

        private void Start()
        {
            if (worldMapConfig == null)
            {
                UnityEngine.Debug.LogError("[WorldMapSystemTest] WorldMapConfigSO is not assigned.");
                return;
            }

            InitializeSystems();
            RunTests();
        }

        private void InitializeSystems()
        {
            // ZoneResolver 초기화
            _zoneResolver = new ZoneResolver(worldMapConfig);

            // ConditionEvaluator 및 ZoneStateEvaluator 초기화
            UnlockConditionEvaluator conditionEvaluator = new UnlockConditionEvaluator();
            _zoneStateEvaluator = new ZoneStateEvaluator(conditionEvaluator);

            // Mock 진행도 설정
            if (useMockProgress)
            {
                _mockProgressQuery = new MockWorldProgressQuery();
                _mockProgressQuery.AddUpgrade(testUpgradeId);
                _mockProgressQuery.AddLog(testLogId);
                _mockProgressQuery.AddNarrativeFlag(testNarrativeFlag);
                _mockProgressQuery.SetHullTier(mockHullTier);
                _mockProgressQuery.SetDepthLevel(mockDepthLevel);
                _mockProgressQuery.SetSensorAccuracy(mockSensorAccuracy);

                // 로그 수 설정
                for (int i = 0; i < mockLogCount; i++)
                {
                    _mockProgressQuery.AddLog($"log_test_{i}");
                }

                UnityEngine.Debug.Log($"[WorldMapSystemTest] Mock progress initialized: {_mockProgressQuery.GetDebugState()}");
            }
        }

        private void RunTests()
        {
            StringBuilder sb = new();
            sb.AppendLine("=== World Map System Test ===");

            // 1. ZoneResolver 테스트 (안전 API 사용)
            sb.AppendLine("\n1. ZoneResolver Tests (Safe API):");
            Vector3 testPos = testPositionTransform != null ?
                testPositionTransform.position : testWorldPosition;

            bool inBounds = _zoneResolver.IsWorldPositionInBounds(testPos);
            sb.AppendLine($"World Position: {testPos}");
            sb.AppendLine($"In Bounds: {inBounds}");

            if (inBounds && _zoneResolver.TryGetZoneIdFromWorldPosition(testPos, out ZoneId zoneId))
            {
                if (_zoneResolver.TryGetZoneCoordinateFromWorldPosition(testPos, out ZoneCoordinate zoneCoordinate))
                {
                    Vector3 zoneCenter = _zoneResolver.GetZoneCenterWorldPosition(zoneId);
                    (Vector3 min, Vector3 max) zoneBounds = _zoneResolver.GetZoneBounds(zoneId);

                    sb.AppendLine($"Zone ID: {zoneId}");
                    sb.AppendLine($"Zone Coordinate: {zoneCoordinate}");
                    sb.AppendLine($"Zone Center: {zoneCenter}");
                    sb.AppendLine($"Zone Bounds: [{zoneBounds.min} - {zoneBounds.max}]");
                }
                else
                {
                    sb.AppendLine($"Failed to get zone coordinate for position: {testPos}");
                }
            }
            else
            {
                sb.AppendLine($"Position out of bounds or failed to get zone ID: {testPos}");
                // 클램프된 API로 폴백 테스트
                ZoneId clampedZoneId = _zoneResolver.GetClampedZoneIdFromWorldPosition(testPos);
                ZoneCoordinate clampedCoordinate = _zoneResolver.GetClampedZoneCoordinateFromWorldPosition(testPos);
                sb.AppendLine($"Clamped Zone ID: {clampedZoneId}");
                sb.AppendLine($"Clamped Coordinate: {clampedCoordinate}");
            }

            // 2. ZoneStateEvaluator 테스트
            if (testZoneData != null && _mockProgressQuery != null)
            {
                sb.AppendLine("\n2. ZoneStateEvaluator Tests:");

                ZoneRuntimeState zoneState = _zoneStateEvaluator.EvaluateZoneState(
                    testZoneData.ZoneId, testZoneData, _mockProgressQuery);

                sb.AppendLine($"Zone: {testZoneData.ZoneId}");
                sb.AppendLine($"Display Name: {testZoneData.DisplayName}");
                sb.AppendLine($"Region: {testZoneData.RegionId}");
                sb.AppendLine($"Base Risk: {testZoneData.BaseRiskLevel:P0}");
                sb.AppendLine($"Is Starting Zone: {testZoneData.IsStartingZone}");
                sb.AppendLine($"Is Story Critical: {testZoneData.IsStoryCritical}");
                sb.AppendLine($"--- Runtime State ---");
                sb.AppendLine($"Accessibility: {zoneState.Accessibility}");
                sb.AppendLine($"Lock Reason: {zoneState.LockReason}");
                sb.AppendLine($"Current Risk: {zoneState.CurrentRiskLevel:P0}");
                sb.AppendLine($"Discovered: {zoneState.IsDiscovered}");
                sb.AppendLine($"Unlocked: {zoneState.IsUnlocked}");
                sb.AppendLine($"Safe: {zoneState.IsSafe}");
                sb.AppendLine($"Risky: {zoneState.IsRisky}");

                if (zoneState.FailedConditions.Count > 0)
                {
                    sb.AppendLine($"Failed Conditions: {zoneState.GetFailedConditionsString()}");
                }

                // 해금 가능성 테스트
                bool isUnlockable = _zoneStateEvaluator.IsZoneUnlockable(
                    testZoneData.ZoneId, testZoneData, _mockProgressQuery,
                    out ZoneLockReason lockReason, out List<UnlockConditionSetSO.ConditionEntry> failedConditions);

                sb.AppendLine($"\nIs Unlockable: {isUnlockable}");
                sb.AppendLine($"Lock Reason: {lockReason}");
                sb.AppendLine($"Failed Condition Count: {failedConditions.Count}");
            }

            // 3. 모든 존 ID 열거 테스트
            sb.AppendLine("\n3. All Zone IDs:");
            int count = 0;
            foreach (ZoneId id in worldMapConfig.GetAllZoneIds())
            {
                sb.Append($"{id} ");
                count++;
                if (count % 10 == 0) sb.AppendLine();
            }

            UnityEngine.Debug.Log(sb.ToString());
        }

        private void OnDrawGizmosSelected()
        {
            // 레이지 초기화
            if (worldMapConfig == null)
                return;

            if (_zoneResolver == null)
            {
                _zoneResolver = new ZoneResolver(worldMapConfig);
            }

            // 현재 테스트 위치의 존 시각화
            Vector3 testPos = testPositionTransform != null ?
                testPositionTransform.position : testWorldPosition;

            if (_zoneResolver.IsWorldPositionInBounds(testPos))
            {
                ZoneId zoneId = _zoneResolver.GetZoneIdFromWorldPosition(testPos);
                (Vector3 min, Vector3 max) bounds = _zoneResolver.GetZoneBounds(zoneId);

                // 존 경계 박스 그리기
                Gizmos.color = Color.green;
                Vector3 center = (bounds.min + bounds.max) / 2f;
                Vector3 size = new Vector3(
                    bounds.max.x - bounds.min.x,
                    10f, // 높이는 임의
                    bounds.max.z - bounds.min.z);

                Gizmos.DrawWireCube(center, size);

                // 존 중심 표시
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(_zoneResolver.GetZoneCenterWorldPosition(zoneId), 5f);

                // 테스트 위치 표시
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(testPos, 2f);

                // 그리드 전체 경계
                Gizmos.color = Color.blue;
                Vector3 worldCenter = new Vector3(
                    (worldMapConfig.WorldMinX + worldMapConfig.WorldMaxX) / 2f,
                    0f,
                    (worldMapConfig.WorldMinZ + worldMapConfig.WorldMaxZ) / 2f);
                Vector3 worldSize = new Vector3(
                    worldMapConfig.WorldWidth,
                    10f,
                    worldMapConfig.WorldHeight);

                Gizmos.DrawWireCube(worldCenter, worldSize);
            }
        }

        /// <summary>에디터에서 테스트 실행</summary>
        [ContextMenu("Run Tests")]
        public void RunTestsInEditor()
        {
            InitializeSystems();
            RunTests();
        }

        /// <summary>에디터에서 존 정보 출력</summary>
        [ContextMenu("Print Current Zone Info")]
        public void PrintCurrentZoneInfo()
        {
            if (_zoneResolver == null && worldMapConfig != null)
            {
                _zoneResolver = new ZoneResolver(worldMapConfig);
            }

            if (_zoneResolver != null)
            {
                Vector3 testPos = testPositionTransform != null ?
                    testPositionTransform.position : testWorldPosition;

                string info = _zoneResolver.GetDebugZoneInfo(testPos);
                UnityEngine.Debug.Log($"[WorldMapSystemTest] Current Zone Info: {info}");
            }
        }
    }
}
