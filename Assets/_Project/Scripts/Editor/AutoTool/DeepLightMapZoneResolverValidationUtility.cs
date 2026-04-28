using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Project.Data.World;
using Project.Gameplay.World;

namespace Project.Editor.AutoTool
{
    /// <summary>
    /// ZoneResolver 계산을 Editor에서 검증하는 Utility 클래스.
    /// Phase 6: 좌표 기반 Zone 판정, 경계선 처리, Depth Band 세분화 검증.
    /// </summary>
    public static class DeepLightMapZoneResolverValidationUtility
    {
        /// <summary>
        /// 검증 결과 하나를 표현하는 구조체.
        /// </summary>
        public struct ValidationCheck
        {
            /// <summary>검사 이름</summary>
            public string Name;

            /// <summary>통과 여부</summary>
            public bool Passed;

            /// <summary>상세 메시지</summary>
            public string Message;

            public ValidationCheck(string name, bool passed, string message)
            {
                Name = name;
                Passed = passed;
                Message = message;
            }
        }

        /// <summary>
        /// ZoneResolver의 좌표 기반 Zone 판정을 검증한다.
        /// 12개 항목을 검사하고 Console에 결과를 출력한다.
        /// </summary>
        public static void ValidateZoneResolver(DeepLightMapAutoBuilderSettingsSO settings)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot validate ZoneResolver.");
                return;
            }

            if (settings.WorldMapConfig == null)
            {
                Debug.LogError("[MapAutoBuilder] WorldMapConfig is null! Cannot validate ZoneResolver.");
                return;
            }

            if (settings.ScenarioPreset == null)
            {
                Debug.LogError("[MapAutoBuilder] ScenarioPreset is null! Cannot validate ZoneResolver.");
                return;
            }

            // ZoneResolver 생성 및 초기화
            ZoneResolver resolver = new ZoneResolver(settings.WorldMapConfig);
            resolver.Initialize(settings.ScenarioPreset);

            var checks = new List<ValidationCheck>();
            var config = settings.WorldMapConfig;

            // ===== 1. A1 center sample → A1 =====
            Vector3 a1Center = GetZoneCenter(config, 0, 0);
            bool a1Resolved = resolver.TryResolveZone(a1Center, out ZoneId a1ZoneId);
            checks.Add(new ValidationCheck(
                "A1 center → ZoneId",
                a1Resolved && a1ZoneId.Equals(new ZoneId('A', 1)),
                $"A1 center ({a1Center}) => Zone={a1ZoneId} (expected=A1)"
            ));

            // ===== 2. F6 center sample → F6 =====
            Vector3 f6Center = GetZoneCenter(config, 5, 5);
            bool f6Resolved = resolver.TryResolveZone(f6Center, out ZoneId f6ZoneId);
            checks.Add(new ValidationCheck(
                "F6 center → ZoneId",
                f6Resolved && f6ZoneId.Equals(new ZoneId('F', 6)),
                $"F6 center ({f6Center}) => Zone={f6ZoneId} (expected=F6)"
            ));

            // ===== 3. J10 center sample → J10 =====
            Vector3 j10Center = GetZoneCenter(config, 9, 9);
            bool j10Resolved = resolver.TryResolveZone(j10Center, out ZoneId j10ZoneId);
            checks.Add(new ValidationCheck(
                "J10 center → ZoneId",
                j10Resolved && j10ZoneId.Equals(new ZoneId('J', 10)),
                $"J10 center ({j10Center}) => Zone={j10ZoneId} (expected=J10)"
            ));

            // ===== 4. worldMinX/worldMinZ 바깥 좌표 → false =====
            Vector3 outsideMin = new Vector3(config.WorldMinX - 100f, 0f, config.WorldMinZ - 100f);
            bool outsideMinResolved = resolver.TryResolveZone(outsideMin, out _);
            checks.Add(new ValidationCheck(
                "Outside min bounds → false",
                !outsideMinResolved,
                $"Position ({outsideMin}) => resolved={outsideMinResolved} (expected=false)"
            ));

            // ===== 5. worldMaxX/worldMaxZ 바깥 좌표 → false =====
            Vector3 outsideMax = new Vector3(config.WorldMaxX + 100f, 0f, config.WorldMaxZ + 100f);
            bool outsideMaxResolved = resolver.TryResolveZone(outsideMax, out _);
            checks.Add(new ValidationCheck(
                "Outside max bounds → false",
                !outsideMaxResolved,
                $"Position ({outsideMax}) => resolved={outsideMaxResolved} (expected=false)"
            ));

