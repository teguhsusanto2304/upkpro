"""
Konverter DFM (Delphi, format teks) -> WinForms C# (.Designer.cs, .Generated.cs, .resx, .cs).

Pemakaian:
    py -I tools/dfm2winforms/dfm2winforms.py <folder_delphi> <folder_output> [--force-code]

- <Form>.Designer.cs, <Form>.Generated.cs dan <Form>.resx selalu ditulis ulang.
- <Form>.cs (stub event handler, kode manual) hanya ditulis jika belum ada,
  kecuali diberi --force-code.

Hanya UI yang dikonversi. Komponen data (ADO, DataSource, Field) dan FastReport
dilewati dan dicatat di komentar file .Generated.cs.
"""

from __future__ import annotations

import base64
import io
import re
import struct
import sys
from dataclasses import dataclass, field
from pathlib import Path

try:
    from PIL import Image
except ImportError:  # pragma: no cover
    Image = None

NAMESPACE = "UPKPro.Forms"

# Unit Delphi yang dikonversi (sesuai upk_pro.dpr), urutan bebas.
FORM_UNITS = [
    "unit_utama", "Unit_utama1", "Unit_back", "Unit_Anggota", "Unit_pengaturan",
    "unit_pembiayaan", "Unit_Keuangan", "U_Grup", "U_SetupRekening", "Unit_frsp",
    "Unit_blm", "Unit_faskab", "Unit_lok", "Unit_proposal", "unit_verifikasi",
]

# --------------------------------------------------------------------------------------
# Parser DFM
# --------------------------------------------------------------------------------------


@dataclass
class Binary:
    data: bytes


@dataclass
class Ident:
    name: str


@dataclass
class Collection:
    items: list[dict]


@dataclass
class Node:
    name: str
    type: str
    props: dict
    children: list["Node"] = field(default_factory=list)
    line: int = 0
    parent: "Node | None" = None

    def p(self, key, default=None):
        return self.props.get(key, default)


class Lexer:
    def __init__(self, text: str):
        self.s = text
        self.i = 0
        self.line = 1

    def skip_ws(self):
        s = self.s
        while self.i < len(s) and s[self.i] in " \t\r\n":
            if s[self.i] == "\n":
                self.line += 1
            self.i += 1

    def peek(self):
        self.skip_ws()
        return self.s[self.i] if self.i < len(self.s) else ""

    def expect(self, ch):
        self.skip_ws()
        if self.s[self.i] != ch:
            raise SyntaxError(f"Expected {ch!r} at line {self.line}, got {self.s[self.i:self.i + 20]!r}")
        self.i += 1

    def ident(self):
        self.skip_ws()
        m = re.compile(r"[A-Za-z_][A-Za-z0-9_.]*").match(self.s, self.i)
        if not m:
            raise SyntaxError(f"Expected identifier at line {self.line}: {self.s[self.i:self.i + 20]!r}")
        self.i = m.end()
        return m.group(0)

    def string_piece(self):
        """Membaca 'abc'#13#10'def' (tanpa '+')."""
        s = self.s
        out = []
        while self.i < len(s) and s[self.i] in "'#":
            if s[self.i] == "'":
                self.i += 1
                while True:
                    j = s.index("'", self.i)
                    out.append(s[self.i:j])
                    self.i = j + 1
                    if self.i < len(s) and s[self.i] == "'":
                        out.append("'")
                        self.i += 1
                    else:
                        break
            else:
                m = re.compile(r"#(\$[0-9A-Fa-f]+|\d+)").match(s, self.i)
                code = m.group(1)
                out.append(chr(int(code[1:], 16) if code.startswith("$") else int(code)))
                self.i = m.end()
        return "".join(out)

    def value(self):
        c = self.peek()
        s = self.s
        if c in "'#":
            parts = [self.string_piece()]
            while self.peek() == "+":
                self.i += 1
                self.skip_ws()
                parts.append(self.string_piece())
            return "".join(parts)
        if c == "[":
            self.i += 1
            items = []
            while self.peek() != "]":
                if self.peek() == ",":
                    self.i += 1
                    continue
                if self.peek() in "-0123456789$":
                    items.append(self.number())
                else:
                    items.append(self.ident())
            self.i += 1
            return set(str(x) for x in items)
        if c == "(":
            self.i += 1
            items = []
            while self.peek() != ")":
                items.append(self.value())
            self.i += 1
            return items
        if c == "<":
            self.i += 1
            items = []
            while self.peek() != ">":
                word = self.ident()
                if word.lower() != "item":
                    raise SyntaxError(f"Expected item at line {self.line}")
                if self.peek() == "[":  # item [n]
                    self.i += 1
                    self.number()
                    self.expect("]")
                props = {}
                while True:
                    self.skip_ws()
                    m = re.compile(r"end\b", re.I).match(s, self.i)
                    if m:
                        self.i = m.end()
                        break
                    key = self.ident()
                    self.expect("=")
                    props[key] = self.value()
                items.append(props)
            self.i += 1
            return Collection(items)
        if c == "{":
            self.i += 1
            j = s.index("}", self.i)
            hexstr = re.sub(r"\s", "", s[self.i:j])
            self.line += s.count("\n", self.i, j)
            self.i = j + 1
            return Binary(bytes.fromhex(hexstr))
        if c in "-0123456789$":
            return self.number()
        name = self.ident()
        if name.lower() == "true":
            return True
        if name.lower() == "false":
            return False
        return Ident(name)

    def number(self):
        self.skip_ws()
        m = re.compile(r"-?\$[0-9A-Fa-f]+|-?\d+(\.\d+)?([eE][-+]?\d+)?").match(self.s, self.i)
        self.i = m.end()
        t = m.group(0)
        if "$" in t:
            neg = t.startswith("-")
            v = int(t.replace("-", "").replace("$", ""), 16)
            return -v if neg else v
        if "." in t or "e" in t.lower():
            return float(t)
        return int(t)

    def parse_object(self, parent=None) -> Node:
        kw = self.ident().lower()
        if kw not in ("object", "inherited", "inline"):
            raise SyntaxError(f"Expected object at line {self.line}")
        line = self.line
        name = self.ident()
        typ = ""
        if self.peek() == ":":
            self.i += 1
            typ = self.ident()
        else:
            typ, name = name, ""
        if self.peek() == "[":
            self.i += 1
            self.number()
            self.expect("]")
        node = Node(name, typ, {}, [], line, parent)
        while True:
            self.skip_ws()
            m = re.compile(r"(end|object|inherited|inline)\b", re.I).match(self.s, self.i)
            if m and m.group(1).lower() == "end":
                self.i = m.end()
                return node
            if m:
                node.children.append(self.parse_object(node))
                continue
            key = self.ident()
            self.expect("=")
            node.props[key] = self.value()


def parse_dfm(path: Path) -> Node:
    raw = path.read_bytes()
    if raw.startswith(b"\xef\xbb\xbf"):
        text = raw[3:].decode("utf-8")
    else:
        text = raw.decode("cp1252", errors="replace")
    return Lexer(text).parse_object()


# --------------------------------------------------------------------------------------
# Utilitas
# --------------------------------------------------------------------------------------

CS_KEYWORDS = {
    "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class",
    "const", "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event",
    "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto", "if",
    "implicit", "in", "int", "interface", "internal", "is", "lock", "long", "namespace", "new", "null",
    "object", "operator", "out", "override", "params", "private", "protected", "public", "readonly",
    "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string", "struct",
    "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort",
    "using", "virtual", "void", "volatile", "while",
}

# Nama member Form/Control yang tidak boleh dipakai sebagai nama field/method.
FORM_MEMBERS = {
    "text", "name", "controls", "font", "icon", "size", "location", "width", "height", "left", "top",
    "right", "bottom", "visible", "enabled", "close", "show", "hide", "update", "refresh", "focus",
    "select", "invalidate", "dispose", "components", "menu", "parent", "owner", "tag", "cursor",
    "handle", "region", "site", "container", "events", "capture", "created", "focused", "bounds",
    "activate", "showdialog", "dialogresult", "initializecomponent", "applydelphidefaults", "scale",
    "load", "shown", "activated", "closing", "closed", "resize", "paint", "click", "validate",
    "acceptbutton", "cancelbutton", "opacity", "padding", "margin", "dock", "anchor", "anchors",
    "backcolor", "forecolor", "clientsize", "keypreview", "toString", "equals", "gethashcode", "gettype",
    "layout", "move", "enter", "leave", "keypress", "keydown", "keyup", "mousedown", "mouseup",
    "contextmenustrip", "imeMode", "rightToLeft", "textchanged", "doubleclick", "productname",
    "productversion", "companyname", "modal", "toplevel", "topmost", "mdichildren", "mdiparent",
}


def cs_str(s: str) -> str:
    out = ['"']
    for ch in s:
        o = ord(ch)
        if ch == "\\":
            out.append("\\\\")
        elif ch == '"':
            out.append('\\"')
        elif ch == "\r":
            out.append("\\r")
        elif ch == "\n":
            out.append("\\n")
        elif ch == "\t":
            out.append("\\t")
        elif o < 32:
            out.append(f"\\u{o:04x}")
        else:
            out.append(ch)
    out.append('"')
    return "".join(out)


def ident_name(name: str) -> str:
    return re.sub(r"\W", "_", name)


