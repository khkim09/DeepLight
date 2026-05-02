using System.Text;
using UnityEditor;
using UnityEngine;
using Project.Data.World;
using Project.Gameplay.World;

namespace Project.Editor.AutoTool
{
    /// <summary>
    /// Phase 14.10-B-1: A~J 전체 100개 ZoneRoot에 Content root 구조만 안정적으로 생성하는 독립 유틸리티.
    /// 실제 marker 생성/metadata 생성은 하지 않고, Content 하위 root 골격만 만든다.
    /// 기존 DeepLightMapZoneContentPlaceholderUtility와 독립적으로 동작하며 수정하지 않는다.
    /// </summary>
    public static class DeepLightMapFullContentRootGenerationUtility
    {
        // ===== Constants =====
        private const string ContentRootName = "Content";
        private const string ResourceSpawnsName = "ResourceSpawns";
        private const string HazardSpawnsName = "HazardSpawns";
        private const string LandmarkSpawnsName = "LandmarkSpawns";
        private const string NarrativeSpawnsName = "NarrativeSpawns";
        private const string RouteMarkersName = "RouteMarkers";
        private const string DebugMarkersName = "DebugMarkers";

        /// <summary>전체 Zone 열 개수 (A~J = 10열)</summary>
        private const int TotalColumns = 10;

        /// <summary>전체 Zone 행 개수 (1~10 = 10행)</summary>
        private const int TotalRows = 10;

        /// <summary>전체 Zone 개수 (10x10 = 100)</summary>
        private const int TotalZoneCount = 100;

        /// <summary>Content 하위 root 개수 (6개: ResourceSpawns, HazardSpawns, LandmarkSpawns, NarrativeSpawns, RouteMarkers, DebugMarkers)</summary>
        private const int ContentChildRootCount = 6;

        /// <summary>Content root 이름 목록 (Validate에서 사용)</summary>
        private static readonly string[] ContentChildRootNames = new[]
        {
            ResourceSpawnsName,
            HazardSpawnsName,
            LandmarkSpawnsName,
            NarrativeSpawnsName,
            RouteMarkersName,
            DebugMarkersName
        };

        // ===== Public API =====