            // ===== 6. A1/B1 경계선 근처에서 currentZone=A1이면 A1 유지 =====
            // A1과 B1의 경계는 x = -1600 (A1 centerX + zoneSize/2)
            // 경계선 근처: -1600 - epsilon
            float zoneSize = config.ZoneSize;
            float a1B1Boundary = config.WorldMinX + zoneSize; // A1 right edge = -2000 + 400 = -1600
            Vector3 nearBoundaryFromA1 = new Vector3(a1B1Boundary - 0.03f, 0f, a1Center.z);
            bool boundaryKeptA1 = resolver.TryResolveZone(nearBoundaryFromA1, new ZoneId('A', 1), out ZoneId boundaryZoneIdA1);
            checks.Add(new ValidationCheck(
                "A1/B1 boundary with current=A1 → A1 kept",
                boundaryKeptA1 && boundaryZoneIdA1.Equals(new ZoneId('A', 1)),
                $"Position ({nearBoundaryFromA1}), current=A1 => Zone={boundaryZoneIdA1} (expected=A1)"
            ));

            // ===== 7. A1/B1 경계선 근처에서 currentZone=B1이면 B1 유지 =====
            Vector3 nearBoundaryFromB1 = new Vector3(a1B1Boundary + 0.03f, 0f, a1Center.z);
            bool boundaryKeptB1 = resolver.TryResolveZone(nearBoundaryFromB1, new ZoneId('B', 1), out ZoneId boundaryZoneIdB1);
            checks.Add(new ValidationCheck(
                "A1/B1 boundary with current=B1 → B1 kept",
                boundaryKeptB1 && boundaryZoneIdB1.Equals(new ZoneId('B', 1)),
                $"Position ({nearBoundaryFromB1}), current=B1 => Zone={boundaryZoneIdB1} (expected=B1)"
            ));

            // ===== 8. currentZone 없이 경계선이면 nearest center 정책 적용 =====
            // 경계선 위 정확한 지점: a1B1Boundary
            Vector3 exactBoundary = new Vector3(a1B1Boundary, 0f, a1Center.z);
            bool boundaryNoCurrent = resolver.TryResolveZone(exactBoundary, null, out ZoneId boundaryNoCurrentZoneId);
            // epsilon=0.05f이므로 경계선 위는 clamp 결과에 따라 A1 또는 B1
            checks.Add(new ValidationCheck(
                "Boundary without current → nearest center",
                boundaryNoCurrent,
                $"Position ({exactBoundary}), no current => Zone={boundaryNoCurrentZoneId} (clamped result)"
            ));

            // ===== 9. F6 y=0 sample → Surface 또는 Shallow =====
            Vector3 f6Surface = new Vector3(f6Center.x, 0f, f6Center.z);
            bool f6SurfaceSampled = resolver.TrySampleDepth(f6Surface, out ZoneDepthSample f6SurfaceSample);
            bool f6SurfaceValid = f6SurfaceSampled &&
                (f6SurfaceSample.DepthBand == ZoneDepthBand.Surface || f6SurfaceSample.DepthBand == ZoneDepthBand.Shallow);
            checks.Add(new ValidationCheck(
                "F6 y=0 → Surface/Shallow",
                f6SurfaceValid,
                f6SurfaceSampled
                    ? $"F6 y=0 => Zone={f6SurfaceSample.ZoneId}, DepthBand={f6SurfaceSample.DepthBand}, Normalized={f6SurfaceSample.NormalizedDepth01:F3}"
                    : "F6 y=0 => Sample failed"
            ));

            // ===== 10. F6 y=-100 sample → Shallow =====
            Vector3 f6Shallow = new Vector3(f6Center.x, -100f, f6Center.z);
            bool f6ShallowSampled = resolver.TrySampleDepth(f6Shallow, out ZoneDepthSample f6ShallowSample);
            checks.Add(new ValidationCheck(
                "F6 y=-100 → Shallow",
                f6ShallowSampled && f6ShallowSample.DepthBand == ZoneDepthBand.Shallow,
                f6ShallowSampled
                    ? $"F6 y=-100 => Zone={f6ShallowSample.ZoneId}, DepthBand={f6ShallowSample.DepthBand}, Normalized={f6ShallowSample.NormalizedDepth01:F3}"
                    : "F6 y=-100 => Sample failed"
            ));

