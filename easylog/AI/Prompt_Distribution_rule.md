# LogPilot - AAOS Log Viewer 배포 운영 규칙

## 1. 목적

이 문서는 사용자가 다음과 같이 요청했을 때,

- `원스톱으로 v0.0.2 버전 배포해주세요.`
- `베타버전 배포해주세요.`
- `배포파일 준비하고 컨플에 올려주세요.`

추가 설명 없이도 **빌드 → 검증 → 패키징 → Confluence 업로드 → 변경이력표 갱신 → 완료 보고**까지 일관되게 수행하기 위한 실행 규칙입니다.

핵심 목적은 아래 4가지입니다.

1. 배포 산출물 이름/구조를 항상 동일한 규칙으로 유지할 것
2. 사용자가 바로 실행할 수 있는 형태로 패키징할 것
3. Confluence 페이지에 파일 + 한/영 사용자 안내 + 변경이력표까지 한 번에 반영할 것
4. 다음 버전(`v0.0.2`, `v0.0.3` ...)에서도 문서만 보고 그대로 반복 가능할 것

---

## 2. 배포 트리거 문구 해석 규칙

사용자가 아래와 유사하게 말하면, 단순 파일 생성이 아니라 **전체 배포 플로우**를 수행합니다.

- `v0.0.2 버전 배포해주세요`
- `원스톱으로 배포해주세요`
- `배포파일 준비해주세요`
- `컨플에 올려주세요`

이 경우 기본 수행 범위는 다음과 같습니다.

1. 버전 문자열 확인 및 반영 범위 점검
2. 빌드/테스트/앱 시작 검증
3. self-contained Windows 배포본 생성
4. `.7z` 패키지 생성
5. Confluence 첨부 업로드
6. Confluence 본문(한글/영문 안내) 갱신
7. **변경이력표(Date / Version / Changes / File) 갱신**
8. 최종 경로/링크/검증 결과 보고

사용자가 별도 지시하지 않으면 기본 배포 유형은 **beta**로 간주합니다.

---

## 3. 현재 기준 배포 기본값

### 앱 이름
- 표시명: `LogPilot - AAOS Log Viewer`
- 실행 파일명: `LogPilot.exe`

### 기본 배포 형식
- 플랫폼: `Windows`
- 런타임: `win-x64`
- 방식: `self-contained`
- 압축 형식: `.7z`

### 현재 기본 패키지명 규칙
- 형식: `LogPilot-AAOS-Log-Viewer-vX.Y.Z-win-x64.7z`
- 예시: `LogPilot-AAOS-Log-Viewer-v1.0.0-win-x64.7z`

