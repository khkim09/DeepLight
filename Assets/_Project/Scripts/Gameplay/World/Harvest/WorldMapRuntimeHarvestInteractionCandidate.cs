using UnityEngine;
using System.Text;

namespace Project.Gameplay.World.Harvest
{
    /// <summary>
    /// WorldMapRuntimeHarvestResourceCandidate를 Harvest interaction 후보 데이터로 변환한 read-only data class.
    /// MonoBehaviour가 아니며, 기존 Harvest 시스템이 안전하게 조회할 수 있는 adapter 역할을 한다.
    /// UnityEditor API를 사용하지 않으므로 runtime/editor 양쪽에서 안전하게 사용 가능.
    /// </summary>
    [System.Serializable]
    public class WorldMapRuntimeHarvestInteractionCandidate
    {
        // ===== Serialized Fields =====

        [SerializeField, Tooltip("대응하는 source marker의 고유 식별자")]
        private string _sourceMarkerId;

        [SerializeField, Tooltip("이 candidate가 속한 Zone ID (예: A1, B5, F6)")]
        private string _zoneId;

        [SerializeField, Tooltip("Resolver가 결정한 RuntimeCategory")]
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

        [SerializeField, Tooltip("이 candidate의 월드 좌표")]
        private Vector3 _worldPosition;

        [SerializeField, Tooltip("runtimeKey 기반으로 매핑된 상호작용 후보 종류")]
        private WorldMapRuntimeHarvestInteractionCandidateKind _interactionKind = WorldMapRuntimeHarvestInteractionCandidateKind.None;

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

        /// <summary>이 candidate의 월드 좌표</summary>
        public Vector3 WorldPosition => _worldPosition;

