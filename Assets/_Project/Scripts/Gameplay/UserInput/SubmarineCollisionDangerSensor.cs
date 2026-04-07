using System.Collections.Generic;
using Project.Gameplay.Runtime;
using UnityEngine;

namespace Project.Gameplay.UserInput
{
    /// <summary>잠수함 충돌을 감지해 위험 피해와 화면 피드백을 발생시키는 센서이다.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class SubmarineCollisionDangerSensor : MonoBehaviour
    {
        [Header("Collision Filter")]
        [SerializeField] private LayerMask damageLayerMask = ~0; // 피해를 줄 충돌 레이어
        [SerializeField] private float minRelativeVelocityToDamage = 2.5f; // 피해 시작 상대 속도
        [SerializeField] private float maxRelativeVelocityForMaxDamage = 18f; // 최대 피해 기준 속도

        [Header("Hull Damage")]
        [SerializeField] private float minHullDamage = 1f; // 최소 내구도 피해
        [SerializeField] private float maxHullDamage = 7f; // 최대 내구도 피해

        [Header("Battery Damage")]
        [SerializeField] private bool applyBatteryDangerDamage = true; // 충돌 시 배터리 위험 소모 여부
        [SerializeField] private float minBatteryDamage = 0.5f; // 최소 배터리 피해
        [SerializeField] private float maxBatteryDamage = 5f; // 최대 배터리 피해
        [SerializeField] private float batteryDangerIntensityMultiplier = 1f; // 배터리 danger 연출 강도

        [Header("Debug")]
        [SerializeField] private bool ignoreTriggerColliders = true; // 트리거 무시 여부

        private readonly HashSet<int> activeContactKeys = new(); // 현재 접촉 중인 대상 키
        private SubmarineRuntimeState submarineRuntimeState; // 잠수함 런타임 상태
        private Rigidbody ownRigidbody; // 자기 자신의 Rigidbody

        /// <summary>초기 Rigidbody 참조를 캐싱한다.</summary>
        private void Awake()
        {
            ownRigidbody = GetComponent<Rigidbody>();
        }

        /// <summary>비활성화 시 현재 접촉 상태를 정리한다.</summary>
        private void OnDisable()
        {
            activeContactKeys.Clear();
        }

        /// <summary>잠수함 런타임 상태를 주입한다.</summary>
        public void Initialize(SubmarineRuntimeState newSubmarineRuntimeState)
        {
            submarineRuntimeState = newSubmarineRuntimeState;
        }

        /// <summary>충돌 시작 시 최초 1회만 상대 속도 기반 피해를 적용한다.</summary>
        private void OnCollisionEnter(Collision collision)
        {
            TryApplyCollisionDamageOnEnter(collision);
        }

        /// <summary>충돌 종료 시 해당 접촉 대상을 재피해 가능 상태로 되돌린다.</summary>
        private void OnCollisionExit(Collision collision)
        {
            if (collision == null || collision.collider == null)
                return;

            int contactKey = BuildContactKey(collision.collider);
            activeContactKeys.Remove(contactKey);
        }

        /// <summary>충돌 진입 순간의 상대 속도로 피해를 적용한다.</summary>
        private void TryApplyCollisionDamageOnEnter(Collision collision)
        {
            if (submarineRuntimeState == null)
                return;

            if (collision == null || collision.collider == null)
                return;

            Collider otherCollider = collision.collider;
            if (ignoreTriggerColliders && otherCollider.isTrigger)
                return;

            if (!IsLayerIncluded(otherCollider.gameObject.layer, damageLayerMask))
                return;

            int contactKey = BuildContactKey(otherCollider);

            // 같은 접촉 쌍이 유지되는 동안에는 최초 1회만 피해
            if (activeContactKeys.Contains(contactKey))
                return;

            activeContactKeys.Add(contactKey);

            float relativeVelocity = collision.relativeVelocity.magnitude;
            if (relativeVelocity < minRelativeVelocityToDamage)
                return;

            float velocity01 = Mathf.InverseLerp(
                minRelativeVelocityToDamage,
                Mathf.Max(minRelativeVelocityToDamage + 0.01f, maxRelativeVelocityForMaxDamage),
                relativeVelocity);

            // 내구도 danger 피해
            float hullDamageAmount = Mathf.Lerp(minHullDamage, maxHullDamage, velocity01);
            submarineRuntimeState.DamageHullDanger(
                hullDamageAmount,
                SubmarineDangerFeedbackType.CollisionWithEnvironment,
                1f);

            // 배터리 danger 피해
            if (applyBatteryDangerDamage)
            {
                float batteryDamageAmount = Mathf.Lerp(minBatteryDamage, maxBatteryDamage, velocity01);
                submarineRuntimeState.ConsumeBatteryDanger(
                    batteryDamageAmount,
                    batteryDangerIntensityMultiplier);
            }
        }

        /// <summary>접촉 대상을 대표하는 키를 생성한다.</summary>
        private int BuildContactKey(Collider otherCollider)
        {
            if (otherCollider == null)
                return 0;

            // 상대에 Rigidbody가 있으면 Rigidbody 단위로, 없으면 루트 Transform 기준으로 묶는다.
            Rigidbody otherBody = otherCollider.attachedRigidbody;
            if (otherBody != null && otherBody != ownRigidbody)
                return otherBody.GetInstanceID();

            return otherCollider.transform.root.GetInstanceID();
        }

        /// <summary>레이어가 마스크에 포함되는지 확인한다.</summary>
        private bool IsLayerIncluded(int layer, LayerMask mask)
        {
            return (mask.value & (1 << layer)) != 0;
        }
    }
}
