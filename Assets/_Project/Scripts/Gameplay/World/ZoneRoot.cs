using Project.Data.World;
using UnityEngine;

namespace Project.Gameplay.World
{
    /// <summary>
    /// 씬에 배치되는 존 오브젝트의 루트 MonoBehaviour.
    /// ZoneDataSO를 참조하며, 하위에 Geometry/Props/HarvestSpawns/LogSpawns/Hazards/Triggers/DebugGizmos
    /// 같은 자식 루트를 둘 수 있는 구조를 제공한다.
    ///
    /// [설계 원칙]
    /// - 런타임에 FindObjectsOfType / Resolver 의존 없음 → 인스펙터에서 모든 참조를 미리 할당
    /// - Gizmo는 transform.position 기준 fallback 박스로 동작 (WorldMapConfig 불필요)
    /// - ValidateSetup()은 ZoneData null 체크 + 이름 일치 검사만 수행
    /// - 추후 프로덕션 빌드에서 Debug.Log만 제거하면 바로 사용 가능
    /// </summary>
    public class ZoneRoot : MonoBehaviour
    {
        [Header("Zone Data")]
        [SerializeField] private ZoneDataSO zoneData;

        [Header("Child Root References (Optional)")]
        [SerializeField] private Transform geometryRoot;
        [SerializeField] private Transform propsRoot;
        [SerializeField] private Transform harvestSpawnsRoot;
        [SerializeField] private Transform logSpawnsRoot;
        [SerializeField] private Transform hazardsRoot;
        [SerializeField] private Transform triggersRoot;
        [SerializeField] private Transform debugGizmosRoot;

        [Header("Gizmo Settings")]
        [SerializeField] private bool drawGizmo = true;
        [SerializeField] private Color gizmoColor = Color.white;
        [SerializeField] private bool showLabel = true;
        [SerializeField] private bool showBounds = true;
        [SerializeField] private bool showCenter = true;

        /// <summary>연결된 ZoneDataSO</summary>
        public ZoneDataSO ZoneData => zoneData;

        /// <summary>ZoneDataSO로부터 ZoneId 반환 (없으면 기본값)</summary>
        public ZoneId GetZoneId()
        {
            return zoneData != null ? zoneData.ZoneId : default;
        }

        /// <summary>ZoneDataSO 반환</summary>
        public ZoneDataSO GetZoneData()
        {
            return zoneData;
        }

        /// <summary>Geometry 자식 루트</summary>
        public Transform GeometryRoot => geometryRoot;

        /// <summary>Props 자식 루트</summary>
        public Transform PropsRoot => propsRoot;

        /// <summary>HarvestSpawns 자식 루트</summary>
        public Transform HarvestSpawnsRoot => harvestSpawnsRoot;

        /// <summary>LogSpawns 자식 루트</summary>
        public Transform LogSpawnsRoot => logSpawnsRoot;

        /// <summary>Hazards 자식 루트</summary>
        public Transform HazardsRoot => hazardsRoot;

        /// <summary>Triggers 자식 루트</summary>
        public Transform TriggersRoot => triggersRoot;

        /// <summary>DebugGizmos 자식 루트</summary>
        public Transform DebugGizmosRoot => debugGizmosRoot;

        /// <summary>
        /// ZoneDataSO와 씬 배치의 유효성 검사.
        /// 런타임 Resolver 의존 없이 ZoneData null 체크 + 이름 일치 검사만 수행.
        /// </summary>
        public bool ValidateSetup()
        {
            if (zoneData == null)
            {
                UnityEngine.Debug.LogError($"[ZoneRoot] {name}: ZoneDataSO is not assigned!", this);
                return false;
            }

            if (!zoneData.IsValid())
            {
                UnityEngine.Debug.LogError($"[ZoneRoot] {name}: ZoneData '{zoneData.name}' is invalid!", this);
                return false;
            }

            // ZoneId와 게임 오브젝트 이름이 일치하는지 검사 (권장)
            string expectedName = $"ZoneRoot_{zoneData.ZoneId}";
            if (name != expectedName)
            {
                UnityEngine.Debug.LogWarning($"[ZoneRoot] {name}: GameObject name mismatch. Expected '{expectedName}' but got '{name}'.", this);
            }

            return true;
        }

