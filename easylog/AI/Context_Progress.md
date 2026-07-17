# LogPilot - AAOS Log Viewer 메모리 뱅크 / 작업일지

## 1. 프로젝트 한 줄 요약

`LogPilot - AAOS Log Viewer`는 Windows에서 Android / AAOS 로그를 **빠르게 열고, 실시간으로 수집하고, 검색/필터링/내보내기** 하기 위한 WPF 기반 로그 뷰어입니다.

---

## 2. 현재 스냅샷

### 현재 기술 스택
- UI: `C# + .NET 8 + WPF`
- 구조: `Contracts / Engine / Collectors / Parsers / Storage / Indexes / Query / Diagnostics / App`
- 실시간 수집: `adb logcat`
- 파일 로드: `.log`, `.logcat`, `.txt`, `.zip`, `.7z` (압축 파일 내 로그 자동 추출, 다중 파일 동시 로드)

### 현재 검증 상태
- 솔루션 빌드: 성공 (경고 0, 오류 0)
- 기본 smoke test: 성공 (인코딩 감지 5종 포함)
- 실장치 `--adb-smoke`: 성공
- 앱 시작: 성공
- 시작 예외 로그: 없음
- 메모리 최적화 적용 후 빌드/테스트: 성공
- 메인 로그 / 검색 결과 컬럼 너비·순서 영속화 재검증: 성공
- 정식 UI 테스트(`tests/EasyLog.UiTests`) 실행: 성공 (3/3 통과)
- `win-x64` self-contained publish: 성공
- `v1.0.0` 7z 패키지 생성: 성공 (46MB)
- Confluence 첨부 업로드 + 한/영 사용자 안내 반영: 성공
- 로그 로드/실시간 로깅 성능 개선 후 검증: Debug/Release 빌드 성공(경고 0, 오류 0), smoke 테스트 성공, UI 테스트 3/3 통과
- v1.0.3 배포 준비: 솔루션 Debug 빌드 성공(경고 0, 오류 0), smoke+기능 테스트 성공(다중 파일 순서 회귀 포함), 앱 시작 검증 성공, `LogPilot-AAOS-Log-Viewer-v1.0.3-win-x64.7z` 패키지 생성 성공(49MB)

### 최근 검증 명령
```bat
cd /d D:\work\EasyLog
dotnet build .\EasyLog.sln -c Release
dotnet run --project .\tests\EasyLog.Tests\EasyLog.Tests.csproj -c Release
dotnet test .\tests\EasyLog.UiTests\EasyLog.UiTests.csproj -c Debug
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Publish-Beta.ps1 -Version v0.0.5
dotnet build .\EasyLog.sln -c Debug
dotnet run --project .\tests\EasyLog.Tests\EasyLog.Tests.csproj -c Debug
dotnet test .\tests\EasyLog.UiTests\EasyLog.UiTests.csproj -c Debug
dotnet build .\EasyLog.sln -c Release
```

---

## 3. 현재 실제 구현된 것

### A. 로그 수집 / 세션
- `adb devices` 조회 및 장치 선택
- 단일 장치가 1개일 때 자동 선택 시도
- `Start Live` / `Stop Live`
- 상단 ADB 연결 indicator(연결됨/연결안됨)
- 장치 연결 해제 시 목록과 선택 장치 자동 정리
- `Pause Updates` / `Auto Scroll`
- 마우스 휠 입력 시 auto scroll 해제
- `Home` 키 입력 시 로그 최상단 이동
- `End` 키 입력 시 auto scroll 재활성화 + 마지막 로그 행 선택/키보드 포커스 이동
- 앱의 `Start Live`는 **최근 30초 로그를 백필한 뒤 새 로그를 이어서 수집**하도록 동작

### B. 파일 로드 / 저장
- `Open Log`로 `.log`, `.logcat`, `.txt` 로드
- `Open Log`로 `.zip`, `.7z` 압축 파일 직접 열기 지원 (내부 로그 파일 자동 추출)
- `Open Log` 다이얼로그에서 **다중 파일 선택(Multiselect)** 지원 — 분할된 여러 로그 파일을 한번에 로드
- 마지막으로 성공적으로 연 로그 파일 경로를 저장하고 앱 재실행 시 자동 복원
- 로그 파일 및 압축 파일 drag & drop으로 열기 지원 (다중 파일 동시 드롭 가능)
- `Export Logs`로 **현재 수집된 전체 로그를 50MB 분할 후 7z 아카이브로 저장**
- 분할 로그 파일명에 각 파일의 마지막 로그 시각 포함
- export한 로그를 다시 `Open Log`로 열 수 있는 plain text 포맷 유지
- 파일 로드 시 검색 상태 / 라이브 상태 정리
- **고성능 배치 로딩**: 동기 파일 읽기(64KB 버퍼) + 5만 건 단위 배치 파싱/저장 + 디스크 스풀 스킵으로 대폭 성능 향상
- **단일 파일 progressive 렌더링**: 일반 단일 로그 파일 전체 로드 시 5만 건 batch가 파싱될 때마다 먼저 UI에 표시하고, 완료 시 최종 목록으로 한 번 정합화
- **필터 룰 ON 상태 파일 로드 중복 렌더링 제거**: 파일 로드 후 전체 렌더링 → 필터 재렌더링 대신, 최종 표시 대상 계산 후 1회 렌더링
- **다중 파일 로드 시 Timestamp 기준 정렬**: 여러 파일을 동시에 로드하면 모든 레코드를 Timestamp 기준으로 정렬한 후 RowId를 1부터 순차 재할당. 로그가 시간순으로 표시됨
- **대용량 파일 로드 진행률 + 취소**: 파일 로드 중 엔진의 진행 상태(파싱 건수, 정렬 중 등)를 로딩 오버레이에 실시간 표시. Cancel 버튼으로 로드 중단 가능. CancellationToken 기반

### B-1. 로그 파서 (ThreadtimeLogParser)
- **다중 포맷 지원**: 연도 접두사(`YYYY-MM-DD`), 마이크로초 타임스탬프, UID 필드, 태그 없는 커널 로그 자동 인식
- **수동 Span 파서**: Regex 완전 제거. `ReadOnlySpan<char>` 기반 수동 파싱으로 zero-allocation 달성
- `V/D/I/W/E/F/S` 전체 로그 레벨 지원

### C. 필터
- 좌측 리사이즈 가능한 `Filter Set` / `Filter Rules` 패널
- 팝업 기반 필터 편집
- include 조건
  - text / message
  - tag
  - pid
- exclude 조건
  - text / message
  - tag
  - pid
- 다중 검색 구분자: `|` 전용
- level 필터(`V/D/I/W/E/F`)
- 현재 필터 적용 / 초기화
- 필터 규칙(`Filter Rule`) 저장/편집/삭제
- 규칙별 색상 하이라이트
- 여러 규칙 동시 활성화 가능
- 선택한 규칙을 `Enable` / `Disable` 버튼으로 즉시 전환 가능
- 규칙별 체크박스로 여러 항목을 일괄 선택 가능
- 상단 `Select all` 체크박스로 규칙 전체 선택 가능
- 체크된 규칙 `Enable` 시 미체크 규칙은 자동으로 `Disable`
- 필터 우선순위를 drag & drop으로 재정렬 가능
- 필터 리스트 우측 상단 `↑ / ↓` 버튼으로 우선순위 조정 가능
- 필터 리스트 내부 상단 헤더에 작은 `Select all`, `↑`, `↓` 컨트롤 배치

### D. 필터 세트
- `New Set`
- `Open Set`
- `Save Set`
- `Save Set As`
- 기본 필터 저장 위치: 실행 파일 기준 `LogFilter/filters.json`
- 기본 배포본은 저장된 필터 없이 빈 `LogFilter/` 폴더만 포함
- 현재 열려 있는 세트 파일명 표시
- 미저장 상태(`dirty`) 추적
- `New Set`, `Open Set` 전에 저장 여부 확인 팝업 표시
- 저장 포맷: JSON

### E. 검색
- 상단 통합 검색창
- `Ctrl+F` 단축키로 검색창 즉시 포커스 및 전체 선택
- 검색 대상: `tag`, `message`, `pid`
- 검색창에서 `|` OR / `&` AND 다중 검색 지원 (`&` 우선 평가)
- 검색창에 `| OR`, `& AND` 힌트 배지 표시
- **검색 범위 선택**: `Search All Logs (ignore filter)` 체크박스로 전체 로그 / 필터된 로그 중 선택 가능
- 검색 범위 옵션 앱 재시작 시에도 영속화 (`UiPreferences.IsSearchInAllLogs`)
- 하단 `Search Results` 패널
- 검색 결과 패널 리사이즈 가능
- 검색 결과 선택 시 메인 로그 위치로 이동
- 메인 로그 그리드의 `Tag / PID / Message`에도 매칭 검색어 하이라이트 표시
- 검색 결과의 `Tag / PID / Message`에 매칭 검색어 하이라이트 표시
- 검색 전에도 패널 용도를 알 수 있도록 안내 문구 표시

