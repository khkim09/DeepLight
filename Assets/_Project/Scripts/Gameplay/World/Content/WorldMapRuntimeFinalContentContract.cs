using UnityEngine;

namespace Project.Gameplay.World.Content
{
    /// <summary>
    /// RuntimeSpawnedInstances 하위 runtime instance에 부착되는 최종 content readiness contract component.
    /// 실제 gameplay 기능은 실행하지 않으며, 오직 최종 content prefab/profile로 교체될 준비가 되었는지
    /// category별 contract와 coverage를 표현한다.
    /// </summary>
    public class WorldMapRuntimeFinalContentContract : MonoBehaviour
    {
        // ===== Serialized Fields =====

        [SerializeField, Tooltip("이 contract가 대응하는 source marker의 고유 식별자")]
        private string _sourceMarkerId;

        [SerializeField, Tooltip("이 instance가 속한 Zone ID (예: A1, B5, F6)")]
        private string _zoneId;

        [SerializeField, Tooltip("Resolver가 결정한 RuntimeCategory (예: HarvestResource, Hazard, WorldProp, NarrativeLog, RouteTrigger, Unknown)")]
        private string _runtimeCategory;

        [SerializeField, Tooltip("Resolver가 결정한 RuntimeKey (prefab/profile 식별 키)")]
        private string _runtimeKey;

        [SerializeField, Tooltip("매칭된 Spawn Profile Entry의 ProfileId")]
        private string _profileId;

        [SerializeField, Tooltip("매칭된 Prefab의 이름")]
        private string _prefabName;

        [SerializeField, Tooltip("runtimeCategory 기반으로 결정된 gameplay adapter 종류")]
        private WorldMapRuntimeGameplayAdapterKind _adapterKind = WorldMapRuntimeGameplayAdapterKind.None;

        [SerializeField, Tooltip("adapterKind 기반으로 매핑된 최종 content 종류")]
        private WorldMapRuntimeFinalContentKind _finalContentKind = WorldMapRuntimeFinalContentKind.None;

        [SerializeField, Tooltip("이 plan이 fallback profile entry를 통해 매칭되었는지 여부")]
        private bool _isFallbackProfile;

        [SerializeField, Tooltip("매칭된 Prefab이 Editor/Test용 placeholder prefab인지 여부")]
        private bool _isEditorPlaceholderPrefab;

        [SerializeField, Tooltip("실제 final content prefab으로 교체가 필요한지 여부 (editor placeholder 또는 fallback이면 true)")]
        private bool _requiresRealPrefabReplacement;

        [SerializeField, Tooltip("최종 content가 실제 게임에서 사용 가능한 상태인지 여부")]
        private bool _isFinalContentReady;

        [SerializeField, Tooltip("왜 ready/not ready인지 설명하는 짧은 영문 메시지")]
        private string _readinessReason;

        // ===== Public Getters =====

        /// <summary>이 contract가 대응하는 source marker의 고유 식별자</summary>
        public string SourceMarkerId => _sourceMarkerId;

        /// <summary>이 instance가 속한 Zone ID (예: "A1", "B5", "F6")</summary>
        public string ZoneId => _zoneId;

        /// <summary>Resolver가 결정한 RuntimeCategory</summary>
        public string RuntimeCategory => _runtimeCategory;

        /// <summary>Resolver가 결정한 RuntimeKey</summary>
        public string RuntimeKey => _runtimeKey;

        /// <summary>매칭된 Spawn Profile Entry의 ProfileId</summary>
        public string ProfileId => _profileId;

        /// <summary>매칭된 Prefab의 이름</summary>
        public string PrefabName => _prefabName;

        /// <summary>runtimeCategory 기반으로 결정된 gameplay adapter 종류</summary>
        public WorldMapRuntimeGameplayAdapterKind AdapterKind => _adapterKind;

