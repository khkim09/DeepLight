using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.Inventory
{
    /// <summary>인벤토리 슬롯의 점유 상태와 배치 프리뷰 색상을 표시한다.</summary>
    public class InventorySlotUI : MonoBehaviour
    {
        /// <summary>슬롯 프리뷰 상태이다.</summary>
        public enum SlotPreviewState
        {
            None = 0,
            Valid = 1,
            Invalid = 2,
            Swap = 3
        }

        [Header("References")]
        [SerializeField] private Image background; // 슬롯 배경 이미지

        [Header("Base Colors")]
        [SerializeField] private Color emptyColor = new Color(0f, 0f, 0f, 0.8f); // 기본 빈칸 색
        [SerializeField] private Color occupiedColor = new Color(0.196f, 0.196f, 0.196f, 0.8f); // 기본 점유칸 색

        [Header("Preview Colors")]
        [SerializeField] private Color validEmptyPreviewColor = new Color(0f, 1f, 0f, 0.35f); // 빈칸 위 정상 배치 가능
        [SerializeField] private Color invalidEmptyPreviewColor = new Color(1f, 0f, 0f, 0.35f); // 빈칸 위 배치 불가
        [SerializeField] private Color swapEmptyPreviewColor = new Color(1f, 0.75f, 0f, 0.35f); // 빈칸 포함 교체 배치

        [Header("Occupied Preview Colors")]
        [SerializeField] private Color validOccupiedPreviewColor = new Color(0f, 1f, 0f, 0.35f); // 이론상 점유칸 valid
        [SerializeField] private Color invalidOccupiedPreviewColor = new Color(1f, 0f, 0f, 0.35f); // 점유칸 위 배치 불가
        [SerializeField] private Color swapOccupiedPreviewColor = new Color(1f, 0.85f, 0f, 0.35f); // 점유칸 위 swap 가능

        public int GridX { get; private set; } // 논리 그리드 X
        public int GridY { get; private set; } // 논리 그리드 Y

        private bool isOccupied; // 현재 점유 여부
        private SlotPreviewState previewState = SlotPreviewState.None; // 현재 프리뷰 상태

        /// <summary>슬롯 좌표를 초기화한다.</summary>
        public void Initialize(int x, int y)
        {
            GridX = x;
            GridY = y;

            RefreshVisual();
        }

        /// <summary>현재 슬롯 점유 여부를 갱신한다.</summary>
        public void SetOccupied(bool occupied)
        {
            isOccupied = occupied;
            RefreshVisual();
        }

        /// <summary>현재 슬롯 프리뷰 상태를 갱신한다.</summary>
        public void SetPreviewState(SlotPreviewState state)
        {
            previewState = state;
            RefreshVisual();
        }

        /// <summary>프리뷰 상태를 기본값으로 되돌린다.</summary>
        public void ResetPreviewState()
        {
            previewState = SlotPreviewState.None;
            RefreshVisual();
        }

        /// <summary>현재 상태에 맞는 슬롯 색상을 다시 적용한다.</summary>
        private void RefreshVisual()
        {
            if (background == null)
                return;

            if (!isOccupied)
            {
                background.color = previewState switch
                {
                    SlotPreviewState.Valid => validEmptyPreviewColor,
                    SlotPreviewState.Invalid => invalidEmptyPreviewColor,
                    SlotPreviewState.Swap => swapEmptyPreviewColor,
                    _ => emptyColor
                };

                return;
            }

            background.color = previewState switch
            {
                SlotPreviewState.Valid => validOccupiedPreviewColor,
                SlotPreviewState.Invalid => invalidOccupiedPreviewColor,
                SlotPreviewState.Swap => swapOccupiedPreviewColor,
                _ => occupiedColor
            };
        }
    }
}
