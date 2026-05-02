using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Project.Data.World;
using Project.Gameplay.World;
using Project.Gameplay.World.Content;

namespace Project.Editor.AutoTool
{
    /// <summary>
    /// Phase 14.10-D-2: Content Marker Debug Visual Placeholder Generation.
    /// A~J 100개 zone에 생성된 WorldMapZoneContentMarker 오브젝트에 editor/debug 확인용 visual child를 생성한다.
    /// 실제 게임 prefab binding이 아니라, 현재 자동 생성된 marker metadata를 Scene에서 쉽게 확인하기 위한
    /// lightweight placeholder visual만 만든다.
    /// </summary>
    public static class DeepLightMapContentMarkerDebugVisualUtility
    {
        // ===== Constants =====
        private const string DebugVisualChildName = "DebugVisual";
        private const string GeneratedMaterialsFolder = "Assets/_Project/Generated/EditorDebugMaterials";

        // ===== Marker Type → Primitive Type Mapping =====
        private static readonly Dictionary<WorldMapZoneContentMarkerType, PrimitiveType> MarkerPrimitiveMap =
            new Dictionary<WorldMapZoneContentMarkerType, PrimitiveType>
            {
                { WorldMapZoneContentMarkerType.Resource, PrimitiveType.Sphere },
                { WorldMapZoneContentMarkerType.Hazard, PrimitiveType.Cube },
                { WorldMapZoneContentMarkerType.Landmark, PrimitiveType.Cylinder },
                { WorldMapZoneContentMarkerType.Narrative, PrimitiveType.Capsule },
                { WorldMapZoneContentMarkerType.Route, PrimitiveType.Quad },
                { WorldMapZoneContentMarkerType.Debug, PrimitiveType.Cube },
                { WorldMapZoneContentMarkerType.Unknown, PrimitiveType.Cube },
            };

        // ===== Marker Type → Local Scale Mapping =====
        private static readonly Dictionary<WorldMapZoneContentMarkerType, Vector3> MarkerScaleMap =
            new Dictionary<WorldMapZoneContentMarkerType, Vector3>
            {
                { WorldMapZoneContentMarkerType.Resource, new Vector3(6f, 6f, 6f) },
                { WorldMapZoneContentMarkerType.Hazard, new Vector3(7f, 7f, 7f) },
                { WorldMapZoneContentMarkerType.Landmark, new Vector3(8f, 8f, 8f) },
                { WorldMapZoneContentMarkerType.Narrative, new Vector3(6f, 10f, 6f) },
                { WorldMapZoneContentMarkerType.Route, new Vector3(10f, 2f, 10f) },
                { WorldMapZoneContentMarkerType.Debug, new Vector3(5f, 5f, 5f) },
                { WorldMapZoneContentMarkerType.Unknown, new Vector3(5f, 5f, 5f) },
            };

        // ===== Marker Type → Material Color Mapping =====
        private static readonly Dictionary<WorldMapZoneContentMarkerType, Color> MarkerColorMap =
            new Dictionary<WorldMapZoneContentMarkerType, Color>
            {
                { WorldMapZoneContentMarkerType.Resource, new Color(0.2f, 0.8f, 0.2f, 0.7f) },   // Green
                { WorldMapZoneContentMarkerType.Hazard, new Color(0.9f, 0.2f, 0.2f, 0.7f) },     // Red
                { WorldMapZoneContentMarkerType.Landmark, new Color(1.0f, 0.6f, 0.1f, 0.7f) },   // Yellow/Orange
                { WorldMapZoneContentMarkerType.Narrative, new Color(0.2f, 0.7f, 1.0f, 0.7f) },  // Cyan/Blue
                { WorldMapZoneContentMarkerType.Route, new Color(0.8f, 0.8f, 0.8f, 0.7f) },      // White/Gray
                { WorldMapZoneContentMarkerType.Debug, new Color(0.5f, 0.5f, 0.5f, 0.7f) },      // Gray
                { WorldMapZoneContentMarkerType.Unknown, new Color(0.5f, 0.5f, 0.5f, 0.7f) },    // Gray
            };

