using System.Text;
using UnityEngine;

namespace Project.Gameplay.World.Harvest
{
    /// <summary>
    /// RuntimeFinalContentInstances 하위 final content object에 부착되는 얇은 metadata component.
    /// 기존 Harvest 시스템이 나중에 이 component/query를 통해 target 후보를 안전하게 읽을 수 있게 한다.
    /// WorldMapRuntimeHarvestInteractionCandidate의 정보를 runtime target adapter로 변환하여 보관한다.
    /// UnityEditor API를 사용하지 않으므로 runtime/editor 양쪽에서 안전하게 사용 가능.
    /// Awake/Update/LateUpdate/코루틴을 사용하지 않는다.
    /// Collider, trigger, interaction event를 추가하지 않는다.
    /// </summary>
    public class WorldMapRuntimeHarvestInteractionTargetAdapter : MonoBehaviour
    {
        // ===== Serialized Fields =====

        [SerializeField, Tooltip("대응하는 source marker의 고유 식별자")]
        private string _sourceMarkerId;

        [SerializeField, Tooltip("이 target이 속한 Zone ID (예: A1, B5, F6)")]
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

        [SerializeField, Tooltip("runtimeKey 기반으로 매핑된 상호작용 후보 종류")]
        private WorldMapRuntimeHarvestInteractionCandidateKind _interactionKind = WorldMapRuntimeHarvestInteractionCandidateKind.None;

        [SerializeField, Tooltip("interactionKind에서 매핑된 runtime target 종류")]
        private WorldMapRuntimeHarvestInteractionTargetKind _targetKind = WorldMapRuntimeHarvestInteractionTargetKind.None;

        [SerializeField, Tooltip("이 target의 월드 좌표")]
        private Vector3 _worldPosition;

        [SerializeField, Tooltip("이 target이 generated placeholder final content인지 여부")]
        private bool _isGeneratedPlaceholderContent;

        [SerializeField, Tooltip("이 target이 사용자가 직접 할당한 final content인지 여부")]
        private bool _isUserAssignedFinalContent;

        [SerializeField, Tooltip("이 target이 유효한 상태인지 여부")]
        private bool _isTargetReady;

        [SerializeField, Tooltip("target 상태에 대한 설명/이유")]
        private string _targetReason;

        // ===== Public Getters =====

        /// <summary>대응하는 source marker의 고유 식별자</summary>
        public string SourceMarkerId => _sourceMarkerId;

        /// <summary>이 target이 속한 Zone ID (예: "A1", "B5", "F6")</summary>
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

        /// <summary>runtimeKey 기반으로 매핑된 상호작용 후보 종류</summary>
        public WorldMapRuntimeHarvestInteractionCandidateKind InteractionKind => _interactionKind;

        /// <summary>interactionKind에서 매핑된 runtime target 종류</summary>
        public WorldMapRuntimeHarvestInteractionTargetKind TargetKind => _targetKind;

        /// <summary>이 target의 월드 좌표</summary>
        public Vector3 WorldPosition => _worldPosition;

        /// <summary>이 target이 generated placeholder final content인지 여부</summary>
        public bool IsGeneratedPlaceholderContent => _isGeneratedPlaceholderContent;

        /// <summary>이 target이 사용자가 직접 할당한 final content인지 여부</summary>
        public bool IsUserAssignedFinalContent => _isUserAssignedFinalContent;

        /// <summary>이 target이 유효한 상태인지 여부</summary>
        public bool IsTargetReady => _isTargetReady;

        /// <summary>target 상태에 대한 설명/이유</summary>
        public string TargetReason => _targetReason;

        // ===== Public API =====

