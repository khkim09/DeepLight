using System.IO;
using UnityEditor;
using UnityEngine;
using Project.Data.World;

namespace Project.Editor.AutoTool
{
    /// <summary>
    /// 기본 WorldMapVisualProfileSetSO 에셋을 자동 생성/보정하는 Editor Utility.
    /// </summary>
    public static class DeepLightMapVisualProfileAssetCreator
    {
        private const string DefaultAssetPath = "Assets/_Project/ScriptableObjects/World/VisualProfiles";
        private const string DefaultAssetName = "WVPS_DeepLight_Default.asset";

        /// <summary>
        /// 기본 Visual Profile Set 에셋을 생성하거나 업데이트한다.
        /// idempotent하게 동작하며, 기존 에셋이 있으면 재사용/보정한다.
        /// </summary>
        [MenuItem("Tools/DeepLight/World Map/Create Default Visual Profile Set")]
        public static void CreateOrUpdateDefaultVisualProfileSetMenu()
        {
            CreateOrUpdateDefaultVisualProfileSet();
        }

        /// <summary>
        /// 기본 Visual Profile Set 에셋을 생성하거나 업데이트하고, 생성된 에셋을 반환한다.
        /// idempotent하게 동작하며, 기존 에셋이 있으면 재사용/보정한다.
        /// </summary>
        public static WorldMapVisualProfileSetSO CreateOrUpdateDefaultVisualProfileSet()
        {
            // 폴더 생성
            if (!Directory.Exists(DefaultAssetPath))
            {
                Directory.CreateDirectory(DefaultAssetPath);
                AssetDatabase.Refresh();
                Debug.Log($"[MapAutoBuilder] Created directory: {DefaultAssetPath}");
            }

            string fullPath = Path.Combine(DefaultAssetPath, DefaultAssetName);

            // 기존 에셋 로드 또는 생성
            WorldMapVisualProfileSetSO profileSet = AssetDatabase.LoadAssetAtPath<WorldMapVisualProfileSetSO>(fullPath);
            if (profileSet == null)
            {
                profileSet = ScriptableObject.CreateInstance<WorldMapVisualProfileSetSO>();
                AssetDatabase.CreateAsset(profileSet, fullPath);
                Debug.Log($"[MapAutoBuilder] Created new Visual Profile Set: {fullPath}");
            }
            else
            {
                Debug.Log($"[MapAutoBuilder] Found existing Visual Profile Set: {fullPath}. Updating rules...");
            }

            // Default Rules 설정 (덮어쓰기)
            SetDefaultRules(profileSet);

            // Biome Overrides 설정 (덮어쓰기)
            SetBiomeOverrides(profileSet);

            // 저장
            EditorUtility.SetDirty(profileSet);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Selection
            Selection.activeObject = profileSet;
            EditorGUIUtility.PingObject(profileSet);

            Debug.Log($"[MapAutoBuilder] Default visual profile set created/updated: {DefaultAssetName}");
            return profileSet;
        }

