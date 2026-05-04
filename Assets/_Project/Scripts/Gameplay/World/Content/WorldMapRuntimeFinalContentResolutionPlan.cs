using UnityEngine;

namespace Project.Gameplay.World.Content
{
    /// <summary>
    /// M-4 단계에서 생성되는 Runtime Final Content Resolution Plan.
    /// 각 RuntimeSpawnedInstance가 어떤 final content prefab/profile로 교체될 수 있는지
    /// "최종 해석 계획"을 표현한다. 이 단계에서는 scene instance를 실제로 교체하지 않는다.
    /// UnityEditor API를 사용하지 않으므로 runtime/editor 양쪽에서 안전하게 사용 가능.
    /// </summary>
    [System.Serializable]
    public class WorldMapRuntimeFinalContentResolutionPlan
    {
        // ===== Serialized Fields =====

        [SerializeField, Tooltip("Source runtime instance의 GameObject 이름")]
        private string _sourceInstanceName;

        [SerializeField, Tooltip("이 instance가 대응하는 source marker의 고유 식별자")]
        private string _sourceMarkerId;

        [SerializeField, Tooltip("이 instance가 속한 Zone ID (예: A1, B5, F6)")]
        private string _zoneId;

        [SerializeField, Tooltip("Resolver가 결정한 RuntimeCategory (예: HarvestResource, Hazard, WorldProp, NarrativeLog, RouteTrigger, Unknown)")]
        private string _runtimeCategory;

        [SerializeField, Tooltip("Resolver가 결정한 RuntimeKey (prefab/profile 식별 키)")]
        private string _runtimeKey;

        [SerializeField, Tooltip("매칭된 Spawn Profile Entry의 ProfileId")]
        private string _profileId;

        [SerializeField, Tooltip("runtimeCategory 기반으로 결정된 gameplay adapter 종류")]
        private WorldMapRuntimeGameplayAdapterKind _adapterKind = WorldMapRuntimeGameplayAdapterKind.None;

        [SerializeField, Tooltip("adapterKind 기반으로 매핑된 최종 content 종류")]
        private WorldMapRuntimeFinalContentKind _finalContentKind = WorldMapRuntimeFinalContentKind.None;

        [SerializeField, Tooltip("매칭된 Requirement Entry의 고유 식별자")]
        private string _requirementId;

        [SerializeField, Tooltip("매칭된 Requirement Entry의 표시 이름")]
        private string _requirementDisplayName;

        [SerializeField, Tooltip("매칭된 Requirement Entry의 상세 설명")]
        private string _requirementDescription;

        [SerializeField, Tooltip("최종 교체에 사용할 final prefab의 이름")]
        private string _finalPrefabName;

        [SerializeField, Tooltip("최종 교체에 사용할 final prefab 참조")]
        private GameObject _finalPrefab;

        [SerializeField, Tooltip("최종 교체에 사용할 final profile의 이름")]
        private string _finalProfileName;

        [SerializeField, Tooltip("최종 교체에 사용할 final profile 참조 (구체 타입으로 강제하지 않고 Object로 받음)")]
        private Object _finalProfile;

        [SerializeField, Tooltip("Source instance의 월드 위치")]
        private Vector3 _worldPosition;

        [SerializeField, Tooltip("Source instance의 월드 회전")]
        private Quaternion _worldRotation;

        [SerializeField, Tooltip("Source instance의 월드 스케일")]
        private Vector3 _worldScale;

        [SerializeField, Tooltip("이 plan이 fallback profile entry를 통해 매칭되었는지 여부")]
        private bool _isFallbackProfile;

        [SerializeField, Tooltip("이 plan이 fallback requirement entry를 통해 매칭되었는지 여부")]
        private bool _isFallbackRequirement;

        [SerializeField, Tooltip("매칭된 Prefab이 Editor/Test용 placeholder prefab인지 여부")]
        private bool _isEditorPlaceholderPrefab;

        [SerializeField, Tooltip("매칭된 finalPrefab이 generated placeholder인지 여부")]
        private bool _isGeneratedFinalPlaceholder;

        [SerializeField, Tooltip("매칭된 finalPrefab이 사용자가 직접 할당한 final asset인지 여부")]
        private bool _isUserAssignedFinalAsset;

        [SerializeField, Tooltip("이 plan이 유효한지 여부 (모든 필수 조건 충족)")]
        private bool _isValidPlan;

