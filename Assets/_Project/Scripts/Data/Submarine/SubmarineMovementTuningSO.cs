using System;
using UnityEngine;

namespace Project.Data.Submarine
{
    /// <summary>잠수함 탐사 이동의 고정 기본값과 감도 계산 규칙을 정의하는 SO이다.</summary>
    [CreateAssetMenu(
        fileName = "SubmarineMovementTuningSO",
        menuName = "Project/Submarine/Submarine Movement Tuning")]
    public class SubmarineMovementTuningSO : ScriptableObject
    {
        [Header("Fixed Movement")]
        [SerializeField] private FixedMovementSettings fixedMovement; // 게임 내부 고정 이동 수치

        [Header("Default Input Sensitivity")]
        [SerializeField] private DefaultInputSensitivity defaultInputSensitivity; // 기본 마우스 감도

        /// <summary>유저 옵션 감도를 반영한 실제 런타임 이동 세팅을 생성한다.</summary>
        public SubmarineMovementRuntimeSettings BuildRuntimeSettings(SubmarineControlUserSettings userSettings)
        {
            float resolvedYawMultiplier = userSettings.GetResolvedYawMultiplier();
            float resolvedPitchMultiplier = userSettings.GetResolvedPitchMultiplier();

            return new SubmarineMovementRuntimeSettings(
                maxForwardSpeed: Mathf.Max(0f, fixedMovement.maxForwardSpeed),
                maxReverseSpeed: Mathf.Max(0f, fixedMovement.maxReverseSpeed),
                forwardAcceleration: Mathf.Max(0f, fixedMovement.forwardAcceleration),
                reverseAcceleration: Mathf.Max(0f, fixedMovement.reverseAcceleration),
                boostMultiplier: Mathf.Max(0.01f, fixedMovement.boostMultiplier),
                ascendSpeed: Mathf.Max(0f, fixedMovement.ascendSpeed),
                descendSpeed: Mathf.Max(0f, fixedMovement.descendSpeed),
                idleDrag: Mathf.Max(0f, fixedMovement.idleDrag),
                brakeDeceleration: Mathf.Max(0f, fixedMovement.brakeDeceleration),
                maxPitchAngle: Mathf.Clamp(fixedMovement.maxPitchAngle, 1f, 89f),
                rotationSmoothTime: Mathf.Max(0.01f, fixedMovement.rotationSmoothTime),
                loopRotationSpeed: fixedMovement.loopRotationSpeed,
                verticalSmoothTime: Mathf.Max(0.01f, fixedMovement.verticalSmoothTime),
                yawSensitivity: Mathf.Max(0.01f, defaultInputSensitivity.yawSensitivity * resolvedYawMultiplier),
                pitchSensitivity: Mathf.Max(0.01f, defaultInputSensitivity.pitchSensitivity * resolvedPitchMultiplier));
        }

        /// <summary>게임 내부에서 고정으로 사용할 잠수함 이동 수치 묶음이다.</summary>
        [Serializable]
        public struct FixedMovementSettings
        {
            public float maxForwardSpeed;     // 최대 전진 속도
            public float maxReverseSpeed;     // 최대 후진 속도
            public float forwardAcceleration; // 전진 가속도
            public float reverseAcceleration; // 후진 가속도
            public float boostMultiplier;     // 부스트 배율
            public float ascendSpeed;         // 상승 속도
            public float descendSpeed;        // 하강 속도
            public float idleDrag;            // 무입력 감속도
            public float brakeDeceleration;   // 제동 감속도
            public float maxPitchAngle;       // 최대 피치 각도
            public float rotationSmoothTime;  // 회전 스무딩 시간
            public float loopRotationSpeed;   // 루프 보조 회전 속도
            public float verticalSmoothTime;  // 수직 이동 스무딩 시간
        }

        /// <summary>기본 입력 감도 묶음이다.</summary>
        [Serializable]
        public struct DefaultInputSensitivity
        {
            public float yawSensitivity;   // 기본 yaw 감도
            public float pitchSensitivity; // 기본 pitch 감도
        }
    }

    /// <summary>잠수함 조작용 유저 옵션 감도 상태이다.</summary>
    [Serializable]
    public struct SubmarineControlUserSettings
    {
        [SerializeField] private float yawSensitivityMultiplier;   // yaw 감도 배율
        [SerializeField] private float pitchSensitivityMultiplier; // pitch 감도 배율

        /// <summary>유효한 yaw 감도 배율을 반환한다.</summary>
        public float GetResolvedYawMultiplier()
        {
            return yawSensitivityMultiplier <= 0f ? 1f : yawSensitivityMultiplier;
        }

        /// <summary>유효한 pitch 감도 배율을 반환한다.</summary>
        public float GetResolvedPitchMultiplier()
        {
            return pitchSensitivityMultiplier <= 0f ? 1f : pitchSensitivityMultiplier;
        }

        /// <summary>기본 옵션 상태를 반환한다.</summary>
        public static SubmarineControlUserSettings Default()
        {
            return new SubmarineControlUserSettings
            {
                yawSensitivityMultiplier = 1f,
                pitchSensitivityMultiplier = 1f
            };
        }
    }

    /// <summary>잠수함 이동에 실제 적용되는 런타임 계산 결과이다.</summary>
    public readonly struct SubmarineMovementRuntimeSettings
    {
        public readonly float MaxForwardSpeed;
        public readonly float MaxReverseSpeed;
        public readonly float ForwardAcceleration;
        public readonly float ReverseAcceleration;
        public readonly float BoostMultiplier;
        public readonly float AscendSpeed;
        public readonly float DescendSpeed;
        public readonly float IdleDrag;
        public readonly float BrakeDeceleration;
        public readonly float MaxPitchAngle;
        public readonly float RotationSmoothTime;
        public readonly float LoopRotationSpeed;
        public readonly float VerticalSmoothTime;
        public readonly float YawSensitivity;
        public readonly float PitchSensitivity;

        /// <summary>잠수함 이동 런타임 세팅을 생성한다.</summary>
        public SubmarineMovementRuntimeSettings(
            float maxForwardSpeed,
            float maxReverseSpeed,
            float forwardAcceleration,
            float reverseAcceleration,
            float boostMultiplier,
            float ascendSpeed,
            float descendSpeed,
            float idleDrag,
            float brakeDeceleration,
            float maxPitchAngle,
            float rotationSmoothTime,
            float loopRotationSpeed,
            float verticalSmoothTime,
            float yawSensitivity,
            float pitchSensitivity)
        {
            MaxForwardSpeed = maxForwardSpeed;
            MaxReverseSpeed = maxReverseSpeed;
            ForwardAcceleration = forwardAcceleration;
            ReverseAcceleration = reverseAcceleration;
            BoostMultiplier = boostMultiplier;
            AscendSpeed = ascendSpeed;
            DescendSpeed = descendSpeed;
            IdleDrag = idleDrag;
            BrakeDeceleration = brakeDeceleration;
            MaxPitchAngle = maxPitchAngle;
            RotationSmoothTime = rotationSmoothTime;
            LoopRotationSpeed = loopRotationSpeed;
            VerticalSmoothTime = verticalSmoothTime;
            YawSensitivity = yawSensitivity;
            PitchSensitivity = pitchSensitivity;
        }
    }
}
