using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.Inventory
{
    /// <summary>인벤토리의 단일 그리드 타일 시각과 점유/하이라이트 상태를 관리한다.</summary>
    public class InventorySlotUI : MonoBehaviour
    {
        [SerializeField] private Image background; // 슬롯 배경 이미지
        [SerializeField] private Color emptyColor = new Color(0.1f, 0.3f, 0.4f, 0.8f);
        [SerializeField] private Color occupiedColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);

        private int gridX; // 슬롯 x 좌표
        private int gridY; // 슬롯 y 좌표
        private bool isOccupied; // 현재 점유 여부

        /// <summary>현재 이 타일이 아이템에 의해 차지되었는지 여부이다.</summary>
        public bool IsOccupied => isOccupied;

        /// <summary>현재 슬롯의 x 좌표를 반환한다.</summary>
        public int GridX => gridX;

        /// <summary>현재 슬롯의 y 좌표를 반환한다.</summary>
        public int GridY => gridY;

        /// <summary>슬롯의 논리적 좌표를 설정한다.</summary>
        public void Initialize(int x, int y)
        {
            gridX = x;
            gridY = y;
            SetOccupied(false);
        }

        /// <summary>점유 상태를 갱신하고 기본 색상을 변경한다.</summary>
        public void SetOccupied(bool occupied)
        {
            isOccupied = occupied;
            ApplyBaseColor();
        }

        /// <summary>현재 슬롯에 하이라이트 색상을 적용한다.</summary>
        public void SetHighlightColor(Color highlightColor)
        {
            if (background != null)
                background.color = highlightColor;
        }

        /// <summary>현재 점유 상태에 맞는 기본 색상을 다시 적용한다.</summary>
        public void ApplyBaseColor()
        {
            if (background != null)
                background.color = isOccupied ? occupiedColor : emptyColor;
        }
    }
}
