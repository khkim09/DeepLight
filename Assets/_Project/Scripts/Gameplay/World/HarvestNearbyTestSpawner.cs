using System.Collections.Generic;
using Project.Data.Harvest;
using Project.Gameplay.Harvest;
using UnityEngine;

namespace Project.Gameplay.World
{
    /// <summary>잠수함 또는 지정 중심점 주변에 테스트용 채집 타깃을 빠르게 생성하는 클래스이다.</summary>
    public class HarvestNearbyTestSpawner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HarvestTargetBehaviour harvestTargetPrefab; // 공용 target 프리팹
        [SerializeField] private Transform centerTransform; // 근접 생성 기준점(잠수함)
        [SerializeField] private Transform spawnParent; // 생성 타깃 부모(없으면 자기 자신)

        [Header("Spawn Profiles")]
        [SerializeField] private List<HarvestTargetSpawnProfileSO> spawnProfiles = new(); // 테스트에 사용할 스폰 프로필 목록

        [Header("Nearby Range")]
        [SerializeField] private float minSpawnRadiusXZ = 20f; // 중심 기준 최소 XZ 반경
        [SerializeField] private float maxSpawnRadiusXZ = 120f; // 중심 기준 최대 XZ 반경
        [SerializeField] private float minYOffset = -35f; // 중심 기준 최소 Y 오프셋
        [SerializeField] private float maxYOffset = 35f; // 중심 기준 최대 Y 오프셋

        [Header("World Clamp")]
        [SerializeField] private float minAbsoluteY = -1200f; // 월드 최소 Y
        [SerializeField] private float maxAbsoluteY = -20f; // 월드 최대 Y

        [Header("Spacing")]
        [SerializeField] private float spawnClearRadius = 18f; // 타깃 간 최소 거리
        [SerializeField] private int maxPositionRetry = 40; // 위치 재시도 횟수

        [Header("Story Debug")]
        [SerializeField] private int currentDay = 1; // 현재 스토리 일자 디버그 값

        [Header("Debug")]
        [SerializeField] private bool spawnOnStart = true; // 시작 시 자동 생성 여부
        [SerializeField] private bool clearBeforeSpawn = true; // 생성 전 기존 테스트 타깃 정리 여부
        [SerializeField] private bool logSpawnFlow = true; // 로그 출력 여부
        [SerializeField] private bool drawGizmos = true; // 씬 뷰 반경 표시 여부

        private readonly List<HarvestTargetBehaviour> spawnedTargets = new(); // 현재 테스트 스포너가 생성한 타깃 캐시

        /// <summary>시작 시 설정에 따라 근접 테스트 타깃을 생성한다.</summary>
        private void Start()
        {
            if (!spawnOnStart)
                return;

            SpawnAllNearCenter();
        }

        /// <summary>등록된 프로필 기준으로 중심점 주변에 테스트 타깃을 생성한다.</summary>
        [ContextMenu("Spawn Nearby Test Targets")]
        public void SpawnAllNearCenter()
        {
            if (clearBeforeSpawn)
                ClearSpawnedTargets();

            if (harvestTargetPrefab == null)
            {
                Log("Spawn aborted: Harvest Target Prefab is null.");
                return;
            }

            if (centerTransform == null)
            {
                Log("Spawn aborted: Center Transform is null.");
                return;
            }

            Transform parent = spawnParent != null ? spawnParent : transform;

            Log($"Nearby spawn begin | currentDay={currentDay}, center={centerTransform.position}");

            int totalSpawned = 0;

            for (int i = 0; i < spawnProfiles.Count; i++)
            {
                HarvestTargetSpawnProfileSO profile = spawnProfiles[i];
                if (profile == null)
                {
                    Log($"Profile[{i}] skipped: null");
                    continue;
                }

                // 테스트 스포너에서는 프로필의 day 조건만 유지한다.
                if (!IsProfileAllowedToday(profile))
                {
                    Log($"Profile [{profile.ProfileId}] skipped: currentDay={currentDay} out of range");
                    continue;
                }

                int spawnCount = Random.Range(profile.MinSpawnCount, profile.MaxSpawnCount + 1);
                Log($"Profile [{profile.ProfileId}] nearby spawning count={spawnCount}");

                for (int spawnIndex = 0; spawnIndex < spawnCount; spawnIndex++)
                {
                    HarvestTargetSO targetData = PickTargetFromProfile(profile);
                    if (targetData == null)
                    {
                        Log($"Profile [{profile.ProfileId}] spawn[{spawnIndex}] skipped: no valid target in pool");
                        continue;
                    }

                    if (!TryFindNearbySpawnPosition(out Vector3 spawnPosition))
                    {
                        Log($"Profile [{profile.ProfileId}] spawn[{spawnIndex}] skipped: no valid nearby position");
                        continue;
                    }

                    HarvestTargetBehaviour instance = Instantiate(
                        harvestTargetPrefab,
                        spawnPosition,
                        Quaternion.identity,
                        parent);

                    instance.name = $"NearbyTestTarget_{targetData.TargetId}_{spawnIndex}";
                    instance.SetTargetData(targetData);

                    // 테스트에서도 day 제한은 동일하게 전달한다.
                    instance.SetHarvestAvailabilityWindow(profile.MinDay, profile.MaxDay, currentDay);

                    spawnedTargets.Add(instance);
                    totalSpawned++;

                    Log($"Spawned [{instance.name}] at {spawnPosition}");
                }
            }

            Log($"Nearby spawn finished | totalSpawned={totalSpawned}");
        }

