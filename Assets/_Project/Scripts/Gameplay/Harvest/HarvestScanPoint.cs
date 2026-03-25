using UnityEngine;

namespace Project.Gameplay.Harvest
{
    /// <summary>회수 콘솔에서 선택 가능한 고정 포인트를 정의하는 컴포넌트</summary>
    public class HarvestScanPoint : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string pointId = "A"; // 포인트 고유 ID
        [SerializeField] private int displayOrder = 0; // 표시 순서
        [SerializeField] private string displayLabel = "A"; // HUD 표시 라벨

        [Header("Recovery Weights")]
        [Tooltip("포인트 기본 안정성")][SerializeField, Range(0f, 1f)] private float baseStability = 0.5f; // 포인트 기본 안정성
        [Tooltip("첫 포인트 적합도")][SerializeField, Range(0f, 1f)] private float firstAnchorBias = 0.5f; // 첫 포인트 적합도
        [Tooltip("후속 순서 적합도")][SerializeField, Range(0f, 1f)] private float sequenceBias = 0.5f; // 후속 순서 적합도
        [Tooltip("위험도")][SerializeField, Range(0f, 1f)] private float riskWeight = 0.5f; // 위험도

        [Header("Sensor Signatures")]
        [Tooltip("소나 반응도")][SerializeField, Range(0f, 1f)] private float sonarSignature = 0.5f; // 소나 반응도
        [Tooltip("라이다 반응도")][SerializeField, Range(0f, 1f)] private float lidarSignature = 0.5f; // 라이다 반응도

        [Header("Visual")]
        [SerializeField] private GameObject pointVisualRoot; // 포인트 비주얼 루트
        [SerializeField] private SpriteRenderer pointSpriteRenderer; // 포인트 스프라이트 렌더러
        [SerializeField] private Sprite hiddenSprite; // 미공개 스프라이트(black)
        [SerializeField] private Sprite revealedSprite; // 공개/선택 스프라이트(white)
        [SerializeField] private Color hiddenColor = new Color(1f, 1f, 1f, 0.55f); // 미공개 색상
        [SerializeField] private Color revealedColor = new Color(0.8f, 0.95f, 1f, 1f); // 공개 색상
        [SerializeField] private Color hoveredColor = new Color(1f, 0.95f, 0.5f, 1f); // hover 색상
        [SerializeField] private Color selectedColor = new Color(0.35f, 1f, 0.35f, 1f); // 선택/배치 완료 색상
        [SerializeField] private float hoveredScaleMultiplier = 1.2f; // hover 시 확대 배율
        [SerializeField] private float selectedScaleMultiplier = 1.25f; // 선택 시 확대 배율

        private bool isRevealed; // 스캔으로 공개되었는지
        private bool isSelected; // 회수 순서에 배치되었는지
        private bool isHovered; // 현재 마우스 hover 상태인지
        private Vector3 visualBaseScale = Vector3.one; // 기본 비주얼 스케일

        public string PointId => pointId;
        public int DisplayOrder => displayOrder;
        public string DisplayLabel => displayLabel;
        public float BaseStability => baseStability;
        public float FirstAnchorBias => firstAnchorBias;
        public float SequenceBias => sequenceBias;
        public float RiskWeight => riskWeight;
        public float SonarSignature => sonarSignature;
        public float LidarSignature => lidarSignature;
        public bool IsRevealed => isRevealed;
        public bool IsSelected => isSelected;
        public bool IsHovered => isHovered;

        /// <summary>초기 비주얼 상태를 적용한다</summary>
        private void Awake()
        {
            if (pointVisualRoot != null)
                visualBaseScale = pointVisualRoot.transform.localScale; // 기본 스케일 캐싱

            ApplyVisualState(); // 시작 비주얼 적용
        }

        /// <summary>포인트 상태를 초기화한다</summary>
        public void ResetRuntimeState()
        {
            isRevealed = false; // 공개 상태 초기화
            isSelected = false; // 선택 상태 초기화
            isHovered = false; // hover 상태 초기화
            ApplyVisualState(); // 비주얼 적용
        }

        /// <summary>포인트를 공개 상태로 전환한다</summary>
        public void Reveal()
        {
            isRevealed = true; // 공개 처리
            ApplyVisualState(); // 비주얼 적용
        }

        /// <summary>포인트를 회수 순서에 배치된 상태로 전환한다</summary>
        public void Select()
        {
            isSelected = true; // 선택 완료 상태
            isRevealed = true; // 선택되면 공개 상태도 보장
            isHovered = false; // 선택 완료 시 hover 해제
            ApplyVisualState(); // 비주얼 적용
        }

        /// <summary>포인트를 회수 순서에서 제거한다</summary>
        public void Deselect()
        {
            isSelected = false; // 선택 완료 상태 해제
            isHovered = false; // hover도 해제
            ApplyVisualState(); // 현재 공개/비공개 상태에 맞춰 복귀
        }

        /// <summary>포인트 hover 상태를 갱신한다</summary>
        public void SetHovered(bool hovered)
        {
            if (isSelected)
                hovered = false; // 이미 배치된 포인트는 hover보다 selected 우선

            if (isHovered == hovered)
                return;

            isHovered = hovered; // hover 상태 반영
            ApplyVisualState(); // 비주얼 적용
        }

        /// <summary>현재 상태에 맞는 비주얼을 갱신한다</summary>
        private void ApplyVisualState()
        {
            if (pointVisualRoot != null)
                pointVisualRoot.SetActive(true); // 포인트 위치 자체는 항상 보이게 유지

            if (pointSpriteRenderer != null)
            {
                // 스프라이트 종류 결정
                pointSpriteRenderer.sprite = isRevealed || isSelected ? revealedSprite : hiddenSprite;

                // 색상 결정
                if (isSelected)
                    pointSpriteRenderer.color = selectedColor;
                else if (isHovered)
                    pointSpriteRenderer.color = hoveredColor;
                else if (isRevealed)
                    pointSpriteRenderer.color = revealedColor;
                else
                    pointSpriteRenderer.color = hiddenColor;
            }

            if (pointVisualRoot == null)
                return;

            // 상태에 따라 크기 강조
            float scaleMultiplier = 1f;
            if (isSelected)
                scaleMultiplier = selectedScaleMultiplier;
            else if (isHovered)
                scaleMultiplier = hoveredScaleMultiplier;

            pointVisualRoot.transform.localScale = visualBaseScale * scaleMultiplier;
        }
    }
}
