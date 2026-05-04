using UnityEngine;

namespace Project.Gameplay.World.Content
{
    /// <summary>
    /// RuntimeFinalContentInstances 하위 final content instance에 부착되는 category별 gameplay binding component.
    /// 실제 Harvest/Hazard/Narrative/Landmark/RouteTrigger 시스템에 직접 연결하지 않고,
    /// WorldMapRuntimeFinalContentInstanceTag 기반으로 gameplay 후보 데이터를 읽을 수 있는 얇은 adapter/binding 계층.
    /// UnityEditor API를 사용하지 않으므로 runtime/editor 양쪽에서 안전하게 사용 가능.
    /// Awake/Update/LateUpdate/코루틴을 사용하지 않는다.
    /// </summary>
    public class WorldMapRuntimeFinalContentGameplayBinding : MonoBehaviour
    {
        // ===== Serialized Fields =====

        [SerializeField, Tooltip("대응하는 source marker의 고유 식별자")]
        private string _sourceMarkerId;

        [SerializeField, Tooltip("이 instance가 속한 Zone ID (예: A1, B5, F6)")]
        private string _zoneId;

        [SerializeField, Tooltip("Resolver가 결정한 RuntimeCategory (예: HarvestResource, Hazard, WorldProp, NarrativeLog, RouteTrigger)")]
        private string _runtimeCategory;

        [SerializeField, Tooltip("Resolver가 결정한 RuntimeKey (prefab/profile 식별 키)")]
        private string _runtimeKey;

        [SerializeField, Tooltip("매칭된 Spawn Profile Entry의 ProfileId")]
        private string _profileId;

        [SerializeField, Tooltip("매칭된 Requirement Entry의 고유 식별자")]
        private string _requirementId;

        [SerializeField, Tooltip("최종 교체에 사용한 final prefab의 이름")]
        private string _finalPrefabName;

        [SerializeField, Tooltip("최종 교체에 사용한 final profile의 이름")]
        private string _finalProfileName;

        [SerializeField, Tooltip("runtimeCategory 기반으로 결정된 gameplay adapter 종류")]
        private WorldMapRuntimeGameplayAdapterKind _adapterKind = WorldMapRuntimeGameplayAdapterKind.None;

        [SerializeField, Tooltip("adapterKind 기반으로 매핑된 최종 content 종류")]
        private WorldMapRuntimeFinalContentKind _finalContentKind = WorldMapRuntimeFinalContentKind.None;

        [SerializeField, Tooltip("finalContentKind 기반으로 매핑된 gameplay binding 종류")]
        private WorldMapRuntimeFinalContentGameplayBindingKind _bindingKind = WorldMapRuntimeFinalContentGameplayBindingKind.None;

        [SerializeField, Tooltip("이 instance가 generated placeholder final content인지 여부")]
        private bool _isGeneratedPlaceholderContent;

        [SerializeField, Tooltip("이 instance가 사용자가 직접 할당한 final content인지 여부")]
        private bool _isUserAssignedFinalContent;

        [SerializeField, Tooltip("gameplay binding이 유효한 상태인지 여부 (모든 필수 필드가 채워져 있음)")]
        private bool _isGameplayBindingReady;

        [SerializeField, Tooltip("binding 상태에 대한 설명/이유")]
        private string _bindingReason;

        // ===== Public Getters =====

        /// <summary>대응하는 source marker의 고유 식별자</summary>
        public string SourceMarkerId => _sourceMarkerId;

        /// <summary>이 instance가 속한 Zone ID (예: "A1", "B5", "F6")</summary>
        public string ZoneId => _zoneId;

        /// <summary>Resolver가 결정한 RuntimeCategory</summary>
        public string RuntimeCategory => _runtimeCategory;

        /// <summary>Resolver가 결정한 RuntimeKey</summary>
        public string RuntimeKey => _runtimeKey;

        /// <summary>매칭된 Spawn Profile Entry의 ProfileId</summary>
        public string ProfileId => _profileId;

        /// <summary>매칭된 Requirement Entry의 고유 식별자</summary>
        public string RequirementId => _requirementId;

        /// <summary>최종 교체에 사용한 final prefab의 이름</summary>
        public string FinalPrefabName => _finalPrefabName;

        /// <summary>최종 교체에 사용한 final profile의 이름</summary>
        public string FinalProfileName => _finalProfileName;

