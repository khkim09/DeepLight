using UnityEngine;

namespace Project.Gameplay.Submarine
{
    /// <summary>잠수정의 물리적 속도를 기반으로 프로펠러 회전 및 방향별 수중 이펙트를 제어하는 클래스이다.</summary>
    public class SubmarineVFXController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Rigidbody subRigidbody; // 잠수정 강체 (속도 측정용)
        [SerializeField] private Transform propellerTransform; // 후면 주 프로펠러 트랜스폼

        [Header("Particle Systems")]
        [SerializeField] private ParticleSystem forwardWakeVFX; // 전진 시 프로펠러 뒤로 뿜어지는 기포
        [SerializeField] private ParticleSystem backwardWakeVFX; // 후진 시 선체 쪽으로 퍼지는 난기류 기포
        [SerializeField] private ParticleSystem ascendVFX; // 부상 시 하단에서 나오는 기포 (또는 밸러스트)
        [SerializeField] private ParticleSystem descendVFX; // 하강 시 상단에서 나오는 기포

        [Header("Tuning")]
        [SerializeField] private float propellerSpeedMultiplier = 1500f; // 프로펠러 회전 속도 배율
        [SerializeField] private float vfxActivationThreshold = 0.2f; // 이펙트가 켜지기 위한 최소 속도 (노이즈 방지)

        /// <summary>매 프레임 잠수정의 로컬 속도를 계산하여 회전과 이펙트를 갱신한다.</summary>
        private void Update()
        {
            if (subRigidbody == null)
                return;

            // 글로벌 속도를 잠수정 기준의 로컬 속도로 변환하여 전후좌우 이동 상태를 파악한다.
            Vector3 localVelocity = subRigidbody.transform.InverseTransformDirection(subRigidbody.linearVelocity);

            UpdatePropellerRotation(localVelocity.z);
            UpdateVfxStates(localVelocity);
        }

        /// <summary>로컬 Z축 속도(전후진)에 비례하여 프로펠러 트랜스폼을 회전시킨다.</summary>
        private void UpdatePropellerRotation(float forwardVelocity)
        {
            if (propellerTransform == null)
                return;

            // 속도에 비례하여 회전량을 계산한다.
            // 전진(양수)이면 시계방향, 후진(음수)이면 반시계방향으로 자동 계산된다.
            // Z축을 기준으로 회전한다고 가정 (모델링에 따라 Vector3.forward 대신 right나 up으로 수정 필요)
            float rotationAmount = forwardVelocity * propellerSpeedMultiplier * Time.deltaTime;
            propellerTransform.Rotate(Vector3.forward, rotationAmount, Space.Self);
        }

        /// <summary>로컬 속도의 방향과 크기에 따라 각 파티클 시스템의 재생(Play)/정지(Stop)를 제어한다.</summary>
        private void UpdateVfxStates(Vector3 localVelocity)
        {
            // 전진 이펙트: Z축 속도가 임계값 이상일 때 재생
            ToggleParticle(forwardWakeVFX, localVelocity.z > vfxActivationThreshold);

            // 후진 이펙트: Z축 속도가 음수 임계값 이하일 때 재생
            ToggleParticle(backwardWakeVFX, localVelocity.z < -vfxActivationThreshold);

            // 부상 이펙트: Y축 속도가 임계값 이상일 때 재생
            ToggleParticle(ascendVFX, localVelocity.y > vfxActivationThreshold);

            // 하강 이펙트: Y축 속도가 음수 임계값 이하일 때 재생
            ToggleParticle(descendVFX, localVelocity.y < -vfxActivationThreshold);
        }

        /// <summary>상태 변화가 있을 때만 파티클 시스템의 Play/Stop을 호출하여 성능 낭비를 막는다.</summary>
        private void ToggleParticle(ParticleSystem ps, bool shouldPlay)
        {
            if (ps == null)
                return;

            if (shouldPlay && !ps.isPlaying)
                ps.Play();
            else if (!shouldPlay && ps.isPlaying)
                ps.Stop();
        }
    }
}
