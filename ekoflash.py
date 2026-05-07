import os
import sys
import subprocess
import threading
import time
import tkinter as tk
from tkinter import filedialog, ttk

# ═══════════════════════════════════════════════════════════════
#   NOVA THEME — DUAL-PANEL MATRIX DARK (AKRO EDITION)
#   Deep obsidian core · Neon lime signals · Violet partition marks
#   Project: github.com/akro7/TURBO
# ═══════════════════════════════════════════════════════════════
BG_ROOT      = "#05070D"
BG_SIDEBAR   = "#080C14"
BG_PANEL     = "#0A0E18"
BG_CARD      = "#0D1220"
BG_ROW_A     = "#0F1525"
BG_ROW_B     = "#0C1120"
BG_ENTRY     = "#070B14"
BG_HDR       = "#060A12"

NE_LIME      = "#39FF6E"   # neon lime   — FASTBOOT
NE_VIOLET    = "#BF6EFF"   # neon violet — ODIN
NE_AMBER     = "#FFB347"   # amber       — warnings/browse
NE_CYAN      = "#00EEFF"   # cyan        — info
NE_RED       = "#FF3B5C"   # red         — danger
NE_PINK      = "#FF2D9F"   # pink        — reboot

FG_BRIGHT    = "#DEE8F0"
FG_MID       = "#5A7090"
FG_DIM       = "#1E2E40"

BORDER_DIM   = "#141E2E"
BORDER_ACT   = "#1E304A"

FONT_LOGO    = ("Courier New", 20, "bold")
FONT_MONO_B  = ("Courier New", 8,  "bold")
FONT_MONO    = ("Courier New", 8)
FONT_SMALL   = ("Courier New", 7)
FONT_MED     = ("Courier New", 9,  "bold")
FONT_PANEL   = ("Courier New", 10, "bold")


# ══════════════════════════════════════════════════════════════
#   SMART PATH RESOLVER — يدعم PyInstaller + مجلد bin + مجلد src
# ══════════════════════════════════════════════════════════════
def _resolve_exe(filename: str) -> str:
    """
    يبحث عن ملف تنفيذي في المسارات التالية بالترتيب:
      1. مجلد التطبيق (base)          — TURBO/
      2. TURBO/bin/
      3. مجلد العمل الحالي (cwd)
      4. TURBO/cwd/bin/
      5. PATH الخاص بالنظام (shutil)
    يعيد المسار الكامل إذا وُجد، وإلا اسم الملف لاستخدامه من PATH.
    """
    if getattr(sys, "frozen", False):
        # حالة PyInstaller (ekoflash.exe compiled)
        base = sys._MEIPASS
    else:
        base = os.path.dirname(os.path.abspath(__file__))

    cwd = os.getcwd()

    candidates = [
        # جذر المشروع TURBO/
        os.path.join(base, filename),
        # TURBO/bin/
        os.path.join(base, "bin", filename),
        # مجلد العمل
        os.path.join(cwd, filename),
        os.path.join(cwd, "bin", filename),
        # مجلد المشروع صعوداً (في حال تشغيل من src/ أو app/)
        os.path.join(base, "..", filename),
        os.path.join(base, "..", "bin", filename),
    ]

    for path in candidates:
        norm = os.path.normpath(path)
        if os.path.isfile(norm):
            return norm

    # الملف غير موجود — أعِد الاسم فقط لعله موجود في PATH
    return filename


def _resolve_src_dir(subpath: str) -> str:
    """
    يُعيد المسار الكامل لمجلد داخل src/
    مثال: _resolve_src_dir("protocol/odin") → TURBO/src/protocol/odin
    """
    if getattr(sys, "frozen", False):
        base = sys._MEIPASS
    else:
        base = os.path.dirname(os.path.abspath(__file__))

    candidates = [
        os.path.join(base, "src", subpath),
        os.path.join(base, subpath),
        os.path.join(os.getcwd(), "src", subpath),
        os.path.join(os.getcwd(), subpath),
        os.path.join(base, "..", "src", subpath),
    ]

    for path in candidates:
        norm = os.path.normpath(path)
        if os.path.isdir(norm):
            return norm

    return os.path.join(base, "src", subpath)   # قيمة افتراضية


# ──────────────────────────────────────────────────────────────
def make_pill(parent, text, bg, fg, w=None, font=FONT_MONO_B, pad_x=8, pad_y=4):
    kw = dict(bg=bg, fg=fg, font=font, relief="flat", padx=pad_x, pady=pad_y, cursor="hand2")
    if w:
        kw["width"] = w
    return tk.Label(parent, text=text, **kw)


def hline(parent, color=BORDER_DIM, h=1, pady=0):
    tk.Frame(parent, bg=color, height=h).pack(fill="x", pady=pady)


def section_badge(parent, text, color):
    f = tk.Frame(parent, bg=BG_PANEL)
    f.pack(fill="x", pady=(8, 3))
    tk.Frame(f, bg=color, width=3, height=14).pack(side="left")
    tk.Label(f, text=f"  {text}", bg=BG_PANEL, fg=color,
             font=FONT_MONO_B).pack(side="left")
    tk.Frame(f, bg=FG_DIM, height=1).pack(side="left", fill="x",
                                           expand=True, padx=(6, 0), pady=7)
    return f


