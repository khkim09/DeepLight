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
    /// TargetData는 resolver를 통해 resolve된 HarvestTargetSO를 반환하며,
    /// resolver가 없거나 매핑이 없으면 null을 반환한다.
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

        // ===== Resolver Integration =====

        /// <summary>resolver를 통해 resolve된 harvest target data (lazy resolve)</summary>
        private ResolvedGeneratedHarvestTargetData _resolvedData;

        /// <summary>resolver 참조 (AssignDataResolver 또는 lazy find로 설정)</summary>
        private WorldMapGeneratedHarvestTargetDataResolver _dataResolver;

        /// <summary>resolver를 찾았는지 여부 (lazy find 1회 시도)</summary>
        private bool _resolverSearched;

        /// <summary>resolvedData가 이미 resolve되었는지 여부</summary>
        private bool _resolved;

        /// <summary>이 target이 consume되었는지 여부</summary>
        private bool _consumed;

        /// <summary>명시적으로 resolver가 할당되었는지 여부</summary>
        private bool _hasExplicitResolver;

        // ===== IHarvestTarget Implementation =====

        /// <summary>
        /// IHarvestTarget.TargetData: resolver를 통해 resolve된 HarvestTargetSO를 반환한다.
        /// resolver가 없거나 매핑이 없으면 null을 반환한다.
        /// null 반환 시 기존 HarvestResolver는 base recovery chance를 0으로 처리하므로,
        /// O-16에서 generated target 전용 resolver bridge가 필요하다.
        /// </summary>
        public HarvestTargetSO TargetData
        {
            get
            {
                // lazy resolve
                EnsureResolved();
                return _resolvedData?.HarvestTargetSO;
            }
        }

        /// <summary>
        /// IHarvestTarget.IsAvailable: 다음 조건을 모두 만족해야 true:
        /// - generated target ready (_isReady)
        /// - sourceMarkerId/profileId/requirementId/runtimeKey 유효
        /// - resolver 결과가 ready
        /// - 아직 Consume되지 않음
        /// </summary>
        public bool IsAvailable
        {
            get
            {
                if (_consumed)
                    return false;

                if (!_isReady)
                    return false;

                // 필수 ID 유효성 검사
                if (string.IsNullOrEmpty(_sourceMarkerId))
                    return false;
                if (string.IsNullOrEmpty(_profileId))
                    return false;
                if (string.IsNullOrEmpty(_runtimeKey))
                    return false;

                // resolver 결과 ready 검사
                EnsureResolved();
                if (_resolvedData == null)
                    return false;
                if (!_resolvedData.IsReady)
                    return false;

                return true;
            }
        }

        /// <summary>
        /// IHarvestTarget.Consume: 상태만 consumed 처리한다.
        /// reward 지급/인벤토리 추가/시간 패널티는 O-16에서 실제 HarvestResolver/Reward 시스템에 연결한다.
        /// </summary>
        public void Consume()
        {
            if (_consumed || !_isReady)
                return;

            // consumed 상태로만 전환 (O-16에서 실제 보상/소비 로직 연결)
            _consumed = true;
            _isReady = false;
            _reason = "Consumed (placeholder - O-16 will implement actual reward/penalty flow)";
            UnityEngine.Debug.Log($"[GeneratedHarvestTarget] Consume called on '{_sourceMarkerId}' (runtimeKey={_runtimeKey}). Actual reward/penalty flow deferred to O-16.", gameObject);
        }

        /// <summary>
        /// IHarvestTarget.OnClawCollision: 기존 Harvest 흐름과 호환되도록 안전한 no-op.
        /// interaction attempt flag만 기록한다.
        /// </summary>
        public void OnClawCollision()
        {
            // generated target은 claw collision을 직접 처리하지 않음.
            // O-16에서 필요시 HarvestResolver/Reward 시스템에 연결.
            // 현재는 no-op으로 유지.
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

        /// <summary>이 target이 consume되었는지 여부</summary>
        public bool IsConsumed => _consumed;

        /// <summary>resolver를 통해 resolve된 data (없으면 null). 호출 시 자동 resolve 시도.</summary>
        public ResolvedGeneratedHarvestTargetData ResolvedData
        {
            get
            {
                EnsureResolved();
                return _resolvedData;
            }
        }

        /// <summary>resolved data가 존재하고 IsReady인지 여부</summary>
        public bool HasResolvedData
        {
            get
            {
                EnsureResolved();
                return _resolvedData != null && _resolvedData.IsReady;
            }
        }

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

            // resolver cache 초기화 (다음 IsAvailable/TargetData 호출 시 재resolve)
            _resolverSearched = false;
            _dataResolver = null;
            _resolved = false;
            _resolvedData = null;
            _consumed = false;
            _hasExplicitResolver = false;

            // 유효성 검사
            ValidateTarget();
        }

        /// <summary>
        /// WorldMapGeneratedHarvestTargetDataResolver 참조를 명시적으로 할당한다.
        /// null이어도 예외를 내지 않으며, lazy find보다 우선한다.
        /// </summary>
        /// <param name="resolver">할당할 resolver 참조 (null 허용)</param>
        public void AssignDataResolver(WorldMapGeneratedHarvestTargetDataResolver resolver)
        {
            _dataResolver = resolver;
            _hasExplicitResolver = resolver != null;
            _resolverSearched = true; // 명시적 할당이 lazy find를 대체
            _resolved = false; // 재resolve 필요
            _resolvedData = null;
        }

        /// <summary>
        /// 현재 할당된 resolver를 통해 resolved data를 갱신한다.
        /// resolver가 없으면 false를 반환하고 _resolvedData는 null로 유지된다.
        /// </summary>
        /// <returns>resolve 성공 여부 (fallback 포함)</returns>
        public bool RefreshResolvedData()
        {
            // resolver 확인
            if (_dataResolver == null && !_hasExplicitResolver)
            {
                // lazy find 시도
                if (!_resolverSearched)
                {
                    _resolverSearched = true;
                    _dataResolver = GetComponentInParent<WorldMapGeneratedHarvestTargetDataResolver>();
                }
            }

            if (_dataResolver == null)
            {
                _resolved = true;
                _resolvedData = null;
                return false;
            }

            // resolver를 통해 data resolve
            bool result = _dataResolver.TryResolve(this, out _resolvedData);
            _resolved = true;

            // resolve 결과가 true이고 data가 유효하면 _hasResolvedData=true
            if (result && _resolvedData != null && _resolvedData.IsReady)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// resolved data를 안전하게 가져온다.
        /// </summary>
        /// <param name="data">resolve 결과 (out)</param>
        /// <returns>resolved data가 유효한 상태인지 여부</returns>
        public bool TryGetResolvedData(out ResolvedGeneratedHarvestTargetData data)
        {
            EnsureResolved();
            data = _resolvedData;
            return _resolvedData != null && _resolvedData.IsReady;
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
        /// resolved 상태를 포함한다 (예: [RESOLVED:Iron Scrap], [FALLBACK:Iron], [NO_DATA]).
        /// </summary>
        /// <returns>요약 문자열</returns>
        public string GetDebugSummary()
        {
            string readyStr = _isReady ? " [READY]" : " [NOT_READY]";
            string consumedStr = _consumed ? " [CONSUMED]" : "";
            string placeholderStr = _isGeneratedPlaceholderContent ? " [PLACEHOLDER]" : "";
            string userStr = _isUserAssignedFinalContent ? " [USER_ASSET]" : "";

            string resolvedInfo = string.Empty;
            if (_resolvedData != null)
            {
                if (_resolvedData.IsFallback)
                {
                    resolvedInfo = $" [FALLBACK:{_resolvedData.DisplayName}]";
                }
                else if (_resolvedData.HarvestTargetSO != null)
                {
                    resolvedInfo = $" [RESOLVED:{_resolvedData.HarvestTargetSO.name}]";
                }
                else
                {
                    resolvedInfo = $" [RESOLVED:{_resolvedData.DisplayName}]";
                }
            }
            else
            {
                resolvedInfo = " [NO_DATA]";
            }

            return $"[{_requirementId}]{readyStr}{consumedStr}{placeholderStr}{userStr}{resolvedInfo} | " +
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
            _resolvedData = null;
            _dataResolver = null;
            _resolverSearched = false;
            _resolved = false;
            _consumed = false;
            _hasExplicitResolver = false;
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

        /// <summary>
        /// Resolver를 찾고 data를 resolve한다. (lazy initialization)
        /// 명시적 할당(_hasExplicitResolver)이 있으면 그것을 우선 사용하고,
        /// 없으면 parent hierarchy에서 WorldMapGeneratedHarvestTargetDataResolver를 찾는다.
        /// </summary>
        private void EnsureResolved()
        {
            // 이미 resolve되었으면 skip
            if (_resolved)
                return;

            // resolver 찾기 (명시적 할당 우선, 없으면 최초 1회 lazy find)
            if (_dataResolver == null && !_hasExplicitResolver && !_resolverSearched)
            {
                _resolverSearched = true;
                _dataResolver = GetComponentInParent<WorldMapGeneratedHarvestTargetDataResolver>();
            }

            // resolver가 없으면 resolve 불가
            if (_dataResolver == null)
            {
                _resolved = true; // 재시도 방지
                return;
            }

            // resolver를 통해 data resolve
            _dataResolver.TryResolve(this, out _resolvedData);
            _resolved = true;
        }
    }
}
