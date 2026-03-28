using System;
using UnityEngine;

namespace Project.Data.Common
{
    /// <summary>기본 수치에 런타임 가산과 배율을 적용하는 값 타입이다.</summary>
    [Serializable]
    public struct RuntimeStatModifier
    {
        [SerializeField] private float additive;   // 기본값에 더할 수치
        [SerializeField] private float multiplier; // 가산 후 곱할 배율

        /// <summary>기본값에 현재 보정값을 적용한 최종 수치를 반환한다.</summary>
        public float Apply(float baseValue)
        {
            // 배율이 0 이하로 내려가면 의도치 않은 0배/음수 배율이 되므로 1로 보정한다.
            float resolvedMultiplier = multiplier <= 0f ? 1f : multiplier;
            return (baseValue + additive) * resolvedMultiplier;
        }

        /// <summary>아무 보정도 하지 않는 기본 보정값을 반환한다.</summary>
        public static RuntimeStatModifier Identity()
        {
            return new RuntimeStatModifier
            {
                additive = 0f,
                multiplier = 1f
            };
        }
    }
}
