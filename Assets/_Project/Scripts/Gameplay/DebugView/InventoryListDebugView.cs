using System.Collections.Generic;
using System.Text;
using Project.Core.Events;
using Project.Data.Items;
using TMPro;
using UnityEngine;

namespace Project.Gameplay.DebugView
{
    /// <summary>인벤토리 추가 결과를 텍스트 목록으로 디버깅</summary>
    public class InventoryListDebugView : MonoBehaviour
    {
        [SerializeField] private TMP_Text inventoryText; // 인벤토리 표시 텍스트
        [SerializeField] private ItemDatabaseSO itemDatabase; // 아이템 데이터베이스

        private readonly Dictionary<string, int> itemCounts = new(); // 아이템 수량 캐시

        /// <summary>이벤트 구독 등록</summary>
        private void OnEnable()
        {
            EventBus.Subscribe<InventoryItemAddedEvent>(OnInventoryItemAdded);
            EventBus.Subscribe<InventoryItemRemovedEvent>(OnInventoryItemRemoved);
        }

        /// <summary>이벤트 구독 해제</summary>
        private void OnDisable()
        {
            EventBus.Unsubscribe<InventoryItemAddedEvent>(OnInventoryItemAdded);
            EventBus.Unsubscribe<InventoryItemRemovedEvent>(OnInventoryItemRemoved);
        }

        /// <summary>초기 인벤토리 텍스트 설정</summary>
        private void Start()
        {
            RefreshText();
        }

        /// <summary>아이템 추가</summary>
        private void OnInventoryItemAdded(InventoryItemAddedEvent publishedEvent)
        {
            if (!itemCounts.ContainsKey(publishedEvent.ItemId))
                itemCounts.Add(publishedEvent.ItemId, 0);

            itemCounts[publishedEvent.ItemId] += publishedEvent.Amount;
            RefreshText();
        }

        /// <summary>아이템 제거</summary>
        private void OnInventoryItemRemoved(InventoryItemRemovedEvent publishedEvent)
        {
            if (!itemCounts.ContainsKey(publishedEvent.ItemId))
                return;

            itemCounts[publishedEvent.ItemId] -= publishedEvent.Amount;

            // 0 이하이면 제거
            if (itemCounts[publishedEvent.ItemId] <= 0)
                itemCounts.Remove(publishedEvent.ItemId);

            RefreshText();
        }

        /// <summary>인벤토리 텍스트 갱신</summary>
        private void RefreshText()
        {
            if (inventoryText == null) return;

            if (itemCounts.Count == 0)
            {
                inventoryText.text = "Inventory : Empty";
                return;
            }

            // 문자열 조립
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Inventory :");

            foreach (KeyValuePair<string, int> pair in itemCounts)
            {
                string displayName = GetDisplayName(pair.Key);
                builder.AppendLine($"- {displayName} x{pair.Value}");
            }

            inventoryText.text = builder.ToString();
        }

        /// <summary>아이템 표시 이름 반환</summary>
        private string GetDisplayName(string itemId)
        {
            // DB 없으면 ID 반환
            if (itemDatabase == null)
                return itemId;

            // 조회 실패면 ID 반환
            if (!itemDatabase.TryGetItem(itemId, out ItemSO itemData))
                return itemId;

            // 표시 이름 우선 반환
            if (!string.IsNullOrWhiteSpace(itemData.DisplayName))
                return itemData.DisplayName;

            return itemId;
        }
    }
}