DELPHI_COLORS = {
    "clblack": "System.Drawing.Color.Black", "clmaroon": "System.Drawing.Color.Maroon",
    "clgreen": "System.Drawing.Color.Green", "clolive": "System.Drawing.Color.Olive",
    "clnavy": "System.Drawing.Color.Navy", "clpurple": "System.Drawing.Color.Purple",
    "clteal": "System.Drawing.Color.Teal", "clgray": "System.Drawing.Color.Gray",
    "clsilver": "System.Drawing.Color.Silver", "clred": "System.Drawing.Color.Red",
    "cllime": "System.Drawing.Color.Lime", "clyellow": "System.Drawing.Color.Yellow",
    "clblue": "System.Drawing.Color.Blue", "clfuchsia": "System.Drawing.Color.Fuchsia",
    "claqua": "System.Drawing.Color.Aqua", "clwhite": "System.Drawing.Color.White",
    "clltgray": "System.Drawing.Color.Silver", "cldkgray": "System.Drawing.Color.Gray",
    "clmoneygreen": "System.Drawing.Color.FromArgb(192, 220, 192)",
    "clskyblue": "System.Drawing.Color.FromArgb(166, 202, 240)",
    "clcream": "System.Drawing.Color.FromArgb(255, 251, 240)",
    "clmedgray": "System.Drawing.Color.FromArgb(160, 160, 164)",
    "clbtnface": "System.Drawing.SystemColors.Control", "clwindow": "System.Drawing.SystemColors.Window",
    "clwindowtext": "System.Drawing.SystemColors.WindowText",
    "clbtntext": "System.Drawing.SystemColors.ControlText",
    "clbtnshadow": "System.Drawing.SystemColors.ControlDark",
    "clbtnhighlight": "System.Drawing.SystemColors.ControlLightLight",
    "cl3dlight": "System.Drawing.SystemColors.ControlLight",
    "cl3ddkshadow": "System.Drawing.SystemColors.ControlDarkDark",
    "clhighlight": "System.Drawing.SystemColors.Highlight",
    "clhighlighttext": "System.Drawing.SystemColors.HighlightText",
    "clgraytext": "System.Drawing.SystemColors.GrayText",
    "clinfobk": "System.Drawing.SystemColors.Info", "clinfotext": "System.Drawing.SystemColors.InfoText",
    "clactivecaption": "System.Drawing.SystemColors.ActiveCaption",
    "clinactivecaption": "System.Drawing.SystemColors.InactiveCaption",
    "clcaptiontext": "System.Drawing.SystemColors.ActiveCaptionText",
    "clinactivecaptiontext": "System.Drawing.SystemColors.InactiveCaptionText",
    "clgradientactivecaption": "System.Drawing.SystemColors.GradientActiveCaption",
    "clgradientinactivecaption": "System.Drawing.SystemColors.GradientInactiveCaption",
    "clmenu": "System.Drawing.SystemColors.Menu", "clmenutext": "System.Drawing.SystemColors.MenuText",
    "clmenubar": "System.Drawing.SystemColors.MenuBar",
    "clmenuhighlight": "System.Drawing.SystemColors.MenuHighlight",
    "clscrollbar": "System.Drawing.SystemColors.ScrollBar",
    "clbackground": "System.Drawing.SystemColors.Desktop",
    "clappworkspace": "System.Drawing.SystemColors.AppWorkspace",
    "clactiveborder": "System.Drawing.SystemColors.ActiveBorder",
    "clinactiveborder": "System.Drawing.SystemColors.InactiveBorder",
    "clwindowframe": "System.Drawing.SystemColors.WindowFrame",
    "clhotlight": "System.Drawing.SystemColors.HotTrack",
}


def color_expr(v) -> str | None:
    if isinstance(v, Ident):
        key = v.name.lower()
        if key in ("clnone", "cldefault"):
            return None
        return DELPHI_COLORS.get(key)
    if isinstance(v, int):
        if v < 0:
            return None
        if v & 0xFF000000:  # warna sistem tak dikenal
            return None
        r, g, b = v & 0xFF, (v >> 8) & 0xFF, (v >> 16) & 0xFF
        return f"System.Drawing.Color.FromArgb({r}, {g}, {b})"
    return None


FONT_ALIASES = {"ms sans serif": "Microsoft Sans Serif"}


@dataclass(frozen=True)
class FontSpec:
    name: str
    size: float
    style: frozenset

    def expr(self) -> str:
        styles = sorted(self.style)
        style_map = {"fsBold": "Bold", "fsItalic": "Italic", "fsUnderline": "Underline", "fsStrikeOut": "Strikeout"}
        parts = [f"System.Drawing.FontStyle.{style_map[s]}" for s in styles if s in style_map]
        style = " | ".join(parts) if parts else "System.Drawing.FontStyle.Regular"
        size = f"{self.size:g}F"
        return f"new System.Drawing.Font({cs_str(self.name)}, {size}, {style})"


def font_from(props: dict, prefix: str, base: FontSpec) -> FontSpec:
    name = props.get(prefix + ".Name")
    height = props.get(prefix + ".Height")
    size = props.get(prefix + ".Size")
    style = props.get(prefix + ".Style")
    fname = FONT_ALIASES.get(name.lower(), name) if isinstance(name, str) else base.name
    if isinstance(height, int):
        fsize = round(abs(height) * 72 / 96, 2) if height < 0 else round(height * 72 / 96 * 0.85, 2)
    elif isinstance(size, int):
        fsize = float(size)
    else:
        fsize = base.size
    fstyle = frozenset(style) if isinstance(style, set) else base.style
    return FontSpec(fname, fsize, fstyle)


# --------------------------------------------------------------------------------------
# Gambar
# --------------------------------------------------------------------------------------

SIGS = [b"BM", b"\xff\xd8", b"\x89PNG", b"\x00\x00\x01\x00", b"GIF8"]


def _find_image_payload(data: bytes) -> bytes | None:
    for off in (0, 4):
        chunk = data[off:]
        if any(chunk.startswith(s) for s in SIGS):
            return chunk
    return None


def picture_bytes(b: Binary) -> bytes | None:
    """Picture.Data: [len][ClassName][(size)][data]."""
    data = b.data
    if not data:
        return None
    n = data[0]
    return _find_image_payload(data[1 + n:])


def glyph_bytes(b: Binary) -> bytes | None:
    """Glyph.Data/Bitmap: [size:int32][BMP]."""
    return _find_image_payload(b.data)


def to_png(raw: bytes, num_glyphs: int = 1, transparent_auto: bool = False) -> bytes | None:
    if raw.startswith(b"\xff\xd8") and not transparent_auto:
        return raw  # JPEG disimpan apa adanya
    if Image is None:
        return raw
    try:
        img = Image.open(io.BytesIO(raw))
        img.load()
    except Exception:
        return None
    if num_glyphs > 1 and img.width >= num_glyphs:
        w = img.width // num_glyphs
        img = img.crop((0, 0, w, img.height))
    img = img.convert("RGBA")
    if transparent_auto:
        key = img.getpixel((0, img.height - 1))
        px = img.load()
        for y in range(img.height):
            for x in range(img.width):
                if px[x, y][:3] == key[:3]:
                    px[x, y] = (0, 0, 0, 0)
    out = io.BytesIO()
    img.save(out, "PNG", optimize=True)
    return out.getvalue()


# --------------------------------------------------------------------------------------
# Generator
# --------------------------------------------------------------------------------------

NON_VISUAL_SKIP_PREFIX = ("TADO", "Tfrx", "TDataSource", "TClientDataSet", "TDataSetProvider", "TTable",
                          "TQuery", "TDatabase", "TSession", "TBatchMove", "TZ", "TComPort",
                          "TResizeKit", "TXPManifest", "TApplicationEvents", "TPieSeries", "TBarSeries",
                          "TLineSeries", "TAreaSeries")
FIELD_RE = re.compile(r"^T\w*Field$")

GRAPHIC_CONTAINERS = {"TShape", "TBevel", "TImage"}

# event Delphi -> (event WinForms, tipe EventArgs)
EVENT_ARGS = {
    "Click": "System.EventArgs", "DoubleClick": "System.EventArgs", "Enter": "System.EventArgs",
    "Leave": "System.EventArgs", "TextChanged": "System.EventArgs", "ValueChanged": "System.EventArgs",
    "SelectedIndexChanged": "System.EventArgs", "SelectionChangeCommitted": "System.EventArgs",
    "CheckedChanged": "System.EventArgs", "Tick": "System.EventArgs", "Shown": "System.EventArgs",
    "Load": "System.EventArgs", "Activated": "System.EventArgs", "Deactivate": "System.EventArgs",
    "Resize": "System.EventArgs", "Disposed": "System.EventArgs", "CurrentCellChanged": "System.EventArgs",
    "KeyPress": "System.Windows.Forms.KeyPressEventArgs", "KeyDown": "System.Windows.Forms.KeyEventArgs",
    "KeyUp": "System.Windows.Forms.KeyEventArgs", "MouseDown": "System.Windows.Forms.MouseEventArgs",
    "MouseUp": "System.Windows.Forms.MouseEventArgs", "MouseMove": "System.Windows.Forms.MouseEventArgs",
    "MouseEnter": "System.EventArgs", "MouseLeave": "System.EventArgs",
    "CellClick": "System.Windows.Forms.DataGridViewCellEventArgs",
    "CellDoubleClick": "System.Windows.Forms.DataGridViewCellEventArgs",
    "CellEnter": "System.Windows.Forms.DataGridViewCellEventArgs",
    "ColumnHeaderMouseClick": "System.Windows.Forms.DataGridViewCellMouseEventArgs",
    "FormClosed": "System.Windows.Forms.FormClosedEventArgs",
    "FormClosing": "System.Windows.Forms.FormClosingEventArgs",
    "ButtonClick": "UPKPro.Controls.NavigatorButtonEventArgs",
}

GENERIC_EVENTS = {
    "OnClick": "Click", "OnDblClick": "DoubleClick", "OnKeyPress": "KeyPress", "OnKeyDown": "KeyDown",
    "OnKeyUp": "KeyUp", "OnEnter": "Enter", "OnExit": "Leave", "OnChange": "TextChanged",
    "OnMouseDown": "MouseDown", "OnMouseUp": "MouseUp", "OnMouseMove": "MouseMove",
    "OnMouseEnter": "MouseEnter", "OnMouseLeave": "MouseLeave", "OnResize": "Resize",
}

