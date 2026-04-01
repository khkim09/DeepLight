using UnityEngine;

namespace Project.Gameplay.Harvest
{
    /// <summary>채집 대상의 레이어를 변경하여 아웃라인(Glow) 효과를 켜고 끄는 클래스</summary>
    public class HarvestTargetHighlightController : MonoBehaviour
    {
        [SerializeField] private Renderer[] targetRenderers; // 하이라이트를 적용할 렌더러 배열
        [SerializeField] private LayerMask highlightLayerMask; // 인스펙터에서 지정할 아웃라인 레이어 마스크

        private int defaultLayer; // 본체가 원래 가지고 있던 레이어
        private int highlightLayer; // 캐싱된 하이라이트 레이어 ID
        private bool isInitialized; // 초기화 여부

        /// <summary>초기 렌더러와 레이어 정보를 캐싱한다.</summary>
        private void Awake()
        {
            if (targetRenderers == null || targetRenderers.Length == 0)
                targetRenderers = GetComponentsInChildren<Renderer>(true);

            defaultLayer = gameObject.layer;

            // LayerMask 값에서 단일 레이어 인덱스(int)를 추출하여 캐싱
            highlightLayer = (int)Mathf.Log(highlightLayerMask.value, 2);

            isInitialized = true;
        }

        /// <summary>레이어를 교체하여 외곽선 효과를 토글한다.</summary>
        public void SetHighlight(bool isHighlighted)
        {
            if (!isInitialized || targetRenderers == null) return;

            // 켤 때는 Glow 레이어, 끌 때는 원래 레이어
            int appliedLayer = isHighlighted ? highlightLayer : defaultLayer;

            gameObject.layer = appliedLayer;

            for (int i = 0; i < targetRenderers.Length; i++)
            {
                if (targetRenderers[i] != null)
                    targetRenderers[i].gameObject.layer = appliedLayer;
            }
        }

        /// <summary>기본 레이어 상태로 복귀한다.</summary>
        public void ResetHighlight()
        {
            if (!isInitialized) return;
            SetHighlight(false);
        }
    }
}
