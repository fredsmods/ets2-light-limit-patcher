// SPDX-License-Identifier: MIT
// ETS2 Light Limit Patcher (GUI) - raises the light list capacity of eurotrucks2.exe v1.62.3.3
// Compiled with the .NET Framework 4 csc that ships with Windows (C# 5): see build.ps1
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("ETS2 Light Limit Patcher")]
[assembly: AssemblyProduct("ETS2 Light Limit Patcher")]
[assembly: AssemblyVersion("1.0.0.0")]

// ------------------------------------------------------------------------------------------
// Patch logic (port of ets2_light_patcher.py; verified byte-identical for 500/550/600/700)
// ------------------------------------------------------------------------------------------
public static class LightPatch
{
    public static string AssetDir;   // set by the PowerShell launcher so images can be loaded from files
    public const int ORIG = 480;
    public const long EXPECT_SIZE = 53747600;
    const long F_OLD_FRAME = 0x179A8, F_TOP = 0x178A8;
    const long H_OLD_FRAME = 0x17D0, H_TOP = 0x16D0;

    public class Site
    {
        public long Rva; public int Size; public string Kind; public long Orig;
        public Site(long rva, int size, string kind, long orig) { Rva = rva; Size = size; Kind = kind; Orig = orig; }
    }

    // (field RVA, size, kind, value in the ORIGINAL exe)
    static readonly Site[] TABLE = new Site[] {
        new Site(0x14cfad4, 4, "H_RBP", -5840L),
        new Site(0x14cfad9, 4, "H_ALLOC", 6096L),
        new Site(0x14cfaec, 4, "C4PTR", 1952L),
        new Site(0x14cfb0c, 4, "H_XMM", 6080L),
        new Site(0x14cfb22, 4, "H_XMM", 6064L),
        new Site(0x14cfb35, 4, "H_XMM", 6048L),
        new Site(0x14cfb4a, 4, "CNT", 480L),
        new Site(0x14cfb73, 4, "H_CALLER", 5920L),
        new Site(0x14cfb7a, 4, "H_CALLER", 5928L),
        new Site(0x14cfb91, 4, "H_CALLER", 5920L),
        new Site(0x14cfbd9, 4, "H_CALLER", 5928L),
        new Site(0x14cfd24, 4, "H_CALLER", 5920L),
        new Site(0x14cfd2a, 4, "H_CALLER", 5920L),
        new Site(0x14cfdcc, 4, "H_CALLER", 5920L),
        new Site(0x14cfdd7, 4, "H_CALLER", 5920L),
        new Site(0x14cffbd, 4, "H_EPI", 6096L),
        new Site(0x14d107e, 4, "F_RBP", -96424L),
        new Site(0x14d1083, 4, "F_ALLOC", 96680L),
        new Site(0x14d1092, 4, "F_CALLER", 96512L),
        new Site(0x14d10ea, 4, "F_CALLER", 96496L),
        new Site(0x14d10fd, 4, "F_ALLOC", 96680L),
        new Site(0x14d1193, 4, "F_CALLER", 96512L),
        new Site(0x14d119a, 4, "CNT", 480L),
        new Site(0x14d11a6, 4, "CNT", 480L),
        new Site(0x14d11c6, 4, "CNT", 480L),
        new Site(0x14d11d6, 4, "LIGHTB", 59520L),
        new Site(0x14d124d, 4, "F_CALLER", 96504L),
        new Site(0x14d125a, 4, "NEGCNT", -481L),
        new Site(0x14d1267, 4, "CNT", 480L),
        new Site(0x14d12e9, 4, "DATAB", 92160L),
        new Site(0x14d13a1, 4, "F_CALLER", 96512L),
        new Site(0x14d13a8, 4, "F_CALLER", 96488L),
        new Site(0x14d13b3, 4, "CNT", 480L),
        new Site(0x14d13e7, 4, "F_CALLER", 96504L),
        new Site(0x14d15ac, 4, "F_CALLER", 96512L),
        new Site(0x14d15c0, 4, "CNT", 480L),
        new Site(0x14d15c7, 4, "C1FILL", 4160L),
        new Site(0x14d160d, 4, "C1BASE", 4128L),
        new Site(0x14d161e, 4, "CNT", 480L),
        new Site(0x14d1714, 4, "F_CALLER", 96496L),
        new Site(0x14d176b, 4, "F_CALLER", 96496L),
        new Site(0x14d17b6, 4, "C2", 2208L),
        new Site(0x14d17bd, 4, "C2", 2192L),
        new Site(0x14d17cf, 4, "C2", 2184L),
        new Site(0x14d17db, 4, "C2", 2176L),
        new Site(0x14d17e2, 4, "C2", 2176L),
        new Site(0x14d17e9, 4, "C2", 2200L),
        new Site(0x14d17ed, 4, "CNT", 480L),
        new Site(0x14d1805, 4, "F_CALLER", 96488L),
        new Site(0x14d1820, 4, "C2", 2176L),
        new Site(0x14d1841, 4, "C3PTR", 256L),
        new Site(0x14d186e, 4, "CNT", 480L),
        new Site(0x14d187d, 4, "CNT", 480L),
        new Site(0x14d18b3, 4, "C2", 2192L),
        new Site(0x14d18c0, 4, "C2", 2184L),
        new Site(0x14d19cd, 4, "F_CALLER", 96512L),
        new Site(0x14d1a0f, 4, "F_CALLER", 96512L),
        new Site(0x14d1a16, 4, "F_CALLER", 96480L),
        new Site(0x14d1aec, 4, "F_CALLER", 96472L),
        new Site(0x14d1b75, 4, "F_CALLER", 96464L),
        new Site(0x14d1b8a, 4, "C2", 2192L),
        new Site(0x14d1ba1, 4, "C2", 2176L),
        new Site(0x14d1bad, 4, "C2", 2184L),
        new Site(0x14d1e4b, 4, "F_ALLOC", 96680L),
        new Site(0x14d1f97, 4, "C2", 2176L),
        new Site(0x26a1322, 2, "U_XMM_H", 378L),
        new Site(0x26a1326, 2, "U_XMM_H", 379L),
        new Site(0x26a132a, 2, "U_XMM_H", 380L),
        new Site(0x26a132e, 2, "U_NV_H", 770L),
        new Site(0x26a1332, 2, "U_ALLOC_H", 762L),
        new Site(0x26a13b6, 2, "U_ALLOC_F", 12085L),
    };

