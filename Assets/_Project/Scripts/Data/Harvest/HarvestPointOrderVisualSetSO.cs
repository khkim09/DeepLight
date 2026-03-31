using UnityEngine;

namespace Project.Data.Harvest
{
    /// <summary>회수 포인트 공용 시각 세트를 정의한다.</summary>
    [CreateAssetMenu(
        fileName = "HarvestPointOrderVisualSetSO",
        menuName = "Project/Harvest/Visual/Harvest Point Order Visual Set")]
    public class HarvestPointOrderVisualSetSO : ScriptableObject
    {
        [Header("Ring Colors")]
        [SerializeField] private Color hiddenRingColor = new Color(0.15f, 0.15f, 0.15f, 0.8f); // 미공개 링 색
        [SerializeField] private Color revealedRingColor = Color.white; // 공개 링 색
        [SerializeField] private Color hoverRingColor = new Color(1f, 0.95f, 0.55f, 1f); // hover 링 색

        [Header("Fill")]
        [SerializeField] private Sprite[] orderFillSprites; // 1~6 순번용 내부 fill sprite

        [Header("Interaction")]
        [SerializeField] private float hoveredScaleMultiplier = 1.15f; // hover 확대 배율

        /// <summary>미공개 링 색을 반환한다.</summary>
        public Color HiddenRingColor => hiddenRingColor;

        /// <summary>공개 링 색을 반환한다.</summary>
        public Color RevealedRingColor => revealedRingColor;

        /// <summary>hover 링 색을 반환한다.</summary>
        public Color HoverRingColor => hoverRingColor;

        /// <summary>hover 확대 배율을 반환한다.</summary>
        public float HoveredScaleMultiplier => Mathf.Max(1f, hoveredScaleMultiplier);

        /// <summary>순번에 대응하는 fill sprite를 반환한다.</summary>
        public Sprite GetSpriteForOrder(int order)
        {
            if (orderFillSprites == null || orderFillSprites.Length == 0)
                return null;

            int index = Mathf.Clamp(order - 1, 0, orderFillSprites.Length - 1);
            return orderFillSprites[index];
        }
    }
}
