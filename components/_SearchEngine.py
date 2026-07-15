import re


class SearchEngine:
    def __init__(self, chunk_size=4 * 1024 * 1024):
        self._chunk_size = max(1024, int(chunk_size))

    def search_line_numbers(self, file_path, offsets, pattern, case_sensitive=False, is_regex=False, cancel_check=None, progress_callback=None):
        query = (pattern or "").strip()
        if not query:
            return []

        flags = 0 if case_sensitive else re.IGNORECASE
        if is_regex:
            regex = re.compile(query, flags)
        else:
            regex = re.compile(re.escape(query), flags)

        total_lines = len(offsets)
        matches = []

        with open(file_path, "rb") as file:
            for line_number, offset in enumerate(offsets):
                if cancel_check and cancel_check():
                    return None

                file.seek(offset)
                raw_line = file.readline()
                text = raw_line.decode("utf-8", errors="replace")
                if regex.search(text):
                    matches.append(line_number)

                if progress_callback and (line_number % 10000 == 0 or line_number + 1 == total_lines):
                    progress_callback(int(((line_number + 1) / max(1, total_lines)) * 100), line_number + 1, total_lines)

        return matches