        // ===== Material Name Map =====
        private static readonly Dictionary<WorldMapZoneContentMarkerType, string> MaterialNameMap =
            new Dictionary<WorldMapZoneContentMarkerType, string>
            {
                { WorldMapZoneContentMarkerType.Resource, "Resource_Debug_Mat" },
                { WorldMapZoneContentMarkerType.Hazard, "Hazard_Debug_Mat" },
                { WorldMapZoneContentMarkerType.Landmark, "Landmark_Debug_Mat" },
                { WorldMapZoneContentMarkerType.Narrative, "Narrative_Debug_Mat" },
                { WorldMapZoneContentMarkerType.Route, "Route_Debug_Mat" },
                { WorldMapZoneContentMarkerType.Debug, "Debug_Debug_Mat" },
                { WorldMapZoneContentMarkerType.Unknown, "Unknown_Debug_Mat" },
            };

        // ===== Cached Materials =====
        private static Dictionary<WorldMapZoneContentMarkerType, Material> _cachedMaterials = null;

        // ===== Public API =====

        /// <summary>
        /// GeneratedWorldRoot 아래의 모든 WorldMapZoneContentMarker 컴포넌트를 찾아
        /// 각 marker GameObject 하위에 DebugVisual child를 생성/갱신한다.
        /// 기존 DebugVisual이 있으면 재사용하거나 삭제 후 재생성한다.
        /// </summary>
        public static void RebuildMarkerDebugVisuals(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MarkerDebugVisual] Settings is null! Cannot rebuild debug visuals.");
                return;
            }
            if (context == null)
            {
                Debug.LogError("[MarkerDebugVisual] Context is null! Cannot rebuild debug visuals.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.10-D-2: Rebuild Marker Debug Visuals =====");

            // 1. GeneratedWorldRoot 찾기
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                Debug.LogError("[MarkerDebugVisual] GeneratedWorldRoot not found. Run Generate Full Scenario Map first.");
                return;
            }
            log.AppendLine($"  [OK] GeneratedWorldRoot found: {generatedRoot.name}");

            // 2. 모든 WorldMapZoneContentMarker 컴포넌트 찾기
            WorldMapZoneContentMarker[] allMarkers = generatedRoot.GetComponentsInChildren<WorldMapZoneContentMarker>(true);
            if (allMarkers == null || allMarkers.Length == 0)
            {
                Debug.LogWarning("[MarkerDebugVisual] No WorldMapZoneContentMarker components found. Nothing to visualize.");
                log.AppendLine("  [WARN] No WorldMapZoneContentMarker components found.");
                Debug.Log(log.ToString());
                return;
            }
            log.AppendLine($"  [OK] Found {allMarkers.Length} WorldMapZoneContentMarker components.");

            // 3. Editor material cache 초기화 (URP 호환 shader 사용)
            EnsureMaterialCache();

            // 4. 각 marker에 DebugVisual child 생성/갱신
            int totalCreated = 0;
            int totalReused = 0;
            int totalSkipped = 0;

            foreach (WorldMapZoneContentMarker marker in allMarkers)
            {
                if (marker == null) continue;

                GameObject markerGo = marker.gameObject;
                WorldMapZoneContentMarkerType markerType = marker.MarkerType;

                // 4a. 기존 DebugVisual child 찾기
                Transform existingDebugVisual = markerGo.transform.Find(DebugVisualChildName);

                if (existingDebugVisual != null)
                {
                    // 기존 DebugVisual이 있으면 재사용 (내용물 갱신)
                    RebuildDebugVisualContent(existingDebugVisual.gameObject, markerType);
                    totalReused++;
                }
                else
                {
                    // 기존 DebugVisual이 없으면 새로 생성
                    GameObject debugVisual = CreateDebugVisual(markerGo, markerType);
                    if (debugVisual != null)
                    {
                        totalCreated++;
                    }
                    else
                    {
                        totalSkipped++;
                    }
                }
            }

            log.AppendLine($"  DebugVisual created: {totalCreated}, reused: {totalReused}, skipped: {totalSkipped}");
            log.AppendLine("===== Phase 14.10-D-2: Rebuild Marker Debug Visuals Complete =====");
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// GeneratedWorldRoot 아래의 모든 WorldMapZoneContentMarker에 대해
        /// DebugVisual child의 유효성을 검사한다.
        /// </summary>
        public static void ValidateMarkerDebugVisuals(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MarkerDebugVisual] Settings is null! Cannot validate debug visuals.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine("===== Phase 14.10-D-2: Validate Marker Debug Visuals =====");

            int passCount = 0;
            int failCount = 0;
            int warnCount = 0;