        /// <summary>adapterKind 기반으로 매핑된 최종 content 종류</summary>
        public WorldMapRuntimeFinalContentKind FinalContentKind => _finalContentKind;

        /// <summary>이 plan이 fallback profile entry를 통해 매칭되었는지 여부</summary>
        public bool IsFallbackProfile => _isFallbackProfile;

        /// <summary>매칭된 Prefab이 Editor/Test용 placeholder prefab인지 여부</summary>
        public bool IsEditorPlaceholderPrefab => _isEditorPlaceholderPrefab;

        /// <summary>실제 final content prefab으로 교체가 필요한지 여부</summary>
        public bool RequiresRealPrefabReplacement => _requiresRealPrefabReplacement;

        /// <summary>최종 content가 실제 게임에서 사용 가능한 상태인지 여부</summary>
        public bool IsFinalContentReady => _isFinalContentReady;

        /// <summary>왜 ready/not ready인지 설명하는 짧은 영문 메시지</summary>
        public string ReadinessReason => _readinessReason;

        // ===== Public API =====

        /// <summary>
        /// WorldMapRuntimeSpawnGameplayAdapter의 값을 contract 필드로 복사하고,
        /// adapterKind 기반으로 finalContentKind를 매핑한다.
        /// adapter가 null이면 return한다.
        /// </summary>
        /// <param name="adapter">값을 복사할 source adapter</param>
        public void Configure(WorldMapRuntimeSpawnGameplayAdapter adapter)
        {
            // adapter가 null이면 return
            if (adapter == null)
            {
                UnityEngine.Debug.LogWarning("[WorldMapRuntimeFinalContentContract] Configure called with null adapter. Contract not configured.", this);
                return;
            }

            // adapter 값을 contract 필드로 복사
            _sourceMarkerId = adapter.MarkerId;
            _zoneId = adapter.ZoneId;
            _runtimeCategory = adapter.RuntimeCategory;
            _runtimeKey = adapter.RuntimeKey;
            _profileId = adapter.ProfileId;
            _prefabName = adapter.PrefabName;
            _adapterKind = adapter.AdapterKind;
            _isFallbackProfile = adapter.IsFallbackProfile;
            _isEditorPlaceholderPrefab = adapter.IsEditorPlaceholderPrefab;

            // adapterKind 기반으로 finalContentKind 매핑
            _finalContentKind = ResolveFinalContentKind(_adapterKind);

            // isEditorPlaceholderPrefab이 true이거나 isFallbackProfile이 true면 requiresRealPrefabReplacement=true
            _requiresRealPrefabReplacement = _isEditorPlaceholderPrefab || _isFallbackProfile;

            // finalContentKind가 None 또는 Unknown이면 isFinalContentReady=false
            // finalContentKind가 valid라도 editor placeholder/fallback이면 isFinalContentReady=false
            bool kindValid = _finalContentKind != WorldMapRuntimeFinalContentKind.None
                          && _finalContentKind != WorldMapRuntimeFinalContentKind.Unknown;

            if (!kindValid)
            {
                _isFinalContentReady = false;
                _readinessReason = $"FinalContentKind is {_finalContentKind}. Cannot determine final content type.";
            }
            else if (_isEditorPlaceholderPrefab)
            {
                _isFinalContentReady = false;
                _readinessReason = $"Prefab '{_prefabName}' is an editor placeholder. Requires real prefab replacement.";
            }
            else if (_isFallbackProfile)
            {
                _isFinalContentReady = false;
                _readinessReason = $"Profile '{_profileId}' is a fallback. Requires dedicated profile assignment.";
            }
            else if (_requiresRealPrefabReplacement)
            {
                _isFinalContentReady = false;
                _readinessReason = "Requires real prefab replacement (placeholder or fallback).";
            }
            else
            {
                // 모든 조건 통과: finalContentKind valid + editor placeholder 아님 + fallback 아님
                _isFinalContentReady = true;
                _readinessReason = $"Final content ready. Kind={_finalContentKind}, Prefab='{_prefabName}', Profile='{_profileId}'.";
            }
        }

