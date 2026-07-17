# LogPilot - AAOS Log Viewer 베타 배포 가이드 (`v0.0.5`)

## v0.0.5 주요 변경 내용

### 신규 기능
- **앱 이름 변경**: ALV → **LogPilot** 으로 리브랜딩
- **검색 히스토리**: 이전 검색어 최대 10개 자동 저장, 드롭다운 탐색(`↑`/`↓`), 개별 삭제(`✕`), 앱 재시작 시 유지
- **필터 적용 로딩 오버레이**: 필터 ON/OFF, 추가/삭제/편집 시 "Applying filters..." 로딩 표시
- **검색 로딩 오버레이**: 검색 수행 중 "Searching logs..." 로딩 표시
- **압축 파일 로그 로드 개선**: 다중 zip 파일 내 로그 로드 실패 문제 해결

### 성능 최적화
- **VirtualLogList**: Records 컬렉션 가상화 — 50만 건 기준 LogRowModel 50만 개 → 화면 ~50개만 생성
- **메모리 최적화**: Tag 인터닝, LogRowModel 경량화(LogRecord 참조 기반), Snapshot 복사 제거, LINQ 핫패스 → for 루프 전환

## 권장 배포 형식

`LogPilot`은 Windows 전용 WPF 앱이며, 베타 사용자 배포는 **`win-x64` Self-contained 바이너리 배포 + 7z 압축**입니다.

## `v0.0.5` 배포본에 포함된 것

### 반드시 포함
- 패키지 최상단의 publish 결과물 전체
  - `LogPilot.exe`
  - 어플리케이션 DLL들
  - .NET self-contained runtime 파일들
  - WPF/WinDesktop 관련 native/runtime 파일들
- `LogFilter/` 폴더
- `README.md`
- `BETA-README-v0.0.5.md`

### 선택 포함
- `sample-logs/aaos-sample.log`
- `tools/` 폴더 (adb.exe, 7z.exe 등 외부 도구 안내)

## 최종 패키지 구조 예시

```text
LogPilot-AAOS-Log-Viewer-v0.0.5-beta-win-x64.7z
└─ LogPilot-AAOS-Log-Viewer-v0.0.5-beta-win-x64/
   ├─ LogPilot.exe
   ├─ LogPilot.dll
   ├─ EasyLog.Engine.dll
   ├─ ... publish 전체 파일 ...
   ├─ LogFilter/
   ├─ tools/
   │  └─ README.txt
   ├─ sample-logs/
   │  └─ aaos-sample.log
   ├─ README.md
   └─ BETA-README-v0.0.5.md
```