        /// <summary>ZoneDataSO의 색상 또는 설정된 기즈모 색상 반환</summary>
        private Color GetEffectiveGizmoColor()
        {
            if (zoneData != null && zoneData.ZoneColor != Color.white)
                return zoneData.ZoneColor;
            return gizmoColor;
        }

        /// <summary>
        /// 에디터 Gizmo 시각화 (ZoneDataSO 색상, 라벨, 경계, 중심).
        /// WorldMapConfig/Resolver 의존 없이 transform.position 기준 fallback 박스 사용.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (!drawGizmo || zoneData == null)
                return;

            Color effectiveColor = GetEffectiveGizmoColor();
            Gizmos.color = effectiveColor;

            // transform.position 기준 200x200 fallback 박스 (zoneSize/2)
            // 추후 WorldMapConfig가 필요하면 인스펙터에 SerializeField로 추가
            Vector3 center = transform.position;
            Vector3 size = new Vector3(200f, 10f, 200f);

            if (showBounds)
            {
                Gizmos.color = effectiveColor;
                Gizmos.DrawWireCube(center, size);
            }

            if (showCenter)
            {
                Gizmos.color = Color.Lerp(effectiveColor, Color.yellow, 0.5f);
                Gizmos.DrawSphere(center, 3f);
            }

            if (showLabel)
            {
                // 라벨은 Handles로 표시 (UnityEditor 의존)
                DrawLabel(center, zoneData.ZoneId.ToString(), effectiveColor);
            }
        }

        /// <summary>Handles.Label을 사용한 3D 라벨 드로잉</summary>
        private static void DrawLabel(Vector3 position, string text, Color color)
        {
#if UNITY_EDITOR
            UnityEditor.Handles.BeginGUI();
            var style = new GUIStyle(UnityEditor.EditorStyles.boldLabel)
            {
                normal = { textColor = color },
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter
            };

            Vector3 screenPos = UnityEditor.HandleUtility.WorldToGUIPoint(position + Vector3.up * 12f);
            Rect rect = new Rect(screenPos.x - 50f, screenPos.y - 12f, 100f, 24f);
            GUI.Label(rect, text, style);
            UnityEditor.Handles.EndGUI();
#endif
        }

        /// <summary>에디터에서 자식 루트 GameObject들을 생성하는 컨텍스트 메뉴</summary>
        [ContextMenu("Create Child Roots")]
        private void CreateChildRoots()
        {
#if UNITY_EDITOR
            string[] rootNames = { "Geometry", "Props", "HarvestSpawns", "LogSpawns", "Hazards", "Triggers", "DebugGizmos" };

            for (int i = 0; i < rootNames.Length; i++)
            {
                Transform existing = GetFieldValue(i);
                if (existing != null)
                    continue;

                GameObject child = new GameObject(rootNames[i]);
                child.transform.SetParent(transform);
                child.transform.localPosition = Vector3.zero;

                SetFieldValue(i, child.transform);
            }

            UnityEditor.EditorUtility.SetDirty(this);
            UnityEngine.Debug.Log($"[ZoneRoot] {name}: Child roots created.", this);
#endif
        }

#if UNITY_EDITOR
        private Transform GetFieldValue(int index)
        {
            switch (index)
            {
                case 0: return geometryRoot;
                case 1: return propsRoot;
                case 2: return harvestSpawnsRoot;
                case 3: return logSpawnsRoot;
                case 4: return hazardsRoot;
                case 5: return triggersRoot;
                case 6: return debugGizmosRoot;
                default: return null;
            }
        }

        private void SetFieldValue(int index, Transform value)
        {
            switch (index)
            {
                case 0: geometryRoot = value; break;
                case 1: propsRoot = value; break;
                case 2: harvestSpawnsRoot = value; break;
                case 3: logSpawnsRoot = value; break;
                case 4: hazardsRoot = value; break;
                case 5: triggersRoot = value; break;
                case 6: debugGizmosRoot = value; break;
            }
        }
#endif
    }
}
