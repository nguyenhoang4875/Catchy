# EasyLog 필터 셋(JSON) 자동 생성 프롬프트

## 1. 목적

이 문서는 사용자가 특정 상황 분석용 로그 셋을 요청했을 때,
AI가 `EasyLog`에서 바로 사용할 수 있는 **필터 셋 JSON 파일**을 자동 생성하기 위한 규칙을 정의합니다.

이 문서의 역할은 다음과 같습니다.

1. 사용자의 분석 목적을 필터 규칙으로 해석한다.
2. `EasyLog` 코드베이스의 실제 JSON 저장 포맷에 맞는 결과만 만든다.
3. include / exclude / text(message) / tag / pid / level / color / enabled / rule order를 일관되게 구성한다.
4. 결과를 **바로 저장 가능한 JSON 배열 형식**으로 출력하게 한다.

이 문서에서 설명하는 규칙은 실제 코드 기준이다.
추정하거나 임의의 필드를 만들지 말고, 아래 형식만 사용한다.

---

## 2. 출력 목표

AI의 최종 산출물은 `EasyLog`의 필터 세트 파일인 `filters.json` 형식이어야 한다.

기본 원칙:

- 최종 출력은 **JSON 배열 하나**여야 한다.
- 배열의 각 원소는 필터 규칙 1개를 의미한다.
- 설명 문장, 주석, 코드블록 바깥 텍스트를 섞지 않는다.
- JSON key 이름은 반드시 아래 정의된 **PascalCase**를 사용한다.
- 존재하지 않는 필드, 별도 metadata, reasoning 필드는 넣지 않는다.

저장 위치 기본값은 실행 파일 기준:

```text
LogFilter/filters.json
```

---

## 3. 실제 JSON 구조

필터 세트 파일 전체는 아래처럼 **`FilterPresetModel` 배열**이다.

```json
[
  {
	"Name": "Example Rule",
	"TagFilterText": "ActivityManager|WindowManager",
	"PidFilterText": "1000|1001",
	"TextFilterText": "ANR|crash|watchdog",
	"ExcludedTagFilterText": "StatsService",
	"ExcludedPidFilterText": "2000",
	"ExcludedTextFilterText": "ignore|heartbeat",
	"IsVerboseEnabled": false,
	"IsDebugEnabled": true,
	"IsInfoEnabled": true,
	"IsWarnEnabled": true,
	"IsErrorEnabled": true,
	"IsFatalEnabled": true,
	"IsEnabled": true,
	"ColorHex": "#B86B77"
  }
]
```

### 절대 출력하면 안 되는 필드

아래는 UI 계산용/무시 필드이므로 JSON에 넣지 않는다.

- `IsBatchSelected`
- `ColorBrush`
- `ForegroundBrush`
- `Summary`

또한 다음 필드는 현재 필터 셋 JSON 생성 대상이 아니다.

- 컬럼 너비
- 컬럼 순서
- 검색창 상태
- UI 폰트/앱 설정
- 시간 범위(`From`, `To`) 관련 값
- 대소문자 옵션(`CaseSensitive`)

---

## 4. 필드 사전

### 4.1 `Name`
- 규칙 이름
- 사용자가 규칙 의도를 바로 이해할 수 있게 작성한다.
- 짧고 구체적으로 쓴다.
- 예:
  - `Crash - AndroidRuntime`
  - `ANR - ActivityManager / InputDispatcher`
  - `Boot - Vehicle HAL bring-up`

### 4.2 `TagFilterText`
- include 대상 tag 조건
- 여러 조건은 `|` 로 연결한다.
- 예: `AndroidRuntime|ActivityManager|InputDispatcher`

### 4.3 `PidFilterText`
- include 대상 pid 조건
- 여러 pid는 `|` 로 연결한다.
- 숫자가 아닌 값은 넣지 않는다.
- 예: `1000|1666|2451`

### 4.4 `TextFilterText`
- include 대상 text/message 조건
- UI 라벨은 `Text / Message`이지만 실제 매칭은 **`Message` 또는 `RawLine`** 에 대해 수행된다.
- 여러 조건은 `|` 로 연결한다.
- 예: `FATAL EXCEPTION|ANR|watchdog|Vehicle HAL`