    static void Layout(int n, out long holeF, out long holeH, out long c1, out long c3, out long c2h)
    {
        if (n <= 500)
        {   // C1 grows downward into the vacated old C2/C3 area
            holeF = 0x1000;
            c3 = F_TOP + 8;
            c2h = (c3 + n * 4L + 15) & ~15L;
            c1 = 0x1020 - (n - ORIG) * 0xC0L;
        }
        else
        {   // C1 lives completely in the new hole
            c1 = F_TOP + 8;
            holeF = (c1 + n * 0xC0L - F_TOP + 15) & ~15L;
            c3 = 0x1040;
            c2h = (c3 + n * 4L + 15) & ~15L;
        }
        holeH = (0x10 + n * 8L + 0xFF) & ~0xFFL;
    }

    static long Value(Site s, int n)
    {
        if (n == ORIG) return s.Orig;
        long hf, hh, c1, c3, c2h;
        Layout(n, out hf, out hh, out c1, out c3, out c2h);
        switch (s.Kind)
        {
            case "F_RBP": return -(F_TOP + hf);
            case "F_ALLOC": return F_OLD_FRAME + hf;
            case "H_RBP": return -(H_TOP + hh);
            case "H_ALLOC": return H_OLD_FRAME + hh;
            case "F_CALLER": return s.Orig + hf;
            case "H_CALLER": return s.Orig + hh;
            case "C2": return c2h + (s.Orig - 0x880);
            case "C3PTR": return c3;
            case "C1BASE": return c1;
            case "C1FILL": return c1 + 0x20;
            case "C4PTR": return H_TOP + 0x10;
            case "CNT": return n;
            case "NEGCNT": return -(n + 1L);
            case "LIGHTB": return n * 0x7CL;
            case "DATAB": return n * 0xC0L;
            case "H_EPI": return H_OLD_FRAME + hh;
            case "H_XMM": return s.Orig + hh;
            case "U_ALLOC_F": return (F_OLD_FRAME + hf) / 8;
            case "U_ALLOC_H": return (H_OLD_FRAME + hh) / 8;
            case "U_NV_H": return s.Orig + hh / 8;
            case "U_XMM_H": return s.Orig + hh / 16;
        }
        throw new InvalidOperationException("unknown site kind " + s.Kind);
    }

