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
# ═══════════════════════════════════════════════════════════════
BG_ROOT      = "#05070D"
BG_SIDEBAR   = "#080C14"
BG_PANEL     = "#0A0E18"
BG_CARD      = "#0D1220"
BG_ROW_A     = "#0F1525"
BG_ROW_B     = "#0C1120"
BG_ENTRY     = "#070B14"
BG_HDR       = "#060A12"

NE_LIME      = "#39FF6E"   # neon lime — FASTBOOT
NE_VIOLET    = "#BF6EFF"   # neon violet — ODIN
NE_AMBER     = "#FFB347"   # amber — warnings/browse
NE_CYAN      = "#00EEFF"   # cyan — info
NE_RED       = "#FF3B5C"   # red — danger
NE_PINK      = "#FF2D9F"   # pink — reboot

FG_BRIGHT    = "#DEE8F0"
FG_MID       = "#5A7090"
FG_DIM       = "#1E2E40"

BORDER_DIM   = "#141E2E"
BORDER_ACT   = "#1E304A"

FONT_LOGO    = ("Courier New", 20, "bold")
FONT_MONO_B  = ("Courier New", 8, "bold")
FONT_MONO    = ("Courier New", 8)
FONT_SMALL   = ("Courier New", 7)
FONT_MED     = ("Courier New", 9, "bold")
FONT_PANEL   = ("Courier New", 10, "bold")

# ──────────────────────────────────────────────────────────────
def make_pill(parent, text, bg, fg, w=None, font=FONT_MONO_B, pad_x=8, pad_y=4):
    kw = dict(bg=bg, fg=fg, font=font, relief="flat", padx=pad_x, pady=pad_y, cursor="hand2")
    if w: kw["width"] = w
    return tk.Label(parent, text=text, **kw)

def hline(parent, color=BORDER_DIM, h=1, pady=0):
    tk.Frame(parent, bg=color, height=h).pack(fill="x", pady=pady)

def section_badge(parent, text, color):
    f = tk.Frame(parent, bg=BG_PANEL)
    f.pack(fill="x", pady=(8, 3))
    tk.Frame(f, bg=color, width=3, height=14).pack(side="left")
    tk.Label(f, text=f"  {text}", bg=BG_PANEL, fg=color, font=FONT_MONO_B).pack(side="left")
    tk.Frame(f, bg=FG_DIM, height=1).pack(side="left", fill="x", expand=True, padx=(6, 0), pady=7)
    return f

# ──────────────────────────────────────────────────────────────
class NeonBtn(tk.Frame):
    def __init__(self, parent, text, cmd, accent=NE_LIME, width=10, small=False, **kw):
        super().__init__(parent, bg=parent.cget("bg"), **kw)
        font = ("Courier New", 7, "bold") if small else FONT_MONO_B
        self._a = accent
        self._lbl = tk.Label(self, text=text, bg=BG_CARD, fg=accent,
                             font=font, width=width, pady=4, padx=6,
                             cursor="hand2", relief="flat",
                             highlightbackground=accent, highlightthickness=1)
        self._lbl.pack()
        self._lbl.bind("<Button-1>", lambda e: cmd())
        self._lbl.bind("<Enter>", self._on)
        self._lbl.bind("<Leave>", self._off)

    def _on(self, e): self._lbl.config(bg=self._a, fg=BG_CARD)
    def _off(self, e): self._lbl.config(bg=BG_CARD, fg=self._a)

# ──────────────────────────────────────────────────────────────
class PartRow(tk.Frame):
    def __init__(self, parent, idx, name, var, browse_cmd, flash_cmd, accent):
        bg = BG_ROW_A if idx % 2 == 0 else BG_ROW_B
        super().__init__(parent, bg=bg, height=36)
        self.pack(fill="x")
        self.pack_propagate(False)

        tk.Label(self, text=f"{idx:02}", bg=bg, fg=FG_DIM, font=FONT_SMALL, width=3).pack(side="left", padx=(5, 0))
        tk.Label(self, text=name.upper(), bg=bg, fg=accent, font=("Courier New", 7, "bold"), width=9, anchor="w").pack(side="left", padx=4)
        
        self.ent = tk.Entry(self, textvariable=var, bg=BG_ENTRY, fg=NE_CYAN, insertbackground=NE_CYAN, 
                            font=FONT_MONO, relief="flat", bd=0, highlightthickness=1,
                            highlightbackground=BORDER_DIM, highlightcolor=accent)
        self.ent.pack(side="left", fill="x", expand=True, ipady=5, padx=4)

        NeonBtn(self, "BRW", browse_cmd, NE_AMBER, 4).pack(side="left", padx=2)
        NeonBtn(self, "FLASH", flash_cmd, accent, 5).pack(side="left", padx=(0, 5))