        /// <summary>
        /// 기본 Depth Visual Rule을 설정한다.
        /// </summary>
        private static void SetDefaultRules(WorldMapVisualProfileSetSO profileSet)
        {
            // SerializedObject로 접근
            SerializedObject so = new SerializedObject(profileSet);
            SerializedProperty defaultRules = so.FindProperty("defaultRules");
            if (defaultRules == null)
            {
                Debug.LogError("[MapAutoBuilder] Could not find defaultRules property on WorldMapVisualProfileSetSO!");
                return;
            }

            // 기존 rules 클리어
            defaultRules.ClearArray();

            // Surface rule (normalized 0.0 ~ 0.05)
            AddDepthRule(defaultRules, ZoneDepthBand.Surface, 0f, 0.05f,
                CreateSurfaceProfile(ZoneDepthBand.Surface, ZoneBiomeType.OpenWater, 0f),
                CreateSurfaceProfile(ZoneDepthBand.Surface, ZoneBiomeType.OpenWater, 0.05f));

            // Shallow rule (normalized 0.05 ~ 0.25)
            AddDepthRule(defaultRules, ZoneDepthBand.Shallow, 0.05f, 0.25f,
                CreateShallowProfileStart(ZoneDepthBand.Shallow, ZoneBiomeType.OpenWater, 0.05f),
                CreateShallowProfileEnd(ZoneDepthBand.Shallow, ZoneBiomeType.OpenWater, 0.25f));

            // Mid rule (normalized 0.25 ~ 0.55)
            AddDepthRule(defaultRules, ZoneDepthBand.Mid, 0.25f, 0.55f,
                CreateMidProfileStart(ZoneDepthBand.Mid, ZoneBiomeType.OpenWater, 0.25f),
                CreateMidProfileEnd(ZoneDepthBand.Mid, ZoneBiomeType.OpenWater, 0.55f));

            // Deep rule (normalized 0.55 ~ 0.85)
            AddDepthRule(defaultRules, ZoneDepthBand.Deep, 0.55f, 0.85f,
                CreateDeepProfileStart(ZoneDepthBand.Deep, ZoneBiomeType.OpenWater, 0.55f),
                CreateDeepProfileEnd(ZoneDepthBand.Deep, ZoneBiomeType.OpenWater, 0.85f));

            // Forbidden rule (normalized 0.85 ~ 1.0)
            AddDepthRule(defaultRules, ZoneDepthBand.Forbidden, 0.85f, 1f,
                CreateForbiddenProfileStart(ZoneDepthBand.Forbidden, ZoneBiomeType.OpenWater, 0.85f),
                CreateForbiddenProfileEnd(ZoneDepthBand.Forbidden, ZoneBiomeType.OpenWater, 1f));

            so.ApplyModifiedProperties();
            Debug.Log($"[MapAutoBuilder] Default rules set: 5 rules (Surface, Shallow, Mid, Deep, Forbidden)");
        }

