using UnityEngine;

namespace Project.Gameplay.Runtime
{
    /// <summary>
    /// 잠수함의 전방 스포트라이트를 제어하며, 배터리 잔량에 따른 밝기 조절 및 Harvest 모드 센서 색상 전환을 처리합니다.
    /// </summary>
    public class SubmarineLightController : MonoBehaviour
    {
        [Header("Light References")]
        [Tooltip("상단 2개의 스포트라이트 (항시 유지)")]
        public Light[] topLights;

        [Tooltip("하단 2개의 스포트라이트 (Harvest 모드 시 소나/라이다 전환)")]
        public Light[] bottomLights;

        [Header("Intensity Settings")]
        [Tooltip("배터리 100%일 때의 최대 조명 강도")]
        public float maxIntensity = 10f;

        // 내부 배터리 상태 추적 (0.0f ~ 1.0f)
        private float _currentBatteryPercentage = 1f;

        [Header("Color Settings")]
        [Tooltip("일반 주행 시의 기본 라이트 색상")]
        public Color defaultColor = Color.white;

        [Tooltip("Harvest 모드 - 소나 활성화 시 색상 (오렌지/앰버)")]
        public Color sonarColor = new Color(1f, 0.7f, 0f);

        [Tooltip("Harvest 모드 - 라이다 활성화 시 색상 (마젠타)")]
        public Color lidarColor = new Color(1f, 0f, 1f);

        // 현재 Harvest 모드 진입 여부
        private bool _isHarvestMode = false;

        private void Start()
        {
            // 초기화: 모든 라이트를 기본 색상과 최대 밝기로 설정합니다.
            SetAllLightsColor(defaultColor);
            UpdateLightIntensity();
        }

        /// <summary>
        /// 배터리 시스템에서 호출되어 남은 전력에 따라 스포트라이트의 강도를 조절합니다.
        /// </summary>
        /// <param name="percentage">현재 배터리 잔량 비율 (0.0 ~ 1.0)</param>
        public void SetBatteryLevel(float percentage)
        {
            _currentBatteryPercentage = Mathf.Clamp01(percentage);
            UpdateLightIntensity();
        }

        /// <summary>
        /// Harvest 모드에 진입하거나 센서를 전환할 때 하단 라이트의 색상을 변경합니다.
        /// </summary>
        /// <param name="useSonar">true면 소나(앰버), false면 라이다(마젠타) 색상을 적용합니다.</param>
        public void SetHarvestSensorMode(bool useSonar)
        {
            _isHarvestMode = true;
            Color targetColor = useSonar ? sonarColor : lidarColor;

            // 하단 조명만 센서 전용 색상으로 변경합니다.
            foreach (var light in bottomLights)
            {
                if (light != null) light.color = targetColor;
            }
        }

        /// <summary>
        /// Harvest 모드를 종료하고 모든 라이트를 일반 주행 모드로 복구합니다.
        /// </summary>
        public void ExitHarvestMode()
        {
            _isHarvestMode = false;
            foreach (var light in bottomLights)
            {
                if (light != null) light.color = defaultColor;
            }
        }

        /// <summary>
        /// 현재 배터리 비율에 맞춰 모든 라이트의 실제 강도를 업데이트합니다.
        /// </summary>
        private void UpdateLightIntensity()
        {
            float calculatedIntensity = maxIntensity * _currentBatteryPercentage;

            foreach (var light in topLights)
                if (light != null) light.intensity = calculatedIntensity;

            foreach (var light in bottomLights)
                if (light != null) light.intensity = calculatedIntensity;
        }

        /// <summary>
        /// 상/하단 모든 라이트의 색상을 일괄 변경하는 유틸리티 메서드입니다.
        /// </summary>
        private void SetAllLightsColor(Color color)
        {
            foreach (var light in topLights)
                if (light != null) light.color = color;

            foreach (var light in bottomLights)
                if (light != null) light.color = color;
        }
    }
}