            // 1. GeneratedWorldRoot 존재 확인
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
                log.AppendLine($"\n  === Summary: PASS={passCount} FAIL={failCount} WARN={warnCount} ===");
                log.AppendLine("===== Phase 14.10-D-2: Validate Marker Debug Visuals Complete =====");
                Debug.LogWarning(log.ToString());
                return;
            }

            // 2. WorldMapZoneContentMarker component count > 0 확인
            WorldMapZoneContentMarker[] allMarkers = generatedRoot.GetComponentsInChildren<WorldMapZoneContentMarker>(true);
            if (allMarkers != null && allMarkers.Length > 0)
            {
                log.AppendLine($"  [PASS] WorldMapZoneContentMarker count: {allMarkers.Length} (> 0).");
                passCount++;
            }
            else
            {
                log.AppendLine("  [FAIL] No WorldMapZoneContentMarker components found!");
                failCount++;
            }

            if (allMarkers == null || allMarkers.Length == 0)
            {
                log.AppendLine($"\n  === Summary: PASS={passCount} FAIL={failCount} WARN={warnCount} ===");
                log.AppendLine("===== Phase 14.10-D-2: Validate Marker Debug Visuals Complete =====");
                Debug.LogWarning(log.ToString());
                return;
            }

            // 3-9. 각 marker 상세 검사
            int markersWithDebugVisual = 0;
            int markersMissingDebugVisual = 0;
            int markersWithMultipleDebugVisuals = 0;
            int debugVisualsMissingMeshFilter = 0;
            int debugVisualsMissingMeshRenderer = 0;
            int debugVisualsWithCollider = 0;
            int debugVisualsNonZeroPosition = 0;
            int debugVisualsNonIdentityRotation = 0;
            int debugVisualsZeroScale = 0;
            int debugVisualsNullMaterial = 0;
            int debugVisualsNullShader = 0;
            int debugVisualsStandardShader = 0;
            int debugVisualsNonURPShader = 0;
            int debugVisualsWrongMaterialName = 0;
            int markersMetadataPreserved = 0;

            // type별 material color 추적 (모두 동일한지 검사용)
            Dictionary<WorldMapZoneContentMarkerType, Color> typeColors = new Dictionary<WorldMapZoneContentMarkerType, Color>();

            foreach (WorldMapZoneContentMarker marker in allMarkers)
            {
                if (marker == null) continue;

                GameObject markerGo = marker.gameObject;
                WorldMapZoneContentMarkerType markerType = marker.MarkerType;

                // 3. DebugVisual child가 정확히 1개 있는지 확인
                int debugVisualCount = CountChildrenNamed(markerGo.transform, DebugVisualChildName);
                if (debugVisualCount == 1)
                {
                    markersWithDebugVisual++;
                }
                else if (debugVisualCount == 0)
                {
                    markersMissingDebugVisual++;
                }
                else
                {
                    markersWithMultipleDebugVisuals++;
                }

                // DebugVisual이 정확히 1개일 때만 상세 검사
                if (debugVisualCount == 1)
                {
                    Transform debugVisualTransform = markerGo.transform.Find(DebugVisualChildName);
                    GameObject debugVisual = debugVisualTransform.gameObject;

                    // 4. MeshFilter/MeshRenderer 존재 확인
                    MeshFilter mf = debugVisual.GetComponent<MeshFilter>();
                    MeshRenderer mr = debugVisual.GetComponent<MeshRenderer>();
                    if (mf == null) debugVisualsMissingMeshFilter++;
                    if (mr == null) debugVisualsMissingMeshRenderer++;

                    // 5. Collider가 없는지 확인
                    Collider col = debugVisual.GetComponent<Collider>();
                    if (col != null) debugVisualsWithCollider++;

                    // 6. localPosition == Vector3.zero 확인
                    if (debugVisualTransform.localPosition != Vector3.zero)
                        debugVisualsNonZeroPosition++;

                    // 7. localRotation == Quaternion.identity 확인
                    if (debugVisualTransform.localRotation != Quaternion.identity)
                        debugVisualsNonIdentityRotation++;

                    // 8. localScale이 Vector3.zero가 아닌지 확인
                    if (debugVisualTransform.localScale == Vector3.zero)
                        debugVisualsZeroScale++;

                    // 9. material null 아님 확인 + shader 검사
                    if (mr != null)
                    {
                        Material sharedMat = mr.sharedMaterial;

                        // 9a. material null 검사
                        if (sharedMat == null)
                        {
                            debugVisualsNullMaterial++;
                        }
                        else
                        {
                            // 9b. shader null 검사
                            Shader shader = sharedMat.shader;
                            if (shader == null)
                            {
                                debugVisualsNullShader++;
                            }
                            else
                            {
                                string shaderName = shader.name;

                                // 9c. Standard shader 검사
                                if (shaderName.Contains("Standard"))
                                {
                                    debugVisualsStandardShader++;
                                }

                                // 9d. URP 호환 shader 검사
                                bool isURPCompatible = shaderName.Contains("Universal Render Pipeline")
                                    || shaderName.Contains("Unlit")
                                    || shaderName.Contains("Sprites/Default");
                                if (!isURPCompatible)
                                {
                                    debugVisualsNonURPShader++;
                                }
                            }

                            // 9e. material name이 기대값과 일치하는지 확인
                            string expectedMatName = GetMaterialNameForType(markerType);
                            if (!string.IsNullOrEmpty(expectedMatName) && sharedMat.name != expectedMatName)
                            {
                                debugVisualsWrongMaterialName++;
                            }

                            // 9f. type별 color 기록 (모두 동일한지 검사용)
                            if (!typeColors.ContainsKey(markerType))
                            {
                                // shader property에 따라 color 읽기
                                Color matColor = Color.white;
                                if (sharedMat.HasProperty("_BaseColor"))
                                    matColor = sharedMat.GetColor("_BaseColor");
                                else if (sharedMat.HasProperty("_Color"))
                                    matColor = sharedMat.GetColor("_Color");
                                else
                                    matColor = sharedMat.color;
                                typeColors[markerType] = matColor;
                            }
                        }
                    }
                }

                // 10. marker root의 WorldMapZoneContentMarker metadata가 보존되는지 확인
                if (marker != null && !string.IsNullOrEmpty(marker.MarkerId) && !string.IsNullOrEmpty(marker.ZoneId))
                {
                    markersMetadataPreserved++;
                }
            }

            // 3. DebugVisual child count 검사
            if (markersMissingDebugVisual == 0 && markersWithMultipleDebugVisuals == 0)
            {
                log.AppendLine($"  [PASS] All {allMarkers.Length} markers have exactly 1 DebugVisual child.");
                passCount++;
            }
            else
            {
                if (markersMissingDebugVisual > 0)
                    log.AppendLine($"  [FAIL] {markersMissingDebugVisual} marker(s) missing DebugVisual child.");
                if (markersWithMultipleDebugVisuals > 0)
                    log.AppendLine($"  [FAIL] {markersWithMultipleDebugVisuals} marker(s) have multiple DebugVisual children.");
                failCount++;
            }

            // 4. MeshFilter
            if (debugVisualsMissingMeshFilter == 0)
            {
                log.AppendLine("  [PASS] All DebugVisuals have MeshFilter.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {debugVisualsMissingMeshFilter} DebugVisual(s) missing MeshFilter.");
                failCount++;
            }

            // 4. MeshRenderer
            if (debugVisualsMissingMeshRenderer == 0)
            {
                log.AppendLine("  [PASS] All DebugVisuals have MeshRenderer.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {debugVisualsMissingMeshRenderer} DebugVisual(s) missing MeshRenderer.");
                failCount++;
            }

            // 5. Collider
            if (debugVisualsWithCollider == 0)
            {
                log.AppendLine("  [PASS] No DebugVisual has Collider.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {debugVisualsWithCollider} DebugVisual(s) have Collider (should be removed).");
                failCount++;
            }

            // 6. localPosition
            if (debugVisualsNonZeroPosition == 0)
            {
                log.AppendLine("  [PASS] All DebugVisual localPosition == Vector3.zero.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {debugVisualsNonZeroPosition} DebugVisual(s) have non-zero localPosition.");
                failCount++;
            }

            // 7. localRotation
            if (debugVisualsNonIdentityRotation == 0)
            {
                log.AppendLine("  [PASS] All DebugVisual localRotation == Quaternion.identity.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {debugVisualsNonIdentityRotation} DebugVisual(s) have non-identity localRotation.");
                failCount++;
            }

            // 8. localScale
            if (debugVisualsZeroScale == 0)
            {
                log.AppendLine("  [PASS] All DebugVisual localScale is non-zero.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {debugVisualsZeroScale} DebugVisual(s) have zero localScale.");
                failCount++;
            }

            // 9a. Material null
            if (debugVisualsNullMaterial == 0)
            {
                log.AppendLine("  [PASS] All DebugVisual materials are non-null.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {debugVisualsNullMaterial} DebugVisual(s) have null material.");
                failCount++;
            }

            // 9b. Shader null
            if (debugVisualsNullShader == 0)
            {
                log.AppendLine("  [PASS] All DebugVisual material shaders are non-null.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {debugVisualsNullShader} DebugVisual(s) have null shader.");
                failCount++;
            }

            // 9c. Standard shader 검사
            if (debugVisualsStandardShader == 0)
            {
                log.AppendLine("  [PASS] No DebugVisual material uses Standard shader.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [FAIL] {debugVisualsStandardShader} DebugVisual(s) still use Standard shader (should be URP).");
                failCount++;
            }

            // 9d. URP 호환 shader 검사
            if (debugVisualsNonURPShader == 0)
            {
                log.AppendLine("  [PASS] All DebugVisual materials use URP-compatible shaders.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [WARN] {debugVisualsNonURPShader} DebugVisual(s) use non-URP shaders (may appear pink).");
                warnCount++;
            }

            // 9e. Material name 일치 검사
            if (debugVisualsWrongMaterialName == 0)
            {
                log.AppendLine("  [PASS] All DebugVisual material names match expected type names.");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [WARN] {debugVisualsWrongMaterialName} DebugVisual(s) have mismatched material names.");
                warnCount++;
            }

            // 9f. Type별 color가 모두 동일하지 않은지 확인 (최소 2개 type 이상 존재할 때)
            if (typeColors.Count >= 2)
            {
                bool allColorsSame = true;
                Color firstColor = Color.white;
                bool firstSet = false;
                foreach (var kvp in typeColors)
                {
                    if (!firstSet)
                    {
                        firstColor = kvp.Value;
                        firstSet = true;
                    }
                    else if (kvp.Value != firstColor)
                    {
                        allColorsSame = false;
                        break;
                    }
                }

                if (!allColorsSame)
                {
                    log.AppendLine("  [PASS] Marker type materials have distinct colors (type differentiation works).");
                    passCount++;
                }
                else
                {
                    log.AppendLine("  [WARN] All marker type materials have the same color (type differentiation may not work).");
                    warnCount++;
                }
            }

            // 10. Metadata preserved
            if (markersMetadataPreserved == allMarkers.Length)
            {
                log.AppendLine($"  [PASS] All {allMarkers.Length} markers have preserved metadata (non-empty ZoneId + MarkerId).");
                passCount++;
            }
            else
            {
                log.AppendLine($"  [WARN] {allMarkers.Length - markersMetadataPreserved} marker(s) have incomplete metadata.");
                warnCount++;
            }

            // Summary
            log.AppendLine($"\n  === Summary: PASS={passCount} FAIL={failCount} WARN={warnCount} ===");
            log.AppendLine("===== Phase 14.10-D-2: Validate Marker Debug Visuals Complete =====");
            Debug.Log(log.ToString());
        }

        // ===== Internal Helpers =====

        /// <summary>
        /// URP 호환 shader를 우선순위대로 찾아 반환한다.
        /// 1. Universal Render Pipeline/Unlit
        /// 2. Universal Render Pipeline/Lit
        /// 3. Sprites/Default
        /// 4. Unlit/Color (fallback)
        /// </summary>
        private static Shader FindURPCompatibleShader()
        {
            // 우선순위 1: Universal Render Pipeline/Unlit
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader != null) return shader;

            // 우선순위 2: Universal Render Pipeline/Lit
            shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null) return shader;

            // 우선순위 3: Sprites/Default
            shader = Shader.Find("Sprites/Default");
            if (shader != null) return shader;

            // 우선순위 4: Unlit/Color (최후의 fallback)
            shader = Shader.Find("Unlit/Color");
            if (shader != null) return shader;

            // 모든 shader를 찾을 수 없으면 null 반환
            Debug.LogError("[MarkerDebugVisual] No URP-compatible shader found! Check URP package installation.");
            return null;
        }

        /// <summary>
        /// material의 shader가 Standard shader이거나 null인 경우 URP 호환 shader로 교체한다.
        /// </summary>
        private static void EnsureMaterialShaderIsURP(Material mat)
        {
            if (mat == null) return;

            Shader currentShader = mat.shader;

            // shader가 null이거나 Standard shader인 경우에만 교체
            bool needsReplacement = (currentShader == null) || currentShader.name.Contains("Standard");

            if (!needsReplacement) return;

            Shader urpShader = FindURPCompatibleShader();
            if (urpShader == null)
            {
                Debug.LogError($"[MarkerDebugVisual] Cannot replace shader for material '{mat.name}': no URP shader found.");
                return;
            }

            // shader 교체
            mat.shader = urpShader;

            // URP shader에 맞게 color 프로퍼티 재설정
            SetMaterialColorSafe(mat, mat.color);

            Debug.Log($"[MarkerDebugVisual] Replaced shader for material '{mat.name}': " +
                      $"'{currentShader?.name ?? "null"}' -> '{urpShader.name}'");
        }

        /// <summary>
        /// shader 프로퍼티에 안전하게 material color를 설정한다.
        /// _BaseColor (URP Unlit/Lit), _Color (Legacy), mat.color 순으로 시도.
        /// </summary>
        private static void SetMaterialColorSafe(Material mat, Color color)
        {
            if (mat == null) return;

            // URP shader는 _BaseColor 사용
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", color);
            }

            // Legacy shader는 _Color 사용
            if (mat.HasProperty("_Color"))
            {
                mat.SetColor("_Color", color);
            }

            // mat.color도 설정 (가장 기본적인 방법)
            mat.color = color;
        }

        /// <summary>
        /// URP 호환 shader에 대해 투명도(alpha blending)를 설정한다.
        /// </summary>
        private static void SetupTransparentMode(Material mat)
        {
            if (mat == null) return;

            // URP Unlit shader의 SurfaceType 설정
            if (mat.HasProperty("_Surface"))
            {
                // _Surface: 0=Opaque, 1=Transparent
                mat.SetFloat("_Surface", 1.0f);
            }

            // URP Blend mode 설정
            if (mat.HasProperty("_Blend"))
            {
                // _Blend: 0=Alpha, 1=Premultiply, 2=Additive, 3=Multiply
                mat.SetFloat("_Blend", 0.0f);
            }

            // URP Alpha Clipping
            if (mat.HasProperty("_AlphaClip"))
            {
                mat.SetFloat("_AlphaClip", 0.0f);
            }

            // Render queue 설정 (Transparent)
            mat.renderQueue = 3000;

            // ZWrite 설정
            if (mat.HasProperty("_ZWrite"))
            {
                mat.SetFloat("_ZWrite", 0.0f);
            }

            // Keyword 설정
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");

            // SurfaceType keyword (URP)
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }

        /// <summary>
        /// marker type에 대응하는 material 이름을 반환한다.
        /// </summary>
        private static string GetMaterialNameForType(WorldMapZoneContentMarkerType markerType)
        {
            if (MaterialNameMap.TryGetValue(markerType, out string name))
            {
                return name;
            }
            return null;
        }

        /// <summary>
        /// Editor material cache를 초기화한다.
        /// Assets/_Project/Generated/EditorDebugMaterials 폴더 아래에
        /// marker type별 material을 생성/로드하여 캐싱한다.
        /// URP 호환 shader를 사용하며, 기존 Standard shader material은 자동 교체한다.
        /// </summary>
        private static void EnsureMaterialCache()
        {
            if (_cachedMaterials != null) return;

            _cachedMaterials = new Dictionary<WorldMapZoneContentMarkerType, Material>();

            // GeneratedMaterialsFolder 존재 확인/생성
            if (!AssetDatabase.IsValidFolder(GeneratedMaterialsFolder))
            {
                // 상위 폴더 확인
                string parentFolder = "Assets/_Project/Generated";
                if (!AssetDatabase.IsValidFolder(parentFolder))
                {
                    AssetDatabase.CreateFolder("Assets/_Project", "Generated");
                }
                AssetDatabase.CreateFolder(parentFolder, "EditorDebugMaterials");
            }

            // 각 marker type별 material 생성/로드
            foreach (var kvp in MaterialNameMap)
            {
                WorldMapZoneContentMarkerType markerType = kvp.Key;
                string matName = kvp.Value;
                string matPath = $"{GeneratedMaterialsFolder}/{matName}.mat";

                Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    // 새 material 생성 — URP 호환 shader 사용
                    Shader urpShader = FindURPCompatibleShader();
                    if (urpShader == null)
                    {
                        Debug.LogError($"[MarkerDebugVisual] Cannot create material '{matName}': no URP shader found. " +
                                       "Falling back to built-in shader.");
                        mat = new Material(Shader.Find("Standard"));
                    }
                    else
                    {
                        mat = new Material(urpShader);
                    }

                    mat.name = matName;

                    // color 설정 (shader 프로퍼티에 안전하게)
                    Color markerColor = MarkerColorMap[markerType];
                    SetMaterialColorSafe(mat, markerColor);

                    // 투명도 설정
                    SetupTransparentMode(mat);

                    AssetDatabase.CreateAsset(mat, matPath);
                    AssetDatabase.SaveAssets();
                }
                else
                {
                    // 기존 material이 있으면 shader 검사 및 교체
                    EnsureMaterialShaderIsURP(mat);

                    // color가 올바른지 확인하고 갱신
                    Color expectedColor = MarkerColorMap[markerType];
                    SetMaterialColorSafe(mat, expectedColor);

                    // 투명도 설정 확인
                    SetupTransparentMode(mat);

                    // 변경사항 저장
                    EditorUtility.SetDirty(mat);
                }

                _cachedMaterials[markerType] = mat;
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// marker root GameObject 하위에 DebugVisual child를 생성한다.
        /// </summary>
        private static GameObject CreateDebugVisual(GameObject markerRoot, WorldMapZoneContentMarkerType markerType)
        {
            if (markerRoot == null) return null;

            // 1. DebugVisual child GameObject 생성
            GameObject debugVisual = new GameObject(DebugVisualChildName);
            debugVisual.transform.SetParent(markerRoot.transform, false);

            // 2. Transform 설정
            debugVisual.transform.localPosition = Vector3.zero;
            debugVisual.transform.localRotation = Quaternion.identity;
            debugVisual.transform.localScale = GetMarkerScale(markerType);

            // 3. Primitive mesh 추가
            PrimitiveType primitiveType = GetMarkerPrimitive(markerType);

            // Quad는 PrimitiveType.Quad가 아닌 Plane을 사용 (Quad는 MeshFilter가 없음)
            // 실제로 Quad는 PrimitiveType.Quad로 생성하면 MeshFilter가 없으므로 Plane으로 대체
            GameObject primitive = null;
            if (primitiveType == PrimitiveType.Quad)
            {
                // Quad는 GameObject.CreatePrimitive로 생성 시 MeshFilter가 없음
                // 대신 Plane을 생성하고 MeshFilter의 mesh를 Quad mesh로 교체
                primitive = GameObject.CreatePrimitive(PrimitiveType.Plane);
                // Plane mesh를 Quad mesh로 교체
                Mesh quadMesh = BuildQuadMesh();
                MeshFilter mf = primitive.GetComponent<MeshFilter>();
                if (mf != null && quadMesh != null)
                {
                    mf.sharedMesh = quadMesh;
                }
            }
            else
            {
                primitive = GameObject.CreatePrimitive(primitiveType);
            }

            if (primitive != null)
            {
                // primitive의 컴포넌트를 debugVisual로 이동
                MeshFilter srcMf = primitive.GetComponent<MeshFilter>();
                MeshRenderer srcMr = primitive.GetComponent<MeshRenderer>();
                Collider srcCol = primitive.GetComponent<Collider>();

                // MeshFilter 복사
                if (srcMf != null)
                {
                    MeshFilter dstMf = debugVisual.AddComponent<MeshFilter>();
                    dstMf.sharedMesh = srcMf.sharedMesh;
                }

                // MeshRenderer 복사 — material 할당
                if (srcMr != null)
                {
                    MeshRenderer dstMr = debugVisual.AddComponent<MeshRenderer>();
                    dstMr.sharedMaterial = GetMarkerMaterial(markerType);
                }

                // Collider 제거 (primitive 생성 시 자동으로 붙음)
                // debugVisual에는 Collider를 추가하지 않음

                // primitive 삭제
                Object.DestroyImmediate(primitive);
            }

            // 4. Collider가 있으면 제거 (안전장치)
            Collider existingCollider = debugVisual.GetComponent<Collider>();
            if (existingCollider != null)
            {
                Object.DestroyImmediate(existingCollider);
            }

            Undo.RegisterCreatedObjectUndo(debugVisual, $"Create DebugVisual for {markerRoot.name}");

            return debugVisual;
        }

        /// <summary>
        /// 기존 DebugVisual GameObject의 내용물을 marker type에 맞게 갱신한다.
        /// MeshFilter/MeshRenderer/Collider를 교체한다.
        /// </summary>
        private static void RebuildDebugVisualContent(GameObject debugVisual, WorldMapZoneContentMarkerType markerType)
        {
            if (debugVisual == null) return;

            // 1. Transform 설정
            debugVisual.transform.localPosition = Vector3.zero;
            debugVisual.transform.localRotation = Quaternion.identity;
            debugVisual.transform.localScale = GetMarkerScale(markerType);

            // 2. 기존 MeshFilter/MeshRenderer/Collider 제거
            MeshFilter existingMf = debugVisual.GetComponent<MeshFilter>();
            MeshRenderer existingMr = debugVisual.GetComponent<MeshRenderer>();
            Collider existingCol = debugVisual.GetComponent<Collider>();

            if (existingMf != null) Object.DestroyImmediate(existingMf);
            if (existingMr != null) Object.DestroyImmediate(existingMr);
            if (existingCol != null) Object.DestroyImmediate(existingCol);

            // 3. Primitive mesh 재생성
            PrimitiveType primitiveType = GetMarkerPrimitive(markerType);

            GameObject primitive = null;
            if (primitiveType == PrimitiveType.Quad)
            {
                primitive = GameObject.CreatePrimitive(PrimitiveType.Plane);
                Mesh quadMesh = BuildQuadMesh();
                MeshFilter mf = primitive.GetComponent<MeshFilter>();
                if (mf != null && quadMesh != null)
                {
                    mf.sharedMesh = quadMesh;
                }
            }
            else
            {
                primitive = GameObject.CreatePrimitive(primitiveType);
            }

            if (primitive != null)
            {
                MeshFilter srcMf = primitive.GetComponent<MeshFilter>();
                MeshRenderer srcMr = primitive.GetComponent<MeshRenderer>();

                if (srcMf != null)
                {
                    MeshFilter dstMf = debugVisual.AddComponent<MeshFilter>();
                    dstMf.sharedMesh = srcMf.sharedMesh;
                }

                if (srcMr != null)
                {
                    MeshRenderer dstMr = debugVisual.AddComponent<MeshRenderer>();
                    dstMr.sharedMaterial = GetMarkerMaterial(markerType);
                }

                Object.DestroyImmediate(primitive);
            }

            // 4. Collider 제거 확인
            Collider remainingCol = debugVisual.GetComponent<Collider>();
            if (remainingCol != null) Object.DestroyImmediate(remainingCol);
        }

        /// <summary>
        /// Quad mesh를 빌드한다. Quad는 PrimitiveType.Quad로 생성 시 MeshFilter가 없으므로
        /// 직접 mesh를 생성한다.
        /// </summary>
        private static Mesh BuildQuadMesh()
        {
            Mesh mesh = new Mesh();
            mesh.name = "Quad_DebugVisual";

            Vector3[] vertices = new Vector3[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
            };

            int[] triangles = new int[]
            {
                0, 2, 1,
                2, 3, 1,
            };

            Vector3[] normals = new Vector3[]
            {
                Vector3.forward,
                Vector3.forward,
                Vector3.forward,
                Vector3.forward,
            };

            Vector2[] uv = new Vector2[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
            };

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.normals = normals;
            mesh.uv = uv;

            return mesh;
        }

        /// <summary>
        /// marker type에 대응하는 PrimitiveType을 반환한다.
        /// </summary>
        private static PrimitiveType GetMarkerPrimitive(WorldMapZoneContentMarkerType markerType)
        {
            if (MarkerPrimitiveMap.TryGetValue(markerType, out PrimitiveType primitive))
            {
                return primitive;
            }
            return PrimitiveType.Cube;
        }

        /// <summary>
        /// marker type에 대응하는 local scale을 반환한다.
        /// </summary>
        private static Vector3 GetMarkerScale(WorldMapZoneContentMarkerType markerType)
        {
            if (MarkerScaleMap.TryGetValue(markerType, out Vector3 scale))
            {
                return scale;
            }
            return Vector3.one;
        }

        /// <summary>
        /// marker type에 대응하는 editor material을 반환한다.
        /// </summary>
        private static Material GetMarkerMaterial(WorldMapZoneContentMarkerType markerType)
        {
            if (_cachedMaterials != null && _cachedMaterials.TryGetValue(markerType, out Material mat))
            {
                return mat;
            }
            return null;
        }

        /// <summary>
        /// 특정 이름을 가진 직계 자식의 개수를 센다.
        /// </summary>
        private static int CountChildrenNamed(Transform parent, string childName)
        {
            if (parent == null) return 0;
            int count = 0;
            for (int i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i).name == childName)
                {
                    count++;
                }
            }
            return count;
        }
    }
}
