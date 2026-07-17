"""
ALV Confluence Beta Page Updater

Rules:
1. Update existing History table — insert new version row below header (newest first).
2. Guide section — maintain ONLY the latest version (replace, not append).
3. Old per-version guide sections are removed.
"""
import json
import os
import re
import sys
import urllib.request
import urllib.error
import base64

PAGE_IDS = ["3598372101", "3648829450"]
BASE_URL = "http://collab.lge.com/main"
USERNAME = os.environ.get("CONFLUENCE_USER")
PASSWORD = os.environ.get("CONFLUENCE_PASS")

# ── Update these on each release ──
VERSION = "v1.0.3"
ATTACHMENT_NAME = "LogPilot-AAOS-Log-Viewer-v1.0.3-win-x64.7z"
HISTORY_DATE = "26.06.10"
HISTORY_CHANGES = (
    "⚡ v1.0.3 로그 로딩/실시간 성능 개선 + 버그 수정 — "
    "파일 점진적 렌더링, 라이브 배치 append, 단일 패스 필터 + 검색 취소, "
    "다중 파일 동일 ms 타임스탬프 순서 보존(안정 정렬), "
    "About 팝업 텍스트 선택/이메일 복사"
)

MARKER_START = "<!-- LOGPILOT_BETA_START -->"
MARKER_END = "<!-- LOGPILOT_BETA_END -->"

if not USERNAME or not PASSWORD:
    raise SystemExit("Missing CONFLUENCE_USER or CONFLUENCE_PASS")


def build_auth_header() -> str:
    token = base64.b64encode(f"{USERNAME}:{PASSWORD}".encode("utf-8")).decode("ascii")
    return f"Basic {token}"


def http_get(url: str) -> dict:
    request = urllib.request.Request(url)
    request.add_header("Authorization", build_auth_header())
    with urllib.request.urlopen(request) as response:
        return json.loads(response.read().decode("utf-8"))


def http_put(url: str, payload: dict) -> dict:
    data = json.dumps(payload, ensure_ascii=False).encode("utf-8")
    request = urllib.request.Request(url, data=data, method="PUT")
    request.add_header("Authorization", build_auth_header())
    request.add_header("Content-Type", "application/json; charset=utf-8")
    with urllib.request.urlopen(request) as response:
        return json.loads(response.read().decode("utf-8"))


# ─── History table: insert new row ───────────────────────────────
def build_history_row() -> str:
    return (
        f'<tr><td>{HISTORY_DATE}</td><td>{VERSION}</td>'
        f'<td>{HISTORY_CHANGES}</td>'
        f'<td><ac:link><ri:attachment ri:filename="{ATTACHMENT_NAME}" /></ac:link></td></tr>'
    )


def update_history_table(body: str) -> str:
    """Insert a new row into the existing History table right after the header row."""
    if f"<td>{VERSION}</td>" in body:
        print(f"History table already contains {VERSION}, skipping insert.")
        return body

    # Pattern: header row — handles <th scope="col"> and plain <th>, typo 'Chnages'
    header_pattern = re.compile(
        r'(<tr>\s*<th[^>]*>\s*Date\s*</th>\s*<th[^>]*>\s*Version\s*</th>\s*<th[^>]*>\s*Chn?a(?:n)?ges\s*</th>\s*<th[^>]*>\s*File\s*</th>\s*</tr>)',
        re.DOTALL | re.IGNORECASE
    )
    match = header_pattern.search(body)
    if not match:
        # Fallback: simpler pattern
        header_pattern = re.compile(
            r'(<tr><th>Date</th><th>Version</th><th>Chn?a(?:n)?ges</th><th>File</th></tr>)',
            re.IGNORECASE
        )
        match = header_pattern.search(body)

    if match:
        insert_pos = match.end()
        # Skip any empty placeholder rows right after header
        empty_row = re.compile(
            r'\s*<tr>\s*<td>\s*<br\s*/?\s*>\s*</td>\s*<td>\s*<br\s*/?\s*>\s*</td>\s*<td>\s*<br\s*/?\s*>\s*</td>\s*<td>\s*<br\s*/?\s*>\s*</td>\s*</tr>',
            re.DOTALL
        )
        empty_match = empty_row.match(body[insert_pos:])
        if empty_match:
            insert_pos += empty_match.end()
        return body[:insert_pos] + build_history_row() + body[insert_pos:]

    print("WARNING: Could not find History table header row. History not updated.")
    return body


