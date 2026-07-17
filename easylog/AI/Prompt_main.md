# LogPilot - AAOS Log Viewer 작업 운영 규칙

## 1. 목적

이 문서는 `LogPilot - AAOS Log Viewer` 프로젝트의 작업 운영 원칙을 정의합니다.

핵심 목적은 다음 3가지입니다.

1. 세션이 바뀌어도 맥락이 끊기지 않을 것
2. 코드 상태와 문서 상태가 어긋나지 않을 것
3. Git / GitLab / 작업 기록이 같은 기준으로 관리될 것

---

## 2. 최상위 운영 원칙

1. **코드가 진실 원본**입니다. 문서는 코드를 설명해야 하며, 코드를 대체하면 안 됩니다.
2. 모든 세션은 시작 전에 아래 3개 문서를 확인합니다.
   - `AI/Prompt_main.md`
   - `AI/Context_Main_Plan_and_goal.md`
   - `AI/Context_Progress.md`
3. 세션 종료 전에는 반드시 `AI/Context_Progress.md`를 갱신합니다.
4. 오래된 계획, 이미 폐기된 방향, 현재 코드와 맞지 않는 설명은 남기지 않습니다.
5. 문서는 길이보다 **현재성 / 인수인계성 / 사실성**을 우선합니다.

---

## 3. 문서 역할 분리

### `AI/Context_Main_Plan_and_goal.md`
- 현재 제품 방향
- 현재 유효한 목표 / 범위 / 설계 원칙
- 장기 계획 중 아직 의미 있는 것만 유지
- 이미 끝난 초기 생성 계획, 폐기된 대안, 현재와 맞지 않는 초안은 제거

### `AI/Context_Progress.md`
- 실제 완료한 작업
- 현재 진행 중인 작업
- 다음 우선순위
- 최근 문제와 해결 경험
- 다음 세션이 바로 이어받을 수 있는 상태

### `AI/Prompt_main.md`
- 운영 규칙
- 문서 관리 규칙
- Git / 보안 규칙

---

## 4. 문서 업데이트 규칙

문서를 갱신할 때는 아래 기준을 따릅니다.

1. **이미 구현된 것**은 완료 항목으로 옮깁니다.
2. **더 이상 쓰지 않는 계획**은 삭제합니다.
3. **실패했던 접근 / 해결된 문제 / 교훈**은 짧게 남깁니다.
4. 다음 세션이 필요한 정보만 남기고, 중복 설명은 줄입니다.
5. "예정", "검토", "가능" 같은 표현은 실제로 아직 유효할 때만 남깁니다.

---

## 5. Git / GitLab 운영 규칙

### 원격 저장소
- 기본 원격: `origin`
- 원격 URL: `http://source.lge.com/gitlab/sungyeon22.kim/easylog`

### 커밋 원칙
- 구조 변경
- 기능 추가
- 버그 수정
- 성능 최적화
- 문서 업데이트

예시:
- `feat: add search result pane and export flow`
- `fix: reduce file load latency with batched spool flush`
- `docs: refresh progress and product context`

### 동기화 순서
1. `git status` 확인
2. 변경 파일 검토
3. `AI/Context_Progress.md` 업데이트
4. 필요 시 `AI/Context_Main_Plan_and_goal.md` 정리
5. 커밋
6. 원격 push 준비

---

## 6. 보안 규칙

다음 정보는 프로젝트 파일에 평문 저장하지 않습니다.

- 비밀번호
- 개인 액세스 토큰
- 세션 쿠키
- 인증 헤더
- API 키

원칙:
- GitLab URL은 기록 가능
- 인증 비밀은 문서/코드/스크립트/커밋 메시지에 남기지 않음
- 인증은 OS 자격 증명 저장소 / IDE 저장소 / PAT 등 외부 보안 저장소 사용

---

## 7. 세션 시작 / 종료 체크리스트

### 시작할 때
1. `AI/Prompt_main.md` 확인
2. `AI/Context_Main_Plan_and_goal.md` 확인
3. `AI/Context_Progress.md` 확인
4. 현재 코드/빌드 상태 확인
5. 이번 세션 목표를 명확히 정리

### 종료할 때
1. 변경 파일 정리
2. `AI/Context_Progress.md` 업데이트
3. 필요 시 `AI/Context_Main_Plan_and_goal.md` 정리
4. 오래된/틀린 설명 제거 여부 확인
5. Git 상태 확인

---

## 8. 핵심 실무 원칙 요약

- 작업 전: 컨텍스트 문서 먼저 읽기
- 작업 중: 코드와 문서를 같이 맞추기
- 작업 후: 진행 문서 반드시 갱신하기
- 문서 작성 시: 계획보다 사실 중심으로 쓰기
- 보안 비밀은 리포지토리에 남기지 않기

---

## 9. 테스트 케이스 관리 규칙

### 테스트 케이스 문서
- `AI/context_test_case.md`에서 기능 동작 기반 테스트 케이스를 관리합니다.

### 운영 원칙

1. **기능 변경 시 TC 업데이트**: 사용자 요청으로 기능 동작이 변경되면, 변경된 기능에 해당하는 테스트 케이스를 즉시 추가하거나 수정합니다.
2. **코드 수정 후 변경점 TC 실행**: 코드 수정 → 빌드 성공 후, 변경점 관련 TC와 기본 기능 TC를 자동 테스트로 실행합니다.
3. **배포 전 풀 TC 검증**: 사용자가 배포 버전 생성을 요청하면, 전체 TC를 실행하여 이상 없는지 점검한 뒤 진행합니다.
4. **기능 동작 베이스 검증**: AI는 빌드 오류 해결뿐 아니라, 기능 동작 수준에서 로그 뷰어에 문제가 없는지 TC를 관리하고 검증해야 합니다.

