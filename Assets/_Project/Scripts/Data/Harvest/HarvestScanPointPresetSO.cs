using System;
using UnityEngine;

namespace Project.Data.Harvest
{
    /// <summary>회수 포인트의 식별 정보, 랜덤 스탯 범위, 생성 규칙을 정의한다.</summary>
    [CreateAssetMenu(
        fileName = "HarvestScanPointPresetSO",
        menuName = "Project/Harvest/Definitions/Harvest Scan Point Preset")]
    public class HarvestScanPointPresetSO : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string pointId = "A"; // 포인트 ID
        [SerializeField] private int displayOrder; // 표시 순서
        [SerializeField] private string displayLabel = "A"; // 툴팁/라벨

        [Header("Recovery Weights")]
        [SerializeField] private FloatRange baseStabilityRange = new FloatRange(0.7f, 0.99f);
        [SerializeField] private FloatRange firstAnchorBiasRange = new FloatRange(0.6f, 0.95f);
        [SerializeField] private FloatRange sequenceBiasRange = new FloatRange(0.2f, 0.8f);
        [SerializeField] private FloatRange riskWeightRange = new FloatRange(0.05f, 0.4f);

        [Header("Sensor Signatures")]
        [SerializeField] private FloatRange sonarSignatureRange = new FloatRange(0.3f, 0.9f);
        [SerializeField] private FloatRange lidarSignatureRange = new FloatRange(0.2f, 0.9f);

        [Header("Spawn Rule")]
        [SerializeField] private HarvestPointAllowedFaces allowedFaces = HarvestPointAllowedFaces.All;

        [Header("Visual")]
        [SerializeField] private HarvestPointOrderVisualSetSO orderVisualSet;

        public string PointId => pointId;
        public int DisplayOrder => displayOrder;
        public string DisplayLabel => displayLabel;
        public HarvestPointAllowedFaces AllowedFaces => allowedFaces;
        public HarvestPointOrderVisualSetSO OrderVisualSet => orderVisualSet;

        /// <summary>현재 preset 기준 랜덤 런타임 스탯을 생성한다.</summary>
        public HarvestScanPointRuntimeStats BuildRuntimeStats()
        {
            return new HarvestScanPointRuntimeStats(
                baseStabilityRange.Sample(),
                firstAnchorBiasRange.Sample(),
                sequenceBiasRange.Sample(),
                riskWeightRange.Sample(),
                sonarSignatureRange.Sample(),
                lidarSignatureRange.Sample());
        }

        /// <summary>float 최소/최대 범위를 정의한다.</summary>
        [Serializable]
        public struct FloatRange
        {
            [SerializeField] private float min;
            [SerializeField] private float max;

            /// <summary>범위를 생성한다.</summary>
            public FloatRange(float min, float max)
            {
                this.min = min;
                this.max = max;
            }

            /// <summary>범위 내 랜덤값을 반환한다.</summary>
            public float Sample()
            {
                float clampedMin = Mathf.Min(min, max);
                float clampedMax = Mathf.Max(min, max);
                return UnityEngine.Random.Range(clampedMin, clampedMax);
            }
        }
    }

    /// <summary>회수 포인트 생성 시 사용하는 런타임 스탯 묶음이다.</summary>
    [Serializable]
    public readonly struct HarvestScanPointRuntimeStats
    {
        public readonly float BaseStability;
        public readonly float FirstAnchorBias;
        public readonly float SequenceBias;
        public readonly float RiskWeight;
        public readonly float SonarSignature;
        public readonly float LidarSignature;

        /// <summary>런타임 스탯을 생성한다.</summary>
        public HarvestScanPointRuntimeStats(
            float baseStability,
            float firstAnchorBias,
            float sequenceBias,
            float riskWeight,
            float sonarSignature,
            float lidarSignature)
        {
            BaseStability = baseStability;
            FirstAnchorBias = firstAnchorBias;
            SequenceBias = sequenceBias;
            RiskWeight = riskWeight;
            SonarSignature = sonarSignature;
            LidarSignature = lidarSignature;
        }
    }

    /// <summary>회수 포인트가 생성될 수 있는 면 규칙이다.</summary>
    [Flags]
    public enum HarvestPointAllowedFaces
    {
        None = 0,
        PositiveX = 1 << 0,
        NegativeX = 1 << 1,
        PositiveY = 1 << 2,
        NegativeY = 1 << 3,
        PositiveZ = 1 << 4,
        NegativeZ = 1 << 5,
        SideOnly = PositiveX | NegativeX | PositiveZ | NegativeZ,
        TopAndSides = PositiveY | PositiveX | NegativeX | PositiveZ | NegativeZ,
        All = PositiveX | NegativeX | PositiveY | NegativeY | PositiveZ | NegativeZ
    }
}