    static List<long[]> Sections(byte[] d)
    {
        int pe = BitConverter.ToInt32(d, 0x3C);
        int nsec = BitConverter.ToUInt16(d, pe + 6);
        int opt = BitConverter.ToUInt16(d, pe + 20);
        List<long[]> r = new List<long[]>();
        for (int i = 0; i < nsec; i++)
        {
            int o = pe + 24 + opt + 40 * i;
            long vsz = BitConverter.ToUInt32(d, o + 8), va = BitConverter.ToUInt32(d, o + 12);
            long rsz = BitConverter.ToUInt32(d, o + 16), raw = BitConverter.ToUInt32(d, o + 20);
            r.Add(new long[] { va, Math.Max(vsz, rsz), raw });
        }
        return r;
    }

    static int Off(List<long[]> secs, long rva)
    {
        foreach (long[] s in secs)
            if (rva >= s[0] && rva < s[0] + s[1]) return (int)(s[2] + rva - s[0]);
        throw new InvalidDataException("RVA outside of all sections");
    }

    static long Read(byte[] d, int off, int size)
    {
        return size == 4 ? (long)BitConverter.ToInt32(d, off) : (long)BitConverter.ToUInt16(d, off);
    }

    static void Write(byte[] d, int off, int size, long v)
    {
        byte[] b = size == 4 ? BitConverter.GetBytes(unchecked((int)v)) : BitConverter.GetBytes(unchecked((ushort)v));
        Buffer.BlockCopy(b, 0, d, off, size);
    }

    // state: "original" / "patched" / "unknown"
    public static string Detect(byte[] d, out int limit)
    {
        limit = 0;
        try
        {
            if (d.Length < 0x1000 || d[0] != 'M' || d[1] != 'Z') return "unknown";
            List<long[]> secs = Sections(d);
            int cur = 0;
            foreach (Site s in TABLE)
                if (s.Kind == "CNT") { cur = (int)Read(d, Off(secs, s.Rva), s.Size); break; }
            if (cur < ORIG || cur > 2000) return "unknown";
            foreach (Site s in TABLE)
                if (Read(d, Off(secs, s.Rva), s.Size) != Value(s, cur)) return "unknown";
            limit = cur;
            return cur == ORIG ? "original" : "patched";
        }
        catch { return "unknown"; }
    }

    // returns the patched image (from original or any patched state)
    public static byte[] Build(byte[] d, int n)
    {
        int cur; string st = Detect(d, out cur);
        if (st == "unknown") throw new InvalidDataException("unsupported");
        byte[] o = (byte[])d.Clone();
        List<long[]> secs = Sections(o);
        foreach (Site s in TABLE) Write(o, Off(secs, s.Rva), s.Size, s.Orig);   // back to original
        foreach (Site s in TABLE) Write(o, Off(secs, s.Rva), s.Size, Value(s, n));
        int chk; string st2 = Detect(o, out chk);
        if (chk != n) throw new InvalidOperationException("self-check failed");
        return o;
    }

    public static bool GameRunning()
    {
        try { return Process.GetProcessesByName("eurotrucks2").Length > 0; } catch { return false; }
    }

    public static string FindExe()
    {
        string rel = @"Euro Truck Simulator 2\bin\win_x64\eurotrucks2.exe";
        List<string> roots = new List<string>();
        try
        {
            string sp = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
            if (!string.IsNullOrEmpty(sp))
            {
                sp = sp.Replace('/', '\\');
                roots.Add(Path.Combine(sp, @"steamapps\common"));
                string vdf = Path.Combine(sp, @"steamapps\libraryfolders.vdf");
                if (File.Exists(vdf))
                    foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                        roots.Add(Path.Combine(m.Groups[1].Value.Replace("\\\\", "\\"), @"steamapps\common"));
            }
        }
        catch { }
        string[] sub = { @"Steam\steamapps\common", @"SteamLibrary\steamapps\common",
                         @"Program Files (x86)\Steam\steamapps\common", @"Games\SteamLibrary\steamapps\common", "" };
        try
        {
            foreach (DriveInfo di in DriveInfo.GetDrives())
                if (di.DriveType == DriveType.Fixed || di.DriveType == DriveType.Removable)
                    foreach (string s in sub) roots.Add(Path.Combine(di.RootDirectory.FullName, s));
        }
        catch { }
        foreach (string r in roots)
        {
            string p = Path.Combine(r, rel);
            if (File.Exists(p)) return p;
        }
        return null;
    }
}