TYPE_EVENTS = {
    "TCheckBox": {"OnClick": "CheckedChanged", "OnChange": "CheckedChanged"},
    "TDBCheckBox": {"OnClick": "CheckedChanged", "OnChange": "CheckedChanged"},
    "TRadioGroup": {"OnClick": "SelectedIndexChanged", "OnChange": "SelectedIndexChanged"},
    "TComboBox": {"OnClick": "SelectionChangeCommitted", "OnSelect": "SelectionChangeCommitted"},
    "TDBComboBox": {"OnClick": "SelectionChangeCommitted"},
    "TDBLookupComboBox": {"OnClick": "SelectionChangeCommitted", "OnCloseUp": "SelectionChangeCommitted",
                          "OnChange": "SelectedIndexChanged"},
    "TDateTimePicker": {"OnChange": "ValueChanged", "OnCloseUp": "ValueChanged"},
    "TPageControl": {"OnChange": "SelectedIndexChanged"},
    "TTabControl": {"OnChange": "SelectedIndexChanged"},
    "TUpDown": {"OnClick": "ValueChanged", "OnChanging": "ValueChanged"},
    "TDBGrid": {"OnCellClick": "CellClick", "OnColEnter": "CurrentCellChanged",
                "OnTitleClick": "ColumnHeaderMouseClick"},
    "TStringGrid": {"OnSelectCell": "CellEnter", "OnClick": "Click"},
    "TTimer": {"OnTimer": "Tick"},
    "TListBox": {"OnClick": "SelectedIndexChanged"},
    "TDBNavigator": {"OnClick": "ButtonClick"},
    "FORM": {"OnShow": "Shown", "OnCreate": "Load", "OnActivate": "Activated", "OnClose": "FormClosed",
             "OnCloseQuery": "FormClosing", "OnDeactivate": "Deactivate", "OnDestroy": "Disposed"},
}


@dataclass
class Ctl:
    node: Node
    field: str           # nama field C#
    cs_type: str         # tipe C#
    is_component: bool = False   # komponen non-visual (Timer, Dialog)
    children: list["Ctl"] = field(default_factory=list)
    x: int = 0
    y: int = 0
    w: int = 0
    h: int = 0
    graphic: bool = False  # graphic control Delphi (TLabel, TShape, TImage, ...)
    docked: bool = False