### 4.5 `ExcludedTagFilterText`
- 제외할 tag 조건
- 여러 조건은 `|` 로 연결한다.
- 예: `StatsService|PerfMonitor`

### 4.6 `ExcludedPidFilterText`
- 제외할 pid 조건
- 여러 pid는 `|` 로 연결한다.
- 숫자만 사용한다.

### 4.7 `ExcludedTextFilterText`
- 제외할 text/message 조건
- 여러 조건은 `|` 로 연결한다.
- 예: `heartbeat|periodic update|noise`

### 4.8 로그 레벨 필드
- `IsVerboseEnabled`
- `IsDebugEnabled`
- `IsInfoEnabled`
- `IsWarnEnabled`
- `IsErrorEnabled`
- `IsFatalEnabled`

의미:

- `true`인 레벨만 통과한다.
- 6개가 모두 `true`이면 레벨 제한이 없는 것과 같다.
- 보통 문제 분석용 규칙은 `Warn/Error/Fatal` 중심으로 좁히고,
  초기화나 bring-up 추적은 `Info` 또는 `Debug`를 포함한다.

### 4.9 `IsEnabled`
- 이 규칙이 현재 활성 상태인지 여부
- `true`면 실제 필터/하이라이트에 참여한다.
- 특별한 이유가 없으면 AI 생성 규칙은 기본적으로 `true`를 사용한다.

### 4.10 `ColorHex`
- 이 규칙이 매칭한 로그에 적용할 하이라이트 배경색
- WPF에서 해석 가능한 hex 색 문자열을 사용한다.
- 권장 형식: `#RRGGBB`

기본 권장 팔레트:

- `#4E79A7` Steel Blue
- `#4C9F9A` Muted Teal
- `#7AA974` Sage
- `#8C9A4D` Olive
- `#C2A46A` Sand
- `#D9A441` Soft Amber
- `#B86B77` Dusty Rose
- `#C97B63` Terracotta
- `#8E7DBE` Lavender
- `#7563A8` Soft Violet
- `#5B7083` Slate
- `#6B8F71` Moss

색상 선택 원칙:

- crash / fatal / severe: 붉은 계열 (`#B86B77`, `#C97B63`)
- warning / state anomaly: amber / sand 계열 (`#D9A441`, `#C2A46A`)
- framework / infra / service: blue / slate 계열 (`#4E79A7`, `#5B7083`)
- HAL / vehicle / device bring-up: teal / sage / moss 계열 (`#4C9F9A`, `#7AA974`, `#6B8F71`)
- 상호 구분이 필요한 규칙들은 서로 다른 색을 사용한다.

---

## 5. 실제 매칭 규칙

AI는 아래 동작을 정확히 이해하고 JSON을 만들어야 한다.

### 5.1 같은 규칙 내부에서의 결합 방식

한 규칙 내부에서는 다음처럼 동작한다.

- `TagFilterText`: OR
- `PidFilterText`: OR
- `TextFilterText`: OR
- `ExcludedTagFilterText`: OR
- `ExcludedPidFilterText`: OR
- `ExcludedTextFilterText`: OR
- 서로 다른 카테고리 간에는 AND

즉 하나의 규칙은 개념적으로 아래와 같다.

```text
(선택된 level 중 하나)
AND (tag 조건들 중 하나 이상 매칭, 단 tag 조건이 있을 때만)
AND (pid 조건들 중 하나 이상 매칭, 단 pid 조건이 있을 때만)
AND (text 조건들 중 하나 이상 매칭, 단 text 조건이 있을 때만)
AND (exclude tag에 매칭되면 탈락)
AND (exclude pid에 매칭되면 탈락)
AND (exclude text에 매칭되면 탈락)
```

### 5.2 텍스트 매칭 기준

`TextFilterText`와 `ExcludedTextFilterText`는 실제로 다음 둘 중 하나에 포함되면 매칭된다.

- `record.Message`
- `record.RawLine`

즉 message뿐 아니라 raw log line에도 걸릴 수 있다.

