using UnityEngine;

namespace Project.Gameplay.Submarine
{
    /// <summary>잠수정의 물리적 속도를 기반으로 프로펠러의 회전을 제어하는 클래스이다.</summary>
    public class SubmarinePropellerController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Rigidbody subRigidbody; // 속도 측정을 위한 잠수정 강체
        [SerializeField] private Transform propellerTransform; // 회전시킬 프로펠러 트랜스폼

        [Header("Tuning")]
        [SerializeField] private float rotationSpeedMultiplier = 1500f; // 기본 회전 속도 배율
        [SerializeField] private Vector3 rotationAxis = Vector3.forward; // 회전 축 (모델에 따라 변경 가능)

        /// <summary>매 프레임 잠수정의 로컬 속도를 기반으로 프로펠러를 회전시킨다.</summary>
        private void Update()
        {
            if (subRigidbody == null || propellerTransform == null)
                return;

            // 글로벌 속도를 잠수정 기준의 로컬 속도로 변환하여 이동 상태를 파악한다.
            Vector3 localVelocity = subRigidbody.transform.InverseTransformDirection(subRigidbody.linearVelocity);

            // 전진(+Z), 상승(+Y)은 양수 / 후진(-Z), 하강(-Y)은 음수로 간주하여 값을 합산한다.
            // 합산된 값이 양수면 시계 방향, 음수면 반시계 방향으로 자동 회전된다.
            float effectiveSpeed = localVelocity.z + localVelocity.y;

            // 최종 회전량 계산 (유효 속도 * 배율 * 델타타임)
            float rotationAmount = effectiveSpeed * rotationSpeedMultiplier * Time.deltaTime;

            // 지정된 축을 기준으로 로컬 회전 적용
            propellerTransform.Rotate(rotationAxis, rotationAmount, Space.Self);
        }
    }
}
