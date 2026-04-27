using System.Collections.Generic;
using Project.Data.Harvest;
using Project.Gameplay.Harvest;
using UnityEngine;

namespace Project.Gameplay.World
{
    /// <summary>수심과 거리 규칙에 따라 채집 타깃을 월드 전체에 생성하는 클래스이다.</summary>
    public class HarvestWorldSpawner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HarvestTargetBehaviour harvestTargetPrefab; // 공용 target 프리팹
        [SerializeField] private Transform playerTransform; // 현재 플레이어 또는 잠수함
        [SerializeField] private Transform startPoint; // 시작 기준점

        [Header("Spawn Profiles")]
        [SerializeField] private List<HarvestTargetSpawnProfileSO> spawnProfiles = new(); // 생성 규칙 프로필들

        [Header("World Range")]
        [SerializeField] private float minSpawnHeight = -1200f; // 최소 생성 y
        [SerializeField] private float maxSpawnHeight = -20f; // 최대 생성 y
        [SerializeField] private float spawnClearRadius = 20f; // 타깃 간 최소 거리
        [SerializeField] private int maxPositionRetry = 20; // 위치 재시도 횟수

        [Header("Story Debug")]
        [SerializeField] private int currentDay = 1; // 현재 스토리 일자 디버그 값

        [Header("Debug")]
        [SerializeField] private bool spawnOnStart = true; // 시작 시 자동 생성 여부
        [SerializeField] private bool logSpawnFlow = true; // 생성 흐름 로그 출력 여부

        private readonly List<HarvestTargetBehaviour> spawnedTargets = new(); // 생성된 타깃 캐시

        /// <summary>시작 시 모든 프로필을 기준으로 타깃을 생성한다.</summary>
        private void Start()
        {
            if (!spawnOnStart)
                return;

            SpawnAll();
        }

        /// <summary>등록된 모든 프로필에 대해 월드 타깃을 다시 생성한다.</summary>
        [ContextMenu("Spawn All Harvest Targets")]
        public void SpawnAll()
        {
            ClearSpawnedTargets();

            if (harvestTargetPrefab == null)
            {
                Log("Spawn aborted: Harvest Target Prefab is null.");
                return;
            }

            if (startPoint == null)
            {
                Log("Spawn aborted: Start Point is null.");
                return;
            }

            Log($"Spawn begin | world-wide generation, currentDay={currentDay}");

            int totalSpawned = 0;

            for (int i = 0; i < spawnProfiles.Count; i++)
            {
                HarvestTargetSpawnProfileSO profile = spawnProfiles[i];
                if (profile == null)
                {
                    Log($"Profile[{i}] skipped: null");
                    continue;
                }

                int spawnCount = Random.Range(profile.MinSpawnCount, profile.MaxSpawnCount + 1);
                Log($"Profile [{profile.ProfileId}] spawning count={spawnCount}");

                for (int spawnIndex = 0; spawnIndex < spawnCount; spawnIndex++)
                {
                    HarvestTargetSO targetData = PickTargetFromProfile(profile);
                    if (targetData == null)
                    {
                        Log($"Profile [{profile.ProfileId}] spawn[{spawnIndex}] skipped: no valid target in pool");
                        continue;
                    }

                    if (!TryFindSpawnPosition(profile, out Vector3 spawnPosition))
                    {
                        Log($"Profile [{profile.ProfileId}] spawn[{spawnIndex}] skipped: no valid spawn position");
                        continue;
                    }

                    HarvestTargetBehaviour instance = Instantiate(
                        harvestTargetPrefab,
                        spawnPosition,
                        Quaternion.identity,
                        transform);

                    instance.name = $"HarvestTarget_{targetData.TargetId}_{spawnIndex}";
                    instance.SetTargetData(targetData);

                    // 스폰 프로필의 day 범위를 “채집 가능 시기”로 전달한다.
                    instance.SetHarvestAvailabilityWindow(profile.MinDay, profile.MaxDay, currentDay);

                    spawnedTargets.Add(instance);
                    totalSpawned++;

                    Log($"Spawned [{instance.name}] at {spawnPosition}");
                }
            }

            Log($"Spawn finished | totalSpawned={totalSpawned}");
        }

        /// <summary>기존 생성 타깃을 모두 제거한다.</summary>
        [ContextMenu("Clear Spawned Harvest Targets")]
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

        /// <summary>프로필의 target pool에서 가중치 기반으로 target을 하나 선택한다.</summary>
        private HarvestTargetSO PickTargetFromProfile(HarvestTargetSpawnProfileSO profile)
        {
            if (profile == null || profile.TargetPool == null || profile.TargetPool.Count == 0)
                return null;

            int totalWeight = 0;

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

        /// <summary>프로필의 거리/수심 범위 안에서 실제 생성 위치를 찾는다.</summary>
        private bool TryFindSpawnPosition(HarvestTargetSpawnProfileSO profile, out Vector3 spawnPosition)
        {
            spawnPosition = Vector3.zero;
            Vector3 origin = startPoint.position;

            for (int attempt = 0; attempt < maxPositionRetry; attempt++)
            {
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float distance = Random.Range(profile.MinDistanceFromStartXZ, profile.MaxDistanceFromStartXZ);
                float y = -Random.Range(profile.MinDepth, profile.MaxDepth);

                y = Mathf.Clamp(y, minSpawnHeight, maxSpawnHeight);

                Vector3 candidate = new Vector3(
                    origin.x + Mathf.Cos(angle) * distance,
                    y,
                    origin.z + Mathf.Sin(angle) * distance);

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

            UnityEngine.Debug.Log($"[HarvestWorldSpawner] {message}");
        }
    }
}