### 현재 기본 산출물 위치
- 압축 파일: `artifacts\LogPilot-AAOS-Log-Viewer-vX.Y.Z-win-x64.7z`
- 스테이징 폴더: `artifacts\package\LogPilot-AAOS-Log-Viewer-vX.Y.Z-win-x64\`

---

## 4. 배포 전 반드시 확인할 것

### 1) 코드/문서 상태
다음 문서를 먼저 확인합니다.

- `AI/Prompt_main.md`
- `AI/Context_Main_Plan_and_goal.md`
- `AI/Context_Progress.md`

### 2) 배포 관련 파일 확인
- `src/EasyLog.App/EasyLog.App.csproj`
- `scripts/Publish-Beta.ps1`
- `scripts/Generate-AppIcon.ps1`
- `scripts/Update-Confluence-BetaPage.py`
- `docs/Beta-Distribution-v0.0.1.md` 또는 최신 버전 문서

### 3) Confluence 인증 환경 변수 확인
다음 환경 변수가 설정되어 있어야 합니다.

- `CONFLUENCE_USER`
- `CONFLUENCE_PASS`

없으면 업로드/본문 수정은 중단하고, 사용자에게 인증 미설정 사실을 명확히 보고합니다.

---

## 5. 배포 버전 변경 시 반영 규칙

사용자가 `v0.0.2` 배포를 요청하면, 최소한 아래 항목을 점검합니다.

### 반드시 점검할 항목
1. `src/EasyLog.App/EasyLog.App.csproj`
   - `Version`
   - `AssemblyVersion`
   - `FileVersion`
   - `InformationalVersion`

2. 배포 문서 파일명
   - `docs/Beta-Distribution-v0.0.1.md` → 필요 시 새 버전 문서 생성 또는 갱신

3. 배포 스크립트 기본값
   - `scripts/Publish-Beta.ps1`의 `Version` 기본값

4. Confluence 본문 업로드 스크립트 상수
   - `ATTACHMENT_NAME`
   - `SECTION_HEADER`

5. README / AI 문서 내 버전 표기

### 권장 원칙
- 버전이 바뀌면 **문서/스크립트/패키지명/본문 링크가 서로 어긋나지 않게** 한 번에 수정합니다.
- 버전 문자열은 `v0.0.2`처럼 `v` 포함 형식으로 통일합니다.

---

## 6. 빌드 / 테스트 / 패키징 절차

### 1) 사전 정리
- 실행 중인 앱 종료
- 이전 동일 버전 패키지 산출물 존재 여부 확인
- 필요 시 이전 동일 이름 산출물 삭제

### 2) 기본 검증 명령
```bat
taskkill /IM ALV.exe /F >nul 2>&1
taskkill /IM EasyLog.App.exe /F >nul 2>&1
cd /d D:\work\EasyLog
dotnet build .\EasyLog.sln -c Debug --no-restore
dotnet run --project .\tests\EasyLog.Tests\EasyLog.Tests.csproj -c Debug --no-build
```

### 3) 앱 시작 검증
```bat
taskkill /IM ALV.exe /F >nul 2>&1
cd /d D:\work\EasyLog
powershell.exe -NoProfile -Command "$log = Join-Path $env:TEMP 'ALV\logs\app-startup.log'; if (Test-Path $log) { Remove-Item $log -Force }; $p = Start-Process -FilePath '.\src\EasyLog.App\bin\Debug\net8.0-windows\ALV.exe' -PassThru; Start-Sleep -Seconds 4; $p.Refresh(); Write-Output ('HasExited=' + $p.HasExited); Write-Output ('LogExists=' + (Test-Path $log)); if (-not $p.HasExited) { Stop-Process -Id $p.Id }"
```

### 4) 배포 패키지 생성
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Publish-Beta.ps1 -Version v0.0.2
```

### 5) 패키지 검증 포인트
- `.7z` 생성 성공
- `ALV.exe`가 패키지 **최상단**에 존재
- `LogFilter/` 폴더 존재
- 기본 `filters.json`은 **포함하지 않음**
- `README.md`, `BETA-README-버전.md`, `tools/README.txt`, `sample-logs/aaos-sample.log` 포함 여부 확인

---

## 7. 포함 / 제외 파일 규칙

### 반드시 포함
- 최상단 publish 결과물 전체
  - `LogPilot.exe`
  - `LogPilot.dll`
  - 런타임 DLL / `.json` / native 파일
- `LogFilter/` 폴더 (빈 폴더 허용)
- `README.md`
- `BETA-README-버전.md`
- `tools/README.txt`
- `sample-logs/aaos-sample.log`

### 기본적으로 포함하지 않음
- `filters.json` 같은 기본 저장 필터 파일
- 대용량 개인 로그 파일
  - 예: `sample-logs/sample_log.log`
- `obj/`, 개발용 `bin/Debug`, 테스트 출력물
- 소스코드 전체
- IDE 설정 파일

### 외부 도구 포함 정책
기본 배포본은 `tools/README.txt`만 포함합니다.

필요 시 아래 파일을 `tools/`에 추가 동봉할 수 있습니다.
- ADB: `adb.exe`, `AdbWinApi.dll`, `AdbWinUsbApi.dll`
- 7-Zip: `7z.exe`, `7z.dll`

