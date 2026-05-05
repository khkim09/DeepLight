using System.Text;
using UnityEngine;

namespace Project.Gameplay.World.Harvest
{
    /// <summary>
    /// 기존 Harvest 시스템이 WorldMapRuntimeHarvestInteractionTargetAdapter를 직접 참조하지 않고
    /// 읽을 수 있는 read-only context data class.
    /// MonoBehaviour가 아니므로 Unity lifecycle에 종속되지 않는다.
    /// </summary>
    [System.Serializable]
    public class WorldMapRuntimeHarvestInteractionTargetConsumerContext
    {
        // ===== Fields =====

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

        [SerializeField, Tooltip("이 context가 유효한 상태인지 여부")]
        private bool _isReady;

        [SerializeField, Tooltip("context 상태에 대한 설명/이유")]
        private string _reason;

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

        /// <summary>이 context가 유효한 상태인지 여부</summary>
        public bool IsReady => _isReady;

        /// <summary>context 상태에 대한 설명/이유</summary>
        public string Reason => _reason;

        // ===== Public API =====

        /// <summary>
        /// WorldMapRuntimeHarvestInteractionTargetAdapter의 정보를 기반으로 이 context를 구성한다.
        /// target이 null이거나 유효하지 않으면 IsReady=false로 설정된다.
        /// </summary>
        /// <param name="target">구성에 사용할 target adapter</param>
        public void Configure(WorldMapRuntimeHarvestInteractionTargetAdapter target)
        {
            if (target == null)
            {
                // target이 null이면 모든 필드를 초기화
                ClearFields();
                _isReady = false;
                _reason = "Target adapter is null";
                return;
            }

            // target.IsReady() == false면 IsReady=false
            if (!target.IsReady())
            {
                ClearFields();
                _isReady = false;
                _reason = "Source target adapter is not ready";
                return;
            }

            // target adapter 값들을 복사
            _sourceMarkerId = target.SourceMarkerId ?? string.Empty;
            _zoneId = target.ZoneId ?? string.Empty;
            _runtimeCategory = target.RuntimeCategory ?? string.Empty;
            _runtimeKey = target.RuntimeKey ?? string.Empty;
            _profileId = target.ProfileId ?? string.Empty;
            _requirementId = target.RequirementId ?? string.Empty;
            _finalPrefabName = target.FinalPrefabName ?? string.Empty;
            _finalProfileName = target.FinalProfileName ?? string.Empty;
            _interactionKind = target.InteractionKind;
            _targetKind = target.TargetKind;
            _worldPosition = target.WorldPosition;
            _isGeneratedPlaceholderContent = target.IsGeneratedPlaceholderContent;
            _isUserAssignedFinalContent = target.IsUserAssignedFinalContent;

            // 유효성 검사
            ValidateContext();
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
        /// <param name="interactionKind">비교할 InteractionKind</param>
        /// <returns>일치 여부</returns>
        public bool IsInteractionKind(WorldMapRuntimeHarvestInteractionCandidateKind interactionKind)
        {
            return _interactionKind == interactionKind;
        }

        /// <summary>
        /// 지정한 targetKind와 일치하는지 확인한다.
        /// </summary>
        /// <param name="targetKind">비교할 TargetKind</param>
        /// <returns>일치 여부</returns>
        public bool IsTargetKind(WorldMapRuntimeHarvestInteractionTargetKind targetKind)
        {
            return _targetKind == targetKind;
        }

        /// <summary>
        /// 현재 context 상태를 요약한 디버그 문자열을 반환한다.
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
                $"InteractionKind={_interactionKind} " +
                $"TargetKind={_targetKind} " +
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
            _interactionKind = WorldMapRuntimeHarvestInteractionCandidateKind.None;
            _targetKind = WorldMapRuntimeHarvestInteractionTargetKind.None;
            _worldPosition = Vector3.zero;
            _isGeneratedPlaceholderContent = false;
            _isUserAssignedFinalContent = false;
        }

        /// <summary>
        /// 모든 필수 필드가 유효한지 검사하고 _isReady와 _reason을 갱신한다.
        /// Ready 판정 조건:
        /// - target == null 이면 false
        /// - target.IsReady() == false 이면 false
        /// - SourceMarkerId/ZoneId/RuntimeCategory/ProfileId/RequirementId 비어 있으면 false
        /// - InteractionKind == None 이면 false
        /// - TargetKind == None 이면 false
        /// - 위 조건 통과 시 true
        /// </summary>
        private void ValidateContext()
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
            if (string.IsNullOrEmpty(_runtimeKey))
                reasons.Add("RuntimeKey is empty");

            // InteractionKind == None이면 IsReady=false
            if (_interactionKind == WorldMapRuntimeHarvestInteractionCandidateKind.None)
                reasons.Add("InteractionKind is None");

            // TargetKind == None이면 IsReady=false
            if (_targetKind == WorldMapRuntimeHarvestInteractionTargetKind.None)
                reasons.Add("TargetKind is None");

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