# ──────────────────────────────────────────────────────────────
class OdinFileRow(tk.Frame):
    COLORS = {"BL": NE_AMBER, "AP": NE_VIOLET, "CP": NE_LIME, "CSC": NE_CYAN, "USERDATA": NE_RED}
    def __init__(self, parent, idx, slot, check_var, path_var, browse_cmd, flash_cmd=None):
        bg = BG_ROW_A if idx % 2 == 0 else BG_ROW_B
        super().__init__(parent, bg=bg, height=38)
        self.pack(fill="x")
        self.pack_propagate(False)
        col = self.COLORS.get(slot, NE_VIOLET)

        tk.Checkbutton(self, variable=check_var, bg=bg, selectcolor=BG_ENTRY, activebackground=bg, fg=col, activeforeground=col).pack(side="left", padx=6)
        tk.Label(self, text=slot, bg=col, fg="#000", font=("Courier New", 8, "bold"), width=8, pady=3).pack(side="left", padx=(0, 6))

        self.ent = tk.Entry(self, textvariable=path_var, bg=BG_ENTRY, fg=col, insertbackground=col, font=FONT_MONO,
                            relief="flat", bd=0, highlightthickness=1, highlightbackground=BORDER_DIM, highlightcolor=col)
        self.ent.pack(side="left", fill="x", expand=True, ipady=6, padx=4)

        NeonBtn(self, "SELECT", browse_cmd, col, 7).pack(side="left", padx=(4, 4))
        if flash_cmd: NeonBtn(self, "FLASH", flash_cmd, col, 5).pack(side="left", padx=(0, 8))

