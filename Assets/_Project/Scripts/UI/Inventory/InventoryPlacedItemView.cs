using Project.Gameplay.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.UI.Inventory
{
    /// <summary>인벤토리에 실제 배치된 아이템 하나의 UI 표현과 포인터 상호작용을 담당한다.</summary>
    public class InventoryPlacedItemView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IBeginDragHandler
    {
        [Header("References")]
        [SerializeField] private RectTransform visualRect; // 실제 스프라이트를 그리는 비주얼 자식
        [SerializeField] private Image iconImage; // 비주얼 자식의 Image

        private InventoryItemInstance itemInstance; // 연결된 런타임 아이템
        private InventoryGrabbedItemPresenter grabbedItemPresenter; // grab 프리젠터
        private InventoryItemTooltipPresenter tooltipPresenter; // hover tooltip

        /// <summary>연결된 인스턴스를 반환한다.</summary>
        public InventoryItemInstance ItemInstance => itemInstance;

        /// <summary>루트 RectTransform을 반환한다.</summary>
        public RectTransform RectTransform => transform as RectTransform;

        /// <summary>비주얼 RectTransform을 반환한다.</summary>
        public RectTransform VisualRectTransform => visualRect != null ? visualRect : RectTransform;

        /// <summary>참조가 비어 있으면 자동으로 찾는다.</summary>
        private void Awake()
        {
            if (iconImage == null)
                iconImage = GetComponentInChildren<Image>(true);

            if (visualRect == null && iconImage != null)
                visualRect = iconImage.rectTransform;
        }

        /// <summary>배치 아이템 뷰를 초기화한다.</summary>
        public void Initialize(
            InventoryItemInstance newItemInstance,
            InventoryGrabbedItemPresenter newGrabbedItemPresenter,
            InventoryItemTooltipPresenter newTooltipPresenter)
        {
            itemInstance = newItemInstance;
            grabbedItemPresenter = newGrabbedItemPresenter;
            tooltipPresenter = newTooltipPresenter;

            if (iconImage == null)
                iconImage = GetComponentInChildren<Image>(true);

            if (visualRect == null && iconImage != null)
                visualRect = iconImage.rectTransform;

            if (iconImage != null && itemInstance != null && itemInstance.ItemData != null)
            {
                iconImage.sprite = itemInstance.ItemData.Icon;
                iconImage.color = Color.white;
                iconImage.raycastTarget = true;
                iconImage.enabled = true;
                iconImage.preserveAspect = false;
            }

            gameObject.SetActive(true);
        }

        /// <summary>비주얼 자식의 크기와 회전을 적용한다.</summary>
        public void ApplyVisual(Vector2 unrotatedPixelSize, int rotationQuarterTurns)
        {
            if (visualRect == null)
                return;

            visualRect.anchorMin = new Vector2(0.5f, 0.5f);
            visualRect.anchorMax = new Vector2(0.5f, 0.5f);
            visualRect.pivot = new Vector2(0.5f, 0.5f);
            visualRect.anchoredPosition = Vector2.zero;

            visualRect.sizeDelta = unrotatedPixelSize;
            visualRect.localEulerAngles = new Vector3(0f, 0f, InventoryRotationUtility.GetRotationZ(rotationQuarterTurns));
            visualRect.localScale = Vector3.one;
            visualRect.SetAsLastSibling();
        }

        /// <summary>포인터 hover 진입 시 배치 아이템 hover 전용 툴팁을 띄운다.</summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (grabbedItemPresenter == null || grabbedItemPresenter.IsAnyGrabActive)
                return;

            if (tooltipPresenter != null && itemInstance != null && itemInstance.ItemData != null)
                tooltipPresenter.ShowForPlacedHover(itemInstance.ItemData, VisualRectTransform);
        }

        /// <summary>포인터 hover 이탈 시 툴팁을 숨긴다.</summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            if (tooltipPresenter != null)
                tooltipPresenter.Hide();
        }

        /// <summary>좌클릭 단일 클릭 시 토글형 grab을 시작한다.</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            if (grabbedItemPresenter == null || grabbedItemPresenter.IsAnyGrabActive)
                return;

            grabbedItemPresenter.BeginToggleGrabFromPlacedItem(this);
        }

        /// <summary>좌클릭 드래그 시작 시 홀드형 grab을 시작한다.</summary>
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            if (grabbedItemPresenter == null || grabbedItemPresenter.IsAnyGrabActive)
                return;

            grabbedItemPresenter.BeginHoldGrabFromPlacedItem(this);
        }
    }
}
