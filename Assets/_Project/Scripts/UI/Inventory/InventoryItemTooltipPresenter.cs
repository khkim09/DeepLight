using Project.Data.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.Inventory
{
    /// <summary>인벤토리 아이템 툴팁 패널의 표시/숨김과 텍스트 갱신을 담당한다.</summary>
    public class InventoryItemTooltipPresenter : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject rootObject; // 툴팁 전체 루트

        [Header("Content")]
        [SerializeField] private Image itemTypeIconImage; // 아이템 유형 아이콘
        [SerializeField] private TextMeshProUGUI itemNameText; // 아이템 이름
        [SerializeField] private TextMeshProUGUI itemDescriptionText; // 아이템 설명

        /// <summary>시작 시 툴팁을 숨긴다.</summary>
        private void Awake()
        {
            Hide();
        }

        /// <summary>아이템 정보를 툴팁에 표시한다.</summary>
        public void ShowItem(ItemSO itemData)
        {
            if (itemData == null)
            {
                Hide();
                return;
            }

            if (itemTypeIconImage != null)
                itemTypeIconImage.sprite = itemData.TypeIcon;

            if (itemNameText != null)
                itemNameText.text = itemData.DisplayName;

            if (itemDescriptionText != null)
                itemDescriptionText.text = itemData.Description;

            if (rootObject != null)
                rootObject.SetActive(true);
        }

        /// <summary>툴팁을 숨긴다.</summary>
        public void Hide()
        {
            if (rootObject != null)
                rootObject.SetActive(false);
        }
    }
}