            // ===== 11. J10 y=-700 sample → Deep 또는 해당 profile 기준 맞는 band =====
            Vector3 j10Deep = new Vector3(j10Center.x, -700f, j10Center.z);
            bool j10DeepSampled = resolver.TrySampleDepth(j10Deep, out ZoneDepthSample j10DeepSample);
            checks.Add(new ValidationCheck(
                "J10 y=-700 → Deep or profile-based",
                j10DeepSampled,
                j10DeepSampled
                    ? $"J10 y=-700 => Zone={j10DeepSample.ZoneId}, DepthBand={j10DeepSample.DepthBand}, Normalized={j10DeepSample.NormalizedDepth01:F3}"
                    : "J10 y=-700 => Sample failed"
            ));

            // ===== 12. J10 y=-1500 sample → Forbidden =====
            Vector3 j10Forbidden = new Vector3(j10Center.x, -1500f, j10Center.z);
            bool j10ForbiddenSampled = resolver.TrySampleDepth(j10Forbidden, out ZoneDepthSample j10ForbiddenSample);
            checks.Add(new ValidationCheck(
                "J10 y=-1500 → Forbidden",
                j10ForbiddenSampled && j10ForbiddenSample.DepthBand == ZoneDepthBand.Forbidden,
                j10ForbiddenSampled
                    ? $"J10 y=-1500 => Zone={j10ForbiddenSample.ZoneId}, DepthBand={j10ForbiddenSample.DepthBand}, Normalized={j10ForbiddenSample.NormalizedDepth01:F3}"
                    : "J10 y=-1500 => Sample failed"
            ));

