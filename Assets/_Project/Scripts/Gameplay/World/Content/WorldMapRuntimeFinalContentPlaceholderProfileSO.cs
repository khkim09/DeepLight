using UnityEngine;

namespace Project.Gameplay.World.Content
{
    /// <summary>
    /// M-3 단계에서 생성되는 generated placeholder final content profile ScriptableObject.
    /// requirement entry의 finalProfile이 null일 때 자동 생성되어 연결된다.
    /// 사용자가 나중에 real final profile로 교체할 수 있다.
    /// </summary>
    [CreateAssetMenu(
        fileName = "RuntimeFinalContentPlaceholderProfile",
        menuName = "DeepLight/World/Content/Runtime Final Content Placeholder Profile",
        order = 216)]
    public class WorldMapRuntimeFinalContentPlaceholderProfileSO : ScriptableObject
    {
        [SerializeField, Tooltip("대응하는 requirement entry의 고유 식별자")]
        private string _requirementId;

        [SerializeField, Tooltip("이 placeholder profile이 나타내는 최종 content 종류")]
        private WorldMapRuntimeFinalContentKind _finalContentKind = WorldMapRuntimeFinalContentKind.None;

        [SerializeField, Tooltip("이 placeholder profile이 대응하는 gameplay adapter 종류")]
        private WorldMapRuntimeGameplayAdapterKind _adapterKind = WorldMapRuntimeGameplayAdapterKind.None;

        [SerializeField, Tooltip("매칭 대상 RuntimeCategory")]
        private string _runtimeCategory;

        [SerializeField, Tooltip("매칭 대상 RuntimeKey")]
        private string _runtimeKey;

        [SerializeField, Tooltip("매칭 대상 ProfileId")]
        private string _profileId;

        [SerializeField, Tooltip("표시 이름. 에디터/디버그용")]
        private string _displayName;

        [SerializeField, Tooltip("상세 설명. 에디터/디버그용")]
        private string _description;

        [SerializeField, Tooltip("이 requirement가 fallback requirement인지 여부")]
        private bool _isFallbackRequirement;

        [SerializeField, Tooltip("이 profile이 generated placeholder인지 여부. true이면 자동 생성된 placeholder임")]
        private bool _isGeneratedPlaceholder = true;

        // ===== Public Properties =====

        /// <summary>대응하는 requirement entry의 고유 식별자</summary>
        public string RequirementId => _requirementId;

        /// <summary>이 placeholder profile이 나타내는 최종 content 종류</summary>
        public WorldMapRuntimeFinalContentKind FinalContentKind => _finalContentKind;

        /// <summary>이 placeholder profile이 대응하는 gameplay adapter 종류</summary>
        public WorldMapRuntimeGameplayAdapterKind AdapterKind => _adapterKind;

        /// <summary>매칭 대상 RuntimeCategory</summary>
        public string RuntimeCategory => _runtimeCategory;

        /// <summary>매칭 대상 RuntimeKey</summary>
        public string RuntimeKey => _runtimeKey;

        /// <summary>매칭 대상 ProfileId</summary>
        public string ProfileId => _profileId;

        /// <summary>표시 이름</summary>
        public string DisplayName => _displayName;

        /// <summary>상세 설명</summary>
        public string Description => _description;

        /// <summary>이 requirement가 fallback requirement인지 여부</summary>
        public bool IsFallbackRequirement => _isFallbackRequirement;

        /// <summary>이 profile이 generated placeholder인지 여부</summary>
        public bool IsGeneratedPlaceholder => _isGeneratedPlaceholder;

        // ===== Public API =====

        /// <summary>
        /// requirement entry의 정보로 이 profile을 구성한다.
        /// isGeneratedPlaceholder는 항상 true로 설정된다.
        /// </summary>
        /// <param name="entry">구성에 사용할 requirement entry</param>
        public void Configure(WorldMapRuntimeFinalContentRequirementEntry entry)
        {
            if (entry == null)
            {
                UnityEngine.Debug.LogWarning("[FinalContentPlaceholderProfile] Configure called with null entry.");
                return;
            }

            _requirementId = entry.RequirementId ?? string.Empty;
            _finalContentKind = entry.FinalContentKind;
            _adapterKind = entry.AdapterKind;
            _runtimeCategory = entry.RuntimeCategory ?? string.Empty;
            _runtimeKey = entry.RuntimeKey ?? string.Empty;
            _profileId = entry.ProfileId ?? string.Empty;
            _displayName = entry.DisplayName ?? string.Empty;
            _description = entry.Description ?? string.Empty;
            _isFallbackRequirement = entry.IsFallbackRequirement;
            _isGeneratedPlaceholder = true; // 이 profile은 항상 generated placeholder로 표시
        }

        /// <summary>
        /// 현재 profile 상태를 요약한 디버그 문자열을 반환한다.
        /// </summary>
        /// <returns>요약 문자열</returns>
        public string GetDebugSummary()
        {
            string fallbackStr = _isFallbackRequirement ? " [FALLBACK]" : "";
            string generatedStr = _isGeneratedPlaceholder ? " [GENERATED]" : " [USER]";
            return $"[{_requirementId}]{fallbackStr}{generatedStr} | " +
                $"Kind={_finalContentKind} Adapter={_adapterKind} " +
                $"Cat={_runtimeCategory} Key={_runtimeKey} ProfileId={_profileId} " +
                $"DisplayName={_displayName}";
        }
    }
}