        [SerializeField, Tooltip("plan이 유효하지 않은 경우 실패 사유")]
        private string _failureReason;

        // ===== Public Getters =====

        /// <summary>Source runtime instance의 GameObject 이름</summary>
        public string SourceInstanceName => _sourceInstanceName;

        /// <summary>이 instance가 대응하는 source marker의 고유 식별자</summary>
        public string SourceMarkerId => _sourceMarkerId;

        /// <summary>이 instance가 속한 Zone ID (예: "A1", "B5", "F6")</summary>
        public string ZoneId => _zoneId;

        /// <summary>Resolver가 결정한 RuntimeCategory</summary>
        public string RuntimeCategory => _runtimeCategory;

        /// <summary>Resolver가 결정한 RuntimeKey</summary>
        public string RuntimeKey => _runtimeKey;

        /// <summary>매칭된 Spawn Profile Entry의 ProfileId</summary>
        public string ProfileId => _profileId;

        /// <summary>runtimeCategory 기반으로 결정된 gameplay adapter 종류</summary>
        public WorldMapRuntimeGameplayAdapterKind AdapterKind => _adapterKind;

        /// <summary>adapterKind 기반으로 매핑된 최종 content 종류</summary>
        public WorldMapRuntimeFinalContentKind FinalContentKind => _finalContentKind;

        /// <summary>매칭된 Requirement Entry의 고유 식별자</summary>
        public string RequirementId => _requirementId;

        /// <summary>매칭된 Requirement Entry의 표시 이름</summary>
        public string RequirementDisplayName => _requirementDisplayName;

        /// <summary>매칭된 Requirement Entry의 상세 설명</summary>
        public string RequirementDescription => _requirementDescription;

        /// <summary>최종 교체에 사용할 final prefab의 이름</summary>
        public string FinalPrefabName => _finalPrefabName;

        /// <summary>최종 교체에 사용할 final prefab 참조</summary>
        public GameObject FinalPrefab => _finalPrefab;

        /// <summary>최종 교체에 사용할 final profile의 이름</summary>
        public string FinalProfileName => _finalProfileName;

        /// <summary>최종 교체에 사용할 final profile 참조 (Object 기반, 구체 타입으로 강제하지 않음)</summary>
        public Object FinalProfile => _finalProfile;

        /// <summary>Source instance의 월드 위치</summary>
        public Vector3 WorldPosition => _worldPosition;

        /// <summary>Source instance의 월드 회전</summary>
        public Quaternion WorldRotation => _worldRotation;

        /// <summary>Source instance의 월드 스케일</summary>
        public Vector3 WorldScale => _worldScale;

        /// <summary>이 plan이 fallback profile entry를 통해 매칭되었는지 여부</summary>
        public bool IsFallbackProfile => _isFallbackProfile;

        /// <summary>이 plan이 fallback requirement entry를 통해 매칭되었는지 여부</summary>
        public bool IsFallbackRequirement => _isFallbackRequirement;

        /// <summary>매칭된 Prefab이 Editor/Test용 placeholder prefab인지 여부</summary>
        public bool IsEditorPlaceholderPrefab => _isEditorPlaceholderPrefab;

        /// <summary>매칭된 finalPrefab이 generated placeholder인지 여부</summary>
        public bool IsGeneratedFinalPlaceholder => _isGeneratedFinalPlaceholder;

        /// <summary>매칭된 finalPrefab이 사용자가 직접 할당한 final asset인지 여부</summary>
        public bool IsUserAssignedFinalAsset => _isUserAssignedFinalAsset;

        /// <summary>이 plan이 유효한지 여부</summary>
        public bool IsValidPlan => _isValidPlan;

        /// <summary>plan이 유효하지 않은 경우 실패 사유</summary>
        public string FailureReason => _failureReason;

        // ===== Public API =====

        /// <summary>
        /// 이 plan이 유효한지 검사한다.
        /// isValidPlan == true, finalPrefab != null, finalProfile != null,
        /// sourceMarkerId/zoneId/profileId/requirementId/sourceInstanceName 모두 non-empty이어야 true.
        /// </summary>
        /// <returns>유효하면 true</returns>
        public bool IsValid()
        {
            if (!_isValidPlan) return false;
            if (_finalPrefab == null) return false;
            if (_finalProfile == null) return false;
            if (string.IsNullOrEmpty(_sourceMarkerId)) return false;
            if (string.IsNullOrEmpty(_zoneId)) return false;
            if (string.IsNullOrEmpty(_profileId)) return false;
            if (string.IsNullOrEmpty(_requirementId)) return false;
            if (string.IsNullOrEmpty(_sourceInstanceName)) return false;
            return true;
        }