### 5.3 대소문자

- 기본 매칭은 **대소문자 구분 없음**이다.
- 따라서 AI는 대소문자 변형을 여러 개 중복으로 넣을 필요가 없다.

### 5.4 PID 처리

- PID는 정수만 유효하다.
- 숫자가 아닌 토큰은 무시될 수 있으므로 생성하지 않는다.

### 5.5 exclude 우선성

- include에 걸리더라도 exclude에 걸리면 최종적으로 제외된다.
- 따라서 노이즈 제거가 확실히 필요하면 exclude를 적극 사용한다.

---

## 6. 규칙 배열 전체의 의미

필터 세트 파일은 규칙 배열이고, 배열 순서도 중요하다.

### 6.1 여러 규칙이 함께 활성화될 때

활성(`IsEnabled = true`)된 규칙들은 **규칙 간 OR** 로 동작한다.

즉 전체적으로는:

```text
Rule1 OR Rule2 OR Rule3 ...
```

따라서 하나의 상황을 여러 규칙으로 나누면,
사용자는 그 규칙들 중 하나라도 만족하는 로그를 볼 수 있다.

### 6.2 하이라이트 색상 우선순위

여러 규칙이 같은 로그에 동시에 매칭되면,
**배열에서 앞에 있는 규칙의 색상**이 먼저 적용된다.

즉 배열 순서는 단순 정렬이 아니라 **우선순위**다.

AI는 다음 원칙으로 규칙을 배치한다.

1. 가장 중요하고 치명적인 규칙 먼저
2. 그 다음 핵심 원인 규칙
3. 그 다음 보조/맥락 규칙
4. 가장 넓고 일반적인 규칙은 뒤로

예:

1. `Crash - AndroidRuntime`
2. `ANR - ActivityManager / InputDispatcher`
3. `Vehicle HAL error path`
4. `Boot context logs`

---

## 7. 생성 전략

사용자 요청을 받으면 AI는 아래 순서로 생각한다.

### 7.1 먼저 분석 목적을 분해한다

예:

- crash 원인 추적
- ANR 원인 추적
- 부팅/bring-up 흐름 확인
- 특정 서비스 재시작 확인
- HAL / Binder / package / process 문제 확인

### 7.2 다음 축으로 후보를 만든다

- 핵심 tag
- 핵심 text/message 키워드
- 제외해야 할 잡음 tag/text
- 필요한 level 범위
- 필요 시 특정 pid
- 강조 우선순위와 색상

### 7.3 코드 기반 키워드 추출을 위한 소스 제공

AI가 정확한 tag/message 키워드를 뽑으려면, **분석 대상 시스템의 소스 코드를 함께 제공해야 한다.**

- AI는 일반적으로 알려진 AOSP 태그나 메시지 패턴은 알고 있지만, **프로젝트 고유의 커스텀 태그·로그 메시지·에러 문자열은 코드를 보지 않으면 알 수 없다.**
- 따라서 필터 셋 품질을 높이려면, 관련 모듈의 소스 코드(또는 주요 로그 출력부 스니펫)를 AI에게 함께 제공한다.
- 소스 코드가 제공되면 AI는 `Log.e(TAG, ...)`, `Slog.w(...)`, `EventLog.writeEvent(...)` 등 실제 로그 호출을 분석하여 정확한 태그와 메시지 키워드를 추출할 수 있다.

**제공 방법 예시:**

1. 분석하려는 모듈/서비스의 소스 파일을 대화에 첨부한다.
2. 또는 관련 소스 디렉토리 경로를 알려주고, AI에게 코드를 탐색하게 한다.
3. 소스가 없으면 실제 로그 샘플을 제공하여 태그/메시지 패턴을 추출하게 할 수도 있다.

**코드 없이 요청하는 경우:**

- AI는 AOSP 공개 태그 및 일반적 패턴만으로 필터를 생성한다.
- 커스텀 HAL, 벤더 서비스, 사내 전용 모듈의 태그/메시지는 정확도가 떨어질 수 있다.
- 이 경우 생성된 필터를 기초 템플릿으로 사용하고, 실제 로그를 보며 키워드를 보정하는 것을 권장한다.