        /// <summary>runtimeCategory 기반으로 결정된 gameplay adapter 종류</summary>
        public WorldMapRuntimeGameplayAdapterKind AdapterKind => _adapterKind;

        /// <summary>adapterKind 기반으로 매핑된 최종 content 종류</summary>
        public WorldMapRuntimeFinalContentKind FinalContentKind => _finalContentKind;

        /// <summary>finalContentKind 기반으로 매핑된 gameplay binding 종류</summary>
        public WorldMapRuntimeFinalContentGameplayBindingKind BindingKind => _bindingKind;

        /// <summary>이 instance가 generated placeholder final content인지 여부</summary>
        public bool IsGeneratedPlaceholderContent => _isGeneratedPlaceholderContent;

        /// <summary>이 instance가 사용자가 직접 할당한 final content인지 여부</summary>
        public bool IsUserAssignedFinalContent => _isUserAssignedFinalContent;

        /// <summary>gameplay binding이 유효한 상태인지 여부</summary>
        public bool IsGameplayBindingReady => _isGameplayBindingReady;

        /// <summary>binding 상태에 대한 설명/이유</summary>
        public string BindingReason => _bindingReason;

        // ===== Public API =====

        /// <summary>
        /// WorldMapRuntimeFinalContentInstanceTag의 정보를 기반으로 이 binding component를 구성한다.
        /// finalContentKind를 기준으로 bindingKind를 매핑하고, 필수 필드 유효성을 검사한다.
        /// </summary>
        /// <param name="tag">구성에 사용할 final content instance tag</param>
        public void Configure(WorldMapRuntimeFinalContentInstanceTag tag)
        {
            if (tag == null)
            {
                _isGameplayBindingReady = false;
                _bindingReason = "Configure called with null tag.";
                return;
            }

            // tag 값들을 복사
            _sourceMarkerId = tag.SourceMarkerId ?? string.Empty;
            _zoneId = tag.ZoneId ?? string.Empty;
            _runtimeCategory = tag.RuntimeCategory ?? string.Empty;
            _runtimeKey = tag.RuntimeKey ?? string.Empty;
            _profileId = tag.ProfileId ?? string.Empty;
            _requirementId = tag.RequirementId ?? string.Empty;
            _finalPrefabName = tag.FinalPrefabName ?? string.Empty;
            _finalProfileName = tag.FinalProfileName ?? string.Empty;
            _adapterKind = tag.AdapterKind;
            _finalContentKind = tag.FinalContentKind;

            // finalContentKind를 기준으로 bindingKind 매핑
            _bindingKind = MapBindingKind(_finalContentKind);

            // placeholder 여부: tag.IsGeneratedPlaceholderContent 우선 사용
            _isGeneratedPlaceholderContent = tag.IsGeneratedPlaceholderContent;

            // user assigned 여부: tag.IsUserAssignedFinalContent 우선 사용
            _isUserAssignedFinalContent = tag.IsUserAssignedFinalContent;

            // 유효성 검사: 모든 필수 필드가 채워져 있는지 확인
            ValidateBinding();
        }

        /// <summary>
        /// 지정한 zoneId와 일치하는지 확인한다.
        /// </summary>
        /// <param name="zoneId">비교할 Zone ID</param>
        /// <returns>일치 여부</returns>
        public bool IsZone(string zoneId)
        {
            return string.Equals(_zoneId, zoneId, System.StringComparison.Ordinal);
        }

        /// <summary>
        /// 지정한 runtimeCategory와 일치하는지 확인한다.
        /// </summary>
        /// <param name="runtimeCategory">비교할 RuntimeCategory</param>
        /// <returns>일치 여부</returns>
        public bool IsRuntimeCategory(string runtimeCategory)
        {
            return string.Equals(_runtimeCategory, runtimeCategory, System.StringComparison.Ordinal);
        }

        /// <summary>
        /// 지정한 finalContentKind와 일치하는지 확인한다.
        /// </summary>
        /// <param name="kind">비교할 FinalContentKind</param>
        /// <returns>일치 여부</returns>
        public bool IsFinalContentKind(WorldMapRuntimeFinalContentKind kind)
        {
            return _finalContentKind == kind;
        }

        /// <summary>
        /// 지정한 bindingKind와 일치하는지 확인한다.
        /// </summary>
        /// <param name="kind">비교할 BindingKind</param>
        /// <returns>일치 여부</returns>
        public bool IsBindingKind(WorldMapRuntimeFinalContentGameplayBindingKind kind)
        {
            return _bindingKind == kind;
        }

