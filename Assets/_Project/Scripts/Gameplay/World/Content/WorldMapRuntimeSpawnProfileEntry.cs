using UnityEngine;

namespace Project.Gameplay.World.Content
{
    /// <summary>
    /// 하나의 spawn profile mapping entry를 표현하는 serializable class.
    /// Candidate의 RuntimeCategory + RuntimeKey + MarkerType + SemanticTags를 기준으로
    /// 어떤 prefab/label/profile을 사용할지 정의한다.
    /// MonoBehaviour가 아니므로 runtime/editor 양쪽에서 사용 가능.
    /// </summary>
    [System.Serializable]
    public class WorldMapRuntimeSpawnProfileEntry
    {
        [Tooltip("이 entry의 고유 식별자. 중복 방지용. 예: HarvestResource_iron, Fallback_Hazard")]
        public string ProfileId;

        [Tooltip("매칭 대상 marker 타입. Unknown이면 모든 타입에 매칭 시도")]
        public WorldMapZoneContentMarkerType MarkerType = WorldMapZoneContentMarkerType.Unknown;

        [Tooltip("매칭 대상 RuntimeCategory (예: HarvestResource, Hazard, WorldProp, NarrativeLog, RouteTrigger, Debug, Unknown). 비어있으면 모든 category에 매칭")]
        public string RuntimeCategory;

        [Tooltip("매칭 대상 RuntimeKey (예: iron, steel, fuel, current, pressure, log, route, hub, wreck, harbor). 비어있으면 모든 key에 매칭")]
        public string RuntimeKey;

        [Tooltip("candidate의 SemanticTags와 비교할 매칭 태그 배열. 하나라도 일치하면 가산점")]
        public string[] MatchTags;

        [Tooltip("이 entry가 매칭될 때 사용할 실제 prefab 참조. 이번 단계에서는 null 허용")]
        public GameObject Prefab;

        [Tooltip("정확 매칭이 없을 때 fallback으로 선택될 entry인지 여부. true이면 MarkerType/RuntimeCategory 기반 fallback 매칭에 사용됨")]
        public bool IsFallbackProfile;

        [Tooltip("매칭 우선순위. 높을수록 우선 선택됨. 동일 조건에서는 Priority가 높은 entry가 선택됨")]
        public int Priority;

        [Tooltip("이 entry의 용도를 설명하는 문자열. 에디터/디버그용")]
        public string Description;

        /// <summary>
        /// 기본 생성자. 모든 필드는 기본값으로 초기화된다.
        /// </summary>
        public WorldMapRuntimeSpawnProfileEntry()
        {
        }

        /// <summary>
        /// 주요 필드를 한 번에 설정하는 생성자.
        /// </summary>
        /// <param name="profileId">고유 식별자</param>
        /// <param name="markerType">매칭 대상 marker 타입</param>
        /// <param name="runtimeCategory">매칭 대상 RuntimeCategory</param>
        /// <param name="runtimeKey">매칭 대상 RuntimeKey</param>
        /// <param name="matchTags">매칭 태그 배열</param>
        /// <param name="prefab">연결할 prefab (null 허용)</param>
        /// <param name="isFallbackProfile">fallback entry 여부</param>
        /// <param name="priority">매칭 우선순위</param>
        /// <param name="description">설명 문자열</param>
        public WorldMapRuntimeSpawnProfileEntry(
            string profileId,
            WorldMapZoneContentMarkerType markerType,
            string runtimeCategory,
            string runtimeKey,
            string[] matchTags,
            GameObject prefab,
            bool isFallbackProfile,
            int priority,
            string description)
        {
            ProfileId = profileId;
            MarkerType = markerType;
            RuntimeCategory = runtimeCategory;
            RuntimeKey = runtimeKey;
            MatchTags = matchTags;
            Prefab = prefab;
            IsFallbackProfile = isFallbackProfile;
            Priority = priority;
            Description = description;
        }

        /// <summary>
        /// 현재 entry 상태를 요약한 디버그 문자열을 반환한다.
        /// </summary>
        public string GetSummary()
        {
            string fallbackStr = IsFallbackProfile ? " [FALLBACK]" : "";
            string prefabStr = Prefab != null ? Prefab.name : "null";
            return $"[{ProfileId}]{fallbackStr} | Type={MarkerType} Cat={RuntimeCategory} Key={RuntimeKey} Prefab={prefabStr} Priority={Priority}";
        }
    }
}