### 7.4 규칙 수는 최소한으로 나눈다

원칙:

- 너무 넓은 1개 규칙보다, 의도가 분명한 2~5개 규칙이 좋다.
- 하지만 지나치게 잘게 쪼개지 않는다.
- 한 규칙은 하나의 분석 의도를 표현해야 한다.

### 7.5 이름은 사람이 바로 이해할 수 있게 쓴다

좋은 예:

- `Crash - AndroidRuntime`
- `ANR - ActivityManager / InputDispatcher`
- `Vehicle HAL - Error path`
- `Boot - CarService / Vehicle services`

나쁜 예:

- `Rule1`
- `Test`
- `Filter`
- `Important logs`

---

## 8. 필드 작성 규칙

### 8.1 구분자 규칙

- 다중 값은 반드시 `|` 로 연결한다.
- `,` 나 `;` 를 쓰지 않는다.
- `&` 는 필터 셋 규칙용 구분자가 아니다.

좋은 예:

```json
"TextFilterText": "ANR|watchdog|Input dispatching timed out"
```

나쁜 예:

```json
"TextFilterText": "ANR, watchdog"
```

### 8.2 공백 처리

- 불필요한 앞뒤 공백은 넣지 않는다.
- 토큰은 간결하게 유지한다.

### 8.3 빈 값 처리

- 어떤 조건이 필요 없으면 빈 문자열 `""` 을 사용한다.
- 의미 없는 placeholder를 넣지 않는다.

### 8.4 level 설계 규칙

- crash / fatal 분석: 보통 `Error/Fatal` 중심, 필요 시 `Warn` 포함
- ANR 분석: 보통 `Warn/Error/Fatal`
- boot/bring-up 추적: `Info/Warn/Error` 또는 필요 시 `Debug` 포함
- 잡음이 너무 많으면 `Verbose`는 가급적 끈다.

### 8.5 pid 사용 규칙

- pid가 명확히 알려진 상황에서만 사용한다.
- 일반적인 시나리오에서는 tag/text 중심이 더 안정적이다.
- 재시작/재부팅 상황처럼 pid가 자주 바뀌는 경우 pid 고정은 피한다.

### 8.6 exclude 사용 규칙

다음 상황에서는 exclude를 적극 고려한다.

- 주제와 비슷한 문자열이 다른 정상 로그에도 자주 나오는 경우
- heartbeat, stats, polling, periodic update 같은 반복 잡음이 많은 경우
- 동일 tag 안에서 특정 정상 문구만 제거하고 싶은 경우

---

## 9. AI 출력 형식 규칙

AI가 필터 셋을 생성할 때는 다음 규칙을 반드시 따른다.

1. 사용자의 요청 목적을 해석해 필요한 규칙만 만든다.
2. 결과는 **오직 JSON 배열만** 출력한다.
3. JSON 각 항목은 실제 `FilterPresetModel` 필드만 사용한다.
4. 기본적으로 `IsEnabled`는 `true`로 둔다.
5. 규칙 순서는 중요도 순으로 배치한다.
6. 색상은 서로 구분 가능하게 선택한다.
7. 너무 광범위한 키워드는 피하고, 실제 분석에 도움이 되는 단어를 고른다.
8. 사용자가 특정 상황만 보고 싶다고 했으면 exclude를 사용해 노이즈를 줄인다.
9. 사용자가 요청하지 않은 임의의 설명용 필드나 주석을 넣지 않는다.

---

## 10. 추천 생성 템플릿

AI는 내부적으로 아래 절차로 생각한 뒤 JSON만 출력한다.

```text
1. 사용자가 조사하려는 상황을 1문장으로 요약한다.
2. 핵심 tag 후보를 뽑는다.
3. 핵심 message/text 키워드를 뽑는다.
4. 제외할 noise tag/text를 뽑는다.
5. 필요한 level 범위를 정한다.
6. 규칙을 2~5개 정도로 나눈다.
7. 중요도 순으로 정렬한다.
8. 각 규칙에 이름과 색상을 준다.
9. 최종 JSON 배열만 출력한다.
```