사용자 요청이 없으면 외부 도구 실행 파일은 기본적으로 리포지토리에 넣지 않습니다.

---

## 8. Confluence 업로드 규칙

### 현재 대상 페이지

배포 시 **두 페이지 모두**에 동일하게 첨부 업로드 + 본문 업데이트를 수행합니다.

| # | 페이지 | 페이지 ID | URL |
|---|--------|-----------|-----|
| 1 | AAOS Log Viewer (AVN APP Unit) | `3598372101` | `http://collab.lge.com/main/pages/viewpage.action?pageId=3598372101` |
| 2 | LogPilot - Android Log Viewer (CCIC XXIV) | `3648829450` | `http://collab.lge.com/main/spaces/CCICXXIV/pages/3648829450/LogPilot+-+Android+Log+Viewer` |

### 업로드 순서 (각 페이지에 대해 반복)
1. 배포 파일 첨부 업로드
2. 페이지 본문 업데이트
3. 변경이력표 업데이트
4. 링크/첨부/버전 번호 재검증

### 첨부 업로드 방식
현재 기준으로 REST API 업로드를 사용합니다.

#### 신규 첨부 (파일명이 해당 페이지에 없을 때)
```bat
curl -s -u "%CONFLUENCE_USER%:%CONFLUENCE_PASS%" -H "X-Atlassian-Token: nocheck" -F "file=@artifacts\LogPilot-AAOS-Log-Viewer-v1.0.2-win-x64.7z" -F "comment=LogPilot v1.0.2 release" "http://collab.lge.com/main/rest/api/content/{PAGE_ID}/child/attachment"
```

#### 기존 첨부 업데이트 (동일 파일명이 이미 존재할 때)
동일 파일명 첨부가 이미 존재하면 `400 Bad Request`가 발생합니다. 이 경우:
1. 먼저 기존 첨부 ID를 조회합니다:
```bat
curl -s -u "%CONFLUENCE_USER%:%CONFLUENCE_PASS%" "http://collab.lge.com/main/rest/api/content/{PAGE_ID}/child/attachment?filename={FILENAME}"
```
2. 응답에서 `"id":"..."` 값을 확인한 후, `/data` 엔드포인트로 파일을 업데이트합니다:
```bat
curl -s -u "%CONFLUENCE_USER%:%CONFLUENCE_PASS%" -H "X-Atlassian-Token: nocheck" -X POST -F "file=@artifacts\{FILENAME}" -F "comment=LogPilot v1.0.2 (updated)" "http://collab.lge.com/main/rest/api/content/{PAGE_ID}/child/attachment/{ATTACHMENT_ID}/data"
```

> **주의**: PUT 메서드로 대용량 파일을 전송하면 502 Bad Gateway가 발생할 수 있습니다. 기존 첨부 업데이트에는 반드시 **POST + `/data` 엔드포인트**를 사용하세요.

### 본문 업데이트 방식
현재는 `scripts/Update-Confluence-BetaPage.py`를 사용합니다.

버전이 바뀌면 최소한 아래 상수를 함께 갱신합니다.
- `ATTACHMENT_NAME`
- `VERSION`

### 본문 업데이트 핵심 규칙

#### 규칙 1: History 테이블 업데이트
- 페이지 상단에 이미 존재하는 `History` 테이블(`<table class="relative-table wrapped">`)에 **새 버전 행을 삽입**합니다.
- 새 행은 헤더 행 바로 다음에 삽입합니다 (최신 버전이 위).
- File 칸에는 Confluence 첨부 링크(`<ac:link><ri:attachment ri:filename="..." /></ac:link>`)를 사용합니다.
- 기존 행은 삭제하지 않습니다 (이력 유지).

