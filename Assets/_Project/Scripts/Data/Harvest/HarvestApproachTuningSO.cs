using System;
using Project.Data.Common;
using UnityEngine;

namespace Project.Data.Harvest
{
    /// <summary>채집 진입 자동 접근 연출의 기본값과 런타임 계산 규칙을 정의하는 SO이다.</summary>
    [CreateAssetMenu(
        fileName = "HarvestApproachTuningSO",
        menuName = "Project/Harvest/Harvest Approach Tuning")]
    public class HarvestApproachTuningSO : ScriptableObject
    {
        [Header("Upgradeable")]
        [SerializeField] private UpgradeableApproachSettings upgradeableSettings; // 업그레이드 가능한 접근 연출 속도

        [Header("Fixed")]
        [SerializeField] private FixedApproachSettings fixedSettings; // 고정 연출 세팅

        /// <summary>업그레이드를 반영한 자동 접근 런타임 세팅을 생성한다.</summary>
        public HarvestApproachRuntimeSettings BuildRuntimeSettings(HarvestApproachUpgradeOverrides upgradeOverrides)
        {
            return new HarvestApproachRuntimeSettings(
                approachMoveSpeed: Mathf.Max(0.01f, upgradeOverrides.ApproachMoveSpeed.Apply(upgradeableSettings.approachMoveSpeed)),
                approachRotationSpeed: Mathf.Max(0.01f, upgradeOverrides.ApproachRotationSpeed.Apply(upgradeableSettings.approachRotationSpeed)),
                minApproachDuration: Mathf.Max(0.01f, fixedSettings.minApproachDuration),
                stopDistancePadding: fixedSettings.stopDistancePadding,
                preserveCurrentDepth: fixedSettings.preserveCurrentDepth,
                approachCurve: fixedSettings.approachCurve);
        }

        /// <summary>업그레이드 가능한 접근 기본 수치 묶음이다.</summary>
        [Serializable]
        public struct UpgradeableApproachSettings
        {
            public float approachMoveSpeed;     // 자동 접근 이동 속도
            public float approachRotationSpeed; // 자동 접근 회전 속도
        }

        /// <summary>고정 접근 연출 세팅 묶음이다.</summary>
        [Serializable]
        public struct FixedApproachSettings
        {
            public float minApproachDuration;  // 최소 연출 시간
            public float stopDistancePadding;  // 정지 거리 여유값
            public bool preserveCurrentDepth;  // 현재 깊이 유지 여부
            public AnimationCurve approachCurve; // 접근 보간 곡선
        }
    }

    /// <summary>자동 접근 연출 업그레이드 보정값 묶음이다.</summary>
    [Serializable]
    public struct HarvestApproachUpgradeOverrides
    {
        [SerializeField] private RuntimeStatModifier approachMoveSpeed;     // 접근 이동 속도 보정
        [SerializeField] private RuntimeStatModifier approachRotationSpeed; // 접근 회전 속도 보정

        public RuntimeStatModifier ApproachMoveSpeed => approachMoveSpeed;
        public RuntimeStatModifier ApproachRotationSpeed => approachRotationSpeed;

        /// <summary>보정이 없는 기본 업그레이드 상태를 반환한다.</summary>
        public static HarvestApproachUpgradeOverrides Default()
        {
            return new HarvestApproachUpgradeOverrides
            {
                approachMoveSpeed = RuntimeStatModifier.Identity(),
                approachRotationSpeed = RuntimeStatModifier.Identity()
            };
        }
    }

    /// <summary>자동 접근 연출에 실제 사용되는 런타임 세팅이다.</summary>
    public readonly struct HarvestApproachRuntimeSettings
    {
        public readonly float ApproachMoveSpeed;
        public readonly float ApproachRotationSpeed;
        public readonly float MinApproachDuration;
        public readonly float StopDistancePadding;
        public readonly bool PreserveCurrentDepth;
        public readonly AnimationCurve ApproachCurve;

        /// <summary>자동 접근 연출 런타임 세팅을 생성한다.</summary>
        public HarvestApproachRuntimeSettings(
            float approachMoveSpeed,
            float approachRotationSpeed,
            float minApproachDuration,
            float stopDistancePadding,
            bool preserveCurrentDepth,
            AnimationCurve approachCurve)
        {
            ApproachMoveSpeed = approachMoveSpeed;
            ApproachRotationSpeed = approachRotationSpeed;
            MinApproachDuration = minApproachDuration;
            StopDistancePadding = stopDistancePadding;
            PreserveCurrentDepth = preserveCurrentDepth;
            ApproachCurve = approachCurve;
        }
    }
}