        /// <summary>
        /// 지정한 category 문자열이 이 contract의 RuntimeCategory와 일치하는지 확인한다.
        /// 대소문자를 구분한다.
        /// </summary>
        /// <param name="category">비교할 category 문자열</param>
        /// <returns>일치하면 true</returns>
        public bool IsCategory(string category)
        {
            return _runtimeCategory == category;
        }

        /// <summary>
        /// 지정한 zone 문자열이 이 contract의 ZoneId와 일치하는지 확인한다.
        /// 대소문자를 구분한다.
        /// </summary>
        /// <param name="zone">비교할 zone 문자열</param>
        /// <returns>일치하면 true</returns>
        public bool IsZone(string zone)
        {
            return _zoneId == zone;
        }

        /// <summary>
        /// 이 contract의 최종 content가 실제 게임에서 사용 가능한 상태인지 반환한다.
        /// </summary>
        /// <returns>ready 상태이면 true</returns>
        public bool IsReady()
        {
            return _isFinalContentReady;
        }

        /// <summary>
        /// 이 contract의 현재 상태를 요약한 디버그 문자열을 반환한다.
        /// </summary>
        /// <returns>디버그 요약 문자열</returns>
        public string GetDebugSummary()
        {
            return $"[WorldMapRuntimeFinalContentContract] " +
                $"ZoneId={_zoneId}, " +
                $"MarkerId={_sourceMarkerId}, " +
                $"Category={_runtimeCategory}, " +
                $"AdapterKind={_adapterKind}, " +
                $"FinalContentKind={_finalContentKind}, " +
                $"ProfileId={_profileId}, " +
                $"Prefab={_prefabName}, " +
                $"Ready={_isFinalContentReady}, " +
                $"Fallback={_isFallbackProfile}, " +
                $"EditorPlaceholder={_isEditorPlaceholderPrefab}, " +
                $"RequiresReplacement={_requiresRealPrefabReplacement}, " +
                $"Reason={_readinessReason}";
        }

        // ===== Internal Helpers =====

        /// <summary>
        /// WorldMapRuntimeGameplayAdapterKind를 WorldMapRuntimeFinalContentKind로 매핑한다.
        /// 알 수 없는 값이나 None은 각각 Unknown/None을 반환한다.
        /// </summary>
        /// <param name="adapterKind">매핑할 adapterKind</param>
        /// <returns>매핑된 finalContentKind</returns>
        private static WorldMapRuntimeFinalContentKind ResolveFinalContentKind(WorldMapRuntimeGameplayAdapterKind adapterKind)
        {
            switch (adapterKind)
            {
                case WorldMapRuntimeGameplayAdapterKind.HarvestResource:
                    return WorldMapRuntimeFinalContentKind.HarvestResource;
                case WorldMapRuntimeGameplayAdapterKind.Hazard:
                    return WorldMapRuntimeFinalContentKind.HazardVolume;
                case WorldMapRuntimeGameplayAdapterKind.Landmark:
                    return WorldMapRuntimeFinalContentKind.LandmarkProp;
                case WorldMapRuntimeGameplayAdapterKind.NarrativeLog:
                    return WorldMapRuntimeFinalContentKind.NarrativeLog;
                case WorldMapRuntimeGameplayAdapterKind.RouteTrigger:
                    return WorldMapRuntimeFinalContentKind.RouteTrigger;
                case WorldMapRuntimeGameplayAdapterKind.Debug:
                    return WorldMapRuntimeFinalContentKind.DebugMarker;
                case WorldMapRuntimeGameplayAdapterKind.None:
                    return WorldMapRuntimeFinalContentKind.None;
                case WorldMapRuntimeGameplayAdapterKind.Unknown:
                default:
                    return WorldMapRuntimeFinalContentKind.Unknown;
            }
        }
    }
}
