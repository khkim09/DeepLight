using Project.Core.Events;
using UnityEngine;

namespace Project.Gameplay.CameraSystem
{
    /// <summary>탐사 상단 방향 HUD용 잠수함 헤딩 정보를 발행하는 클래스이다.</summary>
    public class ExplorationNavigationTelemetryPublisher : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform target; // 잠수함 본체 Transform

        [Header("Publish Settings")]
        [SerializeField] private float republishThresholdDegrees = 0.1f; // 이 각도 이상 변했을 때만 재발행

        private float lastPublishedHeading = -999f; // 마지막 발행 각도

        /// <summary>외부에서 헤딩 기준 대상을 지정한다.</summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            PublishCurrentHeading(force: true);
        }

        /// <summary>시작 시 현재 헤딩을 즉시 발행한다.</summary>
        private void Start()
        {
            PublishCurrentHeading(force: true);
        }

        /// <summary>매 프레임 헤딩 변화를 감지해 필요한 경우 재발행한다.</summary>
        private void LateUpdate()
        {
            PublishCurrentHeading(force: false);
        }

        /// <summary>현재 잠수함 헤딩을 계산해 이벤트로 발행한다.</summary>
        private void PublishCurrentHeading(bool force)
        {
            if (target == null)
                return;

            Vector3 planarForward = target.forward;
            planarForward.y = 0f;

            if (planarForward.sqrMagnitude <= 0.0001f)
                return;

            planarForward.Normalize();

            float headingDegrees = Mathf.Atan2(planarForward.x, planarForward.z) * Mathf.Rad2Deg;
            if (headingDegrees < 0f)
                headingDegrees += 360f;

            if (!force)
            {
                float delta = Mathf.Abs(Mathf.DeltaAngle(lastPublishedHeading, headingDegrees));
                if (delta < republishThresholdDegrees)
                    return;
            }

            lastPublishedHeading = headingDegrees;

            EventBus.Publish(new ExplorationHeadingChangedEvent(
                headingDegrees,
                headingDegrees / 360f,
                ResolveMajorCardinal(headingDegrees)));
        }

        /// <summary>가장 가까운 주방위 문자열을 반환한다.</summary>
        private string ResolveMajorCardinal(float headingDegrees)
        {
            if (headingDegrees >= 315f || headingDegrees < 45f)
                return "N";

            if (headingDegrees < 135f)
                return "E";

            if (headingDegrees < 225f)
                return "S";

            return "W";
        }
    }
}
