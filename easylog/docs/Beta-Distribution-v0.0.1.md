# ALV - AAOS Log Viewer 베타 배포 가이드 (`v0.0.1`)

## 권장 배포 형식

`ALV`는 Windows 전용 WPF 앱이므로, 베타 사용자 배포는 **`win-x64` Self-contained 폴더 배포 + 7z 압축**이 가장 안전합니다.

권장 이유:
- 대상 PC에 `.NET 8 Desktop Runtime` 설치 여부를 신경 쓰지 않아도 됨
- 압축 해제 후 바로 실행 가능
- 사내 베타/파일 전달 방식에 적합
- DLL, native runtime 파일, 설정 파일을 한 번에 그대로 전달 가능

## `v0.0.1` 배포본에 포함할 것

### 반드시 포함
- 패키지 최상단의 publish 결과물 전체
  - `ALV.exe`
  - 애플리케이션 DLL들
  - .NET self-contained runtime 파일들
  - WPF/WinDesktop 관련 native/runtime 파일들
  - publish가 생성한 `.json`, `.dll`, `.pdb` 등 전체
- `LogFilter/` 폴더
- `README.md`
- `BETA-README-v0.0.1.md`

### 선택 포함
- `sample-logs/aaos-sample.log`
  - 사용자가 앱 실행 직후 파일 열기 동작을 빠르게 시험 가능
- `tools/` 폴더
  - `adb.exe`, `AdbWinApi.dll`, `AdbWinUsbApi.dll`
  - `7z.exe`, `7z.dll`
  - 외부 도구를 앱 폴더 내부에서 바로 쓰고 싶을 때만 포함

## 포함하지 않아야 할 것

- 소스 코드 전체
- `obj/`
- 개발용 `bin/Debug`
- 테스트 프로젝트 출력물
- 대용량 개인 샘플 로그(`sample_log.log` 같은 로컬 대형 파일)
- IDE/개인 설정 파일

## 외부 도구 의존성 정리

### 1) ADB live 수집
다음 둘 중 하나면 동작합니다.
- 시스템에 Android SDK Platform Tools가 설치되어 있음
- 또는 배포본 `tools/` 폴더에 아래 파일이 있음
  - `adb.exe`
  - `AdbWinApi.dll`
  - `AdbWinUsbApi.dll`

### 2) 7z 압축 export
다음 둘 중 하나면 동작합니다.
- 시스템에 7-Zip이 설치되어 있음
- 또는 배포본 `tools/` 폴더에 아래 파일이 있음
  - `7z.exe`
  - `7z.dll`

## 최종 패키지 구조 예시

```text
ALV-AAOS-Log-Viewer-v0.0.1-beta-win-x64.7z
└─ ALV-AAOS-Log-Viewer-v0.0.1-beta-win-x64/
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
   └─ BETA-README-v0.0.1.md
```

## 이번 버전 권장 배포 판단

`v0.0.1` 베타는 아래 기준이 가장 적절합니다.
- **배포 형식**: `Self-contained`
- **대상 런타임**: `win-x64`
- **압축 형식**: `.7z`
- **전달 단위**: `ALV.exe`가 최상단에 보이는 publish 결과물 전체 폴더를 통째로 전달

부분 파일만 골라서 전달하면 누락 위험이 크므로, **publish 출력 폴더 전체를 그대로 묶는 방식**을 권장합니다.

