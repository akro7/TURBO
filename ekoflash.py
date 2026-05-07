import os
import sys
import subprocess
import threading
import time
import tkinter as tk
from tkinter import filedialog, ttk
import math

# ==================== ULTRA MODERN THEME - CYBER DARK ====================
BG_MAIN       = "#080C10"
BG_PANEL      = "#0D1117"
BG_CARD       = "#111820"
BG_ENTRY      = "#0A0F15"
BG_ROW        = "#0F1620"
BG_ROW_HOVER  = "#141E2A"

ACCENT_CYAN   = "#00D4FF"
ACCENT_GOLD   = "#FFB800"
ACCENT_GREEN  = "#00FF88"
ACCENT_RED    = "#FF2D55"
ACCENT_PURPLE = "#B44FFF"

FG_PRIMARY    = "#E8F0F8"
FG_SECONDARY  = "#6A8099"
FG_DIM        = "#2A3A4A"

BORDER_DIM    = "#1A2535"
BORDER_GLOW   = "#00D4FF"

BTN_FLASH     = "#00D4FF"
BTN_BROWSE    = "#FFB800"
BTN_DANGER    = "#FF2D55"
BTN_SUCCESS   = "#00FF88"
BTN_NEUTRAL   = "#1E2D3D"
BTN_PURPLE    = "#B44FFF"

FONT_TITLE    = ("Courier New", 30, "bold")
FONT_SUB      = ("Courier New", 10)
FONT_LABEL    = ("Courier New", 9, "bold")
FONT_ENTRY    = ("Courier New", 9)
FONT_BTN      = ("Courier New", 8, "bold")
FONT_LOG      = ("Courier New", 8)
FONT_HEAD     = ("Courier New", 11, "bold")
FONT_SECTION  = ("Courier New", 10, "bold")

class GlowCanvas(tk.Canvas):
    """Canvas with animated scanline effect"""
    def __init__(self, parent, **kw):
        super().__init__(parent, **kw)
        self._scan_y = 0
        self._animate()

    def _animate(self):
        self.delete("scan")
        h = self.winfo_height() or 800
        self.create_line(0, self._scan_y, 2000, self._scan_y,
                         fill="#00D4FF", width=1, tags="scan", stipple="gray12")
        self._scan_y = (self._scan_y + 2) % h
        self.after(40, self._animate)

class ModernBtn(tk.Frame):
    def __init__(self, parent, text, cmd, width=10, accent=BTN_FLASH, fg="#000000", small=False, **kw):
        super().__init__(parent, bg=BG_MAIN, **kw)
        self._accent = accent
        self._fg = fg
        font = ("Courier New", 7, "bold") if small else FONT_BTN
        self._btn = tk.Label(
            self, text=text, bg=accent, fg=fg,
            font=font, width=width, pady=5, padx=4,
            cursor="hand2", relief="flat"
        )
        self._btn.pack()
        self._btn.bind("<Button-1>", lambda e: cmd())
        self._btn.bind("<Enter>", self._hover_on)
        self._btn.bind("<Leave>", self._hover_off)
        # corner dots
        for corner in ["nw","ne","sw","se"]:
            dot = tk.Label(self, text="▪", bg=BG_MAIN, fg=accent, font=("Courier New", 5))
            dot.place(relx=0 if "w" in corner else 1, rely=0 if "n" in corner else 1,
                      anchor=corner)

    def _hover_on(self, e):
        self._btn.config(bg=BG_MAIN, fg=self._accent,
                         highlightbackground=self._accent, highlightthickness=1)

    def _hover_off(self, e):
        self._btn.config(bg=self._accent, fg=self._fg, highlightthickness=0)

class SectionLabel(tk.Frame):
    def __init__(self, parent, text, **kw):
        super().__init__(parent, bg=BG_MAIN, **kw)
        line_l = tk.Frame(self, bg=ACCENT_CYAN, height=1, width=20)
        line_l.pack(side="left", pady=8)
        tk.Label(self, text=f"  {text}  ", bg=BG_MAIN, fg=ACCENT_CYAN,
                 font=FONT_SECTION).pack(side="left")
        line_r = tk.Frame(self, bg=FG_DIM, height=1)
        line_r.pack(side="left", fill="x", expand=True, pady=8)

