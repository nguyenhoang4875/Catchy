# LogPilot - AAOS Log Viewer 현재 방향 / 목표 / 설계 원칙

## 1. 제품 정의

`LogPilot - AAOS Log Viewer`는 Windows에서 Android / AAOS 로그를 **빠르게 열고, 실시간으로 수집하고, 검색/필터링/내보내기** 하기 위한 데스크톱 로그 디버깅 도구입니다.

현재 제품 방향의 핵심은 아래 4가지입니다.

1. `adb logcat` 실시간 수집이 끊기지 않고 UI를 멈추지 않을 것
2. 큰 로그 파일도 가능한 한 빠르게 첫 화면을 보여줄 것
3. 검색과 필터를 역할이 다른 기능으로 분리해 직관적으로 제공할 것
4. AAOS 디버깅에 필요한 최소 기능을 빠르고 가볍게 제공할 것

---

## 2. 현재 확정 범위

### 포함 범위
- `adb devices` 조회 및 장치 선택
- `adb logcat` 실시간 수집
- `.log`, `.logcat`, `.txt` 파일 열기
- 로그 검색(tag/message/pid 통합)
- 로그 필터(include/exclude, level, tag, pid, text)
- 필터 규칙 저장 / 색상 하이라이트 / 세트 파일 관리
- 로그 export
- 자동 스크롤 / 일시정지 / 검색 결과 패널 / 상태 표시
- Crash / ANR 등 문제 탐색에 필요한 기본 컬럼과 하이라이트

### 현재 제외 또는 후순위
- 복잡한 정규식 최적화
- 협업 / 클라우드 / 원격 동기화
- 무거운 통계 대시보드
- 다중 세션 비교 분석
- 최근 파일 / 세션 프로젝트 복원 고도화

---

## 3. 현재 요구사항 해석

### 실시간 로그
- 장치가 1개면 자동 선택 가능해야 함
- `Start Live`는 현재 기본값으로 **최근 30초 로그를 백필한 뒤 새 로그를 이어서 수집**해야 함
- 필요 시 더 긴 backlog/부팅 로그를 별도 옵션이나 모드로 확장할 수 있어야 함
- 수집 중 pause / auto scroll / 검색 / 필터가 UI를 멈추게 하면 안 됨

### 파일 로그
- `.log`, `.logcat`, `.txt`를 안정적으로 열 수 있어야 함
- 대용량 파일에서 첫 화면 표시가 중요함
- 로딩 중 상태를 사용자에게 알려야 함
- 장기적으로 progress / cancel까지 고려해야 함

### 검색과 필터
- 검색은 상단에서 `tag/message/pid` 통합 검색
- 검색창은 `|` OR / `&` AND를 지원하고, `&`를 `|`보다 우선 적용
- 필터는 좌측 패널에서 현재 표시 로그를 좁히는 역할
- 필터는 include / exclude 조건을 모두 지원
- 필터 규칙 입력의 다중 조건 구분자는 현재 `|` 중심으로 유지

### 필터 규칙 / 세트
- 개별 규칙은 팝업에서 편집
- 규칙은 색상 하이라이트를 가짐
- 여러 규칙을 동시에 활성화할 수 있음
- 규칙 묶음은 파일로 저장 / 열기 가능해야 함
- 미저장 세트는 `New Set`, `Open Set` 전에 저장 여부를 확인해야 함

---

## 4. 현재 기술 스택과 구조

### 기술 스택
- UI: `C# + .NET 8 + WPF`
- 런타임: `.NET 8`
- 플랫폼: Windows 우선

### 솔루션 구조
```text
EasyLog.sln
src/
  EasyLog.App/
  EasyLog.Contracts/
  EasyLog.Engine/
  EasyLog.Engine.Collectors.Adb/
  EasyLog.Engine.Collectors.File/
  EasyLog.Engine.Parsers/
  EasyLog.Engine.Storage/
  EasyLog.Engine.Indexes/
  EasyLog.Engine.Query/
  EasyLog.Engine.Diagnostics/
tests/
  EasyLog.Tests/
benchmarks/
  EasyLog.Benchmarks/
```

### 계층 역할
- `EasyLog.App`: WPF UI, ViewModel, 사용자 상호작용
- `EasyLog.Contracts`: 공용 모델 / 계약
- `EasyLog.Engine`: 세션 조립, orchestration, export
- `Collectors`: `adb`, file 입력 처리
- `Parsers`: logcat line parser
- `Storage`: spool / recent buffer
- `Indexes`: level/tag/pid/time 중심 인덱스
- `Query`: 구조화 필터 / contains 검색
- `Diagnostics`: crash / anr / 문제 패턴 강조

---

## 5. 현재 유효한 아키텍처 원칙

### 1) 스트리밍 입력
- 파일과 `adb` 입력 모두 line stream으로 통일
- 파싱 실패해도 raw line은 보존

