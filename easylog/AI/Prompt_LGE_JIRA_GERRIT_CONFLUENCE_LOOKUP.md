# LGE Jira/Gerrit/Confluence 조회 가이드

> **이 파일의 목적**: AI가 Jira 티켓, Gerrit Change, Confluence 페이지를 자동으로 조회하고 분석하도록 안내합니다.
> **사용법**: 티켓 번호, Change ID, Confluence URL 언급 시 이 파일을 AI에게 첨부하세요.
> **최종 업데이트**: 2026-03-20
> **⚠️ 환경**: 이 가이드는 **Android Studio AI 플러그인** 전용으로 커스텀된 파일입니다.

---

## 🎯 AI에게: 이 가이드를 읽었다면

**당신의 역할**: 사용자가 티켓 번호, Change ID, Confluence 페이지를 언급하면 자동으로 조회하고 요약해주세요.

**중요**: 
- 티켓 번호를 감지하면 즉시 조회 실행
- 웹 접속이 안 되는 경우 SSH/API 사용
- 요약은 간결하고 핵심 정보만 제공
- **터미널 명령 실행 실패 시**: 사용자에게 즉시 아래 사항을 안내하세요.
  - Android Studio 기본 터미널은 **PowerShell**이므로 `curl` 명령이 정상 작동하지 않을 수 있습니다.
  - `Settings → Tools → Terminal` 에서 **Shell path를 `cmd.exe`** 로 변경하고 **`Use classic terminal`** 을 활성화하도록 안내하세요.
  - 설정 변경 후 재시도하면 정상 작동합니다.

---

## 🎫 티켓/Change/Confluence 페이지 조회

**키워드**: "AAUEXPERT", "HKMCPOIP", "HMCCW", "HMCCWQE", "POIPOEM", "CCR", "LAMPAUTO", "Change 번호", "collab", "confluence", Jira 티켓 패턴, Gerrit Change 번호, Confluence 페이지 ID

**자동 인식 패턴**:
- **Jira 티켓**:
  - `HMCCW-XXXXX` : DQA 이슈
  - `HMCCWQE-XXXXX` : QE 이슈
  - `POIPOEM-XXXXX` : OEM 이슈
  - `AAUEXPERT-XXXXX` : AAU 태스크
  - `HKMCPOIP-XXXXX` : 프로젝트 태스크
  - `CCR-XXXXX` : 코드 변경 요청
  - `LAMPAUTO-XXXXX` : CCC 티켓
- **Gerrit Change**: 6자리 숫자 (예: `287788`, `285798`)
- **Gerrit URL**: `http://lamp.lge.com/as/c/...+/CHANGE-NUMBER`
- **Confluence 페이지**: 숫자 10자리 (예: `1593905983`), `collab.lge.com` URL

**AI 액션**:
1. **티켓 번호 감지 시** 자동으로 curl/ssh 명령 실행:
   ```bash
   # Jira 티켓 조회 (기본 정보)
   # ⚠️ 실제 계정/비밀번호를 문서에 직접 쓰지 말고 환경 변수 사용
   curl -s -u "$JIRA_USER:$JIRA_PASS" "http://jira.lge.com/issue/rest/api/2/issue/TICKET-ID" 2>&1 | head -50
   
   # 전체 JSON 파싱
   curl -s -u "$JIRA_USER:$JIRA_PASS" "http://jira.lge.com/issue/rest/api/2/issue/TICKET-ID" 2>&1 | python3 -m json.tool
   
   # Gerrit Change 조회 (SSH 방식 - 권장)
   ssh -p 29418 sungyeon22.kim@lamp.lge.com gerrit query --format=JSON --current-patch-set CHANGE-NUMBER
   
   # Confluence 페이지 조회 (REST API)
   curl -s -u "$CONFLUENCE_USER:$CONFLUENCE_PASS" \
     "http://collab.lge.com/main/rest/api/content/PAGE-ID?expand=body.storage,version,space" \
     | python3 -m json.tool
   ```

2. **정보 요약 제공**:
   - Jira: Summary, Status, Assignee, Description 요약
   - Gerrit: Subject, Owner, Status, Changed Files
   - Confluence: Title, Space, Last Updated, 본문 요약 (HTML → 텍스트)

3. **연관 문서 확인** (필요 시):

**⚠️ Gerrit 웹 접속 주의사항** (2026-03-12 확인):
- Gerrit 웹 UI는 로그인 세션 필요 (AI 도구로 직접 접속 불가)
- fetch_webpage 시도 시 404 Error 발생 → **SSH 방식으로 대체**
- URL에서 Change 번호 추출 후 SSH query 사용 권장

