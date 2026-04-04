using Project.Core.Events;
using Project.Data.Enums;
using Project.Data.Items;
using Project.Gameplay.Services;
using Project.UI.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.Harvest
{
    /// <summary>채집 성공 시 도감 팝업과 fresh recovery grab 시작을 제어한다.</summary>
    public class HarvestResultUIController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ItemDatabaseSO itemDatabase; // 아이템 DB
        [SerializeField] private InventoryGrabbedItemPresenter grabPresenter; // grab 총괄 프리젠터

        [Header("Discovery Popup")]
        [SerializeField] private GameObject newDiscoveryPanel; // 새 발견 팝업 루트
        [SerializeField] private Image targetImage; // 발견한 아이템 아이콘
        [SerializeField] private TextMeshProUGUI targetNameText; // 발견한 아이템 이름
        [SerializeField] private TextMeshProUGUI descriptionText; // 팝업 설명 문구

        private EncyclopediaService encyclopediaService; // 도감 서비스
        private GameTimeService gameTimeService; // 인게임 시간 서비스

        /// <summary>도감 서비스와 시간 서비스를 주입하고 초기 패널 상태를 정리한다.</summary>
        public void Initialize(EncyclopediaService newEncyclopediaService, GameTimeService newGameTimeService)
        {
            encyclopediaService = newEncyclopediaService;
            gameTimeService = newGameTimeService;

            if (newDiscoveryPanel != null)
                newDiscoveryPanel.SetActive(false);
        }

        /// <summary>이벤트를 구독한다.</summary>
        private void OnEnable()
        {
            EventBus.Subscribe<HarvestRecoveryResolvedEvent>(OnHarvestResolved);
            EventBus.Subscribe<InventoryItemPlacementConfirmedEvent>(OnInventoryItemPlacementConfirmed);
        }

        /// <summary>이벤트 구독을 해제한다.</summary>
        private void OnDisable()
        {
            EventBus.Unsubscribe<HarvestRecoveryResolvedEvent>(OnHarvestResolved);
            EventBus.Unsubscribe<InventoryItemPlacementConfirmedEvent>(OnInventoryItemPlacementConfirmed);
        }

        /// <summary>시작 시 팝업을 숨긴다.</summary>
        private void Awake()
        {
            if (newDiscoveryPanel != null)
                newDiscoveryPanel.SetActive(false);
        }

        /// <summary>채집 성공 이벤트를 받아 DiscoveryPopup과 fresh recovery grab을 시작한다.</summary>
        private void OnHarvestResolved(HarvestRecoveryResolvedEvent publishedEvent)
        {
            if (!publishedEvent.IsSuccess)
                return;

            if (itemDatabase == null || !itemDatabase.TryGetItem(publishedEvent.ItemId, out ItemSO itemData))
                return;

            if (grabPresenter == null || encyclopediaService == null)
                return;

            bool isNewlyDiscovered = !encyclopediaService.IsItemDiscovered(itemData.ItemId);

            if (isNewlyDiscovered)
            {
                float currentInGameHours = gameTimeService != null ? gameTimeService.CurrentInGameHours : 0f;
                encyclopediaService.DiscoverItem(itemData.ItemId, currentInGameHours);
                ShowDiscoveryPopup(itemData);
            }

            // 채집 성공 즉시 fresh recovery grab 시작
            grabPresenter.StartRecoveredGrab(itemData, isNewlyDiscovered);
        }

        /// <summary>아이템 배치 확정 시 fresh recovery 팝업을 닫는다.</summary>
        private void OnInventoryItemPlacementConfirmed(InventoryItemPlacementConfirmedEvent publishedEvent)
        {
            if (!publishedEvent.WasFreshRecovery)
                return;

            if (newDiscoveryPanel != null)
                newDiscoveryPanel.SetActive(false);
        }

        /// <summary>새로 발견한 아이템 정보를 팝업에 반영한다.</summary>
        private void ShowDiscoveryPopup(ItemSO itemData)
        {
            if (itemData == null || newDiscoveryPanel == null)
                return;

            if (targetImage != null)
                targetImage.sprite = itemData.Icon;

            if (targetNameText != null)
                targetNameText.text = itemData.DisplayName;

            if (descriptionText != null)
            {
                if (itemData.Category == ItemCategoryType.Relic)
                {
                    descriptionText.text =
                        $"...!! Relic discovered! " +
                        $"Detailed analysis required. " +
                        $"{itemData.AnalysisTeaserMessage}";
                }
                else
                {
                    descriptionText.text =
                        "New discovery! " +
                        "Registering new resource information to the encyclopedia.";
                }
            }

            newDiscoveryPanel.SetActive(true);
        }
    }
}
