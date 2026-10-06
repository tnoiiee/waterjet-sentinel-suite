#!/usr/bin/env python3
"""WJSS Stage 0.2.1A — reproducible Google Sans Latin subset (spike tooling, not a runtime dependency).

Input : the official, unmodified Google Fonts file GoogleSans[GRAD,opsz,wght].ttf from
        https://github.com/google/fonts/tree/<ref>/ofl/googlesans (SIL OFL 1.1, no Reserved Font Name).
Output: GoogleSans-Latin-Variable.woff2 (variable wght 400..700; GRAD fixed 0; opsz fixed 18).

Tooling (outside the repository, not a project dependency):
    python3 -m venv .venv && .venv/bin/pip install fonttools==4.60.1 brotli==1.1.0
Usage:
    .venv/bin/python build_google_sans_subset.py <input.ttf> <output.woff2>

Order matters for speed: subset first (8 311 -> ~877 glyphs), then instance GRAD/opsz.
"""
import hashlib
import sys

from fontTools import subset
from fontTools.ttLib import TTFont
from fontTools.varLib import instancer

# Basic Latin, Latin-1 Supplement, Latin Extended-A, plus the punctuation and symbols the
# Operations UI renders (dashes, quotes, bullet, ellipsis, arrows, minus, warning triangle,
# check mark, multiplication x, less/greater-or-equal).
UNICODES = (
    list(range(0x20, 0x7F))
    + list(range(0xA0, 0x180))
    + [0x2013, 0x2014, 0x2018, 0x2019, 0x201C, 0x201D, 0x2022, 0x2026,
       0x2190, 0x2191, 0x2192, 0x2193, 0x2212, 0x25B2, 0x2713, 0x2715, 0x2264, 0x2265]
)


def main(src: str, dst: str) -> None:
    opts = subset.Options()
    opts.layout_features = ["*"]  # keep every OpenType feature (tnum, kern, liga, case, ...)
    opts.name_IDs = ["*"]  # keep copyright / licence / licence-URL name records
    opts.name_languages = ["*"]
    opts.notdef_outline = True
    # Deterministic output: keep the original head.modified timestamp (no build-time stamp).
    font = TTFont(src, recalcTimestamp=False)
    sub = subset.Subsetter(opts)
    sub.populate(unicodes=UNICODES)
    sub.subset(font)
    font = instancer.instantiateVariableFont(font, {"GRAD": 0, "opsz": 18})
    font.recalcTimestamp = False
    font.flavor = "woff2"
    font.save(dst)
    check = TTFont(dst)
    axes = [(a.axisTag, a.minValue, a.maxValue) for a in check["fvar"].axes]
    feats = sorted({fr.FeatureTag for fr in check["GSUB"].table.FeatureList.FeatureRecord})
    assert axes == [("wght", 400.0, 700.0)], axes
    assert "tnum" in feats
    assert check["name"].getDebugName(1) == "Google Sans"
    print("axes", axes)
    print("glyphs", len(check.getGlyphOrder()), "cmap", len(check.getBestCmap()))
    print("GSUB features", " ".join(feats))
    print("copyright", check["name"].getDebugName(0))
    print("licence URL", check["name"].getDebugName(14))
    with open(dst, "rb") as fh:
        data = fh.read()
    print("output bytes", len(data), "sha256", hashlib.sha256(data).hexdigest())


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
