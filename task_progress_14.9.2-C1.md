# Phase 14.9.2-C1: DesignDatabase validation/data fix only

## Task Plan

### Fix 1: C6 communication/drone reference FAIL 수정
- C6는 최종 Wreck prototype zone
- keyObjects/logOrHint/narrativeFunction에 communication/drone/antenna/OBSERVE LINE 키워드 보강
- 기존 기획 의미 유지

### Fix 2: D5/D6 primaryPurpose validation 수정
- D5/D6를 CreateWreckEntry → CreateHarborEntry로 변경
- D5/D6는 HarborDebrisBelt / Research 접근권으로 변경
- Wreck primary validation은 B5, C5, B6, C6, C7 기준으로 수정

### Fix 3: Sparse WARN 정리
- D10, G1, H1, I1, I6, J4, J5, J9 중 intentionallySparse=false인 zone을 true로 보정
- Hub/Harbor/Wreck prototype zone은 제외

### Fix 4: Validation 기준 최신화
- A~J entries count == 100이면 count 관련 FAIL 없어야 함
- D~J 누락 검증 100개 기준으로 수정
- A1, A10, J1, J10은 EarlySurvival/Hub/Harbor/Wreck로 분류되면 안 됨