// ------------------------------------------------------------------------------------------
// UI
// ------------------------------------------------------------------------------------------
class Card : Panel
{
    public Card()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
        BackColor = Color.Transparent;
    }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (Parent == null) { base.OnPaintBackground(e); return; }
        GraphicsState st = e.Graphics.Save();
        e.Graphics.TranslateTransform(-Left, -Top);
        PaintEventArgs pe = new PaintEventArgs(e.Graphics, new Rectangle(Left, Top, Width, Height));
        InvokePaintBackground(Parent, pe);
        InvokePaint(Parent, pe);
        e.Graphics.Restore(st);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (GraphicsPath p = Round(r, 16))
        using (SolidBrush b = new SolidBrush(Color.FromArgb(196, 9, 11, 17)))
        using (Pen pen = new Pen(Color.FromArgb(110, 255, 184, 60), 1.4f))
        { g.FillPath(b, p); g.DrawPath(pen, p); }
    }
    static GraphicsPath Round(Rectangle r, int rad)
    {
        GraphicsPath p = new GraphicsPath(); int d = rad * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure(); return p;
    }
}

class MainForm : Form
{
    static readonly Color Amber = Color.FromArgb(255, 184, 48);
    static readonly Color AmberHot = Color.FromArgb(255, 205, 100);
    static readonly Color Ink = Color.FromArgb(24, 26, 34);

    Image bg, bulb;
    Card card;
    TextBox txtPath;
    Button btnBrowse, btnPatch, btnRestore;
    NumericUpDown numLimit;
    Label lblState, lblResult;
    System.Windows.Forms.Timer debounce;
    string currentState = "unknown"; int currentLimit = 0;

    static Image Res(string name)
    {
        using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
        { if (s != null) return new Bitmap(s); }
        try
        {
            if (LightPatch.AssetDir != null)
            {
                string f = Path.Combine(LightPatch.AssetDir, name);
                if (File.Exists(f)) return new Bitmap(new MemoryStream(File.ReadAllBytes(f)));
            }
        }
        catch { }
        return null;
    }

    Label MakeLabel(string text, int x, int y, int w, int h, float size, FontStyle st, Color c)
    {
        Label l = new Label();
        l.Text = text; l.SetBounds(x, y, w, h); l.BackColor = Color.Transparent; l.ForeColor = c;
        l.Font = new Font("Segoe UI", size, st, GraphicsUnit.Point); return l;
    }

    Button MakeButton(string text, int x, int y, int w, int h, bool primary)
    {
        Button b = new Button();
        b.Text = text; b.SetBounds(x, y, w, h); b.FlatStyle = FlatStyle.Flat; b.Cursor = Cursors.Hand;
        b.UseVisualStyleBackColor = false;
        if (primary)
        {
            b.BackColor = Amber; b.ForeColor = Ink; b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = AmberHot; b.FlatAppearance.MouseDownBackColor = Color.FromArgb(230, 150, 20);
            b.Font = new Font("Segoe UI Semibold", 15f, FontStyle.Bold, GraphicsUnit.Point);
        }
        else
        {
            b.BackColor = Color.FromArgb(34, 37, 48); b.ForeColor = Color.FromArgb(235, 235, 240);
            b.FlatAppearance.BorderColor = Color.FromArgb(120, 125, 140); b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(52, 56, 70);
            b.Font = new Font("Segoe UI", 10f, FontStyle.Regular, GraphicsUnit.Point);
        }
        return b;
    }

