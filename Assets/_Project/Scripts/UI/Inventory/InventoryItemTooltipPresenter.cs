using Project.Data.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.Inventory
{
    /// <summary>인벤토리 아이템 툴팁의 내용 갱신과 아이템 기준 위치 정렬을 담당한다.</summary>
    public class InventoryItemTooltipPresenter : MonoBehaviour
    {
        private enum TooltipFollowMode
        {
            None,
            GrabbedPreview,
            PlacedHover
        }

        [Header("Root")]
        [SerializeField] private GameObject rootObject; // 툴팁 전체 루트
        [SerializeField] private RectTransform tooltipRect; // 실제 이동시킬 툴팁 RectTransform
        [SerializeField] private Canvas rootCanvas; // 루트 캔버스

        [Header("Content")]
        [SerializeField] private Image itemTypeIconImage; // 아이템 유형 아이콘
        [SerializeField] private TextMeshProUGUI itemNameText; // 아이템 이름
        [SerializeField] private TextMeshProUGUI itemDescriptionText; // 아이템 설명

        [Header("Layout")]
        [SerializeField] private float inventoryLeftLocalX = 320f; // center anchor 기준 인벤토리 시작 x
        [SerializeField] private float hoverFixedLocalX = 145f; // hover 툴팁 고정 x
        [SerializeField] private float sideGap = 0f; // 아이템과 툴팁 사이 간격
        [SerializeField] private float sideSwitchThresholdX = -320f; // 좌우 반전 기준점
        [SerializeField] private float screenPadding = 12f; // 화면 바깥 이탈 방지 여백

        private TooltipFollowMode followMode = TooltipFollowMode.None; // 현재 추적 방식
        private RectTransform followedSourceRect; // 현재 따라갈 대상 Rect
        private ItemSO currentItemData; // 현재 표시 중인 아이템 데이터

        /// <summary>초기 참조를 자동 보정하고 시작 시 툴팁을 숨긴다.</summary>
        private void Awake()
        {
            if (tooltipRect == null && rootObject != null)
                tooltipRect = rootObject.transform as RectTransform;

            if (rootCanvas == null)
                rootCanvas = GetComponentInParent<Canvas>();

            Hide();
        }

        /// <summary>활성 상태일 때 현재 기준 Rect를 따라 툴팁 위치를 갱신한다.</summary>
        private void Update()
        {
            if (followMode == TooltipFollowMode.None)
                return;

            if (rootObject == null || !rootObject.activeSelf)
                return;

            if (followedSourceRect == null || currentItemData == null)
            {
                Hide();
                return;
            }

            UpdateFollowPosition();
        }

        /// <summary>기본 아이템 정보만 표시한다. 위치 추적은 하지 않는다.</summary>
        public void ShowItem(ItemSO itemData)
        {
            if (itemData == null)
            {
                Hide();
                return;
            }

            currentItemData = itemData;
            followMode = TooltipFollowMode.None;
            followedSourceRect = null;

            RefreshContent(itemData);

            if (rootObject != null)
                rootObject.SetActive(true);
        }

        /// <summary>재그랩 중인 preview 기준으로 툴팁 표시를 시작한다.</summary>
        public void ShowForGrabbedPreview(ItemSO itemData, RectTransform grabbedPreviewRect)
        {
            if (itemData == null || grabbedPreviewRect == null)
            {
                Hide();
                return;
            }

            currentItemData = itemData;
            followedSourceRect = grabbedPreviewRect;
            followMode = TooltipFollowMode.GrabbedPreview;

            RefreshContent(itemData);

            if (rootObject != null)
                rootObject.SetActive(true);

            Canvas.ForceUpdateCanvases();
            UpdateFollowPosition();
        }

        /// <summary>배치된 아이템 hover 기준으로 툴팁 표시를 시작한다.</summary>
        public void ShowForPlacedHover(ItemSO itemData, RectTransform placedItemRect)
        {
            if (itemData == null || placedItemRect == null)
            {
                Hide();
                return;
            }

            currentItemData = itemData;
            followedSourceRect = placedItemRect;
            followMode = TooltipFollowMode.PlacedHover;

            RefreshContent(itemData);

            if (rootObject != null)
                rootObject.SetActive(true);

            Canvas.ForceUpdateCanvases();
            UpdateFollowPosition();
        }

        /// <summary>툴팁을 숨기고 현재 추적 상태를 초기화한다.</summary>
        public void Hide()
        {
            followMode = TooltipFollowMode.None;
            followedSourceRect = null;
            currentItemData = null;

            if (rootObject != null)
                rootObject.SetActive(false);
        }

        /// <summary>현재 아이템 데이터 기준으로 툴팁 내용을 갱신한다.</summary>
        private void RefreshContent(ItemSO itemData)
        {
            if (itemData == null)
                return;

            if (itemTypeIconImage != null)
                itemTypeIconImage.sprite = itemData.TypeIcon;

            if (itemNameText != null)
                itemNameText.text = itemData.DisplayName;

            if (itemDescriptionText != null)
                itemDescriptionText.text = itemData.Description;
        }

        /// <summary>현재 추적 대상 Rect 기준으로 툴팁의 최종 위치를 계산한다.</summary>
        private void UpdateFollowPosition()
        {
            if (rootCanvas == null || tooltipRect == null || followedSourceRect == null)
                return;

            RectTransform canvasRect = rootCanvas.transform as RectTransform;
            if (canvasRect == null)
                return;

            Rect sourceLocalRect = GetLocalRectInCanvas(followedSourceRect, canvasRect);
            if (sourceLocalRect.width <= 0f || sourceLocalRect.height <= 0f)
                return;

            float tooltipWidth = tooltipRect.rect.width;
            float tooltipHeight = tooltipRect.rect.height;

            float canvasLeft = canvasRect.rect.xMin;   // 보통 -960
            float canvasRight = canvasRect.rect.xMax;  // 보통 +960
            float canvasBottom = canvasRect.rect.yMin; // 보통 -540
            float canvasTop = canvasRect.rect.yMax;    // 보통 +540

            float sourceLeft = sourceLocalRect.xMin;
            float sourceRight = sourceLocalRect.xMax;
            float sourceTop = sourceLocalRect.yMax;
            float sourceBottom = sourceLocalRect.yMin;

            Vector2 mouseLocal = ScreenToCanvasLocal(Input.mousePosition, canvasRect);
            float itemHalfWidth = sourceLocalRect.width * 0.5f;

            float tooltipCenterLocalX;

            if (followMode == TooltipFollowMode.GrabbedPreview)
            {
                // 좌우 반전 기준점을 0에서 -320으로 변경
                bool placeTooltipOnLeft = mouseLocal.x >= sideSwitchThresholdX;

                if (placeTooltipOnLeft)
                {
                    // 현재 마우스 x - sprite width * 0.5 >= 320 이면 x 고정
                    bool shouldClampAtInventoryBoundary = (mouseLocal.x - itemHalfWidth) >= inventoryLeftLocalX;

                    if (shouldClampAtInventoryBoundary)
                    {
                        // 툴팁의 오른쪽 끝이 인벤토리 좌측 라인에 닿도록 고정
                        tooltipCenterLocalX = inventoryLeftLocalX - sideGap - (tooltipWidth * 0.5f);
                    }
                    else
                    {
                        // 그 전에는 아이템 왼쪽에 딱 붙음
                        tooltipCenterLocalX = sourceLeft - sideGap - (tooltipWidth * 0.5f);
                    }
                }
                else
                {
                    // 기준점 왼쪽 영역에서는 툴팁을 아이템 오른쪽에 배치
                    tooltipCenterLocalX = sourceRight + sideGap + (tooltipWidth * 0.5f);
                }
            }
            else
            {
                // hover 시에는 x를 무조건 고정
                tooltipCenterLocalX = hoverFixedLocalX;
            }

            // 기본은 항상 상단 정렬
            float topAlignedCenterY = sourceTop - (tooltipHeight * 0.5f);

            // 상단 정렬 상태에서 tooltip 하단이 canvas bottom 아래로 내려갈 때만 하단 정렬로 반전
            bool shouldFlipToBottomAlign = (sourceTop - tooltipHeight) < canvasBottom;

            float tooltipCenterLocalY;
            if (shouldFlipToBottomAlign)
                tooltipCenterLocalY = sourceBottom + (tooltipHeight * 0.5f);
            else
                tooltipCenterLocalY = topAlignedCenterY;

            // 화면 좌우 이탈 방지
            float minCenterX = canvasLeft + (tooltipWidth * 0.5f) + screenPadding;
            float maxCenterX = canvasRight - (tooltipWidth * 0.5f) - screenPadding;
            tooltipCenterLocalX = Mathf.Clamp(tooltipCenterLocalX, minCenterX, maxCenterX);

            // 화면 상하 이탈 방지
            float minCenterY = canvasBottom + (tooltipHeight * 0.5f) + screenPadding;
            float maxCenterY = canvasTop - (tooltipHeight * 0.5f) - screenPadding;
            tooltipCenterLocalY = Mathf.Clamp(tooltipCenterLocalY, minCenterY, maxCenterY);

            tooltipRect.anchoredPosition = new Vector2(tooltipCenterLocalX, tooltipCenterLocalY);
        }

        /// <summary>스크린 좌표를 현재 루트 캔버스 기준 로컬 좌표로 변환한다.</summary>
        private Vector2 ScreenToCanvasLocal(Vector2 screenPosition, RectTransform canvasRect)
        {
            Camera eventCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : rootCanvas.worldCamera;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPosition,
                eventCamera,
                out Vector2 localPoint);

            return localPoint;
        }

        /// <summary>지정 RectTransform의 현재 사각형을 루트 캔버스 로컬 좌표 기준으로 계산한다.</summary>
        private Rect GetLocalRectInCanvas(RectTransform targetRect, RectTransform canvasRect)
        {
            Vector3[] corners = new Vector3[4];
            targetRect.GetWorldCorners(corners);

            Vector2 min = Vector2.zero;
            Vector2 max = Vector2.zero;
            bool initialized = false;

            for (int i = 0; i < 4; i++)
            {
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, corners[i]);
                Vector2 localPoint = ScreenToCanvasLocal(screenPoint, canvasRect);

                if (!initialized)
                {
                    min = localPoint;
                    max = localPoint;
                    initialized = true;
                }
                else
                {
                    min = Vector2.Min(min, localPoint);
                    max = Vector2.Max(max, localPoint);
                }
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
