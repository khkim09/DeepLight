using System;
using Project.Core.Events;
using Project.Data.Claw;
using UnityEngine;

namespace Project.Gameplay.Runtime
{
    [Serializable]
    public class ClawRuntimeState
    {
        [SerializeField] private ClawStatsSO baseStats; // 로봇 팔 기본 스탯 데이터
        [SerializeField] private float currentDurability; // 현재 팔 내구도

        public ClawStatsSO BaseStats => baseStats;
        public float CurrentDurability => currentDurability;

        /// <summary>로봇 팔 런타임 상태 생성</summary>
        public ClawRuntimeState(ClawStatsSO baseStats)
        {
            this.baseStats = baseStats;
            currentDurability = baseStats.MaxDurability;
        }

        /// <summary>로봇 팔 런타임 상태 초기화</summary>
        public void Initialize(ClawStatsSO newBaseStats)
        {
            baseStats = newBaseStats;
            currentDurability = baseStats.MaxDurability;
        }

        /// <summary>로봇 팔 내구도 감소</summary>
        public void Damage(float amount)
        {
            currentDurability = Mathf.Max(0f, currentDurability - amount);
            EventBus.Publish(new ClawDurabilityChangedEvent(currentDurability, baseStats.MaxDurability));
        }

        /// <summary>로봇 팔 내구도 회복</summary>
        public void Repair(float amount)
        {
            currentDurability = Mathf.Min(baseStats.MaxDurability, currentDurability + amount);
            EventBus.Publish(new ClawDurabilityChangedEvent(currentDurability, baseStats.MaxDurability));
        }
    }
}