#### 규칙 2: 가이드/사용방법은 최신 버전 1벌만 유지
- 한국어/영어 가이드 섹션은 **버전별로 추가하지 않습니다**.
- 항상 **최신 버전의 정보만 유지**하고 기존 버전 가이드를 **교체(replace)** 합니다.
- 섹션 헤더는 버전 번호를 포함하지 않습니다: `ALV Beta Distribution / 배포 안내`
- 스크립트의 마커(`<!-- ALV_BETA_START -->` ~ `<!-- ALV_BETA_END -->`)로 관리 영역을 식별하여 교체합니다.
- 마커가 없으면 기존 버전별 섹션(`<h2>ALV v0.0.X ...`)을 찾아 제거하고 최신 1벌로 교체합니다.

#### 규칙 3: 첨부 파일 참조
- 가이드 내 다운로드 링크와 History 테이블의 File 칸 모두 **최신 배포 파일명**을 참조합니다.
- 이전 버전 첨부 파일은 Confluence에서 삭제하지 않습니다 (History에서 링크 유지).

### 본문에 반드시 포함할 섹션
- 한국어 안내
  - 주요 특징
  - 실행 방법
  - 사용 방법
- English Guide
  - Key Features
  - How to Run
  - How to Use

### README.md 내용 반영 규칙
- Confluence 페이지 본문의 한국어 안내/사용 방법 섹션은 **`README.md`의 내용을 원본으로** 작성합니다.
- 배포 시 `README.md`의 주요 특징, 실행 방법, 사용법, 단축키, 지원 로그 형식 등을 Confluence 본문(한/영 가이드)에 **동기화**합니다.
- `README.md`에 기능이 추가/변경되었으면 Confluence 본문도 함께 갱신합니다.
- Confluence 본문과 `README.md`가 서로 어긋나지 않도록 유지합니다.

### AI 필터 셋 생성 안내 반영 규칙
- `AI/Prompt_Make_filter_set.md` 파일 자체를 각 Confluence 페이지에 **첨부 파일로 업로드**합니다.
- Confluence 본문에는 첨부 파일 다운로드 링크 + 간단한 사용 흐름(4단계)만 안내합니다.
- 상세 규칙은 첨부된 프롬프트 파일 자체에 포함되어 있으므로, 본문에 중복 기재하지 않습니다.

---

## 9. 변경이력표 작성 규칙

다음부터는 배포 시 **반드시 페이지의 변경이력표(Date / Version / Changes / File)를 채웁니다.**

### 규칙
- `Date`: 배포 날짜 (`YY.MM.DD` 또는 페이지 기존 형식 유지)
- `Version`: 배포 버전 (`v0.0.2`)
- `Changes`: 사용자 관점의 핵심 변경 1~3줄 요약
- `File`: 첨부 파일명 또는 링크

### 작성 예시
| Date | Version | Changes | File |
|---|---|---|---|
| 26.03.27 | v0.0.2 | ALV 브랜딩 반영, 검색 하이라이트 개선, 배포 구조 정리 | `ALV-AAOS-Log-Viewer-v0.0.2-beta-win-x64.7z` |

### 변경이력표 작성 원칙
- 개발자 중심 표현보다 사용자 변화 중심으로 쓸 것
- 너무 길게 쓰지 말 것
- 파일 칸은 가능하면 첨부 링크 또는 정확한 파일명으로 채울 것

---

## 10. 배포 완료 보고 템플릿

배포 완료 후에는 아래 항목을 사용자에게 보고합니다.

1. 완료 여부
2. 최종 배포 파일 경로
3. Confluence 업로드 완료 여부
4. 첨부 파일명
5. 본문/변경이력표 업데이트 여부
6. 빌드/테스트/앱 시작 검증 결과
7. 남아 있는 주의사항 (`tools/` 미동봉 등)

예시:

- 배포 완료
- 파일: `D:\work\EasyLog\artifacts\ALV-AAOS-Log-Viewer-v0.0.2-beta-win-x64.7z`
- Confluence 업로드 완료
- 변경이력표 반영 완료
- 빌드/스모크테스트/앱 시작 검증 완료

---

