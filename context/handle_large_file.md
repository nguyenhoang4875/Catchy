Your current architecture is clean for a medium-sized log viewer, but it will struggle with 200MB+ log files because the bottleneck is not Qt, it's your data model:

LogModel parses the entire file into a list of dictionaries before displaying it.

For a 200MB log, this can easily become:

1–3 million log lines
2-5 GB RAM usage after Python object overhead
several seconds of parsing
UI freezes if not carefully chunked

I would redesign the architecture around streaming + indexing + virtualization.

Proposed Architecture
                 +------------------------+
                 |       QML TableView    |
                 +-----------+------------+
                             |
                     QAbstractTableModel
                             |
                     VirtualLogModel
                             |
          +------------------+------------------+
          |                                     |
          |                                     |
   LogCacheManager                     SearchEngine
          |                                     |
          |                                     |
    LogFileIndex                     Search Index
          |                                     |
          +------------------+------------------+
                             |
                      LogFileReader
1. LogFileReader

Only responsible for reading files.

No parsing logic.

class LogFileReader:

    open(path)

    read_line(offset)

    read_lines(start, count)

    close()

This class only performs file IO.

2. LogFileIndexer

When opening a file:

line0 -> byte offset 0

line1 -> byte offset 73

line2 -> byte offset 140

...

Store

offsets = [
0,
73,
140,
...
]

Then reading line 900000 is

seek(offsets[900000])

readline()

O(1)

instead of

seek(0)

read 900000 lines
Building index
while chunk := file.read(8MB):

    find '\n'

    store byte offsets

No parsing yet.

Memory usage:

1 million lines

offset = uint64

8 MB

Very small.

3. VirtualLogModel

Instead of storing

[
 dict,
 dict,
 dict,
 dict
]

Store

line count

cache

file index

When Qt requests row 1000

data(index):

    if cached

       return cached

    else

       parse line

       cache

       return

This is called lazy parsing.

Only visible rows are parsed.

Maybe

100 rows

instead of

1 million rows.
4. LRU Cache

Recently viewed rows stay in memory.

row 1000

row 1001

row 1002

Scrolling uses cache.

cache size

5000 rows

or

10000 rows

Using OrderedDict:

OrderedDict()

move_to_end()

popitem(last=False)
5. Background Parser

When scrolling,

predict next rows

1000

1001

1002

Background thread parses

1003...

1100

before user reaches them.

Scrolling becomes instant.

6. Loading Flow

Instead of

Read file

↓

Parse everything

↓

Display

Do

Open file

↓

Build offsets

↓

Show table immediately

↓

Background parsing

↓

Cache visible rows

The UI appears after indexing, without waiting for full parsing.

7. Search Engine

Current architecture appears to search by iterating through every parsed entry.

That becomes too slow.

Instead

SearchWorker

↓

Read file chunk

↓

Regex search

↓

Return matching line numbers

Searching should never access the UI thread.

For plain text search, a memory-mapped file (mmap) can significantly improve performance because it avoids repeated file reads.

Results:

[
 42,
 59,
 6002,
 91023
]

Then

VirtualModel

↓

jump to row
8. Filtering

Avoid filtering Python objects.

Instead

worker

↓

scan file

↓

matched line numbers

↓

proxy model

Only matching rows are exposed to the model.

9. Thread Layout
UI Thread

    QML

    VirtualLogModel

-----------------------------------

Index Thread

    Build offsets

-----------------------------------

Search Thread

    Regex

-----------------------------------

Filter Thread

    Scan

-----------------------------------

Streaming Thread

    SSH

-----------------------------------

Parser Thread

    Background cache

Each heavy task has its own worker thread, ensuring the UI remains responsive.

10. Memory Estimate

Current design:

200 MB file

↓

parsed dicts

↓

2~5 GB RAM

Proposed:

200 MB file

↓

offset index

≈ 8~20 MB

cache

≈ 10 MB

Total

≈ 20~40 MB

This is dramatically more scalable.

11. Recommended Component Structure
components/

    Controller.py

    VirtualLogModel.py

    LogFileReader.py

    LogFileIndexer.py

    LogCache.py

    LogParser.py

    SearchEngine.py

    FilterEngine.py

    Worker.py

    RemoteDeviceManager.py

qml/

    LogTable.qml

    SearchPanel.qml

    FilterPanel.qml
12. Overall Recommendation

For a professional log viewer capable of handling 200 MB+ files smoothly, I recommend these design principles:

Never load the entire file into Python objects.
Index line byte offsets once during file opening.
Parse log lines lazily only when requested by the view.
Use an LRU cache to keep recently viewed rows.
Run indexing, searching, filtering, and parsing in dedicated worker threads.
Use memory-mapped files (mmap) where appropriate for fast searching.
Keep QAbstractTableModel virtual—its job is to provide data on demand, not own the entire dataset.

This architecture is similar to what high-performance log viewers (such as BareTail, glogg, and klogg) use, and it scales well from tens of megabytes to multi-gigabyte log files while keeping the UI responsive.