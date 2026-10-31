# Process-I/O Mapping Source Provenance — 2026-10-22 Artifact Adoption (H2)

Status: **[PROVENANCE RECORD — EXACT BYTES VERIFIED].** This record documents the
adoption of the Owner-supplied Process-I/O mapping HTML artifact into the
repository, with complete connected-source identity, byte-identity proof and
parsing provenance. It is an evidence record only: it authorises no polling, no
connection, no write, no commissioning and no production use. It grants no
runtime behaviour, control effect, address, scaling or equipment identity to any
row (see the companion H2 Final Scope Gate Report).

| Item | Value |
|---|---|
| Exact source filename | `WAGO_IO_Mapping_Exact_Excel_Cyberpunk_PositiveIOSlot_2026-10-22.html` |
| Repository artifact path | [`WAGO_IO_Mapping_Exact_Excel_Cyberpunk_PositiveIOSlot_2026-10-22.html`](WAGO_IO_Mapping_Exact_Excel_Cyberpunk_PositiveIOSlot_2026-10-22.html) (this directory; exact filename preserved) |
| Connected-source provider | Google Drive connected source (repository external; access via the session connector only) |
| Connected file identity | file id `1GPig0jsZCLNbdgvBNR6a0LeXUdDHUjQd`, MIME `text/html`, in folder `13mRy81IIThPMEAjNx7PdUa3ktFZw5Dul` ("Arena AI Share Foler"); not trashed; not a shortcut; exactly one file of this exact name found (exact-name search returned 1 result) |
| Retrieved timestamp | 2026-10-26 (session retrieval; two independent downloads performed) |
| Source created timestamp (provider) | `2026-10-26T07:14:18.832Z` |
| Source modified timestamp (provider) | `2026-10-22T07:29:55.460Z` |
| Source byte size | 36,667 bytes |
| Source SHA-256 (provider metadata) | `28245ad207a24078902fca48f4fa2622fbca631bd92f660aa4faf1681f4c5299` |
| Source MD5 (provider metadata) | `09522589f6f6617e7cdedeef7ee98b9d` |
| Source SHA-1 (provider metadata) | `0ea7ea9fc225c08c70e023804306141b9b894203` |
| Repository artifact byte size | 36,667 bytes |
| Repository artifact SHA-256 | `28245ad207a24078902fca48f4fa2622fbca631bd92f660aa4faf1681f4c5299` |
| Exact-byte-match result | **TRUE — proven** (SHA-256 + MD5 + SHA-1 + byte size all match the provider metadata; see §1) |
| Upstream Excel workbook bytes available | **NO.** Only the HTML artifact exists in the connected source. The workbook `WAGO_IO_Mapping(Update19-10-26)_2.xlsx` was not found as a connected file and its bytes were not retrieved. |
| Source workbook identity independently verified | **NO.** The artifact's embedded header states `Source: WAGO_IO_Mapping(Update19-10-26)_2.xlsx · SHA-256 4937dda8dcae · 26,325 bytes`. The shown digest is a 12-hex-character prefix (truncated) and the workbook bytes are unavailable, so the workbook identity is recorded **as declared, NOT independently verified**. |
| Artifact purpose | Immutable evidence source for the complete Process-I/O (AI/DI/DO/AO) inventory of the Process-I/O rack, adopted as the H2 scope-gate evidence baseline; input to the deterministic normalized inventory only. |
| Owner authorization basis | Owner instruction of 2026-10-26 (H2 Physical Modbus I/O Evidence Adoption and Final Scope Gate): explicit authorization to adopt this exact connected-source artifact into repository evidence with complete provenance and byte-identity proof; read-only analysis; no product runtime behaviour; no device contact. |
| Parsing status | **PARSED — deterministic.** Generator-owned normalized inventory at [`process-io-inventory-2026-10-22.csv`](process-io-inventory-2026-10-22.csv), produced by [`tools/process-io-evidence/generate.mjs`](../../tools/process-io-evidence/generate.mjs) (fail-closed; parity mode; 15 focused tests green in the authoring session). |
| Total extracted row count | **111** (all rows preserved in complete source order; no row dropped, corrected or reordered) |
| Row counts by I/O class (DERIVED) | DI 40 · DO 32 · AI 36 · AO 2 · End module 1 · UNRESOLVED 0 |
| Row counts by source `Status` | USED 105 · SPARE 6 |
| Tagged / untagged rows | 108 tagged · 3 untagged (2 spare channel rows + 1 end-module row) |
| Filtering statement found inside the artifact | `Filtered I/O Slot less than 0, equal to 0, and blank` (embedded header sub-line; verbatim) |
| Known limitations | See §3 |
| Provenance status | **VERIFIED — exact bytes, single unambiguous source file, deterministic parse.** |

**Binding rule.** No value in this record is invented. Provider metadata values
are quoted as reported by the connected source. The artifact's internal
statements (source workbook name, digest prefix, byte size, filter) are quoted
verbatim from the artifact and are **declarations of the artifact**, not
independently verified facts. No credentials, access tokens, connector URLs or
signed URLs are recorded anywhere in this document or the artifacts.

---

## 1. Byte-identity proof and the retrieval transformation (full disclosure)

The connector's file-download path fetched the artifact through a proxy that
injects Cloudflare Rocket Loader material into served HTML. The raw staged
downloads therefore did **not** equal the source bytes:

1. The first staged download was 36,857 bytes, SHA-256
   `a10acc97760fa68d1d8a7d2d2f04ac888b197f6191e6394412abfefd3b466378` —
   **not** the source bytes (190 bytes larger).
2. A second, independent download staged 36,857 bytes with a **different**
   SHA-256 (`85db4b5948d9b4fff11bc855646dd4b2371047811ef6c5331f97c62c4d71a665`),
   proving the injected material is per-fetch dynamic, not part of the source.
3. The two injected modifications were identified exactly and were the only
   differences (independently corroborated by the connector's native text
   extraction path, which showed the original ends `…</script></body></html>`
   with a plain `<script>` tag and no injected material):
   - the page's single inline `<script>` tag was rewritten to
     `<script type="<per-fetch hex nonce>-text/javascript">` (+48 bytes for a
     24-hex-nonce fetch), and
   - `<script src="/cdn-cgi/scripts/…/cloudflare-static/rocket-loader.min.js"
     data-cf-settings="<same nonce>-|49" defer></script>` was injected
     immediately before `</body></html>` (+142 bytes).
4. Reversing exactly those two modifications (restoring `<script>`; removing the
   injected tag; nonces verified equal within a fetch) yields 36,667 bytes
   matching the provider metadata on **all three** digest algorithms and the
   exact byte size:
   SHA-256 `28245ad207a24078902fca48f4fa2622fbca631bd92f660aa4faf1681f4c5299` ✓ ·
   MD5 `09522589f6f6617e7cdedeef7ee98b9d` ✓ ·
   SHA-1 `0ea7ea9fc225c08c70e023804306141b9b894203` ✓ · size 36,667 ✓.
5. The repository copy was written from those verified bytes; its SHA-256 was
   re-computed after writing and re-matched. A three-algorithm digest match
   makes any other content than the source bytes computationally implausible.

The transformed staged copies were **not** committed. The generator
(`--check` parity mode) re-verifies the artifact hash
`28245ad2…` on every run and refuses a modified artifact.

## 2. Repository artifacts owned by this adoption

| Path | Role |
|---|---|
| `docs/evidence/WAGO_IO_Mapping_Exact_Excel_Cyberpunk_PositiveIOSlot_2026-10-22.html` | Immutable source copy (exact bytes; never reformatted, minified, prettified, sanitized, line-ending-normalized or rewritten) |
| `docs/evidence/process-io-inventory-2026-10-22.csv` | Deterministic normalized inventory (generator-owned; every row and the complete source order preserved; DERIVED columns explicitly marked) |
| `tools/process-io-evidence/generate.mjs` | Deterministic parser/generator (fail-closed; hash-verified input; parity mode) |
| `tools/process-io-evidence/test/generate.test.mjs` | Focused artifact/parser tests (15 tests) |
| `docs/evidence/process-io-source-provenance-2026-10-22.md` | This provenance record |
| `docs/H2_FINAL_SCOPE_GATE_REPORT.md` | Companion H2 Final Scope Gate analysis (read-only; consumes this evidence) |

## 3. Known limitations

1. **Declared row filter.** The artifact itself states
   `Filtered I/O Slot less than 0, equal to 0, and blank`. Rows whose "I/O Slot"
   is negative, zero or blank are therefore **not present** in the artifact. Per
   the committed authoritative rack topology (23 physical RackSlots), the rows
   absent by this filter are the non-process modules with no configuration-tool
   position: RackSlot 1 (750-362 fieldbus coupler), RackSlot 2 (750-601 power
   supply) and RackSlot 12 (750-613 system power supply). The end module
   (RackSlot 23, 750-600, I/O Slot 20) **is** present. The complete inventory
   therefore means: complete **within the artifact's declared filter**.
2. **Upstream workbook not available.** The declared source workbook
   (`WAGO_IO_Mapping(Update19-10-26)_2.xlsx`, 26,325 bytes, digest prefix
   `4937dda8dcae`) was not available through the connected source; its identity
   is recorded as declared only, and the filtered-out rows cannot be recovered
   from this artifact.
3. **Wiring facts are Owner declarations.** Every row value (module, slot,
   I/O slot, channel, tag, description, I/O type, terminal, From/To/Ref,
   location, contact note, status) is preserved verbatim, including source
   spelling ("Auxilary", "BRAEKER", " contractor"), Thai text, trailing-`+` tag
   suffixes (AI-001+ … AI-011+, AO-001+) and unresolved placeholder
   descriptions ("XXX รอพี่ยิมตอบ"). Nothing was corrected, translated or
   interpreted. Physical truth of any wiring statement is NOT verified here.
4. **No address, scaling, polarity, function-code or endpoint evidence** is
   contained in this artifact, and none was invented. The artifact carries
   physical-position columns only (Slot / I/O Slot / Channel / Terminal); the
   process-image, transport and equipment-identity questions are analysed in
   the companion H2 Final Scope Gate Report with explicit evidence states.
5. **Version relationship to the committed authoritative workbook.** The
   previously committed workbook `T8_IO_Card_Mapping.xlsx` (SHA-256
   `4e0337e25c8377c01559f264653baab25bcfa23f4d3e071fdc8c80896f422e8e`, 16,648
   bytes, 114 data rows) remains the Mapping-Configuration authoritative
   default. The adopted artifact derives from a **newer declared workbook
   revision** (Update19-10-26); the observed cross-version relationship is
   recorded in the H2 Final Scope Gate Report §10.5. This adoption does not
   change the Mapping Configuration workbook or any product source.