        /// <summary>이 스포너가 생성한 테스트 타깃을 모두 제거한다.</summary>
        [ContextMenu("Clear Nearby Test Targets")]
        public void ClearSpawnedTargets()
        {
            for (int i = spawnedTargets.Count - 1; i >= 0; i--)
            {
                if (spawnedTargets[i] == null)
                    continue;

                if (Application.isPlaying)
                    Destroy(spawnedTargets[i].gameObject);
                else
                    DestroyImmediate(spawnedTargets[i].gameObject);
            }

            spawnedTargets.Clear();
        }

        /// <summary>프로필이 현재 day에 허용되는지 검사한다.</summary>
        private bool IsProfileAllowedToday(HarvestTargetSpawnProfileSO profile)
        {
            if (profile == null)
                return false;

            return currentDay >= profile.MinDay && currentDay <= profile.MaxDay;
        }

        /// <summary>프로필의 target pool에서 가중치 기반으로 target을 하나 선택한다.</summary>
        private HarvestTargetSO PickTargetFromProfile(HarvestTargetSpawnProfileSO profile)
        {
            if (profile == null || profile.TargetPool == null || profile.TargetPool.Count == 0)
                return null;

            int totalWeight = 0;

            // 유효한 타깃만 가중치 합산
            for (int i = 0; i < profile.TargetPool.Count; i++)
            {
                HarvestTargetSO target = profile.TargetPool[i].Target;
                if (target == null || !target.IsValid())
                    continue;

                totalWeight += profile.TargetPool[i].Weight;
            }

            if (totalWeight <= 0)
                return null;

            int roll = Random.Range(0, totalWeight);
            int cumulative = 0;

            for (int i = 0; i < profile.TargetPool.Count; i++)
            {
                HarvestTargetSO target = profile.TargetPool[i].Target;
                if (target == null || !target.IsValid())
                    continue;

                cumulative += profile.TargetPool[i].Weight;
                if (roll < cumulative)
                    return target;
            }

            return null;
        }

        /// <summary>중심점 주변 반경 안에서 실제 생성 위치를 찾는다.</summary>
        private bool TryFindNearbySpawnPosition(out Vector3 spawnPosition)
        {
            spawnPosition = Vector3.zero;

            if (centerTransform == null)
                return false;

            Vector3 center = centerTransform.position;

            for (int attempt = 0; attempt < maxPositionRetry; attempt++)
            {
                // 중심 주변 원형 범위 안에서 위치를 뽑는다.
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float radius = Random.Range(minSpawnRadiusXZ, maxSpawnRadiusXZ);

                float offsetX = Mathf.Cos(angle) * radius;
                float offsetZ = Mathf.Sin(angle) * radius;
                float offsetY = Random.Range(minYOffset, maxYOffset);

                Vector3 candidate = new Vector3(
                    center.x + offsetX,
                    Mathf.Clamp(center.y + offsetY, minAbsoluteY, maxAbsoluteY),
                    center.z + offsetZ);

                // 기존 생성 타깃과 너무 붙지 않도록 보정
                if (IsTooCloseToExisting(candidate))
                    continue;

                spawnPosition = candidate;
                return true;
            }

            return false;
        }

        /// <summary>기존 생성 타깃과 너무 가까운지 검사한다.</summary>
        private bool IsTooCloseToExisting(Vector3 candidate)
        {
            for (int i = 0; i < spawnedTargets.Count; i++)
            {
                if (spawnedTargets[i] == null)
                    continue;

                float sqrDistance = (spawnedTargets[i].transform.position - candidate).sqrMagnitude;
                if (sqrDistance < spawnClearRadius * spawnClearRadius)
                    return true;
            }

            return false;
        }

        /// <summary>디버그 로그를 조건부 출력한다.</summary>
        private void Log(string message)
        {
            if (!logSpawnFlow)
                return;

            Debug.Log($"[HarvestNearbyTestSpawner] {message}");
        }

        /// <summary>씬 뷰에서 근접 스폰 반경을 시각화한다.</summary>
        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos || centerTransform == null)
                return;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(centerTransform.position, minSpawnRadiusXZ);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(centerTransform.position, maxSpawnRadiusXZ);
        }
    }
}
