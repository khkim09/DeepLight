using UnityEngine;

namespace Project.Gameplay.World.Content
{
    /// <summary>
    /// RuntimePlaceholder에 부착되어 source marker metadata를 추적하고
    /// runtime spawn binding 정보를 제공하는 에디터/런타임 공용 컴포넌트.
    /// Phase 14.10-G-1: Runtime Placeholder Metadata Binding.
    /// </summary>
    public class WorldMapRuntimePlaceholderBinding : MonoBehaviour
    {
        [Header("Source Marker Identity")]
        [SerializeField, Tooltip("이 placeholder가 속한 Zone ID (예: A1, B5, F6)")]
        private string _zoneId;

        [SerializeField, Tooltip("이 placeholder가 대응하는 source marker의 고유 식별자")]
        private string _markerId;

        [SerializeField, Tooltip("source marker의 타입 (Resource/Hazard/Landmark/Narrative/Route/Debug/Unknown)")]
        private WorldMapZoneContentMarkerType _markerType = WorldMapZoneContentMarkerType.Unknown;

        [Header("Semantic Tags")]
        [SerializeField, Tooltip("source marker에서 전달된 semantic tag 배열")]
        private string[] _semanticTags;

        [Header("Source Info")]
        [SerializeField, Tooltip("생성에 사용된 source design rule의 요약 문자열")]
        private string _sourceRuleSummary;

        [SerializeField, Tooltip("생성에 사용된 source terrain plan의 요약 문자열")]
        private string _sourcePlanSummary;

        [Header("Runtime Binding")]
        [SerializeField, Tooltip("런타임 spawn/prefab 카테고리 (예: HarvestResource, Hazard, Landmark, NarrativeLog, RouteTrigger, Debug)")]
        private string _runtimeCategory;

        [SerializeField, Tooltip("런타임 spawn/prefab 식별 키 (예: 첫 번째 semantic tag, markerId 기반)")]
        private string _runtimeKey;

        [Header("Runtime Flags")]
        [SerializeField, Tooltip("실제 prefab/profile이 연결되어 런타임 spawn이 준비된 상태인지 여부. 이번 단계에서는 기본 false.")]
        private bool _isRuntimeReady;

        [SerializeField, Tooltip("Prototype zone(17개)에 속한 placeholder인지 여부")]
        private bool _isPrototypeZone;

        [SerializeField, Tooltip("source marker를 이름 기반으로 찾지 못해 fallback/추정으로 생성된 binding인지 여부")]
        private bool _isFallbackBinding;

        // ===== Public Properties =====

        /// <summary>이 placeholder가 속한 Zone ID (예: "A1", "B5", "F6")</summary>
        public string ZoneId => _zoneId;

        /// <summary>이 placeholder가 대응하는 source marker의 고유 식별자</summary>
        public string MarkerId => _markerId;

        /// <summary>source marker의 타입</summary>
        public WorldMapZoneContentMarkerType MarkerType => _markerType;

        /// <summary>source marker에서 전달된 semantic tag 배열</summary>
        public string[] SemanticTags => _semanticTags;

        /// <summary>생성에 사용된 source design rule의 요약 문자열</summary>
        public string SourceRuleSummary => _sourceRuleSummary;

        /// <summary>생성에 사용된 source terrain plan의 요약 문자열</summary>
        public string SourcePlanSummary => _sourcePlanSummary;

        /// <summary>런타임 spawn/prefab 카테고리</summary>
        public string RuntimeCategory => _runtimeCategory;

        /// <summary>런타임 spawn/prefab 식별 키</summary>
        public string RuntimeKey => _runtimeKey;

        /// <summary>실제 prefab/profile이 연결되어 런타임 spawn이 준비된 상태인지 여부</summary>
        public bool IsRuntimeReady => _isRuntimeReady;

        /// <summary>Prototype zone(17개)에 속한 placeholder인지 여부</summary>
        public bool IsPrototypeZone => _isPrototypeZone;

        /// <summary>source marker를 찾지 못해 fallback으로 생성된 binding인지 여부</summary>
        public bool IsFallbackBinding => _isFallbackBinding;

        /// <summary>
        /// 모든 binding 필드를 설정한다.
        /// RebuildRuntimePlaceholderBindings에서 호출되어 marker metadata를 기반으로 필드를 채운다.
        /// </summary>
        /// <param name="zoneId">Zone ID (예: "A1")</param>
        /// <param name="markerId">고유 marker 식별자</param>
        /// <param name="markerType">marker 타입</param>
        /// <param name="semanticTags">의미적 태그 배열</param>
        /// <param name="sourceRuleSummary">source rule 요약</param>
        /// <param name="sourcePlanSummary">source plan 요약</param>
        /// <param name="runtimeCategory">런타임 spawn 카테고리</param>
        /// <param name="runtimeKey">런타임 spawn 키</param>
        /// <param name="isRuntimeReady">런타임 준비 상태 (이번 단계에서는 기본 false)</param>
        /// <param name="isPrototypeZone">prototype zone 여부</param>
        /// <param name="isFallbackBinding">fallback binding 여부</param>
        public void Configure(
            string zoneId,
            string markerId,
            WorldMapZoneContentMarkerType markerType,
            string[] semanticTags,
            string sourceRuleSummary,
            string sourcePlanSummary,
            string runtimeCategory,
            string runtimeKey,
            bool isRuntimeReady,
            bool isPrototypeZone,
            bool isFallbackBinding)
        {
            _zoneId = zoneId;
            _markerId = markerId;
            _markerType = markerType;
            _semanticTags = semanticTags;
            _sourceRuleSummary = sourceRuleSummary;
            _sourcePlanSummary = sourcePlanSummary;
            _runtimeCategory = runtimeCategory;
            _runtimeKey = runtimeKey;
            _isRuntimeReady = isRuntimeReady;
            _isPrototypeZone = isPrototypeZone;
            _isFallbackBinding = isFallbackBinding;
        }

        /// <summary>
        /// IsRuntimeReady 플래그를 true로 설정한다.
        /// 실제 prefab/profile이 연결되는 단계에서 호출된다.
        /// </summary>
        public void MarkRuntimeReady()
        {
            _isRuntimeReady = true;
        }

        /// <summary>
        /// IsRuntimeReady 플래그를 false로 재설정한다.
        /// </summary>
        public void MarkRuntimeNotReady()
        {
            _isRuntimeReady = false;
        }

        /// <summary>
        /// 현재 binding 상태를 요약한 디버그 문자열을 반환한다.
        /// </summary>
        public string GetBindingSummary()
        {
            return $"[{_runtimeCategory}] {_runtimeKey} | Zone={_zoneId} Marker={_markerId} Type={_markerType} " +
                   $"Ready={_isRuntimeReady} Proto={_isPrototypeZone} Fallback={_isFallbackBinding}";
        }
    }
}
