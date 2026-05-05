using Project.Data.Harvest;
using Project.Gameplay.Harvest;
using UnityEngine;

namespace Project.Gameplay.World.Harvest
{
    /// <summary>
    /// generated ConsumerContext 하나를 기존 Harvest 시스템이 이해할 수 있는 target 후보처럼 표현하는 MonoBehaviour adapter.
    /// RuntimeFinalContentInstances 하위 final content object에 부착 가능.
    /// 직접 reward/time penalty/harvest result를 처리하지 않음.
    /// metadata + position + kind + runtimeKey + profileId만 제공.
    /// IHarvestTarget을 구현하여 기존 HarvestPointInteractor/HarvestModeCoordinator가
    /// 이 adapter를 기존 HarvestTargetBehaviour와 동일한 인터페이스로 취급할 수 있게 한다.
    /// 단, TargetData는 null을 반환하므로 기존 시스템이 TargetData에 의존하는 로직은
    /// O-11에서 별도 bridge 처리가 필요하다.
    /// </summary>
    public class WorldMapGeneratedHarvestTarget : MonoBehaviour, IHarvestTarget
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

        [SerializeField, Tooltip("runtimeKey 기반으로 매핑된 상호작용 후보 종류")]
        private WorldMapRuntimeHarvestInteractionCandidateKind _interactionKind = WorldMapRuntimeHarvestInteractionCandidateKind.None;

        [SerializeField, Tooltip("interactionKind에서 매핑된 runtime target 종류")]
        private WorldMapRuntimeHarvestInteractionTargetKind _targetKind = WorldMapRuntimeHarvestInteractionTargetKind.None;

        [SerializeField, Tooltip("이 target의 월드 좌표 (Configure 시점에 기록)")]
        private Vector3 _worldPosition;

        [SerializeField, Tooltip("이 target이 generated placeholder final content인지 여부")]
        private bool _isGeneratedPlaceholderContent;

        [SerializeField, Tooltip("이 target이 사용자가 직접 할당한 final content인지 여부")]
        private bool _isUserAssignedFinalContent;

        [SerializeField, Tooltip("이 target이 유효한 상태인지 여부")]
        private bool _isReady;

        [SerializeField, Tooltip("target 상태에 대한 설명/이유")]
        private string _reason;

        // ===== IHarvestTarget Implementation =====

        /// <summary>
        /// IHarvestTarget.TargetData: generated target은 SO 기반 TargetData가 없으므로 null 반환.
        /// O-11에서 필요시 HarvestTargetSO bridge를 추가할 수 있다.
        /// </summary>
        public HarvestTargetSO TargetData => null;

        /// <summary>
        /// IHarvestTarget.IsAvailable: _isReady 상태를 그대로 반환.
        /// generated target이 준비된 경우에만 available로 간주한다.
        /// </summary>
        public bool IsAvailable => _isReady;

        /// <summary>
        /// IHarvestTarget.Consume: generated target은 아직 실제 소비 로직을 처리하지 않음.
        /// O-11에서 실제 harvest result 처리 시 구현 예정.
        /// 현재는 _isReady = false로만 설정하고 로그를 남긴다.
        /// </summary>
        public void Consume()
        {
            if (!_isReady)
                return;

            // 아직 실제 보상/소비 로직은 연결하지 않음 (O-11 예약)
            _isReady = false;
            _reason = "Consumed (placeholder - O-11 will implement actual consumption)";
            UnityEngine.Debug.Log($"[GeneratedHarvestTarget] Consume called on '{_sourceMarkerId}' (placeholder). Actual harvest logic deferred to O-11.", gameObject);
        }

        /// <summary>
        /// IHarvestTarget.OnClawCollision: generated target은 claw collision을 처리하지 않음.
        /// O-11에서 필요시 구현 예정.
        /// </summary>
        public void OnClawCollision()
        {
            // generated target은 claw collision을 처리하지 않음 (O-11 예약)
        }

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
        public bool IsReady => _isReady;

        /// <summary>target 상태에 대한 설명/이유</summary>
        public string Reason => _reason;

        // ===== Public API =====

        /// <summary>
        /// ConsumerContext의 정보로 이 generated harvest target을 구성한다.
        /// context가 null이거나 IsReady=false면 _isReady=false로 설정된다.
        /// </summary>
        /// <param name="context">구성에 사용할 consumer context</param>
        public void Configure(WorldMapRuntimeHarvestInteractionTargetConsumerContext context)
        {
            if (context == null)
            {
                // context가 null이면 모든 필드를 초기화
                ClearFields();
                _isReady = false;
                _reason = "ConsumerContext is null";
                return;
            }

            // context.IsReady() == false면 _isReady=false
            if (!context.IsReady)
            {
                ClearFields();
                _isReady = false;
                _reason = "Source consumer context is not ready";
                return;
            }

            // context 값들을 복사
            _sourceMarkerId = context.SourceMarkerId ?? string.Empty;
            _zoneId = context.ZoneId ?? string.Empty;
            _runtimeCategory = context.RuntimeCategory ?? string.Empty;
            _runtimeKey = context.RuntimeKey ?? string.Empty;
            _profileId = context.ProfileId ?? string.Empty;
            _requirementId = context.RequirementId ?? string.Empty;
            _interactionKind = context.InteractionKind;
            _targetKind = context.TargetKind;
            _worldPosition = context.WorldPosition;
            _isGeneratedPlaceholderContent = context.IsGeneratedPlaceholderContent;
            _isUserAssignedFinalContent = context.IsUserAssignedFinalContent;

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
        /// 이 target의 월드 위치를 반환한다.
        /// </summary>
        /// <returns>월드 좌표</returns>
        public Vector3 GetWorldPosition()
        {
            return _worldPosition;
        }

        /// <summary>
        /// 현재 target 상태를 요약한 디버그 문자열을 반환한다.
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
            _interactionKind = WorldMapRuntimeHarvestInteractionCandidateKind.None;
            _targetKind = WorldMapRuntimeHarvestInteractionTargetKind.None;
            _worldPosition = Vector3.zero;
            _isGeneratedPlaceholderContent = false;
            _isUserAssignedFinalContent = false;
        }

        /// <summary>
        /// 모든 필수 필드가 유효한지 검사하고 _isReady와 _reason을 갱신한다.
        /// Ready 판정 조건:
        /// - SourceMarkerId/ZoneId/RuntimeCategory/ProfileId/RequirementId 비어 있으면 false
        /// - InteractionKind == None 이면 false
        /// - TargetKind == None 이면 false
        /// - 위 조건 통과 시 true
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