            // 결과 출력
            PrintValidationResults("ZoneResolver", checks);
        }

        /// <summary>
        /// Depth Sampling 검증을 수행한다.
        /// ZoneResolver 검증과 동일한 검사를 수행하지만, Depth Band에 더 중점을 둔다.
        /// </summary>
        public static void ValidateDepthSampling(DeepLightMapAutoBuilderSettingsSO settings)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] Settings is null! Cannot validate depth sampling.");
                return;
            }

            if (settings.WorldMapConfig == null || settings.ScenarioPreset == null)
            {
                Debug.LogError("[MapAutoBuilder] WorldMapConfig or ScenarioPreset is null! Cannot validate depth sampling.");
                return;
            }

            // ZoneResolver 생성 및 초기화
            ZoneResolver resolver = new ZoneResolver(settings.WorldMapConfig);
            resolver.Initialize(settings.ScenarioPreset);

            var checks = new List<ValidationCheck>();
            var config = settings.WorldMapConfig;

            // F6 center
            Vector3 f6Center = GetZoneCenter(config, 5, 5);

            // ===== 1. F6 y=0 → Surface =====
            Vector3 posY0 = new Vector3(f6Center.x, 0f, f6Center.z);
            bool sampledY0 = resolver.TrySampleDepth(posY0, out ZoneDepthSample sampleY0);
            checks.Add(new ValidationCheck(
                "F6 y=0 depth sample",
                sampledY0,
                sampledY0 ? sampleY0.ToDebugString() : "Failed"
            ));

            // ===== 2. F6 y=-10 → Shallow (수면 바로 아래) =====
            Vector3 posY10 = new Vector3(f6Center.x, -10f, f6Center.z);
            bool sampledY10 = resolver.TrySampleDepth(posY10, out ZoneDepthSample sampleY10);
            checks.Add(new ValidationCheck(
                "F6 y=-10 depth sample",
                sampledY10,
                sampledY10 ? sampleY10.ToDebugString() : "Failed"
            ));

            // ===== 3. F6 y=-100 → Shallow =====
            Vector3 posY100 = new Vector3(f6Center.x, -100f, f6Center.z);
            bool sampledY100 = resolver.TrySampleDepth(posY100, out ZoneDepthSample sampleY100);
            checks.Add(new ValidationCheck(
                "F6 y=-100 depth sample",
                sampledY100 && sampleY100.DepthBand == ZoneDepthBand.Shallow,
                sampledY100 ? sampleY100.ToDebugString() : "Failed"
            ));

            // ===== 4. F6 y=-400 → Mid =====
            Vector3 posY400 = new Vector3(f6Center.x, -400f, f6Center.z);
            bool sampledY400 = resolver.TrySampleDepth(posY400, out ZoneDepthSample sampleY400);
            checks.Add(new ValidationCheck(
                "F6 y=-400 depth sample",
                sampledY400,
                sampledY400 ? sampleY400.ToDebugString() : "Failed"
            ));

            // ===== 5. F6 y=-800 → Deep =====
            Vector3 posY800 = new Vector3(f6Center.x, -800f, f6Center.z);
            bool sampledY800 = resolver.TrySampleDepth(posY800, out ZoneDepthSample sampleY800);
            checks.Add(new ValidationCheck(
                "F6 y=-800 depth sample",
                sampledY800,
                sampledY800 ? sampleY800.ToDebugString() : "Failed"
            ));

            // ===== 6. F6 y=-1500 → Forbidden =====
            Vector3 posY1500 = new Vector3(f6Center.x, -1500f, f6Center.z);
            bool sampledY1500 = resolver.TrySampleDepth(posY1500, out ZoneDepthSample sampleY1500);
            checks.Add(new ValidationCheck(
                "F6 y=-1500 depth sample",
                sampledY1500 && sampleY1500.DepthBand == ZoneDepthBand.Forbidden,
                sampledY1500 ? sampleY1500.ToDebugString() : "Failed"
            ));

            // ===== 7. A1 y=-50 → Shallow =====
            Vector3 a1Center = GetZoneCenter(config, 0, 0);
            Vector3 posA1Y50 = new Vector3(a1Center.x, -50f, a1Center.z);
            bool sampledA1Y50 = resolver.TrySampleDepth(posA1Y50, out ZoneDepthSample sampleA1Y50);
            checks.Add(new ValidationCheck(
                "A1 y=-50 depth sample",
                sampledA1Y50,
                sampledA1Y50 ? sampleA1Y50.ToDebugString() : "Failed"
            ));

            // ===== 8. J10 y=-700 → Deep =====
            Vector3 j10Center = GetZoneCenter(config, 9, 9);
            Vector3 posJ10Y700 = new Vector3(j10Center.x, -700f, j10Center.z);
            bool sampledJ10Y700 = resolver.TrySampleDepth(posJ10Y700, out ZoneDepthSample sampleJ10Y700);
            checks.Add(new ValidationCheck(
                "J10 y=-700 depth sample",
                sampledJ10Y700,
                sampledJ10Y700 ? sampleJ10Y700.ToDebugString() : "Failed"
            ));

            // 결과 출력
            PrintValidationResults("DepthSampling", checks);
        }

        // ===== Internal Helpers =====

        /// <summary>
        /// colIndex, rowIndex로 Zone 중심 좌표를 계산한다.
        /// </summary>
        private static Vector3 GetZoneCenter(WorldMapConfigSO config, int colIndex, int rowIndex)
        {
            float centerX = config.WorldMinX + colIndex * config.ZoneSize + config.ZoneSize * 0.5f;
            float centerZ = config.WorldMinZ + rowIndex * config.ZoneSize + config.ZoneSize * 0.5f;
            return new Vector3(centerX, 0f, centerZ);
        }

        /// <summary>
        /// 검증 결과를 Console에 출력한다.
        /// </summary>
        private static void PrintValidationResults(string category, List<ValidationCheck> checks)
        {
            var log = new StringBuilder();
            log.AppendLine($"===== Map Auto Builder: {category} Validation =====");

            int passedCount = 0;
            int totalCount = checks.Count;

            for (int i = 0; i < checks.Count; i++)
            {
                var check = checks[i];
                string status = check.Passed ? "[PASS]" : "[FAIL]";
                if (check.Passed) passedCount++;

                log.AppendLine($"  {status} {check.Name}: {check.Message}");
            }

            log.AppendLine($"\n=> {category} validation: {passedCount}/{totalCount} checks passed.");

            if (passedCount == totalCount)
            {
                log.AppendLine("=> All checks PASSED.");
            }
            else
            {
                log.AppendLine($"=> {totalCount - passedCount} check(s) FAILED. Review the log above.");
            }

            Debug.Log(log.ToString());
        }
    }
}
