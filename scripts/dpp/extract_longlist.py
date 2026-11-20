"""Extract the attribute rows of the DPP V3 longlist workbook into JSON for the compliance test.

Usage:
    python -I scripts/dpp/extract_longlist.py <V3 workbook .xlsx> BatteryPassWeb.Tests/Dpp/dpp-v3-longlist-extract.json

Standard library only. Reads the tab "Data attribute longlist_v2.0" and stops with an error if the
header layout differs from the one this script was written for, so a new file version can't slip
through unnoticed. Re-run it whenever the file changes, then run the compliance test.
"""
import hashlib
import json
import re
import sys
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path

SHEET = "Data attribute longlist_v2.0"
MAIN = "{http://schemas.openxmlformats.org/spreadsheetml/2006/main}"
DOC_REL = "{http://schemas.openxmlformats.org/officeDocument/2006/relationships}"
PKG_REL = "{http://schemas.openxmlformats.org/package/2006/relationships}"

# Column letter -> (key in the extract, expected start of the column's header text).
COLUMNS = {
    "B": ("no", "#"),
    "F": ("oi", "Other Industrial"),
    "J": ("feb27", "Mandatory from Feb 2027"),
    "M": ("attribute", "Attribute"),
    "U": ("unit", "Unit of attribute"),
    "V": ("format", "Data format"),
    "W": ("access", "Access rights"),
    "X": ("staticDynamic", "Data behavioural characteristic"),
    "Y": ("updateRequirement", "Update requirement"),
    "Z": ("granularity", "Granularity level I"),
}


def shared_strings(book):
    if "xl/sharedStrings.xml" not in book.namelist():
        return []
    strings = []
    for item in ET.fromstring(book.read("xl/sharedStrings.xml")).findall(f"{MAIN}si"):
        parts = []
        for child in item:  # plain text or rich-text runs; phonetic runs (rPh) are skipped
            if child.tag == f"{MAIN}t":
                parts.append(child.text or "")
            elif child.tag == f"{MAIN}r":
                parts.extend(t.text or "" for t in child.findall(f"{MAIN}t"))
        strings.append("".join(parts))
    return strings


def sheet_path(book, name):
    workbook = ET.fromstring(book.read("xl/workbook.xml"))
    ids = [s.get(f"{DOC_REL}id") for s in workbook.iter(f"{MAIN}sheet") if s.get("name") == name]
    if not ids:
        raise SystemExit(f"tab {name!r} not found")
    rels = ET.fromstring(book.read("xl/_rels/workbook.xml.rels"))
    target = next(r.get("Target") for r in rels.iter(f"{PKG_REL}Relationship") if r.get("Id") == ids[0])
    return target.lstrip("/") if target.startswith("/") else "xl/" + target


def sheet_rows(book, path, strings):
    for row in ET.fromstring(book.read(path)).iter(f"{MAIN}row"):
        cells = {}
        for cell in row.findall(f"{MAIN}c"):
            column = re.match(r"[A-Z]+", cell.get("r")).group(0)
            value = cell.find(f"{MAIN}v")
            kind = cell.get("t")
            if kind == "s":
                text = strings[int(value.text)] if value is not None else ""
            elif kind == "inlineStr":
                text = "".join(t.text or "" for t in cell.iter(f"{MAIN}t"))
            else:
                text = value.text if value is not None and value.text else ""
            cells[column] = text
        yield cells


def main(xlsx, out):
    data = Path(xlsx).read_bytes()
    with zipfile.ZipFile(xlsx) as book:
        rows = list(sheet_rows(book, sheet_path(book, SHEET), shared_strings(book)))
    header = next((cells for cells in rows if cells.get("B", "").strip() == "#"), None)
    if header is None:
        raise SystemExit("header row ('#' in column B) not found")
    for column, (_, expected) in COLUMNS.items():
        found = header.get(column, "").strip()
        if not found.startswith(expected):
            raise SystemExit(f"column {column}: expected a header starting {expected!r}, found {found!r}")
    records = []
    for cells in rows:
        number = cells.get("B", "").strip()
        if not re.fullmatch(r"\d+(\.0)?", number):
            continue
        record = {key: cells.get(column, "").strip() for column, (key, _) in COLUMNS.items()}
        record["no"] = int(float(number))
        records.append(record)
    records.sort(key=lambda r: r["no"])
    document = {
        "source": {
            "file": Path(xlsx).name,
            "sha256": hashlib.sha256(data).hexdigest(),
            "sheet": SHEET,
            "extractedBy": "scripts/dpp/extract_longlist.py",
        },
        "rows": records,
    }
    Path(out).write_text(json.dumps(document, indent=1, ensure_ascii=True) + "\n", encoding="utf-8")
    print(f"{len(records)} rows written to {out}")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit(__doc__)
    main(sys.argv[1], sys.argv[2])