        /// <summary>
        /// Biome Override를 설정한다.
        /// </summary>
        private static void SetBiomeOverrides(WorldMapVisualProfileSetSO profileSet)
        {
            SerializedObject so = new SerializedObject(profileSet);
            SerializedProperty biomeOverrides = so.FindProperty("biomeOverrides");
            if (biomeOverrides == null)
            {
                Debug.LogError("[MapAutoBuilder] Could not find biomeOverrides property on WorldMapVisualProfileSetSO!");
                return;
            }

            // 기존 biomeOverrides 클리어
            biomeOverrides.ClearArray();

            // Hub: 비교적 안전하고 밝음
            AddBiomeOverride(biomeOverrides, ZoneBiomeType.Hub, so,
                CreateSurfaceProfile(ZoneDepthBand.Surface, ZoneBiomeType.Hub, 0f),
                CreateSurfaceProfile(ZoneDepthBand.Surface, ZoneBiomeType.Hub, 0.05f),
                CreateShallowProfileStart(ZoneDepthBand.Shallow, ZoneBiomeType.Hub, 0.05f),
                CreateShallowProfileEnd(ZoneDepthBand.Shallow, ZoneBiomeType.Hub, 0.25f));

            // ShallowWreck: 약간 탁하고 녹슨 느낌
            AddBiomeOverride(biomeOverrides, ZoneBiomeType.ShallowWreck, so,
                CreateSurfaceProfile(ZoneDepthBand.Surface, ZoneBiomeType.ShallowWreck, 0f),
                CreateSurfaceProfile(ZoneDepthBand.Surface, ZoneBiomeType.ShallowWreck, 0.05f),
                CreateShallowProfileStart(ZoneDepthBand.Shallow, ZoneBiomeType.ShallowWreck, 0.05f),
                CreateShallowProfileEnd(ZoneDepthBand.Shallow, ZoneBiomeType.ShallowWreck, 0.25f));

            // ResearchField: 차갑고 인공적인 푸른 느낌
            AddBiomeOverride(biomeOverrides, ZoneBiomeType.ResearchField, so,
                CreateSurfaceProfile(ZoneDepthBand.Surface, ZoneBiomeType.ResearchField, 0f),
                CreateSurfaceProfile(ZoneDepthBand.Surface, ZoneBiomeType.ResearchField, 0.05f),
                CreateShallowProfileStart(ZoneDepthBand.Shallow, ZoneBiomeType.ResearchField, 0.05f),
                CreateShallowProfileEnd(ZoneDepthBand.Shallow, ZoneBiomeType.ResearchField, 0.25f));

            // SealedNorth: 어둡고 위험한 붉은/보라 tint 약간
            AddBiomeOverride(biomeOverrides, ZoneBiomeType.SealedNorth, so,
                CreateSurfaceProfile(ZoneDepthBand.Surface, ZoneBiomeType.SealedNorth, 0f),
                CreateSurfaceProfile(ZoneDepthBand.Surface, ZoneBiomeType.SealedNorth, 0.05f),
                CreateShallowProfileStart(ZoneDepthBand.Shallow, ZoneBiomeType.SealedNorth, 0.05f),
                CreateShallowProfileEnd(ZoneDepthBand.Shallow, ZoneBiomeType.SealedNorth, 0.25f));

            // AbyssExperiment: 매우 어둡고 비정상적인 색감
            AddBiomeOverride(biomeOverrides, ZoneBiomeType.AbyssExperiment, so,
                CreateSurfaceProfile(ZoneDepthBand.Surface, ZoneBiomeType.AbyssExperiment, 0f),
                CreateSurfaceProfile(ZoneDepthBand.Surface, ZoneBiomeType.AbyssExperiment, 0.05f),
                CreateShallowProfileStart(ZoneDepthBand.Shallow, ZoneBiomeType.AbyssExperiment, 0.05f),
                CreateShallowProfileEnd(ZoneDepthBand.Shallow, ZoneBiomeType.AbyssExperiment, 0.25f));

            // OpenWater: 기본 바이옴 (defaultRules와 동일하지만 명시적으로 추가)
            AddBiomeOverride(biomeOverrides, ZoneBiomeType.OpenWater, so,
                CreateSurfaceProfile(ZoneDepthBand.Surface, ZoneBiomeType.OpenWater, 0f),
                CreateSurfaceProfile(ZoneDepthBand.Surface, ZoneBiomeType.OpenWater, 0.05f),
                CreateShallowProfileStart(ZoneDepthBand.Shallow, ZoneBiomeType.OpenWater, 0.05f),
                CreateShallowProfileEnd(ZoneDepthBand.Shallow, ZoneBiomeType.OpenWater, 0.25f));

            so.ApplyModifiedProperties();
            Debug.Log($"[MapAutoBuilder] Biome overrides set: 6 biomes (Hub, ShallowWreck, ResearchField, SealedNorth, AbyssExperiment, OpenWater)");
        }

        // ===== Rule Addition Helpers =====

        /// <summary>
        /// SerializedProperty 배열에 Depth Rule을 추가한다.
        /// </summary>
        private static void AddDepthRule(SerializedProperty rulesArray, ZoneDepthBand depthBand,
            float normalizedStart, float normalizedEnd,
            WorldMapVisualProfile profileStart, WorldMapVisualProfile profileEnd)
        {
            int index = rulesArray.arraySize;
            rulesArray.InsertArrayElementAtIndex(index);
            SerializedProperty rule = rulesArray.GetArrayElementAtIndex(index);

            rule.FindPropertyRelative("depthBand").enumValueIndex = (int)depthBand;
            rule.FindPropertyRelative("normalizedStart01").floatValue = normalizedStart;
            rule.FindPropertyRelative("normalizedEnd01").floatValue = normalizedEnd;

            SetProfileProperties(rule.FindPropertyRelative("profileAtStart"), profileStart);
            SetProfileProperties(rule.FindPropertyRelative("profileAtEnd"), profileEnd);
        }

