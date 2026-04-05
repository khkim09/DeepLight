using System;
using System.Collections.Generic;
using Project.Data.Items;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Project.Data.Harvest
{
    /// <summary>채집 대상의 정적 설정과 드랍 아이템 풀을 정의한다.</summary>
    [CreateAssetMenu(
        fileName = "HarvestTarget_",
        menuName = "Project/Harvest/Definitions/Harvest Target")]
    public class HarvestTargetSO : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string targetId; // 채집 대상 ID
        [SerializeField] private HarvestPreviewNamePoolSO previewNamePool; // 회수 전 위장 이름 풀
        [SerializeField] private List<HarvestItemWeightedEntry> itemPool = new(); // 드랍 아이템 후보 풀
        [SerializeField] private float additionalDifficulty; // 추가 난이도
        [SerializeField] private float collisionDamage = 10f; // 충돌 피해량
        [SerializeField] private bool consumeOnSuccess = true; // 성공 시 소멸 여부

        [Header("Runtime Point Generation")]
        [SerializeField] private GameObject scanPointPrefab; // 공용 포인트 프리팹
        [SerializeField] private List<HarvestScanPointPresetSO> pointPresets = new(); // 생성할 포인트 preset 목록

        [Header("Cube Surface Rule")]
        [SerializeField] private float faceSurfaceOffset = 0.501f; // 표면으로부터 살짝 바깥 offset
        [SerializeField] private float planarMin = -0.405f; // 면 내부 최소 좌표
        [SerializeField] private float planarMax = 0.405f; // 면 내부 최대 좌표
        [SerializeField] private float minPointSpacing = 0.2025f; // 동일 면 최소 간격

        public string TargetId => targetId;
        public HarvestPreviewNamePoolSO PreviewNamePool => previewNamePool;
        public IReadOnlyList<HarvestItemWeightedEntry> ItemPool => itemPool;
        public float AdditionalDifficulty => additionalDifficulty;
        public float CollisionDamage => collisionDamage;
        public bool ConsumeOnSuccess => consumeOnSuccess;
        public GameObject ScanPointPrefab => scanPointPrefab;
        public IReadOnlyList<HarvestScanPointPresetSO> PointPresets => pointPresets;
        public float FaceSurfaceOffset => Mathf.Max(0.501f, faceSurfaceOffset);
        public float PlanarMin => planarMin;
        public float PlanarMax => planarMax;
        public float MinPointSpacing => Mathf.Max(0.01f, minPointSpacing);

        /// <summary>회수 전 위장 표시 이름을 생성한다.</summary>
        public string BuildPreviewDisplayName()
        {
            return previewNamePool != null
                ? previewNamePool.GetRandomDisplayName()
                : "Suspicious Wooden Box";
        }

        /// <summary>아이템 풀에서 가중치 기반으로 실제 드랍 아이템 하나를 선택한다.</summary>
        public ItemSO PickRandomItem()
        {
            if (itemPool == null || itemPool.Count == 0)
                return null;

            int totalWeight = 0;

            for (int i = 0; i < itemPool.Count; i++)
            {
                ItemSO item = itemPool[i].Item;
                if (item == null || !item.IsValid())
                    continue;

                totalWeight += itemPool[i].Weight;
            }

            if (totalWeight <= 0)
                return null;

            int roll = Random.Range(0, totalWeight);
            int cumulative = 0;

            for (int i = 0; i < itemPool.Count; i++)
            {
                ItemSO item = itemPool[i].Item;
                if (item == null || !item.IsValid())
                    continue;

                cumulative += itemPool[i].Weight;
                if (roll < cumulative)
                    return item;
            }

            return null;
        }

        /// <summary>데이터 유효성을 검사한다.</summary>
        public bool IsValid()
        {
            if (string.IsNullOrWhiteSpace(targetId))
                return false;

            if (scanPointPrefab == null)
                return false;

            if (pointPresets == null || pointPresets.Count == 0)
                return false;

            if (itemPool == null || itemPool.Count == 0)
                return false;

            return true;
        }
    }

    /// <summary>가중치가 포함된 드랍 아이템 후보 엔트리이다.</summary>
    [Serializable]
    public struct HarvestItemWeightedEntry
    {
        [SerializeField] private ItemSO item; // 후보 아이템
        [SerializeField] private int weight; // 등장 가중치

        public ItemSO Item => item;
        public int Weight => Mathf.Max(0, weight);
    }
}
