using Project.Data.World;
using UnityEngine;

namespace Project.Gameplay.World
{
    /// <summary>
    /// 씬 오브젝트에 부착하여 해당 오브젝트가 어떤 존(들)에 속하는지 선언하는 경량 컴포넌트.
    ///
    /// WorldMapZoneContentController가 이 컴포넌트를 수집하여 존 상태에 따라 활성화/비활성화를 결정한다.
    ///
    /// [설계 의도]
    /// - 단일 존 바인딩을 기본으로 하되, allowNeighborZoneVisibility로 이웃 존까지 확장 가능
    /// - 발견/해금 조건을 개별적으로 설정 가능
    /// - ContentCategory는 디버그/분류용으로만 사용 (실제 로직에는 영향 없음)
    /// - 인스펙터에서 직관적으로 설정 가능한 경량 구조
    /// </summary>
    public class WorldMapZoneContentBinding : MonoBehaviour
    {
        [Header("Zone Binding")]
        [SerializeField] private ZoneId primaryZoneId; // 이 오브젝트가 속한 주 존

        [Header("Visibility Rules")]
        [SerializeField] private bool activeOnlyWhenCurrentZone = true; // 현재 존이 primaryZoneId일 때만 활성화
        [SerializeField] private bool activeWhenCurrentOrNeighbor; // 현재 존이 primaryZoneId 또는 8-이웃일 때 활성화
        [SerializeField] private bool requireZoneDiscovered; // 대상 존이 발견되어야 활성화
        [SerializeField] private bool requireZoneUnlocked; // 대상 존이 해금되어야 활성화

        [Header("Category")]
        [SerializeField] private ContentCategory category = ContentCategory.Other;

        [Header("Debug")]
        [SerializeField] private string debugLabel; // 인스펙터용 메모
        [SerializeField] private bool showDebugGizmo = true;

        // ===== 내부 캐시 =====

        /// <summary>마지막으로 Controller가 결정한 활성화 상태 (중복 SetActive 방지)</summary>
        private bool _lastActiveState = true; // 기본값 true (Controller가 초기화 전까지 보임)

        /// <summary>주 존 ID (읽기 전용)</summary>
        public ZoneId PrimaryZoneId => primaryZoneId;

        /// <summary>에디터/코드에서 ZoneId 설정 (SerializedObject 없이 직접 할당)</summary>
        public void SetZoneId(ZoneId zoneId)
        {
            primaryZoneId = zoneId;
        }

        /// <summary>현재 존일 때만 활성화</summary>
        public bool ActiveOnlyWhenCurrentZone => activeOnlyWhenCurrentZone;

        /// <summary>현재 존 또는 이웃 존일 때 활성화</summary>
        public bool ActiveWhenCurrentOrNeighbor => activeWhenCurrentOrNeighbor;

        /// <summary>대상 존 발견 필요</summary>
        public bool RequireZoneDiscovered => requireZoneDiscovered;

        /// <summary>대상 존 해금 필요</summary>
        public bool RequireZoneUnlocked => requireZoneUnlocked;

        /// <summary>콘텐츠 카테고리</summary>
        public ContentCategory Category => category;

        /// <summary>디버그 라벨</summary>
        public string DebugLabel => debugLabel;

        /// <summary>마지막으로 설정된 활성화 상태</summary>
        public bool LastActiveState => _lastActiveState;

        /// <summary>
        /// Controller가 결정한 활성화 상태를 적용한다.
        /// 중복 호출을 방지하여 불필요한 SetActive를 피한다.
        /// </summary>
        /// <param name="active">설정할 활성화 상태</param>
        /// <returns>실제로 상태가 변경되었으면 true</returns>
        public bool ApplyActiveState(bool active)
        {
            if (_lastActiveState == active)
                return false; // 변경 없음

            _lastActiveState = active;
            gameObject.SetActive(active);
            return true;
        }

        /// <summary>에디터에서 바인딩 정보를 강제로 새로고침 (컨텍스트 메뉴용)</summary>
        [ContextMenu("Log Binding Info")]
        private void LogBindingInfo()
        {
            UnityEngine.Debug.Log($"[ZoneContentBinding] {name}: Zone={primaryZoneId}, " +
                $"ActiveOnlyWhenCurrent={activeOnlyWhenCurrentZone}, " +
                $"ActiveWhenCurrentOrNeighbor={activeWhenCurrentOrNeighbor}, " +
                $"RequireDiscovered={requireZoneDiscovered}, " +
                $"RequireUnlocked={requireZoneUnlocked}, " +
                $"Category={category}, Label={debugLabel}", this);
        }

#if UNITY_EDITOR
        /// <summary>에디터 기즈모: 바인딩된 존 ID와 카테고리를 라벨로 표시</summary>
        private void OnDrawGizmosSelected()
        {
            if (!showDebugGizmo)
                return;

            // 오브젝트 위치에 작은 아이콘 표시
            Gizmos.color = GetCategoryColor();
            Gizmos.DrawWireSphere(transform.position, 1.5f);

            // 라벨 표시
            DrawLabel(transform.position + Vector3.up * 2.5f, GetLabelText(), GetCategoryColor());
        }

        /// <summary>카테고리별 기즈모 색상</summary>
        private Color GetCategoryColor()
        {
            return category switch
            {
                ContentCategory.Harvest => new Color(0.2f, 0.8f, 0.2f),   // 녹색
                ContentCategory.Log => new Color(0.8f, 0.8f, 0.2f),       // 노란색
                ContentCategory.Hazard => new Color(0.8f, 0.2f, 0.2f),    // 빨간색
                ContentCategory.AmbientProp => new Color(0.2f, 0.6f, 0.8f), // 청색
                ContentCategory.Debug => new Color(0.6f, 0.2f, 0.8f),     // 보라색
                _ => new Color(0.5f, 0.5f, 0.5f),                         // 회색
            };
        }

        /// <summary>라벨 텍스트 생성</summary>
        private string GetLabelText()
        {
            string text = $"[{primaryZoneId}] {category}";
            if (!string.IsNullOrEmpty(debugLabel))
                text += $" {debugLabel}";
            return text;
        }

        /// <summary>Handles.Label을 사용한 3D 라벨 드로잉</summary>
        private static void DrawLabel(Vector3 position, string text, Color color)
        {
            UnityEditor.Handles.BeginGUI();
            var style = new GUIStyle(UnityEditor.EditorStyles.boldLabel)
            {
                normal = { textColor = color },
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter
            };

            Vector3 screenPos = UnityEditor.HandleUtility.WorldToGUIPoint(position);
            float width = Mathf.Max(100f, text.Length * 7f);
            Rect rect = new Rect(screenPos.x - width * 0.5f, screenPos.y - 10f, width, 20f);
            GUI.Label(rect, text, style);
            UnityEditor.Handles.EndGUI();
        }
#endif
    }

    /// <summary>존 바인딩 콘텐츠 카테고리 (분류용)</summary>
    public enum ContentCategory
    {
        /// <summary>채집 가능 오브젝트</summary>
        Harvest,

        /// <summary>로그/문서 오브젝트</summary>
        Log,

        /// <summary>위험 요소 (장애물, 함정 등)</summary>
        Hazard,

        /// <summary>분위기 소품 (Ambient Prop)</summary>
        AmbientProp,

        /// <summary>디버그 전용</summary>
        Debug,

        /// <summary>기타</summary>
        Other
    }
}