class FormGenerator:
    def __init__(self, unit: str, root: Node, pas_text: str):
        self.unit = unit
        self.root = root
        self.cls = root.type[1:] if root.type.startswith("T") else root.type
        self.pas_text = pas_text
        self.fields: list[tuple[str, str]] = []  # (type, name)
        self.creates: list[str] = []
        self.suspend: list[str] = []
        self.body: list[str] = []
        self.post: list[str] = []      # statement ApplyDelphiDefaults
        self.resources: dict[str, tuple[str, bytes]] = {}  # key -> (type, bytes)
        self.handlers: dict[str, dict] = {}  # name -> {"args": set, "uses": [...]}
        self.skipped: list[str] = []
        self.unmapped_events: list[str] = []
        self.warnings: list[str] = []
        self.used_names: set[str] = set()
        self.name_map: dict[str, str] = {}
        self.tooltip_needed = False
        self.form_font = font_from(root.props, "Font", FontSpec("Microsoft Sans Serif", 8.25, frozenset()))
        self.pas_lines = self._index_pas()

    # ---------- util ----------
    def _index_pas(self) -> dict[str, int]:
        res = {}
        pat = re.compile(r"^\s*procedure\s+" + re.escape(self.root.type) + r"\.(\w+)", re.I | re.M)
        for m in pat.finditer(self.pas_text):
            line = self.pas_text.count("\n", 0, m.start(1)) + 1
            res.setdefault(m.group(1).lower(), line)
        return res

    def safe_name(self, name: str) -> str:
        n = ident_name(name) or "ctl"
        if n.lower() in FORM_MEMBERS or n == self.cls or n.lower() == self.cls.lower():
            n = n + "_"
        if n in CS_KEYWORDS:
            n = "@" + n
        base = n
        k = 2
        while n.lower() in self.used_names:
            n = f"{base}{k}"
            k += 1
        self.used_names.add(n.lower())
        return n

    def handler_name(self, name: str) -> str:
        n = ident_name(name)
        if n.lower() in FORM_MEMBERS or n in CS_KEYWORDS:
            n = n + "_Handler"
        return n

    def add_handler(self, target: str, delphi_event: str, cs_event: str, handler: str, via_post=False):
        hn = self.handler_name(handler)
        info = self.handlers.setdefault(hn, {"args": set(), "uses": [], "delphi": handler})
        info["args"].add(EVENT_ARGS.get(cs_event, "System.EventArgs"))
        info["uses"].append(f"{target}.{delphi_event}")
        if not via_post:
            self.body.append(f"{target}.{cs_event} += {hn};")
        return hn

    def map_events(self, node: Node, target: str, kind: str):
        mapping = dict(GENERIC_EVENTS)
        mapping.update(TYPE_EVENTS.get(kind, {}))
        for key, val in node.props.items():
            if not key.startswith("On") or not isinstance(val, Ident):
                continue
            if kind == "TTabSheet" and key == "OnShow":
                continue  # ditangani khusus
            cs_event = mapping.get(key)
            if cs_event is None:
                self.unmapped_events.append(f"{node.name or self.cls}.{key} = {val.name}")
                hn = self.handler_name(val.name)
                info = self.handlers.setdefault(hn, {"args": set(), "uses": [], "delphi": val.name})
                info["uses"].append(f"{node.name}.{key} (belum terhubung)")
                continue
            self.add_handler(target, key, cs_event, val.name)

    def common_props(self, c: Ctl, out: list[str], parent_font: FontSpec, parent_color: str | None,
                     text_prop: str | None = "Text", is_root=False):
        n = c.node
        f = c.field if not is_root else ""
        pre = f + "." if f else ""
        if not is_root:
            out.append(f"{pre}Name = {cs_str(n.name)};")
        align = n.p("Align")
        if isinstance(align, Ident) and not is_root:
            dock = {"alTop": "Top", "alBottom": "Bottom", "alLeft": "Left", "alRight": "Right",
                    "alClient": "Fill"}.get(align.name)
            if dock:
                out.append(f"{pre}Dock = System.Windows.Forms.DockStyle.{dock};")
                c.docked = True
        anchors = n.p("Anchors")
        if isinstance(anchors, set) and not is_root:
            amap = {"akLeft": "Left", "akTop": "Top", "akRight": "Right", "akBottom": "Bottom"}
            parts = [f"System.Windows.Forms.AnchorStyles.{amap[a]}" for a in sorted(anchors) if a in amap]
            if parts and set(anchors) != {"akLeft", "akTop"}:
                out.append(f"{pre}Anchor = {' | '.join(parts)};")
            elif not parts:
                out.append(f"{pre}Anchor = System.Windows.Forms.AnchorStyles.None;")
        if not is_root:
            out.append(f"{pre}Location = new System.Drawing.Point({c.x}, {c.y});")
            out.append(f"{pre}Size = new System.Drawing.Size({c.w}, {c.h});")
        if text_prop:
            cap = n.p("Caption", n.p("Text"))
            if isinstance(cap, str) and cap:
                out.append(f"{pre}{text_prop} = {cs_str(cap)};")
        if n.p("Visible") is False and not is_root:
            out.append(f"{pre}Visible = false;")
        if n.p("Enabled") is False:
            out.append(f"{pre}Enabled = false;")
        if isinstance(n.p("TabOrder"), int) and not is_root:
            out.append(f"{pre}TabIndex = {n.p('TabOrder')};")
        if n.p("TabStop") is False and not is_root:
            out.append(f"{pre}TabStop = false;")
        cur = n.p("Cursor")
        if isinstance(cur, Ident):
            cmap = {"crHandPoint": "Hand", "crHourGlass": "WaitCursor", "crIBeam": "IBeam",
                    "crCross": "Cross", "crArrow": "Arrow", "crSizeAll": "SizeAll"}
            if cur.name in cmap:
                out.append(f"{pre}Cursor = System.Windows.Forms.Cursors.{cmap[cur.name]};")
        hint = n.p("Hint")
        if isinstance(hint, str) and hint and not is_root:
            self.tooltip_needed = True
            out.append(f"toolTip1.SetToolTip({c.field}, {cs_str(hint.split('|')[0])});")

    def font_props(self, c: Ctl, out: list[str], parent_font: FontSpec, parent_fc: str, pre: str,
                   prefix="Font") -> tuple[FontSpec, str]:
        n = c.node
        fnt = parent_font
        fc = parent_fc
        if (prefix + ".Name") in n.props or (prefix + ".Height") in n.props:
            if n.p("ParentFont") is not True:
                fnt = font_from(n.props, prefix, parent_font)
                col = n.p(prefix + ".Color")
                fc = color_expr(col) or parent_fc
        if fnt != parent_font:
            out.append(f"{pre}Font = {fnt.expr()};")
        if fc != parent_fc:
            out.append(f"{pre}ForeColor = {fc};")
        return fnt, fc

    # ---------- konversi node ----------
    def is_skipped(self, node: Node) -> bool:
        t = node.type
        if t.startswith(NON_VISUAL_SKIP_PREFIX) or FIELD_RE.match(t):
            return True
        return False

    def convert_children(self, parent: Ctl, nodes: list[Node], font: FontSpec, fc: str):
        for child in nodes:
            if self.is_skipped(child):
                if not child.type.startswith(("Tfrx",)) or child.type == "TfrxReport":
                    if not FIELD_RE.match(child.type):
                        self.skipped.append(f"{child.name}: {child.type}")
                continue
            ctl = self.convert(child, font, fc)
            if ctl is not None:
                parent.children.append(ctl)

    def geom(self, c: Ctl):
        n = c.node
        c.x = int(n.p("Left", 0) or 0)
        c.y = int(n.p("Top", 0) or 0)
        c.w = int(n.p("Width", 0) or 0)
        c.h = int(n.p("Height", 0) or 0)

    def declare(self, cs_type: str, name: str, ctor_args: str = ""):
        self.fields.append((cs_type, name))
        self.creates.append(f"{name} = new {cs_type}({ctor_args});")

    def convert(self, n: Node, pfont: FontSpec, pfc: str) -> Ctl | None:
        t = n.type
        name = self.safe_name(n.name)
        self.name_map[n.name.lower()] = name
        out: list[str] = []
        pre = name + "."

        def make(cs_type, graphic=False, component=False, ctor=""):
            c = Ctl(n, name, cs_type, is_component=component, graphic=graphic)
            self.geom(c)
            self.declare(cs_type, name, ctor)
            return c

        # ---- komponen non-visual ----
        if t == "TTimer":
            c = make("System.Windows.Forms.Timer", component=True, ctor="components")
            if isinstance(n.p("Interval"), int):
                out.append(f"{pre}Interval = {n.p('Interval')};")
            if n.p("Enabled") is not False:
                out.append(f"{pre}Enabled = true;")
            self.map_events(n, name, t)
            self.body.append(f"// \n// {name}\n// ")
            self.body.extend(out)
            return None
        if t in ("TOpenDialog", "TOpenPictureDialog", "TSaveDialog", "TSavePictureDialog"):
            cs = "System.Windows.Forms.SaveFileDialog" if "Save" in t else "System.Windows.Forms.OpenFileDialog"
            make(cs, component=True)
            if isinstance(n.p("Filter"), str):
                out.append(f"{pre}Filter = {cs_str(n.p('Filter'))};")
            elif "Picture" in t:
                out.append(f'{pre}Filter = "Gambar|*.bmp;*.jpg;*.jpeg;*.png;*.gif|Semua file|*.*";')
            if isinstance(n.p("DefaultExt"), str):
                out.append(f"{pre}DefaultExt = {cs_str(n.p('DefaultExt'))};")
            if isinstance(n.p("Title"), str):
                out.append(f"{pre}Title = {cs_str(n.p('Title'))};")
            if isinstance(n.p("FileName"), str):
                out.append(f"{pre}FileName = {cs_str(n.p('FileName'))};")
            self.body.append(f"// \n// {name}\n// ")
            self.body.extend(out)
            return None
        if t == "TImageList":
            make("System.Windows.Forms.ImageList", component=True, ctor="components")
            w, h = n.p("Width", 16), n.p("Height", 16)
            out.append(f"{pre}ImageSize = new System.Drawing.Size({w}, {h});")
            out.append(f"{pre}ColorDepth = System.Windows.Forms.ColorDepth.Depth32Bit;")
            self.body.append(f"// \n// {name}\n// ")
            self.body.extend(out)
            return None
        if t in ("TPopupMenu", "TMainMenu"):
            self.skipped.append(f"{n.name}: {t} (menu belum dikonversi)")
            return None

        # ---- kontrol visual ----
        text_prop = "Text"
        font, fc = pfont, pfc
        c: Ctl

        if t in ("TLabel", "TDBText", "TStaticText"):
            c = make("System.Windows.Forms.Label", graphic=t != "TStaticText")
            align = n.p("Alignment")
            layout = n.p("Layout")
            wordwrap = n.p("WordWrap") is True
            autosize = n.p("AutoSize") is not False and not wordwrap and t != "TDBText"
            if isinstance(align, Ident) and align.name != "taLeftJustify":
                autosize = False
            out.append(f"{pre}AutoSize = {'true' if autosize else 'false'};")
            v = {"tlTop": "Top", "tlCenter": "Middle", "tlBottom": "Bottom"}.get(
                layout.name if isinstance(layout, Ident) else "tlTop", "Top")
            h = {"taLeftJustify": "Left", "taCenter": "Center", "taRightJustify": "Right"}.get(
                align.name if isinstance(align, Ident) else "taLeftJustify", "Left")
            if (v, h) != ("Top", "Left"):
                out.append(f"{pre}TextAlign = System.Drawing.ContentAlignment.{v}{h};")
            if n.p("Transparent") is True:
                out.append(f"{pre}BackColor = System.Drawing.Color.Transparent;")
            if t == "TDBText":
                self._data_tag(n, out, pre)
        elif t in ("TEdit", "TDBEdit", "TLabeledEdit"):
            c = make("System.Windows.Forms.TextBox")
            self._edit_props(n, out, pre)
            self._data_tag(n, out, pre)
        elif t == "TMaskEdit":
            c = make("System.Windows.Forms.MaskedTextBox")
            mask = n.p("EditMask")
            if isinstance(mask, str) and mask:
                out.append(f"{pre}Mask = {cs_str(convert_mask(mask))};")
            self._edit_props(n, out, pre)
        elif t in ("TMemo", "TDBMemo", "TRichEdit", "TDBRichEdit"):
            c = make("System.Windows.Forms.TextBox")
            out.append(f"{pre}Multiline = true;")
            sb = n.p("ScrollBars")
            if isinstance(sb, Ident) and sb.name != "ssNone":
                m = {"ssVertical": "Vertical", "ssHorizontal": "Horizontal", "ssBoth": "Both"}[sb.name]
                out.append(f"{pre}ScrollBars = System.Windows.Forms.ScrollBars.{m};")
            if n.p("WordWrap") is False:
                out.append(f"{pre}WordWrap = false;")
            lines = n.p("Lines.Strings")
            if isinstance(lines, list) and lines:
                out.append(f"{pre}Text = {cs_str(chr(13).join(str(x) + chr(10) for x in lines).rstrip())};")
            self._edit_props(n, out, pre)
            self._data_tag(n, out, pre)
            text_prop = None
        elif t in ("TButton", "TBitBtn"):
            c = make("System.Windows.Forms.Button")
            self._button_props(n, out, pre, name)
            out.append(f"{pre}UseVisualStyleBackColor = true;")
        elif t == "TSpeedButton":
            c = make("System.Windows.Forms.Button", graphic=True)
            self._button_props(n, out, pre, name)
            if n.p("Flat") is True:
                out.append(f"{pre}FlatStyle = System.Windows.Forms.FlatStyle.Flat;")
                out.append(f"{pre}FlatAppearance.BorderSize = 0;")
                out.append(f"{pre}FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(229, 241, 251);")
                out.append(f"{pre}BackColor = System.Drawing.Color.Transparent;")
            else:
                out.append(f"{pre}UseVisualStyleBackColor = true;")
            out.append(f"{pre}TabStop = false;")
        elif t in ("TPanel", "TDBCtrlGrid", "TFlowPanel", "TGridPanel", "TScrollBox"):
            if t == "TScrollBox":
                c = make("System.Windows.Forms.Panel")
                out.append(f"{pre}AutoScroll = true;")
                out.append(f"{pre}BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;")
                text_prop = None
            else:
                c = make("UPKPro.Controls.CaptionPanel")
                self._panel_props(n, out, pre, t)
        elif t == "TGroupBox":
            c = make("System.Windows.Forms.GroupBox")
        elif t in ("TRadioGroup", "TDBRadioGroup"):
            c = make("UPKPro.Controls.RadioGroup")
            items = n.p("Items.Strings")
            if isinstance(items, list):
                arr = ", ".join(cs_str(str(x)) for x in items)
                out.append(f"{pre}Items = new string[] {{ {arr} }};")
            if isinstance(n.p("Columns"), int):
                out.append(f"{pre}Columns = {n.p('Columns')};")
            if isinstance(n.p("ItemIndex"), int):
                out.append(f"{pre}ItemIndex = {n.p('ItemIndex')};")
        elif t in ("TCheckBox", "TDBCheckBox"):
            c = make("System.Windows.Forms.CheckBox")
            if n.p("Checked") is True or (isinstance(n.p("State"), Ident) and n.p("State").name == "cbChecked"):
                out.append(f"{pre}Checked = true;")
            if isinstance(n.p("Alignment"), Ident) and n.p("Alignment").name == "taLeftJustify":
                out.append(f"{pre}CheckAlign = System.Drawing.ContentAlignment.MiddleRight;")
            out.append(f"{pre}UseVisualStyleBackColor = true;")
            self._data_tag(n, out, pre)
        elif t == "TRadioButton":
            c = make("System.Windows.Forms.RadioButton")
            if n.p("Checked") is True:
                out.append(f"{pre}Checked = true;")
            if isinstance(n.p("Alignment"), Ident) and n.p("Alignment").name == "taLeftJustify":
                out.append(f"{pre}CheckAlign = System.Drawing.ContentAlignment.MiddleRight;")
            out.append(f"{pre}UseVisualStyleBackColor = true;")
        elif t in ("TComboBox", "TDBComboBox", "TDBLookupComboBox", "TDBLookupListBox"):
            c = make("System.Windows.Forms.ComboBox")
            style = n.p("Style")
            if t == "TDBLookupComboBox" or (isinstance(style, Ident) and style.name.startswith("csDropDownList")):
                out.append(f"{pre}DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;")
            elif isinstance(style, Ident) and style.name == "csSimple":
                out.append(f"{pre}DropDownStyle = System.Windows.Forms.ComboBoxStyle.Simple;")
            out.append(f"{pre}FormattingEnabled = true;")
            items = n.p("Items.Strings")
            if isinstance(items, list) and items:
                arr = ", ".join(cs_str(str(x)) for x in items)
                out.append(f"{pre}Items.AddRange(new object[] {{ {arr} }});")
            if isinstance(n.p("ListField"), str):
                out.append(f"{pre}DisplayMember = {cs_str(n.p('ListField').split(';')[0])};")
            if isinstance(n.p("KeyField"), str):
                out.append(f"{pre}ValueMember = {cs_str(n.p('KeyField'))};")
            if isinstance(n.p("ItemIndex"), int) and n.p("ItemIndex") >= 0 and isinstance(items, list):
                self.post.append(f"if ({name}.Items.Count > {n.p('ItemIndex')}) {name}.SelectedIndex = {n.p('ItemIndex')};")
            self._data_tag(n, out, pre)
            if isinstance(style, Ident) and "DropDownList" in style.name:
                text_prop = None
            if isinstance(n.p("DropDownCount"), int):
                out.append(f"{pre}MaxDropDownItems = {n.p('DropDownCount')};")
        elif t in ("TListBox", "TDBListBox", "TCheckListBox"):
            c = make("System.Windows.Forms.CheckedListBox" if t == "TCheckListBox" else "System.Windows.Forms.ListBox")
            out.append(f"{pre}FormattingEnabled = true;")
            out.append(f"{pre}IntegralHeight = false;")
            items = n.p("Items.Strings")
            if isinstance(items, list) and items:
                arr = ", ".join(cs_str(str(x)) for x in items)
                out.append(f"{pre}Items.AddRange(new object[] {{ {arr} }});")
            text_prop = None
        elif t == "TDirectoryListBox":
            c = make("System.Windows.Forms.TreeView")
            out.append(f"{pre}HideSelection = false;")
            text_prop = None
        elif t in ("TFileListBox", "TDriveComboBox"):
            c = make("System.Windows.Forms.ListBox" if t == "TFileListBox" else "System.Windows.Forms.ComboBox")
            text_prop = None
        elif t == "TDateTimePicker":
            c = make("System.Windows.Forms.DateTimePicker")
            kind = n.p("Kind")
            fmt = n.p("Format")
            if isinstance(kind, Ident) and kind.name == "dtkTime":
                out.append(f"{pre}Format = System.Windows.Forms.DateTimePickerFormat.Time;")
                out.append(f"{pre}ShowUpDown = true;")
            elif isinstance(fmt, str) and fmt:
                out.append(f"{pre}Format = System.Windows.Forms.DateTimePickerFormat.Custom;")
                out.append(f"{pre}CustomFormat = {cs_str(fmt)};")
            elif isinstance(n.p("DateFormat"), Ident) and n.p("DateFormat").name == "dfLong":
                out.append(f"{pre}Format = System.Windows.Forms.DateTimePickerFormat.Long;")
            else:
                out.append(f"{pre}Format = System.Windows.Forms.DateTimePickerFormat.Short;")
            text_prop = None
        elif t in ("TDBGrid", "TStringGrid", "TDrawGrid"):
            c = make("System.Windows.Forms.DataGridView")
            self._grid_props(n, out, pre, name)
            text_prop = None
        elif t == "TPageControl":
            c = make("System.Windows.Forms.TabControl")
            text_prop = None
            if isinstance(n.p("Style"), Ident) and n.p("Style").name in ("tsButtons", "tsFlatButtons"):
                out.append(f"{pre}Appearance = System.Windows.Forms.TabAppearance."
                           f"{'FlatButtons' if n.p('Style').name == 'tsFlatButtons' else 'Buttons'};")
            if isinstance(n.p("TabPosition"), Ident):
                tp = {"tpBottom": "Bottom", "tpLeft": "Left", "tpRight": "Right"}.get(n.p("TabPosition").name)
                if tp:
                    out.append(f"{pre}Alignment = System.Windows.Forms.TabAlignment.{tp};")
            if isinstance(n.p("MultiLine"), bool) and n.p("MultiLine"):
                out.append(f"{pre}Multiline = true;")
        elif t == "TTabSheet":
            c = make("System.Windows.Forms.TabPage")
            out.append(f"{pre}UseVisualStyleBackColor = true;")
            parent_field = self.name_map.get(n.parent.name.lower()) if n.parent else None
            if n.p("TabVisible") is False and parent_field:
                self.post.append(f"{parent_field}.TabPages.Remove({name}); // TabVisible = False di Delphi")
            show = n.p("OnShow")
            if isinstance(show, Ident) and parent_field:
                hn = self.add_handler(name, "OnShow", "Shown", show.name, via_post=True)
                self.post.append(
                    f"{parent_field}.SelectedIndexChanged += (s, e) => {{ if ({parent_field}.SelectedTab == {name}) {hn}(s, e); }};")
                self.post.append(
                    f"Shown += (s, e) => {{ if ({parent_field}.SelectedTab == {name}) {hn}(s, e); }};")
        elif t == "TTabControl":
            c = make("System.Windows.Forms.TabControl")
            tabs = n.p("Tabs.Strings")
            if isinstance(tabs, list):
                for i, tab in enumerate(tabs):
                    tp = f"{name}_Tab{i}"
                    self.declare("System.Windows.Forms.TabPage", tp)
                    self.body.append(f"{tp}.Text = {cs_str(str(tab))};")
                    self.body.append(f"{tp}.UseVisualStyleBackColor = true;")
                    out.append(f"{pre}TabPages.Add({tp});")
            text_prop = None
            if n.children:
                self.warnings.append(f"{n.name}: TTabControl dengan child, child diletakkan di tab pertama")
        elif t in ("TImage", "TDBImage"):
            c = make("System.Windows.Forms.PictureBox", graphic=True)
            text_prop = None
            if n.p("Stretch") is True:
                mode = "Zoom" if n.p("Proportional") is True else "StretchImage"
            elif n.p("Center") is True:
                mode = "CenterImage"
            # AutoSize Delphi hanya berlaku saat desain (ukuran sudah ada di DFM); SizeMode.AutoSize
            # di WinForms tidak ikut diskalakan DPI sehingga child-nya bisa terpotong.
            else:
                mode = "Normal"
            if mode != "Normal":
                out.append(f"{pre}SizeMode = System.Windows.Forms.PictureBoxSizeMode.{mode};")
            pic = n.p("Picture.Data")
            if isinstance(pic, Binary):
                raw = picture_bytes(pic)
                png = to_png(raw, transparent_auto=n.p("Transparent") is True) if raw else None
                if png:
                    key = f"{name}.Image"
                    self.resources[key] = ("bitmap", png)
                    out.append(f"{pre}Image = (System.Drawing.Image)resources.GetObject({cs_str(key)});")
                else:
                    self.warnings.append(f"{n.name}: gambar tidak dapat dibaca")
            if n.p("Transparent") is True:
                out.append(f"{pre}BackColor = System.Drawing.Color.Transparent;")
            out.append(f"{pre}TabStop = false;")
            if t == "TDBImage":
                self._data_tag(n, out, pre)
        elif t == "TShape":
            c = make("UPKPro.Controls.ShapeControl", graphic=True)
            text_prop = None
            shp = n.p("Shape")
            if isinstance(shp, Ident) and shp.name != "stRectangle":
                k = {"stSquare": "Square", "stRoundRect": "RoundRect", "stRoundSquare": "RoundSquare",
                     "stEllipse": "Ellipse", "stCircle": "Circle"}.get(shp.name, "Rectangle")
                out.append(f"{pre}Shape = UPKPro.Controls.ShapeKind.{k};")
            bc = color_expr(n.p("Brush.Color", Ident("clWhite")))
            if bc and bc != "System.Drawing.Color.White":
                out.append(f"{pre}BrushColor = {bc};")
            if isinstance(n.p("Brush.Style"), Ident) and n.p("Brush.Style").name == "bsClear":
                out.append(f"{pre}BrushClear = true;")
            pc = color_expr(n.p("Pen.Color", Ident("clBlack")))
            if pc and pc != "System.Drawing.Color.Black":
                out.append(f"{pre}PenColor = {pc};")
            if isinstance(n.p("Pen.Width"), int) and n.p("Pen.Width") != 1:
                out.append(f"{pre}PenWidth = {n.p('Pen.Width')};")
            if isinstance(n.p("Pen.Style"), Ident) and n.p("Pen.Style").name == "psClear":
                out.append(f"{pre}PenClear = true;")
        elif t == "TBevel":
            c = make("UPKPro.Controls.Bevel", graphic=True)
            text_prop = None
            shp = n.p("Shape")
            if isinstance(shp, Ident) and shp.name != "bsBox":
                k = {"bsFrame": "Frame", "bsTopLine": "TopLine", "bsBottomLine": "BottomLine",
                     "bsLeftLine": "LeftLine", "bsRightLine": "RightLine", "bsSpacer": "Spacer"}.get(shp.name, "Box")
                out.append(f"{pre}Shape = UPKPro.Controls.BevelShape.{k};")
            if isinstance(n.p("Style"), Ident) and n.p("Style").name == "bsRaised":
                out.append(f"{pre}Raised = true;")
        elif t in ("TGauge", "TProgressBar"):
            c = make("System.Windows.Forms.ProgressBar")
            text_prop = None
            mx = n.p("MaxValue", n.p("Max"))
            mn = n.p("MinValue", n.p("Min"))
            pos = n.p("Progress", n.p("Position"))
            if isinstance(mn, int):
                out.append(f"{pre}Minimum = {mn};")
            if isinstance(mx, int):
                out.append(f"{pre}Maximum = {mx};")
            if isinstance(pos, int):
                out.append(f"{pre}Value = {pos};")
        elif t == "TUpDown":
            c = make("System.Windows.Forms.NumericUpDown")
            text_prop = None
            mn, mx, pos = n.p("Min", 0), n.p("Max", 100), n.p("Position", 0)
            out.append(f"{pre}Minimum = new decimal(new int[] {{ {abs(mn)}, 0, 0, {'-2147483648' if mn < 0 else '0'} }});")
            out.append(f"{pre}Maximum = new decimal(new int[] {{ {abs(mx)}, 0, 0, {'-2147483648' if mx < 0 else '0'} }});")
            if pos:
                out.append(f"{pre}Value = new decimal(new int[] {{ {abs(pos)}, 0, 0, {'-2147483648' if pos < 0 else '0'} }});")
        elif t == "TDBNavigator":
            c = make("UPKPro.Controls.DbNavigator")
            text_prop = None
            if isinstance(n.p("DataSource"), Ident):
                out.append(f"{pre}Tag = {cs_str(n.p('DataSource').name)};")
        elif t == "TStatusBar":
            c = make("System.Windows.Forms.StatusStrip")
            text_prop = None
            out.append(f"{pre}SizingGrip = false;")
            panels = n.p("Panels")
            labels = []
            if isinstance(panels, Collection) and panels.items and n.p("SimplePanel") is not True:
                for i, item in enumerate(panels.items):
                    lb = f"{name}_Panel{i}"
                    self.declare("System.Windows.Forms.ToolStripStatusLabel", lb)
                    labels.append(lb)
                    self.body.append(f"{lb}.Name = {cs_str(lb)};")
                    if isinstance(item.get("Text"), str):
                        self.body.append(f"{lb}.Text = {cs_str(item['Text'])};")
                    self.body.append(f"{lb}.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;")
                    if i == len(panels.items) - 1:
                        self.body.append(f"{lb}.Spring = true;")
                    elif isinstance(item.get("Width"), int):
                        self.body.append(f"{lb}.AutoSize = false;")
                        self.body.append(f"{lb}.Size = new System.Drawing.Size({item['Width']}, 17);")
                    self.body.append(f"{lb}.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Right;")
            else:
                lb = f"{name}_Panel0"
                self.declare("System.Windows.Forms.ToolStripStatusLabel", lb)
                labels.append(lb)
                self.body.append(f"{lb}.Name = {cs_str(lb)};")
                self.body.append(f"{lb}.Spring = true;")
                self.body.append(f"{lb}.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;")
                if isinstance(n.p("SimpleText"), str):
                    self.body.append(f"{lb}.Text = {cs_str(n.p('SimpleText'))};")
            out.append(f"{pre}Items.AddRange(new System.Windows.Forms.ToolStripItem[] {{ {', '.join(labels)} }});")
            if not isinstance(n.p("Align"), Ident):
                n.props["Align"] = Ident("alBottom")
        elif t in ("TDBChart", "TChart"):
            c = make("UPKPro.Controls.CaptionPanel")
            out.append(f"{pre}BevelOuter = UPKPro.Controls.PanelBevel.Lowered;")
            out.append(f"{pre}BackColor = System.Drawing.Color.White;")
            title = n.p("Title.Text.Strings")
            label = (" ".join(title) if isinstance(title, list) else n.name)
            out.append(f"{pre}Text = {cs_str('[Grafik] ' + label)};")
            out.append(f"{pre}Tag = \"TODO: grafik ({t}) belum diporting\";")
            text_prop = None
            self.warnings.append(f"{n.name}: {t} diganti placeholder (perlu library chart)")
            n = Node(n.name, n.type, n.props, [], n.line, n.parent)  # abaikan series
            c.node = n
        else:
            if "Width" in n.props and "Height" in n.props:
                c = make("UPKPro.Controls.CaptionPanel")
                out.append(f"{pre}BevelOuter = UPKPro.Controls.PanelBevel.Lowered;")
                out.append(f"{pre}Text = {cs_str('[' + t + '] ' + n.name)};")
                text_prop = None
                self.warnings.append(f"{n.name}: tipe {t} tidak dikenal, diganti placeholder")
            else:
                self.skipped.append(f"{n.name}: {t}")
                return None

        # ---- properti umum ----
        props: list[str] = []
        self.common_props(c, props, pfont, None, text_prop)
        font, fc = self.font_props(c, props, pfont, pfc, pre)
        col = n.p("Color")
        if col is not None and n.p("ParentColor") is not True and c.cs_type not in (
                "UPKPro.Controls.ShapeControl", "UPKPro.Controls.Bevel"):
            ce = color_expr(col)
            if ce and not (c.cs_type == "System.Windows.Forms.Label" and n.p("Transparent") is True):
                if c.cs_type == "System.Windows.Forms.DataGridView":
                    props.append(f"{pre}BackgroundColor = {ce};")
                    props.append(f"{pre}DefaultCellStyle.BackColor = {ce};")
                else:
                    props.append(f"{pre}BackColor = {ce};")
                    if c.cs_type == "System.Windows.Forms.Button":
                        props.append(f"{pre}UseVisualStyleBackColor = false;")
        if c.cs_type == "System.Windows.Forms.TabPage":
            props = [p for p in props if not p.startswith(f"{pre}Location") and not p.startswith(f"{pre}Size")
                     and not p.startswith(f"{pre}TabIndex")]
        self.map_events(n, name, t)
        c_lines = props + out
        self.body.append(f"// \n// {name}\n// ")
        self.body.append(f"@@CTL:{name}")
        self.ctl_lines[name] = c_lines

        if t not in ("TDBChart", "TChart"):
            self.convert_children(c, n.children, font, fc)
        return c

    # ---------- helper properti ----------
    def _data_tag(self, n: Node, out: list[str], pre: str):
        ds = n.p("DataSource")
        fld = n.p("DataField")
        if isinstance(ds, Ident) or isinstance(fld, str):
            tag = f"{ds.name if isinstance(ds, Ident) else ''}:{fld if isinstance(fld, str) else ''}"
            out.append(f"{pre}Tag = {cs_str(tag)};")

    def _edit_props(self, n: Node, out: list[str], pre: str):
        if n.p("ReadOnly") is True:
            out.append(f"{pre}ReadOnly = true;")
        if isinstance(n.p("MaxLength"), int) and n.p("MaxLength") > 0:
            out.append(f"{pre}MaxLength = {n.p('MaxLength')};")
        pc = n.p("PasswordChar")
        if isinstance(pc, str) and pc and pc != "#0":
            out.append(f"{pre}PasswordChar = '{pc[0]}';")
        cc = n.p("CharCase")
        if isinstance(cc, Ident) and cc.name in ("ecUpperCase", "ecLowerCase"):
            out.append(f"{pre}CharacterCasing = System.Windows.Forms.CharacterCasing."
                       f"{'Upper' if cc.name == 'ecUpperCase' else 'Lower'};")
        if isinstance(n.p("BorderStyle"), Ident) and n.p("BorderStyle").name == "bsNone":
            out.append(f"{pre}BorderStyle = System.Windows.Forms.BorderStyle.None;")
        al = n.p("Alignment")
        if isinstance(al, Ident) and al.name in ("taRightJustify", "taCenter"):
            out.append(f"{pre}TextAlign = System.Windows.Forms.HorizontalAlignment."
                       f"{'Right' if al.name == 'taRightJustify' else 'Center'};")

    def _button_props(self, n: Node, out: list[str], pre: str, name: str):
        kind = n.p("Kind")
        mr = n.p("ModalResult")
        kind_caption = {"bkOK": "OK", "bkCancel": "Cancel", "bkClose": "&Close", "bkYes": "&Yes",
                        "bkNo": "&No", "bkHelp": "&Help", "bkAbort": "Abort", "bkRetry": "&Retry",
                        "bkIgnore": "&Ignore", "bkAll": "&All"}
        kind_result = {"bkOK": "OK", "bkCancel": "Cancel", "bkYes": "Yes", "bkNo": "No", "bkAbort": "Abort",
                       "bkRetry": "Retry", "bkIgnore": "Ignore"}
        if isinstance(kind, Ident) and kind.name in kind_caption and not isinstance(n.p("Caption"), str):
            n.props["Caption"] = kind_caption[kind.name]
        result = None
        if isinstance(mr, int) and mr:
            result = {1: "OK", 2: "Cancel", 3: "Abort", 4: "Retry", 5: "Ignore", 6: "Yes", 7: "No"}.get(mr)
        elif isinstance(mr, Ident):
            result = mr.name[2:] if mr.name.startswith("mr") else None
        elif isinstance(kind, Ident):
            result = kind_result.get(kind.name)
        if result and result != "None":
            out.append(f"{pre}DialogResult = System.Windows.Forms.DialogResult.{result};")
        glyph = n.p("Glyph.Data")
        if isinstance(glyph, Binary):
            raw = glyph_bytes(glyph)
            ng = n.p("NumGlyphs", 1)
            png = to_png(raw, num_glyphs=ng if isinstance(ng, int) else 1, transparent_auto=True) if raw else None
            if png:
                key = f"{name}.Image"
                self.resources[key] = ("bitmap", png)
                out.append(f"{pre}Image = (System.Drawing.Image)resources.GetObject({cs_str(key)});")
                layout = n.p("Layout")
                lay = layout.name if isinstance(layout, Ident) else "blGlyphLeft"
                rel = {"blGlyphLeft": "ImageBeforeText", "blGlyphRight": "TextBeforeImage",
                       "blGlyphTop": "ImageAboveText", "blGlyphBottom": "TextAboveImage"}[lay]
                cap = n.p("Caption")
                if isinstance(cap, str) and cap:
                    out.append(f"{pre}TextImageRelation = System.Windows.Forms.TextImageRelation.{rel};")

    def _panel_props(self, n: Node, out: list[str], pre: str, t: str):
        bo = n.p("BevelOuter")
        bi = n.p("BevelInner")
        bw = n.p("BevelWidth", 1) if isinstance(n.p("BevelWidth"), int) else 1
        bdw = n.p("BorderWidth", 0) if isinstance(n.p("BorderWidth"), int) else 0
        outer = "Raised" if not isinstance(bo, Ident) else {"bvNone": "None", "bvLowered": "Lowered",
                                                             "bvRaised": "Raised", "bvSpace": "None"}[bo.name]
        inner = "None" if not isinstance(bi, Ident) else {"bvNone": "None", "bvLowered": "Lowered",
                                                           "bvRaised": "Raised", "bvSpace": "None"}[bi.name]
        if t == "TDBCtrlGrid":
            outer = "Lowered"
        if outer != "Raised":
            out.append(f"{pre}BevelOuter = UPKPro.Controls.PanelBevel.{outer};")
        if inner != "None":
            out.append(f"{pre}BevelInner = UPKPro.Controls.PanelBevel.{inner};")
        if bw != 1:
            out.append(f"{pre}BevelWidth = {bw};")
        pad = (bw if outer != "None" else 0) + (bw if inner != "None" else 0) + bdw
        if pad:
            out.append(f"{pre}Padding = new System.Windows.Forms.Padding({pad});")
        if isinstance(n.p("BorderStyle"), Ident) and n.p("BorderStyle").name == "bsSingle":
            out.append(f"{pre}BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;")
        al = n.p("Alignment")
        if isinstance(al, Ident) and al.name != "taCenter":
            out.append(f"{pre}TextAlign = System.Drawing.ContentAlignment."
                       f"{'MiddleLeft' if al.name == 'taLeftJustify' else 'MiddleRight'};")

    def _grid_props(self, n: Node, out: list[str], pre: str, name: str):
        t = n.type
        opts = n.p("Options")
        opts = opts if isinstance(opts, set) else None
        out.append(f"{pre}AllowUserToAddRows = false;")
        out.append(f"{pre}AllowUserToDeleteRows = false;")
        out.append(f"{pre}ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;")
        out.append(f"{pre}RowTemplate.Height = 20;")
        out.append(f"{pre}BackgroundColor = System.Drawing.SystemColors.Window;")
        if t == "TDBGrid":
            editing = opts is None or "dgEditing" in opts
            if not editing or n.p("ReadOnly") is True:
                out.append(f"{pre}ReadOnly = true;")
            if opts is not None and "dgRowSelect" in opts:
                out.append(f"{pre}SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;")
            if opts is not None and "dgIndicator" not in opts:
                out.append(f"{pre}RowHeadersVisible = false;")
            else:
                out.append(f"{pre}RowHeadersWidth = 24;")
            if opts is not None and "dgTitles" not in opts:
                out.append(f"{pre}ColumnHeadersVisible = false;")
            if isinstance(n.p("DataSource"), Ident):
                out.append(f"{pre}Tag = {cs_str(n.p('DataSource').name)};")
            cols = n.p("Columns")
            if isinstance(cols, Collection) and cols.items:
                names = []
                used = set()
                for i, item in enumerate(cols.items):
                    fld = item.get("FieldName") if isinstance(item.get("FieldName"), str) else ""
                    base = ident_name(fld) or f"Col{i}"
                    cn = f"{name}_{base}"
                    k = 2
                    while cn.lower() in used:
                        cn = f"{name}_{base}{k}"
                        k += 1
                    used.add(cn.lower())
                    self.declare("System.Windows.Forms.DataGridViewTextBoxColumn", cn)
                    names.append(cn)
                    self.body.append(f"{cn}.Name = {cs_str(cn)};")
                    if fld:
                        self.body.append(f"{cn}.DataPropertyName = {cs_str(fld)};")
                    hdr = item.get("Title.Caption") if isinstance(item.get("Title.Caption"), str) else fld
                    self.body.append(f"{cn}.HeaderText = {cs_str(hdr)};")
                    if isinstance(item.get("Width"), int):
                        self.body.append(f"{cn}.Width = {max(2, item['Width'])};")
                    if item.get("Visible") is False:
                        self.body.append(f"{cn}.Visible = false;")
                    al = item.get("Alignment")
                    if isinstance(al, Ident) and al.name in ("taRightJustify", "taCenter"):
                        a = "MiddleRight" if al.name == "taRightJustify" else "MiddleCenter"
                        self.body.append(f"{cn}.DefaultCellStyle.Alignment = System.Windows.Forms.DataGridViewContentAlignment.{a};")
                out.append(f"{pre}Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {{ {', '.join(names)} }});")
                self.post.append(f"{name}.AutoGenerateColumns = false;")
        else:
            cc = n.p("ColCount", 5)
            rc = n.p("RowCount", 5)
            fr = n.p("FixedRows", 1)
            fcols = n.p("FixedCols", 1)
            dw = n.p("DefaultColWidth", 64)
            names = []
            for i in range(cc):
                cn = f"{name}_Col{i}"
                self.declare("System.Windows.Forms.DataGridViewTextBoxColumn", cn)
                names.append(cn)
                self.body.append(f"{cn}.Name = {cs_str(cn)};")
                self.body.append(f"{cn}.HeaderText = \"\";")
                self.body.append(f"{cn}.Width = {dw};")
                if i < fcols:
                    self.body.append(f"{cn}.Frozen = true;")
            if names:
                out.append(f"{pre}Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {{ {', '.join(names)} }});")
            if fr == 0:
                out.append(f"{pre}ColumnHeadersVisible = false;")
            out.append(f"{pre}RowHeadersVisible = false;")
            if rc - fr > 0:
                self.post.append(f"{name}.RowCount = {rc - fr};")

    # ---------- reparent ke shape/bevel/image ----------
    def reparent(self, parent: Ctl):
        kids = parent.children
        containers = [k for k in kids if k.node.type in GRAPHIC_CONTAINERS and not k.docked]
        if containers:
            moved = True
            while moved:
                moved = False
                for k in list(kids):
                    # Hanya graphic control (label, shape, image) yang dipindah; kontrol windowed
                    # tetap di parent aslinya sehingga selalu di atas shape, seperti di Delphi.
                    if k.docked or not k.graphic or k.cs_type == "System.Windows.Forms.Button":
                        continue
                    best = None
                    for g in containers:
                        if g is k or g not in kids:
                            continue
                        if g.w * g.h <= k.w * k.h:
                            continue
                        if g.x <= k.x and g.y <= k.y and k.x + k.w <= g.x + g.w and k.y + k.h <= g.y + g.h:
                            if best is None or g.w * g.h < best.w * best.h:
                                best = g
                    if best is not None:
                        kids.remove(k)
                        k.x -= best.x
                        k.y -= best.y
                        self.ctl_lines[k.field] = [
                            f"{k.field}.Location = new System.Drawing.Point({k.x}, {k.y});"
                            if ln.startswith(f"{k.field}.Location =") else ln
                            for ln in self.ctl_lines[k.field]
                        ]
                        best.children.append(k)
                        moved = True
        for k in parent.children:
            self.reparent(k)

    # ---------- generate ----------
    def generate(self):
        self.ctl_lines: dict[str, list[str]] = {}
        root = self.root
        root_ctl = Ctl(root, "", "System.Windows.Forms.Form")
        fc_root = color_expr(root.p("Font.Color", Ident("clWindowText"))) or "System.Drawing.SystemColors.WindowText"
        self.convert_children(root_ctl, root.children, self.form_font, fc_root)
        self.reparent(root_ctl)

        # susun Controls.Add; urutan z-order Delphi: windowed di atas graphic, yang dibuat belakangan di atas
        add_lines: dict[str, list[str]] = {}
        containers_with_children: list[str] = []

        def dock_of(k: Ctl) -> str | None:
            a = k.node.p("Align")
            return a.name if isinstance(a, Ident) and a.name != "alNone" else None

        def ordered(children: list[Ctl]) -> list[Ctl]:
            # Delphi menata Align berdasarkan jenis (alTop, alBottom, alLeft, alRight, alClient)
            # dan posisi; WinForms men-dock dari indeks terakhir ke indeks 0. Susun ulang agar sama.
            docked = [k for k in children if dock_of(k)]
            seq = (sorted([k for k in docked if dock_of(k) == "alTop"], key=lambda k: k.y)
                   + sorted([k for k in docked if dock_of(k) == "alBottom"], key=lambda k: -(k.y + k.h))
                   + sorted([k for k in docked if dock_of(k) == "alLeft"], key=lambda k: k.x)
                   + sorted([k for k in docked if dock_of(k) == "alRight"], key=lambda k: -(k.x + k.w))
                   + [k for k in docked if dock_of(k) == "alClient"])
            seq += [k for k in docked if k not in seq]
            free = [k for k in children if not dock_of(k)]
            windowed = [k for k in free if not k.graphic]
            graphic = list(reversed([k for k in free if k.graphic]))
            # Di Delphi kontrol windowed selalu di atas graphic control. Shape/Image yang menampung
            # kontrol windowed (hasil reparent) dinaikkan agar isinya tidak tertutup shape lain.
            graphic.sort(key=lambda k: 0 if has_windowed(k) else 1)
            return list(reversed(windowed)) + list(reversed(seq)) + graphic

        def has_windowed(c: Ctl) -> bool:
            return any((not k.graphic) or has_windowed(k) for k in c.children)

        def walk(c: Ctl):
            target = c.field or "this"
            lines = []
            if c.cs_type == "System.Windows.Forms.TabControl":
                # Tab page mengikuti urutan Delphi; kontrol lain (TTabControl) ke tab pertama.
                for k in c.children:
                    if k.cs_type == "System.Windows.Forms.TabPage":
                        lines.append(f"{target}.Controls.Add({k.field});")
                for k in ordered([k for k in c.children if k.cs_type != "System.Windows.Forms.TabPage"]):
                    lines.append(f"{target}.TabPages[0].Controls.Add({k.field});")
                ap = c.node.p("ActivePage")
                if isinstance(ap, Ident) and ap.name.lower() in self.name_map:
                    self.post.append(f"{c.field}.SelectedTab = {self.name_map[ap.name.lower()]};")
            else:
                for k in ordered(c.children):
                    lines.append(f"{target}.Controls.Add({k.field});")
            add_lines[target] = lines
            if c.children and c.field:
                containers_with_children.append(c.field)
            for k in c.children:
                walk(k)

        walk(root_ctl)

        # bangun body final
        final_body = []
        for ln in self.body:
            if ln.startswith("@@CTL:"):
                nm = ln[6:]
                final_body.extend(add_lines.get(nm, []))
                final_body.extend(self.ctl_lines[nm])
            else:
                final_body.append(ln)

        # properti form
        form_lines = []
        rp = root.props
        form_lines.append("AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);")
        form_lines.append("AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;")
        if "ClientWidth" in rp:
            form_lines.append(f"ClientSize = new System.Drawing.Size({rp['ClientWidth']}, {rp.get('ClientHeight', 300)});")
        elif "Width" in rp:
            form_lines.append(f"Size = new System.Drawing.Size({rp['Width']}, {rp.get('Height', 300)});")
        form_lines.extend(add_lines.get("this", []))
        ce = color_expr(rp.get("Color")) if "Color" in rp else None
        if ce:
            form_lines.append(f"BackColor = {ce};")
        form_lines.append(f"Font = {self.form_font.expr()};")
        if fc_root != "System.Drawing.SystemColors.WindowText":
            form_lines.append(f"ForeColor = {fc_root};")
        bs = rp.get("BorderStyle")
        if isinstance(bs, Ident):
            m = {"bsDialog": "FixedDialog", "bsSingle": "FixedSingle", "bsNone": "None",
                 "bsToolWindow": "FixedToolWindow", "bsSizeToolWin": "SizableToolWindow"}.get(bs.name)
            if m:
                form_lines.append(f"FormBorderStyle = System.Windows.Forms.FormBorderStyle.{m};")
        bi = rp.get("BorderIcons")
        if isinstance(bi, set):
            if "biMaximize" not in bi or (isinstance(bs, Ident) and bs.name in ("bsDialog", "bsSingle")):
                form_lines.append("MaximizeBox = false;")
            if "biMinimize" not in bi or (isinstance(bs, Ident) and bs.name == "bsDialog"):
                form_lines.append("MinimizeBox = false;")
            if "biSystemMenu" not in bi:
                form_lines.append("ControlBox = false;")
        elif isinstance(bs, Ident) and bs.name == "bsDialog":
            form_lines.append("MaximizeBox = false;")
            form_lines.append("MinimizeBox = false;")
        if rp.get("KeyPreview") is True:
            form_lines.append("KeyPreview = true;")
        if rp.get("AutoScroll") is True:
            form_lines.append("AutoScroll = true;")
        if isinstance(rp.get("FormStyle"), Ident) and rp["FormStyle"].name == "fsStayOnTop":
            form_lines.append("TopMost = true;")
        pos = rp.get("Position")
        posname = pos.name if isinstance(pos, Ident) else "poDesigned"
        if posname in ("poMainFormCenter", "poOwnerFormCenter"):
            form_lines.append("StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;")
        else:
            form_lines.append("StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;")
        if isinstance(rp.get("WindowState"), Ident) and rp["WindowState"].name == "wsMaximized":
            form_lines.append("WindowState = System.Windows.Forms.FormWindowState.Maximized;")
        icon = rp.get("Icon.Data")
        if isinstance(icon, Binary):
            raw = _find_image_payload(icon.data)
            if raw and raw.startswith(b"\x00\x00\x01\x00"):
                self.resources["$this.Icon"] = ("icon", raw)
                form_lines.append('Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");')
        form_lines.append(f"Name = {cs_str(self.cls)};")
        cap = rp.get("Caption")
        form_lines.append(f"Text = {cs_str(cap) if isinstance(cap, str) else cs_str('')};")
        root_ctl.field = "this"
        self.map_events(root, "this", "FORM")
        # pindahkan event form (ditambahkan ke self.body paling akhir) ke form_lines
        form_events = [ln for ln in self.body if ln.startswith("this.")]
        final_body = [ln for ln in final_body if not ln.startswith("this.")]
        form_lines.extend(form_events)

        if self.tooltip_needed:
            self.fields.insert(0, ("System.Windows.Forms.ToolTip", "toolTip1"))
            self.creates.insert(0, "toolTip1 = new System.Windows.Forms.ToolTip(components);")

        return self._designer(final_body, form_lines, containers_with_children)

    def _designer(self, body, form_lines, containers):
        L = []
        L.append("// <auto-generated>")
        L.append(f"// Dihasilkan oleh tools/dfm2winforms dari {self.unit}.dfm.")
        L.append("// Boleh diubah lewat WinForms Designer; jalankan ulang konverter akan menimpa file ini.")
        L.append("// </auto-generated>")
        L.append("#nullable disable")
        L.append("")
        L.append(f"namespace {NAMESPACE}")
        L.append("{")
        L.append(f"    partial class {self.cls}")
        L.append("    {")
        L.append("        private System.ComponentModel.IContainer components = null;")
        L.append("")
        L.append("        protected override void Dispose(bool disposing)")
        L.append("        {")
        L.append("            if (disposing && (components != null))")
        L.append("            {")
        L.append("                components.Dispose();")
        L.append("            }")
        L.append("            base.Dispose(disposing);")
        L.append("        }")
        L.append("")
        L.append("        #region Windows Form Designer generated code")
        L.append("")
        L.append("        private void InitializeComponent()")
        L.append("        {")
        I = " " * 12
        L.append(I + "components = new System.ComponentModel.Container();")
        if self.resources:
            L.append(I + f"System.ComponentModel.ComponentResourceManager resources = "
                         f"new System.ComponentModel.ComponentResourceManager(typeof({self.cls}));")
        for ln in self.creates:
            L.append(I + ln)
        susp = [c for c in containers]
        for c in susp:
            L.append(I + f"{c}.SuspendLayout();")
        L.append(I + "SuspendLayout();")
        for ln in body:
            for sub in ln.split("\n"):
                L.append(I + sub)
        L.append(I + "// ")
        L.append(I + f"// {self.cls}")
        L.append(I + "// ")
        for ln in form_lines:
            L.append(I + ln)
        for c in reversed(susp):
            L.append(I + f"{c}.ResumeLayout(false);")
            L.append(I + f"{c}.PerformLayout();")
        L.append(I + "ResumeLayout(false);")
        L.append(I + "PerformLayout();")
        L.append("        }")
        L.append("")
        L.append("        #endregion")
        L.append("")
        # internal: di Delphi komponen form bersifat published dan sering diakses dari form lain.
        for typ, nm in self.fields:
            L.append(f"        internal {typ} {nm};")
        L.append("    }")
        L.append("}")
        return "\n".join(L) + "\n"

    def generated_cs(self) -> str:
        L = ["// <auto-generated>",
             f"// Dihasilkan oleh tools/dfm2winforms dari {self.unit}.dfm. Jangan diubah manual.",
             "// </auto-generated>",
             "#nullable disable",
             "",
             f"namespace {NAMESPACE}",
             "{",
             f"    partial class {self.cls}",
             "    {"]
        if self.skipped:
            L.append("        // Komponen Delphi yang tidak dikonversi (data/laporan/non-visual):")
            for s in self.skipped:
                L.append(f"        //   {s}")
            L.append("")
        if self.unmapped_events:
            L.append("        // Event Delphi yang belum punya padanan WinForms:")
            for s in self.unmapped_events:
                L.append(f"        //   {s}")
            L.append("")
        L.append("        /// <summary>Pengaturan runtime dari DFM yang tidak didukung WinForms Designer.</summary>")
        L.append("        private void ApplyDelphiDefaults()")
        L.append("        {")
        for ln in self.post:
            L.append("            " + ln)
        L.append("        }")
        L.append("    }")
        L.append("}")
        return "\n".join(L) + "\n"

    def code_cs(self, title: str) -> str:
        L = [f"namespace {NAMESPACE};", "",
             f"/// <summary>",
             f"/// {title}",
             f"/// Port UI dari {self.unit}.pas ({self.root.type}). Logika masih TODO.",
             f"/// </summary>",
             f"public partial class {self.cls} : Form",
             "{",
             f"    public {self.cls}()",
             "    {",
             "        InitializeComponent();",
             "        ApplyDelphiDefaults();",
             "    }"]
        items = sorted(self.handlers.items(), key=lambda kv: self.pas_lines.get(kv[1]["delphi"].lower(), 10 ** 9))
        for hn, info in items:
            args = info["args"]
            arg = next(iter(args)) if len(args) == 1 else "System.EventArgs"
            arg = arg.replace("System.Windows.Forms.", "").replace("System.", "").replace("UPKPro.Controls.", "Controls.")
            line = self.pas_lines.get(info["delphi"].lower())
            src = f"{self.unit}.pas:{line}" if line else f"{self.unit}.pas (tidak ditemukan)"
            L.append("")
            L.append(f"    private void {hn}(object? sender, {arg} e)")
            L.append("    {")
            L.append(f"        // TODO: port dari {src} ({', '.join(info['uses'][:4])}{', ...' if len(info['uses']) > 4 else ''})")
            L.append("    }")
        L.append("}")
        return "\n".join(L) + "\n"

    def resx(self) -> str:
        head = RESX_HEAD
        body = []
        for key, (kind, data) in self.resources.items():
            b64 = base64.b64encode(data).decode()
            wrapped = "\n".join("        " + b64[i:i + 80] for i in range(0, len(b64), 80))
            typ = "System.Drawing.Icon, System.Drawing" if kind == "icon" else "System.Drawing.Bitmap, System.Drawing"
            body.append(f'  <data name="{key}" type="{typ}" mimetype="application/x-microsoft.net.object.bytearray.base64">\n'
                        f"    <value>\n{wrapped}\n</value>\n  </data>")
        return head + "\n".join(body) + "\n</root>\n"