### E-1. 검색 히스토리 (v0.0.5)
- **검색 히스토리 드롭다운**: 검색창에 포커스하면 이전 검색어 드롭다운 팝업 표시
- **최대 10개** 히스토리 유지, 최신 검색어가 상단
- **키보드 탐색**: `↑` / `↓` 화살표로 히스토리 항목 선택
- **개별 삭제**: 각 항목 우측 `✕` 버튼으로 히스토리 항목 개별 삭제
- **영속화**: 검색 히스토리를 UiPreferences에 저장하여 앱 재시작 후에도 유지
- **중복 방지**: 동일 검색어 재검색 시 기존 항목을 제거하고 최상단에 재삽입

### E-2. 로딩 오버레이 (v0.0.5)
- **필터 적용 로딩**: 필터 ON/OFF, 추가/삭제/편집 시 "Applying filters..." 로딩 오버레이 표시
- **검색 로딩**: 검색 수행 중 "Searching logs..." 로딩 오버레이 표시
- 기존 파일 로드/내보내기/ADB 세션 시작 등의 로딩 오버레이와 동일한 UI 사용

### F. UI / UX
- 기본 테마: 라이트(흰 배경 / 검정 글씨)
- 앱 아이콘 추가 및 실행 파일 아이콘 반영
- **앱 이름**: `LogPilot - AAOS Log Viewer` (v0.0.5에서 ALV → LogPilot 리브랜딩)
- 메인 상단에 현재 버전 배지 표시 (`v0.0.5`)
- 전체 UI 폰트 종류 선택 메뉴 제공(`Segoe UI`, `Malgun Gothic`, `Bahnschrift`, `Consolas`, `Cascadia Mono`, `Arial`, `Tahoma`)
- 선택한 UI 폰트 종류를 앱 재시작 후에도 유지
- 로그 그리드만 폰트 크기 조절
- 마지막 폰트 크기 설정을 앱 재시작 후에도 유지
- 좌측 필터 패널 + 우측 로그 뷰 구조
- 상단 메뉴 아래 별도 검색 행 배치
- 좌측 `Filter Rules` 액션 바를 `Add / Edit / Enable / Disable / Delete` 중심으로 정리
- 새로 추가한 필터 규칙은 체크박스가 기본 체크 상태로 생성
- 필터 규칙 행 좌클릭으로 체크박스 토글 가능
- 필터 규칙 행 우클릭 컨텍스트 메뉴로 `Edit...` / `Delete` 가능
- 필터 리스트 빈 영역 우클릭 시에도 `Add... / Edit... / Delete` 컨텍스트 메뉴 제공
- 필터 규칙 편집 더블클릭은 실제 항목 행에서만 열리도록 제한
- `Delete Current` 버튼은 체크박스 다중 선택이 아니라 현재 선택된 규칙 1개만 삭제
- 메인 로그/검색 결과 그리드에서 가로 스크롤로 긴 `Message` 끝까지 확인 가능
- 메인 로그/검색 결과 그리드에서 사용자가 조정한 컬럼 너비와 컬럼 순서를 디바운스 저장 + 종료 시 final flush로 영속화하고 앱 재시작 후에도 유지
- **PageUp/PageDown 페이지 단위 선택 이동**: 로그 뷰·검색결과 그리드에서 PageUp/PageDown 시 실제 렌더링된 행 높이 기반으로 한 페이지 분량만큼 선택 행 이동
- **DataGrid 클릭 시 키보드 포커스 이동**: 로그/검색결과 그리드 행 클릭 시 키보드 포커스가 그리드로 이동하여 화살표·PageUp/Down 키가 즉시 동작. 검색창 포커스는 마우스 클릭 또는 Ctrl+F로만 이동
- **ESC 키 포커스 해제**: 검색창 또는 DataGrid에서 ESC 키를 누르면 포커스가 해제됨
- **About 다이얼로그**: `More > About LogPilot`에서 앱 이름, 버전, 카피라이트(`© 2025-2026 LG Electronics Inc.`), 버그/개선 문의 메일(`sungyeon22.kim@lge.com`) 표시. csproj에도 `<Copyright>` 속성 추가
- **상단 툴바 재구성**: 핵심 액션(`Open Log`, `Export Logs`, Device, `Start Live`, `Stop Live`, `Pause`, `Auto`)만 1줄에 유지하고, `Clear Logs`와 설정성 기능은 단일 `More` 메뉴로 통합. Device refresh는 Device 콤보박스 오른쪽에 배치. `Start Live` 옆 드롭다운으로 backfill별 즉시 시작 가능하며 현재 선택 옵션을 체크 표시
- **버튼 클릭감 개선**: 공통 `Button` 스타일에 hover/pressed 색상 변화와 1px 눌림 이펙트 추가
- **Open Log 옵션 팝업 테마 정리**: 어두운 별도 테마 대신 LogPilot 라이트 카드/패널 톤과 보라색 accent로 통일
- **검색 연산자 힌트 정리**: `| OR`, `& AND`를 별도 강한 색 배지 2개가 아니라 보라색 accent의 단일 라이트 힌트 칩으로 통합
- `More > Clear Logs...`로 현재 출력 로그 즉시 비우기 제공
- 필터 편집 팝업은 배경 음영 없이 메인 창 기준 중앙 배치
- 필터 팝업 헤더를 `Configure Log Filter`로 단순화
- 로딩 중 우측 상단 토스트형 오버레이 표시
- `▶ Start Live`, `■ Stop Live` 아이콘형 텍스트 사용
- `Load Demo Logs`, `Load Sample` 버튼 제거
- 오른쪽 디테일 패널 제거
- **Ctrl+C 복사 동작 수정**: TextBox에 선택된 텍스트가 있으면 네이티브 복사, 없으면 마우스 오버/선택 기반으로 DataGrid 로그 행 복사 (DataGridCell.Focusable=False 환경에서도 정상 동작)
- **Ctrl+C가 SearchResultsGrid에서도 동작**: `CopySelectedRows`가 `SearchResultRowModel`도 처리하도록 수정
- **Preview 패널**: 좌측 필터 리스트 하단에 Preview 영역 배치 — 로그/검색 결과에서 선택한 행의 Message를 표시
- Preview 헤더(#, Time, Level, Tag, PID, TID)도 읽기전용 TextBox로 배치하여 드래그/Ctrl+C 복사 가능
- Preview TextBox에서 텍스트 드래그 선택 및 Ctrl+C 복사 가능 (ReadOnly TextBox, Consolas 폰트)
- 검색 결과 선택 시 Records에 매칭 행이 없어도 Preview에 메시지 직접 표시
- **TextBox 드래그 선택 색상 강화**: `InputSelectionBrush`를 `#BFDBFE` → `#60A5FA`로 변경하여 가시성 향상
- **필터 규칙 Enable/Disable 표시 개선**: 텍스트 라벨 → 배지 스타일. ON=진한 초록 배경(`#16A34A`)+흰 텍스트, OFF=회색 텍스트
- **More 메뉴/검색 힌트/필터 액션 버튼 UX 보완**: `More` 진입점에 버튼형 테두리 적용, 메뉴 팝업의 기본 좌측 gutter/라인/빈 체크 공간을 줄인 플랫 메뉴 스타일 적용, `More > Settings > Appearance`는 폰트 ComboBox와 A-/A+ 버튼을 메뉴 안에서 바로 조작하는 기존 접근성 유지, 검색 연산자 힌트를 검색창 바로 아래 작은 안내 텍스트로 이동, `Enable Selected`=초록 테마 / `Disable Selected`=빨강 테마 버튼으로 구분
- `docs/Beta-Distribution-v0.0.1.md`로 베타 배포 형식/포함 파일 기준 정리
- `scripts/Generate-AppIcon.ps1`로 아이콘 재생성 가능
- `scripts/Publish-Beta.ps1`로 `win-x64` self-contained publish + `7z` 패키징 자동화
- `AI/Prompt_Distribution_rule.md`에 원스톱 배포 규칙, Confluence 업로드 절차, 변경이력표 작성 규칙 정리
- `AI/Prompt_Make_filter_set.md`에 코드 기준 필터 셋 JSON 생성 규칙, 필드 사전, 우선순위, 예시 출력 형식 정리
- publish 배포본은 `ALV.exe`가 최상단에 오고 `LogFilter/`, `README.md`, `BETA-README-v0.0.1.md`, `sample-logs/aaos-sample.log`, `tools/README.txt` 구조로 생성

### G. 성능 / 안정화
- 라이브 append를 큐 + 타이머 기반 배치 업데이트로 전환
- 세션 상태 이벤트 스로틀링
- 자동 스크롤 예약 coalescing
- `DataGrid` 가상화 명시
- 파일/데모 로드 시 스풀 저장 flush를 배치화
- 로그/검색 결과 컬렉션을 `RangeObservableCollection`으로 교체하여 대량 로드 비용 감소
- `RangeObservableCollection.ReplaceRange` / `AddRange`에서 `List<T>.AddRange()` 벌크 삽입 사용으로 대량 데이터 UI 갱신 성능 향상
- **파일 로드 시 `ReplaceRecordsAsync`**: LogRowModel 생성 작업을 백그라운드 스레드에서 수행 → UI 프리징 제거
- **파일 progressive 렌더링 이벤트**: `EasyLogEngine.LogRecordsBatchLoaded`로 단일 파일 순차 batch 로드를 UI에 알리고, WPF 컬렉션 갱신은 Dispatcher에서만 수행
- **라이브 spool write batch화**: `RawSpoolStore.AppendRangeAsync` 추가. 실시간 수집 중 1줄마다 disk write하지 않고 상태 업데이트 단위로 batch write/flush
- **라이브 Clear Logs 세대 가드**: live batch 중 `Clear Logs`가 호출되면 Clear 이전 미flush batch를 폐기해 이전 로그가 다시 저장/표시되지 않도록 보장
- **AutoScroll OFF batch append**: live UI append가 AutoScroll 상태와 무관하게 `RangeObservableCollection.AddRange`를 사용해 row-by-row UI 알림 제거
- **Start Live Backfill 옵션화**: 상단 Backfill 콤보에서 Live only / Last 10 sec / Last 30 sec / Last 2 min 선택 가능. 기본은 기존과 동일한 Last 30 sec
- **재연결 lookback 중복 방지**: 30초 lookback은 첫 연결에만 적용하고, 재연결은 `-T 1` 기준으로 시작해 동일 30초 구간 반복 유입 방지
- **필터 hot path 추가 정리**: `ApplyEnabledFilterRules` / `MatchesEnabledFilterRules`의 LINQ를 명시 루프로 교체하고, live append에서도 QuickFilter를 선적용
- **파일 로드 시 diagnostics 스킵**: 대량 파일 로드 경로에서 per-record `DiagnosticHighlighter.Evaluate()` 호출 생략하여 CPU 부하 절감
- **[Phase 1] Snapshot 복사 제거**: 파일 로드 완료 후 `SnapshotView()` (O(1), 복사 없음) 사용. 라이브 세션용 `Snapshot()`은 gate 락 추가로 thread-safe 보장
- **[Phase 1] 인덱스 빌드 스킵**: 파일 로드 시 `InMemoryLogIndexes.AddRange()` 호출 완전 제거 — `LogQueryEngine.Apply()`가 brute-force 필터링을 사용하므로 인덱스는 실제로 미사용
- **[Phase 1] 타임스탬프 Span 파싱**: `$"{year}-{value}"` string interpolation + `DateTimeOffset.TryParseExact` → 수동 digit 파싱으로 zero-allocation 달성
- **[Phase 2] Regex → 수동 Span 파서**: `ThreadtimeLogParser`에서 `Regex.Match()` 2회 호출 → `ReadOnlySpan<char>` 기반 수동 파서로 전면 교체. Regex 할당/매칭 비용 완전 제거
- **[Phase 2] 파일 병렬 로드**: 다중 파일 로드 시 `Parallel.For`로 파일별 병렬 파싱 후 파일 순서대로 스토리지에 병합. 단일 파일/maxLines 지정 시에는 기존 순차 로드 유지
- **[Phase 2] RawLine 제거**: `LogRecord`에서 `RawLine` 필드 삭제 → 레코드당 메모리 ~40-50% 감소. Export/spool 기록은 `ReconstructRawLine()` 메서드로 필요 시 재구성. 텍스트 검색/진단은 `Message` + `Tag` 필드 기반으로 전환
- **[Phase 2+] LogRowModel 생성 병렬화**: 5만 건 이상일 때 `Parallel.For`로 LogRowModel 배열 구축. 필터 규칙 없으면 완전 병렬, 있으면 frozen Brush 캐시 기반 병렬 처리
- **[Phase 2+] Timestamp 수동 포맷**: `DateTimeOffset.ToString("MM-dd HH:mm:ss.fff")` → `string.Create(18, ...)` + digit 직접 쓰기로 per-record string 할당 최소화
- **[Phase 2+] Level 문자열 캐싱**: `LogLevel.ToString()` 호출 → enum 값 7개를 `string[]`로 미리 캐시하여 zero-allocation
- **[Phase 2+] _visibleRowIds 백그라운드 구축**: UI 스레드에서 HashSet 순회 → 백그라운드에서 구축 후 참조 교체
- **인코딩 감지 강화**: `DetectFileEncoding()` strict UTF-8 → CP949 → EUC-KR fallback 체인. BOM 우선 검사
- **ADB stdout UTF-8 명시**: `StandardOutputEncoding = Encoding.UTF8` 설정으로 한글 ADB 로그 깨짐 방지
- `adb` 없음 / no devices / unauthorized / offline 메시지 개선
- 앱 시작 예외를 `%TEMP%\EasyLog\logs\app-startup.log`에 기록
- 배포본 내부 `tools\adb.exe`, `tools\7z.exe`를 우선 탐색하도록 경로 해석 개선
- FilterEditorWindow 오버레이 클릭 처리에서 `Run` 같은 비-Visual 원본도 안전하게 부모 탐색하도록 보강
- `tests/EasyLog.UiTests` 정식 WPF UI 테스트 프로젝트 추가
- 컬럼 순서 영속화 / FilterEditorWindow `Run` 원본 처리 안전성을 `dotnet test`로 회귀 검증 가능하게 승격
- 기존 `artifacts/temp/ColumnOrderHarness` 임시 하네스는 정식 테스트 편입 후 제거

### G-1. 성능 개선 검토 메모 (2026-05-25)
- **다중 파일 Open Log 속도**: 현재 파일별 병렬 파싱 후 전체 병합 + `Timestamp` 전체 정렬 + RowId 재할당 구조. 다음 병목 후보는 `merged.Sort(...)` O(N log N), 병합 리스트 추가 메모리, 파일별 `List<LogRecord>` 보유. 개선 후보는 파일별 정렬 전제 시 k-way merge, 안정 정렬 키 추가, 파싱 중 RowId 부여 지연.
- **Open Log/Live 중 필터 적용 속도**: 현재 QuickFilter와 Filter Rule 모두 전체 스캔 기반. `LogQueryEngine.Matches()`의 단일 term fallback이 레코드마다 작은 배열을 만들 수 있고, Filter Rule이 많으면 `rules × records` 반복. 개선 후보는 `FilterQuery` 정규화 시점에 term/pid 컬렉션 확정, enabled rule compiled predicate 캐시, level/tag/pid 사전 인덱스의 실제 재활용.
- **실시간 로깅 버벅임**: live 수집은 storage batch화되어 있으나 `LogRecordAppended` 이벤트는 레코드마다 발생하고 ViewModel에서 레코드마다 필터 매칭 후 큐잉. 개선 후보는 엔진→UI live 이벤트도 batch화하고 UI flush 주기/배치 크기를 동적으로 조정.
- **실시간 중 필터 변경 버벅임**: 필터 변경 시 live snapshot 복사 + 전체 필터 + `Records.ReplaceRange`가 한 번에 발생. 개선 후보는 필터 적용 세대 번호/cancellation, live append 일시 버퍼링 후 최신 필터만 반영, chunked ReplaceRange 또는 점진 렌더링.
- **로그 위치 점프 속도**: `ScrollIntoView`는 가상화 상태에서 유효하지만 선택/포커스와 분리되어 있으면 사용자 체감이 나쁨. 이번 세션에서 `End` 키는 마지막 행 선택 + `CurrentItem` 갱신 + row focus까지 이동하도록 보강. 추가 개선 후보는 RowId→visible index 맵 유지로 검색 결과 이동 시 `FirstOrDefault` 선형 탐색 제거.

### G-2. 획기적 성능 개선 5대 항목 (수정안, 2026-05-25)

> 1차 초안의 5대 항목을 코드/시행착오와 대조해 정정한 최종 버전. 각 항목에 "이미 적용된 것 / 정정 / 잔여 작업 / 충돌 위험"을 분리해 적었다. 적용 순서는 Phase 1 → Phase 2 → Phase 3.

#### 1순위. 라이브 append 엔진→UI **이벤트** batch화 (Phase 1)
- **대상**: 실시간 로깅 버벅임, 실시간 중 필터 변경 버벅임
- **이미 적용된 것**:
  - storage write batch화 (`LiveStatusUpdateRecordInterval` 단위 `AppendRangeAsync`)
  - UI append batch화: ViewModel의 `FlushPendingLiveRecords`가 timer + `LiveUiBatchSize`/`LiveUiCatchupBatchSize`로 이미 batch flush 수행
  - AutoScroll OFF 상태도 `Records.AddRange` 사용
  - Clear Logs 세대 가드 (`_clearGeneration`)
- **초안 정정**:
  - "UI flush도 batch 단위로 유지" 는 이미 적용 상태. **실제 잔여 병목은 두 곳뿐**:
    1. `EasyLogEngine.RunLiveSessionAsync`에서 `LogRecordAppended?.Invoke(...)`가 per-record로 호출 → 이벤트 dispatch + ViewModel `OnLogRecordAppended` 진입이 라인당 1회
    2. `OnLogRecordAppended`에서 per-record로 `_engine.MatchesFilter` + `MatchesEnabledFilterRules` 호출 → 필터 매칭 함수 호출 자체가 N회
- **잔여 작업**:
  - `LogRecordsAppendedBatch` (또는 `LogRecordsLiveAppended`) 이벤트 추가. payload는 `IReadOnlyList<LogRecord>` 1개 batch (storage flush 주기와 동일 batch)
  - ViewModel은 batch 내 레코드를 순회해 활성 `FilterQuery` + Filter Rule을 1회 매칭, 통과한 것만 `_pendingLiveRecords`에 enqueue
  - 기존 `LogRecordAppended` 단건 이벤트는 호환 유지하되 ViewModel은 batch 이벤트만 구독
- **과거 시행착오 / 충돌 위험**:
  - 과거 "라이브 세션 시작 시 UI 프리즈" 사례에서 **WPF DataGrid와 충돌하는 범위 Add 알림을 Reset으로 교정**한 적 있음 → 본 작업은 *UI 컬렉션 알림*을 바꾸는 것이 아니라 *엔진 이벤트*만 batch화하므로 해당 이슈 재발 가능성 없음
  - `_clearGeneration` 세대 가드: batch 이벤트 발행 시점에 `Volatile.Read(ref _clearGeneration)` 확인 후 발행해야 ViewModel에서 Clear 직후 batch가 재유입되지 않음
  - `_isFilterEditorOpen` 라이브 flush 억제와 충돌 없음 (큐잉 단계는 유지, flush만 억제하는 기존 구조 그대로)
  - `IsLiveAppendPaused` 분기 유지: batch 핸들러에서도 paused면 `_queuedPausedAppendCount` 누계만 갱신
- **추천도**: 매우 높음 (체감 1순위)

#### 2순위. `FilterQuery` 사전 정규화 + Quick/Rule **단일 pass** (Phase 2)
- **대상**: Open Log 후 필터 적용, live append 필터 매칭, 필터 변경 후 refresh
- **이미 적용된 것**:
  - `ApplyEnabledFilterRules` / `MatchesEnabledFilterRules` LINQ→for 변환
  - `LogQueryEngine.Matches()` HashSet 제거, delegate 할당 제거
  - `InvalidateEnabledFilterRulesCache` (활성 규칙 캐시)
  - 파일 로드 시 QuickFilter+Rule 최종 결과 1회 계산 후 `ReplaceRecordsAsync` 1회 (중복 렌더링 제거)
- **초안 정정**:
  - "compiled predicate" 표현은 IL emit 같은 인상을 주는데 실제로는 **정규화된 즉치 구조체**로 충분. 추가 컴파일 단계 불필요
  - "단일 term fallback이 매 레코드마다 작은 배열을 만들 수 있다"는 G-1 추정은 `LogQueryEngine.Matches()` 실코드 재확인 후 정확히 수정 (현재 hot path는 G-1에 적어둔 형태대로 정정)
- **잔여 작업**:
  - `BuildCurrentFilterQuery()` 호출 시점마다 `ParseSearchTerms`로 term 배열 재생성 → ViewModel에서 QuickFilter 텍스트 변경 시에만 재계산해 캐시
  - QuickFilter 결과와 enabled Filter Rules 매칭을 같은 루프에서 처리(`MatchesFilter` + `MatchesEnabledFilterRules`를 합친 single-pass helper)
  - 활성 규칙 0개 + QuickFilter empty → **fast path 분기**로 즉시 통과
- **과거 시행착오 / 충돌 위험**:
  - **구조화 필터 우선 원칙(Plan §5-3) 유지 필수**: level mask → tag/pid 사전 컬렉션 → text contains 순서로 가지치기. 순서 바꾸면 large-tag 로그에서 오히려 느려짐 (과거 검증된 원칙)
  - **인덱스 미사용 결정 유지**: `InMemoryLogIndexes`는 이미 제거됨 (H 섹션). 본 작업에서 다시 인덱스를 끌어들이지 말 것 — 과거 "인덱스 빌드 스킵" 결정과 충돌 가능
  - include/exclude/OR term/level 조합 정합성은 TC로 보강 필요 (검색 결과 동등성 회귀 테스트)
- **추천도**: 매우 높음

#### 3순위. 필터 적용 **cancellation + generation + coalescing** (Phase 1)
- **대상**: 실시간 중 필터 변경 버벅임, 필터 반복 변경 시 반응성
- **이미 적용된 것**:
  - 검색에는 동일 패턴이 이미 적용·검증됨 (Prompt_main §11: `_searchCts`, `_searchCts == cts` IsBusy 가드, Dispose 정리)
  - OFF 상태 필터 편집 시 `RefreshRecords` 생략 (Prompt_main §10) — 무의미한 재적용은 이미 차단
  - `_isFilterEditorOpen` 동안 live UI flush 억제
- **초안 정정**:
  - "filter 적용 cancellation"은 **검색에서 이미 검증된 패턴의 이식**으로 표현 정정. 신규 설계가 아니라 *기존 안정 패턴 재사용*
  - "debounce/coalescing"은 사용자가 슬라이더처럼 빠르게 바꾸는 경우에만 의미 있음. 현재 UX는 팝업 편집 후 OK/Apply이므로 **debounce보다는 cancellation + generation이 본질**. debounce는 후속 검토로 강등
- **잔여 작업**:
  - `_filterApplyCts` 추가 + `_filterApplyGeneration` 증가식 채택
  - `RefreshRecordsFromEngineAsync`가 generation을 캡처해 UI 반영 직전 비교 (`current == captured`일 때만 ReplaceRange)
  - IsBusy 해제는 검색과 동일하게 "활성 작업만 해제"
- **과거 시행착오 / 충돌 위험**:
  - **`_isFilterEditorOpen` 라이브 flush 억제와 동시 작동 검증 필요**: 팝업이 열려있는 동안에는 cancellation이 일어나도 flush는 막혀있으므로 이중 차단 상태가 됨. 팝업 close 시 두 가드가 같은 시점에 풀려야 함 (이미 그렇게 동작하지만 TC 추가 권장)
  - 검색 cancellation에서 이미 발견한 함정: 취소된 작업이 `IsBusy=false`로 활성 작업 상태를 덮어쓰지 않도록 `_filterApplyCts == cts` 비교 필수
- **추천도**: 매우 높음

#### 4순위. 다중 파일 로드 **k-way merge** 또는 전체 sort 회피 (Phase 3)
- **대상**: 다중 파일 Open Log 속도
- **이미 적용된 것**:
  - 파일별 병렬 파싱
  - **파싱 중 RowId 부여 지연** (placeholder 0 → 최종 정렬 후 1-based 순차 할당) — TC#14 해결로 적용 완료
  - `(Timestamp, fileIndex, lineIndex)` 3단 안정 정렬 키 — TC#14 해결의 핵심
- **초안 정정**:
  - "RowId는 파싱 중 부여하지 않고 최종 merge 후 순차 부여" 는 **이미 적용된 상태**. 본 항목에 잔여 작업으로 포함하지 말 것
  - "각 파일이 시간순이라는 전제를 활용" — **logcat은 buffer를 섞어 출력할 수 있어 단일 파일 내에서도 비단조일 수 있음**. 따라서 무조건 k-way merge로 가면 안 되고, 다음 분기 채택:
    1. 파일별 파싱 종료 후 **파일 내부 stable sort** (대부분 거의 정렬 상태이므로 TimSort 류가 빠름)
    2. 모든 파일을 `(Timestamp, lineIndex)`로 정렬된 stream으로 보고 `(Timestamp, fileIndex, lineIndex)` 기준 k-way merge
- **잔여 작업**:
  - `LoadFilesParallelCore`에서 merge 단계 교체: `List.Sort` (O(N log N)) → 파일 내부 정렬 + k-way merge (O(N log K))
  - k-way merge는 `PriorityQueue<(int fileIdx, int recordIdx), (DateTimeOffset, int)>` 활용
- **과거 시행착오 / 충돌 위험**:
  - **TC#14 회귀 위험**: tie-breaker `(Timestamp, fileIndex, lineIndex)` 3단 비교가 깨지면 동일 ms 로그 raw 순서가 다시 섞임. k-way merge의 우선순위 키에 반드시 동일 3단 키 포함
  - **append-only 저장 원칙(Plan §5-2)** 은 다중 파일 정렬에 한해 이미 명시적 예외 — 본 작업도 같은 예외 범위 내, 신규 위반 없음
  - 단일 파일/`maxLines` 경로는 기존 순차 로드 유지 (분기 그대로)
- **추천도**: 높음 (수백만 줄급 다중 파일에서 효과 큼, 그 미만은 차이 미미)

#### 5순위. `RowId → LogRowModel` 맵으로 점프 O(1)화 (Phase 3)
- **대상**: 검색 결과 → 메인 로그 이동, 위치 점프
- **이미 적용된 것**:
  - G-1 메모에 후보로 명시됨
  - `_visibleRowIds` HashSet은 *visible set 검사*용으로 이미 존재 (값→bool)
- **초안 정정**:
  - 사용자가 제시한 `Dictionary<long, LogRowModel>` 또는 `Dictionary<long, int>` 중, **`Dictionary<long, LogRowModel>` 채택 권장**: 인덱스 기반은 ReplaceRange 후 Records.IndexOf 호출 부담이 남기 때문
  - 이름 충돌 방지: 기존 `_visibleRowIds` 와 분리된 `_visibleRowsByRowId` 로 명명
- **잔여 작업**:
  - `ReplaceRecordsAsync` / `AppendProgressiveFileRecordsAsync` / `FlushPendingLiveRecords` 세 경로에서 동시 갱신
  - 검색 결과 클릭 핸들러의 `Records.FirstOrDefault(x => x.RowId == ...)` → `_visibleRowsByRowId.TryGetValue`
  - 필터 적용으로 visible set이 줄어들면 맵 재구축 (ReplaceRange 시점에 한 번)
- **과거 시행착오 / 충돌 위험**:
  - 메모리 추가: long+ref 약 24B × N. 100만 행 ≈ 24MB. 메모리 최적화(H 섹션) 노력 대비 수용 가능
  - **`_visibleRowIds`와 `_visibleRowsByRowId` 동기화 누락 위험**: 두 컬렉션을 항상 같은 트랜잭션에서 갱신하도록 helper 메서드로 일원화 권장
  - 충돌 없음 (기존 결정과 모순되는 부분 없음)
- **추천도**: 높음 (구현 범위 작고 안정)

#### 적용 순서 (정정)
- **Phase 1 (즉시)**: 1순위(live batch 이벤트) + 3순위(필터 cancellation/generation) — 실시간 체감 영향이 가장 큼, 검색에서 검증된 패턴 재사용
- **Phase 2**: 2순위(filter normalize + single-pass) — Open Log/live/필터변경 공통 가속
- **Phase 3**: 4순위(k-way merge) + 5순위(RowId 맵) — 특정 시나리오 고효율 개선

### G-3. 획기적 성능 개선 5대 항목 — 적용 완료 (2026-05-25)

5대 항목 모두 적용 + 검증 완료. Debug/Release 빌드 경고 0/오류 0, UI 테스트 3/3 통과, smoke 테스트 통과.

#### 1순위. 라이브 append 엔진→UI 이벤트 batch화 (Phase 1) ✅
- `EasyLogEngine.LogRecordsLiveAppended` (신규 batch 이벤트) 추가 — payload `IReadOnlyList<LogRecord>` 1 batch (storage flush 주기와 동일).
- `RunLiveSessionAsync` 핫 루프의 per-record `LogRecordAppended?.Invoke(...)` 제거.
- `FlushLiveStorageBatchAsync` 가 storage `AppendRangeAsync` 직후 batch 이벤트를 1회 dispatch. 호환용 per-record `LogRecordAppended` 도 같은 지점에서 batch 1회 루프로 발행(이전엔 hot 루프 라인당 1회 → 이젠 flush당 N회 batch 루프).
- `_clearGeneration` / `IsLiveAppendPaused` / `_isFilterEditorOpen` 가드 그대로.
- ViewModel `OnLogRecordAppended` → `OnLogRecordsLiveAppended` 로 전환. batch 1회 루프에서 quickFilter + rule을 매칭, 필터 빈 경우 fast path로 즉시 enqueue.
- 신규 파일: `src/EasyLog.Engine/LogRecordsLiveAppendedEventArgs.cs`.

#### 2순위. FilterQuery 정규화 + Quick/Rule 단일 pass (Phase 2) ✅
- `LogQueryEngine.Matches()` 에서 `EnumerateTerms` / `EnumeratePids` 가 매 호출 작은 배열을 할당하던 fallback 경로 제거. 다중 term 컬렉션 / 단일 legacy term 분기를 inline 처리. 데드 코드 헬퍼 2개 삭제.
- `MainWindowViewModel.ApplyCombinedFilters` 신규 — QuickFilter + Filter Rules 를 단일 pass로 처리. 둘 다 비면 입력 리스트를 그대로 반환(fast path).
- `RefreshRecordsFromEngineAsync` 가 `_engine.ApplyFilter` + `ApplyEnabledFilterRules` 2단 호출 → `ApplyCombinedFilters` 1회 호출로 단축. 중간 `baseRecords` 리스트 할당 제거.

#### 3순위. 필터 적용 cancellation + generation (Phase 1) ✅
- `_filterApplyCts` 필드 추가, `RefreshRecordsFromEngineAsync` 진입 시 이전 작업 cancel + dispose, 새 CTS 등록.
- 외부 ct가 있으면 linked source 로 합성, 없으면 standalone.
- `OperationCanceledException` 은 supersede 된 작업으로 silent 처리.
- finally 에서 `ReferenceEquals(_filterApplyCts, cts)` 확인 후에만 `IsBusy = false` 해제 (검색에서 검증된 활성 작업만 해제 패턴).
- UI mutation (`ReplaceRecords`) 직전에도 동일 비교로 generation guard. 새로운 task 가 시작됐다면 mutation 생략.
- `Dispose`에서 `_filterApplyCts` 정리.

#### 4순위. 다중 파일 로드 k-way merge (Phase 3) ✅
- `EasyLogEngine.LoadFilesParallelCore` 의 merge 단계 교체:
  - 이전: 모든 파일을 단일 `merged` 리스트에 concat → `List.Sort` (unstable introsort, O(N log N)).
  - 신규: 파일별 in-place `List.Sort` (각 파일은 거의 정렬 상태) → `PriorityQueue<(fileIdx, recIdx), (Timestamp, fileIdx)>` 기반 k-way merge (O(N log K)).
- tie-break: `(Timestamp, fileIdx)`. fileIdx 만으로 충분 — 각 파일이 사전 정렬되어 enqueue 순서가 monotonic 이므로 동일 파일 내 ordering 은 자연히 보존.
- 파일별 리스트는 소진되는 즉시 `perFileResults[fileIdx] = null` 로 해제, peak memory 감소.
- 단일 파일 / `maxLines` 지정 경로는 기존 순차 로드 유지 (분기 그대로).

#### 5순위. RowId → LogRowModel 맵 (Phase 3) ✅
- `Dictionary<long, LogRowModel> _visibleRowsByRowId` 신규 — `_visibleRowIds` HashSet 과 lockstep 유지.
- 동기화 지점:
  - `Records.Clear()` 호출 4 곳 (`OnAppLoadedAsync`, `LoadFilesAsync` progressive 분기, `StartLiveSessionCoreAsync`, `ClearLogs`) 에서 `_visibleRowsByRowId.Clear()` 추가.
  - `ReplaceRecords` / `ReplaceRecordsAsync` 빌드 루프에서 dict 도 같이 채움 (백그라운드 스레드 빌드 → atomic field swap).
  - `AppendProgressiveFileRecordsAsync` / `FlushPendingLiveRecords` 추가 경로에서 dict 갱신.
- `SelectedSearchResult` setter 의 `Records.FirstOrDefault(x => x.RowId == ...)` 선형 탐색 → `_visibleRowsByRowId.TryGetValue` O(1) 로 교체. 100만 행에서 점프당 최대 100만 비교 → 1회 해시 lookup.
- 메모리 증가: long+ref ≈ 24B × N. 100만 행 ≈ 24MB. H 섹션 메모리 최적화 누적 여유 안에서 수용 가능. **VirtualLogList 와 달리 on-demand 생성이 아닌 단순 lookup**이므로 §I 롤백 사례와 충돌 없음.

#### 후보에서 보조 자료로 강등
- LogQueryEngine `Matches()` 작은 할당 제거 → 2순위 작업에 흡수
- live UI flush batch size 동적 조정 → 이미 `LiveUiBatchSize`/`LiveUiCatchupBatchSize`로 동적 분기 적용됨. 추가 작업 우선순위 낮음
- ScrollIntoView 호출 coalescing → 이미 일부 적용. 5순위 이후 보조
- "필터 변경 debounce" → 현재 UX(팝업 OK/Apply)에서 효과 작음. cancellation/generation만으로 충분

### H. 메모리 최적화 (v0.0.5)
- **Tag 문자열 인터닝**: `ThreadtimeLogParser`에 `Dictionary<string, string>` 인턴 풀 추가. 동일 태그가 수십만 건 반복될 때 1개의 string 인스턴스만 유지 → 대용량 로그에서 수십 MB 절감
- **LogRowModel → LogRecord 참조 기반 경량 클래스 전환**: `sealed record`(문자열 복사) → `sealed class`(LogRecord 참조). Tag/Message/Level은 LogRecord에서 직접 반환, Timestamp/Pid/Tid는 on-demand 포맷. DataGrid 가상화로 실제 화면에 보이는 ~50행만 getter 호출 → 레코드당 object 1개 + Brush 2개만 추가 할당
- **SearchResultRowModel도 동일 패턴**: LogRecord 참조 기반으로 전환
- **Pid/Tid 문자열 캐시**: 0~65535 범위의 int→string을 static 배열로 미리 캐시. per-access `int.ToString()` 할당 제거
- **LogQueryEngine.Matches() HashSet 제거**: `query.Levels.ToHashSet()` 가 매 레코드(50만 건)마다 호출 → 단순 foreach 루프로 교체. 50만 건 기준 50만 개 HashSet 할당 완전 제거
- **LINQ delegate 할당 제거**: `Any(term => ...)` 패턴 → 명시적 foreach 루프. 필터링/검색 핫패스에서 delegate 객체 할당 제거
- **ApplyEnabledFilterRules LINQ→for 루프**: `.Where(...).ToArray()` → pre-allocated `List<LogRecord>` + for 루프. 중간 배열 할당 절감
- **InMemoryLogIndexes 라이브 세션 빌드 제거**: 인덱스가 실제 쿼리에 사용되지 않음을 확인. 라이브/데모 세션에서 `_indexes.Add()` 호출 완전 제거 → 3개 Dictionary의 List<long> 메모리 절감
- **GetSnapshotView() Engine API 추가**: 라이브 세션이 아닐 때 `_storage.SnapshotView()` (O(1), 복사 없음) 사용. 기존 `Snapshot()`은 `_records.ToArray()` 풀카피 → 수십 MB 임시 배열 할당 제거
- **RecordCount 프로퍼티 추가**: `_engine.GetSnapshot().Count` → `_engine.RecordCount` (배열 복사 없이 카운트만 조회)

### I. VirtualLogList (시도 → 롤백)
- `VirtualLogList` 클래스를 구현하여 DataGrid의 on-demand LogRowModel 생성을 시도했으나, 오히려 성능이 저하되어 사용하지 않음
- 현재는 기존 `RangeObservableCollection<LogRowModel>` + 배경 스레드 병렬 생성(`ReplaceRecordsAsync`) 방식 유지
- `VirtualLogList.cs` 파일은 코드베이스에 잔류하지만 ViewModel에서 참조하지 않음
- **필터 편집 중 라이브 flush 억제**: `_isFilterEditorOpen` 플래그 추가. FilterEditorWindow가 열려 있으면 `FlushPendingLiveRecords`를 건너뛰어 팝업 내 텍스트 입력/버튼 클릭 지연 해소
- **필터 팝업 테두리 강화**: FilterEditorWindow의 DialogCard에 `BorderBrush="#4B5563" BorderThickness="1.5"` + `DropShadowEffect` 추가. 팝업이 배경과 명확히 구분됨

---

## 4. 최근 문제와 해결 경험

### 0) 다중 파일 로드 시 동일 ms 타임스탬프 로그 순서 뒤섞임 (2026-06-10)
원인:
- `EasyLogEngine.LoadFilesParallelCore`에서 파일별 정렬에 `List<T>.Sort`(불안정 introsort)를 사용
- 같은 파일 내 ms 단위까지 동일한 타임스탬프 레코드의 원래 라인 순서가 보장되지 않아 뒤죽박죽됨
- (단일 파일 경로 `LoadFilesSequentialCore`는 정렬이 없어 영향 없음 → 다중 파일/폴더 열기에서만 재현)

해결:
- 파일별 정렬 비교자를 `(Timestamp, RowId)` 2차 키로 변경
- `RowId`는 파싱 시 파일 라인 순서대로 단조 증가하므로 동률 시 원래 라인 순서 복원
- K-way 병합은 파일 내 레코드를 순서대로 enqueue하므로 추가 변경 불필요

검증:
- 회귀 테스트 `TestMultiFileSameTimestampOrderingAsync` 추가 (동일 ms 20행 × 2파일 순서 검증)
- Engine 빌드 성공, smoke + 기능 테스트 통과

교훈:
- `List<T>.Sort` / `Array.Sort` / `OrderBy`가 아닌 일반 정렬은 불안정하므로, 표시 순서가 중요한 데이터는 항상 안정적 2차 키(시퀀스/RowId)를 명시할 것

### 1) 앱 시작 실패
원인:
- WPF 읽기 전용 바인딩 및 startup 흐름 문제

해결:
- `App.xaml.cs`에 전역 예외 처리와 명시적 startup 경로 추가
- 바인딩 문제 수정

교훈:
- WPF는 시작 예외가 조용히 죽는 경우가 있으므로 초기에는 startup log가 필수

### 2) 라이브 세션 시작 시 UI 프리즈
원인:
- 로그 1건마다 `Dispatcher` / `ObservableCollection.Add` / 상태 이벤트 / 스크롤이 동시에 발생

해결:
- 큐 + 타이머 기반 배치 append
- 상태 이벤트 스로틀링
- 스크롤 예약 coalescing
- WPF `DataGrid`와 충돌하는 범위 Add 알림을 `Reset` 알림으로 교정
- live 저장소 flush를 상태 갱신 시점 기준 배치화해 런타임 버벅임 완화
- 활성 필터 규칙 계산을 캐시해 실시간 로깅 중 팝업 입력/버튼 지연 완화

교훈:
- 라이브 로그 도구는 기능보다 **배치 업데이트 구조**가 먼저 안정적이어야 함

### 3) `adb` smoke test timeout
원인:
- `logcat -T 1` vs 기존 backlog 처리 정책이 테스트와 앱 요구를 동시에 만족하지 못함

해결:
- 엔진에 lookback timestamp 경로 추가
- 앱은 최근 30초 로그만 백필 후 live tail 시작
- 테스트는 tail-start 유지

교훈:
- 실제 제품 요구와 테스트 요구가 다르면 백필 범위를 분리해 다뤄야 함

### 4) `Open Log`가 지나치게 느림
원인:
- 스풀 파일에 줄마다 flush
- UI 컬렉션에 로그를 하나씩 추가

해결:
- 배치 flush 추가
- `RangeObservableCollection.ReplaceRange`로 UI 갱신 단순화

교훈:
- 대용량 로그는 저장소 flush 정책과 UI 컬렉션 정책이 체감 성능을 결정함

### 5) 필터 세트 저장 팝업이 안 뜸
원인:
- 필터 규칙 변경 시 자동 저장 성격으로 흘러 dirty 상태가 사라짐

해결:
- 규칙 변경은 dirty만 남기고 파일 저장은 `Save Set / Save Set As`에만 위임

교훈:
- 편집 상태와 파일 저장 상태를 분리해야 사용자 의도가 보존됨

### 6) 필터/검색/라이브 UX 혼동
원인:
- 검색과 필터가 역할이 다른데 화면에서 비슷하게 보였음

해결:
- 상단은 검색, 좌측은 필터로 역할 분리
- 검색 결과 패널과 필터 패널의 위치/용어 정리

교훈:
- 로그 도구는 “검색”과 “필터”의 역할 차이를 UI에서 명확히 드러내야 함

### 7) 검색 하이라이트 클릭 시 UI 스레드 예외
원인:
- 메인/검색 결과 그리드의 하이라이트 텍스트는 `Run` 인라인으로 구성되는데,
- 마우스 클릭 시 `e.OriginalSource`가 `Run`이 될 수 있음
- `MainWindow.xaml.cs`의 조상 탐색 헬퍼가 `VisualTreeHelper.GetParent()`만 사용해서 `Run` 같은 비-Visual 객체에서 예외가 발생함

해결:
- 조상 탐색 로직이 `Visual/Visual3D`뿐 아니라 `FrameworkContentElement`, `ContentElement`, `LogicalTree`도 따라가도록 수정
- 결과적으로 하이라이트된 텍스트 클릭 시 `Run`이 들어와도 안전하게 `DataGridRow`/`CheckBox`/`ListBoxItem` 조상 탐색 가능

교훈:
- WPF 입력 이벤트의 `OriginalSource`는 항상 `Visual`이 아니므로, `Run`/`TextElement`를 포함한 부모 탐색을 고려해야 함

### 8) 컬럼 너비는 저장되는데 컬럼 순서는 유지되지 않음
원인:
- 기존 UI 설정에는 컬럼 너비만 저장되고 `DisplayIndex`는 저장/복원하지 않았음

해결:
- `UiPreferences`에 `ColumnDisplayIndexes` 저장소 추가
- `MainWindowViewModel`에 컬럼 순서 preference 로드/저장 API 추가
- `MainWindow.xaml.cs`에서 `DataGrid.ColumnReordered`를 추적하고 종료 시 최종 스냅샷 저장
- 앱 시작 시 저장된 `DisplayIndex`를 먼저 복원한 뒤 컬럼 너비를 적용하도록 순서 조정
- 정식 `EasyLog.UiTests`에서 메인 로그/검색 결과 컬럼 순서 저장·복원 성공 검증

교훈:
- `DataGrid` 레이아웃 영속화는 너비만이 아니라 `DisplayIndex`까지 함께 다뤄야 사용자가 기대하는 "컬럼 상태 복원"이 완성됨

### 9) Ctrl+C가 검색창/프리뷰에서 동작하지 않음
원인:
- `PreviewKeyDown`에서 Ctrl+C를 Window 레벨에서 가로채면서, TextBox 포커스 여부를 명확히 구분하지 못함
- `CopySelectedRows`가 `LogRowModel`만 처리해서 `SearchResultsGrid`에서 Ctrl+C 복사 시 아무것도 복사되지 않음

해결:
- Ctrl+C 핸들러에서 `Keyboard.FocusedElement`가 `TextBox/PasswordBox/RichTextBox`이면 즉시 `return`하여 네이티브 복사 동작 보장
- `CopySelectedRows`가 `SearchResultRowModel`도 처리하도록 확장
- Preview TextBox에서 텍스트 선택 + Ctrl+C 복사가 정상 동작

교훈:
- WPF `PreviewKeyDown`에서 전역 단축키를 가로챌 때는, 현재 포커스된 컨트롤이 자체 키보드 처리를 해야 하는 경우를 반드시 먼저 제외해야 함

### 10) 한글(Korean) 로그가 깨져서 표시됨
원인:
- `EasyLogEngine.DetectFileEncoding()`이 64KB 샘플만 읽어 non-ASCII 문자가 파일 후반부에 있으면 감지 실패
- UTF-8 멀티바이트 시퀀스가 샘플 경계에서 잘리면 유효한 UTF-8도 오탐 발생
- `Encoding.GetEncoding("cp949")` 문자열 이름이 .NET 런타임에 따라 인식 안 되는 경우 존재
- `FileLogCollector`의 인코딩 감지에서 `l.Contains('?')` 조건이 정상 `?` 문자를 오탐
- `AdbLogCollector`에서 `StandardOutputEncoding` 미설정 → 한국어 Windows에서 adb stdout이 CP949로 해석

해결:
- **멀티-포지션 샘플링**: 파일 시작(64KB), 중간(64KB), 끝(64KB) 3곳에서 샘플을 읽어 non-ASCII 바이트 포함 여부 확인
- **UTF-8 경계 트림**: `TrimToUtf8Boundary()`로 샘플 끝의 불완전 멀티바이트 시퀀스 제거 후 strict UTF-8 검사
- **코드페이지 번호 사용**: `Encoding.GetEncoding("cp949")` → `Encoding.GetEncoding(949)`, EUC-KR → `51949`
- **strict UTF-8**: `UTF8Encoding(false, throwOnInvalidBytes: true)` + `DecoderFallbackException` 방식
- **ADB UTF-8 명시**: `StandardOutputEncoding = Encoding.UTF8` 설정
- **인코딩 감지 자동 테스트 5종**: UTF-8 한글, CP949 한글, CP949 64KB 이후 한글, UTF-8 경계 한글, 순수 ASCII

교훈:
- 인코딩 감지 샘플은 파일 시작부만으로 부족함 — 대형 로그 파일에서 한글이 후반에만 나타나는 경우가 흔함
- UTF-8 멀티바이트 경계 잘림 처리가 없으면 유효한 UTF-8도 비-UTF-8로 오탐됨
- `.NET`에서 `"cp949"` 문자열은 런타임/플랫폼에 따라 해석 안 될 수 있으므로 코드페이지 번호(`949`) 사용이 안전함

### 11) 다중 zip 파일 내 로그 로드 실패 (v0.0.5)
원인:
- 압축 파일 추출 후 유효한 로그 파일 식별 로직이 일부 zip 구조에서 로그 파일을 인식하지 못함

해결:
- ArchiveExtractor에서 추출된 파일 경로 탐색 로직 개선
- 유효한 로그 파일 확장자 필터링 강화

교훈:
- 다양한 압축 구조/파일명 패턴에 대한 방어적 처리가 필요

### 12) PageUp/PageDown이 1행만 이동하는 문제
원인:
- DataGrid에 `ScrollViewer.CanContentScroll="True"` (가상화 모드)가 설정되어 있으면 `ScrollViewer.ViewportHeight`가 **픽셀이 아닌 논리적 단위(보이는 아이템 수)**를 반환함
- 이 값을 픽셀 기반 행 높이(24px)로 나누면 `25 / 24.0 ≈ 1`이 되어 페이지 이동이 1칸만 됨

해결:
- `scrollViewer.CanContentScroll`을 확인하여, true이면 `ViewportHeight`를 그대로 페이지 크기(행 수)로 사용
- false(픽셀 스크롤)일 때만 실제 렌더링된 행 높이로 나눠서 계산

교훈:
- **WPF ScrollViewer의 논리적 스크롤 vs 물리적 스크롤 차이를 반드시 인지해야 함**: `CanContentScroll=True`이면 `ViewportHeight`, `ExtentHeight`, `VerticalOffset` 모두 아이템 단위(논리적)로 바뀜. 픽셀 단위로 가정하고 나누면 항상 틀린 결과가 나옴
- 가상화된 DataGrid에서 스크롤 관련 계산을 할 때는 항상 `CanContentScroll` 분기가 필요함

### 13) 검색창에 항상 포커스가 남아 키보드 네비게이션 불가
원인:
- DataGrid 행을 클릭해도 키보드 포커스가 검색 TextBox에 남아있어, `PreviewKeyDown`의 `if (Keyboard.FocusedElement is TextBox) return;` 가드에 의해 모든 네비게이션 키가 무시됨

해결:
- `OnLogGridPreviewMouseLeftButtonDown`에서 DataGrid 행 클릭 시 `grid.Focus()`로 키보드 포커스를 그리드로 이동
- SearchResultsGrid에도 동일 핸들러 추가
- ESC 키로 어디서든 포커스 해제 기능 추가

교훈:
- WPF DataGrid는 기본적으로 행 클릭 시 키보드 포커스를 자동으로 가져오지 않을 수 있음 — 특히 `DataGridCell.Focusable`이 false인 경우
- 전역 키보드 핸들러에서 포커스 기반 가드를 사용할 때는, 포커스가 의도대로 이동하는지 반드시 검증해야 함

### 14) 동일 ms 타임스탬프 로그의 raw 순서 뒤섞임 (다중 파일 로드)
원인:
- `LoadFilesParallelCore`에서 `Parallel.For`로 파일을 병렬 파싱하면서 RowId를 `Interlocked.Increment`로 할당 → 동일 파일 내 레코드의 RowId가 비연속적(interleaved)
- `merged.OrderBy(r => r.Timestamp)` stable sort는 동일 타임스탬프 레코드를 RowId 삽입 순서로 유지하지만, 그 RowId 자체가 파일 내 원본 순서와 무관하게 할당되어 있었음
- 결과적으로 동일 ms 타임스탬프를 가진 로그들이 뒤죽박죽 섞여 표시됨

해결:
- Phase 1: RowId를 `0`으로 플레이스홀더 할당 (Interlocked interleave 제거)
- Phase 2: `(Record, fileIndex, lineIndex)` 튜플로 태깅한 뒤 `(Timestamp, fileIndex, lineIndex)` 3단 비교로 정렬
  - 1차: Timestamp 순서 (파일 간 시간순 보장)
  - 2차: fileIndex (동일 ts에서 파일 순서 유지)
  - 3차: lineIndex (동일 ts + 동일 파일에서 raw data 순서 유지)
- RowId는 최종 정렬 후 1-based 순차 할당

교훈:
- 병렬 파싱에서 전역 카운터(`Interlocked`)로 RowId를 할당하면 파일 내 순서 정보가 유실됨
- 다중 파일 merge sort 시 원본 위치 정보(fileIndex, lineIndex)를 명시적으로 보존해야 파일 내 raw 순서를 지킬 수 있음

---

## 5. 현재 남아 있는 핵심 과제

### 다음 단계 (v1.1.0+)
1. 시간 범위 필터 추가 여부 검토
2. 검색 하이라이트 색상/가독성 미세조정 여부 검토
3. 최근 열었던 로그 / 최근 필터 세트 목록 추가 여부 검토
4. 필터 세트 포맷 버전 관리 검토
5. 장치 없음 / adb 오류 UX 정리

---

## 6. 현재 제약 / 주의사항

- 앱을 실행한 상태에서 다시 빌드하면 DLL 잠금으로 빌드가 실패할 수 있음
- 실장치 검증은 현재 장치 연결 상태에 의존함
- `Start Live`는 현재 **최근 30초 로그만 백필**하므로, 더 오래된 부팅 초기 로그는 기본 시작 동작에 자동 포함되지 않음
- `Open Log`는 구조적으로 빨라졌지만, 매우 큰 파일에서는 progress / cancel이 없어서 체감 개선 여지가 남아 있음
- 필터 세트는 JSON 파일 단위로 관리되며, 최근 파일 기능은 아직 없음
- Confluence 배포 시 다음부터는 변경이력표(Date / Version / Changes / File)까지 함께 갱신해야 함
- v0.0.4까지 설정 폴더는 `ALV`였으나 v0.0.5부터 `LogPilot`으로 변경됨 — 기존 설정 마이그레이션은 자동 수행하지 않음

---

### 15) 로그 필터 적용 일관성 문제 (v1.0.2)
원인:
- `StartLiveAsync`에서 `_activeFilterQuery = FilterQuery.Empty`로 QuickFilter를 리셋
- `OnLogRecordAppended`에서 Filter Rule만 체크하고 QuickFilter(`_activeFilterQuery`)는 미적용
- `LoadDemoAsync`, `LoadSampleAsync`에서 필터 Rule 활성 상태와 무관하게 `ReplaceRecords`만 호출
- `RefreshRecordsFromEngineAsync`에서 QuickFilter를 전혀 참조하지 않음

해결:
- `StartLiveAsync`: `BuildCurrentFilterQuery()`로 현재 QuickFilter 조건 보존
- `OnLogRecordAppended`: `_activeFilterQuery.IsEmpty` 체크 후 QuickFilter 매칭 추가
- `LoadDemoAsync`/`LoadSampleAsync`: 로드 후 `enabledRules > 0`이면 `RefreshRecordsFromEngineAsync` 호출
- `RefreshRecordsFromEngineAsync`: `_activeFilterQuery`가 비어있지 않으면 `_engine.ApplyFilter()`로 1차 필터 후 Filter Rule 적용
- `FilterQuery`에 `IsEmpty` 속성 추가

교훈:
- 두 종류의 필터(QuickFilter / Filter Rule)가 있을 때, 모든 로그 경로(파일/라이브/데모)에서 일관되게 적용해야 사용자 혼란 방지

### 16) ADB 장치 대기 및 자동 재연결 (v1.0.2)
원인:
- 장치 재부팅 후 Start Live 시 장치가 아직 미연결 → 사용자가 반복 클릭해야 함
- 라이브 세션 중 장치 재부팅 → ADB 연결 끊김 → 세션 Faulted 종료 → 수동 재시작 필요

해결 (ViewModel - Wait for Device):
- `StartLiveAsync`에서 장치 없으면 `WaitForDeviceAndStartLiveAsync` 진입
- 2초 간격 장치 폴링, 60초 타임아웃, Stop 버튼으로 취소 가능
- `IsWaitingForDevice` 속성으로 UI 상태 관리

해결 (Engine - Auto-Reconnect):
- `RunLiveSessionAsync`를 while 루프로 변경 — ADB 오류 발생 시 3초 대기 후 장치 폴링(2초 간격)
- 장치 ready 감지 시 logcat 자동 재시작, 기존 로그 보존
- `OperationCanceledException`(Stop Live)만 루프 탈출
- `WaitForDeviceReadyAsync` 메서드로 특정 시리얼 장치 대기

교훈:
- 엔진 레벨 재연결과 ViewModel 레벨 장치 대기를 분리하면 관심사가 명확해짐
- `SessionRunState`를 `Running`으로 유지하면서 재연결 대기하면 UI 상태 전환이 자연스러움

### 17) Confluence 첨부 업로드 실패 (v1.0.2 배포)
원인:
- 동일 파일명 첨부가 이미 존재할 때 POST로 신규 업로드 → `400 Bad Request`
- PUT 메서드로 대용량(46MB) 파일 전송 → `502 Bad Gateway` (프록시 타임아웃)

해결:
- 기존 첨부 ID를 조회(`GET /child/attachment?filename=...`)
- `POST /child/attachment/{ATTACHMENT_ID}/data` 엔드포인트로 파일 업데이트
- `Prompt_Distribution_rule.md`에 신규/업데이트 분기 절차를 명시적으로 기록

교훈:
- Confluence REST API에서 동일 파일명 첨부 업데이트는 `/data` 서브 엔드포인트를 사용해야 함
- 대용량 바이너리는 PUT보다 POST가 안정적

### 18) 로그 로드 / 실시간 로깅 지연 개선 (2026-05-25)
원인:
- 파일 로드 후 활성 Filter Rule이 있으면 전체 로그 렌더링 후 필터 결과를 다시 렌더링하는 중복 경로 존재
- 일반 단일 파일도 전체 파싱이 끝난 뒤에야 첫 화면이 표시됨
- live 수집은 flush는 배치였지만 spool 파일 write 자체는 1줄마다 수행
- AutoScroll OFF 상태에서 live UI append가 row-by-row `Records.Add()`로 수행됨
- Start Live의 최근 30초 백필이 고정이라 로그 많은 장치에서 시작 지연 가능

해결:
- 파일 로드 시 QuickFilter/Filter Rule 최종 결과를 먼저 계산한 뒤 `ReplaceRecordsAsync`를 1회만 수행
- 단일 일반 파일 전체 로드에서 `LogRecordsBatchLoaded` 이벤트 기반 progressive 렌더링 적용
- `RawSpoolStore.AppendRangeAsync` 추가 및 live 수집 storage flush를 상태 업데이트 단위 batch로 전환
- live Clear Logs 중 미flush batch 역삽입 방지를 위해 `_clearGeneration` 세대 가드 추가
- AutoScroll OFF도 `AddRange`로 batch 반영
- Start Live Backfill 옵션(Live only / Last 10 sec / Last 30 sec / Last 2 min) 추가, 기본은 기존 Last 30 sec 유지
- 재연결 시 첫 연결 lookback을 반복하지 않도록 보정

검증:
- `dotnet build .\EasyLog.sln -c Debug` 성공 (경고 0, 오류 0)
- `dotnet run --project .\tests\EasyLog.Tests\EasyLog.Tests.csproj -c Debug` 성공
- `dotnet test .\tests\EasyLog.UiTests\EasyLog.UiTests.csproj -c Debug` 성공 (3/3)
- `dotnet build .\EasyLog.sln -c Release` 성공 (경고 0, 오류 0)

교훈:
- 대용량 로그 UX는 총 처리 시간뿐 아니라 “첫 batch 표시 시간”과 “중복 렌더링 제거”가 체감 성능을 좌우함
- live batch화는 Clear/Stop 같은 세션 경계와 함께 설계해야 이전 batch 재유입을 막을 수 있음

### 19) 상단 툴바 메뉴 과밀 문제 개선 (2026-05-25)
원인:
- 로그 로드, 장치 제어, live 옵션, export/clear, 폰트 설정, About 등이 한 줄에 모두 노출되어 창 폭이 작아지면 잘림 발생
- Backfill/Font/About처럼 설정성 기능과 Start/Stop처럼 즉시 실행 기능이 같은 우선순위로 보였음

해결:
- 상단에는 핵심 작업 흐름(`Open Log`, Device 선택/감지, `Start Live`, `Stop Live`, `Pause`, `Auto`)만 유지
- `Start Live` 옆 드롭다운 메뉴에서 Live only / Last 10 sec / Last 30 sec / Last 2 min 즉시 시작 지원
- `Export Logs`는 `Open Log` 옆에 외부 노출하고, `Clear Logs...`만 `More` 메뉴로 이동
- 독립 `Settings` 메뉴는 제거하고 Appearance(Font Family, Log Font Size), Live Defaults(Backfill 기본값), About을 `More` 하위로 통합
- 기존 Command와 ViewModel 상태를 재사용해 동작 변경 범위를 최소화

추가 UX 보완:
- Backfill 메뉴의 현재 선택값을 `MenuItem.IsCheckable` 체크 표시로 시각화
- Device refresh 버튼을 Device 콤보박스 오른쪽에 붙이고 `More` 메뉴의 중복 Discover 항목 제거
- 공통 Button pressed 상태에 색상 변화와 눌림 이펙트 추가
- Open Log 옵션 팝업을 LogPilot 라이트 카드 + 보라색 accent 스타일로 변경
- Search 연산자 힌트를 보라색 accent의 단일 라이트 힌트 칩으로 변경해 테마 일체감 개선

검증:
- `dotnet build .\EasyLog.sln -c Debug` 성공 (경고 0, 오류 0)
- `dotnet test .\tests\EasyLog.UiTests\EasyLog.UiTests.csproj -c Debug` 성공 (3/3)
- `dotnet run --project .\tests\EasyLog.Tests\EasyLog.Tests.csproj -c Debug` 성공
- `dotnet build .\EasyLog.sln -c Release` 성공 (경고 0, 오류 0)

교훈:
- 로그 뷰어 상단은 “분석 흐름의 즉시 액션” 위주로 유지하고, 저빈도 작업/설정은 메뉴화해야 작은 창에서도 핵심 조작성이 유지됨

### 20) More 메뉴/검색 힌트/필터 액션 버튼 UX 보완 (2026-05-25)
원인:
- `More` 메뉴가 다른 버튼보다 테두리/형태가 약해 잘 보이지 않았음
- `More > Settings > Appearance`에 ComboBox와 작은 버튼이 직접 들어가 메뉴 트리 안 배치가 어색하고 일관성이 떨어졌음
- 검색 연산자 힌트가 검색 행의 버튼 사이에 칩 형태로 있어 공간을 차지하고 시선 흐름을 방해했음
- `Enable Selected` / `Disable Selected` 버튼이 동일 스타일이라 활성/비활성 액션 구분이 약했음

해결:
- `More` 메뉴를 라이트 배경 + 1px 테두리 + 둥근 모서리 Border로 감싸 버튼형 진입점으로 표시
- Appearance 메뉴를 `Font Family` / `Log Font Size` 하위 일반 메뉴 항목으로 재구성하고, 폰트 선택용 `SetAppFontFamilyCommand` 추가
- `Operators: | OR & AND` 안내를 검색창 바로 아래 작은 텍스트로 이동
- `SuccessButtonStyle` / `DangerButtonStyle`을 추가해 `Enable Selected`는 초록 테마, `Disable Selected`는 빨강 테마로 표시

검증:
- `dotnet build .\EasyLog.sln -c Debug` 성공 (경고 0, 오류 0)
- `dotnet test .\tests\EasyLog.UiTests\EasyLog.UiTests.csproj -c Debug` 성공 (3/3)
- `dotnet run --project .\tests\EasyLog.Tests\EasyLog.Tests.csproj -c Debug` 성공

교훈:
- 메뉴에는 입력 컨트롤을 직접 끼워 넣기보다 일반 메뉴 항목/하위 메뉴로 정리해야 앱의 버튼·메뉴 UX가 일관되게 보임
- 위험/상태 전환 액션은 색상 테마를 분리하면 사용자가 결과를 더 빠르게 예측할 수 있음

---

## 7. 다음 세션 진입점

1. 로그 로드/실시간 로깅 성능 개선 및 상단 툴바 재구성 적용 완료 — 실사용 대형 로그/실장치에서 체감 시간과 작은 창 UI 확인 권장
2. 시간 범위 필터, 최근 파일 목록 등 v1.1.0 기능 검토
3. 배포 전 ADB 실장치 smoke(`--adb-smoke`)와 수동 TC(FL-17~FL-18, ADB-10~ADB-12, UI-14~UI-20) 확인

---

## 8. 참고 메모

- 현재 작업 기준의 사실은 **코드와 최근 빌드/실행 결과**를 우선합니다.
- 오래된 계획, 과거 제안, 폐기된 방향은 `Context_Main_Plan_and_goal.md`에서 정리했습니다.
- 이 문서는 다음 세션이 바로 이어받을 수 있도록 **현재 상태와 실제 경험** 중심으로 유지합니다.