        /// <summary>
        /// A~J 전체 100개 ZoneRoot에 Content root 구조를 재구축한다.
        /// 각 ZoneRoot_XX 하위에 Content root를 생성/보장하고,
        /// Content 하위에 ResourceSpawns/HazardSpawns/LandmarkSpawns/NarrativeSpawns/RouteMarkers/DebugMarkers 6개 root를 생성/보장한다.
        /// 기존 prototype marker 자식들은 삭제하지 않고 보존한다.
        /// </summary>
        public static void RebuildFullContentRoots(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            // settings null 체크
            if (settings == null)
            {
                Debug.LogError("[FullContentRoot] Settings is null! Cannot rebuild full content roots.");
                return;
            }

            // context null 체크
            if (context == null)
            {
                Debug.LogError("[FullContentRoot] Context is null! Cannot rebuild full content roots.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.10-B-1: Rebuild Full Content Roots (A1~J10) =====");

            // 1. GeneratedWorldRoot 찾기
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                Debug.LogError("[FullContentRoot] GeneratedWorldRoot not found. Run Generate Full Scenario Map first.");
                return;
            }
            log.AppendLine($"  [OK] GeneratedWorldRoot found: {generatedRoot.name}");

            // 2. ZoneRoots 부모 찾기
            Transform zoneRootsTransform = generatedRoot.transform.Find(settings.ZoneRootParentName);
            if (zoneRootsTransform == null)
            {
                Debug.LogError($"[FullContentRoot] '{settings.ZoneRootParentName}' not found under GeneratedWorldRoot.");
                return;
            }
            log.AppendLine($"  [OK] ZoneRoots parent found: {settings.ZoneRootParentName}");

            // 3. A~J 전체 100개 ZoneRoot 순회하며 Content root 구조 생성
            int totalZonesProcessed = 0;
            int totalContentRootsCreated = 0;
            int totalContentRootsReused = 0;
            int totalChildRootsCreated = 0;
            int totalChildRootsReused = 0;

            ForEachZoneId((string zoneIdStr, int colIndex, int rowIndex) =>
            {
                string zoneRootName = $"ZoneRoot_{zoneIdStr}";

                // ZoneRoot 찾기
                Transform zoneRootTransform = zoneRootsTransform.Find(zoneRootName);
                if (zoneRootTransform == null)
                {
                    LogIfVerbose(settings, $"[SKIP] {zoneRootName} not found.");
                    return;
                }

                // Content root 찾기/생성 (기존에 있으면 재사용, 없으면 생성)
                Transform contentTransform = zoneRootTransform.Find(ContentRootName);
                if (contentTransform == null)
                {
                    // 새 Content root 생성
                    GameObject contentRoot = new GameObject(ContentRootName);
                    contentRoot.transform.SetParent(zoneRootTransform, false);
                    contentRoot.transform.localPosition = Vector3.zero;
                    contentRoot.transform.localRotation = Quaternion.identity;
                    contentRoot.transform.localScale = Vector3.one;
                    Undo.RegisterCreatedObjectUndo(contentRoot, $"Create {ContentRootName} under {zoneRootName}");
                    contentTransform = contentRoot.transform;
                    totalContentRootsCreated++;
                    LogIfVerbose(settings, $"[CREATE] {zoneRootName}/{ContentRootName}");
                }
                else
                {
                    totalContentRootsReused++;
                    LogIfVerbose(settings, $"[REUSE] {zoneRootName}/{ContentRootName}");
                }

                // Content root의 transform 강제 설정 (기존 값이 있어도 재설정)
                contentTransform.localPosition = Vector3.zero;
                contentTransform.localRotation = Quaternion.identity;
                contentTransform.localScale = Vector3.one;

                // Content 하위 6개 child root 생성/보장
                foreach (string childName in ContentChildRootNames)
                {
                    Transform childTransform = contentTransform.Find(childName);
                    if (childTransform == null)
                    {
                        // 새 child root 생성
                        GameObject childGo = new GameObject(childName);
                        childGo.transform.SetParent(contentTransform, false);
                        childGo.transform.localPosition = Vector3.zero;
                        childGo.transform.localRotation = Quaternion.identity;
                        childGo.transform.localScale = Vector3.one;
                        Undo.RegisterCreatedObjectUndo(childGo, $"Create {childName} under {ContentRootName}");
                        totalChildRootsCreated++;
                        LogIfVerbose(settings, $"[CREATE] {zoneRootName}/{ContentRootName}/{childName}");
                    }
                    else
                    {
                        totalChildRootsReused++;
                        // 기존 child root의 transform 강제 설정
                        childTransform.localPosition = Vector3.zero;
                        childTransform.localRotation = Quaternion.identity;
                        childTransform.localScale = Vector3.one;
                    }
                }

                totalZonesProcessed++;
            });

            // 4. 결과 로그 출력
            log.AppendLine($"  Total zones processed: {totalZonesProcessed}");
            log.AppendLine($"  Content roots created: {totalContentRootsCreated}");
            log.AppendLine($"  Content roots reused: {totalContentRootsReused}");
            log.AppendLine($"  Child roots created: {totalChildRootsCreated}");
            log.AppendLine($"  Child roots reused: {totalChildRootsReused}");
            log.AppendLine("===== Phase 14.10-B-1: Rebuild Full Content Roots Complete =====");
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// A~J 전체 100개 ZoneRoot의 Content root 구조 유효성을 검사한다.
        /// GeneratedWorldRoot/ZoneRoots/ZoneRoot/Content/child root 존재,
        /// transform localPosition/Rotation/Scale, prototype marker 보존 여부를 검사한다.
        /// </summary>
        public static void ValidateFullContentRoots(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[FullContentRoot] Settings is null! Cannot validate.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.10-B-1: Validate Full Content Roots (A1~J10) =====");

            int passCount = 0;
            int failCount = 0;
            int warnCount = 0;

            // 1. GeneratedWorldRoot 존재 PASS/FAIL
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            if (generatedRoot != null)
            {
                log.AppendLine("  [PASS] GeneratedWorldRoot exists.");
                passCount++;
            }
            else
            {
                log.AppendLine("  [FAIL] GeneratedWorldRoot not found!");
                failCount++;
            }

            // 2. ZoneRoots parent 존재 PASS/FAIL
            Transform zoneRootsTransform = null;
            if (generatedRoot != null)
            {
                zoneRootsTransform = generatedRoot.transform.Find(settings.ZoneRootParentName);
                if (zoneRootsTransform != null)
                {
                    log.AppendLine("  [PASS] ZoneRoots parent exists.");
                    passCount++;
                }
                else
                {
                    log.AppendLine("  [FAIL] ZoneRoots parent not found!");
                    failCount++;
                }
            }
            else
            {
                log.AppendLine("  [FAIL] Cannot check ZoneRoots parent (GeneratedWorldRoot missing).");
                failCount++;
            }

            // 3. A~J 100개 ZoneRoot 존재 PASS/FAIL
            int zoneRootFound = 0;
            if (zoneRootsTransform != null)
            {
                ForEachZoneId((string zoneIdStr, int colIndex, int rowIndex) =>
                {
                    string zrName = $"ZoneRoot_{zoneIdStr}";
                    if (zoneRootsTransform.Find(zrName) != null)
                    {
                        zoneRootFound++;
                    }
                });
            }

            if (zoneRootFound == TotalZoneCount)
            {
                log.AppendLine($"  [PASS] All {TotalZoneCount} ZoneRoot_A1~J10 exist.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] ZoneRoot_A1~J10 found: {zoneRootFound}/{TotalZoneCount}");
                failCount++;
            }

            // ZoneRoots가 없으면 더 이상 진행 불가
            if (zoneRootsTransform == null)
            {
                log.AppendLine("  [FAIL] Cannot continue validation (ZoneRoots parent missing).");
                failCount++;
                log.AppendLine($"\n  === Summary: PASS={passCount} FAIL={failCount} WARN={warnCount} ===");
                log.AppendLine("===== Phase 14.10-B-1: Validate Full Content Roots Complete =====");
                Debug.LogWarning(log.ToString());
                return;
            }

            // 4. 100개 ZoneRoot 모두 Content root 존재 PASS/FAIL
            int contentRootFound = 0;
            // 5. 100개 ZoneRoot 모두 6개 child root 존재 PASS/FAIL
            int allSixChildRootsFound = 0;
            // Transform 검사 카운터
            int localPositionZeroCount = 0;
            int localRotationIdentityCount = 0;
            int localScaleOneCount = 0;
            int localPositionNonZeroCount = 0;
            int localRotationNonIdentityCount = 0;
            int localScaleNonOneCount = 0;

            // Prototype marker 보존 검증
            int e5ResourceSpawnsChildCount = 0;
            bool b5WreckContentFound = false;
            bool c5WreckContentFound = false;
            bool b6WreckContentFound = false;
            bool c6WreckContentFound = false;
            bool c7WreckContentFound = false;

            ForEachZoneId((string zoneIdStr, int colIndex, int rowIndex) =>
            {
                string zrName = $"ZoneRoot_{zoneIdStr}";
                Transform zr = zoneRootsTransform.Find(zrName);
                if (zr == null) return;

                // Content root 존재 검사
                Transform contentT = zr.Find(ContentRootName);
                if (contentT != null)
                {
                    contentRootFound++;

                    // Content root transform 검사
                    if (contentT.localPosition == Vector3.zero) localPositionZeroCount++;
                    else localPositionNonZeroCount++;
                    if (contentT.localRotation == Quaternion.identity) localRotationIdentityCount++;
                    else localRotationNonIdentityCount++;
                    if (contentT.localScale == Vector3.one) localScaleOneCount++;
                    else localScaleNonOneCount++;

                    // 6개 child root 존재 검사
                    bool allSixExist = true;
                    foreach (string childName in ContentChildRootNames)
                    {
                        Transform childT = contentT.Find(childName);
                        if (childT != null)
                        {
                            // child root transform 검사
                            if (childT.localPosition == Vector3.zero) localPositionZeroCount++;
                            else localPositionNonZeroCount++;
                            if (childT.localRotation == Quaternion.identity) localRotationIdentityCount++;
                            else localRotationNonIdentityCount++;
                            if (childT.localScale == Vector3.one) localScaleOneCount++;
                            else localScaleNonOneCount++;
                        }
                        else
                        {
                            allSixExist = false;
                        }
                    }

                    if (allSixExist)
                    {
                        allSixChildRootsFound++;
                    }

                    // E5 ResourceSpawns 아래 자식 수 확인
                    if (zoneIdStr == "E5")
                    {
                        Transform resourceSpawns = contentT.Find(ResourceSpawnsName);
                        if (resourceSpawns != null)
                        {
                            e5ResourceSpawnsChildCount = resourceSpawns.childCount;
                        }
                    }

                    // B5/C5/B6/C6/C7 Wreck prototype Content root 확인
                    if (zoneIdStr == "B5" && contentT != null) b5WreckContentFound = true;
                    if (zoneIdStr == "C5" && contentT != null) c5WreckContentFound = true;
                    if (zoneIdStr == "B6" && contentT != null) b6WreckContentFound = true;
                    if (zoneIdStr == "C6" && contentT != null) c6WreckContentFound = true;
                    if (zoneIdStr == "C7" && contentT != null) c7WreckContentFound = true;
                }
            });

            // 4. Content root 존재 검증
            if (contentRootFound == TotalZoneCount)
            {
                log.AppendLine($"  [PASS] All {TotalZoneCount} Content roots exist under ZoneRoots.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] Content roots found: {contentRootFound}/{TotalZoneCount}");
                failCount++;
            }

            // 5. 6개 child root 존재 검증
            if (allSixChildRootsFound == TotalZoneCount)
            {
                log.AppendLine($"  [PASS] All {TotalZoneCount} ZoneRoots have all {ContentChildRootCount} child roots under Content.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] ZoneRoots with all {ContentChildRootCount} child roots: {allSixChildRootsFound}/{TotalZoneCount}");
                failCount++;
            }

            // 6. 모든 Content/root localPosition zero 검사
            if (localPositionNonZeroCount == 0)
            {
                log.AppendLine("  [PASS] All Content/root localPosition = Vector3.zero.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {localPositionNonZeroCount} Content/root(s) have non-zero localPosition.");
                failCount++;
            }

            // 7. 모든 Content/root localRotation identity 검사
            if (localRotationNonIdentityCount == 0)
            {
                log.AppendLine("  [PASS] All Content/root localRotation = Quaternion.identity.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {localRotationNonIdentityCount} Content/root(s) have non-identity localRotation.");
                failCount++;
            }

            // 8. 모든 Content/root localScale one 검사
            if (localScaleNonOneCount == 0)
            {
                log.AppendLine("  [PASS] All Content/root localScale = Vector3.one.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {localScaleNonOneCount} Content/root(s) have non-one localScale.");
                failCount++;
            }

            // 9. Prototype marker 보존 검증
            // E5 ResourceSpawns 아래 자식이 1개 이상이면 PASS 또는 INFO
            if (e5ResourceSpawnsChildCount >= 1)
            {
                log.AppendLine($"  [PASS] E5 ResourceSpawns has {e5ResourceSpawnsChildCount} child(ren) preserved.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [INFO] E5 ResourceSpawns has {e5ResourceSpawnsChildCount} children (may be expected if no prototype markers were placed).");
                warnCount++;
            }

            // B5/C5/B6/C6/C7 중 Wreck prototype Content root가 남아있으면 PASS 또는 INFO
            int wreckZonesPreserved = 0;
            if (b5WreckContentFound) wreckZonesPreserved++;
            if (c5WreckContentFound) wreckZonesPreserved++;
            if (b6WreckContentFound) wreckZonesPreserved++;
            if (c6WreckContentFound) wreckZonesPreserved++;
            if (c7WreckContentFound) wreckZonesPreserved++;

            if (wreckZonesPreserved >= 3)
            {
                log.AppendLine($"  [PASS] Wreck prototype Content roots preserved in {wreckZonesPreserved}/5 zones (B5/C5/B6/C6/C7).");
                passCount++;
            }
            else if (wreckZonesPreserved > 0)
            {
                log.AppendLine($"  [INFO] Wreck prototype Content roots preserved in {wreckZonesPreserved}/5 zones (B5/C5/B6/C6/C7).");
                warnCount++;
            }
            else
            {
                log.AppendLine("  [INFO] No Wreck prototype Content roots found in B5/C5/B6/C6/C7 (may be expected if not yet generated).");
                warnCount++;
            }

            // 최종 요약
            log.AppendLine($"\n  === Summary: PASS={passCount} FAIL={failCount} WARN={warnCount} ===");
            log.AppendLine("===== Phase 14.10-B-1: Validate Full Content Roots Complete =====");
            Debug.Log(log.ToString());
        }

        // ===== Private Helpers =====

        /// <summary>
        /// A~J, 1~10 전체 100개 zoneId를 안정적으로 순회하는 private iterator.
        /// A1~A10, B1~B10 ... J1~J10 순서를 유지하며 각 zoneId에 대해 callback을 호출한다.
        /// </summary>
        private static void ForEachZoneId(System.Action<string, int, int> callback)
        {
            // A=0, B=1, ..., J=9 순서로 column 순회
            for (int colIndex = 0; colIndex < TotalColumns; colIndex++)
            {
                char columnChar = (char)('A' + colIndex);

                // 1~10 순서로 row 순회
                for (int rowIndex = 0; rowIndex < TotalRows; rowIndex++)
                {
                    int rowNumber = rowIndex + 1;
                    string zoneIdStr = $"{columnChar}{rowNumber}";
                    callback(zoneIdStr, colIndex, rowIndex);
                }
            }
        }

        /// <summary>
        /// settings.LogVerbose가 true일 때만 Debug.Log를 출력한다.
        /// </summary>
        private static void LogIfVerbose(DeepLightMapAutoBuilderSettingsSO settings, string message)
        {
            if (settings != null && settings.LogVerbose)
            {
                Debug.Log($"[FullContentRoot] {message}");
            }
        }
    }
}
