using UnityEngine;

namespace Project.Gameplay.World.Content
{
    /// <summary>
    /// Runtime Spawn Profile Database의 placeholder prefab에 부착되는 태그 컴포넌트.
    /// profile metadata를 저장하여 prefab이 어떤 spawn profile entry에 연결되었는지 식별한다.
    /// Editor/Test용 placeholder prefab에만 사용되며, 실제 gameplay prefab에는 불필요.
    /// </summary>
    public sealed class WorldMapRuntimeSpawnPlaceholderTag : MonoBehaviour
    {
        [SerializeField, Tooltip("연결된 spawn profile entry의 ProfileId")]
        private string profileId;

        [SerializeField, Tooltip("연결된 spawn profile entry의 MarkerType")]
        private WorldMapZoneContentMarkerType markerType;

        [SerializeField, Tooltip("연결된 spawn profile entry의 RuntimeCategory")]
        private string runtimeCategory;

        [SerializeField, Tooltip("연결된 spawn profile entry의 RuntimeKey")]
        private string runtimeKey;

        [SerializeField, Tooltip("이 entry가 fallback profile인지 여부")]
        private bool isFallbackProfile;

        [SerializeField, Tooltip("이 prefab이 Editor/Test용 placeholder인지 여부. true이면 자동 생성된 placeholder로 간주")]
        private bool isEditorPlaceholder = true;

        // ===== Public Getters =====

        /// <summary>연결된 spawn profile entry의 ProfileId</summary>
        public string ProfileId => profileId;

        /// <summary>연결된 spawn profile entry의 MarkerType</summary>
        public WorldMapZoneContentMarkerType MarkerType => markerType;

        /// <summary>연결된 spawn profile entry의 RuntimeCategory</summary>
        public string RuntimeCategory => runtimeCategory;

        /// <summary>연결된 spawn profile entry의 RuntimeKey</summary>
        public string RuntimeKey => runtimeKey;

        /// <summary>이 entry가 fallback profile인지 여부</summary>
        public bool IsFallbackProfile => isFallbackProfile;

        /// <summary>이 prefab이 Editor/Test용 placeholder인지 여부</summary>
        public bool IsEditorPlaceholder => isEditorPlaceholder;

        // ===== Public Methods =====

        /// <summary>
        /// placeholder prefab의 profile metadata를 설정한다.
        /// PrefabUtility.SaveAsPrefabAsset 저장 전에 호출하여 metadata를 기록한다.
        /// </summary>
        /// <param name="profileId">연결할 spawn profile entry의 ProfileId</param>
        /// <param name="markerType">연결할 spawn profile entry의 MarkerType</param>
        /// <param name="runtimeCategory">연결할 spawn profile entry의 RuntimeCategory</param>
        /// <param name="runtimeKey">연결할 spawn profile entry의 RuntimeKey</param>
        /// <param name="isFallbackProfile">fallback profile 여부</param>
        public void Configure(string profileId, WorldMapZoneContentMarkerType markerType, string runtimeCategory, string runtimeKey, bool isFallbackProfile)
        {
            // profileId는 null/empty 허용하지 않음
            this.profileId = !string.IsNullOrEmpty(profileId) ? profileId : "Unconfigured";
            this.markerType = markerType;
            this.runtimeCategory = runtimeCategory ?? string.Empty;
            this.runtimeKey = runtimeKey ?? string.Empty;
            this.isFallbackProfile = isFallbackProfile;
            // Configure() 호출 시 isEditorPlaceholder는 true 유지 (Editor/Test용)
            this.isEditorPlaceholder = true;
        }
    }
}
