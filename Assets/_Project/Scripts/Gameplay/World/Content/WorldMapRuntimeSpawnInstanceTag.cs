using UnityEngine;

namespace Project.Gameplay.World.Content
{
    /// <summary>
    /// Preview Spawn Instance에 부착되어 어떤 placeholder/plan/profile에서 생성되었는지 추적하는 컴포넌트.
    /// Phase 14.10-J-2: Preview Spawn Instance 생성/검증 단계.
    /// UnityEditor API를 사용하지 않으므로 Gameplay assembly에서 안전하게 사용 가능.
    /// </summary>
    public class WorldMapRuntimeSpawnInstanceTag : MonoBehaviour
    {
        [SerializeField, Tooltip("이 instance를 생성한 source RuntimePlaceholder의 이름")]
        private string _sourcePlaceholderName;

        [SerializeField, Tooltip("이 instance가 속한 Zone ID (예: A1, B5, F6)")]
        private string _zoneId;

        [SerializeField, Tooltip("이 instance가 대응하는 source marker의 고유 식별자")]
        private string _markerId;

        [SerializeField, Tooltip("source marker의 타입 (Resource/Hazard/Landmark/Narrative/Route/Debug/Unknown)")]
        private WorldMapZoneContentMarkerType _markerType = WorldMapZoneContentMarkerType.Unknown;

        [SerializeField, Tooltip("Resolver가 결정한 RuntimeCategory (예: HarvestResource, Hazard, WorldProp, NarrativeLog, RouteTrigger, Unknown)")]
        private string _runtimeCategory;

        [SerializeField, Tooltip("Resolver가 결정한 RuntimeKey (prefab/profile 식별 키)")]
        private string _runtimeKey;

        [SerializeField, Tooltip("매칭된 Spawn Profile Entry의 ProfileId")]
        private string _profileId;

        [SerializeField, Tooltip("매칭된 Prefab의 이름")]
        private string _prefabName;

        [SerializeField, Tooltip("이 plan이 fallback profile entry를 통해 매칭되었는지 여부")]
        private bool _isFallbackProfile;

        [SerializeField, Tooltip("매칭된 Prefab이 Editor/Test용 placeholder prefab인지 여부")]
        private bool _isEditorPlaceholderPrefab;

        [SerializeField, Tooltip("이 instance가 preview instance인지 여부 (true=preview, false=실제 치환 instance)")]
        private bool _isPreviewInstance;

        // ===== Public Getters =====

        /// <summary>이 instance를 생성한 source RuntimePlaceholder의 이름</summary>
        public string SourcePlaceholderName => _sourcePlaceholderName;

        /// <summary>이 instance가 속한 Zone ID (예: "A1", "B5", "F6")</summary>
        public string ZoneId => _zoneId;

        /// <summary>이 instance가 대응하는 source marker의 고유 식별자</summary>
        public string MarkerId => _markerId;

        /// <summary>source marker의 타입</summary>
        public WorldMapZoneContentMarkerType MarkerType => _markerType;

        /// <summary>Resolver가 결정한 RuntimeCategory</summary>
        public string RuntimeCategory => _runtimeCategory;

        /// <summary>Resolver가 결정한 RuntimeKey</summary>
        public string RuntimeKey => _runtimeKey;

        /// <summary>매칭된 Spawn Profile Entry의 ProfileId</summary>
        public string ProfileId => _profileId;

        /// <summary>매칭된 Prefab의 이름</summary>
        public string PrefabName => _prefabName;

        /// <summary>이 plan이 fallback profile entry를 통해 매칭되었는지 여부</summary>
        public bool IsFallbackProfile => _isFallbackProfile;

        /// <summary>매칭된 Prefab이 Editor/Test용 placeholder prefab인지 여부</summary>
        public bool IsEditorPlaceholderPrefab => _isEditorPlaceholderPrefab;

        /// <summary>이 instance가 preview instance인지 여부</summary>
        public bool IsPreviewInstance => _isPreviewInstance;

        /// <summary>
        /// Replacement Plan의 값을 기반으로 모든 필드를 설정한다.
        /// plan이 null이면 아무 작업도 수행하지 않는다.
        /// </summary>
        /// <param name="plan">Replacement Plan 데이터</param>
        /// <param name="previewInstance">preview instance 여부 (true=preview, false=실제 치환)</param>
        public void Configure(WorldMapRuntimeSpawnReplacementPlan plan, bool previewInstance)
        {
            // plan이 null이면 return
            if (plan == null)
            {
                UnityEngine.Debug.LogWarning("[WorldMapRuntimeSpawnInstanceTag] Configure called with null plan. Skipping.");
                return;
            }

            // plan 값들을 필드에 복사
            _sourcePlaceholderName = plan.PlaceholderName;
            _zoneId = plan.ZoneId;
            _markerId = plan.MarkerId;
            _markerType = plan.MarkerType;
            _runtimeCategory = plan.RuntimeCategory;
            _runtimeKey = plan.RuntimeKey;
            _profileId = plan.ProfileId;
            _prefabName = plan.PrefabName;
            _isFallbackProfile = plan.IsFallbackProfile;
            _isEditorPlaceholderPrefab = plan.IsEditorPlaceholderPrefab;

            // preview instance 플래그 설정
            _isPreviewInstance = previewInstance;
        }
    }
}
