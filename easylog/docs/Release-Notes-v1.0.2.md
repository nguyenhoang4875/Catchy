# LogPilot - AAOS Log Viewer v1.0.2 Release Notes

**릴리즈 날짜**: 2026-05-21  
**배포 파일**: `LogPilot-AAOS-Log-Viewer-v1.0.2-win-x64.7z`  
**플랫폼**: Windows 10+ (x64), self-contained (.NET 8 포함)

---

## 🔧 v1.0.2 — 필터 일관성 강화, ADB 연결 안정성, 버그 수정

---

## 변경 내역

### 1. ADB 장치 연결 대기 및 자동 재연결
- **Start Live 시 장치 자동 대기**: 장치가 아직 연결되지 않은 상태에서 Start Live를 클릭하면, 장치가 연결될 때까지 자동으로 대기 후 라이브 세션을 시작 (60초 타임아웃, Stop으로 취소 가능)
- **ADB 연결 끊김 시 자동 재연결**: 라이브 세션 중 장치 재부팅 등으로 ADB 연결이 끊기면, 기존 로그를 보존한 채 장치가 다시 연결될 때까지 대기 후 자동으로 로그 수집을 재개
- 장치 부팅 중 ADB가 잠깐 붙었다 끊기는 문제에서도 수동 재시작 불필요

### 2. 로그 필터 적용 일관성 대폭 개선
- **라이브 세션에서 QuickFilter(상단 필터) 유지**: 라이브 시작 시 QuickFilter 조건이 리셋되던 문제 수정
- **라이브 실시간 수신 시 QuickFilter 반영**: 실시간 로그 수신 시 Filter Rule뿐 아니라 QuickFilter 조건도 함께 체크
- **Demo/Sample 로드 시 Filter Rule 적용**: 데모·샘플 로그 로드 후에도 활성화된 Filter Rule이 즉시 적용
- **필터 재적용 시 QuickFilter + Filter Rule 통합 적용**: Apply Filter 후 전체 필터 체인이 일관되게 동작

### 2. 다중 파일 로드 시 동일 타임스탬프 레코드 순서 보존
- 여러 로그 파일을 동시에 로드할 때, 동일 밀리초 타임스탬프를 가진 레코드들이 파일 내 원래 순서를 유지하도록 수정
- `(Timestamp, fileIndex, lineIndex)` 3단 정렬로 파일 내 raw data 순서를 정확히 보존

### 3. Ctrl+C 복사 행 순서 수정
- DataGrid에서 여러 행을 선택하고 Ctrl+C로 복사할 때, 선택 순서가 아닌 RowId 순서로 정렬하여 복사
- DataGrid 내장 복사 기능과의 충돌 방지

### 4. UI/레이아웃 안정성 개선
- MainWindow XAML/ViewModel 관련 UI 회귀 문제 수정
- 로드 옵션 관련 안정성 개선

---

## 문의

버그 리포트 및 개선 제안: **sungyeon22.kim@lge.com**