    public MainForm()
    {
        SuspendLayout();
        bg = Res("bg.jpg"); bulb = Res("bulb.png");
        Text = "ETS2 Light Limit Patcher";
        FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1000, 560);
        DoubleBuffered = true; BackColor = Color.Black;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        Controls.Add(MakeLabel("ETS2 Light Limit", 118, 30, 520, 52, 26f, FontStyle.Bold, Color.White));
        Controls.Add(MakeLabel("Raises the light limit of Euro Truck Simulator 2 above 480  \u00B7  Version 1.62.3.3",
                               121, 84, 600, 24, 10.5f, FontStyle.Regular, Color.FromArgb(225, 225, 232)));

        card = new Card(); card.SetBounds(36, 138, 460, 392);
        Controls.Add(card);

        card.Controls.Add(MakeLabel("ETS2 executable (eurotrucks2.exe)", 24, 18, 410, 22, 10f, FontStyle.Bold, Amber));
        txtPath = new TextBox();
        txtPath.SetBounds(24, 44, 312, 28); txtPath.BackColor = Color.FromArgb(28, 30, 40); txtPath.ForeColor = Color.White;
        txtPath.BorderStyle = BorderStyle.FixedSingle; txtPath.Font = new Font("Segoe UI", 10f);
        card.Controls.Add(txtPath);
        btnBrowse = MakeButton("Browse\u2026", 344, 42, 92, 32, false);
        btnBrowse.Font = new Font("Segoe UI", 9f);
        card.Controls.Add(btnBrowse);

        lblState = MakeLabel("", 24, 84, 412, 46, 10f, FontStyle.Regular, Color.FromArgb(200, 200, 210));
        card.Controls.Add(lblState);

        card.Controls.Add(MakeLabel("New light limit", 24, 140, 160, 22, 10f, FontStyle.Bold, Amber));
        numLimit = new NumericUpDown();
        numLimit.SetBounds(24, 166, 110, 30); numLimit.Minimum = 480; numLimit.Maximum = 1000; numLimit.Value = 600;
        numLimit.BackColor = Color.FromArgb(28, 30, 40); numLimit.ForeColor = Color.White; numLimit.Font = new Font("Segoe UI", 12f);
        numLimit.BorderStyle = BorderStyle.FixedSingle;
        card.Controls.Add(numLimit);
        card.Controls.Add(MakeLabel("480 = original \u00B7 confirmed: 500, 600 \u00B7 max 1000", 146, 170, 290, 24, 9.5f, FontStyle.Regular, Color.FromArgb(170, 170, 182)));

        btnPatch = MakeButton("Patch", 24, 216, 412, 58, true);
        card.Controls.Add(btnPatch);
        btnRestore = MakeButton("Restore original", 24, 284, 412, 36, false);
        card.Controls.Add(btnRestore);

        lblResult = MakeLabel("", 24, 330, 412, 50, 10f, FontStyle.Regular, Color.White);
        card.Controls.Add(lblResult);

        debounce = new System.Windows.Forms.Timer(); debounce.Interval = 350;
        debounce.Tick += delegate { debounce.Stop(); RefreshState(); };
        txtPath.TextChanged += delegate { debounce.Stop(); debounce.Start(); };
        btnBrowse.Click += OnBrowse; btnPatch.Click += OnPatch; btnRestore.Click += OnRestore;

        AutoScaleDimensions = new SizeF(96F, 96F); AutoScaleMode = AutoScaleMode.Dpi;
        ResumeLayout(false); PerformLayout();

