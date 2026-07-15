import os


class LogFileIndexer:
    def __init__(self, chunk_size=8 * 1024 * 1024):
        self._chunk_size = max(1024, int(chunk_size))

    def build_index(self, file_path, progress_callback=None, cancel_check=None):
        if not file_path:
            raise ValueError("file_path is required")

        file_size = os.path.getsize(file_path)
        if file_size == 0:
            if progress_callback:
                progress_callback(100, 0, 0)
            return []

        offsets = [0]
        bytes_read = 0

        with open(file_path, "rb") as file:
            while True:
                if cancel_check and cancel_check():
                    return None

                chunk = file.read(self._chunk_size)
                if not chunk:
                    break

                base_offset = bytes_read
                for idx, value in enumerate(chunk):
                    if value == 10:  # b'\\n'
                        next_line = base_offset + idx + 1
                        if next_line < file_size:
                            offsets.append(next_line)

                bytes_read += len(chunk)

                if progress_callback:
                    percent = min(100, int((bytes_read / file_size) * 100))
                    progress_callback(percent, bytes_read, file_size)

        if progress_callback:
            progress_callback(100, file_size, file_size)

        return offsets
