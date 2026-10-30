"""Rasterize the authored monochrome SVG subset for Unity charm sprites (Pillow)."""
import argparse
import json
import math
import re
import uuid
import xml.etree.ElementTree as ET
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
SOURCE = Path(__file__).with_name("modifier-icons")
NAMESPACE = uuid.UUID("842c9c99-bb07-4d8f-b347-6f835576715b")
SCALE = 16


def guid(name):
    return uuid.uuid5(NAMESPACE, name).hex


def paths(data):
    tokens = re.findall(r"[A-Za-z]|[-+]?(?:\d*\.\d+|\d+\.?\d*)(?:[eE][-+]?\d+)?", data)
    index, command, current, start, points = 0, None, (0, 0), (0, 0), []
    while index < len(tokens):
        if tokens[index].isalpha():
            command = tokens[index]
            index += 1
        if command not in ("M", "L", "C", "Q", "Z"):
            raise ValueError("Unsupported SVG command: " + str(command))
        if command == "Z":
            if points:
                yield points + [start], True
            points, current, command = [], start, None
            continue
        count = {"M": 2, "L": 2, "C": 6, "Q": 4}[command]
        values = list(map(float, tokens[index:index + count]))
        if len(values) != count:
            raise ValueError("Incomplete SVG path")
        index += count
        end = tuple(values[-2:])
        if command == "M":
            if points:
                yield points, False
            points, start, command = [end], end, "L"
        elif command == "L":
            points.append(end)
        else:
            for step in range(1, 33):
                t, u = step / 32, 1 - step / 32
                if command == "C":
                    points.append(tuple(u ** 3 * current[a] + 3 * u * u * t * values[a]
                                        + 3 * u * t * t * values[a + 2] + t ** 3 * end[a] for a in (0, 1)))
                else:
                    points.append(tuple(u * u * current[a] + 2 * u * t * values[a]
                                        + t * t * end[a] for a in (0, 1)))
        current = end
    if points:
        yield points, False


def render(source):
    root = ET.parse(source).getroot()
    if root.get("viewBox") != "0 0 64 64":
        raise ValueError(f"Expected 64-unit viewBox: {source}")
    alpha = Image.new("L", (64 * SCALE, 64 * SCALE))
    draw = ImageDraw.Draw(alpha)

    def contour(points, closed, style):
        pixels = [(x * SCALE, y * SCALE) for x, y in points]
        fill, stroke = style.get("fill", "none"), style.get("stroke", "none")
        for color in (fill, stroke):
            if color not in ("none", "white", "#fff", "#ffffff", "#FFFFFF"):
                raise ValueError(f"Only white/none paint is supported: {source}, {color}")
        if fill != "none" and len(points) > 2:
            draw.polygon(pixels, fill=255)
        if stroke != "none":
            width = float(style.get("stroke-width", 3.2)) * SCALE
            draw.line(pixels, fill=255, width=round(width), joint="curve")
            # Round joins and caps remain stable after downsampling.
            for x, y in pixels:
                draw.ellipse((x - width / 2, y - width / 2, x + width / 2, y + width / 2), fill=255)

    def visit(node, inherited):
        tag = node.tag.split("}")[-1]
        if tag in ("title", "desc"):
            return
        if any(key in node.attrib for key in ("transform", "style", "filter", "opacity")):
            raise ValueError(f"Unsupported SVG styling: {source}")
        style = dict(inherited)
        style.update(node.attrib)
        number = lambda key, default=0: float(node.get(key, default))
        if tag in ("svg", "g"):
            for child in node:
                visit(child, style)
        elif tag == "path":
            for points, closed in paths(node.attrib["d"]):
                contour(points, closed, style)
        elif tag in ("circle", "ellipse"):
            rx = number("r") if tag == "circle" else number("rx")
            ry = rx if tag == "circle" else number("ry")
            points = [(number("cx") + rx * math.cos(i * math.tau / 96),
                       number("cy") + ry * math.sin(i * math.tau / 96)) for i in range(97)]
            contour(points, True, style)
        elif tag == "line":
            contour([(number("x1"), number("y1")), (number("x2"), number("y2"))], False, style)
        elif tag == "rect":
            x, y, w, h = number("x"), number("y"), number("width"), number("height")
            if number("rx") or number("ry"):
                raise ValueError("Use paths for rounded rectangles")
            contour([(x, y), (x + w, y), (x + w, y + h), (x, y + h), (x, y)], True, style)
        elif tag in ("polyline", "polygon"):
            values = list(map(float, re.split(r"[\s,]+", node.attrib["points"].strip())))
            points = list(zip(values[::2], values[1::2]))
            if tag == "polygon":
                points.append(points[0])
            contour(points, tag == "polygon", style)
        else:
            raise ValueError(f"Unsupported element: {source}, {tag}")

    visit(root, {})
    if alpha.getbbox() is None:
        raise ValueError(f"Empty artwork: {source}")
    image = Image.new("RGBA", (256, 256), (255, 255, 255, 0))
    image.putalpha(alpha.resize(image.size, Image.Resampling.LANCZOS))
    return image