        string found = LightPatch.FindExe();
        if (found != null) { txtPath.Text = found; debounce.Stop(); }
        RefreshState();
        Shown += delegate { ActiveControl = btnPatch.Enabled ? (Control)btnPatch : (Control)btnBrowse; txtPath.SelectionLength = 0; };
    }

    protected override void OnPaintBackground(PaintEventArgs e) { }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics; int W = ClientSize.Width, H = ClientSize.Height;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        if (bg != null)
        {   // truck should sit on the right of the card: show the left ~77% of the picture, slightly shifted down
            float sw = W * 1.30f, sh = sw * bg.Height / bg.Width;
            if (sh < H * 1.08f) { sh = H * 1.08f; sw = sh * bg.Width / bg.Height; }
            g.DrawImage(bg, new RectangleF(0, -(sh - H) * 0.50f, sw, sh));
        }
        else g.Clear(Color.Black);
        using (LinearGradientBrush lg = new LinearGradientBrush(new Rectangle(0, 0, W, H), Color.Black, Color.Black, 0f))
        {
            ColorBlend cb = new ColorBlend(3);
            cb.Colors = new Color[] { Color.FromArgb(185, 0, 0, 0), Color.FromArgb(70, 0, 0, 0), Color.FromArgb(0, 0, 0, 0) };
            cb.Positions = new float[] { 0f, 0.55f, 1f }; lg.InterpolationColors = cb;
            g.FillRectangle(lg, 0, 0, W, H);
        }
        using (LinearGradientBrush tg = new LinearGradientBrush(new Rectangle(0, 0, W, 130), Color.FromArgb(150, 0, 0, 0), Color.FromArgb(0, 0, 0, 0), 90f))
            g.FillRectangle(tg, 0, 0, W, 130);
        if (bulb != null) g.DrawImage(bulb, new Rectangle(34, 22, 78, 78));
    }

    void SetResult(string text, bool ok)
    {
        lblResult.ForeColor = ok ? Color.FromArgb(120, 230, 140) : Color.FromArgb(255, 120, 110);
        lblResult.Text = text;
    }

    string ExePath() { return txtPath.Text.Trim().Trim('"'); }
    string BakPath() { return ExePath() + ".bak"; }

    void RefreshState()
    {
        currentState = "unknown"; currentLimit = 0;
        string p = ExePath();
        if (p.Length == 0 || !File.Exists(p))
        {
            lblState.ForeColor = Color.FromArgb(200, 200, 210);
            lblState.Text = "Select the eurotrucks2.exe of your game\n(folder: \u2026\\Euro Truck Simulator 2\\bin\\win_x64).";
        }
        else
        {
            try
            {
                byte[] d = File.ReadAllBytes(p);
                currentState = LightPatch.Detect(d, out currentLimit);
                if (currentState == "original")
                { lblState.ForeColor = Color.FromArgb(235, 235, 240); lblState.Text = "Original, light limit 480.\nReady to patch."; }
                else if (currentState == "patched")
                { lblState.ForeColor = Color.FromArgb(120, 230, 140); lblState.Text = "Already patched: light limit " + currentLimit + ".\nYou can choose a different value."; }
                else
                { lblState.ForeColor = Color.FromArgb(255, 150, 120); lblState.Text = "Not supported: this exe is not ETS2 1.62.3.3\n(or was modified otherwise). Nothing will be patched."; }
            }
            catch (Exception ex)
            { lblState.ForeColor = Color.FromArgb(255, 150, 120); lblState.Text = "File not readable: " + ex.Message; }
        }
        btnPatch.Enabled = currentState != "unknown";
        btnRestore.Enabled = currentState != "unknown" && (File.Exists(p + ".bak") || currentState == "patched");
        btnPatch.BackColor = btnPatch.Enabled ? Amber : Color.FromArgb(110, 90, 40);
    }

    void OnBrowse(object s, EventArgs e)
    {
        using (OpenFileDialog dlg = new OpenFileDialog())
        {
            dlg.Title = "Select eurotrucks2.exe";
            dlg.Filter = "ETS2 (eurotrucks2.exe)|eurotrucks2.exe|Programs (*.exe)|*.exe";
            string cur = ExePath();
            try { if (File.Exists(cur)) dlg.InitialDirectory = Path.GetDirectoryName(cur); } catch { }
            if (dlg.ShowDialog(this) == DialogResult.OK) txtPath.Text = dlg.FileName;
        }
    }

    bool Precheck()
    {
        if (LightPatch.GameRunning())
        { SetResult("ETS2 is running. Please close the game first.", false); return false; }
        return true;
    }

    void OnPatch(object s, EventArgs e)
    {
        SetResult("", true);
        if (!Precheck()) return;
        string p = ExePath(); int n = (int)numLimit.Value;
        try
        {
            byte[] d = File.ReadAllBytes(p);
            int cur; string st = LightPatch.Detect(d, out cur);
            if (st == "unknown") { SetResult("This file is not supported.", false); return; }
            if (st == "patched" && cur == n) { SetResult("Already patched to limit " + n + " - nothing to do.", true); return; }
            string note = "";
            string bak = p + ".bak";
            if (File.Exists(bak)) note = "Backup exists (.bak, untouched).";
            else if (st == "original") { File.Copy(p, bak, false); note = "Backup created: eurotrucks2.exe.bak."; }
            else note = "No backup found (limit 480 restores the original exactly).";
            byte[] o = LightPatch.Build(d, n);
            string tmp = p + ".tmp";
            File.WriteAllBytes(tmp, o);
            if (File.Exists(p)) File.Delete(p);
            File.Move(tmp, p);
            RefreshState();
            SetResult(n == LightPatch.ORIG ? "Original restored (limit 480). " + note
                                            : "Done! Light limit " + cur + " \u2192 " + n + ". " + note, true);
        }
        catch (UnauthorizedAccessException)
        { SetResult("No write access. Run the patcher as administrator or check the file's read-only flag.", false); }
        catch (Exception ex) { SetResult("Error: " + ex.Message, false); }
    }

    void OnRestore(object s, EventArgs e)
    {
        SetResult("", true);
        if (!Precheck()) return;
        string p = ExePath(); string bak = p + ".bak";
        try
        {
            if (File.Exists(bak))
            {
                byte[] b = File.ReadAllBytes(bak); int l; string st = LightPatch.Detect(b, out l);
                if (st == "unknown")
                { SetResult("The backup (.bak) is not the expected ETS2 version - not used. Use Patch with limit 480 instead.", false); return; }
                File.Copy(bak, p, true);
                RefreshState(); SetResult("Original restored from the backup.", true);
            }
            else
            {
                byte[] d = File.ReadAllBytes(p);
                byte[] o = LightPatch.Build(d, LightPatch.ORIG);
                File.WriteAllBytes(p, o);
                RefreshState(); SetResult("Original restored (limit 480).", true);
            }
        }
        catch (UnauthorizedAccessException)
        { SetResult("No write access. Run the patcher as administrator.", false); }
        catch (Exception ex) { SetResult("Error: " + ex.Message, false); }
    }

    public void SetPath(string p) { txtPath.Text = p; debounce.Stop(); RefreshState(); }

    public void Shot(string png)
    {
        Show(); Application.DoEvents();
        ActiveControl = btnPatch.Enabled ? (Control)btnPatch : (Control)btnBrowse; txtPath.SelectionLength = 0; Application.DoEvents();
        using (Bitmap bmp = new Bitmap(Width, Height))
        {
            DrawToBitmap(bmp, new Rectangle(0, 0, Width, Height));
            bmp.Save(png, ImageFormat.Png);
        }
    }
}

