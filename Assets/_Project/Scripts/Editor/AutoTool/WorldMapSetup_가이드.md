# 월드맵 시스템 자동화 도구 사용 가이드 (한국어)

## 개요
이 문서는 월드맵 시스템을 빠르게 설정하기 위한 에디터 자동화 도구의 사용 방법을 설명합니다.

## 설치된 도구

### 1. WorldMapSetupTool (주요 설정 도구)
**위치**: `Tools > World Map > Setup Tool`

**기능**:
- 모든 필요한 폴더 자동 생성
- ScriptableObject 에셋 일괄 생성
- 씬 오브젝트 자동 배치
- 테스트 컴포넌트 자동 설정

### 2. 월드맵 툴바 버튼
**위치**: Scene 뷰 상단 중앙에 "World Map Setup" 버튼 표시

**기능**:
- 한 번 클릭으로 전체 설정 완료
- Scene 뷰에서 바로 접근 가능

### 3. 컨텍스트 메뉴
**위치**: 
- `GameObject > World Map > Create Debug Setup`
- `GameObject > World Map > Test Current Zone`
- `GameObject > World Map > Run All Tests`

## 사용 방법

### 방법 1: 빠른 설정 (권장)
1. Unity 에디터에서 `Tools > World Map > Quick Setup All` 실행
2. 또는 Scene 뷰 상단의 "World Map Setup" 버튼 클릭

**이렇게 하면 자동으로 생성되는 항목**
```
1. 폴더 구조 생성:
   Assets/_Project/ScriptableObjects/World/
   ├── Config/          # 설정 파일
   ├── Zones/           # 존 데이터
   ├── UnlockConditions/# 해금 조건
   └── Debug/           # 디버그용

2. ScriptableObject 에셋 생성:
   - WorldMapConfig_Main.asset (월드맵 기본 설정)
   - UCS_Free.asset (빈 해금 조건)
   - UCS_ResearchZone_Test.asset (연구 구역 테스트 조건)
   - UCS_SealedNorth_Test.asset (봉인 북부 구역 테스트 조건)
   - ZoneData_DefaultFallback.asset (기본 폴백 존)
   - ZoneData_Hub_E5.asset (시작 허브 존)
   - ZoneData_Research_I6.asset (연구 구역)
   - ZoneData_Sealed_F10.asset (봉인 북부 구역)

3. 씬 오브젝트 생성:
   WorldMap_DebugRoot
   ├── WorldMapSystemTestRoot (테스트 컴포넌트 포함)
   └── WorldMapTestPosition (테스트 위치 마커)
```

### 방법 2: 단계별 설정
1. `Tools > World Map > Setup Tool` 열기
2. 각 섹션별로 버튼 클릭:
   - **1. 폴더 생성**: 기본 폴더 구조 생성
   - **2. WorldMapConfig 생성**: 월드맵 기본 설정 생성
   - **3. 해금 조건 세트 생성**: 3가지 테스트 조건 세트 생성
   - **4. 존 데이터 에셋 생성**: 4가지 테스트 존 데이터 생성
   - **5. 씬 오브젝트 생성**: 디버그 오브젝트 생성
   - **6. 테스트 컴포넌트 설정**: 테스트 컴포넌트 자동 설정
   - **7. 런타임 테스트 설정 (Phase 3)**: 런타임 서비스 테스트 오브젝트 생성 및 설정

## 생성된 에셋 상세 설명

### 1. WorldMapConfig_Main
**위치**: `Assets/_Project/ScriptableObjects/World/Config/`

**설정값**:
- Grid: 10x10 (100개 존)
- Zone Size: 400 유닛
- World Bounds: -2000 ~ 2000 (X, Z 축)
- Default Zone: ZoneData_DefaultFallback

### 2. 해금 조건 세트 (3종)
**위치**: `Assets/_Project/ScriptableObjects/World/UnlockConditions/`

**UCS_Free**:
- 빈 조건 세트 (항상 통과)
- 테스트용 기본 존에 사용

**UCS_ResearchZone_Test**:
- Traversal: `upgrade_battery_mk2` 업그레이드 필요
- Knowledge: 최소 2개 로그 필요
- Narrative: `mara_research_analysis_done` 플래그 필요

**UCS_SealedNorth_Test**:
- Traversal: `upgrade_pressure_hull_lv3` 업그레이드 필요
- Knowledge: `log_base_02`와 `log_research_03` 로그 필요
- Risk: `upgrade_resonance_filter` 업그레이드 필요

