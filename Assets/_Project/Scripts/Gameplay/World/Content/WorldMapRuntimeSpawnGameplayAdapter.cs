using UnityEngine;

namespace Project.Gameplay.World.Content
{
    /// <summary>
    /// Runtime spawned instance에 붙는 얇은 gameplay-facing adapter component.
    /// WorldMapRuntimeSpawnInstanceTag의 metadata를 gameplay 시스템이 안전하게 읽을 수 있도록
    /// category별 adapterKind를 제공한다. 실제 gameplay prefab 교체나 interaction 로직은 포함하지 않는다.
    /// </summary>
    public class WorldMapRuntimeSpawnGameplayAdapter : MonoBehaviour
    {
        // ===== Serialized Fields =====

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

        [SerializeField, Tooltip("이 adapter가 runtime에서 사용 가능한 상태인지 여부 (Configure() 호출 후 결정)")]
        private bool _isRuntimeAdapterReady;

        [SerializeField, Tooltip("runtimeCategory 기반으로 결정된 gameplay adapter 종류")]
        private WorldMapRuntimeGameplayAdapterKind _adapterKind = WorldMapRuntimeGameplayAdapterKind.None;

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

        /// <summary>이 adapter가 runtime에서 사용 가능한 상태인지 여부</summary>
        public bool IsRuntimeAdapterReady => _isRuntimeAdapterReady;

        /// <summary>runtimeCategory 기반으로 결정된 gameplay adapter 종류</summary>
        public WorldMapRuntimeGameplayAdapterKind AdapterKind => _adapterKind;

        // ===== Public API =====

        /// <summary>
        /// WorldMapRuntimeSpawnInstanceTag의 값을 adapter 필드로 복사하고,
        /// runtimeCategory 기반으로 adapterKind를 설정한다.
        /// tag가 null이면 isRuntimeAdapterReady=false로 설정하고 return한다.
        /// </summary>
        /// <param name="tag">값을 복사할 source tag</param>
        public void Configure(WorldMapRuntimeSpawnInstanceTag tag)
        {
            // tag가 null이면 isRuntimeAdapterReady=false 처리 후 return
            if (tag == null)
            {
                _isRuntimeAdapterReady = false;
                UnityEngine.Debug.LogWarning("[WorldMapRuntimeSpawnGameplayAdapter] Configure called with null tag. Adapter marked as not ready.", this);
                return;
            }

            // tag 값을 adapter 필드로 복사
            _sourcePlaceholderName = tag.SourcePlaceholderName;
            _zoneId = tag.ZoneId;
            _markerId = tag.MarkerId;
            _markerType = tag.MarkerType;
            _runtimeCategory = tag.RuntimeCategory;
            _runtimeKey = tag.RuntimeKey;
            _profileId = tag.ProfileId;
            _prefabName = tag.PrefabName;
            _isFallbackProfile = tag.IsFallbackProfile;
            _isEditorPlaceholderPrefab = tag.IsEditorPlaceholderPrefab;

            // runtimeCategory 기반으로 adapterKind 설정
            _adapterKind = ResolveAdapterKind(_runtimeCategory);

            // 필수값 ZoneId/MarkerId/ProfileId/RuntimeCategory non-empty이면 isRuntimeAdapterReady=true
            bool zoneIdValid = !string.IsNullOrEmpty(_zoneId);
            bool markerIdValid = !string.IsNullOrEmpty(_markerId);
            bool profileIdValid = !string.IsNullOrEmpty(_profileId);
            bool categoryValid = !string.IsNullOrEmpty(_runtimeCategory);

            _isRuntimeAdapterReady = zoneIdValid && markerIdValid && profileIdValid && categoryValid;
        }

        /// <summary>
        /// 지정한 category 문자열이 이 adapter의 RuntimeCategory와 일치하는지 확인한다.
        /// 대소문자를 구분한다.
        /// </summary>
        /// <param name="category">비교할 category 문자열</param>
        /// <returns>일치하면 true</returns>
        public bool IsCategory(string category)
        {
            return _runtimeCategory == category;
        }

        /// <summary>
        /// 지정한 zone 문자열이 이 adapter의 ZoneId와 일치하는지 확인한다.
        /// 대소문자를 구분한다.
        /// </summary>
        /// <param name="zone">비교할 zone 문자열</param>
        /// <returns>일치하면 true</returns>
        public bool IsZone(string zone)
        {
            return _zoneId == zone;
        }

        /// <summary>
        /// 지정한 marker 문자열이 이 adapter의 MarkerId와 일치하는지 확인한다.
        /// 대소문자를 구분한다.
        /// </summary>
        /// <param name="marker">비교할 marker 문자열</param>
        /// <returns>일치하면 true</returns>
        public bool IsMarker(string marker)
        {
            return _markerId == marker;
        }

        /// <summary>
        /// 이 adapter의 현재 상태를 요약한 디버그 문자열을 반환한다.
        /// </summary>
        /// <returns>디버그 요약 문자열</returns>
        public string GetDebugSummary()
        {
            return $"[WorldMapRuntimeSpawnGameplayAdapter] " +
                $"ZoneId={_zoneId}, " +
                $"MarkerId={_markerId}, " +
                $"Category={_runtimeCategory}, " +
                $"AdapterKind={_adapterKind}, " +
                $"ProfileId={_profileId}, " +
                $"Ready={_isRuntimeAdapterReady}, " +
                $"Prefab={_prefabName}, " +
                $"Fallback={_isFallbackProfile}";
        }

        // ===== Internal Helpers =====

        /// <summary>
        /// runtimeCategory 문자열을 WorldMapRuntimeGameplayAdapterKind로 매핑한다.
        /// 알 수 없는 값이나 빈 값은 Unknown을 반환한다.
        /// </summary>
        /// <param name="runtimeCategory">매핑할 category 문자열</param>
        /// <returns>매핑된 adapterKind</returns>
        private static WorldMapRuntimeGameplayAdapterKind ResolveAdapterKind(string runtimeCategory)
        {
            if (string.IsNullOrEmpty(runtimeCategory))
                return WorldMapRuntimeGameplayAdapterKind.Unknown;

            switch (runtimeCategory)
            {
                case "HarvestResource":
                    return WorldMapRuntimeGameplayAdapterKind.HarvestResource;
                case "Hazard":
                    return WorldMapRuntimeGameplayAdapterKind.Hazard;
                case "Landmark":
                    return WorldMapRuntimeGameplayAdapterKind.Landmark;
                case "NarrativeLog":
                    return WorldMapRuntimeGameplayAdapterKind.NarrativeLog;
                case "RouteTrigger":
                    return WorldMapRuntimeGameplayAdapterKind.RouteTrigger;
                case "Debug":
                    return WorldMapRuntimeGameplayAdapterKind.Debug;
                default:
                    // "WorldProp" 등 알 수 없는 category는 Unknown으로 처리
                    return WorldMapRuntimeGameplayAdapterKind.Unknown;
            }
        }
    }
}
