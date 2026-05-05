using UnityEngine;
using Project.Gameplay.World.Content;

namespace Project.Gameplay.World.Harvest
{
    /// <summary>
    /// WorldMapRuntimeGameplayConsumerContract에서 HarvestResourceConsumer만 추출해 만든
    /// read-only candidate data class. MonoBehaviour가 아니라 [System.Serializable] class로,
    /// 기존 Harvest 시스템이 안전하게 조회할 수 있는 bridge data 역할을 한다.
    /// UnityEditor API를 사용하지 않으므로 runtime/editor 양쪽에서 안전하게 사용 가능.
    /// </summary>
    [System.Serializable]
    public class WorldMapRuntimeHarvestResourceCandidate
    {
        // ===== Serialized Fields =====

        [SerializeField, Tooltip("대응하는 source marker의 고유 식별자")]
        private string _sourceMarkerId;

        [SerializeField, Tooltip("이 candidate가 속한 Zone ID (예: A1, B5, F6)")]
        private string _zoneId;

        [SerializeField, Tooltip("Resolver가 결정한 RuntimeCategory (예: HarvestResource)")]
        private string _runtimeCategory;

        [SerializeField, Tooltip("Resolver가 결정한 RuntimeKey (prefab/profile 식별 키, 예: iron)")]
        private string _runtimeKey;

        [SerializeField, Tooltip("매칭된 Spawn Profile Entry의 ProfileId")]
        private string _profileId;

        [SerializeField, Tooltip("매칭된 Requirement Entry의 고유 식별자")]
        private string _requirementId;

        [SerializeField, Tooltip("최종 교체에 사용한 final prefab의 이름")]
        private string _finalPrefabName;

        [SerializeField, Tooltip("최종 교체에 사용한 final profile의 이름")]
        private string _finalProfileName;

        [SerializeField, Tooltip("runtimeCategory 기반으로 매핑된 최종 content 종류")]
        private WorldMapRuntimeFinalContentKind _finalContentKind = WorldMapRuntimeFinalContentKind.None;

        [SerializeField, Tooltip("finalContentKind 기반으로 매핑된 gameplay binding 종류")]
        private WorldMapRuntimeFinalContentGameplayBindingKind _bindingKind = WorldMapRuntimeFinalContentGameplayBindingKind.None;

        [SerializeField, Tooltip("bindingKind 기반으로 매핑된 consumer 종류")]
        private WorldMapRuntimeGameplayConsumerKind _consumerKind = WorldMapRuntimeGameplayConsumerKind.None;

        [SerializeField, Tooltip("이 candidate의 월드 좌표")]
        private Vector3 _worldPosition;

        [SerializeField, Tooltip("이 candidate가 generated placeholder final content인지 여부")]
        private bool _isGeneratedPlaceholderContent;

        [SerializeField, Tooltip("이 candidate가 사용자가 직접 할당한 final content인지 여부")]
        private bool _isUserAssignedFinalContent;

        [SerializeField, Tooltip("이 candidate가 유효한 상태인지 여부")]
        private bool _isReady;

        [SerializeField, Tooltip("candidate 상태에 대한 설명/이유")]
        private string _reason;

        // ===== Public Getters =====

        /// <summary>대응하는 source marker의 고유 식별자</summary>
        public string SourceMarkerId => _sourceMarkerId;

        /// <summary>이 candidate가 속한 Zone ID (예: "A1", "B5", "F6")</summary>
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

        /// <summary>runtimeCategory 기반으로 매핑된 최종 content 종류</summary>
        public WorldMapRuntimeFinalContentKind FinalContentKind => _finalContentKind;

        /// <summary>finalContentKind 기반으로 매핑된 gameplay binding 종류</summary>
        public WorldMapRuntimeFinalContentGameplayBindingKind BindingKind => _bindingKind;

        /// <summary>bindingKind 기반으로 매핑된 consumer 종류</summary>
        public WorldMapRuntimeGameplayConsumerKind ConsumerKind => _consumerKind;

        /// <summary>이 candidate의 월드 좌표</summary>
        public Vector3 WorldPosition => _worldPosition;

        /// <summary>이 candidate가 generated placeholder final content인지 여부</summary>
        public bool IsGeneratedPlaceholderContent => _isGeneratedPlaceholderContent;

        /// <summary>이 candidate가 사용자가 직접 할당한 final content인지 여부</summary>
        public bool IsUserAssignedFinalContent => _isUserAssignedFinalContent;

        /// <summary>이 candidate가 유효한 상태인지 여부</summary>
        public bool IsReady => _isReady;

        /// <summary>candidate 상태에 대한 설명/이유</summary>
        public string Reason => _reason;

        // ===== Public API =====

