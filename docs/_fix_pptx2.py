"""Follow-up fix for judge-facing wording in the final PowerPoint deck.
This is a one-off cleanup script used to repair the exported deck after
multiple manual edits. It keeps the content polished without altering layout."""
from pptx import Presentation
from pptx.dml.color import RGBColor
from pptx.enum.text import PP_ALIGN
from pptx.util import Inches, Pt

FINAL_PATH = r"d:\Expert_Task\Catchy\docs\[Expert_Task][hoang5.nguyen]Catchy_Android_Log_Viewer_final.pptx"

SUMMARY_SLIDE_TEXT = [
    "Executive Summary",
    "Quantitative & Qualitative Business Achievements",
    "• 500MB log files load in approximately 12 seconds, compared with about 45 seconds in the legacy tool (about 3x faster).",
    "• Reduced debugging friction by combining log viewing, filtering, bookmarking, multi-file merging, and Scrcpy workflow in one platform.",
    "• Cut manual investigation effort through keyword search, tag filters, split-view readability, and keyboard shortcuts for faster triage.",
    "• Improved operational reliability with auto device detection, live streaming retention, and smoother workflow for Android and embedded QA teams.",
    "• Increased business value by improving traceability, reducing context switching, and enabling faster issue investigation and release validation.",
]

REPLACEMENTS = [
    ("Show the hint of the history whey typing the work to search (Ctrl + Space)",
     "Show a history hint when typing the word to search (Ctrl + Space)"),
    ("Show the hint of the history whey typing the work to search",
     "Show a history hint when typing the word to search"),
    ("Write some script(.bat, .py, \u2026) to merged the log fist",
     "Write a script (.bat, .py, etc.) to merge the logs first"),
    ("Write some script(.bat, .py, …) to merged the log fist",
     "Write a script (.bat, .py, etc.) to merge the logs first"),
    ("To mark the line: click to the line to select, right click to show the menu",
     "To mark a line: click the line to select, then right-click to open the menu."),
    ("The left-side panels allow users to monitor active filters and bookmarks without leaving the main debugging context, click to jump to the line of the bookmark",
     "The left-side panels let users monitor active filters and bookmarks without leaving the main debugging context; click a bookmark to jump to the marked line."),
    ("Two-layer search: first filter by tag, then search by keyword within the tag-filtered results",
     "Two-step search: first filter by tag, then search by keyword within the filtered results."),
    ("The Tool Solution: Merged The file in the tool with open add file or drag to merged.",
     "The tool solution: merge files directly in the app by opening additional logs or dragging them into one tab."),
    ("Support the dark theme",
     "Dark theme support"),
    ("Keboard Shortcuts",
     "Keyboard Shortcuts"),
    ("Seach",
     "Search"),
    ("whey",
     "when"),
    ("the work",
     "the word"),
    ("merged the log fist",
     "merge the logs first"),
    ("Click the bookmark to jump the line is marked and see around source code",
     "Click a bookmark to jump to the marked line and view the surrounding log context."),
    ("Write a script (.bat, .py, etc.) to merge the logs first =>Then combine them to a file and check as normal",
     "Write a script (.bat, .py, etc.) to merge the logs first. Then combine them into a single file and check as normal."),
    ("The updated interface demonstrates a clearer layout, reduced operational friction, and a more efficient debugging workflow.",
     "The updated interface shows a clearer layout, reduced operational friction, and a more efficient debugging workflow."),
    ("Continuous Monitoring: A 500MB retention window preserves recent activity, supporting continuous live monitoring without losing critical context.",
     "Continuous monitoring: a 500MB retention window preserves recent activity and supports continuous live monitoring without losing critical context."),
    ("Streamlined UI & UX: Features smooth scrolling, drag-to-open file handling, and an efficient side-panel layout to drastically reduce friction during log analysis.",
     "Streamlined UI/UX: smooth scrolling, drag-to-open file handling, and an efficient side-panel layout reduce friction during log analysis."),
    ("Proof of the New UI and Features",
     "Proof of the improved UI and features"),
]


def collapse_paragraph_with_replacement(para, replacements):
    joined = ''.join(r.text for r in para.runs)
    changed = False
    for old, new in replacements:
        if old in joined:
            joined = joined.replace(old, new)
            changed = True
    if not changed:
        return

    runs = list(para.runs)
    if not runs:
        para.text = joined
        return

    runs[0].text = joined
    for r in runs[1:]:
        r._r.getparent().remove(r._r)


def add_summary_slide(path):
    prs = Presentation(path)
    blank = prs.slide_layouts[6]
    new_slide = prs.slides.add_slide(blank)

    title_box = new_slide.shapes.add_textbox(Inches(0.7), Inches(0.4), Inches(12.0), Inches(0.8))
    title_frame = title_box.text_frame
    title_frame.text = SUMMARY_SLIDE_TEXT[0]
    title_para = title_frame.paragraphs[0]
    title_para.alignment = PP_ALIGN.CENTER
    title_para.font.size = Pt(24)
    title_para.font.bold = True

    subtitle_box = new_slide.shapes.add_textbox(Inches(0.7), Inches(1.2), Inches(12.0), Inches(0.8))
    subtitle_frame = subtitle_box.text_frame
    subtitle_frame.text = SUMMARY_SLIDE_TEXT[1]
    subtitle_para = subtitle_frame.paragraphs[0]
    subtitle_para.alignment = PP_ALIGN.CENTER
    subtitle_para.font.size = Pt(18)
    subtitle_para.font.italic = True

    body_box = new_slide.shapes.add_textbox(Inches(0.9), Inches(2.0), Inches(11.6), Inches(4.8))
    body = body_box.text_frame
    body.word_wrap = True
    for idx, line in enumerate(SUMMARY_SLIDE_TEXT[2:]):
        p = body.paragraphs[0] if idx == 0 else body.add_paragraph()
        p.text = line
        p.level = 0
        p.bullet = True
        p.space_after = Pt(8)
        p.font.size = Pt(16)

    fill = new_slide.background.fill
    fill.solid()
    fill.fore_color.rgb = RGBColor(248, 249, 252)

    slide_id_list = prs.slides._sldIdLst
    last_slide_id = slide_id_list[-1]
    slide_id_list.remove(last_slide_id)
    slide_id_list.insert(0, last_slide_id)

    prs.save(path)


def apply_replacements_to_pptx(path):
    prs = Presentation(path)
    for slide in prs.slides:
        for shape in slide.shapes:
            if shape.has_text_frame:
                for para in shape.text_frame.paragraphs:
                    collapse_paragraph_with_replacement(para, REPLACEMENTS)
    prs.save(path)


def verify_clean(path):
    prs = Presentation(path)
    bad_strings = []
    stale = [
        "Keboard Shortcuts",
        "Seach",
        "whey",
        "Show the hint of the history whey typing the work to search",
        "The Tool Solution: Merged The file in the tool with open add file or drag to merged.",
        "Two-layer search: first filter by tag, then search by keyword within the tag-filtered results",
        "Write some script(.bat, .py, …) to merged the log fist",
    ]
    for slide in prs.slides:
        for shape in slide.shapes:
            if shape.has_text_frame:
                text = shape.text_frame.text
                 for old in stale:
                    if old.lower() in text.lower():
                        bad_strings.append(old)
    print(f"Remaining bad phrases: {len(bad_strings)}")
    if bad_strings:
        for item in bad_strings[:10]:
            print(' -', item)


add_summary_slide(FINAL_PATH)
apply_replacements_to_pptx(FINAL_PATH)
verify_clean(FINAL_PATH)
print("done")
