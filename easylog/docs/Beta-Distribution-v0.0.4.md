# ALV - AAOS Log Viewer 베타 배포 가이드 (`v0.0.4`)

## v0.0.4 주요 변경 내용

### 신규 기능
- **Preview 패널**: 좌측 필터 리스트 하단에 Preview 영역 추가 — 선택한 로그 행의 Message를 바로 확인하고, 드래그 선택 및 Ctrl+C 복사 가능
- **검색 범위 선택**: `Search All Logs (ignore filter)` 체크박스로 필터 적용 여부와 무관하게 전체 로그 검색 가능 (설정 영속화)
- **다양한 로그 포맷 자동 인식**: 연도 접두사(`YYYY-MM-DD`), 마이크로초 타임스탬프, UID 필드, 태그 없는 커널 로그 등 자동 파싱

### 버그 수정 / 개선
- **한글 로그 인코딩 개선**: 멀티-포지션 샘플링 + strict UTF-8 → CP949 → EUC-KR fallback 체인으로 인코딩 자동 감지 강화
- **ADB 한글 로그 깨짐 해결**: `StandardOutputEncoding = UTF8` 명시
- **필터 규칙 ON/OFF 배지 스타일 개선**: 텍스트 → 배지(ON: 초록 배경, OFF: 회색)로 가시성 향상
- **텍스트 드래그 선택 색상 강화**: 가시성 향상

### 성능 최적화
- **수동 Span 파서**: Regex 완전 제거 → `ReadOnlySpan<char>` 기반 zero-allocation 파싱
- **파일 병렬 로드**: 다중 파일 로드 시 `Parallel.For` 병렬 파싱
- **RawLine 제거**: 레코드당 메모리 ~40-50% 감소
- **LogRowModel 생성 병렬화**: 5만 건 이상 대량 로드 시 병렬 처리
- **Timestamp/Level 캐싱**: zero-allocation 문자열 포맷

## 권장 배포 형식

`ALV`는 Windows 전용 WPF 앱이며, 베타 사용자 배포는 **`win-x64` Self-contained 바이너리 배포 + 7z 압축**입니다.

## `v0.0.4` 배포본에 포함된 것

### 반드시 포함
- 패키지 최상단의 publish 결과물 전체
  - `ALV.exe`
  - 어플리케이션 DLL들
  - .NET self-contained runtime 파일들
  - WPF/WinDesktop 관련 native/runtime 파일들
- `LogFilter/` 폴더
- `README.md`
- `BETA-README-v0.0.4.md`

### 선택 포함
- `sample-logs/aaos-sample.log`
- `tools/` 폴더 (adb.exe, 7z.exe 등 외부 도구 안내)

## 최종 패키지 구조 예시

```text
ALV-AAOS-Log-Viewer-v0.0.4-beta-win-x64.7z
└─ ALV-AAOS-Log-Viewer-v0.0.4-beta-win-x64/
   ├─ ALV.exe
   ├─ ALV.dll
   ├─ EasyLog.Engine.dll
   ├─ ... publish 전체 파일 ...
   ├─ LogFilter/
   ├─ tools/
   │  └─ README.txt
   ├─ sample-logs/
   │  └─ aaos-sample.log
   ├─ README.md
   └─ BETA-README-v0.0.4.md
```