        /// <summary>
        /// WorldMapRuntimeGameplayConsumerContract의 정보를 기반으로 이 candidate data를 구성한다.
        /// ConsumerKind가 HarvestResourceConsumer가 아니면 isReady=false로 설정된다.
        /// </summary>
        /// <param name="contract">구성에 사용할 consumer contract</param>
        public void Configure(WorldMapRuntimeGameplayConsumerContract contract)
        {
            if (contract == null)
            {
                // contract가 null이면 모든 필드를 초기화
                _sourceMarkerId = string.Empty;
                _zoneId = string.Empty;
                _runtimeCategory = string.Empty;
                _runtimeKey = string.Empty;
                _profileId = string.Empty;
                _requirementId = string.Empty;
                _finalPrefabName = string.Empty;
                _finalProfileName = string.Empty;
                _finalContentKind = WorldMapRuntimeFinalContentKind.None;
                _bindingKind = WorldMapRuntimeFinalContentGameplayBindingKind.None;
                _consumerKind = WorldMapRuntimeGameplayConsumerKind.None;
                _worldPosition = Vector3.zero;
                _isGeneratedPlaceholderContent = false;
                _isUserAssignedFinalContent = false;
                _isReady = false;
                _reason = "Contract is null";
                return;
            }

            // ConsumerKind가 HarvestResourceConsumer가 아니면 isReady=false
            if (contract.ConsumerKind != WorldMapRuntimeGameplayConsumerKind.HarvestResourceConsumer)
            {
                _sourceMarkerId = contract.SourceMarkerId ?? string.Empty;
                _zoneId = contract.ZoneId ?? string.Empty;
                _runtimeCategory = contract.RuntimeCategory ?? string.Empty;
                _runtimeKey = contract.RuntimeKey ?? string.Empty;
                _profileId = contract.ProfileId ?? string.Empty;
                _requirementId = contract.RequirementId ?? string.Empty;
                _finalPrefabName = contract.FinalPrefabName ?? string.Empty;
                _finalProfileName = contract.FinalProfileName ?? string.Empty;
                _finalContentKind = contract.FinalContentKind;
                _bindingKind = contract.BindingKind;
                _consumerKind = contract.ConsumerKind;
                _worldPosition = contract.transform != null ? contract.transform.position : Vector3.zero;
                _isGeneratedPlaceholderContent = contract.IsGeneratedPlaceholderContent;
                _isUserAssignedFinalContent = contract.IsUserAssignedFinalContent;
                _isReady = false;
                _reason = $"ConsumerKind is {contract.ConsumerKind}, expected HarvestResourceConsumer";
                return;
            }

            // contract 값들을 복사
            _sourceMarkerId = contract.SourceMarkerId ?? string.Empty;
            _zoneId = contract.ZoneId ?? string.Empty;
            _runtimeCategory = contract.RuntimeCategory ?? string.Empty;
            _runtimeKey = contract.RuntimeKey ?? string.Empty;
            _profileId = contract.ProfileId ?? string.Empty;
            _requirementId = contract.RequirementId ?? string.Empty;
            _finalPrefabName = contract.FinalPrefabName ?? string.Empty;
            _finalProfileName = contract.FinalProfileName ?? string.Empty;
            _finalContentKind = contract.FinalContentKind;
            _bindingKind = contract.BindingKind;
            _consumerKind = contract.ConsumerKind;

            // worldPosition은 contract.transform.position 사용
            _worldPosition = contract.transform != null ? contract.transform.position : Vector3.zero;

            // generated placeholder 여부와 user-assigned 여부는 contract 값 복사
            _isGeneratedPlaceholderContent = contract.IsGeneratedPlaceholderContent;
            _isUserAssignedFinalContent = contract.IsUserAssignedFinalContent;

            // 유효성 검사: 모든 필수 필드가 채워져 있는지 확인
            ValidateCandidate();
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
        /// 지정한 runtimeKey와 일치하는지 확인한다.
        /// </summary>
        /// <param name="runtimeKey">비교할 RuntimeKey</param>
        /// <returns>일치 여부</returns>
        public bool IsRuntimeKey(string runtimeKey)
        {
            return string.Equals(_runtimeKey, runtimeKey, System.StringComparison.Ordinal);
        }

        /// <summary>
        /// 지정한 profileId와 일치하는지 확인한다.
        /// </summary>
        /// <param name="profileId">비교할 ProfileId</param>
        /// <returns>일치 여부</returns>
        public bool IsProfile(string profileId)
        {
            return string.Equals(_profileId, profileId, System.StringComparison.Ordinal);
        }

        /// <summary>
        /// 현재 candidate 상태를 요약한 디버그 문자열을 반환한다.
        /// </summary>
        /// <returns>요약 문자열</returns>
        public string GetDebugSummary()
        {
            string readyStr = _isReady ? " [READY]" : " [NOT_READY]";
            string placeholderStr = _isGeneratedPlaceholderContent ? " [PLACEHOLDER]" : "";
            string userStr = _isUserAssignedFinalContent ? " [USER_ASSET]" : "";

            return $"[{_requirementId}]{readyStr}{placeholderStr}{userStr} | " +
                $"Marker={_sourceMarkerId} " +
                $"Zone={_zoneId} " +
                $"Kind={_finalContentKind} " +
                $"Binding={_bindingKind} " +
                $"Consumer={_consumerKind} " +
                $"Cat={_runtimeCategory} " +
                $"Key={_runtimeKey} " +
                $"ProfileId={_profileId} " +
                $"Prefab={_finalPrefabName} " +
                $"Profile={_finalProfileName} " +
                $"Pos=({_worldPosition.x:F1},{_worldPosition.y:F1},{_worldPosition.z:F1}) " +
                $"Reason={_reason}";
        }

        // ===== Private Methods =====

        /// <summary>
        /// 모든 필수 필드가 유효한지 검사하고 _isReady와 _reason을 갱신한다.
        /// sourceMarkerId, zoneId, runtimeCategory, profileId, requirementId가 비어 있으면 isReady=false.
        /// runtimeKey는 비어 있을 수도 있으나 현재 HarvestResource는 iron 등 값이 있으므로 reason에 명시.
        /// </summary>
        private void ValidateCandidate()
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
            if (_consumerKind == WorldMapRuntimeGameplayConsumerKind.None)
                reasons.Add("ConsumerKind is None");

            // runtimeKey는 비어 있을 수도 있으나 현재 HarvestResource는 iron 등 값이 있으므로 reason에 명시
            if (string.IsNullOrEmpty(_runtimeKey))
                reasons.Add("RuntimeKey is empty (may be expected for some categories)");

            if (reasons.Count == 0)
            {
                _isReady = true;
                _reason = "Ready";
            }
            else
            {
                _isReady = false;
                _reason = string.Join("; ", reasons);
            }
        }
    }
}