        /// <summary>
        /// 이 plan의 현재 상태를 요약한 문자열을 반환한다.
        /// </summary>
        /// <returns>요약 문자열</returns>
        public string GetSummary()
        {
            string validStr = _isValidPlan ? "VALID" : "INVALID";
            string fallbackProfileStr = _isFallbackProfile ? " [FALLBACK_PROFILE]" : "";
            string fallbackReqStr = _isFallbackRequirement ? " [FALLBACK_REQ]" : "";
            string generatedStr = _isGeneratedFinalPlaceholder ? " [GENERATED]" : "";
            string userStr = _isUserAssignedFinalAsset ? " [USER_ASSET]" : "";
            string editorStr = _isEditorPlaceholderPrefab ? " [EDITOR_PLACEHOLDER]" : "";
            string failureStr = string.IsNullOrEmpty(_failureReason) ? "" : $" Failure={_failureReason}";

            return $"[{validStr}]{fallbackProfileStr}{fallbackReqStr}{generatedStr}{userStr}{editorStr}" +
                $" Instance={_sourceInstanceName}" +
                $" Marker={_sourceMarkerId}" +
                $" Zone={_zoneId}" +
                $" Category={_runtimeCategory}" +
                $" Key={_runtimeKey}" +
                $" ProfileId={_profileId}" +
                $" AdapterKind={_adapterKind}" +
                $" FinalContentKind={_finalContentKind}" +
                $" ReqId={_requirementId}" +
                $" Prefab={_finalPrefabName}" +
                $" Profile={_finalProfileName}" +
                $" Pos=({_worldPosition.x:F1},{_worldPosition.y:F1},{_worldPosition.z:F1})" +
                $"{failureStr}";
        }

        /// <summary>
        /// 이 plan의 필드를 설정한다. WorldMapRuntimeFinalContentResolver.TryBuildPlan()에서 호출된다.
        /// </summary>
        public void Configure(
            string sourceInstanceName,
            string sourceMarkerId,
            string zoneId,
            string runtimeCategory,
            string runtimeKey,
            string profileId,
            WorldMapRuntimeGameplayAdapterKind adapterKind,
            WorldMapRuntimeFinalContentKind finalContentKind,
            string requirementId,
            string requirementDisplayName,
            string requirementDescription,
            GameObject finalPrefab,
            Object finalProfile,
            Vector3 worldPosition,
            Quaternion worldRotation,
            Vector3 worldScale,
            bool isFallbackProfile,
            bool isFallbackRequirement,
            bool isEditorPlaceholderPrefab,
            bool isGeneratedFinalPlaceholder,
            bool isUserAssignedFinalAsset,
            bool isValidPlan,
            string failureReason)
        {
            _sourceInstanceName = sourceInstanceName ?? string.Empty;
            _sourceMarkerId = sourceMarkerId ?? string.Empty;
            _zoneId = zoneId ?? string.Empty;
            _runtimeCategory = runtimeCategory ?? string.Empty;
            _runtimeKey = runtimeKey ?? string.Empty;
            _profileId = profileId ?? string.Empty;
            _adapterKind = adapterKind;
            _finalContentKind = finalContentKind;
            _requirementId = requirementId ?? string.Empty;
            _requirementDisplayName = requirementDisplayName ?? string.Empty;
            _requirementDescription = requirementDescription ?? string.Empty;
            _finalPrefabName = finalPrefab != null ? finalPrefab.name : "null";
            _finalPrefab = finalPrefab;
            _finalProfileName = finalProfile != null ? finalProfile.name : "null";
            _finalProfile = finalProfile;
            _worldPosition = worldPosition;
            _worldRotation = worldRotation;
            _worldScale = worldScale;
            _isFallbackProfile = isFallbackProfile;
            _isFallbackRequirement = isFallbackRequirement;
            _isEditorPlaceholderPrefab = isEditorPlaceholderPrefab;
            _isGeneratedFinalPlaceholder = isGeneratedFinalPlaceholder;
            _isUserAssignedFinalAsset = isUserAssignedFinalAsset;
            _isValidPlan = isValidPlan;
            _failureReason = failureReason ?? string.Empty;
        }
    }
}