# ──────────────────────────────────────────────────────────────
class NeonBtn(tk.Frame):
    def __init__(self, parent, text, cmd, accent=NE_LIME,
                 width=10, small=False, **kw):
        super().__init__(parent, bg=parent.cget("bg"), **kw)
        font = ("Courier New", 7, "bold") if small else FONT_MONO_B
        self._a = accent
        self._lbl = tk.Label(self, text=text, bg=BG_CARD, fg=accent,
                             font=font, width=width, pady=4, padx=6,
                             cursor="hand2", relief="flat",
                             highlightbackground=accent, highlightthickness=1)
        self._lbl.pack()
        self._lbl.bind("<Button-1>", lambda e: cmd())
        self._lbl.bind("<Enter>",    self._on)
        self._lbl.bind("<Leave>",    self._off)

    def _on(self, e):
        self._lbl.config(bg=self._a, fg=BG_CARD)

    def _off(self, e):
        self._lbl.config(bg=BG_CARD, fg=self._a)


# ──────────────────────────────────────────────────────────────
class PartRow(tk.Frame):
    """صف واحد من جدول FASTBOOT لقسم معين."""

    def __init__(self, parent, idx, name, var, browse_cmd, flash_cmd, accent):
        bg = BG_ROW_A if idx % 2 == 0 else BG_ROW_B
        super().__init__(parent, bg=bg, height=36)
        self.pack(fill="x")
        self.pack_propagate(False)

        tk.Label(self, text=f"{idx:02}", bg=bg, fg=FG_DIM,
                 font=FONT_SMALL, width=3).pack(side="left", padx=(5, 0))

        tk.Label(self, text=name.upper(), bg=bg, fg=accent,
                 font=("Courier New", 7, "bold"), width=9,
                 anchor="w").pack(side="left", padx=4)

        self.ent = tk.Entry(self, textvariable=var,
                            bg=BG_ENTRY, fg=NE_CYAN,
                            insertbackground=NE_CYAN,
                            font=FONT_MONO, relief="flat", bd=0,
                            highlightthickness=1,
                            highlightbackground=BORDER_DIM,
                            highlightcolor=accent)
        self.ent.pack(side="left", fill="x", expand=True, ipady=5, padx=4)

        NeonBtn(self, "BRW",   browse_cmd, NE_AMBER, 4).pack(side="left", padx=2)
        NeonBtn(self, "FLASH", flash_cmd,  accent,   5).pack(side="left", padx=(0, 5))


# ──────────────────────────────────────────────────────────────
class OdinFileRow(tk.Frame):
    """صف واحد من جدول ODIN لفتحة ملف (slot)."""

    COLORS = {
        "BL":       NE_AMBER,
        "AP":       NE_VIOLET,
        "CP":       NE_LIME,
        "CSC":      NE_CYAN,
        "USERDATA": NE_RED,
    }

    def __init__(self, parent, idx, slot, check_var, path_var,
                 browse_cmd, flash_cmd=None):
        bg = BG_ROW_A if idx % 2 == 0 else BG_ROW_B
        super().__init__(parent, bg=bg, height=38)
        self.pack(fill="x")
        self.pack_propagate(False)

        col = self.COLORS.get(slot, NE_VIOLET)

        tk.Checkbutton(self, variable=check_var, bg=bg,
                       selectcolor=BG_ENTRY, activebackground=bg,
                       fg=col, activeforeground=col).pack(side="left", padx=6)

        tk.Label(self, text=slot, bg=col, fg="#000000",
                 font=("Courier New", 8, "bold"),
                 width=8, pady=3).pack(side="left", padx=(0, 6))

        self.ent = tk.Entry(self, textvariable=path_var,
                            bg=BG_ENTRY, fg=col, insertbackground=col,
                            font=FONT_MONO, relief="flat", bd=0,
                            highlightthickness=1,
                            highlightbackground=BORDER_DIM,
                            highlightcolor=col)
        self.ent.pack(side="left", fill="x", expand=True, ipady=6, padx=4)

        NeonBtn(self, "SELECT", browse_cmd, col, 7).pack(side="left", padx=(4, 4))
        if flash_cmd:
            NeonBtn(self, "FLASH", flash_cmd, col, 5).pack(side="left", padx=(0, 8))


