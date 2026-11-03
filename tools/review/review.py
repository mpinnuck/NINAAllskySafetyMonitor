#!/usr/bin/env python3
"""Review the frames a replay picked (REQ-62).

    python3 tools/review/review.py ~/AllskyFrames/Replay/run1

Without a folder, asks for one. Needs Pillow and openpyxl: pip3 install -r tools/review/requirements.txt
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from allsky_review.review_app import main  # noqa: E402

if __name__ == "__main__":
    sys.exit(main())