**Gerrit SSH 접속 정보**:
- Host: `http://lamp.lge.com/`
- Port: `29418`
- User: 환경 변수 `$GERRIT_USER` 사용

**Jira API 인증 정보**:
- Base URL: `http://jira.lge.com/issue`
- User: 환경 변수 `$JIRA_USER` 사용
- Password: 환경 변수 `$JIRA_PASS` 사용

**Confluence API 인증 정보**:
- Base URL: `http://collab.lge.com/main`
- User: 환경 변수 `$CONFLUENCE_USER` 사용 (Jira와 동일)
- Password: 환경 변수 `$CONFLUENCE_PASS` 사용 (Jira와 동일)

**⚙️ 초기 설정 필요 시**:
- 환경 변수가 없으면 사용자에게 입력 요청
- 상세 설정 방법은 `PROMPTS_SYSTEM_SETUP_GUIDE.md` 섹션 2.2, 2.3 참조

**사용 예시**:
```
사용자: "HMCCW-33101 이거 뭐야?"
AI 액션:
  1. 티켓 번호 감지 (HMCCW-33101, DQA 이슈)
  2. curl 명령으로 Jira API 호출
  3. JSON 응답 파싱 (summary, status, description 등)
  4. 티켓 정보 요약 제공

사용자: "CCR-36232 변경점 분석해줘"
AI 액션:
  1. 티켓 번호 감지 (CCR-36232)
  2. curl로 티켓 정보 조회
  3. Description에서 변경 파일 및 코드 추출
  4. 연관 커밋 확인 (customfield_27077 등)
  5. 변경점 분석 제공

사용자: "http://lamp.lge.com/review/c/platform/vendor/lge/packages/apps/oem/Car/ccshmi/+/287788 이거 뭐야?"
AI 액션:
  1. URL에서 Change 번호 추출 (287788)
  2. SSH로 Gerrit query 실행 (웹 접속 불가)
  3. 결과 요약:
     - Subject: "[Connect Wide][HMI_APPS][ccshmi] Fix bug..."
     - Owner: vantien.tran
     - Status: NEW
     - Branch: connect_w_release (마스터 브랜치)
     - Changes: +64, -28 lines

사용자: "http://collab.lge.com/main/pages/viewpage.action?pageId=2755983189 이 페이지 내용 보여줘"
AI 액션:
  1. URL에서 페이지 ID 추출 (2755983189)
  2. curl로 Confluence REST API 호출
  3. 결과 요약:
     - Title: "개발현황_Vehicle Care(김성연S)"
     - Space: "현대 Connect Wide(구 P-OIP) Project Home" (CCICXXIV)
     - Last Updated: 2024-12-09 (김성연)
     - 본문: HTML 텍스트 추출 및 요약

사용자: "2755983189 페이지에서 WBS 링크 찾아줘"
AI 액션:
  1. 페이지 ID 감지 (2755983189)
  2. Confluence API로 페이지 내용 조회
  3. HTML 본문에서 WBS 링크 추출
  4. 결과 제공
```

**✅ 실제 검증 완료** (2026-03-20):
- Gerrit SSH 접속 정상 작동
- Change 287788 조회 성공 (connect_w_release 브랜치)
- JSON 파싱 및 정보 추출 확인
- **Jira REST API 접속 정상 작동**
- **HMCCW-33101 (DQA 이슈) 조회 성공**
- **Confluence REST API 접속 정상 작동**
- **Confluence 페이지 2755983189 조회 성공**
- **HTML 본문 파싱 및 데이터 추출 가능**

---

## 🔗 커밋/CCR/CCC 연관 분석 규칙

> **사용자가 커밋 검토 또는 CCR 검토를 요청하면 아래 규칙에 따라 능동적으로 연관 정보를 추적하세요.**

### 연관 관계 구조

```
커밋 (Gerrit Change)
 └── 커밋 메시지에 CCR 티켓 번호 포함
      └── CCR 티켓 (CCR-XXXXX)
           ├── LAMPAUTO-XXX 형태의 CCC 티켓 링크
           └── 커밋 링크 (추가 변경점 확인 가능)
                └── CCC 티켓 (LAMPAUTO-XXXXX)
                     └── Status 확인 필요
```

### 각 단계별 분석 방법

**1. 커밋 (Gerrit Change) 분석 시**
- 커밋 메시지에서 `CCR-XXXXX` 패턴의 티켓 번호 추출
- 해당 CCR 티켓을 Jira API로 조회하여 추가 변경점 확인
- 변경된 파일 목록 (`--files` 옵션) 함께 조회

```bash
# 커밋 조회 (파일 목록 포함)
ssh -p 29418 sungyeon22.kim@lamp.lge.com gerrit query --format=JSON --current-patch-set --files CHANGE-NUMBER
```