## 11. 금지사항 / 주의사항

### 금지사항
- 인증 정보(`CONFLUENCE_PASS`, 토큰, 쿠키)를 문서/코드/커밋에 남기지 않음
- 대용량 개인 로그 파일을 배포본/커밋에 포함하지 않음
- 검증 없이 배포 완료라고 단정하지 않음
- 버전 문자열이 코드/문서/파일명/Confluence에서 서로 다르게 남지 않도록 함

### 주의사항
- 앱 실행 중에는 DLL 잠금으로 빌드 실패 가능
- `tools/`에 실행 파일이 없으면 ADB live / 7z export는 사용자 환경 설치에 의존함
- `scripts/Update-Confluence-BetaPage.py`는 현재 고정 페이지 ID 기반이므로, 다른 페이지에 올릴 경우 상수 수정이 필요함
- Confluence 본문 수정 후에는 **첨부 링크 / 버전 / 변경이력표**를 반드시 재확인함

---

## 12. v0.0.2 원스톱 배포 실행 요약

다음에 사용자가 `원스톱으로 v0.0.2 버전 배포해주세요.` 라고 말하면 아래 순서로 처리합니다.

1. 버전 문자열을 `v0.0.2` 기준으로 코드/문서/스크립트 갱신
2. 빌드/스모크 테스트/앱 시작 검증
3. `Publish-Beta.ps1 -Version v0.0.2` 실행
4. 새 `.7z` 생성 확인
5. **각 대상 페이지(2개)에 대해 반복:**
   - Confluence 첨부 업로드
   - 한/영 안내 본문 갱신 (`README.md` 내용 기반)
   - **변경이력표 작성**
6. 최종 경로/링크/검증 결과 보고

이 문서는 이후 배포 요청 시 **실행 체크리스트 겸 기준 문서**로 사용합니다.

---

## 릴리스 노트: v1.0.3

- 배포일: 2026.06.10
- 배포 파일명: `LogPilot-AAOS-Log-Viewer-v1.0.3-win-x64.7z`
- 주요 변경사항:
  - 로그 로딩/실시간 성능 개선 (파일 점진적 렌더링, 라이브 배치 append, 단일 패스 필터 + 검색 취소, 다중 파일 K-way 병합)
  - 다중 파일 로드 시 ms 단위까지 동일한 타임스탬프 레코드의 라인 순서 뒤섞임 버그 수정 (`(Timestamp, RowId)` 안정 정렬, 회귀 테스트 추가)
  - About 팝업 텍스트 선택/복사 가능 + 이메일 복사 버튼 추가
  - csproj 버전 업데이트 → `1.0.3`
- Confluence 반영: 두 페이지(3598372101 / 3648829450) 첨부 업로드 + 본문/변경이력표 갱신 완료

---

## 릴리스 노트: v1.0.2

- 배포일: 2026.05.21
- 배포 파일명: `LogPilot-AAOS-Log-Viewer-v1.0.2-win-x64.7z`
- 주요 변경사항:
  - Start Live 시 장치 미연결이면 자동 대기 후 연결 시 라이브 시작 (60초 타임아웃)
  - 라이브 세션 중 ADB 연결 끊김 시 자동 재연결 (기존 로그 보존)
  - 로그 필터 적용 일관성 대폭 개선 (QuickFilter + Filter Rule 통합 파이프라인)
  - 다중 파일 로드 시 동일 타임스탬프 레코드 순서 보존
  - Ctrl+C 복사 순서 수정 및 DataGrid 내장 복사 충돌 방지
  - UI/레이아웃 회귀 수정, `scripts/fix-load-option.ps1` 추가
  - csproj 버전 업데이트 → `1.0.2`

배포 시 주의: Confluence 업로드를 자동으로 수행하려면 `CONFLUENCE_USER` / `CONFLUENCE_PASS` 환경 변수가 설정되어 있어야 합니다. 없을 경우 업로드 없이 패키지 산출물만 생성합니다.

