using UnityEngine;

namespace Project.Gameplay.Submarine
{
    /// <summary>잠수정의 물리적 로컬 속도를 측정하여 이동 방향에 맞는 수중 이펙트를 제어하는 클래스이다.</summary>
    public class SubmarineVFXController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Rigidbody subRigidbody; // 속도 측정을 위한 잠수정 강체

        [Header("Particle Systems")]
        [SerializeField] private ParticleSystem forwardWakeVFX; // 전진 시 후면으로 뿜어지는 원뿔형 물기둥/회오리
        [SerializeField] private ParticleSystem backwardWakeVFX; // 후진 시 프로펠러 주변에 퍼지는 난기류
        [SerializeField] private ParticleSystem ascendVFX; // 상승 시 선체 하단에서 뿜어지는 기포
        [SerializeField] private ParticleSystem descendVFX; // 하강 시 선체 상단에서 뿜어지는 기포

        [Header("Tuning")]
        [SerializeField] private float vfxActivationThreshold = 0.5f; // 파티클이 켜지기 위한 최소 속도 임계값 (데드존)

        /// <summary>매 프레임 강체의 로컬 속도를 계산하여 방향별 파티클 재생 상태를 갱신한다.</summary>
        private void Update()
        {
            if (subRigidbody == null)
                return;

            // 글로벌 속도를 잠수정 기준의 로컬 속도로 변환하여 현재 어느 방향으로 이동 중인지 파악한다.
            Vector3 localVelocity = subRigidbody.transform.InverseTransformDirection(subRigidbody.linearVelocity);

            UpdateVfxStates(localVelocity);
        }

        /// <summary>로컬 속도의 방향과 크기를 판별하여 각 파티클 시스템을 Play/Stop 처리한다.</summary>
        private void UpdateVfxStates(Vector3 localVelocity)
        {
            // Z축(전후진) 속도 판별: 임계값을 넘어설 때만 켠다.
            ToggleParticle(forwardWakeVFX, localVelocity.z > vfxActivationThreshold);
            ToggleParticle(backwardWakeVFX, localVelocity.z < -vfxActivationThreshold);

            // Y축(상하강) 속도 판별
            ToggleParticle(ascendVFX, localVelocity.y > vfxActivationThreshold);
            ToggleParticle(descendVFX, localVelocity.y < -vfxActivationThreshold);
        }

        /// <summary>파티클의 현재 재생 상태를 확인하고, 변경이 필요할 때만 Play/Stop을 호출하여 성능을 최적화한다.</summary>
        private void ToggleParticle(ParticleSystem ps, bool shouldPlay)
        {
            if (ps == null)
                return;

            if (shouldPlay && !ps.isPlaying)
            {
                ps.Play();
            }
            else if (!shouldPlay && ps.isPlaying)
            {
                ps.Stop();
            }
        }
    }
}
