using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Data.Harvest
{
    /// <summary>채집 타깃의 월드 생성 조건과 후보를 정의한다.</summary>
    [CreateAssetMenu(
        fileName = "HarvestTargetSpawnProfileSO",
        menuName = "Project/Harvest/Definitions/Harvest Target Spawn Profile")]
    public class HarvestTargetSpawnProfileSO : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string profileId; // 스폰 프로필 ID

        [Header("Depth Rule")]
        [SerializeField] private float minDepth; // 최소 수심
        [SerializeField] private float maxDepth = 200f; // 최대 수심

        [Header("Distance Rule")]
        [SerializeField] private float minDistanceFromStartXZ; // 시작점 기준 최소 XZ 거리
        [SerializeField] private float maxDistanceFromStartXZ = 99999f; // 시작점 기준 최대 XZ 거리

        [Header("Story Rule")]
        [SerializeField] private int minDay = 1; // 최소 등장 일자
        [SerializeField] private int maxDay = 999; // 최대 등장 일자

        [Header("Spawn Count")]
        [SerializeField] private int minSpawnCount = 1; // 최소 생성 수
        [SerializeField] private int maxSpawnCount = 3; // 최대 생성 수

        [Header("Target Pool")]
        [SerializeField] private List<HarvestTargetWeightedEntry> targetPool = new(); // 후보 타깃 풀

        public string ProfileId => profileId;
        public float MinDepth => minDepth;
        public float MaxDepth => maxDepth;
        public float MinDistanceFromStartXZ => minDistanceFromStartXZ;
        public float MaxDistanceFromStartXZ => maxDistanceFromStartXZ;
        public int MinDay => minDay;
        public int MaxDay => maxDay;
        public int MinSpawnCount => minSpawnCount;
        public int MaxSpawnCount => maxSpawnCount;
        public IReadOnlyList<HarvestTargetWeightedEntry> TargetPool => targetPool;

        /// <summary>현재 조건이 이 프로필에 맞는지 검사한다.</summary>
        public bool Matches(float depth, float distanceFromStartXZ, int currentDay)
        {
            if (depth < minDepth || depth > maxDepth)
                return false;

            if (distanceFromStartXZ < minDistanceFromStartXZ || distanceFromStartXZ > maxDistanceFromStartXZ)
                return false;

            if (currentDay < minDay || currentDay > maxDay)
                return false;

            return true;
        }
    }

    /// <summary>가중치가 포함된 타깃 후보 엔트리이다.</summary>
    [Serializable]
    public struct HarvestTargetWeightedEntry
    {
        [SerializeField] private HarvestTargetSO target;
        [SerializeField] private int weight;

        public HarvestTargetSO Target => target;
        public int Weight => Mathf.Max(0, weight);
    }
}