# ══════════════════════════════════════════════════════════════
class EkoFlashV3:
    def __init__(self, root):
        self.root = root
        self.root.title("EKO FLASH PRO  v3.0  ·  by AHMED YOUNIS")
        self.root.geometry("980x840")
        self.root.configure(bg=BG_ROOT)
        self.root.resizable(False, False)

        # تحسين المسارات الذكي (يمنع أخطاء المسارات في الشاشات)
        self.fastboot_exe = self._res("fastboot.exe")
        self.adb_exe      = self._res("adb.exe")
        self.odin_exe     = self._res("ekoflash.exe") 

        self.part_vars       = {}
        self.part_rows       = {}
        self.odin_vars       = {}
        self.odin_rows       = {}
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

    def _res(self, filename):
        """دالة تحسين المسارات: تبحث في مجلد التطبيق، مجلد bin، ومجلدات النظام المؤقتة"""
        if getattr(sys, 'frozen', False): 
            base = sys._MEIPASS
        else: 
            base = os.path.dirname(os.path.abspath(__file__))
        
        paths_to_check = [
            os.path.join(base, filename),
            os.path.join(base, "bin", filename),
            os.path.join(os.getcwd(), filename),
            os.path.join(os.getcwd(), "bin", filename)
        ]
        for p in paths_to_check:
            if os.path.exists(p): return p
        return filename

    def _setup_styles(self):
        s = ttk.Style()
        s.theme_use("default")
        s.configure("Violet.Horizontal.TProgressbar", thickness=10, background=NE_VIOLET, troughcolor=BG_ENTRY, borderwidth=0)

    def _build_topbar(self):
        bar = tk.Frame(self.root, bg=BG_SIDEBAR, height=64)
        bar.pack(fill="x")
        bar.pack_propagate(False)

        left = tk.Frame(bar, bg=BG_SIDEBAR); left.pack(side="left", padx=18, pady=10)
        logo = tk.Frame(left, bg=BG_SIDEBAR); logo.pack(anchor="w")
        tk.Label(logo, text="EKO", bg=BG_SIDEBAR, fg=NE_LIME, font=("Courier New", 18, "bold")).pack(side="left")
        tk.Label(logo, text=" FLASH ", bg=BG_SIDEBAR, fg=FG_BRIGHT, font=("Courier New", 18, "bold")).pack(side="left")
        tk.Label(logo, text="PRO", bg=BG_SIDEBAR, fg=NE_VIOLET, font=("Courier New", 18, "bold")).pack(side="left")
        
        dev = tk.Frame(left, bg=BG_SIDEBAR); dev.pack(anchor="w")
        tk.Label(dev, text="v3.0  ·  DEV: ", bg=BG_SIDEBAR, fg=FG_MID, font=FONT_SMALL).pack(side="left")
        tk.Label(dev, text="AHMED YOUNIS", bg=BG_SIDEBAR, fg=NE_AMBER, font=("Courier New", 9, "bold")).pack(side="left")

        right = tk.Frame(bar, bg=BG_SIDEBAR); right.pack(side="right", padx=18, pady=10)
        for title, label_ref, color in [("DEVICE", "_dot", NE_RED), ("MODE", None, NE_CYAN)]:
            card = tk.Frame(right, bg=BG_CARD, highlightbackground=BORDER_ACT, highlightthickness=1)
            card.pack(side="left", padx=8)
            tk.Label(card, text=title, bg=BG_CARD, fg=FG_MID, font=FONT_SMALL).pack(padx=12, pady=(5, 1))
            lbl = tk.Label(card, text="◉  OFFLINE" if title=="DEVICE" else "DUAL PANEL", 
                           bg=BG_CARD, fg=color, font=FONT_MONO_B)
            lbl.pack(padx=12, pady=(1, 5))
            if label_ref: setattr(self, label_ref, lbl)

        tk.Frame(self.root, bg=NE_LIME, height=1).pack(fill="x")

    def _build_body(self):
        body = tk.Frame(self.root, bg=BG_ROOT); body.pack(fill="both", expand=True)
        self._build_sidebar(body)
        right = tk.Frame(body, bg=BG_ROOT); right.pack(side="left", fill="both", expand=True)
        self._build_fastboot_panel(right)
        tk.Frame(right, bg=NE_VIOLET, height=1).pack(fill="x")
        self._build_odin_panel(right)

    def _build_sidebar(self, parent):
        sb = tk.Frame(parent, bg=BG_SIDEBAR, width=170); sb.pack(side="left", fill="y")
        sb.pack_propagate(False)
        tk.Frame(sb, bg=BORDER_ACT, width=1).pack(side="right", fill="y")
        pad = tk.Frame(sb, bg=BG_SIDEBAR); pad.pack(fill="both", expand=True, padx=10, pady=10)

        tk.Label(pad, text="DEVICE STATS", bg=BG_SIDEBAR, fg=NE_CYAN, font=("Courier New", 7, "bold")).pack(anchor="w")
        hline(pad, NE_CYAN, pady=2)
        self._stat_frames = {}
        for k in ["STATUS", "PROTOCOL", "SERIAL"]:
            row = tk.Frame(pad, bg=BG_SIDEBAR); row.pack(fill="x", pady=2)
            tk.Label(row, text=k, bg=BG_SIDEBAR, fg=FG_MID, font=FONT_SMALL, width=9, anchor="w").pack(side="left")
            lbl = tk.Label(row, text="—", bg=BG_SIDEBAR, fg=FG_MID, font=("Courier New", 7, "bold"), anchor="w")
            lbl.pack(side="left", fill="x"); self._stat_frames[k] = lbl

        hline(pad, BORDER_ACT, pady=6)
        tk.Label(pad, text="QUICK CMD", bg=BG_SIDEBAR, fg=NE_AMBER, font=("Courier New", 7, "bold")).pack(anchor="w")
        qcmds = [("REBOOT SYS", self.reboot_device, NE_LIME), ("REBOOT BL", self._reboot_bootloader, NE_AMBER),
                 ("REBOOT REC", self._reboot_recovery, NE_CYAN), ("ADB SIDELOAD", self.adb_sideload, NE_VIOLET),
                 ("WIPE DATA", self.wipe_data, NE_RED), ("ERASE SYSTEM", lambda: self.erase_part("system"), NE_RED)]
        for txt, cmd, col in qcmds:
            btn = tk.Label(pad, text=f"▶ {txt}", bg=BG_CARD, fg=col, font=("Courier New", 7, "bold"), anchor="w",
                           pady=4, padx=6, cursor="hand2", highlightbackground=FG_DIM, highlightthickness=1)
            btn.pack(fill="x", pady=2)
            btn.bind("<Button-1>", lambda e, c=cmd: c())
            btn.bind("<Enter>", lambda e, b=btn, c=col: b.config(bg=c, fg="#000"))
            btn.bind("<Leave>", lambda e, b=btn, c=col: b.config(bg=BG_CARD, fg=c))

        tk.Checkbutton(pad, text=" Auto-Reboot", variable=self.auto_reboot_var, bg=BG_SIDEBAR, fg=FG_MID,
                       selectcolor=BG_ENTRY, font=("Courier New", 8)).pack(anchor="w", pady=10)
        NeonBtn(pad, "CLEAR LOGS", self._clear_all_logs, NE_RED, 13, small=True).pack(side="bottom", pady=10)

    def _build_fastboot_panel(self, parent):
        panel = tk.Frame(parent, bg=BG_PANEL); panel.pack(fill="both", expand=True)
        phdr = tk.Frame(panel, bg=BG_HDR, height=28); phdr.pack(fill="x")
        tk.Label(phdr, text=" ⚡ ", bg=NE_LIME, fg="#000", font=("Courier New", 9, "bold")).pack(side="left")
        tk.Label(phdr, text="  FASTBOOT PARTITION FLASH", bg=BG_HDR, fg=NE_LIME, font=FONT_MED).pack(side="left")

        body = tk.Frame(panel, bg=BG_PANEL); body.pack(fill="both", expand=True, padx=8, pady=4)
        parts = ["boot", "recovery", "system", "vendor", "product", "vbmeta", "vendor_boot", "userdata"]
        colors = [NE_LIME, NE_CYAN, NE_VIOLET, NE_AMBER] * 2
        for i, p in enumerate(parts):
            v = tk.StringVar(); self.part_vars[p] = v
            PartRow(body, i+1, p, v, lambda x=p: self.browse(x, "FASTBOOT"), lambda x=p: self.flash_fastboot(x), colors[i])

        ctrl = tk.Frame(body, bg=BG_PANEL); ctrl.pack(fill="x", pady=4)
        NeonBtn(ctrl, "⚡ FLASH ALL SELECTED", self.flash_all_fastboot, NE_LIME, 20).pack(side="left")
        self.fb_log = tk.Text(body, height=4, bg=BG_ENTRY, fg=NE_LIME, font=FONT_MONO, bd=0, highlightthickness=1, highlightbackground=BORDER_DIM)
        self.fb_log.pack(fill="x", pady=2)

    def _build_odin_panel(self, parent):
        panel = tk.Frame(parent, bg=BG_PANEL); panel.pack(fill="both", expand=True)
        phdr = tk.Frame(panel, bg=BG_HDR, height=28); phdr.pack(fill="x")
        tk.Label(phdr, text=" ◈ ", bg=NE_VIOLET, fg="#000", font=("Courier New", 9, "bold")).pack(side="left")
        tk.Label(phdr, text="  ODIN ENGINE CORE FLASH", bg=BG_HDR, fg=NE_VIOLET, font=FONT_MED).pack(side="left")
        
        prog_hdr = tk.Frame(phdr, bg=BG_HDR); prog_hdr.pack(side="right", padx=10, pady=4)
        self.percent_lbl = tk.Label(prog_hdr, text="0%", bg=BG_HDR, fg=NE_VIOLET, font=FONT_SMALL)
        self.percent_lbl.pack(side="right", padx=4)
        self.progress_bar = ttk.Progressbar(prog_hdr, variable=self.progress_var, maximum=100, style="Violet.Horizontal.TProgressbar", length=120)
        self.progress_bar.pack(side="right")

        body = tk.Frame(panel, bg=BG_PANEL); body.pack(fill="both", expand=True, padx=8, pady=4)
        left = tk.Frame(body, bg=BG_PANEL); left.pack(side="left", fill="both", expand=True)
        right = tk.Frame(body, bg=BG_PANEL, width=240); right.pack(side="right", fill="y", padx=(8, 0)); right.pack_propagate(False)

        for i, slot in enumerate(["BL", "AP", "CP", "CSC", "USERDATA"]):
            cv, pv = tk.BooleanVar(), tk.StringVar()
            self.odin_vars[slot] = {"check": cv, "path": pv}
            OdinFileRow(left, i, slot, cv, pv, lambda s=slot: self._odin_browse(s))

        pit_row = tk.Frame(left, bg=BG_ROW_A, height=34); pit_row.pack(fill="x")
        tk.Label(pit_row, text="PIT", bg=NE_AMBER, fg="#000", font=FONT_MONO_B, width=8).pack(side="left", padx=(28, 6))
        tk.Entry(pit_row, textvariable=self.pit_var, bg=BG_ENTRY, fg=NE_AMBER, font=FONT_MONO, relief="flat").pack(side="left", fill="x", expand=True, padx=4)
        NeonBtn(pit_row, "SELECT", lambda: self._odin_browse("PIT"), NE_AMBER, 7).pack(side="left", padx=8)

        NeonBtn(left, "▶ START ENGINE FLASH", self.start_odin_flash, NE_VIOLET, 22).pack(side="left", pady=10)
        NeonBtn(left, "⊗ RESET", self.odin_reset, NE_RED, 8).pack(side="left", padx=10, pady=10)

        self.odin_log = tk.Text(right, bg=BG_ENTRY, fg=NE_VIOLET, font=FONT_MONO, bd=0)
        self.odin_log.pack(fill="both", expand=True)
        self.pass_label = tk.Label(right, text="READY", bg=BG_CARD, fg=FG_MID, font=("Courier New", 12, "bold"), pady=5)
        self.pass_label.pack(fill="x", pady=4)

    # ════════════════════════════════ LOGIC
    def _write(self, msg, target="FB"):
        ts = time.strftime("%H:%M:%S")
        log = self.fb_log if target == "FB" else self.odin_log
        log.insert(tk.END, f"[{ts}] {msg}\n"); log.see(tk.END)

    def _clear_all_logs(self):
        self.fb_log.delete("1.0", tk.END); self.odin_log.delete("1.0", tk.END)

    def _pulse_tick(self):
        if self._dev_state == "OFFLINE":
            self._dot.config(fg=(NE_RED if self._pulse % 2 == 0 else "#330010"))
        self._pulse += 1; self.root.after(700, self._pulse_tick)

    def _monitor(self):
        cf = subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0
        while True:
            try:
                r = subprocess.run([self.fastboot_exe, "devices"], capture_output=True, text=True, creationflags=cf)
                if r.stdout.strip():
                    serial = r.stdout.strip().splitlines()[0].split()[0]
                    self._dev_state = "FASTBOOT"
                    self._dot.config(text="◉ FASTBOOT", fg=NE_LIME)
                    self._stat_frames["STATUS"].config(text="ONLINE", fg=NE_LIME)
                    self._stat_frames["PROTOCOL"].config(text="FASTBOOT", fg=NE_CYAN)
                    self._stat_frames["SERIAL"].config(text=serial[:12], fg=FG_MID)
                else:
                    r2 = subprocess.run([self.odin_exe, "detect"], capture_output=True, text=True, creationflags=cf)
                    if "detected" in r2.stdout.lower() or "download" in r2.stdout.lower():
                        self._dev_state = "DOWNLOAD"
                        self._dot.config(text="◉ DOWNLOAD", fg=NE_VIOLET)
                        self._stat_frames["STATUS"].config(text="ONLINE", fg=NE_VIOLET)
                        self._stat_frames["PROTOCOL"].config(text="ODIN/DL", fg=NE_VIOLET)
                    else:
                        self._dev_state = "OFFLINE"
                        self._dot.config(text="◉ OFFLINE", fg=NE_RED)
                        self._stat_frames["STATUS"].config(text="OFFLINE", fg=NE_RED)
                        self._stat_frames["PROTOCOL"].config(text="—", fg=FG_DIM)
            except: pass
            time.sleep(3)

    def browse(self, part, mode):
        f = filedialog.askopenfilename()
        if f: self.part_vars[part].set(f)

    def _odin_browse(self, slot):
        f = filedialog.askopenfilename()
        if f:
            if slot == "PIT": self.pit_var.set(f)
            else: self.odin_vars[slot]["path"].set(f); self.odin_vars[slot]["check"].set(True)

    def _exec(self, cmd, title, target="FB"):
        self._write(f"RUN: {title}", target)
        cf = subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0
        try:
            proc = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, creationflags=cf)
            for line in proc.stdout:
                if line.strip(): self._write(f" › {line.strip()}", target)
            proc.wait()
            if proc.returncode == 0:
                self._write(f"SUCCESS ✓ {title}", target)
                if target == "OD":
                    self.pass_label.config(text="PASS ✓", bg="#002010", fg=NE_LIME)
                    self.progress_var.set(100); self.percent_lbl.config(text="100%")
            else:
                self._write(f"ERROR ✗ {title} (Code {proc.returncode})", target)
                if target == "OD": self.pass_label.config(text="FAIL ✗", bg="#200010", fg=NE_RED)
        except Exception as e: self._write(f"CRITICAL: {e}", target)

    def flash_fastboot(self, part):
        path = self.part_vars[part].get()
        if path: threading.Thread(target=self._exec, args=([self.fastboot_exe, "flash", part, path], f"Flash {part}")).start()

    def flash_all_fastboot(self):
        def run():
            for p, v in self.part_vars.items():
                if v.get(): self._exec([self.fastboot_exe, "flash", p, v.get()], f"Flash {p}")
            if self.auto_reboot_var.get(): self.reboot_device()
        threading.Thread(target=run).start()

    def start_odin_flash(self):
        selected = [(s, d["path"].get()) for s, d in self.odin_vars.items() if d["check"].get() and d["path"].get()]
        if not selected: return
        def run():
            map_n = {"BL":"bootloader","AP":"system","CP":"radio","CSC":"csc","USERDATA":"userdata"}
            cmd = [self.odin_exe, "flash"]
            if self.pit_var.get(): cmd += ["--pit", self.pit_var.get()]
            total = len(selected)
            for i, (s, p) in enumerate(selected):
                cmd += [f"--{map_n.get(s, s.lower())}", p]
                pct = ((i + 1) / total) * 60
                self.progress_var.set(pct); self.percent_lbl.config(text=f"{int(pct)}%")
            self._exec(cmd, "Engine Multi-Flash", "OD")
        threading.Thread(target=run).start()

    def adb_sideload(self):
        f = filedialog.askopenfilename(filetypes=[("Zip Update", "*.zip")])
        if f: threading.Thread(target=self._exec, args=([self.adb_exe, "sideload", f], "Sideload")).start()

    def wipe_data(self): threading.Thread(target=self._exec, args=([self.fastboot_exe, "-w"], "Wipe")).start()
    def erase_part(self, part): threading.Thread(target=self._exec, args=([self.fastboot_exe, "erase", part], f"Erase {part}")).start()

    def reboot_device(self): 
        subprocess.run([self.fastboot_exe, "reboot"], creationflags=0x08000000 if os.name=="nt" else 0)
    def _reboot_bootloader(self): subprocess.run([self.fastboot_exe, "reboot-bootloader"], creationflags=0x08000000)
    def _reboot_recovery(self): subprocess.run([self.fastboot_exe, "reboot", "recovery"], creationflags=0x08000000)
    
    def odin_reset(self):
        for s in self.odin_vars: self.odin_vars[s]["check"].set(False); self.odin_vars[s]["path"].set("")
        self.pit_var.set(""); self.progress_var.set(0); self.percent_lbl.config(text="0%"); self.pass_label.config(text="READY", bg=BG_CARD)

if __name__ == "__main__":
    root = tk.Tk()
    app = EkoFlashV3(root)
    root.mainloop()
