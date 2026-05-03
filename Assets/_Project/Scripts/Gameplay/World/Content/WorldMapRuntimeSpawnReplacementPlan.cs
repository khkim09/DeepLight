using UnityEngine;

namespace Project.Gameplay.World.Content
{
    /// <summary>
    /// 하나의 RuntimePlaceholder가 어떤 prefab/profile로 치환될 예정인지 담는 순수 데이터 구조.
    /// MonoBehaviour가 아니므로 runtime/editor 양쪽에서 사용 가능.
    /// Phase 14.10-J-1: Replacement Plan (읽기 전용 검증/계획 단계).
    /// </summary>
    [System.Serializable]
    public class WorldMapRuntimeSpawnReplacementPlan
    {
        // ===== Source Marker Identity =====

        [Tooltip("이 placeholder가 속한 Zone ID (예: A1, B5, F6)")]
        public string ZoneId;

        [Tooltip("이 placeholder가 대응하는 source marker의 고유 식별자")]
        public string MarkerId;

        [Tooltip("source marker의 타입 (Resource/Hazard/Landmark/Narrative/Route/Debug/Unknown)")]
        public WorldMapZoneContentMarkerType MarkerType = WorldMapZoneContentMarkerType.Unknown;

        // ===== Runtime Spawn Profile =====

        [Tooltip("Resolver가 결정한 RuntimeCategory (예: HarvestResource, Hazard, WorldProp, NarrativeLog, RouteTrigger, Unknown)")]
        public string RuntimeCategory;

        [Tooltip("Resolver가 결정한 RuntimeKey (prefab/profile 식별 키)")]
        public string RuntimeKey;

        [Tooltip("매칭된 Spawn Profile Entry의 ProfileId")]
        public string ProfileId;

        [Tooltip("매칭된 Prefab의 이름")]
        public string PrefabName;

        [Tooltip("매칭된 Prefab 참조")]
        public GameObject Prefab;

        // ===== Placeholder Identity =====

        [Tooltip("RuntimePlaceholder GameObject의 이름")]
        public string PlaceholderName;

        // ===== World Transform Snapshot =====

        [Tooltip("RuntimePlaceholder의 월드 위치 (snapshot)")]
        public Vector3 WorldPosition;

        [Tooltip("RuntimePlaceholder의 월드 회전 (snapshot)")]
        public Quaternion WorldRotation;

        [Tooltip("RuntimePlaceholder의 월드 스케일 (snapshot)")]
        public Vector3 WorldScale;

        // ===== Plan Flags =====

        [Tooltip("이 plan이 fallback profile entry를 통해 매칭되었는지 여부")]
        public bool IsFallbackProfile;

        [Tooltip("매칭된 Prefab이 Editor/Test용 placeholder prefab인지 여부")]
        public bool IsEditorPlaceholderPrefab;

        [Tooltip("이 plan이 유효한지 여부. IsValid() 호출 시 참조됨")]
        public bool IsValidPlan;

        [Tooltip("plan이 유효하지 않은 경우의 실패 사유")]
        public string FailureReason;

        /// <summary>
        /// plan이 유효한지 검사한다.
        /// Prefab != null, ZoneId/MarkerId/ProfileId/PlaceholderName non-empty, IsValidPlan == true 조건을 모두 만족해야 true.
        /// </summary>
        public bool IsValid()
        {
            return Prefab != null
                && !string.IsNullOrEmpty(ZoneId)
                && !string.IsNullOrEmpty(MarkerId)
                && !string.IsNullOrEmpty(ProfileId)
                && !string.IsNullOrEmpty(PlaceholderName)
                && IsValidPlan;
        }

        /// <summary>
        /// Console 로그용 요약 문자열을 반환한다.
        /// 예: "{PlaceholderName} -> {ProfileId}/{PrefabName} [Zone={ZoneId}, Marker={MarkerId}, Category={RuntimeCategory}, Key={RuntimeKey}]"
        /// </summary>
        public string GetSummary()
        {
            string prefabStr = Prefab != null ? PrefabName : "null";
            string fallbackStr = IsFallbackProfile ? " [FALLBACK]" : "";
            string editorStr = IsEditorPlaceholderPrefab ? " [EDITOR_PLACEHOLDER]" : "";
            return $"{PlaceholderName} -> {ProfileId}/{prefabStr}{fallbackStr}{editorStr} [Zone={ZoneId}, Marker={MarkerId}, Category={RuntimeCategory}, Key={RuntimeKey}]";
        }
    }
}
