using System;

namespace Project.Gameplay.World.Content
{
    /// <summary>
    /// RuntimePlaceholderBinding을 해석한 결과를 담는 순수 serializable 데이터 구조.
    /// 실제 prefab/ScriptableObject asset 연결 없이 "후보 key"만 안정적으로 구성한다.
    /// MonoBehaviour가 아니므로 runtime/editor 양쪽에서 사용 가능.
    /// </summary>
    [System.Serializable]
    public class WorldMapRuntimeSpawnProfileCandidate
    {
        [UnityEngine.Tooltip("이 placeholder가 속한 Zone ID (예: A1, B5, F6)")]
        public string ZoneId;

        [UnityEngine.Tooltip("이 placeholder가 대응하는 source marker의 고유 식별자")]
        public string MarkerId;

        [UnityEngine.Tooltip("source marker의 타입 (Resource/Hazard/Landmark/Narrative/Route/Debug/Unknown)")]
        public WorldMapZoneContentMarkerType MarkerType = WorldMapZoneContentMarkerType.Unknown;

        [UnityEngine.Tooltip("Resolver가 결정한 RuntimeCategory (예: HarvestResource, Hazard, WorldProp, NarrativeLog, RouteTrigger, Unknown)")]
        public string RuntimeCategory;

        [UnityEngine.Tooltip("Resolver가 결정한 RuntimeKey (prefab/profile 식별 키)")]
        public string RuntimeKey;

        [UnityEngine.Tooltip("source marker에서 전달된 semantic tag 배열")]
        public string[] SemanticTags;

        [UnityEngine.Tooltip("생성에 사용된 source design rule의 요약 문자열")]
        public string SourceRuleSummary;

        [UnityEngine.Tooltip("생성에 사용된 source terrain plan의 요약 문자열")]
        public string SourcePlanSummary;

        [UnityEngine.Tooltip("Prototype zone(17개)에 속한 placeholder인지 여부")]
        public bool IsPrototypeZone;

        [UnityEngine.Tooltip("binding이 불완전하여 fallback/추정으로 resolve된 candidate인지 여부")]
        public bool IsFallbackCandidate;

        [UnityEngine.Tooltip("Resolver가 이 candidate를 어떻게 구성했는지 설명하는 메모")]
        public string ResolverNote;

        /// <summary>
        /// 기본 생성자. 모든 필드는 기본값으로 초기화된다.
        /// </summary>
        public WorldMapRuntimeSpawnProfileCandidate()
        {
        }

        /// <summary>
        /// 주요 필드를 한 번에 설정하는 생성자.
        /// </summary>
        public WorldMapRuntimeSpawnProfileCandidate(
            string zoneId,
            string markerId,
            WorldMapZoneContentMarkerType markerType,
            string runtimeCategory,
            string runtimeKey,
            string[] semanticTags,
            string sourceRuleSummary,
            string sourcePlanSummary,
            bool isPrototypeZone,
            bool isFallbackCandidate,
            string resolverNote)
        {
            ZoneId = zoneId;
            MarkerId = markerId;
            MarkerType = markerType;
            RuntimeCategory = runtimeCategory;
            RuntimeKey = runtimeKey;
            SemanticTags = semanticTags;
            SourceRuleSummary = sourceRuleSummary;
            SourcePlanSummary = sourcePlanSummary;
            IsPrototypeZone = isPrototypeZone;
            IsFallbackCandidate = isFallbackCandidate;
            ResolverNote = resolverNote;
        }

        /// <summary>
        /// 현재 candidate 상태를 요약한 디버그 문자열을 반환한다.
        /// </summary>
        public string GetSummary()
        {
            string typeStr = MarkerType.ToString();
            string fallbackStr = IsFallbackCandidate ? " [FALLBACK]" : "";
            return $"[{RuntimeCategory}] {RuntimeKey}{fallbackStr} | Zone={ZoneId} Marker={MarkerId} Type={typeStr} Proto={IsPrototypeZone}";
        }

        /// <summary>
        /// candidate가 유효한지 간단히 확인한다.
        /// ZoneId / MarkerId / RuntimeCategory / RuntimeKey가 모두 비어있지 않아야 true.
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(ZoneId)
                && !string.IsNullOrEmpty(MarkerId)
                && !string.IsNullOrEmpty(RuntimeCategory)
                && !string.IsNullOrEmpty(RuntimeKey);
        }
    }
}