def sprite_meta(name):
    return f"""fileFormatVersion: 2
guid: {guid(name)}
TextureImporter:
  serializedVersion: 13
  mipmaps:
    enableMipMap: 0
    sRGBTexture: 1
  isReadable: 0
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    wrapU: 1
    wrapV: 1
    wrapW: 1
  maxTextureSize: 256
  textureCompression: 0
  spriteMode: 1
  spritePixelsToUnits: 100
  spriteMeshType: 0
  spriteExtrude: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  alphaIsTransparency: 1
  textureType: 8
  textureShape: 1
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 256
    textureFormat: -1
    textureCompression: 0
    overridden: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: {guid(name + '.sprite')}
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, help="Fresh preview directory under Docs/work")
    parser.add_argument("--write-assets", action="store_true", help="Write production PNGs and explicit presentation references")
    args = parser.parse_args()
    output = (args.output or ROOT / "Docs/work" / ("modifier-icons-" + uuid.uuid4().hex)).resolve()
    if not output.is_relative_to((ROOT / "Docs/work").resolve()) or output.exists():
        raise ValueError("Output must be a fresh directory under Docs/work")
    catalog = json.loads((SOURCE / "catalog.json").read_text())
    images = [(entry, render(SOURCE / entry["file"])) for entry in catalog]
    output.mkdir(parents=True)
    font = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 15)
    sheet = Image.new("RGB", (7 * 180, math.ceil(len(images) / 7) * 150), "#14241e")
    labels = ImageDraw.Draw(sheet)
    for index, (entry, icon) in enumerate(images):
        name = Path(entry["file"]).stem + ".png"
        icon.save(output / name)
        x, y = index % 7 * 180, index // 7 * 150
        for size, offset in ((72, 16), (32, 110)):
            thumbnail = icon.resize((size, size), Image.Resampling.LANCZOS)
            sheet.paste(thumbnail, (x + offset, y + 16), thumbnail)
        labels.text((x + 8, y + 98), entry["name"], font=font, fill="#f6e7be")
    sheet.save(output / "family-contact-sheet.png")
    if args.write_assets:
        destination = ROOT / "Dice-Unity/Assets/Dice/Art/ModifierFamilies"
        destination.mkdir(exist_ok=True)
        folder_meta = destination.with_suffix(".meta")
        if not folder_meta.exists():
            folder_meta.write_text(f"fileFormatVersion: 2\nguid: {guid('ModifierFamilies')}\nfolderAsset: yes\nDefaultImporter:\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n")
        families = ROOT / "Dice-Unity/Assets/Dice/Content/Mechanical/ModifierFamilies"
        skin_path = ROOT / "Dice-Unity/Assets/Dice/Content/Presentation/SpiritHouse/SpiritHouseModifierSkin.asset"
        skin = skin_path.read_bytes().decode("utf-8")
        for entry, icon in images:
            name = Path(entry["file"]).stem + ".png"
            icon.save(destination / name)
            meta = destination / (name + ".meta")
            if not meta.exists():
                meta.write_text(sprite_meta(name))
            sprite_guid = re.search(r"^guid: (\w+)$", meta.read_text(), re.M)[1]
            family_meta = families / (entry["family"] + ".asset.meta")
            if not family_meta.exists():
                raise ValueError("Missing family asset: " + entry["family"])
            family_guid = re.search(r"^guid: (\w+)$", family_meta.read_text(), re.M)[1]
            # The skin's entry for the family holds its icon, shared by every variant of the family.
            skin, count = re.subn(
                rf"(?m)^(  - family: \{{fileID: 11400000, guid: {family_guid}, type: 2\}}\n    name: .*\n)    icon: .+$",
                rf"\g<1>    icon: {{fileID: 21300000, guid: {sprite_guid}, type: 3}}", skin)
            if count != 1:
                raise ValueError("Expected one skin entry with an icon for the family: " + entry["family"])
        skin_path.write_bytes(skin.encode("utf-8"))
    print(f"Rendered {len(images)} authored family icons: {output}")


if __name__ == "__main__":
    main()