        /// <summary>
        /// Biome Override를 추가한다.
        /// Surface + Shallow rule만 포함한다 (나머지는 defaultRules 사용).
        /// </summary>
        private static void AddBiomeOverride(SerializedProperty biomeOverrides, ZoneBiomeType biomeType, SerializedObject so,
            WorldMapVisualProfile surfaceStart, WorldMapVisualProfile surfaceEnd,
            WorldMapVisualProfile shallowStart, WorldMapVisualProfile shallowEnd)
        {
            int index = biomeOverrides.arraySize;
            biomeOverrides.InsertArrayElementAtIndex(index);
            SerializedProperty overrideSet = biomeOverrides.GetArrayElementAtIndex(index);

            overrideSet.FindPropertyRelative("biomeType").enumValueIndex = (int)biomeType;

            SerializedProperty rules = overrideSet.FindPropertyRelative("rules");
            rules.ClearArray();

            // Surface rule
            AddDepthRule(rules, ZoneDepthBand.Surface, 0f, 0.05f, surfaceStart, surfaceEnd);
            // Shallow rule
            AddDepthRule(rules, ZoneDepthBand.Shallow, 0.05f, 0.25f, shallowStart, shallowEnd);
        }

        /// <summary>
        /// SerializedProperty에 VisualProfile 값을 설정한다.
        /// </summary>
        private static void SetProfileProperties(SerializedProperty profileProp, WorldMapVisualProfile profile)
        {
            SetColorProperty(profileProp.FindPropertyRelative("waterTint"), profile.WaterTint);
            SetColorProperty(profileProp.FindPropertyRelative("fogColor"), profile.FogColor);
            SetColorProperty(profileProp.FindPropertyRelative("ambientColor"), profile.AmbientColor);
            profileProp.FindPropertyRelative("fogDensity").floatValue = profile.FogDensity;
            profileProp.FindPropertyRelative("visibilityDistance").floatValue = profile.VisibilityDistance;
            profileProp.FindPropertyRelative("exposure").floatValue = profile.Exposure;
            profileProp.FindPropertyRelative("saturation").floatValue = profile.Saturation;
            profileProp.FindPropertyRelative("contrast").floatValue = profile.Contrast;
            profileProp.FindPropertyRelative("vignetteIntensity").floatValue = profile.VignetteIntensity;
            profileProp.FindPropertyRelative("causticsIntensity").floatValue = profile.CausticsIntensity;
            profileProp.FindPropertyRelative("lightShaftIntensity").floatValue = profile.LightShaftIntensity;
            profileProp.FindPropertyRelative("particleDensityMultiplier").floatValue = profile.ParticleDensityMultiplier;
            profileProp.FindPropertyRelative("normalizedDepth01").floatValue = profile.NormalizedDepth01;
            profileProp.FindPropertyRelative("depthBand").enumValueIndex = (int)profile.DepthBand;
            profileProp.FindPropertyRelative("biomeType").enumValueIndex = (int)profile.BiomeType;
        }

        /// <summary>
        /// SerializedProperty에 Color 값을 설정한다.
        /// </summary>
        private static void SetColorProperty(SerializedProperty colorProp, Color color)
        {
            if (colorProp == null) return;
            colorProp.colorValue = color;
        }

        // ===== Profile Factory Methods =====

        private static WorldMapVisualProfile CreateSurfaceProfile(ZoneDepthBand band, ZoneBiomeType biome, float normalized)
        {
            Color waterTint = biome == ZoneBiomeType.Hub ? new Color(0.2f, 0.7f, 0.8f) :
                              biome == ZoneBiomeType.ShallowWreck ? new Color(0.5f, 0.5f, 0.3f) :
                              biome == ZoneBiomeType.ResearchField ? new Color(0.3f, 0.6f, 0.9f) :
                              biome == ZoneBiomeType.SealedNorth ? new Color(0.4f, 0.3f, 0.5f) :
                              biome == ZoneBiomeType.AbyssExperiment ? new Color(0.2f, 0.1f, 0.3f) :
                              new Color(0.2f, 0.6f, 0.7f); // OpenWater default

            return new WorldMapVisualProfile(
                waterTint: waterTint,
                fogColor: new Color(0.3f, 0.6f, 0.7f),
                ambientColor: new Color(0.4f, 0.6f, 0.7f),
                fogDensity: 0.01f,
                visibilityDistance: 200f,
                exposure: 1.2f,
                saturation: 1.2f,
                contrast: 1f,
                vignetteIntensity: 0f,
                causticsIntensity: 0.8f,
                lightShaftIntensity: 0.8f,
                particleDensityMultiplier: 0.5f,
                normalizedDepth01: normalized,
                depthBand: band,
                biomeType: biome
            );
        }