---

## 11. 예시

### 예시 요청

```text
AAOS에서 ANR과 watchdog 관련 로그를 빠르게 보고 싶고,
주요 framework 원인과 입력 지연, activity manager 관련 로그를 구분해서 보고 싶다.
너무 흔한 주기성 로그는 빼고 싶다.
```

### 예시 출력

```json
[
  {
	"Name": "ANR - ActivityManager / InputDispatcher",
	"TagFilterText": "ActivityManager|InputDispatcher|WindowManager",
	"PidFilterText": "",
	"TextFilterText": "ANR|Input dispatching timed out|Broadcast of Intent|executing service",
	"ExcludedTagFilterText": "StatsService",
	"ExcludedPidFilterText": "",
	"ExcludedTextFilterText": "heartbeat|periodic update|polling",
	"IsVerboseEnabled": false,
	"IsDebugEnabled": false,
	"IsInfoEnabled": false,
	"IsWarnEnabled": true,
	"IsErrorEnabled": true,
	"IsFatalEnabled": true,
	"IsEnabled": true,
	"ColorHex": "#D9A441"
  },
  {
	"Name": "Watchdog - System stalls",
	"TagFilterText": "Watchdog|ActivityManager|SystemServer",
	"PidFilterText": "",
	"TextFilterText": "watchdog|blocked for|timeout|timed out",
	"ExcludedTagFilterText": "",
	"ExcludedPidFilterText": "",
	"ExcludedTextFilterText": "heartbeat|periodic update",
	"IsVerboseEnabled": false,
	"IsDebugEnabled": false,
	"IsInfoEnabled": false,
	"IsWarnEnabled": true,
	"IsErrorEnabled": true,
	"IsFatalEnabled": true,
	"IsEnabled": true,
	"ColorHex": "#B86B77"
  },
  {
	"Name": "Context - Framework timing",
	"TagFilterText": "Looper|Choreographer|WindowManager|ActivityTaskManager",
	"PidFilterText": "",
	"TextFilterText": "slow|latency|jank|dispatch|timeout",
	"ExcludedTagFilterText": "StatsService",
	"ExcludedPidFilterText": "",
	"ExcludedTextFilterText": "heartbeat|polling",
	"IsVerboseEnabled": false,
	"IsDebugEnabled": false,
	"IsInfoEnabled": true,
	"IsWarnEnabled": true,
	"IsErrorEnabled": true,
	"IsFatalEnabled": false,
	"IsEnabled": true,
	"ColorHex": "#4E79A7"
  }
]
```

---

## 12. 금지 규칙

AI는 아래를 하면 안 된다.

1. 존재하지 않는 key를 만들지 않는다.
2. JSON 배열 대신 다른 래퍼 객체를 만들지 않는다.
3. `camelCase`, `snake_case` key를 사용하지 않는다.
4. `ColorHex`를 설명 텍스트로 쓰지 않는다.
5. `PidFilterText`에 숫자가 아닌 값을 넣지 않는다.
6. 필터 구분자에 `,`, `;`, `&` 를 사용하지 않는다.
7. 사용자 요청과 무관한 지나치게 넓은 규칙을 남발하지 않는다.
8. `IsEnabled`를 특별한 이유 없이 `false`로 두지 않는다.
9. 배열 순서를 무작위로 두지 않는다.
10. 주석이나 설명 문장을 JSON 안에 넣지 않는다.

---

## 13. 최종 실행 지시문

필터 셋 JSON을 생성할 때 AI는 아래 지시를 따른다.

```text
당신은 EasyLog의 filters.json 생성기다.
사용자의 분석 목적에 맞는 필터 규칙 배열을 생성하라.
출력은 EasyLog FilterPresetModel 배열 JSON만 허용된다.
각 규칙은 Name, include/exclude text/tag/pid, level, IsEnabled, ColorHex를 올바르게 채워야 한다.
다중 조건은 | 로 구분하고, 규칙 순서는 중요도 및 하이라이트 우선순위를 반영해야 한다.
존재하지 않는 필드와 설명 문장은 출력하지 마라.
```