**2. CCR 티켓 (CCR-XXXXX) 분석 시**
- CCR 티켓의 `issuelinks` 필드에서 `LAMPAUTO-XXXXX` 형태의 **CCC 티켓** 번호 추출
- CCR description 또는 comment에서 커밋 링크(`lamp.lge.com/review`) 추출
- 연관된 모든 커밋의 변경점 종합

```bash
# CCR 티켓 조회
curl -s -u "$JIRA_USER:$JIRA_PASS" "http://jira.lge.com/issue/rest/api/2/issue/CCR-XXXXX"
```

**3. CCC 티켓 (LAMPAUTO-XXXXX) 분석 시**
- `LAMPAUTO-`로 시작하는 티켓 번호 = **CCC(Component Change Control) 티켓**
- 티켓의 **Status** 파악이 핵심 (Approved / Rejected / In Review 등)
- Status에 따라 해당 변경점의 공식 승인 여부 결정

```bash
# CCC 티켓 조회
curl -s -u "$JIRA_USER:$JIRA_PASS" "http://jira.lge.com/issue/rest/api/2/issue/LAMPAUTO-XXXXX"
```

### 능동적 체크리스트 (커밋/CCR 검토 요청 시)
- [ ] 커밋 메시지에서 CCR 티켓 번호 추출 → CCR 티켓 조회
- [ ] CCR 티켓에서 LAMPAUTO CCC 티켓 번호 추출 → CCC 티켓 Status 확인
- [ ] CCR 티켓에서 연관 커밋 링크 추출 → 추가 변경점 확인
- [ ] 모든 연관 정보를 종합하여 최종 분석 결과 제공

---

## 🌿 브랜치 명명 규칙

| 브랜치 | 분류 | 설명 |
|--------|------|------|
| `connect_w_release` | **마스터 브랜치** | 메인 릴리즈 브랜치 |
| 그 외 모든 브랜치 | **이벤트 브랜치** | 특정 이벤트/기능 개발용 브랜치 |

> **AI 주의**: Gerrit Change의 브랜치가 `connect_w_release`이면 마스터 브랜치, 그 외는 이벤트 브랜치로 표기하세요.

---

## 💡 사용 팁

### 빠른 조회
```
# DQA 이슈
"HMCCW-33101 분석해줘"

# QE 이슈
"HMCCWQE-1234 이거 뭐야?"

# OEM 이슈
"POIPOEM-5678 요약해줘"

# AAU 태스크
"AAUEXPERT-910 확인해줘"

# 프로젝트 태스크
"HKMCPOIP-1122 분석해줘"

# Change 번호만 언급  
"287788 이거 뭐야?"

# URL 직접 제공
"http://lamp.lge.com/review/c/platform/vendor/lge/packages/apps/oem/Car/ccshmi/+/287788"
```

### 연관 정보 자동 조회
- Jira Description에서 관련 파일 추출
- Gerrit Changed Files 자동 파싱
- Confluence 테이블 데이터 구조화

### 문제 해결
- **Gerrit 404 Error**: SSH 방식으로 자동 전환
- **Jira 인증 실패**: 환경 변수 확인 (`$JIRA_USER`, `$JIRA_PASS`)
- **Confluence HTML 파싱 실패**: REST API로 raw 데이터 조회
- **터미널 접근 실패 (Android Studio 전용)**:
  - 이 가이드는 **Android Studio AI 플러그인** 환경에 최적화되어 있습니다.
  - Android Studio의 기본 터미널은 **PowerShell**이며, PowerShell에서는 `curl`이 `Invoke-WebRequest`로 매핑되어 `-u` 옵션 등이 정상 작동하지 않습니다.
  - AI가 터미널 명령 실행에 실패하거나 `curl` 오류가 발생하면 아래 설정 변경을 사용자에게 안내하세요.

  **✅ 해결 방법: Classic Terminal + CMD로 변경**
  
  1. Android Studio 상단 메뉴 → **`File` → `Settings`** (Mac: `Preferences`)
  2. **`Tools` → `Terminal`** 이동
  3. **`Shell path`** 를 아래 중 하나로 변경:
     - Windows CMD: `cmd.exe`
     - Git Bash: `C:\Program Files\Git\bin\bash.exe`
  4. **`Use classic terminal`** 옵션 활성화 (체크)
  5. 기존 터미널 탭 닫고 새 터미널 열기
  
  > ⚠️ 설정 변경 후 AI가 터미널 도구를 재시도하면 정상 작동합니다.

---

**버전**: 1.3.0  
**최종 업데이트**: 2026-03-20  
**용도**: Jira/Gerrit/Confluence 자동 조회 및 분석