        /// <summary>
        /// WorldMapRuntimeHarvestInteractionCandidate의 정보를 기반으로 이 target adapter를 구성한다.
        /// candidate가 null이거나 유효하지 않으면 isTargetReady=false로 설정된다.
        /// </summary>
        /// <param name="candidate">구성에 사용할 harvest interaction candidate</param>
        public void Configure(WorldMapRuntimeHarvestInteractionCandidate candidate)
        {
            if (candidate == null)
            {
                // candidate가 null이면 모든 필드를 초기화
                ClearFields();
                _isTargetReady = false;
                _targetReason = "Candidate is null";
                return;
            }

            // candidate.IsReady() == false면 isTargetReady=false
            if (!candidate.IsReady)
            {
                ClearFields();
                _isTargetReady = false;
                _targetReason = "Source candidate is not ready";
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
            _interactionKind = candidate.InteractionKind;
            _worldPosition = candidate.WorldPosition;
            _isGeneratedPlaceholderContent = candidate.IsGeneratedPlaceholderContent;
            _isUserAssignedFinalContent = candidate.IsUserAssignedFinalContent;

            // interactionKind -> targetKind 매핑
            _targetKind = MapInteractionKindToTargetKind(_interactionKind);

            // 유효성 검사
            ValidateTarget();
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
        /// 지정한 targetKind와 일치하는지 확인한다.
        /// </summary>
        /// <param name="kind">비교할 TargetKind</param>
        /// <returns>일치 여부</returns>
        public bool IsTargetKind(WorldMapRuntimeHarvestInteractionTargetKind kind)
        {
            return _targetKind == kind;
        }

        /// <summary>
        /// 이 target adapter가 유효한 상태인지 확인한다.
        /// </summary>
        /// <returns>유효 여부</returns>
        public bool IsReady()
        {
            return _isTargetReady;
        }

        /// <summary>
        /// 현재 target adapter 상태를 요약한 디버그 문자열을 반환한다.
        /// </summary>
        /// <returns>요약 문자열</returns>
        public string GetDebugSummary()
        {
            string readyStr = _isTargetReady ? " [READY]" : " [NOT_READY]";
            string placeholderStr = _isGeneratedPlaceholderContent ? " [PLACEHOLDER]" : "";
            string userStr = _isUserAssignedFinalContent ? " [USER_ASSET]" : "";

            return $"[{_requirementId}]{readyStr}{placeholderStr}{userStr} | " +
                $"Marker={_sourceMarkerId} " +
                $"Zone={_zoneId} " +
                $"InteractionKind={_interactionKind} " +
                $"TargetKind={_targetKind} " +
                $"Cat={_runtimeCategory} " +
                $"Key={_runtimeKey} " +
                $"ProfileId={_profileId} " +
                $"Prefab={_finalPrefabName} " +
                $"Profile={_finalProfileName} " +
                $"Pos=({_worldPosition.x:F1},{_worldPosition.y:F1},{_worldPosition.z:F1}) " +
                $"Reason={_targetReason}";
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
            _interactionKind = WorldMapRuntimeHarvestInteractionCandidateKind.None;
            _targetKind = WorldMapRuntimeHarvestInteractionTargetKind.None;
            _worldPosition = Vector3.zero;
            _isGeneratedPlaceholderContent = false;
            _isUserAssignedFinalContent = false;
        }

        /// <summary>
        /// WorldMapRuntimeHarvestInteractionCandidateKind를 WorldMapRuntimeHarvestInteractionTargetKind로 매핑한다.
        /// None -> None, Unknown -> Unknown, 그 외는 직접 매핑.
        /// </summary>
        /// <param name="kind">매핑할 InteractionKind</param>
        /// <returns>매핑된 TargetKind</returns>
        private static WorldMapRuntimeHarvestInteractionTargetKind MapInteractionKindToTargetKind(
            WorldMapRuntimeHarvestInteractionCandidateKind kind)
        {
            switch (kind)
            {
                case WorldMapRuntimeHarvestInteractionCandidateKind.ResourceNode:
                    return WorldMapRuntimeHarvestInteractionTargetKind.ResourceNodeTarget;
                case WorldMapRuntimeHarvestInteractionCandidateKind.ScrapNode:
                    return WorldMapRuntimeHarvestInteractionTargetKind.ScrapNodeTarget;
                case WorldMapRuntimeHarvestInteractionCandidateKind.SensorNode:
                    return WorldMapRuntimeHarvestInteractionTargetKind.SensorNodeTarget;
                case WorldMapRuntimeHarvestInteractionCandidateKind.DataNode:
                    return WorldMapRuntimeHarvestInteractionTargetKind.DataNodeTarget;
                case WorldMapRuntimeHarvestInteractionCandidateKind.BatteryNode:
                    return WorldMapRuntimeHarvestInteractionTargetKind.BatteryNodeTarget;
                case WorldMapRuntimeHarvestInteractionCandidateKind.CommunicationNode:
                    return WorldMapRuntimeHarvestInteractionTargetKind.CommunicationNodeTarget;
                case WorldMapRuntimeHarvestInteractionCandidateKind.ResearchNode:
                    return WorldMapRuntimeHarvestInteractionTargetKind.ResearchNodeTarget;
                case WorldMapRuntimeHarvestInteractionCandidateKind.Unknown:
                    return WorldMapRuntimeHarvestInteractionTargetKind.Unknown;
                default:
                    return WorldMapRuntimeHarvestInteractionTargetKind.None;
            }
        }

        /// <summary>
        /// 모든 필수 필드가 유효한지 검사하고 _isTargetReady와 _targetReason을 갱신한다.
        /// sourceMarkerId, zoneId, runtimeCategory, profileId, requirementId가 비어 있으면 isTargetReady=false.
        /// targetKind == None이면 isTargetReady=false.
        /// targetKind == Unknown은 isTargetReady=true로 유지하되 reason에 기록.
        /// </summary>
        private void ValidateTarget()
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

            // runtimeKey는 None일 수도 있으므로 빈 값만으로 FAIL 처리하지 않는다.
            // 단 HarvestResource 계열에서는 runtimeKey가 있으면 그대로 보존.
            if (string.IsNullOrEmpty(_runtimeKey))
                reasons.Add("RuntimeKey is empty, mapped to Unknown");

            // targetKind == None이면 isTargetReady=false
            if (_targetKind == WorldMapRuntimeHarvestInteractionTargetKind.None)
                reasons.Add("TargetKind is None");

            // targetKind == Unknown은 WARN 대상이지만 isTargetReady=true로 유지
            if (_targetKind == WorldMapRuntimeHarvestInteractionTargetKind.Unknown && !string.IsNullOrEmpty(_runtimeKey))
                reasons.Add("Unknown runtime key, fallback target");

            if (reasons.Count == 0)
            {
                _isTargetReady = true;
                _targetReason = "Ready";
            }
            else
            {
                // Unknown만 있는 경우는 isTargetReady=true로 유지
                bool onlyUnknownWarning = reasons.Count == 1 &&
                    reasons[0] == "Unknown runtime key, fallback target";

                if (onlyUnknownWarning)
                {
                    _isTargetReady = true;
                    _targetReason = reasons[0];
                }
                else
                {
                    _isTargetReady = false;
                    _targetReason = string.Join("; ", reasons);
                }
            }
        }
    }
}
