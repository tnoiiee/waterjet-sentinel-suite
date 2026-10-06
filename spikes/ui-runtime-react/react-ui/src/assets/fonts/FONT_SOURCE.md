# Google Sans — font source, licence, and processing record (Stage 0.2.1A spike)

> SYNTHETIC FEASIBILITY SPIKE asset. Self-hosted for the React Operations spike UI only. No
> runtime CDN, no external CSS, no Internet access at runtime.

## Official source

| Item | Value |
| --- | --- |
| Official source | Google Fonts repository — <https://github.com/google/fonts/tree/7085eb89a950e85db5b166b7a58d414544b4140c/ofl/googlesans> |
| Repository owner | `google` (GitHub Organization) |
| Immutable reference | `google/fonts` commit `7085eb89a950e85db5b166b7a58d414544b4140c` (`main` at download time); last change to `ofl/googlesans`: commit `a0e3dbcdc3a3ecfafff3f071159ae0221628922d` (2026-09-24) |
| Retrieval method | GitHub REST contents API (`Accept: application/vnd.github.raw`) at the immutable reference above |
| Download date | 2026-10-06 |
| Family name (name ID 1) | `Google Sans` |
| Font version (name ID 5) | `Version 14.000;[08f2ac800]` |

## Original file (not committed)

| Item | Value |
| --- | --- |
| Original filename | `GoogleSans[GRAD,opsz,wght].ttf` |
| Original size | 4,974,940 bytes |
| Original SHA-256 | `d0a87d835a944b8b40d0e82a5651bb59ab97b936a2aeed5946eb57e7b2a3a90a` |
| Official Git blob (tree entry at the reference) | `a5e76cc713206f1cfe9a7fc31cc1b6f035034d81` — **matches** `git hash-object` of the downloaded file |
| Axes | `opsz` 17–18 (default 18), `wght` 400–700 (default 400), `GRAD` −50–200 (default 0) |
| Glyphs / mapped code points | 8,311 / 3,749 |

The original TTF and the italic file are **not** committed (unused formats are excluded).

## Repository files

| File | Size (bytes) | SHA-256 | Note |
| --- | --- | --- | --- |
| `GoogleSans-Latin-Variable.woff2` | 47,672 | `40f917d9d0a4de0577c69089456c9e68d8ad3bbf58ac1f8ac91730538cb1531b` | Subset + instanced (see below) |
| `OFL.txt` | 4,488 | `2b75ef20f13d83a7514aee452c4782c20cdc9ff2dee17600f44d37a06d4fb958` | Verbatim from the package (Git blob `035131accc0d7bc0ec5880f906800ebe1b1f854f`) |
| `TRADEMARKS.md` | 1,470 | `215bc8c1ce46d1b81531f33ebb5af201e7a5da76b8f3ae4c705336fd70fc89e1` | Verbatim from the package (Git blob `621fbbd85d6e346f46b0ef4f0658d3cfba7e3f80`) |

## Processing (reproducible)

- Tooling (outside the repository; **not** a project dependency): Python venv with
  `fonttools==4.60.1`, `brotli==1.1.0`.
- Script: [`measurements/font/build_google_sans_subset.py`](../../../../measurements/font/build_google_sans_subset.py).
- Command: `.venv/bin/python measurements/font/build_google_sans_subset.py GoogleSans[GRAD,opsz,wght].ttf react-ui/src/assets/fonts/GoogleSans-Latin-Variable.woff2`
- Steps: (1) `fontTools.subset` with `layout_features=*`, `name_IDs=*`, `name_languages=*`,
  `notdef_outline=True`; (2) `fontTools.varLib.instancer` pinning `GRAD=0`, `opsz=18`;
  (3) WOFF2 (Brotli) flavour; `recalcTimestamp=False` keeps the output byte-deterministic
  (three independent builds produced the identical SHA-256 above).
- Unicode scope (335 code points, 877 glyphs): U+0020–007E, U+00A0–017F, and U+2013, 2014,
  2018, 2019, 201C, 201D, 2022, 2026, 2190–2193, 2212, 25B2, 2713, 2715, 2264, 2265.
- Weight range: variable `wght` 400–700. **Grade fixed at 0; optical size fixed at 18** (default).
- OpenType features retained (GSUB): c2sc calt case dlig dnom frac liga lnum locl numr ordn
  pnum pref sinf smcp ss01 ss02 ss03 ss04 ss05 ss08 ss09 subs sups **tnum** zero (GPOS kern
  retained). Tabular numerals (`tnum`) are supported and used for numeric values.
- Copyright and licence name records (IDs 0, 13, 14) are preserved in the output.

## Licence conclusion

| Question | Result |
| --- | --- |
| Licence identifier | `OFL-1.1` (SIL Open Font License 1.1), text in `OFL.txt` |
| Reserved Font Name | **None** — the copyright statement declares no Reserved Font Name (the term appears only in the licence definitions) |
| Redistribution | Permitted, bundled or embedded with software, provided `OFL.txt` (copyright + licence) accompanies the font; the font may not be sold by itself |
| Public-repository redistribution | Permitted under the same conditions |
| Web embedding / self-hosting | Permitted (the font is distributed with the application bundle) |
| Modification / subsetting | Permitted; a Modified Version must stay under OFL-1.1. No Reserved Font Name, so the family name may be kept |
| This file | **Modified Version**: subset to Latin + listed symbols, GRAD/opsz instanced, WOFF2-compressed; remains OFL-1.1 |
| Trademark | “Google” and “Google Sans” are trademarks of Google LLC (`TRADEMARKS.md`, shipped alongside). The font name is used only to identify the font; it is not used in the product name and implies no affiliation with or sponsorship by Google LLC |
| Runtime loading | Local WOFF2 via `@font-face` (Vite-managed asset URL) and a preload; no CDN, no external CSS |

This record documents licence compliance for a spike asset. It is not legal advice; Production
font selection remains a separate decision.