# ─── Guide section: single latest version ────────────────────────
def build_guide_section() -> str:
    return f"""{MARKER_START}
<h2>LogPilot 배포 안내 / Distribution Guide</h2>
<p><strong>Latest Version: {VERSION}</strong></p>
<p><strong>Download / 다운로드:</strong> <ac:link><ri:attachment ri:filename="{ATTACHMENT_NAME}" /><ac:plain-text-link-body><![CDATA[{ATTACHMENT_NAME}]]></ac:plain-text-link-body></ac:link></p>
<h3>한국어 안내</h3>
<p><strong>LogPilot - AAOS Log Viewer</strong>는 Android / AAOS 로그를 <strong>실시간 수집, 파일 열기, 검색, 필터링, 내보내기</strong> 할 수 있는 Windows 데스크톱 로그 뷰어입니다.</p>
<p><strong>주요 특징</strong></p>
<ul>
  <li><strong>실시간 ADB 로그 수집</strong> — USB로 연결된 Android 장치 로그 실시간 모니터링</li>
  <li><strong>다양한 로그 파일 열기</strong> — <code>.log</code>, <code>.logcat</code>, <code>.txt</code>, <code>.zip</code>, <code>.7z</code> (다중 파일 / 압축 내 자동 추출)</li>
  <li><strong>다중 파일 타임스탬프 정렬</strong> — 여러 파일을 동시에 열면 시간순 자동 정렬</li>
  <li><strong>파일 로드 진행률 + 취소</strong> — 대용량 파일 로드 중 실시간 상태 표시 및 Cancel 기능</li>
  <li><strong>강력한 필터링</strong> — Tag, PID, Message, Level 기반 include/exclude 필터 + 로딩 오버레이</li>
  <li><strong>통합 검색</strong> — OR(<code>|</code>) / AND(<code>&amp;</code>) 다중 검색, 하이라이트</li>
  <li><strong>검색 히스토리</strong> — 이전 검색어 최대 10개 자동 저장, 드롭다운 선택 및 개별 삭제</li>
  <li><strong>필터 세트 관리</strong> — 필터 규칙 저장/불러오기/색상 하이라이트</li>
  <li><strong>로그 내보내기</strong> — 수집된 전체 로그를 50MB 분할 7z 아카이브로 저장</li>
  <li><strong>한글 로그 지원</strong> — UTF-8, CP949, EUC-KR 인코딩 자동 감지</li>
  <li><strong>고성능 로딩</strong> — Regex 제거, 병렬 파일 로드, 메모리 최적화</li>
  <li><strong>스풀 자동 정리</strong> — 24시간 이상 된 임시 스풀 파일 자동 삭제</li>
</ul>
<p><strong>실행 방법</strong></p>
<ol>
  <li>첨부된 <code>{ATTACHMENT_NAME}</code> 파일을 다운로드합니다.</li>
  <li>압축을 해제합니다.</li>
  <li>압축 해제 폴더 최상단의 <code>LogPilot.exe</code>를 실행합니다.</li>
  <li>ADB Live 기능을 사용하려면 시스템에 Android Platform Tools가 설치되어 있거나, <code>tools</code> 폴더에 <code>adb.exe</code>, <code>AdbWinApi.dll</code>, <code>AdbWinUsbApi.dll</code>를 넣어주세요.</li>
  <li>7z export를 앱 내부에서 바로 사용하려면 시스템 7-Zip이 설치되어 있거나, <code>tools</code> 폴더에 <code>7z.exe</code>, <code>7z.dll</code>를 넣어주세요.</li>
</ol>
<p><strong>사용 방법</strong></p>
<ol>
  <li><strong>파일 로그 보기</strong>: <code>Open Log</code> 버튼으로 로그 파일을 엽니다. 다중 파일 선택 및 .zip/.7z 직접 열기, 드래그 앤 드롭이 가능합니다.</li>
  <li><strong>실시간 로그 보기</strong>: <code>Discover Devices</code> → 디바이스 선택 → <code>Start Live</code>를 누릅니다.</li>
  <li><strong>검색</strong>: 상단 검색창에 키워드를 입력하고 Enter. <code>Ctrl+F</code>로 포커스. <code>|</code>=OR, <code>&amp;</code>=AND. <code>Search All Logs</code>로 전체 로그 검색. 검색 히스토리 드롭다운 지원.</li>
  <li><strong>미리보기</strong>: 로그 행을 선택하면 좌측 하단 <code>Preview</code> 패널에서 전체 메시지를 확인하고 드래그/Ctrl+C로 복사.</li>
  <li><strong>필터</strong>: 좌측 <code>Filter Rules</code>에서 규칙을 추가/수정/활성화. 색상 하이라이트, 우클릭 메뉴, 드래그 앤 드롭 우선순위 조정 지원.</li>
  <li><strong>필터 저장</strong>: <code>Save Set</code> 또는 <code>Save Set As</code>로 <code>LogFilter</code> 폴더에 JSON 저장.</li>
  <li><strong>내보내기</strong>: <code>Export Logs</code>로 현재 로그를 50MB 분할 7z 아카이브로 저장.</li>
  <li><strong>복사</strong>: <code>Ctrl+C</code> 행 복사, 우클릭 → Copy Rows / Copy Message Only.</li>
</ol>
<p><strong>단축키</strong></p>
<ul>
  <li><code>Ctrl+F</code> — 검색창 포커스</li>
  <li><code>Ctrl+C</code> — 선택 영역 복사</li>
  <li><code>Home</code> — 로그 최상단 이동</li>
  <li><code>End</code> — 자동 스크롤 재활성화</li>
  <li><code>PageUp/PageDown</code> — 페이지 단위 이동</li>
  <li><code>ESC</code> — 포커스 해제</li>
</ul>
<h3>🤖 AI 필터 셋 자동 생성</h3>
<p>AI(GitHub Copilot, ChatGPT 등)에게 첨부된 프롬프트 파일을 제공하면, LogPilot에서 바로 사용할 수 있는 필터 셋 JSON을 자동으로 만들어줍니다.</p>
<p><strong>프롬프트 파일:</strong> <ac:link><ri:attachment ri:filename="Prompt_Make_filter_set.md" /><ac:plain-text-link-body><![CDATA[Prompt_Make_filter_set.md]]></ac:plain-text-link-body></ac:link></p>
<p><strong>사용 방법</strong></p>
<ol>
  <li>위 프롬프트 파일을 다운로드하여 AI에게 전달합니다.</li>
  <li>분석하고 싶은 상황을 요청합니다.<br/>예: <em>"AAOS에서 ANR과 watchdog 관련 로그를 보고 싶고, 주기성 로그는 빼주세요"</em></li>
  <li>AI가 생성한 JSON을 LogPilot 폴더의 <code>LogFilter/filters.json</code>에 저장합니다.</li>
  <li>LogPilot에서 <code>Load Set</code>으로 불러와 바로 사용합니다.</li>
</ol>
<h3>English Guide</h3>
<p><strong>LogPilot - AAOS Log Viewer</strong> is a Windows desktop log viewer for Android / AAOS that supports <strong>live ADB collection, file loading, search, filtering, and export</strong>.</p>
<p><strong>Key Features</strong></p>
<ul>
  <li><strong>Live ADB log collection</strong> — real-time monitoring of USB-connected Android devices</li>
  <li><strong>Multiple log file formats</strong> — <code>.log</code>, <code>.logcat</code>, <code>.txt</code>, <code>.zip</code>, <code>.7z</code> (multi-file, auto-extraction)</li>
  <li><strong>Multi-file timestamp sorting</strong> — automatic chronological ordering</li>
  <li><strong>Progress + Cancel</strong> — real-time load status and cancellation for large files</li>
  <li><strong>Powerful filtering</strong> — Tag, PID, Message, Level include/exclude with loading overlay</li>
  <li><strong>Integrated search</strong> — OR(<code>|</code>) / AND(<code>&amp;</code>), highlight, search history</li>
  <li><strong>Filter set management</strong> — save/load/color-highlight filter rules</li>
  <li><strong>Log export</strong> — split 50MB 7z archive</li>
  <li><strong>Korean encoding</strong> — UTF-8, CP949, EUC-KR auto-detection</li>
  <li><strong>High performance</strong> — no regex, parallel file load, memory optimization</li>
</ul>
<p><strong>How to Run</strong></p>
<ol>
  <li>Download the attached <code>{ATTACHMENT_NAME}</code>.</li>
  <li>Extract the archive.</li>
  <li>Run <code>LogPilot.exe</code> from the top level of the extracted folder.</li>
  <li>For ADB live logging: install Android Platform Tools or place <code>adb.exe</code>, <code>AdbWinApi.dll</code>, <code>AdbWinUsbApi.dll</code> into the <code>tools</code> folder.</li>
  <li>For 7z export: install 7-Zip or place <code>7z.exe</code>, <code>7z.dll</code> into the <code>tools</code> folder.</li>
</ol>
<p><strong>How to Use</strong></p>
<ol>
  <li><strong>Open log files</strong>: Click <code>Open Log</code>. Multi-select, .zip/.7z, and drag &amp; drop supported.</li>
  <li><strong>Start live logging</strong>: <code>Discover Devices</code> → select device → <code>Start Live</code>.</li>
  <li><strong>Search</strong>: Enter keywords in the search box. <code>Ctrl+F</code> for focus. <code>|</code>=OR, <code>&amp;</code>=AND. <code>Search All Logs</code> ignores filters. Search history dropdown.</li>
  <li><strong>Preview</strong>: Select a row to view the full message in the <code>Preview</code> panel. Drag-select + Ctrl+C to copy.</li>
  <li><strong>Filter</strong>: Use <code>Filter Rules</code> panel. Add/edit/enable rules. Color highlight, right-click menu, drag &amp; drop priority.</li>
  <li><strong>Save filters</strong>: <code>Save Set</code> / <code>Save Set As</code> to <code>LogFilter</code> folder (JSON).</li>
  <li><strong>Export</strong>: <code>Export Logs</code> to save as split 50MB 7z archive.</li>
  <li><strong>Copy</strong>: <code>Ctrl+C</code> row copy, right-click → Copy Rows / Copy Message Only.</li>
</ol>
<p><strong>Keyboard Shortcuts</strong></p>
<ul>
  <li><code>Ctrl+F</code> — Focus search box</li>
  <li><code>Ctrl+C</code> — Copy selection</li>
  <li><code>Home</code> — Jump to top</li>
  <li><code>End</code> — Re-enable auto scroll</li>
  <li><code>PageUp/PageDown</code> — Page navigation</li>
  <li><code>ESC</code> — Release focus</li>
</ul>
<h3>🤖 AI Filter Set Generator</h3>
<p>Provide the attached prompt file to an AI assistant (GitHub Copilot, ChatGPT, etc.) and it will generate a filter set JSON ready to use in LogPilot.</p>
<p><strong>Prompt file:</strong> <ac:link><ri:attachment ri:filename="Prompt_Make_filter_set.md" /><ac:plain-text-link-body><![CDATA[Prompt_Make_filter_set.md]]></ac:plain-text-link-body></ac:link></p>
<p><strong>How to Use</strong></p>
<ol>
  <li>Download the prompt file above and provide it to an AI assistant.</li>
  <li>Describe the log analysis scenario you need.<br/>Example: <em>"Show ANR and watchdog logs in AAOS, excluding periodic noise"</em></li>
  <li>Save the generated JSON to <code>LogFilter/filters.json</code> in the LogPilot folder.</li>
  <li>Load it in LogPilot via <code>Load Set</code>.</li>
</ol>
{MARKER_END}"""


