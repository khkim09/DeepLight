using UnityEngine;

namespace Project.Gameplay.World.Content
{
    /// <summary>
    /// 하나의 final content requirement slot을 표현하는 serializable class.
    /// M-1 contract 기준으로 어떤 final content prefab/profile이 필요한지를 정의한다.
    /// MonoBehaviour가 아니므로 runtime/editor 양쪽에서 사용 가능.
    /// </summary>
    [System.Serializable]
    public class WorldMapRuntimeFinalContentRequirementEntry
    {
        [SerializeField, Tooltip("이 requirement의 고유 식별자. 중복 방지용. 예: Final_HarvestResource_iron_HarvestResource_iron")]
        private string _requirementId;

        [SerializeField, Tooltip("이 requirement가 대응하는 최종 content 종류")]
        private WorldMapRuntimeFinalContentKind _finalContentKind = WorldMapRuntimeFinalContentKind.None;

        [SerializeField, Tooltip("이 requirement가 대응하는 gameplay adapter 종류")]
        private WorldMapRuntimeGameplayAdapterKind _adapterKind = WorldMapRuntimeGameplayAdapterKind.None;

        [SerializeField, Tooltip("매칭 대상 RuntimeCategory (예: HarvestResource, Hazard, WorldProp, NarrativeLog, RouteTrigger, Debug, Unknown)")]
        private string _runtimeCategory;

        [SerializeField, Tooltip("매칭 대상 RuntimeKey (예: iron, steel, fuel, current, pressure, log, route, hub, wreck, harbor)")]
        private string _runtimeKey;

        [SerializeField, Tooltip("매칭 대상 ProfileId (예: HarvestResource_iron, Hazard_current)")]
        private string _profileId;

        [SerializeField, Tooltip("매칭에 사용할 보조 키. exact match 외 추가 매칭 조건으로 사용")]
        private string _matchKey;

        [SerializeField, Tooltip("이 requirement의 표시 이름. 에디터/디버그용")]
        private string _displayName;

        [SerializeField, Tooltip("이 requirement의 상세 설명. 에디터/디버그용")]
        private string _description;

        [SerializeField, Tooltip("이 requirement가 매칭될 때 사용할 실제 final content prefab 참조. 이번 단계에서는 null 허용")]
        private GameObject _finalPrefab;

        [SerializeField, Tooltip("이 requirement가 매칭될 때 사용할 실제 final content profile 참조. 이번 단계에서는 null 허용")]
        private ScriptableObject _finalProfile;

        [SerializeField, Tooltip("이 requirement가 fallback requirement인지 여부. true이면 finalContentKind 단위 fallback으로 사용됨")]
        private bool _isFallbackRequirement;

        [SerializeField, Tooltip("이 requirement가 fallback 매칭에서 사용될 수 있는지 여부")]
        private bool _allowFallbackUse = true;

        [SerializeField, Tooltip("매칭 우선순위. 높을수록 우선 선택됨. exact=100, fallback=10")]
        private int _priority;

        [SerializeField, Tooltip("매칭 태그 배열. contract의 matchTags와 비교하여 추가 매칭 조건으로 사용")]
        private string[] _matchTags;

        // ===== Public Properties =====

        /// <summary>이 requirement의 고유 식별자</summary>
        public string RequirementId { get => _requirementId; set => _requirementId = value; }

        /// <summary>이 requirement가 대응하는 최종 content 종류</summary>
        public WorldMapRuntimeFinalContentKind FinalContentKind { get => _finalContentKind; set => _finalContentKind = value; }

        /// <summary>이 requirement가 대응하는 gameplay adapter 종류</summary>
        public WorldMapRuntimeGameplayAdapterKind AdapterKind { get => _adapterKind; set => _adapterKind = value; }

        /// <summary>매칭 대상 RuntimeCategory</summary>
        public string RuntimeCategory { get => _runtimeCategory; set => _runtimeCategory = value; }

        /// <summary>매칭 대상 RuntimeKey</summary>
        public string RuntimeKey { get => _runtimeKey; set => _runtimeKey = value; }

        /// <summary>매칭 대상 ProfileId</summary>
        public string ProfileId { get => _profileId; set => _profileId = value; }

        /// <summary>매칭에 사용할 보조 키</summary>
        public string MatchKey { get => _matchKey; set => _matchKey = value; }

        /// <summary>이 requirement의 표시 이름</summary>
        public string DisplayName { get => _displayName; set => _displayName = value; }

        /// <summary>이 requirement의 상세 설명</summary>
        public string Description { get => _description; set => _description = value; }

