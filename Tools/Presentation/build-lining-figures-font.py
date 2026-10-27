"""Build the lining-figures copy of Cormorant Garamond SemiBold used by the HUD's target number.

Cormorant Garamond's default digits are old-style: 3, 5, 7 and 9 descend below the baseline and 6 and 8 rise
above the x-height. Unity's legacy UI.Text cannot apply the OpenType lnum feature, so this script writes a copy
of Dice-Unity/Assets/Dice/Presentation/Shared/Fonts/CormorantGaramond-SemiBold.ttf whose character map sends
U+0030-U+0039 to the font's own lining glyphs zero.lf ... nine.lf in every cmap subtable that maps digits. The
copy is renamed "Cormorant Garamond Lining SemiBold" (PostScript name CormorantGaramondLining-SemiBold) in the
family, unique, full, PostScript and typographic family name records. The copyright, license and every other
record, glyph and table are kept unchanged. The source is licensed under the SIL Open Font License 1.1 without a
Reserved Font Name; CormorantGaramond-OFL.txt beside the fonts covers this modified version too.

Python 3.9 or later with fontTools (install it into an ignored folder, for example
python -m pip install --target Docs/work/python-fonttools fonttools, and put that folder on PYTHONPATH).
Run from any directory:

  python Tools/Presentation/build-lining-figures-font.py
  python Tools/Presentation/build-lining-figures-font.py --write-assets

The default writes a fresh ignored Docs/work/lining-figures-font-* directory; --output chooses an unused directory
under Docs/work. --write-assets writes CormorantGaramondLining-SemiBold.ttf beside the source font and keeps its
.meta file if one exists. The source font is only read, so repeated runs give the same font.
"""

import argparse
from pathlib import Path
import tempfile

from fontTools.ttLib import TTFont

ROOT = Path(__file__).resolve().parents[2]
FONTS = ROOT / "Dice-Unity/Assets/Dice/Presentation/Shared/Fonts"
WORK = ROOT / "Docs/work"
SOURCE = "CormorantGaramond-SemiBold.ttf"
TARGET = "CormorantGaramondLining-SemiBold.ttf"

DIGITS = range(0x30, 0x3A)
LINING = ["zero.lf", "one.lf", "two.lf", "three.lf", "four.lf", "five.lf", "six.lf", "seven.lf", "eight.lf", "nine.lf"]

# Family, unique, full, PostScript and typographic family names carry the family name; subfamily records
# (2 and 17) keep their style names.
RENAMED_RECORDS = (1, 3, 4, 6, 16)
FAMILY = ("Cormorant Garamond", "Cormorant Garamond Lining")
POSTSCRIPT = ("CormorantGaramond", "CormorantGaramondLining")
EXPECTED = {
    1: "Cormorant Garamond Lining SemiBold",
    4: "Cormorant Garamond Lining SemiBold",
    6: "CormorantGaramondLining-SemiBold",
    16: "Cormorant Garamond Lining",
}


def rename(text):
    if POSTSCRIPT[0] in text:
        return text.replace(POSTSCRIPT[0], POSTSCRIPT[1])
    return text.replace(FAMILY[0], FAMILY[1])


def build(source, target):
    font = TTFont(source, recalcBBoxes=False, recalcTimestamp=False)
    glyphs = set(font.getGlyphOrder())
    missing = [name for name in LINING if name not in glyphs]
    if missing:
        raise ValueError(f"{source.name} has no lining glyphs {missing}.")

    remapped = 0
    for table in font["cmap"].tables:
        if not any(code in table.cmap for code in DIGITS):
            continue
        if not all(code in table.cmap for code in DIGITS):
            raise ValueError(f"cmap subtable {table.platformID}/{table.platEncID} maps only some digits.")
        for code, name in zip(DIGITS, LINING):
            table.cmap[code] = name
        remapped += 1
    if remapped == 0:
        raise ValueError(f"{source.name} has no cmap subtable that maps the digits.")

    names = font["name"]
    for record in names.names:
        if record.nameID not in RENAMED_RECORDS:
            continue
        text = record.toUnicode()
        if FAMILY[0] not in text and POSTSCRIPT[0] not in text:
            raise ValueError(f"Name record {record.nameID} does not carry the source family name: {text!r}")
        names.setName(rename(text), record.nameID, record.platformID, record.platEncID, record.langID)

    font.save(target)
    font.close()
    return remapped


def verify(source, target):
    """Reread both fonts and return report lines; raise if the copy differs from the source other than intended."""
    original = TTFont(source, lazy=True)
    built = TTFont(target, lazy=True)
    lines = []
    for table in built["cmap"].tables:
        if any(code in table.cmap for code in DIGITS):
            mapped = [table.cmap[code] for code in DIGITS]
            if mapped != LINING:
                raise ValueError(f"cmap {table.platformID}/{table.platEncID} digits map to {mapped}.")
            lines.append(f"cmap {table.platformID}/{table.platEncID} format {table.format}: U+0030-U+0039 -> {', '.join(mapped)}")
    for old, new in zip(original["cmap"].tables, built["cmap"].tables):
        changed = {code for code in set(old.cmap) | set(new.cmap) if old.cmap.get(code) != new.cmap.get(code)}
        if not changed <= set(DIGITS):
            raise ValueError(f"cmap {new.platformID}/{new.platEncID} changed beyond the digits: {sorted(changed)}")

    for record in built["name"].names:
        before = original["name"].getName(record.nameID, record.platformID, record.platEncID, record.langID)
        text = record.toUnicode()
        expected = EXPECTED.get(record.nameID)
        if expected is not None and text != expected:
            raise ValueError(f"Name record {record.nameID} is {text!r}, expected {expected!r}.")
        if record.nameID not in RENAMED_RECORDS and (before is None or before.toUnicode() != text):
            raise ValueError(f"Name record {record.nameID} changed.")
        if record.nameID in (0, 1, 2, 3, 4, 6, 13, 14, 16, 17):
            lines.append(f"name {record.nameID} ({record.platformID}/{record.platEncID}/{record.langID}): {text}")

    if sorted(original.keys()) != sorted(built.keys()):
        raise ValueError("The table set changed.")
    for tag in original.keys():
        if tag in ("GlyphOrder", "cmap", "name", "head"):
            continue
        if original.reader[tag] != built.reader[tag]:
            raise ValueError(f"Table {tag} changed.")
    head_before, head_after = bytearray(original.reader["head"]), bytearray(built.reader["head"])
    head_before[8:12] = head_after[8:12] = b"\0\0\0\0"  # checksumAdjustment covers the whole file.
    if head_before != head_after:
        raise ValueError("Table head changed beyond its checksum adjustment.")
    lines.append("Every other table is byte-identical to the source (head apart from checksumAdjustment).")
    original.close()
    built.close()
    return lines


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--output", help="unused directory under Docs/work for the built font")
    parser.add_argument("--write-assets", action="store_true", help="write the font beside the source font")
    args = parser.parse_args()
    if args.write_assets:
        targets = FONTS
    elif args.output:
        targets = (ROOT / args.output).resolve()
        if WORK.resolve() not in targets.parents:
            raise ValueError("--output must be under Docs/work.")
        targets.mkdir(parents=True, exist_ok=False)
    else:
        WORK.mkdir(parents=True, exist_ok=True)
        targets = Path(tempfile.mkdtemp(prefix="lining-figures-font-", dir=WORK))
    source, target = FONTS / SOURCE, targets / TARGET
    remapped = build(source, target)
    for line in verify(source, target):
        print(line)
    print(f"Remapped the digits in {remapped} cmap subtables: wrote {target}")


if __name__ == "__main__":
    main()