# ══════════════════════════════════════════════════════════════
class EkoFlashV3:
    def __init__(self, root):
        self.root = root
        self.root.title("EKO FLASH PRO  v3.0  ·  by AHMED YOUNIS")
        self.root.geometry("980x840")
        self.root.configure(bg=BG_ROOT)
        self.root.resizable(False, False)

        # ── مسارات المحركات التنفيذية (محسّنة) ──────────────────
        # fastboot.exe  → TURBO/fastboot.exe  أو  TURBO/bin/fastboot.exe
        self.fastboot_exe = _resolve_exe("fastboot.exe")

        # adb.exe       → TURBO/adb.exe  أو  TURBO/bin/adb.exe
        self.adb_exe      = _resolve_exe("adb.exe")

        # ekoflash.exe  → المحرك C++ المُجمَّع من src/protocol/odin/
        #                 يُبحث في: TURBO/ ثم TURBO/bin/ ثم PATH
        self.odin_exe     = _resolve_exe("ekoflash.exe")

        # ── مسارات مجلدات src/ (للمرجع واللوج) ──────────────────
        self.src_app      = _resolve_src_dir("app")
        self.src_core     = _resolve_src_dir("core")
        self.src_io       = _resolve_src_dir("io")
        self.src_platform = _resolve_src_dir("platform")
        self.src_odin     = _resolve_src_dir("protocol/odin")
        self.src_3p       = _resolve_src_dir("third_party")

        # ── متغيرات الحالة ────────────────────────────────────────
        self.part_vars       = {}
        self.odin_vars       = {}
        self.odin_opts_vars  = {}
        self.pit_var         = tk.StringVar()
        self.auto_reboot_var = tk.BooleanVar(value=True)
        self.progress_var    = tk.DoubleVar()
        self._dev_state      = "OFFLINE"
        self._pulse          = 0

        self._setup_styles()
        self._build_topbar()
        self._build_body()
        self._pulse_tick()
        threading.Thread(target=self._monitor, daemon=True).start()

    # ══════════════════════════════════════════════════════════
    #   STYLES
    # ══════════════════════════════════════════════════════════
    def _setup_styles(self):
        s = ttk.Style()
        s.theme_use("default")
        s.configure("TNotebook", background=BG_CARD, borderwidth=0)
        s.configure("TNotebook.Tab",
                    background=BG_PANEL, foreground=FG_MID,
                    font=("Courier New", 8, "bold"), padding=[12, 4])
        s.map("TNotebook.Tab",
              background=[("selected", BG_CARD)],
              foreground=[("selected", NE_VIOLET)])
        for name, color in [("Lime", NE_LIME), ("Violet", NE_VIOLET)]:
            s.configure(f"{name}.Horizontal.TProgressbar",
                        thickness=10, background=color,
                        troughcolor=BG_ENTRY, borderwidth=0)

    # ══════════════════════════════════════════════════════════
    #   TOP BAR
    # ══════════════════════════════════════════════════════════
    def _build_topbar(self):
        bar = tk.Frame(self.root, bg=BG_SIDEBAR, height=64)
        bar.pack(fill="x")
        bar.pack_propagate(False)

        # ── يسار: شعار + اسم المطوّر ──
        left = tk.Frame(bar, bg=BG_SIDEBAR)
        left.pack(side="left", padx=18, pady=10)

        logo_row = tk.Frame(left, bg=BG_SIDEBAR)
        logo_row.pack(anchor="w")
        tk.Label(logo_row, text="EKO",    bg=BG_SIDEBAR, fg=NE_LIME,
                 font=("Courier New", 18, "bold")).pack(side="left")
        tk.Label(logo_row, text=" FLASH ", bg=BG_SIDEBAR, fg=FG_BRIGHT,
                 font=("Courier New", 18, "bold")).pack(side="left")
        tk.Label(logo_row, text="PRO",    bg=BG_SIDEBAR, fg=NE_VIOLET,
                 font=("Courier New", 18, "bold")).pack(side="left")

        dev_row = tk.Frame(left, bg=BG_SIDEBAR)
        dev_row.pack(anchor="w")
        tk.Label(dev_row, text="v3.0  ·  DEV: ",
                 bg=BG_SIDEBAR, fg=FG_MID,
                 font=("Courier New", 7)).pack(side="left")
        tk.Label(dev_row, text="AHMED YOUNIS",
                 bg=BG_SIDEBAR, fg=NE_AMBER,
                 font=("Courier New", 9, "bold")).pack(side="left")

        # ── يمين: بطاقات الحالة ──
        right = tk.Frame(bar, bg=BG_SIDEBAR)
        right.pack(side="right", padx=18, pady=10)

        # بطاقة الجهاز
        dev_card = tk.Frame(right, bg=BG_CARD,
                            highlightbackground=BORDER_ACT,
                            highlightthickness=1)
        dev_card.pack(side="left", padx=8)
        tk.Label(dev_card, text="DEVICE", bg=BG_CARD, fg=FG_MID,
                 font=FONT_SMALL).pack(padx=12, pady=(5, 1))
        self._dot = tk.Label(dev_card, text="◉  OFFLINE",
                             bg=BG_CARD, fg=NE_RED, font=FONT_MONO_B)
        self._dot.pack(padx=12, pady=(1, 5))

        # بطاقة الوضع
        info_card = tk.Frame(right, bg=BG_CARD,
                             highlightbackground=BORDER_ACT,
                             highlightthickness=1)
        info_card.pack(side="left")
        tk.Label(info_card, text="MODE", bg=BG_CARD, fg=FG_MID,
                 font=FONT_SMALL).pack(padx=12, pady=(5, 1))
        tk.Label(info_card, text="DUAL PANEL", bg=BG_CARD, fg=NE_CYAN,
                 font=FONT_MONO_B).pack(padx=12, pady=(1, 5))

        # خط فاصل
        tk.Frame(self.root, bg=NE_LIME,  height=1).pack(fill="x")
        tk.Frame(self.root, bg=FG_DIM,   height=1).pack(fill="x")

    # ══════════════════════════════════════════════════════════
    #   BODY
    # ══════════════════════════════════════════════════════════
    def _build_body(self):
        body = tk.Frame(self.root, bg=BG_ROOT)
        body.pack(fill="both", expand=True)

        self._build_sidebar(body)

        right = tk.Frame(body, bg=BG_ROOT)
        right.pack(side="left", fill="both", expand=True)

        self._build_fastboot_panel(right)
        tk.Frame(right, bg=NE_VIOLET, height=1).pack(fill="x")
        self._build_odin_panel(right)

    # ══════════════════════════════════════════════════════════
    #   SIDEBAR
    # ══════════════════════════════════════════════════════════
    def _build_sidebar(self, parent):
        sb = tk.Frame(parent, bg=BG_SIDEBAR, width=170)
        sb.pack(side="left", fill="y")
        sb.pack_propagate(False)

        tk.Frame(sb, bg=BORDER_ACT, width=1).pack(side="right", fill="y")

        pad = tk.Frame(sb, bg=BG_SIDEBAR)
        pad.pack(fill="both", expand=True, padx=10, pady=10)

        # ── إحصائيات الجهاز ──
        tk.Label(pad, text="DEVICE STATS", bg=BG_SIDEBAR, fg=NE_CYAN,
                 font=("Courier New", 7, "bold")).pack(anchor="w", pady=(4, 2))
        hline(pad, NE_CYAN, pady=2)

        self._stat_frames = {}
        stats = [
            ("STATUS",   "OFFLINE", NE_RED),
            ("PROTOCOL", "—",       FG_MID),
            ("SERIAL",   "—",       FG_MID),
        ]
        for key, val, col in stats:
            row = tk.Frame(pad, bg=BG_SIDEBAR)
            row.pack(fill="x", pady=2)
            tk.Label(row, text=key, bg=BG_SIDEBAR, fg=FG_MID,
                     font=FONT_SMALL, width=9, anchor="w").pack(side="left")
            lbl = tk.Label(row, text=val, bg=BG_SIDEBAR, fg=col,
                           font=("Courier New", 7, "bold"), anchor="w")
            lbl.pack(side="left", fill="x")
            self._stat_frames[key] = lbl

        # ── أوامر سريعة ──
        hline(pad, BORDER_ACT, pady=6)
        tk.Label(pad, text="QUICK CMD", bg=BG_SIDEBAR, fg=NE_AMBER,
                 font=("Courier New", 7, "bold")).pack(anchor="w", pady=(0, 4))

        qcmds = [
            ("REBOOT SYS",   self.reboot_device,              NE_LIME),
            ("REBOOT BL",    self._reboot_bootloader,         NE_AMBER),
            ("REBOOT REC",   self._reboot_recovery,           NE_CYAN),
            ("ADB SIDELOAD", self.adb_sideload,               NE_VIOLET),
            ("WIPE DATA",    self.wipe_data,                  NE_RED),
            ("ERASE SYSTEM", lambda: self.erase_part("system"), NE_RED),
        ]
        for txt, cmd, col in qcmds:
            btn = tk.Label(pad, text=f"▶  {txt}",
                           bg=BG_CARD, fg=col,
                           font=("Courier New", 7, "bold"),
                           anchor="w", pady=4, padx=6, cursor="hand2",
                           highlightbackground=FG_DIM, highlightthickness=1)
            btn.pack(fill="x", pady=2)
            btn.bind("<Button-1>", lambda e, c=cmd: c())
            btn.bind("<Enter>",   lambda e, b=btn, c=col: b.config(bg=c, fg="#000"))
            btn.bind("<Leave>",   lambda e, b=btn, c=col: b.config(bg=BG_CARD, fg=c))

        # ── خيارات ──
        hline(pad, BORDER_ACT, pady=6)
        tk.Checkbutton(pad, text=" Auto-Reboot",
                       variable=self.auto_reboot_var,
                       bg=BG_SIDEBAR, fg=FG_MID,
                       selectcolor=BG_ENTRY,
                       activebackground=BG_SIDEBAR,
                       activeforeground=NE_LIME,
                       font=("Courier New", 8)).pack(anchor="w")

        hline(pad, BORDER_ACT, pady=6)
        NeonBtn(pad, "CLEAR LOGS", self._clear_all_logs,
                NE_RED, 13, small=True).pack(anchor="w", pady=2)

        # ── تذييل: مسار المحرك ──
        hline(pad, BORDER_ACT, pady=4)
        exe_name = os.path.basename(self.odin_exe)
        tk.Label(pad, text=f"ENGINE: {exe_name}",
                 bg=BG_SIDEBAR, fg=FG_DIM,
                 font=FONT_SMALL, wraplength=145, justify="left").pack(
                     anchor="w", pady=(2, 0))

        tk.Frame(pad, bg=BORDER_ACT, height=1).pack(fill="x", side="bottom", pady=4)
        tk.Label(pad, text="© 2025 AKRO",
                 bg=BG_SIDEBAR, fg=FG_DIM,
                 font=FONT_SMALL).pack(side="bottom")

    # ══════════════════════════════════════════════════════════
    #   FASTBOOT PANEL
    # ══════════════════════════════════════════════════════════
    def _build_fastboot_panel(self, parent):
        panel = tk.Frame(parent, bg=BG_PANEL)
        panel.pack(fill="both", expand=True)

        # رأس اللوحة
        phdr = tk.Frame(panel, bg=BG_HDR, height=28)
        phdr.pack(fill="x")
        phdr.pack_propagate(False)

        tk.Label(phdr, text=" ⚡ ", bg=NE_LIME, fg="#000000",
                 font=("Courier New", 9, "bold")).pack(side="left")
        tk.Label(phdr, text="  FASTBOOT PARTITION FLASH",
                 bg=BG_HDR, fg=NE_LIME, font=FONT_MED).pack(side="left")
        tk.Label(phdr, text=f"fastboot.exe  [{os.path.basename(self.fastboot_exe)}]",
                 bg=BG_HDR, fg=FG_DIM, font=FONT_SMALL).pack(side="right", padx=10)

        body = tk.Frame(panel, bg=BG_PANEL)
        body.pack(fill="both", expand=True, padx=8, pady=4)

        # رأس الجدول
        th = tk.Frame(body, bg=BG_HDR)
        th.pack(fill="x", pady=(0, 1))
        for txt, w in [("  #", 3), ("PARTITION", 10), ("IMAGE FILE PATH", 0), ("", 14)]:
            tk.Label(th, text=txt, bg=BG_HDR, fg=FG_MID,
                     font=FONT_SMALL, width=w, anchor="w").pack(
                         side="left", padx=4, pady=3)

        hline(body, NE_LIME)

        parts = ["boot", "recovery", "system", "vendor",
                 "product", "vbmeta", "vendor_boot", "userdata"]
        colors_cycle = [NE_LIME, NE_CYAN, NE_VIOLET, NE_AMBER] * 2

        tbl = tk.Frame(body, bg=BG_PANEL)
        tbl.pack(fill="both", expand=True)

        for i, p in enumerate(parts):
            v = tk.StringVar()
            self.part_vars[p] = v
            PartRow(tbl, i + 1, p, v,
                    lambda x=p: self.browse(x, "FASTBOOT"),
                    lambda x=p: self.flash_fastboot(x),
                    colors_cycle[i])

        hline(body, NE_LIME, pady=3)
        fa_row = tk.Frame(body, bg=BG_PANEL)
        fa_row.pack(fill="x", pady=2)
        NeonBtn(fa_row, "⚡ FLASH ALL SELECTED",
                self.flash_all_fastboot, NE_LIME, 20).pack(side="left", padx=4)

        hline(body, BORDER_ACT, pady=2)
        tk.Label(body, text="FASTBOOT LOG",
                 bg=BG_PANEL, fg=NE_LIME, font=FONT_SMALL).pack(anchor="w")
        self.fb_log = tk.Text(body, height=4, bg=BG_ENTRY, fg=NE_LIME,
                              font=FONT_MONO, relief="flat", bd=0,
                              highlightthickness=1,
                              highlightbackground=BORDER_DIM,
                              insertbackground=NE_LIME)
        self.fb_log.pack(fill="x", pady=2)

    # ══════════════════════════════════════════════════════════
    #   ODIN PANEL
    # ══════════════════════════════════════════════════════════
    def _build_odin_panel(self, parent):
        panel = tk.Frame(parent, bg=BG_PANEL)
        panel.pack(fill="both", expand=True)

        # رأس اللوحة
        phdr = tk.Frame(panel, bg=BG_HDR, height=28)
        phdr.pack(fill="x")
        phdr.pack_propagate(False)

        tk.Label(phdr, text=" ◈ ", bg=NE_VIOLET, fg="#000000",
                 font=("Courier New", 9, "bold")).pack(side="left")
        tk.Label(phdr, text="  ODIN ENGINE CORE FLASH",
                 bg=BG_HDR, fg=NE_VIOLET, font=FONT_MED).pack(side="left")

        prog_hdr = tk.Frame(phdr, bg=BG_HDR)
        prog_hdr.pack(side="right", padx=10, pady=4)

        self.percent_lbl = tk.Label(prog_hdr, text="0%",
                                    bg=BG_HDR, fg=NE_VIOLET, font=FONT_SMALL)
        self.percent_lbl.pack(side="right", padx=4)

        self.progress_bar = ttk.Progressbar(
            prog_hdr, variable=self.progress_var, maximum=100,
            style="Violet.Horizontal.TProgressbar", length=120)
        self.progress_bar.pack(side="right")

        tk.Label(prog_hdr,
                 text=f"ekoflash  [{os.path.basename(self.odin_exe)}]",
                 bg=BG_HDR, fg=FG_DIM,
                 font=FONT_SMALL).pack(side="right", padx=(0, 8))

        body = tk.Frame(panel, bg=BG_PANEL)
        body.pack(fill="both", expand=True, padx=8, pady=4)

        left  = tk.Frame(body, bg=BG_PANEL)
        left.pack(side="left", fill="both", expand=True)

        right = tk.Frame(body, bg=BG_PANEL, width=240)
        right.pack(side="right", fill="y", padx=(8, 0))
        right.pack_propagate(False)

        # ── فتحات الملفات ──
        hline(left, NE_VIOLET)

        slotbar = tk.Frame(left, bg=BG_HDR)
        slotbar.pack(fill="x")
        for txt, w in [("✓", 4), ("SLOT", 9), ("BINARY FILE PATH", 0), ("", 8)]:
            tk.Label(slotbar, text=txt, bg=BG_HDR, fg=FG_MID,
                     font=FONT_SMALL, width=w, anchor="w").pack(
                         side="left", padx=4, pady=3)

        hline(left, BORDER_DIM)

        odin_slots = ["BL", "AP", "CP", "CSC", "USERDATA"]
        slots_tbl  = tk.Frame(left, bg=BG_PANEL)
        slots_tbl.pack(fill="x")

        for i, slot in enumerate(odin_slots):
            cv = tk.BooleanVar(value=False)
            pv = tk.StringVar()
            self.odin_vars[slot] = {"check": cv, "path": pv}
            OdinFileRow(slots_tbl, i, slot, cv, pv,
                        lambda s=slot: self._odin_browse(s),
                        lambda s=slot: self._flash_odin_single(s))

        hline(left, BORDER_DIM, pady=2)

        # صف PIT
        pit_row = tk.Frame(left, bg=BG_ROW_A, height=34)
        pit_row.pack(fill="x")
        pit_row.pack_propagate(False)
        tk.Label(pit_row, text="PIT", bg=NE_AMBER, fg="#000",
                 font=("Courier New", 8, "bold"),
                 width=8, pady=3).pack(side="left", padx=(28, 6))
        tk.Entry(pit_row, textvariable=self.pit_var,
                 bg=BG_ENTRY, fg=NE_AMBER, insertbackground=NE_AMBER,
                 font=FONT_MONO, relief="flat",
                 highlightthickness=1,
                 highlightbackground=BORDER_DIM,
                 highlightcolor=NE_AMBER).pack(
                     side="left", fill="x", expand=True, ipady=5, padx=4)
        NeonBtn(pit_row, "SELECT",
                lambda: self._odin_browse("PIT"), NE_AMBER, 7).pack(
                    side="left", padx=(4, 8))

        hline(left, BORDER_DIM, pady=3)
        ctrl = tk.Frame(left, bg=BG_PANEL)
        ctrl.pack(fill="x", pady=3)
        NeonBtn(ctrl, "▶  START ENGINE FLASH",
                self.start_odin_flash, NE_VIOLET, 20).pack(side="left", padx=(0, 6))
        NeonBtn(ctrl, "⊗ RESET",
                self.odin_reset, NE_RED, 8).pack(side="left")

        # ── يمين: لوج + نتيجة + خيارات ──
        tk.Label(right, text="ENGINE LOG",
                 bg=BG_PANEL, fg=NE_VIOLET, font=FONT_SMALL).pack(anchor="w")

        log_card = tk.Frame(right, bg=BG_ENTRY,
                            highlightbackground=BORDER_DIM, highlightthickness=1)
        log_card.pack(fill="both", expand=True, pady=2)

        self.odin_log = tk.Text(log_card, bg=BG_ENTRY, fg=NE_VIOLET,
                                font=FONT_MONO, relief="flat", bd=0,
                                insertbackground=NE_VIOLET)
        self.odin_log.pack(fill="both", expand=True, padx=4, pady=4, side="left")

        osb = tk.Scrollbar(log_card, command=self.odin_log.yview,
                           bg=BG_ENTRY, troughcolor=BG_ENTRY,
                           activebackground=NE_VIOLET, width=6)
        osb.pack(side="right", fill="y")
        self.odin_log.config(yscrollcommand=osb.set)

        self.pass_label = tk.Label(right, text="READY",
                                   bg=BG_CARD, fg=FG_MID,
                                   font=("Courier New", 13, "bold"),
                                   pady=6,
                                   highlightbackground=BORDER_ACT,
                                   highlightthickness=1)
        self.pass_label.pack(fill="x", pady=3)

        hline(right, BORDER_ACT, pady=2)
        tk.Label(right, text="OPTIONS",
                 bg=BG_PANEL, fg=NE_AMBER, font=FONT_SMALL).pack(anchor="w")

        opts = [
            ("Auto Reboot",   True),
            ("Nand Erase",    False),
            ("Re-Partition",  False),
            ("F. Reset Time", True),
        ]
        for name, val in opts:
            var = tk.BooleanVar(value=val)
            self.odin_opts_vars[name] = var
            tk.Checkbutton(right, text=name, variable=var,
                           bg=BG_PANEL, fg=FG_MID,
                           selectcolor=BG_ENTRY,
                           activebackground=BG_PANEL,
                           activeforeground=NE_VIOLET,
                           font=("Courier New", 8)).pack(anchor="w", pady=1)

    # ══════════════════════════════════════════════════════════
    #   LOGIC
    # ══════════════════════════════════════════════════════════
    def _write(self, msg: str, target: str = "FB"):
        ts   = time.strftime("%H:%M:%S")
        line = f"[{ts}] {msg}\n"
        if target == "FB":
            self.fb_log.insert(tk.END, line)
            self.fb_log.see(tk.END)
        elif target == "OD":
            self.odin_log.insert(tk.END, line)
            self.odin_log.see(tk.END)
        elif target == "BOTH":
            for log in (self.fb_log, self.odin_log):
                log.insert(tk.END, line)
                log.see(tk.END)

    def _clear_all_logs(self):
        self.fb_log.delete("1.0", tk.END)
        self.odin_log.delete("1.0", tk.END)

    def _pulse_tick(self):
        if self._dev_state == "OFFLINE":
            colors = [NE_RED, "#330010"]
            self._dot.config(fg=colors[self._pulse % 2])
        self._pulse += 1
        self.root.after(700, self._pulse_tick)

    # ──────────────────────────────────────────────────────────
    def _monitor(self):
        """مراقبة الجهاز كل 3 ثوانٍ عبر fastboot ثم ekoflash."""
        cf = subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0
        while True:
            try:
                # 1. تحقق من fastboot
                r = subprocess.run(
                    [self.fastboot_exe, "devices"],
                    capture_output=True, text=True, creationflags=cf,
                    timeout=5)
                if r.stdout.strip():
                    lines  = r.stdout.strip().splitlines()
                    serial = lines[0].split()[0] if lines else "—"
                    self._dev_state = "FASTBOOT"
                    self._dot.config(text="◉  FASTBOOT", fg=NE_LIME)
                    self._stat_frames["STATUS"].config(text="ONLINE",   fg=NE_LIME)
                    self._stat_frames["PROTOCOL"].config(text="FASTBOOT", fg=NE_CYAN)
                    self._stat_frames["SERIAL"].config(text=serial[:12], fg=FG_MID)
                else:
                    # 2. تحقق من وضع DOWNLOAD عبر ekoflash (src/protocol/odin)
                    try:
                        r2 = subprocess.run(
                            [self.odin_exe, "detect"],
                            capture_output=True, text=True, creationflags=cf,
                            timeout=5)
                        out = (r2.stdout + r2.stderr).lower()
                        if "detected" in out or "download" in out or "device" in out:
                            self._dev_state = "DOWNLOAD"
                            self._dot.config(text="◉  DOWNLOAD", fg=NE_VIOLET)
                            self._stat_frames["STATUS"].config(text="ONLINE",   fg=NE_VIOLET)
                            self._stat_frames["PROTOCOL"].config(text="DOWNLOAD", fg=NE_VIOLET)
                            self._stat_frames["SERIAL"].config(text="Samsung",  fg=FG_MID)
                        else:
                            raise ValueError("no device")
                    except Exception:
                        self._dev_state = "OFFLINE"
                        self._dot.config(text="◉  OFFLINE", fg=NE_RED)
                        self._stat_frames["STATUS"].config(text="OFFLINE", fg=NE_RED)
                        self._stat_frames["PROTOCOL"].config(text="—",     fg=FG_DIM)
                        self._stat_frames["SERIAL"].config(text="—",       fg=FG_DIM)
            except Exception:
                pass
            time.sleep(3)

    # ──────────────────────────────────────────────────────────
    def browse(self, part: str, mode: str):
        types = (
            [("Flash Images", "*.img;*.bin;*.zip"), ("All Files", "*.*")]
            if mode == "FASTBOOT"
            else [("Samsung Binaries", "*.tar;*.md5;*.pit"), ("All Files", "*.*")]
        )
        f = filedialog.askopenfilename(filetypes=types)
        if f:
            self.part_vars[part].set(f)

    def _odin_browse(self, slot: str):
        if slot == "PIT":
            types = [("PIT File", "*.pit"), ("All Files", "*.*")]
        else:
            types = [("Samsung Binaries", "*.tar;*.md5"), ("All Files", "*.*")]
        f = filedialog.askopenfilename(filetypes=types)
        if f:
            if slot == "PIT":
                self.pit_var.set(f)
            else:
                self.odin_vars[slot]["path"].set(f)
                self.odin_vars[slot]["check"].set(True)

    # ──────────────────────────────────────────────────────────
    def _exec(self, cmd: list, title: str, target: str = "FB"):
        """تنفيذ أمر خارجي مع قراءة المخرجات سطراً سطراً."""
        self._write(f"RUN: {title}", target)
        cf = subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0
        try:
            proc = subprocess.Popen(
                cmd,
                stdout=subprocess.PIPE,
                stderr=subprocess.STDOUT,
                text=True,
                creationflags=cf)
            for line in proc.stdout:
                if line.strip():
                    self._write(f" › {line.strip()}", target)
            proc.wait()
            if proc.returncode == 0:
                self._write(f"SUCCESS ✓ {title}", target)
                if target == "OD":
                    self.pass_label.config(text="PASS ✓", bg="#002010", fg=NE_LIME)
                    self.progress_var.set(100)
                    self.percent_lbl.config(text="100%")
                if self.auto_reboot_var.get() and target == "FB":
                    self.reboot_device()
            else:
                self._write(f"ERROR ✗ {title} (code {proc.returncode})", target)
                if target == "OD":
                    self.pass_label.config(text="FAIL ✗", bg="#200010", fg=NE_RED)
        except FileNotFoundError:
            self._write(f"CRITICAL: executable not found → {cmd[0]}", target)
            self._write(f"  تحقق من وجود الملف في: {os.path.dirname(os.path.abspath(cmd[0]))}", target)
        except Exception as ex:
            self._write(f"CRITICAL: {ex}", target)

    # ──────────────────────────────────────────────────────────
    # FASTBOOT OPERATIONS
    # ──────────────────────────────────────────────────────────
    def flash_fastboot(self, part: str):
        path = self.part_vars[part].get()
        if not path:
            self._write(f"ERROR: No file for [{part.upper()}]", "FB")
            return
        threading.Thread(
            target=self._exec,
            args=([self.fastboot_exe, "flash", part, path],
                  f"Flash {part.upper()}"),
            daemon=True
        ).start()

    def flash_all_fastboot(self):
        def run():
            for p, v in self.part_vars.items():
                if v.get():
                    self._exec(
                        [self.fastboot_exe, "flash", p, v.get()],
                        f"Flash {p.upper()}")
        threading.Thread(target=run, daemon=True).start()

    # ──────────────────────────────────────────────────────────
    # ODIN OPERATIONS
    # ──────────────────────────────────────────────────────────
    # خريطة تحويل اسم الفتحة → اسم العَلَم في ekoflash.exe
    # (يعتمد على src/protocol/odin/odin_cmd.hpp)
    SLOT_FLAGS = {
        "BL":       "bootloader",
        "AP":       "system",
        "CP":       "radio",
        "CSC":      "csc",
        "USERDATA": "userdata",
    }

    def _build_odin_cmd(self, selected: list) -> list:
        """يبني أمر ekoflash الكامل بناءً على الفتحات المحددة."""
        cmd = [self.odin_exe, "flash"]
        if self.pit_var.get():
            cmd += ["--pit", self.pit_var.get()]
        for slot, path in selected:
            flag = self.SLOT_FLAGS.get(slot, slot.lower())
            cmd += [f"--{flag}", path]
        # خيارات اختيارية من لوحة OPTIONS
        if self.odin_opts_vars.get("Nand Erase",    tk.BooleanVar()).get():
            cmd.append("--nand-erase")
        if self.odin_opts_vars.get("Re-Partition",  tk.BooleanVar()).get():
            cmd.append("--repartition")
        if self.odin_opts_vars.get("F. Reset Time", tk.BooleanVar()).get():
            cmd.append("--reset-time")
        if self.odin_opts_vars.get("Auto Reboot",   tk.BooleanVar()).get():
            cmd.append("--reboot")
        return cmd

    def _flash_odin_single(self, slot: str):
        """يفلاش فتحة واحدة فقط عبر ekoflash."""
        path = self.odin_vars[slot]["path"].get()
        if not path:
            self._write(f"ERROR: No file selected for [{slot}]", "OD")
            self.pass_label.config(text="NO FILE", bg="#201000", fg=NE_AMBER)
            return

        cmd = self._build_odin_cmd([(slot, path)])
        self.pass_label.config(text=f"→ {slot}", bg="#0D0020", fg=NE_VIOLET)
        self.progress_var.set(0)
        self.percent_lbl.config(text="0%")
        self._write(f"<CORE> Flash {slot}: {os.path.basename(path)}", "OD")

        threading.Thread(
            target=self._exec,
            args=(cmd, f"Flash {slot}", "OD"),
            daemon=True
        ).start()

    def start_odin_flash(self):
        """يفلاش كل الفتحات المحددة عبر ekoflash (src/protocol/odin)."""
        selected = [
            (slot, data["path"].get())
            for slot, data in self.odin_vars.items()
            if data["check"].get() and data["path"].get()
        ]
        if not selected:
            self._write("ERROR: No files checked!", "OD")
            self.pass_label.config(text="NO FILES", bg="#201000", fg=NE_AMBER)
            return

        self.pass_label.config(text="FLASHING…", bg="#0D0020", fg=NE_VIOLET)
        self.progress_var.set(0)
        self._write(f"<ENGINE> Multi-flash: {len(selected)} slot(s) queued", "OD")

        def run():
            total = len(selected)
            for i, (slot, path) in enumerate(selected):
                self._write(
                    f" ├ [{i+1}/{total}] {slot}: {os.path.basename(path)}", "OD")
                pct = ((i + 1) / total) * 60
                self.progress_var.set(pct)
                self.percent_lbl.config(text=f"{int(pct)}%")
                time.sleep(0.15)

            self._write(" └ Sending to device via engine…", "OD")
            cmd = self._build_odin_cmd(selected)
            self._exec(cmd, "Engine Multi-Flash", "OD")

        threading.Thread(target=run, daemon=True).start()

    # ──────────────────────────────────────────────────────────
    # ADB & MISC OPERATIONS
    # ──────────────────────────────────────────────────────────
    def adb_sideload(self):
        f = filedialog.askopenfilename(filetypes=[("Zip Update", "*.zip")])
        if f:
            threading.Thread(
                target=self._exec,
                args=([self.adb_exe, "sideload", f], "ADB Sideload"),
                daemon=True
            ).start()

    def wipe_data(self):
        threading.Thread(
            target=self._exec,
            args=([self.fastboot_exe, "-w"], "Wipe Data"),
            daemon=True
        ).start()

    def erase_part(self, part: str):
        threading.Thread(
            target=self._exec,
            args=([self.fastboot_exe, "erase", part], f"Erase {part.upper()}"),
            daemon=True
        ).start()

    def reboot_device(self):
        cf = subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0
        subprocess.run([self.fastboot_exe, "reboot"], creationflags=cf)
        self._write("Reboot → system", "FB")

    def _reboot_bootloader(self):
        cf = subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0
        subprocess.run([self.fastboot_exe, "reboot-bootloader"], creationflags=cf)
        self._write("Reboot → bootloader", "FB")

    def _reboot_recovery(self):
        cf = subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0
        subprocess.run([self.fastboot_exe, "reboot", "recovery"], creationflags=cf)
        self._write("Reboot → recovery", "FB")

    def odin_reset(self):
        for slot in self.odin_vars:
            self.odin_vars[slot]["check"].set(False)
            self.odin_vars[slot]["path"].set("")
        self.pit_var.set("")
        self.odin_log.delete("1.0", tk.END)
        self.pass_label.config(text="READY", bg=BG_CARD, fg=FG_MID)
        self.progress_var.set(0)
        self.percent_lbl.config(text="0%")


# ══════════════════════════════════════════════════════════════
if __name__ == "__main__":
    root = tk.Tk()
    app  = EkoFlashV3(root)
    root.mainloop()