### 2) append-only 저장
- 로그는 추가만 하고 재배열하지 않음
- 세션 데이터는 spool + metadata + recent buffer 구조 유지

### 3) 구조화 필터 우선
- level / tag / pid 같은 구조화 조건으로 먼저 후보를 줄임
- 이후 text contains 검색 수행

### 4) UI는 전체 데이터를 소유하지 않음
- UI는 엔진 스냅샷과 이벤트를 소비하는 역할만 맡음
- 대량 append는 반드시 배치로 반영

### 5) 검색과 필터는 분리
- 검색: 특정 로그를 찾는 기능
- 필터: 현재 보이는 로그를 좁히는 기능
- 두 기능을 같은 위치/용어로 섞지 않음

### 6) AAOS 실사용성 우선
- 앱 중심보다 `tag + pid + message` 중심 디버깅에 집중
- 부팅 직후 / 서비스 재시작 / crash / ANR 로그 탐색이 핵심 시나리오

---

## 6. 이미 검증된 설계 판단

### 옳았던 방향
- `threadtime` 파서 우선 전략
- 실시간 append를 큐 + 타이머 기반으로 배치 반영한 것
- `DataGrid` 가상화 사용
- 좌측 필터 패널 / 상단 검색 / 하단 검색 결과 패널로 역할 분리한 것
- 필터 규칙을 인라인 입력이 아니라 팝업 편집으로 옮긴 것
- 필터 규칙 색상 하이라이트를 뷰모델/행 모델에 직접 반영한 것
- `adb` 오류 메시지를 no devices / unauthorized / offline 기준으로 명확히 나눈 것
- LogRowModel을 LogRecord 참조 기반 경량 클래스로 전환한 것 — 문자열 복사 완전 제거
- Tag 문자열 인터닝 — 동일 태그 수십만 건이 1개 인스턴스 공유
- LINQ 핫패스를 명시적 for 루프로 교체하여 delegate 할당 제거

### 교정된 방향
- `adb logcat -T 1`만으로는 제품 요구를 만족하지 못했음
  - 테스트에는 유리하지만 앱에는 짧은 백필이 필요했음
  - 현재는 최근 30초 백필 후 live tail 시작으로 교정됨
  - 더 긴 backlog/부팅 로그는 별도 UX로 남겨둠
- 필터 세트 변경을 자동 저장처럼 다루면 저장 확인 UX가 깨짐
  - dirty 상태와 파일 저장을 분리하는 방향으로 수정
- 대용량 파일에서 줄마다 flush / UI add는 체감 성능을 크게 악화시킴
  - 현재는 배치 flush / batch replace 방향 채택

---

## 7. 현재 핵심 품질 목표

### 성능
- 큰 로그 파일에서도 첫 화면 표시가 빠를 것
- 수집/검색/필터 중 UI가 멈추지 않을 것
- 메모리가 파일 크기에 비례해 불필요하게 폭증하지 않을 것
- Tag 인터닝, LogRowModel 경량화, Snapshot 복사 제거 등으로 대용량 로그 메모리 사용량 최소화

### 안정성
- 앱 시작 실패 시 원인 추적 가능할 것
- `adb` 장치/권한/연결 상태를 사용자에게 이해 가능하게 보여줄 것
- 세트 저장/열기 흐름이 사용자 기대와 어긋나지 않을 것

### UX
- 검색 / 필터 / 라이브 제어의 역할이 분명할 것
- 로딩 / 검색 / 내보내기 상태를 사용자에게 알려줄 것
- AAOS 로그 분석에 필요한 컬럼과 상호작용을 빠르게 제공할 것

---

## 8. 현재 남아 있는 제품 수준 과제

### 최우선
1. `Open Log` 대용량 파일 progress / cancel UX
2. 실제 대형 파일 기준 로드 시간 측정과 병목 추가 제거
3. 필터 세트 `New/Open/Save` 흐름 실사용 검증
4. 최근 30초 백필 `Start Live`의 실제 체감 검증과 필요 시 backlog 옵션 검토
5. 검색 결과창 UX 보완(하이라이트 / 이동성 / 상태 메시지)

### 다음 단계
6. 시간 범위 필터
7. 최근 파일 / 최근 필터 세트
8. 필터 세트 포맷 버전 관리
9. 장치 없는 환경 / adb 오류 UX 정리
10. 장기적으로 progress 기반 partial load 또는 lazy materialization 검토

---

## 9. 현재 문서 운영 원칙

- 이 문서는 **현재 유효한 제품 방향과 설계 판단만** 유지합니다.
- 완료된 생성 절차, 과거 대안 스택, 환경 세팅 초안, 이미 버린 구조는 남기지 않습니다.
- 실제 구현/문제해결 경험은 `AI/Context_Progress.md`에서 관리합니다.