        /// <summary>runtimeKey 기반으로 매핑된 상호작용 후보 종류</summary>
        public WorldMapRuntimeHarvestInteractionCandidateKind InteractionKind => _interactionKind;

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
        /// WorldMapRuntimeHarvestResourceCandidate의 정보를 기반으로 이 interaction candidate data를 구성한다.
        /// candidate가 null이거나 유효하지 않으면 isReady=false로 설정된다.
        /// </summary>
        /// <param name="candidate">구성에 사용할 harvest resource candidate</param>
        public void Configure(WorldMapRuntimeHarvestResourceCandidate candidate)
        {
            if (candidate == null)
            {
                // candidate가 null이면 모든 필드를 초기화
                ClearFields();
                _isReady = false;
                _reason = "Candidate is null";
                return;
            }

            // candidate.IsReady() == false면 isReady=false
            if (!candidate.IsReady)
            {
                ClearFields();
                _isReady = false;
                _reason = "Source candidate is not ready";
                return;
            }

            // RuntimeCategory가 "HarvestResource"가 아니면 isReady=false
            if (candidate.RuntimeCategory != "HarvestResource")
            {
                ClearFields();
                _isReady = false;
                _reason = $"RuntimeCategory is '{candidate.RuntimeCategory}', expected 'HarvestResource'";
                return;
            }

            // candidate 값들을 복사
            _sourceMarkerId = candidate.SourceMarkerId ?? string.Empty;
            _zoneId = candidate.ZoneId ?? string.Empty;
            _runtimeCategory = candidate.RuntimeCategory ?? string.Empty;
            _runtimeKey = candidate.RuntimeKey ?? string.Empty;
            _profileId = candidate.ProfileId ?? string.Empty;
            _requirementId = candidate.RequirementId ?? string.Empty;
            _finalPrefabName = candidate.FinalPrefabName ?? string.Empty;
            _finalProfileName = candidate.FinalProfileName ?? string.Empty;
            _worldPosition = candidate.WorldPosition;
            _isGeneratedPlaceholderContent = candidate.IsGeneratedPlaceholderContent;
            _isUserAssignedFinalContent = candidate.IsUserAssignedFinalContent;

            // runtimeKey 기반으로 interactionKind 매핑
            _interactionKind = MapRuntimeKeyToInteractionKind(_runtimeKey);

            // 유효성 검사
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
        /// 지정한 interactionKind와 일치하는지 확인한다.
        /// </summary>
        /// <param name="kind">비교할 InteractionKind</param>
        /// <returns>일치 여부</returns>
        public bool IsInteractionKind(WorldMapRuntimeHarvestInteractionCandidateKind kind)
        {
            return _interactionKind == kind;
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
                $"Kind={_interactionKind} " +
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
        /// 모든 필드를 빈 값으로 초기화한다.
        /// </summary>
        private void ClearFields()
        {
            _sourceMarkerId = string.Empty;
            _zoneId = string.Empty;
            _runtimeCategory = string.Empty;
            _runtimeKey = string.Empty;
            _profileId = string.Empty;
            _requirementId = string.Empty;
            _finalPrefabName = string.Empty;
            _finalProfileName = string.Empty;
            _worldPosition = Vector3.zero;
            _interactionKind = WorldMapRuntimeHarvestInteractionCandidateKind.None;
            _isGeneratedPlaceholderContent = false;
            _isUserAssignedFinalContent = false;
        }

        /// <summary>
        /// runtimeKey를 기반으로 WorldMapRuntimeHarvestInteractionCandidateKind를 매핑한다.
        /// 알 수 없는 key나 빈 key는 Unknown으로 매핑한다.
        /// </summary>
        /// <param name="runtimeKey">매핑할 RuntimeKey</param>
        /// <returns>매핑된 InteractionKind</returns>
        private static WorldMapRuntimeHarvestInteractionCandidateKind MapRuntimeKeyToInteractionKind(string runtimeKey)
        {
            if (string.IsNullOrEmpty(runtimeKey))
                return WorldMapRuntimeHarvestInteractionCandidateKind.Unknown;

            // 대소문자 구분 없이 비교
            string key = runtimeKey.Trim().ToLowerInvariant();

            switch (key)
            {
                case "iron":
                    return WorldMapRuntimeHarvestInteractionCandidateKind.ResourceNode;
                case "copper":
                    return WorldMapRuntimeHarvestInteractionCandidateKind.ResourceNode;
                case "iron scrap":
                    return WorldMapRuntimeHarvestInteractionCandidateKind.ScrapNode;
                case "sensor":
                    return WorldMapRuntimeHarvestInteractionCandidateKind.SensorNode;
                case "data":
                    return WorldMapRuntimeHarvestInteractionCandidateKind.DataNode;
                case "battery":
                    return WorldMapRuntimeHarvestInteractionCandidateKind.BatteryNode;
                case "communication":
                    return WorldMapRuntimeHarvestInteractionCandidateKind.CommunicationNode;
                case "research":
                    return WorldMapRuntimeHarvestInteractionCandidateKind.ResearchNode;
                default:
                    return WorldMapRuntimeHarvestInteractionCandidateKind.Unknown;
            }
        }

        /// <summary>
        /// 모든 필수 필드가 유효한지 검사하고 _isReady와 _reason을 갱신한다.
        /// sourceMarkerId, zoneId, runtimeCategory, profileId, requirementId가 비어 있으면 isReady=false.
        /// interactionKind == None이면 isReady=false.
        /// interactionKind == Unknown은 isReady=true로 유지하되 reason에 기록.
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

            // runtimeKey는 가능하면 non-empty여야 하지만, 누락 가능성을 고려해 Unknown으로 매핑하고 reason에 기록
            if (string.IsNullOrEmpty(_runtimeKey))
                reasons.Add("RuntimeKey is empty, mapped to Unknown");

            // interactionKind == None이면 isReady=false
            if (_interactionKind == WorldMapRuntimeHarvestInteractionCandidateKind.None)
                reasons.Add("InteractionKind is None");

            // interactionKind == Unknown은 WARN 대상이지만 isReady=true로 유지
            if (_interactionKind == WorldMapRuntimeHarvestInteractionCandidateKind.Unknown && !string.IsNullOrEmpty(_runtimeKey))
                reasons.Add("Unknown runtime key, fallback interaction candidate");

            if (reasons.Count == 0)
            {
                _isReady = true;
                _reason = "Ready";
            }
            else
            {
                // Unknown만 있는 경우는 isReady=true로 유지
                bool onlyUnknownWarning = reasons.Count == 1 &&
                    reasons[0] == "Unknown runtime key, fallback interaction candidate";

                if (onlyUnknownWarning)
                {
                    _isReady = true;
                    _reason = reasons[0];
                }
                else
                {
                    _isReady = false;
                    _reason = string.Join("; ", reasons);
                }
            }
        }
    }
}