def convert_mask(mask: str) -> str:
    m = mask.split(";")[0]
    out = []
    esc = False
    for ch in m:
        if esc:
            out.append("\\" + ch)
            esc = False
            continue
        if ch == "\\":
            esc = True
        elif ch == "!":
            continue
        elif ch == "l":
            out.append("?")
        elif ch == "C":
            out.append("&")
        elif ch == "c":
            out.append("C")
        else:
            out.append(ch)
    return "".join(out)


RESX_HEAD = """<?xml version="1.0" encoding="utf-8"?>
<root>
  <xsd:schema id="root" xmlns="" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:msdata="urn:schemas-microsoft-com:xml-msdata">
    <xsd:import namespace="http://www.w3.org/XML/1998/namespace" />
    <xsd:element name="root" msdata:IsDataSet="true">
      <xsd:complexType>
        <xsd:choice maxOccurs="unbounded">
          <xsd:element name="metadata">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" />
              </xsd:sequence>
              <xsd:attribute name="name" use="required" type="xsd:string" />
              <xsd:attribute name="type" type="xsd:string" />
              <xsd:attribute name="mimetype" type="xsd:string" />
              <xsd:attribute ref="xml:space" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="assembly">
            <xsd:complexType>
              <xsd:attribute name="alias" type="xsd:string" />
              <xsd:attribute name="name" type="xsd:string" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="data">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
                <xsd:element name="comment" type="xsd:string" minOccurs="0" msdata:Ordinal="2" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" msdata:Ordinal="1" />
              <xsd:attribute name="type" type="xsd:string" msdata:Ordinal="3" />
              <xsd:attribute name="mimetype" type="xsd:string" msdata:Ordinal="4" />
              <xsd:attribute ref="xml:space" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="resheader">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" />
            </xsd:complexType>
          </xsd:element>
        </xsd:choice>
      </xsd:complexType>
    </xsd:element>
  </xsd:schema>
  <resheader name="resmimetype">
    <value>text/microsoft-resx</value>
  </resheader>
  <resheader name="version">
    <value>2.0</value>
  </resheader>
  <resheader name="reader">
    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  <resheader name="writer">
    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  <assembly alias="System.Drawing" name="System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a" />
"""


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    force_code = "--force-code" in sys.argv
    src, dst = Path(args[0]), Path(args[1])
    dst.mkdir(parents=True, exist_ok=True)
    summary = []
    for unit in FORM_UNITS:
        dfm = src / f"{unit}.dfm"
        pas = src / f"{unit}.pas"
        root = parse_dfm(dfm)
        pas_text = pas.read_bytes().decode("cp1252", errors="replace")
        gen = FormGenerator(unit, root, pas_text)
        designer = gen.generate()
        cls = gen.cls
        (dst / f"{cls}.Designer.cs").write_text(designer, encoding="utf-8")
        (dst / f"{cls}.Generated.cs").write_text(gen.generated_cs(), encoding="utf-8")
        (dst / f"{cls}.resx").write_text(gen.resx(), encoding="utf-8")
        code = dst / f"{cls}.cs"
        title = root.p("Caption") if isinstance(root.p("Caption"), str) and root.p("Caption") else cls
        if force_code or not code.exists():
            code.write_text(gen.code_cs(title), encoding="utf-8")
        n_ctl = len(gen.fields)
        summary.append(f"{unit:18} -> {cls:12} kontrol={n_ctl:4} handler={len(gen.handlers):3} "
                       f"gambar={len(gen.resources):3} dilewati={len(gen.skipped):3}")
        for w in gen.warnings:
            summary.append(f"    ! {w}")
        for e in gen.unmapped_events:
            summary.append(f"    ? event {e}")
    print("\n".join(summary))


if __name__ == "__main__":
    main()