class EkoFlashGUI:
    def __init__(self, root):
        self.root = root
        self.root.title("EKO FLASH PRO v2.1  |  by AHMED YOUNIS")
        self.root.geometry("1100x900")
        self.root.configure(bg=BG_MAIN)
        self.root.resizable(False, False)

        self.fastboot_exe = self.get_resource_path("fastboot.exe")
        self.adb_exe      = self.get_resource_path("adb.exe")

        self.part_vars        = {}
        self.auto_reboot_var  = tk.BooleanVar(value=True)
        self.odin_vars        = {}
        self.odin_opts_vars   = {}
        self.pit_var          = tk.StringVar()
        self.current_mode     = "FASTBOOT"
        self.progress_var     = tk.DoubleVar()
        self._blink_state     = True
        self._device_status   = "OFFLINE"

        self._setup_styles()
        self._build_header()
        self._build_mode_bar()
        self._build_container()

        self._tick_status_bar()
        threading.Thread(target=self.monitor_device, daemon=True).start()

    # ------------------------------------------------------------------ helpers
    def get_resource_path(self, rel):
        if hasattr(sys, '_MEIPASS'):
            return os.path.join(sys._MEIPASS, rel)
        return os.path.join(os.path.abspath("."), rel)

    def _setup_styles(self):
        s = ttk.Style()
        s.theme_use("default")
        s.configure("TNotebook", background=BG_PANEL, borderwidth=0)
        s.configure("TNotebook.Tab", background=BG_CARD, foreground=FG_SECONDARY,
                    font=("Courier New", 9, "bold"), padding=[14, 5])
        s.map("TNotebook.Tab",
              background=[("selected", "#0A1520")],
              foreground=[("selected", ACCENT_CYAN)])
        s.configure("Cyan.Horizontal.TProgressbar", thickness=16,
                    background=ACCENT_CYAN, troughcolor=BG_CARD, borderwidth=0)
        s.configure("Green.Horizontal.TProgressbar", thickness=16,
                    background=ACCENT_GREEN, troughcolor=BG_CARD, borderwidth=0)

    # ------------------------------------------------------------------ header
    def _build_header(self):
        hdr = tk.Frame(self.root, bg=BG_MAIN, height=90)
        hdr.pack(fill="x")
        hdr.pack_propagate(False)

        # left decoration
        left_deco = tk.Frame(hdr, bg=BG_MAIN)
        left_deco.pack(side="left", padx=25, pady=15)
        for i, (c, w) in enumerate([(ACCENT_CYAN,3),(ACCENT_GOLD,1),(FG_DIM,1)]):
            tk.Frame(left_deco, bg=c, width=w, height=60).pack(side="left", padx=2)

        # title block
        title_blk = tk.Frame(hdr, bg=BG_MAIN)
        title_blk.pack(side="left", pady=10)

        tk.Label(title_blk, text="EKO FLASH PRO",
                 bg=BG_MAIN, fg=ACCENT_CYAN,
                 font=("Courier New", 28, "bold")).pack(anchor="w")

        sub_row = tk.Frame(title_blk, bg=BG_MAIN)
        sub_row.pack(anchor="w")
        tk.Label(sub_row, text="v2.1", bg=BG_MAIN, fg=ACCENT_GOLD,
                 font=("Courier New", 10, "bold")).pack(side="left")
        tk.Label(sub_row, text="  ▸  DEVELOPER: AHMED YOUNIS", bg=BG_MAIN,
                 fg=FG_SECONDARY, font=("Courier New", 9)).pack(side="left")

        # right: live status box
        right_blk = tk.Frame(hdr, bg=BG_MAIN)
        right_blk.pack(side="right", padx=25, pady=12)

        status_card = tk.Frame(right_blk, bg=BG_CARD,
                               highlightbackground=BORDER_DIM, highlightthickness=1)
        status_card.pack()

        tk.Label(status_card, text="DEVICE STATUS", bg=BG_CARD,
                 fg=FG_SECONDARY, font=("Courier New", 7, "bold")).pack(padx=15, pady=(8,2))

        self.status_dot = tk.Label(status_card, text="◉", bg=BG_CARD,
                                   fg=ACCENT_RED, font=("Courier New", 18))
        self.status_dot.pack()

        self.status_txt = tk.Label(status_card, text="OFFLINE", bg=BG_CARD,
                                   fg=ACCENT_RED, font=("Courier New", 8, "bold"))
        self.status_txt.pack(pady=(0, 8))

        # bottom border line
        tk.Frame(self.root, bg=ACCENT_CYAN, height=1).pack(fill="x")
        tk.Frame(self.root, bg=FG_DIM, height=1).pack(fill="x")

    def _tick_status_bar(self):
        if self._device_status != "OFFLINE":
            self._blink_state = True
        else:
            self._blink_state = not self._blink_state
            self.status_dot.config(fg=ACCENT_RED if self._blink_state else BG_CARD)
        self.root.after(600, self._tick_status_bar)

    # ------------------------------------------------------------------ mode bar
    def _build_mode_bar(self):
        bar = tk.Frame(self.root, bg=BG_PANEL, height=52)
        bar.pack(fill="x")
        bar.pack_propagate(False)

        inner = tk.Frame(bar, bg=BG_PANEL)
        inner.pack(pady=10, padx=25, side="left")

        self.btn_fb = self._mode_btn(inner, "⚡  FASTBOOT MODE", "FASTBOOT")
        self.btn_fb.pack(side="left", padx=(0, 6))

        self.btn_od = self._mode_btn(inner, "◈  ODIN MODE", "ODIN", active=False)
        self.btn_od.pack(side="left")

        # right side info
        info = tk.Frame(bar, bg=BG_PANEL)
        info.pack(side="right", padx=25)
        self.mode_indicator = tk.Label(info, text="[ FASTBOOT ]", bg=BG_PANEL,
                                       fg=ACCENT_CYAN, font=("Courier New", 10, "bold"))
        self.mode_indicator.pack()

        tk.Frame(self.root, bg=BORDER_DIM, height=1).pack(fill="x")

    def _mode_btn(self, parent, text, mode, active=True):
        bg = ACCENT_CYAN if active else BTN_NEUTRAL
        fg = "#000000" if active else FG_SECONDARY
        btn = tk.Label(parent, text=text, bg=bg, fg=fg,
                       font=("Courier New", 10, "bold"), padx=18, pady=5,
                       cursor="hand2", relief="flat")
        btn.bind("<Button-1>", lambda e: self.switch_mode(mode))
        return btn

    # ------------------------------------------------------------------ container
    def _build_container(self):
        self.container = tk.Frame(self.root, bg=BG_MAIN)
        self.container.pack(fill="both", expand=True, padx=0, pady=0)

        self.fastboot_frame = tk.Frame(self.container, bg=BG_MAIN)
        self.odin_frame     = tk.Frame(self.container, bg=BG_MAIN)

        self._build_fastboot_ui()
        self._build_odin_ui()

        self.fastboot_frame.pack(fill="both", expand=True)

    # ================================================================ FASTBOOT UI
    def _build_fastboot_ui(self):
        wrap = tk.Frame(self.fastboot_frame, bg=BG_MAIN)
        wrap.pack(fill="both", expand=True, padx=20, pady=12)

        # ---- left column: partition table ----
        left = tk.Frame(wrap, bg=BG_MAIN)
        left.pack(side="left", fill="both", expand=True, padx=(0, 10))

        SectionLabel(left, "PARTITION FLASH TABLE").pack(fill="x", pady=(0,6))

        # table header
        th = tk.Frame(left, bg=BG_PANEL)
        th.pack(fill="x")
        for txt, w in [("PARTITION", 13), ("IMAGE PATH", 42), ("", 13)]:
            tk.Label(th, text=txt, bg=BG_PANEL, fg=FG_SECONDARY,
                     font=("Courier New", 7, "bold"), width=w, anchor="w").pack(side="left", padx=4, pady=4)

        tk.Frame(left, bg=BORDER_DIM, height=1).pack(fill="x")

        parts = ["boot","recovery","system","vendor","product","vbmeta","vendor_boot","userdata"]
        self._fb_rows = []
        for i, p in enumerate(parts):
            row_bg = BG_ROW if i % 2 == 0 else BG_CARD
            row = tk.Frame(left, bg=row_bg, height=40)
            row.pack(fill="x")
            row.pack_propagate(False)
            self._fb_rows.append(row)

            # index
            tk.Label(row, text=f"{i+1:02d}", bg=row_bg, fg=FG_DIM,
                     font=("Courier New", 8), width=3).pack(side="left", padx=(6,0))

            # name with colored prefix
            name_f = tk.Frame(row, bg=row_bg, width=110)
            name_f.pack(side="left", padx=4)
            name_f.pack_propagate(False)
            tk.Label(name_f, text="▶ ", bg=row_bg, fg=ACCENT_CYAN,
                     font=("Courier New", 8)).pack(side="left")
            tk.Label(name_f, text=p.upper(), bg=row_bg, fg=FG_PRIMARY,
                     font=FONT_LABEL).pack(side="left")

            v = tk.StringVar()
            self.part_vars[p] = v

            ent = tk.Entry(row, textvariable=v, bg=BG_ENTRY, fg=ACCENT_CYAN,
                           insertbackground=ACCENT_CYAN, font=FONT_ENTRY,
                           relief="flat", bd=0, highlightthickness=1,
                           highlightbackground=BORDER_DIM, highlightcolor=ACCENT_CYAN)
            ent.pack(side="left", fill="x", expand=True, ipady=6, padx=6)
            ent.bind("<FocusIn>",  lambda e, r=row, b=row_bg: r.config(bg=BG_ROW_HOVER) or [w.config(bg=BG_ROW_HOVER) for w in r.winfo_children()])
            ent.bind("<FocusOut>", lambda e, r=row, b=row_bg: r.config(bg=b))

            ModernBtn(row, "BROWSE", lambda x=p: self.browse(x, "FASTBOOT"),
                      7, BTN_BROWSE, "#000").pack(side="left", padx=3)
            ModernBtn(row, "FLASH",  lambda x=p: self.flash_fastboot(x),
                      7, BTN_FLASH,  "#000").pack(side="left", padx=(0,6))

        tk.Frame(left, bg=BORDER_DIM, height=1).pack(fill="x", pady=4)

        # ---- quick actions ----
        SectionLabel(left, "QUICK ACTIONS").pack(fill="x", pady=(8,6))

        qa = tk.Frame(left, bg=BG_MAIN)
        qa.pack(fill="x")

        actions = [
            ("⚡ FLASH ALL",     self.flash_all_fastboot, BTN_BROWSE, "#000"),
            ("✕  ERASE SYSTEM",  lambda: self.erase_part("system"), BTN_DANGER, FG_PRIMARY),
            ("⊗  WIPE DATA",     self.wipe_data,           BTN_DANGER, FG_PRIMARY),
            ("↻  ADB SIDELOAD",  self.adb_sideload,        BTN_PURPLE, FG_PRIMARY),
            ("↺  REBOOT",        self.reboot_device,        BTN_FLASH,  "#000"),
        ]
        for txt, cmd, bg, fg in actions:
            ModernBtn(qa, txt, cmd, 14, bg, fg).pack(side="left", padx=3)

        # auto reboot checkbox
        chk_f = tk.Frame(left, bg=BG_MAIN)
        chk_f.pack(fill="x", pady=6)
        tk.Checkbutton(chk_f, text=" Auto-Reboot after flash",
                       variable=self.auto_reboot_var,
                       bg=BG_MAIN, fg=FG_SECONDARY, selectcolor=BG_CARD,
                       activebackground=BG_MAIN, activeforeground=ACCENT_CYAN,
                       font=("Courier New", 9)).pack(side="left")

        # ---- right column: log ----
        right = tk.Frame(wrap, bg=BG_MAIN, width=310)
        right.pack(side="right", fill="y")
        right.pack_propagate(False)

        SectionLabel(right, "OPERATION LOG").pack(fill="x", pady=(0,6))

        log_card = tk.Frame(right, bg=BG_CARD,
                            highlightbackground=BORDER_DIM, highlightthickness=1)
        log_card.pack(fill="both", expand=True)

        self.log_widget = tk.Text(log_card, bg=BG_CARD, fg=ACCENT_GREEN,
                                  font=FONT_LOG, relief="flat", bd=0,
                                  insertbackground=ACCENT_CYAN,
                                  selectbackground=ACCENT_CYAN, selectforeground="#000")
        self.log_widget.pack(fill="both", expand=True, padx=8, pady=8)

        log_sb = tk.Scrollbar(log_card, command=self.log_widget.yview,
                              bg=BG_CARD, troughcolor=BG_CARD,
                              activebackground=ACCENT_CYAN, width=8)
        log_sb.pack(side="right", fill="y")
        self.log_widget.config(yscrollcommand=log_sb.set)

        # clear btn
        ModernBtn(right, "CLEAR LOG", lambda: self.log_widget.delete("1.0","end"),
                  12, BTN_NEUTRAL, FG_SECONDARY, small=True).pack(pady=4)

    # ================================================================ ODIN UI
    def _build_odin_ui(self):
        wrap = tk.Frame(self.odin_frame, bg=BG_MAIN)
        wrap.pack(fill="both", expand=True, padx=20, pady=12)

        # ---- top status strip ----
        strip = tk.Frame(wrap, bg=BG_PANEL,
                         highlightbackground=BORDER_DIM, highlightthickness=1)
        strip.pack(fill="x", pady=(0, 12))

        # ID:COM
        com_blk = tk.Frame(strip, bg=BG_PANEL)
        com_blk.pack(side="left", padx=20, pady=10)
        tk.Label(com_blk, text="ID:COM", bg=BG_PANEL, fg=FG_SECONDARY,
                 font=("Courier New", 7, "bold")).pack()
        self.odin_com_label = tk.Label(com_blk, text="WAITING...", bg=BG_ENTRY,
                                       fg=ACCENT_GOLD, font=("Courier New", 12, "bold"),
                                       width=20, pady=6,
                                       highlightbackground=ACCENT_GOLD, highlightthickness=1)
        self.odin_com_label.pack()

        # PASS/FAIL
        pf_blk = tk.Frame(strip, bg=BG_PANEL)
        pf_blk.pack(side="left", padx=10, pady=10)
        tk.Label(pf_blk, text="RESULT", bg=BG_PANEL, fg=FG_SECONDARY,
                 font=("Courier New", 7, "bold")).pack()
        self.pass_label = tk.Label(pf_blk, text="READY", bg=BTN_NEUTRAL,
                                   fg=FG_PRIMARY, font=("Courier New", 16, "bold"),
                                   width=10, pady=6)
        self.pass_label.pack()

        # progress
        prog_blk = tk.Frame(strip, bg=BG_PANEL)
        prog_blk.pack(side="left", fill="x", expand=True, padx=20, pady=10)
        prog_hdr = tk.Frame(prog_blk, bg=BG_PANEL)
        prog_hdr.pack(fill="x")
        tk.Label(prog_hdr, text="FLASH PROGRESS", bg=BG_PANEL, fg=FG_SECONDARY,
                 font=("Courier New", 7, "bold")).pack(side="left")
        self.percent_lbl = tk.Label(prog_hdr, text="0%", bg=BG_PANEL,
                                    fg=ACCENT_CYAN, font=("Courier New", 9, "bold"))
        self.percent_lbl.pack(side="right")

        self.progress_bar = ttk.Progressbar(prog_blk, variable=self.progress_var,
                                            maximum=100, style="Cyan.Horizontal.TProgressbar")
        self.progress_bar.pack(fill="x", pady=(4,0))

        # ---- body ----
        body = tk.Frame(wrap, bg=BG_MAIN)
        body.pack(fill="both", expand=True)

        # left panel: notebook
        left = tk.Frame(body, bg=BG_MAIN, width=360)
        left.pack(side="left", fill="y", padx=(0,12))
        left.pack_propagate(False)

        SectionLabel(left, "CONTROLS").pack(fill="x", pady=(0,6))

        nb = ttk.Notebook(left)
        nb.pack(fill="both", expand=True)

        # LOG tab
        log_tab = tk.Frame(nb, bg=BG_CARD)
        nb.add(log_tab, text="  LOG  ")
        self.odin_log = tk.Text(log_tab, bg=BG_CARD, fg=ACCENT_GREEN,
                                font=FONT_LOG, relief="flat", bd=0)
        self.odin_log.pack(fill="both", expand=True, padx=8, pady=8)
        osb = tk.Scrollbar(log_tab, command=self.odin_log.yview,
                           bg=BG_CARD, troughcolor=BG_CARD,
                           activebackground=ACCENT_CYAN, width=8)
        osb.pack(side="right", fill="y")
        self.odin_log.config(yscrollcommand=osb.set)

        # OPTIONS tab
        opt_tab = tk.Frame(nb, bg=BG_CARD)
        nb.add(opt_tab, text="  OPTIONS  ")
        opts = [("Auto Reboot", True), ("Nand Erase", False),
                ("Re-Partition", False), ("F. Reset Time", True)]
        for name, val in opts:
            var = tk.BooleanVar(value=val)
            self.odin_opts_vars[name] = var
            row = tk.Frame(opt_tab, bg=BG_CARD)
            row.pack(fill="x", padx=15, pady=6)
            tk.Checkbutton(row, text=name, variable=var,
                           bg=BG_CARD, fg=FG_PRIMARY, selectcolor=BG_ENTRY,
                           activebackground=BG_CARD, activeforeground=ACCENT_CYAN,
                           font=("Courier New", 10)).pack(side="left")

        # PIT tab
        pit_tab = tk.Frame(nb, bg=BG_CARD)
        nb.add(pit_tab, text="  PIT  ")
        tk.Label(pit_tab, text="PIT Partition Table File:", bg=BG_CARD,
                 fg=FG_SECONDARY, font=("Courier New", 9)).pack(anchor="w", padx=15, pady=(18,4))
        pit_ent = tk.Entry(pit_tab, textvariable=self.pit_var,
                           bg=BG_ENTRY, fg=ACCENT_CYAN, font=FONT_ENTRY,
                           relief="flat", highlightthickness=1,
                           highlightbackground=BORDER_DIM, highlightcolor=ACCENT_CYAN)
        pit_ent.pack(fill="x", padx=15, ipady=7)
        ModernBtn(pit_tab, "SELECT PIT FILE",
                  lambda: self.browse("PIT","ODIN"), 18, BTN_BROWSE, "#000").pack(pady=15, padx=15, anchor="w")

        # right panel: file selection
        right = tk.Frame(body, bg=BG_MAIN)
        right.pack(side="left", fill="both", expand=True)

        SectionLabel(right, "FLASH FILE SELECTION").pack(fill="x", pady=(0,6))

        files_card = tk.Frame(right, bg=BG_CARD,
                              highlightbackground=BORDER_DIM, highlightthickness=1)
        files_card.pack(fill="both", expand=True)

        # files header
        fh = tk.Frame(files_card, bg=BG_PANEL)
        fh.pack(fill="x")
        for txt, w in [("", 4), ("SLOT", 6), ("FILE PATH", 38), ("", 6)]:
            tk.Label(fh, text=txt, bg=BG_PANEL, fg=FG_SECONDARY,
                     font=("Courier New", 7, "bold"), width=w).pack(side="left", padx=4, pady=4)

        tk.Frame(files_card, bg=BORDER_DIM, height=1).pack(fill="x")

        odin_parts = ["BL", "AP", "CP", "CSC", "USERDATA"]
        colors_map  = {"BL": ACCENT_GOLD, "AP": ACCENT_CYAN, "CP": ACCENT_GREEN,
                       "CSC": ACCENT_PURPLE, "USERDATA": ACCENT_RED}

        for i, p in enumerate(odin_parts):
            row_bg = BG_CARD if i % 2 == 0 else BG_ROW
            row = tk.Frame(files_card, bg=row_bg, height=50)
            row.pack(fill="x", pady=1)
            row.pack_propagate(False)

            c_var = tk.BooleanVar(value=False)
            p_var = tk.StringVar()
            self.odin_vars[p] = {"check": c_var, "path": p_var}

            col = colors_map.get(p, ACCENT_CYAN)

            # checkbox
            tk.Checkbutton(row, variable=c_var, bg=row_bg, selectcolor=BG_ENTRY,
                           activebackground=row_bg).pack(side="left", padx=10)

            # slot badge
            badge = tk.Label(row, text=p, bg=col, fg="#000000",
                             font=("Courier New", 9, "bold"), width=7, pady=4)
            badge.pack(side="left", padx=(0,8))

            # entry
            ent = tk.Entry(row, textvariable=p_var, bg=BG_ENTRY, fg=col,
                           insertbackground=col, font=FONT_ENTRY,
                           relief="flat", highlightthickness=1,
                           highlightbackground=BORDER_DIM, highlightcolor=col)
            ent.pack(side="left", fill="x", expand=True, ipady=8, padx=5)

            # browse btn
            def make_browse(part):
                return lambda: (self.browse(part, "ODIN"),
                                self.odin_vars[part]["check"].set(True))
            ModernBtn(row, "SELECT", make_browse(p), 8, col, "#000").pack(side="left", padx=(6,10))

        # control buttons
        ctrl = tk.Frame(right, bg=BG_MAIN)
        ctrl.pack(fill="x", pady=10)

        ModernBtn(ctrl, "▶  START FLASH",  self.start_real_odin_flash,
                  22, BTN_SUCCESS, "#000").pack(side="left", padx=(0,8))
        ModernBtn(ctrl, "⊗  RESET ALL",    self.odin_reset,
                  14, BTN_NEUTRAL, FG_SECONDARY).pack(side="left")

    # ================================================================ LOGIC
    def write_log(self, msg, target="MAIN"):
        ts   = time.strftime("%H:%M:%S")
        line = f"[{ts}] {msg}\n"
        tag_map = {
            "SUCCESS": (ACCENT_GREEN, "suc"),
            "ERROR":   (ACCENT_RED,   "err"),
            "CRITICAL":(ACCENT_RED,   "crit"),
            "Process": (ACCENT_GOLD,  "proc"),
        }
        if target in ("MAIN","BOTH"):
            self.log_widget.insert(tk.END, line)
            for kw, (col, tname) in tag_map.items():
                if kw in msg:
                    self.log_widget.tag_add(tname, "end-2l", "end-1l")
                    self.log_widget.tag_config(tname, foreground=col)
            self.log_widget.see(tk.END)
        if target in ("ODIN","BOTH"):
            self.odin_log.insert(tk.END, line)
            self.odin_log.see(tk.END)

    def switch_mode(self, mode):
        self.current_mode = mode
        if mode == "FASTBOOT":
            self.btn_fb.config(bg=ACCENT_CYAN, fg="#000000")
            self.btn_od.config(bg=BTN_NEUTRAL,  fg=FG_SECONDARY)
            self.odin_frame.pack_forget()
            self.fastboot_frame.pack(fill="both", expand=True)
            self.mode_indicator.config(text="[ FASTBOOT ]", fg=ACCENT_CYAN)
        else:
            self.btn_fb.config(bg=BTN_NEUTRAL,  fg=FG_SECONDARY)
            self.btn_od.config(bg=ACCENT_CYAN, fg="#000000")
            self.fastboot_frame.pack_forget()
            self.odin_frame.pack(fill="both", expand=True)
            self.mode_indicator.config(text="[ ODIN ]", fg=ACCENT_GOLD)

    def browse(self, p, mode):
        if mode == "ODIN":
            types = [("Samsung Binaries", "*.tar;*.md5;*.pit"), ("All Files", "*.*")]
        else:
            types = [("Flash Images", "*.img;*.bin;*.zip"), ("All Files", "*.*")]
        f = filedialog.askopenfilename(filetypes=types)
        if f:
            if mode == "FASTBOOT":
                self.part_vars[p].set(f)
            else:
                if p == "PIT":
                    self.pit_var.set(f)
                else:
                    self.odin_vars[p]["path"].set(f)
                    self.odin_vars[p]["check"].set(True)

    def monitor_device(self):
        cf = subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0
        while True:
            try:
                res = subprocess.run([self.fastboot_exe, "devices"],
                                     capture_output=True, text=True, creationflags=cf)
                if res.stdout.strip():
                    self._device_status = "FASTBOOT"
                    self.odin_com_label.config(text="FASTBOOT ✓",
                                               bg="#001A10", fg=ACCENT_GREEN,
                                               highlightbackground=ACCENT_GREEN)
                    self.status_dot.config(fg=ACCENT_GREEN)
                    self.status_txt.config(text="FASTBOOT", fg=ACCENT_GREEN)
                else:
                    self._device_status = "OFFLINE"
                    self.odin_com_label.config(text="WAITING...",
                                               bg=BG_ENTRY, fg=ACCENT_GOLD,
                                               highlightbackground=ACCENT_GOLD)
                    self.status_dot.config(fg=ACCENT_RED)
                    self.status_txt.config(text="OFFLINE", fg=ACCENT_RED)
            except Exception:
                pass
            time.sleep(3)

    def flash_fastboot(self, part):
        path = self.part_vars[part].get()
        if not path:
            self.write_log(f"ERROR: No file selected for partition [{part.upper()}]")
            return
        threading.Thread(
            target=self._exec_cmd,
            args=([self.fastboot_exe, "flash", part, path], f"Flash {part.upper()}")
        ).start()

    def adb_sideload(self):
        f = filedialog.askopenfilename(filetypes=[("Zip Update", "*.zip")])
        if f:
            threading.Thread(
                target=self._exec_cmd,
                args=([self.adb_exe, "sideload", f], "ADB Sideload")
            ).start()

    def start_real_odin_flash(self):
        self.write_log("<OSM> Analysis Started...", "ODIN")
        self.pass_label.config(text="FLASHING", bg=ACCENT_GOLD, fg="#000")
        self.progress_var.set(0)
        self.percent_lbl.config(text="0%")

        selected = [(p, d["path"].get())
                    for p, d in self.odin_vars.items()
                    if d["check"].get() and d["path"].get()]

        if not selected:
            self.write_log("ERROR: No binary files selected!", "ODIN")
            self.pass_label.config(text="FAIL", bg=ACCENT_RED, fg=FG_PRIMARY)
            return

        def run():
            total = len(selected)
            for i, (p, path) in enumerate(selected):
                self.write_log(f"Processing: {p} ← {os.path.basename(path)}", "ODIN")
                time.sleep(0.4)
                pct = ((i + 1) / total) * 100
                self.progress_var.set(pct)
                self.percent_lbl.config(text=f"{int(pct)}%")

            map_names = {"BL":"BOOTLOADER","AP":"SYSTEM","CP":"RADIO","CSC":"HIDDEN"}
            cmd = [self.fastboot_exe]
            if self.pit_var.get():
                cmd += ["--pit", self.pit_var.get()]
            for p, path in selected:
                cmd += [f"--{map_names.get(p,p)}", path]

            self._exec_cmd(cmd, "Odin Multi-Flash", "ODIN")
            if self.odin_opts_vars["Auto Reboot"].get():
                self.reboot_device()

        threading.Thread(target=run, daemon=True).start()

    def _exec_cmd(self, cmd, title, target="MAIN"):
        self.write_log(f"Process: {title}", target)
        cf = subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0
        try:
            proc = subprocess.Popen(cmd, stdout=subprocess.PIPE,
                                    stderr=subprocess.STDOUT,
                                    text=True, creationflags=cf)
            for line in proc.stdout:
                if line.strip():
                    self.write_log(f"  › {line.strip()}", target)
            proc.wait()
            if proc.returncode == 0:
                self.write_log(f"SUCCESS: {title} completed.", target)
                if target == "ODIN":
                    self.pass_label.config(text="PASS!", bg=ACCENT_GREEN, fg="#000")
                    self.progress_var.set(100)
                    self.percent_lbl.config(text="100%")
                if self.auto_reboot_var.get() and target == "MAIN":
                    self.reboot_device()
            else:
                self.write_log(f"ERROR: {title} failed (code {proc.returncode})", target)
                if target == "ODIN":
                    self.pass_label.config(text="FAIL", bg=ACCENT_RED, fg=FG_PRIMARY)
        except Exception as ex:
            self.write_log(f"CRITICAL ERROR: {ex}", target)

    def flash_all_fastboot(self):
        for p, v in self.part_vars.items():
            if v.get():
                self.flash_fastboot(p)

    def erase_part(self, p):
        threading.Thread(
            target=self._exec_cmd,
            args=([self.fastboot_exe, "erase", p], f"Erase {p.upper()}")
        ).start()

    def wipe_data(self):
        threading.Thread(
            target=self._exec_cmd,
            args=([self.fastboot_exe, "-w"], "Wipe Data")
        ).start()

    def reboot_device(self):
        cf = subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0
        subprocess.run([self.fastboot_exe, "reboot"], creationflags=cf)
        self.write_log("Reboot command dispatched.")

    def odin_reset(self):
        for p in self.odin_vars:
            self.odin_vars[p]["check"].set(False)
            self.odin_vars[p]["path"].set("")
        self.pit_var.set("")
        self.odin_log.delete("1.0", tk.END)
        self.pass_label.config(text="READY", bg=BTN_NEUTRAL, fg=FG_PRIMARY)
        self.progress_var.set(0)
        self.percent_lbl.config(text="0%")


if __name__ == "__main__":
    root = tk.Tk()
    app = EkoFlashGUI(root)
    root.mainloop()