        /// <summary>
        /// 이 binding이 gameplay 시스템이 소비할 준비가 되었는지 확인한다.
        /// </summary>
        /// <returns>준비 완료 여부</returns>
        public bool IsReady()
        {
            return _isGameplayBindingReady;
        }

        /// <summary>
        /// 현재 binding 상태를 요약한 디버그 문자열을 반환한다.
        /// </summary>
        /// <returns>요약 문자열</returns>
        public string GetDebugSummary()
        {
            string readyStr = _isGameplayBindingReady ? " [READY]" : " [NOT_READY]";
            string placeholderStr = _isGeneratedPlaceholderContent ? " [PLACEHOLDER]" : "";
            string userStr = _isUserAssignedFinalContent ? " [USER_ASSET]" : "";

            return $"[{_requirementId}]{readyStr}{placeholderStr}{userStr} | " +
                $"Marker={_sourceMarkerId} " +
                $"Zone={_zoneId} " +
                $"Kind={_finalContentKind} " +
                $"Binding={_bindingKind} " +
                $"Adapter={_adapterKind} " +
                $"Cat={_runtimeCategory} " +
                $"Key={_runtimeKey} " +
                $"ProfileId={_profileId} " +
                $"Prefab={_finalPrefabName} " +
                $"Profile={_finalProfileName} " +
                $"Reason={_bindingReason}";
        }

        // ===== Private Methods =====

        /// <summary>
        /// finalContentKind를 기준으로 bindingKind를 매핑한다.
        /// </summary>
        private static WorldMapRuntimeFinalContentGameplayBindingKind MapBindingKind(WorldMapRuntimeFinalContentKind kind)
        {
            switch (kind)
            {
                case WorldMapRuntimeFinalContentKind.HarvestResource:
                    return WorldMapRuntimeFinalContentGameplayBindingKind.HarvestResourceCandidate;
                case WorldMapRuntimeFinalContentKind.HazardVolume:
                    return WorldMapRuntimeFinalContentGameplayBindingKind.HazardVolumeCandidate;
                case WorldMapRuntimeFinalContentKind.LandmarkProp:
                    return WorldMapRuntimeFinalContentGameplayBindingKind.LandmarkCandidate;
                case WorldMapRuntimeFinalContentKind.NarrativeLog:
                    return WorldMapRuntimeFinalContentGameplayBindingKind.NarrativeLogCandidate;
                case WorldMapRuntimeFinalContentKind.RouteTrigger:
                    return WorldMapRuntimeFinalContentGameplayBindingKind.RouteTriggerCandidate;
                case WorldMapRuntimeFinalContentKind.DebugMarker:
                    return WorldMapRuntimeFinalContentGameplayBindingKind.DebugCandidate;
                case WorldMapRuntimeFinalContentKind.Unknown:
                    return WorldMapRuntimeFinalContentGameplayBindingKind.Unknown;
                case WorldMapRuntimeFinalContentKind.None:
                default:
                    return WorldMapRuntimeFinalContentGameplayBindingKind.None;
            }
        }

        /// <summary>
        /// 모든 필수 필드가 유효한지 검사하고 _isGameplayBindingReady와 _bindingReason을 갱신한다.
        /// </summary>
        private void ValidateBinding()
        {
            var reasons = new System.Collections.Generic.List<string>();

            if (string.IsNullOrEmpty(_sourceMarkerId))
                reasons.Add("SourceMarkerId is empty");
            if (string.IsNullOrEmpty(_zoneId))
                reasons.Add("ZoneId is empty");
            if (string.IsNullOrEmpty(_runtimeCategory))
                reasons.Add("RuntimeCategory is empty");
            if (string.IsNullOrEmpty(_profileId))
                reasons.Add("ProfileId is empty");
            if (string.IsNullOrEmpty(_requirementId))
                reasons.Add("RequirementId is empty");
            if (_finalContentKind == WorldMapRuntimeFinalContentKind.None)
                reasons.Add("FinalContentKind is None");
            if (_bindingKind == WorldMapRuntimeFinalContentGameplayBindingKind.None)
                reasons.Add("BindingKind is None");

            if (reasons.Count == 0)
            {
                _isGameplayBindingReady = true;
                _bindingReason = "All fields valid.";
            }
            else
            {
                _isGameplayBindingReady = false;
                _bindingReason = string.Join("; ", reasons);
            }
        }
    }
}