        /// <summary>이 requirement가 매칭될 때 사용할 실제 final content prefab 참조</summary>
        public GameObject FinalPrefab { get => _finalPrefab; set => _finalPrefab = value; }

        /// <summary>이 requirement가 매칭될 때 사용할 실제 final content profile 참조</summary>
        public ScriptableObject FinalProfile { get => _finalProfile; set => _finalProfile = value; }

        /// <summary>이 requirement가 fallback requirement인지 여부</summary>
        public bool IsFallbackRequirement { get => _isFallbackRequirement; set => _isFallbackRequirement = value; }

        /// <summary>이 requirement가 fallback 매칭에서 사용될 수 있는지 여부</summary>
        public bool AllowFallbackUse { get => _allowFallbackUse; set => _allowFallbackUse = value; }

        /// <summary>매칭 우선순위</summary>
        public int Priority { get => _priority; set => _priority = value; }

        /// <summary>매칭 태그 배열</summary>
        public string[] MatchTags { get => _matchTags; set => _matchTags = value; }

        // ===== Public API =====

        /// <summary>
        /// finalPrefab 또는 finalProfile이 할당되어 있으면 true를 반환한다.
        /// 현재 단계에서는 null이어도 WARN이며 FAIL이 아니다.
        /// </summary>
        /// <returns>구성되었으면 true</returns>
        public bool IsConfigured()
        {
            return _finalPrefab != null || _finalProfile != null;
        }

        /// <summary>
        /// 주어진 contract가 이 requirement entry와 매칭되는지 확인한다.
        /// 매칭 정책:
        /// 1. contract null이면 false
        /// 2. finalContentKind 일치 확인
        /// 3. runtimeCategory가 비어있지 않으면 contract.RuntimeCategory와 일치해야 함
        /// 4. runtimeKey가 비어있지 않으면 contract.RuntimeKey와 일치해야 함
        /// 5. profileId가 비어있지 않으면 contract.ProfileId와 일치해야 함
        /// 6. fallback requirement는 exact requirement보다 낮은 우선순위로 쓰일 수 있게 한다
        /// </summary>
        /// <param name="contract">매칭 대상 contract</param>
        /// <returns>매칭되면 true</returns>
        public bool Matches(WorldMapRuntimeFinalContentContract contract)
        {
            if (contract == null)
                return false;

            // 1. finalContentKind 일치 확인
            if (_finalContentKind != contract.FinalContentKind)
                return false;

            // 2. runtimeCategory가 비어있지 않으면 contract.RuntimeCategory와 일치해야 함
            if (!string.IsNullOrEmpty(_runtimeCategory))
            {
                if (!string.Equals(_runtimeCategory, contract.RuntimeCategory, System.StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // 3. runtimeKey가 비어있지 않으면 contract.RuntimeKey와 일치해야 함
            if (!string.IsNullOrEmpty(_runtimeKey))
            {
                if (!string.Equals(_runtimeKey, contract.RuntimeKey, System.StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // 4. profileId가 비어있지 않으면 contract.ProfileId와 일치해야 함
            if (!string.IsNullOrEmpty(_profileId))
            {
                if (!string.Equals(_profileId, contract.ProfileId, System.StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// finalContentKind / runtimeCategory / runtimeKey / profileId 기반의
        /// exact match key를 생성한다.
        /// </summary>
        /// <returns>exact match key 문자열</returns>
        public string BuildExactKey()
        {
            string kindStr = _finalContentKind.ToString();
            string catStr = string.IsNullOrEmpty(_runtimeCategory) ? "None" : _runtimeCategory;
            string keyStr = string.IsNullOrEmpty(_runtimeKey) ? "None" : _runtimeKey;
            string profStr = string.IsNullOrEmpty(_profileId) ? "None" : _profileId;
            return $"{kindStr}|{catStr}|{keyStr}|{profStr}";
        }

        /// <summary>
        /// 현재 entry 상태를 요약한 디버그 문자열을 반환한다.
        /// </summary>
        /// <returns>요약 문자열</returns>
        public string GetSummary()
        {
            string fallbackStr = _isFallbackRequirement ? " [FALLBACK]" : "";
            string configuredStr = IsConfigured() ? " [CONFIGURED]" : " [UNCONFIGURED]";
            string prefabStr = _finalPrefab != null ? _finalPrefab.name : "null";
            string profileStr = _finalProfile != null ? _finalProfile.name : "null";
            return $"[{_requirementId}]{fallbackStr}{configuredStr} | " +
                $"Kind={_finalContentKind} Cat={_runtimeCategory} Key={_runtimeKey} " +
                $"ProfileId={_profileId} Prefab={prefabStr} Profile={profileStr} Priority={_priority}";
        }
    }
}