---

## 10. 로그 필터 적용 최적화 규칙

### 원칙

- **OFF 상태 필터 수정 시 로그 갱신 금지**: 필터가 OFF(IsEnabled=false) 상태일 때 팝업에서 필터 속성을 수정하면, 상태만 저장하고 로그 필터 재적용(RefreshRecords)을 수행하지 않는다.
- **ON/OFF 토글은 항상 갱신**: IsEnabled 속성 자체의 변경(토글)은 필터 적용 상태가 달라지므로 항상 로그를 갱신한다.
- **불필요한 로딩 최소화**: 잦은 필터 재적용은 로딩 오버레이를 반복 표시하여 사용성을 떨어뜨린다. 필터 갱신이 실제로 결과에 영향을 미치는 경우에만 수행한다.

### 적용 위치

| 메서드 | 조건 | 동작 |
|--------|------|------|
| `EditSelectedFilterRuleAsync` | 편집 대상 필터가 OFF | `MarkFilterSetDirty`만 수행, `RefreshRecords` 생략 |
| `OnFilterPresetPropertyChanged` | 변경된 필터가 OFF이고 `IsEnabled` 외 속성 변경 | `InvalidateEnabledFilterRulesCache`만 수행, `SafeRefreshRecordsAsync` 생략 |

---

## 11. 검색 안정성 규칙

### 원칙

- **검색 재진입 시 이전 검색 취소**: 검색 실행 중 새 검색이 요청되면 `CancellationTokenSource`로 이전 검색을 취소하고 새 검색만 실행한다.
- **연타·반복 입력 내성**: 사용자가 Enter를 빠르게 연타해도 에러 없이 마지막 검색만 반영한다.
- **IsBusy 정합성**: 취소된 검색은 `IsBusy`를 해제하지 않고, 현재 활성 검색(`_searchCts == cts`)만 `IsBusy = false`를 수행한다.
- **Dispose 시 정리**: `Dispose()`에서 `_searchCts`를 Cancel/Dispose하여 리소스를 누출하지 않는다.

### TC 분류

| 레벨 | 설명 | 실행 시점 |
|------|------|-----------|
| **Smoke** | 빌드·시작·기본 파싱 | 모든 코드 수정 후 |
| **기능** | 각 기능 영역별 동작 검증 | 변경점 관련 TC + 기본 기능 TC |
| **회귀** | 과거 버그 재발 방지 | 배포 전 풀 TC |
| **UI** | WPF 렌더링·레이아웃 검증 | 배포 전 풀 TC |

### TC 자동화 매핑

| TC 실행 방법 | 커맨드 |
|-------------|--------|
| Smoke + 기능 테스트 | `dotnet run --project tests/EasyLog.Tests -c Debug` |
| UI 렌더링 테스트 | `dotnet test tests/EasyLog.UiTests -c Debug` |
| ADB 실장치 테스트 | `dotnet run --project tests/EasyLog.Tests -c Debug -- --adb-smoke` |

---

## 릴리스 노트: v1.0.3

- 배포일: 2026.06.10
- 배포 파일: `LogPilot-AAOS-Log-Viewer-v1.0.3-win-x64.7z`
- 요약: 로그 로딩/실시간 성능 개선, 안정성 강화, 다중 파일 동일 타임스탬프 순서 버그 수정. 주요 변경사항:
  - 파일 점진적 렌더링, 라이브 배치 append로 로딩/실시간 체감 성능 개선
  - 단일 패스 필터 + 검색 취소, 다중 파일 K-way 병합 적용
  - 다중 파일 로드 시 ms 단위까지 동일한 타임스탬프 레코드의 라인 순서 뒤섞임 버그 수정 (`(Timestamp, RowId)` 안정 정렬, 회귀 테스트 추가)
  - About 팝업 텍스트 선택/복사 가능 + 이메일 복사 버튼 추가
  - csproj 버전 → `1.0.3`

참고: 상세 변경사항은 `docs/Release-Notes-v1.0.3.md`와 `AI/Context_Progress.md`에 기록되어 있습니다.

---

## 릴리스 노트: v1.0.2

- 배포일: 2026.05.21
- 요약: ADB 연결 안정성 대폭 개선, 로그 필터 적용 일관성 강화, 다중 파일 로드/복사 관련 버그 수정. 주요 변경사항:
  - Start Live 시 장치 미연결이면 자동 대기 후 연결 시 라이브 시작 (60초 타임아웃)
  - 라이브 세션 중 ADB 연결 끊김 시 자동 재연결 (기존 로그 보존)
  - 라이브 세션에서 QuickFilter 리셋 문제 수정, 라이브 중 QuickFilter 반영
  - Demo/Sample 로드 후 활성 Filter Rule 즉시 적용
  - 필터 재적용 시 QuickFilter + Filter Rule 통합 파이프라인 적용
  - 다중 파일 로드 시 동일 타임스탬프 레코드의 파일 내 원래 순서 보존
  - Ctrl+C 복사 시 RowId 순서 정렬 및 DataGrid 내장 복사 충돌 방지
  - `FilterQuery.IsEmpty` 속성 추가, csproj 버전 → `1.0.2`

참고: 상세 변경사항은 `docs/Release-Notes-v1.0.2.md`와 `AI/Context_Progress.md`에 기록되어 있습니다.

