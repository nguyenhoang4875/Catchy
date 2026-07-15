class LogFileReader:
    def __init__(self, file_path=None, encoding="utf-8", errors="replace"):
        self._file = None
        self._file_path = ""
        self._encoding = encoding
        self._errors = errors
        if file_path:
            self.open(file_path)

    @property
    def file_path(self):
        return self._file_path

    def open(self, file_path):
        self.close()
        self._file_path = file_path
        self._file = open(file_path, "rb")

    def close(self):
        if self._file:
            self._file.close()
            self._file = None
        self._file_path = ""

    def read_line(self, offset):
        if self._file is None:
            raise RuntimeError("File is not opened")

        self._file.seek(offset)
        line_bytes = self._file.readline()
        return line_bytes.decode(self._encoding, errors=self._errors).rstrip("\r\n")

    def read_lines(self, offsets, start, count):
        if self._file is None:
            raise RuntimeError("File is not opened")

        if count <= 0:
            return []

        result = []
        end = min(len(offsets), start + count)
        for row in range(start, end):
            result.append(self.read_line(offsets[row]))
        return result
