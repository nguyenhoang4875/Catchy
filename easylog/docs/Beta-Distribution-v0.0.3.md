# ALV - AAOS Log Viewer 베타 배포 가이드 (`v0.0.3`)

## v0.0.3 긴급 패치 내용

- **Ctrl+C 로그 복사 버그 수정**: DataGrid에서 Ctrl+C로 복사 시 Tag, PID, Message 열이 누락되던 문제 해결
- **우클릭 복사 메뉴 추가**: 로그 그리드 및 검색 결과 그리드에서 마우스 우클릭 시 복사 메뉴 제공
  - `Copy Rows (Ctrl+C)`: 선택된 행 전체를 탭 구분 형식으로 복사
  - `Copy Message Only`: 선택된 행의 Message만 복사

## 권장 배포 형식

`ALV`는 Windows 전용 WPF 앱이며, 베타 사용자 배포는 **`win-x64` Self-contained 바이너리 배포 + 7z 압축**의 간이 안전합니다.

## `v0.0.3` 배포본에 포함된 것

### 반드시 포함
- 패키지 최상단의 publish 결과물 전체
  - `ALV.exe`
  - 어플리케이션 DLL들
  - .NET self-contained runtime 파일들
  - WPF/WinDesktop 관련 native/runtime 파일들
- `LogFilter/` 폴더
- `README.md`
- `BETA-README-v0.0.3.md`

### 선택 포함
- `sample-logs/aaos-sample.log`
- `tools/` 폴더 (adb.exe, 7z.exe 등 외부 도구 안내)

## 최종 패키지 구조 예시

```text
ALV-AAOS-Log-Viewer-v0.0.3-beta-win-x64.7z
└─ ALV-AAOS-Log-Viewer-v0.0.3-beta-win-x64/
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
   └─ BETA-README-v0.0.3.md
```

