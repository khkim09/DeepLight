using System;
using UnityEngine;

namespace Project.Data.CameraSystem
{
    /// <summary>탐사 3인칭 카메라의 고정 기본값과 감도 계산 규칙을 정의하는 SO이다.</summary>
    [CreateAssetMenu(
        fileName = "ExplorationCameraTuningSO",
        menuName = "Project/Camera/Exploration Camera Tuning")]
    public class ExplorationCameraTuningSO : ScriptableObject
    {
        [Header("Base Follow")]
        [SerializeField] private BaseFollowSettings baseFollow; // 기본 추적 세팅

        [Header("Distance")]
        [SerializeField] private DistanceSettings distance; // 상태별 거리 세팅

        [Header("Free Look")]
        [SerializeField] private FreeLookSettings freeLook; // 자유시점 감도/각도 세팅

        /// <summary>유저 옵션 감도를 반영한 탐사 카메라 런타임 세팅을 생성한다.</summary>
        public ExplorationCameraRuntimeSettings BuildRuntimeSettings(ExplorationCameraUserSettings userSettings)
        {
            float resolvedYawMultiplier = userSettings.GetResolvedYawMultiplier();
            float resolvedPitchMultiplier = userSettings.GetResolvedPitchMultiplier();

            return new ExplorationCameraRuntimeSettings(
                defaultOffsetY: baseFollow.defaultOffsetY,
                rotationFollowSpeed: Mathf.Max(0.01f, baseFollow.rotationFollowSpeed),
                idleDistanceZ: distance.idleDistanceZ,
                normalForwardDistanceZ: distance.normalForwardDistanceZ,
                boostForwardDistanceZ: distance.boostForwardDistanceZ,
                normalBackwardDistanceZ: distance.normalBackwardDistanceZ,
                boostBackwardDistanceZ: distance.boostBackwardDistanceZ,
                distanceSmoothTime: Mathf.Max(0.01f, distance.distanceSmoothTime),
                freeLookTopOffset: freeLook.freeLookTopOffset,
                mouseSensitivityX: Mathf.Max(0.01f, freeLook.mouseSensitivityX * resolvedYawMultiplier),
                mouseSensitivityY: Mathf.Max(0.01f, freeLook.mouseSensitivityY * resolvedPitchMultiplier),
                minPitch: freeLook.minPitch,
                maxPitch: freeLook.maxPitch,
                returnSpeed: freeLook.returnSpeed);
        }

        /// <summary>기본 추적 수치 묶음이다.</summary>
        [Serializable]
        public struct BaseFollowSettings
        {
            public float defaultOffsetY;      // 기본 높이 오프셋
            public float rotationFollowSpeed; // 회전 추적 속도
        }

        /// <summary>상태별 카메라 거리 수치 묶음이다.</summary>
        [Serializable]
        public struct DistanceSettings
        {
            public float idleDistanceZ;
            public float normalForwardDistanceZ;
            public float boostForwardDistanceZ;
            public float normalBackwardDistanceZ;
            public float boostBackwardDistanceZ;
            public float distanceSmoothTime;
        }

        /// <summary>자유 시점 수치 묶음이다.</summary>
        [Serializable]
        public struct FreeLookSettings
        {
            public Vector3 freeLookTopOffset;
            public float mouseSensitivityX;
            public float mouseSensitivityY;
            public float minPitch;
            public float maxPitch;
            public float returnSpeed;
        }
    }

    /// <summary>탐사 카메라용 유저 옵션 감도 상태이다.</summary>
    [Serializable]
    public struct ExplorationCameraUserSettings
    {
        [SerializeField] private float yawSensitivityMultiplier;
        [SerializeField] private float pitchSensitivityMultiplier;

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
        public static ExplorationCameraUserSettings Default()
        {
            return new ExplorationCameraUserSettings
            {
                yawSensitivityMultiplier = 1f,
                pitchSensitivityMultiplier = 1f
            };
        }
    }

    /// <summary>탐사 카메라에 실제 적용되는 런타임 세팅이다.</summary>
    public readonly struct ExplorationCameraRuntimeSettings
    {
        public readonly float DefaultOffsetY;
        public readonly float RotationFollowSpeed;
        public readonly float IdleDistanceZ;
        public readonly float NormalForwardDistanceZ;
        public readonly float BoostForwardDistanceZ;
        public readonly float NormalBackwardDistanceZ;
        public readonly float BoostBackwardDistanceZ;
        public readonly float DistanceSmoothTime;
        public readonly Vector3 FreeLookTopOffset;
        public readonly float MouseSensitivityX;
        public readonly float MouseSensitivityY;
        public readonly float MinPitch;
        public readonly float MaxPitch;
        public readonly float ReturnSpeed;

        /// <summary>탐사 카메라 런타임 세팅을 생성한다.</summary>
        public ExplorationCameraRuntimeSettings(
            float defaultOffsetY,
            float rotationFollowSpeed,
            float idleDistanceZ,
            float normalForwardDistanceZ,
            float boostForwardDistanceZ,
            float normalBackwardDistanceZ,
            float boostBackwardDistanceZ,
            float distanceSmoothTime,
            Vector3 freeLookTopOffset,
            float mouseSensitivityX,
            float mouseSensitivityY,
            float minPitch,
            float maxPitch,
            float returnSpeed)
        {
            DefaultOffsetY = defaultOffsetY;
            RotationFollowSpeed = rotationFollowSpeed;
            IdleDistanceZ = idleDistanceZ;
            NormalForwardDistanceZ = normalForwardDistanceZ;
            BoostForwardDistanceZ = boostForwardDistanceZ;
            NormalBackwardDistanceZ = normalBackwardDistanceZ;
            BoostBackwardDistanceZ = boostBackwardDistanceZ;
            DistanceSmoothTime = distanceSmoothTime;
            FreeLookTopOffset = freeLookTopOffset;
            MouseSensitivityX = mouseSensitivityX;
            MouseSensitivityY = mouseSensitivityY;
            MinPitch = minPitch;
            MaxPitch = maxPitch;
            ReturnSpeed = returnSpeed;
        }
    }
}
