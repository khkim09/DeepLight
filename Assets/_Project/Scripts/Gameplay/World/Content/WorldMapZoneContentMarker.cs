using UnityEngine;

namespace Project.Gameplay.World.Content
{
    /// <summary>
    /// Zone Content Marker의 런타임/에디터 공용 metadata component.
    /// 각 marker root(ResourceSpawn_*, HazardSpawn_*, LandmarkSpawn_*, NarrativeSpawn_*, RouteMarker_*)에 부착되어
    /// zoneId, markerId, markerType, semantic tags, source rule/plan 정보를 제공한다.
    /// </summary>
    public class WorldMapZoneContentMarker : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string _zoneId;
        [SerializeField] private string _markerId;
        [SerializeField] private WorldMapZoneContentMarkerType _markerType = WorldMapZoneContentMarkerType.Unknown;

        [Header("Semantic Tags")]
        [SerializeField] private string[] _tags;

        [Header("Source Info")]
        [SerializeField] private string _sourceRuleSummary;
        [SerializeField] private string _sourcePlanSummary;

        [Header("Runtime Flags")]
        [SerializeField] private bool _isRuntimeSpawnPoint;
        [SerializeField] private bool _isDebugOnly;

        [Header("Positions")]
        [SerializeField] private Vector3 _localPositionInZone;
        [SerializeField] private Vector3 _worldPosition;

        // ===== Public Getters =====

        /// <summary>이 marker가 속한 Zone ID (예: "A1", "B5", "C10")</summary>
        public string ZoneId => _zoneId;

        /// <summary>고유 marker 식별자 (예: "ResourceSpawn_A1_0_iron")</summary>
        public string MarkerId => _markerId;

        /// <summary>marker 타입 (Resource/Hazard/Landmark/Narrative/Route/Debug/Unknown)</summary>
        public WorldMapZoneContentMarkerType MarkerType => _markerType;

        /// <summary>의미적 태그 배열 (예: resourceTags, hazardTags, landmarkTags, route role 등)</summary>
        public string[] Tags => _tags;

        /// <summary>생성에 사용된 source rule의 debugSummary 요약</summary>
        public string SourceRuleSummary => _sourceRuleSummary;

        /// <summary>생성에 사용된 source plan의 debugSummary 요약</summary>
        public string SourcePlanSummary => _sourcePlanSummary;

        /// <summary>런타임에서 실제 spawn point로 사용되어야 하는지 여부</summary>
        public bool IsRuntimeSpawnPoint => _isRuntimeSpawnPoint;

        /// <summary>디버그 전용 marker인지 여부 (실제 게임플레이에 사용되지 않음)</summary>
        public bool IsDebugOnly => _isDebugOnly;

        /// <summary>ZoneRoot 기준 local position (zone 내부 오프셋)</summary>
        public Vector3 LocalPositionInZone => _localPositionInZone;

        /// <summary>월드 좌표 position</summary>
        public Vector3 WorldPosition => _worldPosition;

        /// <summary>
        /// marker metadata를 설정한다.
        /// 생성 직후 호출되어 모든 필드를 채운다.
        /// </summary>
        /// <param name="zoneId">Zone ID (예: "A1")</param>
        /// <param name="markerId">고유 marker 식별자</param>
        /// <param name="markerType">marker 타입</param>
        /// <param name="tags">의미적 태그 배열</param>
        /// <param name="sourceRuleSummary">source rule 요약</param>
        /// <param name="sourcePlanSummary">source plan 요약</param>
        /// <param name="isRuntimeSpawnPoint">런타임 spawn point 여부</param>
        /// <param name="isDebugOnly">디버그 전용 여부</param>
        public void Configure(
            string zoneId,
            string markerId,
            WorldMapZoneContentMarkerType markerType,
            string[] tags,
            string sourceRuleSummary,
            string sourcePlanSummary,
            bool isRuntimeSpawnPoint,
            bool isDebugOnly)
        {
            _zoneId = zoneId;
            _markerId = markerId;
            _markerType = markerType;
            _tags = tags;
            _sourceRuleSummary = sourceRuleSummary;
            _sourcePlanSummary = sourcePlanSummary;
            _isRuntimeSpawnPoint = isRuntimeSpawnPoint;
            _isDebugOnly = isDebugOnly;

            // Transform 기반 위치 기록
            _localPositionInZone = transform.localPosition;
            _worldPosition = transform.position;
        }

        /// <summary>
        /// 현재 Transform 위치를 기준으로 LocalPositionInZone과 WorldPosition을 갱신한다.
        /// Transform이 변경된 후 호출하여 위치 정보를 동기화한다.
        /// </summary>
        public void RefreshPositions()
        {
            _localPositionInZone = transform.localPosition;
            _worldPosition = transform.position;
        }
    }
}
