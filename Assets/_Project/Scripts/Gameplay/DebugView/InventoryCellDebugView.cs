using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Gameplay.DebugView
{
    /// <summary>인벤토리 디버그 셀 한 칸의 표시를 담당하는 클래스</summary>
    public class InventoryCellDebugView : MonoBehaviour
    {
        [SerializeField] private Image backgroundImage; // 셀 배경 이미지
        [SerializeField] private TMP_Text labelText; // 셀 라벨 텍스트
        [SerializeField] private Color emptyColor = new Color(0.15f, 0.15f, 0.15f, 0.85f); // 빈 칸 색상
        [SerializeField] private Color occupiedColor = new Color(0.2f, 0.8f, 0.4f, 0.95f); // 점유 칸 색상

        /// <summary>사용 불가 칸을 숨긴다</summary>
        public void SetDisabled()
        {
            // 오브젝트 숨김
            gameObject.SetActive(false);
        }

        /// <summary>빈 칸 상태를 표시한다</summary>
        public void SetEmpty(int x, int y)
        {
            // 오브젝트 노출
            gameObject.SetActive(true);

            // 배경 색상 적용
            if (backgroundImage != null)
                backgroundImage.color = emptyColor;

            // 좌표 표시
            if (labelText != null)
                labelText.text = string.Empty;
        }

        /// <summary>점유 칸 상태를 표시한다</summary>
        public void SetOccupied(string itemLabel)
        {
            // 오브젝트 노출
            gameObject.SetActive(true);

            // 배경 색상 적용
            if (backgroundImage != null)
                backgroundImage.color = occupiedColor;

            // 라벨 표시
            if (labelText != null)
                labelText.text = itemLabel;
        }
    }
}
