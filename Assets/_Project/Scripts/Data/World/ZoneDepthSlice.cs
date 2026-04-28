using System;
using UnityEngine;

namespace Project.Data.World
{
    /// <summary>
    /// 하나의 Zone 내부에서 Y 수심 구간을 표현하는 직렬화 가능 구조체.
    /// 예: Surface(20~0), Shallow(0~-200), Mid(-200~-600), Deep(-600~-1200), Forbidden(-1200~maxDepth)
    /// topY는 더 위쪽(값이 큰 쪽), bottomY는 더 아래쪽(값이 작은 쪽)이다.
    /// </summary>
    [Serializable]
    public struct ZoneDepthSlice
    {
        [SerializeField] private ZoneDepthBand depthBand; // 수심 대역
        [SerializeField] private float topY; // 구간 상단 Y (예: 0)
        [SerializeField] private float bottomY; // 구간 하단 Y (예: -200)
        [SerializeField] private float normalizedStart01; // waterLevelY 기준 normalized 시작 (0~1)
        [SerializeField] private float normalizedEnd01; // waterLevelY 기준 normalized 끝 (0~1)
        [SerializeField] private Color debugColor; // 디버그 시각화 색상

        /// <summary>수심 대역</summary>
        public ZoneDepthBand DepthBand => depthBand;

        /// <summary>구간 상단 Y (더 위쪽, 값이 더 큼, 예: 0)</summary>
        public float TopY => topY;

        /// <summary>구간 하단 Y (더 아래쪽, 값이 더 작음, 예: -200)</summary>
        public float BottomY => bottomY;

        /// <summary>waterLevelY 기준 normalized 시작 (0~1, 0=수면, 1=maxDepth)</summary>
        public float NormalizedStart01 => normalizedStart01;

        /// <summary>waterLevelY 기준 normalized 끝 (0~1, 0=수면, 1=maxDepth)</summary>
        public float NormalizedEnd01 => normalizedEnd01;

        /// <summary>디버그 시각화 색상</summary>
        public Color DebugColor => debugColor;

        /// <summary>
        /// ZoneDepthSlice 생성자.
        /// </summary>
        /// <param name="depthBand">수심 대역</param>
        /// <param name="topY">구간 상단 Y (더 위쪽)</param>
        /// <param name="bottomY">구간 하단 Y (더 아래쪽)</param>
        /// <param name="normalizedStart01">waterLevelY 기준 normalized 시작</param>
        /// <param name="normalizedEnd01">waterLevelY 기준 normalized 끝</param>
        /// <param name="debugColor">디버그 시각화 색상</param>
        public ZoneDepthSlice(ZoneDepthBand depthBand, float topY, float bottomY,
            float normalizedStart01, float normalizedEnd01, Color debugColor)
        {
            this.depthBand = depthBand;
            this.topY = topY;
            this.bottomY = bottomY;
            this.normalizedStart01 = normalizedStart01;
            this.normalizedEnd01 = normalizedEnd01;
            this.debugColor = debugColor;
        }

        /// <summary>
        /// 주어진 Y값이 이 slice 범위 내에 있는지 확인한다.
        /// topY >= y >= bottomY 이면 true.
        /// </summary>
        public bool ContainsY(float y)
        {
            return y <= topY && y >= bottomY;
        }
    }
}