def remove_old_versioned_sections(body: str) -> str:
    """Remove all old guide sections (versioned and unversioned) and marker-based sections."""
    # Remove new marker-based sections
    marker_pattern = re.compile(
        re.escape(MARKER_START) + r".*?" + re.escape(MARKER_END),
        re.DOTALL
    )
    body = marker_pattern.sub("", body)

    # Remove old ALV marker-based sections (legacy)
    old_marker_pattern = re.compile(
        r"<!-- ALV_BETA_START -->.*?<!-- ALV_BETA_END -->",
        re.DOTALL
    )
    body = old_marker_pattern.sub("", body)

    # Remove ALL guide h2 sections — catches any variant title containing
    # 'Distribution', '배포', or 'LogPilot' followed by content until next <h2> or end
    guide_section = re.compile(
        r'<h2>(?:ALV|LogPilot)[^<]*(?:Distribution|배포)[^<]*</h2>.*?(?=<h2>|$)',
        re.DOTALL | re.IGNORECASE
    )
    body = guide_section.sub("", body)

    return body.strip()


def apply_guide_section(body: str) -> str:
    """Replace or append the single guide section."""
    body = remove_old_versioned_sections(body)
    guide = build_guide_section()

    if body and not body.endswith("\n"):
        body += "\n"
    return body + "\n" + guide


def main() -> int:
    for page_id in PAGE_IDS:
        api_url = f"{BASE_URL}/rest/api/content/{page_id}"
        print(f"\n--- Updating page {page_id} ---")
        current = http_get(f"{api_url}?expand=body.storage,version,title")
        version_num = int(current["version"]["number"])
        title = current["title"]
        body = current["body"]["storage"]["value"]

        # Step 1: Update History table (insert new row)
        body = update_history_table(body)

        # Step 2: Replace guide section (single latest version, remove old duplicates)
        body = apply_guide_section(body)

        payload = {
            "id": page_id,
            "type": "page",
            "title": title,
            "version": {
                "number": version_num + 1,
                "minorEdit": False
            },
            "body": {
                "storage": {
                    "value": body,
                    "representation": "storage"
                }
            }
        }

        updated = http_put(api_url, payload)
        print(json.dumps({
            "pageId": updated.get("id"),
            "title": updated.get("title"),
            "newVersion": updated.get("version", {}).get("number")
        }, ensure_ascii=False))

    return 0


if __name__ == "__main__":
    raise SystemExit(main())

