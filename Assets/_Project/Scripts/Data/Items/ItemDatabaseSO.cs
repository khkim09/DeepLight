using System.Collections.Generic;
using UnityEngine;

namespace Project.Data.Items
{
    /// <summary>아이템 ID 기반 조회를 담당하는 데이터베이스</summary>
    [CreateAssetMenu(fileName = "ItemDatabase", menuName = "Project/Data/Item Database")]
    public class ItemDatabaseSO : ScriptableObject
    {
        [SerializeField] private List<ItemSO> items = new(); // 전체 아이템 목록

        private Dictionary<string, ItemSO> itemMap; // ID 조회 캐시

        public IReadOnlyList<ItemSO> Items => items;

        /// <summary>아이템 조회 캐시를 생성</summary>
        private void BuildCache()
        {
            // 캐시 생성
            itemMap = new Dictionary<string, ItemSO>();

            // 전체 아이템 순회
            for (int i = 0; i < items.Count; i++)
            {
                ItemSO item = items[i]; // 현재 아이템 참조
                if (item == null) continue;
                if (string.IsNullOrWhiteSpace(item.ItemId)) continue;
                if (itemMap.ContainsKey(item.ItemId)) continue;

                // 캐시에 등록
                itemMap.Add(item.ItemId, item);
            }
        }

        /// <summary>아이템 ID로 데이터를 조회</summary>
        public bool TryGetItem(string itemId, out ItemSO item)
        {
            // 캐시 없으면 생성
            if (itemMap == null) BuildCache();

            return itemMap.TryGetValue(itemId, out item);
        }

        /// <summary>캐시를 초기화</summary>
        public void ClearCache()
        {
            // 캐시 제거
            itemMap = null;
        }
    }
}