// Entry points for the PowerShell launcher (ETS2-Light-Patcher.ps1 compiles this file with Add-Type)
public static class Launcher
{
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

    static void Init(string assetDir)
    {
        LightPatch.AssetDir = assetDir;
        try { SetProcessDPIAware(); } catch { }
        try { Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); } catch { }
    }
    public static void Run(string assetDir) { Init(assetDir); Application.Run(new MainForm()); }
    public static void Shot(string assetDir, string png, string exe)
    {
        Init(assetDir);
        MainForm f = new MainForm();
        if (!string.IsNullOrEmpty(exe)) f.SetPath(exe);
        f.Shot(png);
    }
}

static class Program
{
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

    [STAThread]
    static int Main(string[] args)
    {
        // hidden test hooks (used by the build script to verify the exe against the Python reference)
        if (args.Length >= 3 && args[0] == "--cli")
        {
            try
            {
                byte[] d = File.ReadAllBytes(args[1]);
                byte[] o = LightPatch.Build(d, int.Parse(args[2]));
                File.WriteAllBytes(args[3], o); return 0;
            }
            catch (Exception ex) { File.WriteAllText(args[3] + ".err", ex.ToString()); return 1; }
        }
        if (args.Length >= 3 && args[0] == "--info")
        {
            byte[] d = File.ReadAllBytes(args[1]); int l; string st = LightPatch.Detect(d, out l);
            File.WriteAllText(args[2], st + " " + l); return 0;
        }
        try { SetProcessDPIAware(); } catch { }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        MainForm f = new MainForm();
        if (args.Length >= 2 && args[0] == "--shot")
        {
            if (args.Length >= 3) f.SetPath(args[2]);
            f.Shot(args[1]); return 0;
        }
        Application.Run(f);
        return 0;
    }
}