### 3. 존 데이터 (4종)
**위치**: `Assets/_Project/ScriptableObjects/World/Zones/`

**ZoneData_DefaultFallback**:
- ZoneId: E5
- Region: Hub
- 항상 해금됨
- WorldMapConfig의 기본 존으로 설정됨

**ZoneData_Hub_E5**:
- ZoneId: E5  
- Region: Hub
- 시작 존 (IsStartingZone = true)
- Village Dock 표시명
- 연두색 계열

**ZoneData_Research_I6**:
- ZoneId: I6
- Region: East  
- Research Waters 표시명
- 위험도: 0.35 (중간 위험)
- UCS_ResearchZone_Test 조건 세트 사용
- 노란색 계열

**ZoneData_Sealed_F10**:
- ZoneId: F10
- Region: North
- Sealed Perimeter 표시명
- 위험도: 0.7 (고위험)
- UCS_SealedNorth_Test 조건 세트 사용
- 빨간색 계열

## 테스트 방법

### 1. 기본 테스트
1. Hierarchy에서 `WorldMapSystemTestRoot` 선택
2. Inspector에서 컴포넌트 우상단 점 메뉴 클릭
3. `Run Tests` 또는 `Print Current Zone Info` 실행

### 2. 위치 이동 테스트
1. `WorldMapTestPosition` 오브젝트 선택
2. Transform 위치 변경:
   - **허브 구역**: (-200, 0, -200) → E5 존
   - **연구 구역**: (1400, 0, 200) → I6 존  
   - **봉인 북부**: (200, 0, 1800) → F10 존
3. 각 위치에서 `Test Current Zone` 실행

### 3. Gizmo 시각화
1. `WorldMapSystemTestRoot` 선택 상태에서 Scene 뷰 확인
2. 표시되는 요소:
   - **초록색 박스**: 현재 존 경계
   - **노란색 구**: 존 중심점
   - **빨간색 구**: 테스트 위치
   - **파란색 박스**: 전체 월드 경계

## 문제 해결

### 1. 컴파일 에러 발생 시
- Unity 재시작
- `Assets > Reimport All` 실행
- Editor 스크립트만 다시 컴파일

### 2. 에셋이 생성되지 않을 때
- `Tools > World Map > Setup Tool` 열기
- 각 섹션별로 수동 생성 버튼 클릭
- Console 로그 확인

### 3. 테스트 컴포넌트 작동 안 할 때
- `WorldMapSystemTestRoot` 오브젝트 삭제
- `Tools > World Map > Setup Tool`에서 "테스트 컴포넌트 설정" 실행

## 추가 작업 가이드

### 새로운 존 추가하기
1. `Assets/_Project/ScriptableObjects/World/Zones/` 폴더에서
2. 우클릭 → `Create > Project/World/Zone Data`
3. Inspector에서 설정:
   - ZoneId: 원하는 존 코드 (예: "G7")
   - RegionId: 리전 이름 (예: "West")
   - DisplayName: 표시 이름
   - UnlockConditionSet: 해금 조건 세트 선택
   - BaseRiskLevel: 위험도 (0-1)
   - 기타 옵션 설정

### 새로운 해금 조건 세트 만들기
1. `Assets/_Project/ScriptableObjects/World/UnlockConditions/` 폴더에서
2. 우클릭 → `Create > Project/World/Unlock Condition Set`
3. Inspector에서 4축 조건 추가:
   - Traversal Conditions: 이동 관련 조건
   - Knowledge Conditions: 지식 관련 조건  
   - Narrative Conditions: 서사 관련 조건
   - Risk Conditions: 위험도 관련 조건

### 월드맵 설정 수정
1. `WorldMapConfig_Main.asset` 더블클릭
2. Inspector에서 수정:
   - Grid 크기 변경
   - Zone 크기 조정
   - 월드 경계 수정
   - 기본 존 데이터 변경

## Phase 3: 런타임 테스트 (WorldMapService)

### 개요
Phase 3에서는 ZoneResolver, ZoneRepository, ZoneStateEvaluator, WorldMapService를 조립하여 런타임 서비스를 테스트합니다.

### 자동 설정 방법
1. `Tools > World Map > Setup Tool` 열기
2. **7. 런타임 테스트 설정 (Phase 3)** 섹션에서:
   - `런타임 테스트 오브젝트 생성` 버튼 클릭
   - `런타임 테스트 컴포넌트 설정` 버튼 클릭

