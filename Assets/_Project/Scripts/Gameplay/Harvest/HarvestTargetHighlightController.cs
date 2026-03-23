using UnityEngine;

namespace Project.Gameplay.Harvest
{
    /// <summary>채집 대상의 하이라이트 표시를 담당하는 클래스</summary>
    public class HarvestTargetHighlightController : MonoBehaviour
    {
        [SerializeField] private Renderer targetRenderer; // 하이라이트 대상 렌더러
        [SerializeField] private Color normalColor = Color.white; // 기본 색상
        [SerializeField] private Color highlightedColor = Color.yellow; // 하이라이트 색상
        [SerializeField] private bool useMaterialInstance = true; // 머티리얼 인스턴스 사용 여부

        private Material runtimeMaterial; // 런타임 머티리얼
        private bool isInitialized; // 초기화 여부

        /// <summary>렌더러를 초기화한다</summary>
        private void Awake()
        {
            // 렌더러 자동 탐색
            if (targetRenderer == null)
                targetRenderer = GetComponentInChildren<Renderer>();

            if (targetRenderer == null) return;

            // 머티리얼 인스턴스 분리
            if (useMaterialInstance)
                runtimeMaterial = targetRenderer.material;
            else
                runtimeMaterial = targetRenderer.sharedMaterial;

            // 초기 색상 적용
            SetHighlight(false);
            isInitialized = true;
        }

        /// <summary>하이라이트 상태를 적용한다</summary>
        public void SetHighlight(bool isHighlighted)
        {
            if (runtimeMaterial == null) return;

            Color appliedColor = isHighlighted ? highlightedColor : normalColor;

            if (runtimeMaterial.HasProperty("_Color"))
                runtimeMaterial.color = appliedColor;
        }

        /// <summary>기본 상태로 복귀한다</summary>
        public void ResetHighlight()
        {
            // 초기화 안 됐어도 시도
            if (!isInitialized && targetRenderer == null) return;

            SetHighlight(false);
        }
    }
}