        private static WorldMapVisualProfile CreateShallowProfileStart(ZoneDepthBand band, ZoneBiomeType biome, float normalized)
        {
            Color waterTint = biome == ZoneBiomeType.Hub ? new Color(0.2f, 0.6f, 0.7f) :
                              biome == ZoneBiomeType.ShallowWreck ? new Color(0.4f, 0.4f, 0.25f) :
                              biome == ZoneBiomeType.ResearchField ? new Color(0.25f, 0.5f, 0.8f) :
                              biome == ZoneBiomeType.SealedNorth ? new Color(0.35f, 0.25f, 0.45f) :
                              biome == ZoneBiomeType.AbyssExperiment ? new Color(0.15f, 0.1f, 0.25f) :
                              new Color(0.15f, 0.5f, 0.6f);

            return new WorldMapVisualProfile(
                waterTint: waterTint,
                fogColor: new Color(0.2f, 0.4f, 0.5f),
                ambientColor: new Color(0.3f, 0.4f, 0.5f),
                fogDensity: 0.03f,
                visibilityDistance: 120f,
                exposure: 1f,
                saturation: 1f,
                contrast: 1f,
                vignetteIntensity: 0.05f,
                causticsIntensity: 0.6f,
                lightShaftIntensity: 0.5f,
                particleDensityMultiplier: 0.7f,
                normalizedDepth01: normalized,
                depthBand: band,
                biomeType: biome
            );
        }

        private static WorldMapVisualProfile CreateShallowProfileEnd(ZoneDepthBand band, ZoneBiomeType biome, float normalized)
        {
            Color waterTint = biome == ZoneBiomeType.Hub ? new Color(0.15f, 0.5f, 0.6f) :
                              biome == ZoneBiomeType.ShallowWreck ? new Color(0.35f, 0.35f, 0.2f) :
                              biome == ZoneBiomeType.ResearchField ? new Color(0.2f, 0.4f, 0.7f) :
                              biome == ZoneBiomeType.SealedNorth ? new Color(0.3f, 0.2f, 0.4f) :
                              biome == ZoneBiomeType.AbyssExperiment ? new Color(0.1f, 0.08f, 0.2f) :
                              new Color(0.1f, 0.4f, 0.5f);

            return new WorldMapVisualProfile(
                waterTint: waterTint,
                fogColor: new Color(0.15f, 0.3f, 0.4f),
                ambientColor: new Color(0.2f, 0.3f, 0.4f),
                fogDensity: 0.05f,
                visibilityDistance: 80f,
                exposure: 0.9f,
                saturation: 0.9f,
                contrast: 1f,
                vignetteIntensity: 0.1f,
                causticsIntensity: 0.4f,
                lightShaftIntensity: 0.3f,
                particleDensityMultiplier: 0.8f,
                normalizedDepth01: normalized,
                depthBand: band,
                biomeType: biome
            );
        }

        private static WorldMapVisualProfile CreateMidProfileStart(ZoneDepthBand band, ZoneBiomeType biome, float normalized)
        {
            return new WorldMapVisualProfile(
                waterTint: new Color(0.08f, 0.3f, 0.45f),
                fogColor: new Color(0.08f, 0.2f, 0.3f),
                ambientColor: new Color(0.1f, 0.2f, 0.3f),
                fogDensity: 0.08f,
                visibilityDistance: 60f,
                exposure: 0.8f,
                saturation: 0.8f,
                contrast: 1f,
                vignetteIntensity: 0.15f,
                causticsIntensity: 0.3f,
                lightShaftIntensity: 0.2f,
                particleDensityMultiplier: 1f,
                normalizedDepth01: normalized,
                depthBand: band,
                biomeType: biome
            );
        }

