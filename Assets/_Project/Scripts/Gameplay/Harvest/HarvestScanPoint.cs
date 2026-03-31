using Project.Data.Harvest;
using UnityEngine;

namespace Project.Gameplay.Harvest
{
    /// <summary>런타임 회수 포인트의 시각 상태와 스탯을 보관한다.</summary>
    public class HarvestScanPoint : MonoBehaviour
    {
        [Header("Visual References")]
        [SerializeField] private GameObject pointVisualRoot; // 시각 루트
        [SerializeField] private SpriteRenderer outerRingRenderer; // 외곽 링 렌더러
        [SerializeField] private SpriteRenderer fillRenderer; // 내부 fill 렌더러
        [SerializeField] private Transform tooltipAnchor; // 툴팁 기준 위치

        [Header("Layers")]
        [SerializeField] private LayerMask scanPointLayerMask; // point root 레이어
        [SerializeField] private LayerMask visualLayerMask; // visual 레이어

        private string pointId;
        private int displayOrder;
        private string displayLabel;
        private float baseStability;
        private float firstAnchorBias;
        private float sequenceBias;
        private float riskWeight;
        private float sonarSignature;
        private float lidarSignature;

        private bool isRevealed;
        private bool isHovered;
        private int assignedOrder;
        private HarvestPointOrderVisualSetSO visualSet;
        private Vector3 baseScale = Vector3.one;

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
        public bool IsHovered => isHovered;
        public bool IsSelected => assignedOrder > 0;
        public int AssignedOrder => assignedOrder;
        public Transform TooltipAnchor => tooltipAnchor != null ? tooltipAnchor : transform;

        /// <summary>초기 시각 상태와 레이어를 적용한다.</summary>
        private void Awake()
        {
            if (pointVisualRoot != null)
                baseScale = pointVisualRoot.transform.localScale;

            ApplyLayers();
            ApplyVisualState();
        }

        /// <summary>preset과 런타임 스탯으로 현재 포인트를 초기화한다.</summary>
        public void Initialize(HarvestScanPointPresetSO preset, HarvestScanPointRuntimeStats runtimeStats)
        {
            pointId = preset.PointId;
            displayOrder = preset.DisplayOrder;
            displayLabel = preset.DisplayLabel;

            baseStability = runtimeStats.BaseStability;
            firstAnchorBias = runtimeStats.FirstAnchorBias;
            sequenceBias = runtimeStats.SequenceBias;
            riskWeight = runtimeStats.RiskWeight;
            sonarSignature = runtimeStats.SonarSignature;
            lidarSignature = runtimeStats.LidarSignature;

            visualSet = preset.OrderVisualSet;

            ResetRuntimeState();
        }

        /// <summary>포인트 상태를 초기화한다.</summary>
        public void ResetRuntimeState()
        {
            isRevealed = false;
            isHovered = false;
            assignedOrder = 0;
            ApplyVisualState();
        }

        /// <summary>포인트를 공개 상태로 전환한다.</summary>
        public void Reveal()
        {
            isRevealed = true;
            ApplyVisualState();
        }

        /// <summary>hover 상태를 갱신한다.</summary>
        public void SetHovered(bool hovered)
        {
            if (isHovered == hovered)
                return;

            isHovered = hovered;
            ApplyVisualState();
        }

        /// <summary>현재 포인트에 순번을 지정한다.</summary>
        public void AssignOrder(int order)
        {
            assignedOrder = Mathf.Max(0, order);
            if (assignedOrder > 0)
                isRevealed = true;

            ApplyVisualState();
        }

        /// <summary>현재 포인트의 순번을 제거한다.</summary>
        public void ClearAssignedOrder()
        {
            assignedOrder = 0;
            isHovered = false;
            ApplyVisualState();
        }

        /// <summary>현재 상태에 맞는 시각을 반영한다.</summary>
        private void ApplyVisualState()
        {
            if (pointVisualRoot != null)
                pointVisualRoot.SetActive(true);

            if (outerRingRenderer != null)
            {
                if (visualSet == null)
                {
                    outerRingRenderer.color = isRevealed ? Color.white : new Color(0.15f, 0.15f, 0.15f, 0.8f);
                }
                else if (!isRevealed)
                {
                    outerRingRenderer.color = visualSet.HiddenRingColor;
                }
                else if (isHovered && !IsSelected)
                {
                    outerRingRenderer.color = visualSet.HoverRingColor;
                }
                else
                {
                    outerRingRenderer.color = visualSet.RevealedRingColor;
                }
            }

            if (fillRenderer != null)
            {
                if (!IsSelected)
                {
                    fillRenderer.sprite = null;
                    fillRenderer.color = new Color(1f, 1f, 1f, 0f);
                }
                else
                {
                    fillRenderer.sprite = visualSet != null ? visualSet.GetSpriteForOrder(assignedOrder) : null;
                    fillRenderer.color = Color.white;
                }
            }

            if (pointVisualRoot != null)
            {
                float scaleMultiplier = visualSet != null && isHovered && !IsSelected
                    ? visualSet.HoveredScaleMultiplier
                    : 1f;

                pointVisualRoot.transform.localScale = baseScale * scaleMultiplier;
            }
        }

        /// <summary>인스펙터 LayerMask 기준으로 레이어를 적용한다.</summary>
        private void ApplyLayers()
        {
            int scanPointLayer = ExtractSingleLayerIndex(scanPointLayerMask);
            int visualLayer = ExtractSingleLayerIndex(visualLayerMask);

            if (scanPointLayer >= 0)
                gameObject.layer = scanPointLayer;

            if (visualLayer >= 0 && pointVisualRoot != null)
                SetLayerRecursively(pointVisualRoot.transform, visualLayer);
        }

        /// <summary>LayerMask에서 단일 레이어 인덱스를 추출한다.</summary>
        private int ExtractSingleLayerIndex(LayerMask mask)
        {
            int value = mask.value;
            if (value == 0)
                return -1;

            int index = 0;
            while (value > 1)
            {
                value >>= 1;
                index++;
            }

            return index;
        }

        /// <summary>하위 transform 전체에 레이어를 재귀 적용한다.</summary>
        private void SetLayerRecursively(Transform root, int layerIndex)
        {
            root.gameObject.layer = layerIndex;

            for (int i = 0; i < root.childCount; i++)
                SetLayerRecursively(root.GetChild(i), layerIndex);
        }
    }
}
