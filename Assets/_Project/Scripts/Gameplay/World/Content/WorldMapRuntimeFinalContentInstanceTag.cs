using UnityEngine;

namespace Project.Gameplay.World.Content
{
    /// <summary>
    /// RuntimeFinalContentInstances 하위에 생성되는 최종 콘텐츠 인스턴스에 붙는 metadata tag.
    /// M-5 단계에서 생성된 final content instance의 식별/추적 정보를 보관한다.
    /// UnityEditor API를 사용하지 않으므로 runtime/editor 양쪽에서 안전하게 사용 가능.
    /// Update/LateUpdate/코루틴을 사용하지 않는다.
    /// </summary>
    public class WorldMapRuntimeFinalContentInstanceTag : MonoBehaviour
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

        [SerializeField, Tooltip("이 plan이 fallback profile entry를 통해 매칭되었는지 여부")]
        private bool _isFallbackProfile;

        [SerializeField, Tooltip("이 plan이 fallback requirement entry를 통해 매칭되었는지 여부")]
        private bool _isFallbackRequirement;

        [SerializeField, Tooltip("매칭된 finalPrefab이 generated placeholder인지 여부")]
        private bool _isGeneratedPlaceholderContent;

        [SerializeField, Tooltip("매칭된 finalPrefab이 사용자가 직접 할당한 final asset인지 여부")]
        private bool _isUserAssignedFinalContent;

        [SerializeField, Tooltip("이 instance가 M-5 Rebuild에 의해 생성된 final instance인지 여부")]
        private bool _isGeneratedFinalInstance;

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

        /// <summary>이 plan이 fallback profile entry를 통해 매칭되었는지 여부</summary>
        public bool IsFallbackProfile => _isFallbackProfile;

        /// <summary>이 plan이 fallback requirement entry를 통해 매칭되었는지 여부</summary>
        public bool IsFallbackRequirement => _isFallbackRequirement;

        /// <summary>매칭된 finalPrefab이 generated placeholder인지 여부</summary>
        public bool IsGeneratedPlaceholderContent => _isGeneratedPlaceholderContent;

        /// <summary>매칭된 finalPrefab이 사용자가 직접 할당한 final asset인지 여부</summary>
        public bool IsUserAssignedFinalContent => _isUserAssignedFinalContent;

        /// <summary>이 instance가 M-5 Rebuild에 의해 생성된 final instance인지 여부</summary>
        public bool IsGeneratedFinalInstance => _isGeneratedFinalInstance;

        // ===== Public API =====

        /// <summary>
        /// M-4 resolution plan의 정보로 이 태그를 구성한다.
        /// generatedFinalInstance는 항상 true로 설정된다.
        /// </summary>
        /// <param name="plan">구성에 사용할 resolution plan</param>
        /// <param name="generatedFinalInstance">이 instance가 M-5 Rebuild에 의해 생성된 final instance인지 여부</param>
        public void Configure(WorldMapRuntimeFinalContentResolutionPlan plan, bool generatedFinalInstance)
        {
            if (plan == null)
            {
                UnityEngine.Debug.LogWarning("[FinalContentInstanceTag] Configure called with null plan.");
                return;
            }

            _sourceInstanceName = plan.SourceInstanceName ?? string.Empty;
            _sourceMarkerId = plan.SourceMarkerId ?? string.Empty;
            _zoneId = plan.ZoneId ?? string.Empty;
            _runtimeCategory = plan.RuntimeCategory ?? string.Empty;
            _runtimeKey = plan.RuntimeKey ?? string.Empty;
            _profileId = plan.ProfileId ?? string.Empty;
            _requirementId = plan.RequirementId ?? string.Empty;
            _finalPrefabName = plan.FinalPrefabName ?? string.Empty;
            _finalProfileName = plan.FinalProfileName ?? string.Empty;
            _adapterKind = plan.AdapterKind;
            _finalContentKind = plan.FinalContentKind;
            _isFallbackProfile = plan.IsFallbackProfile;
            _isFallbackRequirement = plan.IsFallbackRequirement;
            _isGeneratedPlaceholderContent = plan.IsGeneratedFinalPlaceholder;
            _isUserAssignedFinalContent = plan.IsUserAssignedFinalAsset;
            _isGeneratedFinalInstance = generatedFinalInstance;
        }

        /// <summary>
        /// 현재 태그 상태를 요약한 디버그 문자열을 반환한다.
        /// </summary>
        /// <returns>요약 문자열</returns>
        public string GetDebugSummary()
        {
            string generatedStr = _isGeneratedFinalInstance ? " [GENERATED]" : " [MANUAL]";
            string placeholderStr = _isGeneratedPlaceholderContent ? " [PLACEHOLDER]" : "";
            string userStr = _isUserAssignedFinalContent ? " [USER_ASSET]" : "";
            string fallbackProfileStr = _isFallbackProfile ? " [FALLBACK_PROFILE]" : "";
            string fallbackReqStr = _isFallbackRequirement ? " [FALLBACK_REQ]" : "";

            return $"[{_requirementId}]{generatedStr}{placeholderStr}{userStr}{fallbackProfileStr}{fallbackReqStr} | " +
                $"Instance={_sourceInstanceName} " +
                $"Marker={_sourceMarkerId} " +
                $"Zone={_zoneId} " +
                $"Kind={_finalContentKind} " +
                $"Adapter={_adapterKind} " +
                $"Cat={_runtimeCategory} " +
                $"Key={_runtimeKey} " +
                $"ProfileId={_profileId} " +
                $"Prefab={_finalPrefabName} " +
                $"Profile={_finalProfileName}";
        }
    }
}