        private static WorldMapVisualProfile CreateMidProfileEnd(ZoneDepthBand band, ZoneBiomeType biome, float normalized)
        {
            return new WorldMapVisualProfile(
                waterTint: new Color(0.05f, 0.2f, 0.35f),
                fogColor: new Color(0.05f, 0.12f, 0.2f),
                ambientColor: new Color(0.08f, 0.12f, 0.2f),
                fogDensity: 0.12f,
                visibilityDistance: 40f,
                exposure: 0.7f,
                saturation: 0.7f,
                contrast: 1f,
                vignetteIntensity: 0.2f,
                causticsIntensity: 0.15f,
                lightShaftIntensity: 0.1f,
                particleDensityMultiplier: 1.2f,
                normalizedDepth01: normalized,
                depthBand: band,
                biomeType: biome
            );
        }

        private static WorldMapVisualProfile CreateDeepProfileStart(ZoneDepthBand band, ZoneBiomeType biome, float normalized)
        {
            return new WorldMapVisualProfile(
                waterTint: new Color(0.03f, 0.1f, 0.25f),
                fogColor: new Color(0.02f, 0.05f, 0.12f),
                ambientColor: new Color(0.05f, 0.08f, 0.15f),
                fogDensity: 0.15f,
                visibilityDistance: 30f,
                exposure: 0.6f,
                saturation: 0.6f,
                contrast: 1.1f,
                vignetteIntensity: 0.3f,
                causticsIntensity: 0.05f,
                lightShaftIntensity: 0.05f,
                particleDensityMultiplier: 1.5f,
                normalizedDepth01: normalized,
                depthBand: band,
                biomeType: biome
            );
        }

        private static WorldMapVisualProfile CreateDeepProfileEnd(ZoneDepthBand band, ZoneBiomeType biome, float normalized)
        {
            return new WorldMapVisualProfile(
                waterTint: new Color(0.01f, 0.05f, 0.15f),
                fogColor: new Color(0.01f, 0.02f, 0.05f),
                ambientColor: new Color(0.02f, 0.03f, 0.08f),
                fogDensity: 0.2f,
                visibilityDistance: 20f,
                exposure: 0.5f,
                saturation: 0.5f,
                contrast: 1.2f,
                vignetteIntensity: 0.4f,
                causticsIntensity: 0.02f,
                lightShaftIntensity: 0.02f,
                particleDensityMultiplier: 1.8f,
                normalizedDepth01: normalized,
                depthBand: band,
                biomeType: biome
            );
        }

        private static WorldMapVisualProfile CreateForbiddenProfileStart(ZoneDepthBand band, ZoneBiomeType biome, float normalized)
        {
            return new WorldMapVisualProfile(
                waterTint: new Color(0.005f, 0.02f, 0.08f),
                fogColor: new Color(0.005f, 0.01f, 0.03f),
                ambientColor: new Color(0.01f, 0.01f, 0.05f),
                fogDensity: 0.3f,
                visibilityDistance: 10f,
                exposure: 0.3f,
                saturation: 0.3f,
                contrast: 1.3f,
                vignetteIntensity: 0.6f,
                causticsIntensity: 0f,
                lightShaftIntensity: 0f,
                particleDensityMultiplier: 2f,
                normalizedDepth01: normalized,
                depthBand: band,
                biomeType: biome
            );
        }

        private static WorldMapVisualProfile CreateForbiddenProfileEnd(ZoneDepthBand band, ZoneBiomeType biome, float normalized)
        {
            return new WorldMapVisualProfile(
                waterTint: new Color(0f, 0f, 0.03f),
                fogColor: new Color(0f, 0f, 0.01f),
                ambientColor: new Color(0f, 0f, 0.02f),
                fogDensity: 0.5f,
                visibilityDistance: 5f,
                exposure: 0.2f,
                saturation: 0.2f,
                contrast: 1.5f,
                vignetteIntensity: 0.8f,
                causticsIntensity: 0f,
                lightShaftIntensity: 0f,
                particleDensityMultiplier: 2f,
                normalizedDepth01: normalized,
                depthBand: band,
                biomeType: biome
            );
        }
    }
}