### 생성되는 씬 계층 구조
```
WorldMap_RuntimeRoot
├── WorldMapRuntimeTestRoot (WorldMapRuntimeTest 컴포넌트)
└── WorldMapTrackedTransform (추적용 Transform, 초기 위치: (-200, 0, -200))
```

### WorldMapRuntimeTest 설정값
| 필드 | 값 |
|------|-----|
| WorldMapConfig | WorldMapConfig_Main |
| ZoneDataAssets | ZoneData_DefaultFallback, ZoneData_Hub_E5, ZoneData_Research_I6, ZoneData_Sealed_F10 |
| TrackedTransform | WorldMapTrackedTransform |
| UpdateEveryFrame | false (초기), true (실시간 테스트) |
| UseMockProgress | true |
| TestUpgradeId | upgrade_battery_mk2 |
| TestLogId | log_intro_01 |
| TestNarrativeFlag | mara_research_analysis_done |
| MockHullTier | 2 |
| MockDepthLevel | 3 |
| MockSensorAccuracy | 0.8 |
| MockLogCount | 5 |

### 테스트 실행 방법

#### 1. 초기화 및 상태 확인
1. Hierarchy에서 `WorldMapRuntimeTestRoot` 선택
2. Inspector에서 컴포넌트 우상단 점 메뉴(Context Menu) 클릭
3. 순서대로 실행:
   - **Initialize Runtime Test**: 서비스 초기화 및 현재 존 설정
   - **Print Current Zone State**: 현재 존의 상세 상태 출력
   - **Print Test Zone State By Id**: 5개 테스트 존(E5, I6, F10, A1, J10) 상태 출력

#### 2. 위치 변경 테스트
1. `WorldMapTrackedTransform` 오브젝트 선택
2. Transform Position 변경:
   - **허브 구역 (E5)**: (-200, 0, -200)
   - **연구 구역 (I6)**: (1400, 0, 200)
   - **봉인 북부 (F10)**: (200, 0, 1800)
3. `WorldMapRuntimeTestRoot` 선택 → Context Menu → `Refresh Current Zone` 실행
4. `Print Current Zone State`로 변경 확인

#### 3. 실시간 이동 테스트
1. `WorldMapRuntimeTestRoot` 선택
2. Inspector에서 `Update Every Frame = true` 설정
3. `Initialize Runtime Test` 실행
4. Scene 뷰에서 `WorldMapTrackedTransform`을 드래그하여 이동
5. Console에서 `[WorldMapRuntimeTest] Zone changed to: ...` 로그 확인

#### 4. Gizmo 시각화
1. `WorldMapRuntimeTestRoot` 선택 상태에서 Scene 뷰 확인
2. 표시되는 요소:
   - **초록색 박스**: 현재 존 경계
   - **노란색 구**: 존 중심점
   - **빨간색 구**: 추적 위치
   - **파란색 박스**: 전체 월드 경계 (2000 x 2000)

### Mock 진행도 시나리오
기본 Mock 설정으로 다음 조건이 충족됩니다:
- `upgrade_battery_mk2` 업그레이드 보유 → Research Zone Traversal 조건 통과
- 5개 로그 보유 → Research Zone Knowledge 조건 통과
- `mara_research_analysis_done` 플래그 보유 → Research Zone Narrative 조건 통과
- 선체 티어 2, 깊이 레벨 3, 센서 정확도 80%

**예상 결과**:
- **E5 (Hub)**: 항상 해금, 위험도 0%
- **I6 (Research)**: 모든 조건 충족 → 해금, 위험도 35%
- **F10 (Sealed North)**: 조건 미충족 → 잠금, 사유 반환
- **A1 (경계)**: 명시적 데이터 없음 → fallback 데이터로 평가

### Phase 3에서 하지 않는 것
- EventBus 연동 (Phase 4 예정)
- Additive Scene 스트리밍
- Map UI / Minimap
- SpawnResolver
- FishNet 동기화
- BootStrapper 정식 등록

## 주의사항
1. **에셋 참조**: 생성된 에셋은 상대 경로로 참조되므로 폴더 이동 시 주의
2. **ZoneId 형식**: "A1" ~ "J10" 형식 유지 (A-J 열, 1-10 행)
3. **RegionId**: Hub, East, West, North, South 등 일관된 이름 사용
4. **테스트 완료 후**: 실제 게임에서는 `WorldMap_DebugRoot` 오브젝트 제거 또는 비활성화

이 가이드를 통해 월드맵 시스템을 빠르게 설정하고 테스트할 수 있습니다. 문제가 발생하면 Console 로그를 확인하고 필요한 경우 단계별로 수동 설정을 진행하세요.