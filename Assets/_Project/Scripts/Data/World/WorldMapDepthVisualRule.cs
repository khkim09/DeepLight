using System;
using UnityEngine;

namespace Project.Data.World
{
    /// <summary>
    /// 하나의 DepthBand 내부에서 normalizedDepth01에 따른 profile 보간 규칙을 제공하는 Serializable class.
    /// </summary>
    [Serializable]
    public class WorldMapDepthVisualRule
    {
        [SerializeField] private ZoneDepthBand depthBand; // 수심 대역
        [SerializeField] private float normalizedStart01; // normalized 시작 (0~1)
        [SerializeField] private float normalizedEnd01; // normalized 끝 (0~1)
        [SerializeField] private WorldMapVisualProfile profileAtStart; // 시작점 프로필
        [SerializeField] private WorldMapVisualProfile profileAtEnd; // 끝점 프로필

        /// <summary>수심 대역</summary>
        public ZoneDepthBand DepthBand => depthBand;

        /// <summary>normalized 시작 (0~1)</summary>
        public float NormalizedStart01 => normalizedStart01;

        /// <summary>normalized 끝 (0~1)</summary>
        public float NormalizedEnd01 => normalizedEnd01;

        /// <summary>시작점 프로필</summary>
        public WorldMapVisualProfile ProfileAtStart => profileAtStart;

        /// <summary>끝점 프로필</summary>
        public WorldMapVisualProfile ProfileAtEnd => profileAtEnd;

        /// <summary>
        /// 주어진 normalizedDepth01이 이 규칙의 범위 내에 있는지 확인한다.
        /// </summary>
        public bool Contains(float normalizedDepth01)
        {
            return normalizedDepth01 >= normalizedStart01 && normalizedDepth01 <= normalizedEnd01;
        }

        /// <summary>
        /// 주어진 normalizedDepth01에 대해 보간된 VisualProfile을 계산한다.
        /// </summary>
        public WorldMapVisualProfile Evaluate(float normalizedDepth01)
        {
            // 범위 내로 clamp
            float clamped = Mathf.Clamp(normalizedDepth01, normalizedStart01, normalizedEnd01);

            // 0~1 t 계산
            float range = normalizedEnd01 - normalizedStart01;
            float t = Mathf.Approximately(range, 0f) ? 0f : (clamped - normalizedStart01) / range;

            // Lerp
            WorldMapVisualProfile result = WorldMapVisualProfile.Lerp(profileAtStart, profileAtEnd, t);
            result.ClampValues();
            return result;
        }
    }
}
