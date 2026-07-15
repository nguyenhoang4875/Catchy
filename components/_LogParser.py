import re

LINE_NUMBER = "line_number"
DATE_TIME = "datetime"
PID = "pid"
TID = "tid"
LOG_LEVEL = "log_level"
TAG = "tag"
PROCESS_NAME = "process_name"
MESSAGE = "message"
COLOR = "color"
FILTER_COLOR = "filter_color"
LEVEL_COLOR = "level_color"

COL_DATETIME = "Date Time"
COL_PID = "PID"
COL_TID = "TID"
COL_LOGLEVEL = "Level"
COL_TAG = "Tag"
COL_MESSAGE = "Message"

LOG_LEVEL_COLORS = {
    "V": "#4E7D96 ",
    "D": "#0086D6",
    "I": "#108528",
    "W": "#C47C00",
    "E": "#D32F2F",
    "F": "#A222B2",
}

LOG_PATTERN = re.compile(
    r"(?P<datetime>\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d+Z) "
    r"\[(?P<pid>\d+\.\d+)\] "
    r"user\.(?P<log_level>\w+) "
    r"(?P<tag>.*) "
    r"\[\] "
    r"(?P<message>.*)"
)

COMPACT_PATTERN = re.compile(
    r"\[(?P<datetime>[^\]]*)\]\s+"
    r"\[(?P<pid>[^\]]*)\]\s+"
    r"\[(?P<tid>[^\]]*)\]\s+"
    r"\[(?P<log_level>[^\]]*)\]\s+"
    r"\[(?P<tag>[^\]]*)\]\s+"
    r"\[(?P<message>.*)\]\s*$"
)

LOGCAT_PATTERN = re.compile(
    r"(?P<datetime>\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d+)\s+"
    r"(?P<pid>\d+)\s+"
    r"(?P<tid>\d+)\s+"
    r"(?P<log_level>[VDIWEFS])\s+"
    r"(?P<tag>[^:]+)\s*:\s*"
    r"(?P<message>.*)"
)


def normalize_entry(log_entry):
    log_entry[PID] = str(log_entry.get(PID, "")).strip()
    log_entry[TID] = str(log_entry.get(TID, "")).strip()
    log_entry[TAG] = str(log_entry.get(TAG, "")).strip()
    log_entry[LOG_LEVEL] = str(log_entry.get(LOG_LEVEL, "")).strip()
    log_entry[DATE_TIME] = str(log_entry.get(DATE_TIME, "")).strip()
    log_entry[MESSAGE] = str(log_entry.get(MESSAGE, "")).strip()
    log_entry[PROCESS_NAME] = log_entry[TAG]
    return log_entry


def parse_line(line):
    for pattern in (COMPACT_PATTERN, LOGCAT_PATTERN, LOG_PATTERN):
        match = pattern.match(line)
        if match:
            return normalize_entry(match.groupdict())
    return None


def parse_logcat_line(line):
    match = LOGCAT_PATTERN.match(line)
    if not match:
        return None
    return normalize_entry(match.groupdict())


def format_log_line(log_entry):
    return (
        f"[{log_entry.get(DATE_TIME, '')}] "
        f"[{log_entry.get(PID, '')}] "
        f"[{log_entry.get(TID, '')}] "
        f"[{log_entry.get(LOG_LEVEL, '')}] "
        f"[{log_entry.get(TAG, '')}] "
        f"[{log_entry.get(MESSAGE, '')}]"
    )


def tag_matches(pattern, value):
    if not pattern:
        return False

    try:
        return bool(re.search(pattern, value, re.IGNORECASE))
    except re.error:
        return pattern.lower() == value.lower()


def filter_color_for_entry(log_entry, colors):
    process_name = str(log_entry.get(PROCESS_NAME, ""))
    for tag_pattern, color in colors.items():
        if tag_matches(str(tag_pattern), process_name):
            return color
    return ""


def level_color_for_entry(log_entry):
    return LOG_LEVEL_COLORS.get(log_entry.get(LOG_LEVEL, ""), "")


def color_for_entry(log_entry, colors):
    filter_color = filter_color_for_entry(log_entry, colors)
    if filter_color:
        return filter_color
    return level_color_for_entry(log_entry)
