// NBA 2K7 (2026) launcher: picks graphics settings for the PC's GPU, writes PCSX2 per-game
// settings for NBA 2K7, and starts the game fullscreen. It ships no game or BIOS files.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Windows.Forms;

static class Program {
    [STAThread]
    static void Main(string[] args) {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        if (args.Length > 0 && args[0] == "--write-settings-only") {
            // Used for testing: write settings for the given profile and print them, without starting the game.
            var cfg = Config.Load();
            var gpu = Gpu.Detect();
            string profile = args.Length > 1 ? args[1] : cfg.Graphics;
            string path = Settings.Write(cfg, Settings.Resolve(profile, gpu));
            Console.WriteLine("GPU: " + gpu.Name + " (" + gpu.Tier + ")");
            Console.WriteLine("Wrote: " + path);
            Console.WriteLine(File.ReadAllText(path));
            return;
        }
        Application.Run(new MainForm());
    }
}

class Config {
    public string Pcsx2Exe = "", IsoPath = "", Card = "", Graphics = "Auto", Controls = "2K16";
    public bool CaptureTextures; // dump game textures so logos can be found and replaced
    public bool Parsec;          // also open Parsec so a friend can join as Player 2
    static string FilePath { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NBA2K7-Launcher.ini"); } }

    public static Config Load() {
        var c = new Config();
        if (File.Exists(FilePath))
            foreach (var line in File.ReadAllLines(FilePath)) {
                int eq = line.IndexOf('='); if (eq < 0) continue;
                string k = line.Substring(0, eq).Trim(), v = line.Substring(eq + 1).Trim();
                if (k == "Pcsx2Exe") c.Pcsx2Exe = v; else if (k == "IsoPath") c.IsoPath = v;
                else if (k == "Card") c.Card = v; else if (k == "Graphics") c.Graphics = v;
                else if (k == "Controls") c.Controls = v;
                else if (k == "CaptureTextures") c.CaptureTextures = v.Equals("true", StringComparison.OrdinalIgnoreCase);
                else if (k == "Parsec") c.Parsec = v.Equals("true", StringComparison.OrdinalIgnoreCase);
            }
        if (!File.Exists(c.Pcsx2Exe)) c.Pcsx2Exe = FindPcsx2();
        if (!File.Exists(c.IsoPath)) c.IsoPath = FindIso();
        return c;
    }
    public void Save() {
        File.WriteAllText(FilePath, "Pcsx2Exe=" + Pcsx2Exe + "\r\nIsoPath=" + IsoPath + "\r\nCard=" + Card + "\r\nGraphics=" + Graphics + "\r\nControls=" + Controls + "\r\nCaptureTextures=" + (CaptureTextures ? "true" : "false") + "\r\nParsec=" + (Parsec ? "true" : "false") + "\r\n");
    }
    static string FindPcsx2() {
        string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string[] candidates = {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "engine", "pcsx2-qt.exe"), // self-contained copy next to the launcher
            Path.Combine(docs, "PCSX2", "pcsx2-qt.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PCSX2", "pcsx2-qt.exe"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PCSX2", "pcsx2-qt.exe"),
        };
        foreach (var p in candidates) if (File.Exists(p)) return p;
        return "";
    }
    static string FindIso() {
        try {
            foreach (var f in Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.iso"))
                if (Path.GetFileName(f).IndexOf("2K7", StringComparison.OrdinalIgnoreCase) >= 0) return f;
        } catch (Exception) { }
        return "";
    }
    // PCSX2 keeps its data next to the exe in portable mode, otherwise in Documents\PCSX2.
    public string DataDir {
        get {
            string exeDir = Path.GetDirectoryName(Pcsx2Exe) ?? "";
            if (File.Exists(Path.Combine(exeDir, "portable.ini")) || File.Exists(Path.Combine(exeDir, "portable.txt"))) return exeDir;
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PCSX2");
        }
    }
    public string MemcardDir { get { return Path.Combine(DataDir, "memcards"); } }
}

class Gpu {
    public string Name = "Unknown graphics"; public string Tier = "Low"; public long VramMB;
    static readonly string[] Virtual = { "virtual", "parsec", "basic render", "basic display", "remote", "vnc", "citrix", "meta", "idd" };

    public static Gpu Detect() {
        var best = new Gpu(); int bestScore = -1;
        try {
            using (var s = new ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController")) {
                foreach (ManagementObject o in s.Get()) {
                    string name = (o["Name"] ?? "").ToString();
                    string lower = name.ToLowerInvariant();
                    if (Virtual.Any(v => lower.Contains(v))) continue;
                    long ram = 0; try { ram = Convert.ToInt64(o["AdapterRAM"]) / (1024 * 1024); } catch (Exception) { }
                    string tier = TierOf(lower, ram);
                    int score = tier == "High" ? 2 : tier == "Medium" ? 1 : 0;
                    if (score > bestScore) { bestScore = score; best = new Gpu { Name = name, Tier = tier, VramMB = ram }; }
                }
            }
        } catch (Exception) { }
        return best;
    }
    static string TierOf(string n, long ramMB) {
        if (n.Contains("rtx") || n.Contains("arc ") || n.Contains("radeon rx 6") || n.Contains("radeon rx 7") || n.Contains("radeon rx 9")) return "High";
        if (n.Contains("gtx") || n.Contains("radeon rx") || n.Contains("radeon pro") || n.Contains("iris xe") || n.Contains("radeon(tm) 7") || n.Contains("radeon 7")) return "Medium";
        if (n.Contains("intel") || n.Contains("mx1") || n.Contains("mx2") || n.Contains("vega 3") || n.Contains("vega 6") || n.Contains("vega 8")) return "Low";
        if (ramMB >= 4000) return "Medium";
        return "Low";
    }
}

class Profile {
    public string Name; public int Upscale, Blending, Anisotropy, TriFilter, Preload;
    public bool Fxaa; public int Sharpness; // CAS sharpening 0-100 (0 = off)
    public override string ToString() {
        return Name + ": " + (Upscale == 1 ? "native res" : Upscale + "x res") + ", sharpened" + (Fxaa ? ", smoothed" : "") +
            ", " + Anisotropy + "x textures, " + (Blending == 0 ? "light effects" : Blending == 1 ? "standard effects" : "full effects");
    }
}

static class Settings {
    public const string GameId = "SLUS-21424_F5AEDCC3"; // NBA 2K7 (USA) v1.02, as PCSX2 names it
    public const string ButtonsProfile = "NBA 2K16 Style";      // sprint RT, post LT, icon pass RB, pick LB; right stick = shot stick
    public const string ProStickProfile = "NBA 2K16 Pro Stick"; // same buttons, plus dribble moves on the right stick

    // 2K7 puts Aggressive (sprint) on R1, Icon Pass on R2, Post on L1 and Call for Pick on L2,
    // so the 2K16 layout is the standard bindings with each bumper swapped for its trigger.
    static readonly string[] ButtonBindings = {
        "Up=DPadUp", "Right=DPadRight", "Down=DPadDown", "Left=DPadLeft",
        "Triangle=FaceNorth", "Circle=FaceEast", "Cross=FaceSouth", "Square=FaceWest",
        "Select=Back", "Start=Start",
        "L1=+LeftTrigger", "L2=LeftShoulder", "R1=+RightTrigger", "R2=RightShoulder",
        "LUp=-LeftY", "LRight=+LeftX", "LDown=+LeftY", "LLeft=-LeftX",
        "Analog=Guide", "LargeMotor=LargeMotor", "SmallMotor=SmallMotor" };

    // 2K7 Isomotion = hold R1 and move the left stick (add L1 for hesitations and spins). In Pro Stick mode the
    // right stick holds R1 and drives the left stick, so right-stick flicks and circles become dribble moves.
    // Free throws need a right-stick pull, so right-stick click pulls it; left-stick click is the strip (steal).
    static readonly string[] ProStickExtras = {
        "R1=+RightX", "R1=-RightX", "R1=+RightY", "R1=-RightY",
        "LUp=-RightY", "LRight=+RightX", "LDown=+RightY", "LLeft=-RightX",
        "RDown=RightStick", "RUp=LeftStick" };
    static readonly string[] ShotStickBindings = {
        "L3=LeftStick", "R3=RightStick", "RUp=-RightY", "RRight=+RightX", "RDown=+RightY", "RLeft=-RightX" };

    public static string ProfileFor(string controls) {
        return controls == "ProStick" ? ProStickProfile : controls == "Classic" ? null : ButtonsProfile;
    }

    // PCSX2 reads repeated keys as several bindings for the same PS2 input.
    public static void EnsureControlsProfile(Config cfg, string name) {
        string dir = Path.Combine(cfg.DataDir, "inputprofiles");
        string path = Path.Combine(dir, name + ".ini");
        if (File.Exists(path) && File.ReadAllText(path).Contains("; launcher v4")) return;
        Directory.CreateDirectory(dir);
        var b = new StringBuilder();
        b.AppendLine("; launcher v4 - " + name);
        b.AppendLine("[InputSources]");
        b.AppendLine("Keyboard = true");
        b.AppendLine("Mouse = true");
        b.AppendLine("SDL = true");
        b.AppendLine("DInput = false");
        b.AppendLine("XInput = false");
        b.AppendLine("SDLControllerEnhancedMode = true");
        b.AppendLine();
        b.AppendLine("[Pad]");
        b.AppendLine("MultitapPort1 = false");
        b.AppendLine("MultitapPort2 = false");
        b.AppendLine();
        b.AppendLine("[Pad1]");
        b.AppendLine("Type = DualShock2");
        // Pro Stick: the right stick presses R1 at 8% but only moves the stick past 25%, so R1 is already
        // held when the stick motion starts - 2K7 needs that order to read the motion as a dribble move.
        b.AppendLine("Deadzone = " + (name == ProStickProfile ? "0.25" : "0"));
        b.AppendLine("AxisScale = 1.33");
        b.AppendLine("ButtonDeadzone = " + (name == ProStickProfile ? "0.08" : "0"));
        b.AppendLine("PressureModifier = 0.5");
        var all = ButtonBindings.Concat(name == ProStickProfile ? ProStickExtras : ShotStickBindings).ToList();
        foreach (var kv in all) { var p = kv.Split('='); b.AppendLine(p[0] + " = SDL-0/" + p[1]); }
        // Player 2 = the second controller (a friend's pad over Parsec shows up here), same layout
        b.AppendLine();
        b.AppendLine("[Pad2]");
        b.AppendLine("Type = DualShock2");
        b.AppendLine("Deadzone = " + (name == ProStickProfile ? "0.25" : "0"));
        b.AppendLine("AxisScale = 1.33");
        b.AppendLine("ButtonDeadzone = " + (name == ProStickProfile ? "0.08" : "0"));
        b.AppendLine("PressureModifier = 0.5");
        foreach (var kv in all) { var p = kv.Split('='); b.AppendLine(p[0] + " = SDL-1/" + p[1]); }
        File.WriteAllText(path, b.ToString());
    }

    // Performance suits integrated graphics; Balanced suits mid-range cards; Quality suits strong cards.
    // All levels sharpen (CAS), smooth edges (FXAA) and lift colour a little; higher levels add resolution.
    public static readonly Profile Performance = new Profile { Name = "Performance", Upscale = 1, Blending = 0, Anisotropy = 4, TriFilter = 0, Preload = 2, Fxaa = true, Sharpness = 50 };
    public static readonly Profile Balanced = new Profile { Name = "Balanced", Upscale = 2, Blending = 1, Anisotropy = 8, TriFilter = 0, Preload = 2, Fxaa = true, Sharpness = 40 };
    public static readonly Profile Quality = new Profile { Name = "Quality", Upscale = 3, Blending = 2, Anisotropy = 16, TriFilter = 0, Preload = 2, Fxaa = false, Sharpness = 35 };

    public static Profile Resolve(string choice, Gpu gpu) {
        if (choice == "Performance") return Performance;
        if (choice == "Balanced") return Balanced;
        if (choice == "Quality") return Quality;
        return gpu.Tier == "High" ? Quality : gpu.Tier == "Medium" ? Balanced : Performance;
    }

    public static string Write(Config cfg, Profile p) {
        string dir = Path.Combine(cfg.DataDir, "gamesettings");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, GameId + ".ini");
        var sb = new StringBuilder();
        sb.AppendLine("; Written by the NBA 2K7 (2026) launcher - profile " + p.Name);
        string profile = ProfileFor(cfg.Controls);
        if (profile != null) {
            EnsureControlsProfile(cfg, profile);
            sb.AppendLine("[EmuCore]");
            sb.AppendLine("InputProfileName = " + profile);
            sb.AppendLine();
        }
        sb.AppendLine("[EmuCore/GS]");
        sb.AppendLine("Renderer = 3"); // Direct3D 11: Vulkan crashes on some integrated GPUs and old drivers
        sb.AppendLine("UseBlitSwapChain = true");         // flip swap chains show up black in Parsec's screen capture
        sb.AppendLine("ExclusiveFullscreenControl = 0");  // never exclusive fullscreen, same reason
        sb.AppendLine("upscale_multiplier = " + p.Upscale);
        sb.AppendLine("accurate_blending_unit = " + p.Blending);
        sb.AppendLine("MaxAnisotropy = " + p.Anisotropy);
        sb.AppendLine("TriFilter = " + p.TriFilter);
        sb.AppendLine("texture_preloading = " + p.Preload);
        sb.AppendLine("hw_mipmap = true");
        sb.AppendLine("dithering_ps2 = 1");                       // scaled dithering: less PS2 grain
        sb.AppendLine("fxaa = " + (p.Fxaa ? "true" : "false"));
        sb.AppendLine("CASMode = " + (p.Sharpness > 0 ? 1 : 0));  // 1 = sharpen only
        sb.AppendLine("CASSharpness = " + p.Sharpness);
        sb.AppendLine("ShadeBoost = true");
        sb.AppendLine("ShadeBoost_Brightness = 50");
        sb.AppendLine("ShadeBoost_Contrast = 54");
        sb.AppendLine("ShadeBoost_Saturation = 57");
        sb.AppendLine("LoadTextureReplacements = true"); // 2026 logos from textures\SLUS-21424\replacements
        sb.AppendLine("DumpReplaceableTextures = " + (cfg.CaptureTextures ? "true" : "false"));
        sb.AppendLine("DumpReplaceableMipmaps = false");
        if (!string.IsNullOrEmpty(cfg.Card)) {
            sb.AppendLine();
            sb.AppendLine("[MemoryCards]");
            sb.AppendLine("Slot1_Enable = true");
            sb.AppendLine("Slot1_Filename = " + cfg.Card);
        }
        File.WriteAllText(path, sb.ToString());
        return path;
    }
}

class MainForm : Form {
    Config cfg = Config.Load();
    Gpu gpu = Gpu.Detect();
    ComboBox cardBox = new ComboBox(), gfxBox = new ComboBox(), ctlBox = new ComboBox();
    Button playOnline = new Button();
    static string ParsecExe { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Parsec", "parsecd.exe"); } }
    Label gpuLabel = new Label(), gfxInfo = new Label(), status = new Label();
    Button play = new Button();

    static readonly Color Bg = Color.FromArgb(22, 27, 36), Panel = Color.FromArgb(32, 39, 51), Fg = Color.FromArgb(230, 235, 242),
        Muted = Color.FromArgb(150, 160, 178), Accent = Color.FromArgb(217, 97, 28);

    public MainForm() {
        Text = "NBA 2K7 (2026)"; FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size(460, 400); BackColor = Bg; ForeColor = Fg;
        Font = new Font("Segoe UI", 10f);
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }

        var title = new Label { Text = "NBA 2K7", Font = new Font("Segoe UI", 24f, FontStyle.Bold), AutoSize = true, Location = new Point(22, 14), ForeColor = Fg };
        var sub = new Label { Text = "2026-27 ROSTERS", Font = new Font("Segoe UI", 9f, FontStyle.Bold), AutoSize = true, Location = new Point(26, 58), ForeColor = Accent };
        Controls.Add(title); Controls.Add(sub);

        AddLabel("Roster", 92);
        cardBox.DropDownStyle = ComboBoxStyle.DropDownList; cardBox.Location = new Point(130, 89); cardBox.Width = 306; Style(cardBox);
        Controls.Add(cardBox);

        AddLabel("Graphics", 128);
        gfxBox.DropDownStyle = ComboBoxStyle.DropDownList; gfxBox.Location = new Point(130, 125); gfxBox.Width = 306; Style(gfxBox);
        gfxBox.Items.AddRange(new object[] { "Auto", "Performance", "Balanced", "Quality" });
        gfxBox.SelectedItem = gfxBox.Items.Contains(cfg.Graphics) ? cfg.Graphics : "Auto";
        gfxBox.SelectedIndexChanged += (s, e) => UpdateGfxInfo();
        Controls.Add(gfxBox);

        gfxInfo.Location = new Point(130, 156); gfxInfo.Size = new Size(306, 40); gfxInfo.ForeColor = Muted; gfxInfo.Font = new Font("Segoe UI", 9f);
        gpuLabel.Location = new Point(130, 196); gpuLabel.Size = new Size(306, 20); gpuLabel.ForeColor = Muted; gpuLabel.Font = new Font("Segoe UI", 9f);
        gpuLabel.Text = "Detected: " + gpu.Name;
        Controls.Add(gfxInfo); Controls.Add(gpuLabel);

        AddLabel("Controls", 232);
        ctlBox.DropDownStyle = ComboBoxStyle.DropDownList; ctlBox.Location = new Point(130, 229); ctlBox.Width = 306; Style(ctlBox);
        ctlBox.Items.AddRange(new object[] { "2K16 style (right stick = shot stick)", "2K16 Pro Stick (right stick = dribble moves)", "Classic 2K7" });
        ctlBox.SelectedIndex = cfg.Controls == "ProStick" ? 1 : cfg.Controls == "Classic" ? 2 : 0;
        Controls.Add(ctlBox);
        var ctlInfo = new Label { Location = new Point(130, 260), Size = new Size(306, 20), ForeColor = Muted, Font = new Font("Segoe UI", 9f) };
        EventHandler showInfo = (s, e) => {
            ctlInfo.Text = ctlBox.SelectedIndex == 1 ? "Flick/rotate right stick to dribble · LT for spins · X shoots"
                : ctlBox.SelectedIndex == 2 ? "Original PS2 layout" : "A pass · X shoot · RT sprint · LT post · RB icon pass";
        };
        ctlBox.SelectedIndexChanged += showInfo; showInfo(null, null);
        Controls.Add(ctlInfo);

        play.Text = "PLAY"; play.Font = new Font("Segoe UI", 14f, FontStyle.Bold); play.Location = new Point(22, 292); play.Size = new Size(202, 52);
        play.FlatStyle = FlatStyle.Flat; play.FlatAppearance.BorderSize = 0; play.BackColor = Accent; play.ForeColor = Color.White; play.Cursor = Cursors.Hand;
        play.Click += (s, e) => Play(false);
        Controls.Add(play); AcceptButton = play;

        // Second option: same game, plus Parsec so a friend can join as Player 2
        playOnline.Text = "PLAY ONLINE"; playOnline.Font = new Font("Segoe UI", 14f, FontStyle.Bold); playOnline.Location = new Point(234, 292); playOnline.Size = new Size(202, 52);
        playOnline.FlatStyle = FlatStyle.Flat; playOnline.FlatAppearance.BorderSize = 2; playOnline.FlatAppearance.BorderColor = Accent;
        playOnline.BackColor = Panel; playOnline.ForeColor = Color.White; playOnline.Cursor = Cursors.Hand;
        playOnline.Enabled = File.Exists(ParsecExe);
        playOnline.Click += (s, e) => OpenParsec();
        Controls.Add(playOnline);

        status.Location = new Point(22, 354); status.Size = new Size(414, 40); status.ForeColor = Muted; status.Font = new Font("Segoe UI", 9f);
        Controls.Add(status);

        LoadCards(); UpdateGfxInfo(); CheckReady();
    }

    void AddLabel(string text, int y) { Controls.Add(new Label { Text = text, Location = new Point(24, y), AutoSize = true, ForeColor = Muted }); }
    static void Style(ComboBox c) { c.FlatStyle = FlatStyle.Flat; c.BackColor = Panel; c.ForeColor = Fg; }

    void LoadCards() {
        cardBox.Items.Clear();
        var names = new List<string>();
        try { names = Directory.GetFiles(cfg.MemcardDir, "NBA2K7-2026*.ps2").Select(Path.GetFileName).OrderBy(n => n).ToList(); } catch (Exception) { }
        foreach (var n in names) cardBox.Items.Add(new CardItem(n));
        if (cardBox.Items.Count == 0) { cardBox.Items.Add(new CardItem("")); }
        string want = !string.IsNullOrEmpty(cfg.Card) ? cfg.Card : "NBA2K7-2026-v3-safe.ps2";
        foreach (CardItem it in cardBox.Items) if (it.File == want) cardBox.SelectedItem = it;
        if (cardBox.SelectedIndex < 0) cardBox.SelectedIndex = 0;
    }

    class CardItem {
        public string File;
        public CardItem(string f) { File = f; }
        public override string ToString() {
            if (File == "") return "No 2026 roster card found";
            if (File.EndsWith("-v6-safe.ps2")) return "2026 rosters + Spiderman + Curry shot (latest)";
            if (File.EndsWith("-v5-safe.ps2")) return "2026 rosters + Spiderman (older)";
            if (File.EndsWith("-v4-safe.ps2")) return "2026 rosters, LeBron fix (older)";
            if (File.EndsWith("-v3-safe.ps2")) return "2026 rosters, original roster sizes (older)";
            if (File.EndsWith("-v3.ps2")) return "2026 rosters, 15-man teams";
            return File;
        }
    }

    void UpdateGfxInfo() {
        var p = Settings.Resolve((string)gfxBox.SelectedItem, gpu);
        gfxInfo.Text = ((string)gfxBox.SelectedItem == "Auto" ? "Auto picks " : "") + p.ToString();
    }

    bool CheckReady() {
        if (!File.Exists(cfg.Pcsx2Exe)) { status.Text = "PCSX2 not found. Install it from pcsx2.net, or press Play to find pcsx2-qt.exe."; return false; }
        if (!File.Exists(cfg.IsoPath)) { status.Text = "Game disc image not found. Press Play to pick your NBA 2K7 .iso file."; return false; }
        status.Text = "Ready. Load the roster save named 2012 in the game. PLAY ONLINE also opens Parsec so a friend can join as Player 2.";
        return true;
    }

    // PLAY ONLINE: go straight to Parsec (starting it again brings an open Parsec to the front).
    // The launcher stays open so PLAY can start the game once the friend has joined.
    void OpenParsec() {
        try {
            Process.Start(new ProcessStartInfo(ParsecExe) { UseShellExecute = true });
            status.Text = "Parsec is open. Invite your friend there, then press PLAY. They join as Player 2.";
        } catch (Exception ex) {
            MessageBox.Show(this, "Couldn't open Parsec:\n" + ex.Message, "NBA 2K7 (2026)", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    void Play(bool online) {
        if (!File.Exists(cfg.Pcsx2Exe)) {
            using (var d = new OpenFileDialog { Title = "Find pcsx2-qt.exe", Filter = "PCSX2|pcsx2-qt.exe;pcsx2*.exe" })
                if (d.ShowDialog(this) == DialogResult.OK) cfg.Pcsx2Exe = d.FileName; else return;
            LoadCards();
        }
        if (!File.Exists(cfg.IsoPath)) {
            using (var d = new OpenFileDialog { Title = "Find your NBA 2K7 disc image", Filter = "PS2 disc image|*.iso;*.bin;*.chd;*.cso" })
                if (d.ShowDialog(this) == DialogResult.OK) cfg.IsoPath = d.FileName; else return;
        }
        var card = cardBox.SelectedItem as CardItem;
        cfg.Card = card != null ? card.File : "";
        cfg.Graphics = (string)gfxBox.SelectedItem;
        cfg.Controls = ctlBox.SelectedIndex == 1 ? "ProStick" : ctlBox.SelectedIndex == 2 ? "Classic" : "2K16";
        cfg.Parsec = online;
        try {
            // Parsec streams the game to a friend; their controller arrives as the second pad (Player 2)
            if (cfg.Parsec && File.Exists(ParsecExe) && Process.GetProcessesByName("parsecd").Length == 0)
                Process.Start(new ProcessStartInfo(ParsecExe) { UseShellExecute = true });
            Settings.Write(cfg, Settings.Resolve(cfg.Graphics, gpu));
            cfg.Save();
            var psi = new ProcessStartInfo(cfg.Pcsx2Exe, "-fullscreen -nogui -batch -- \"" + cfg.IsoPath + "\"") { WorkingDirectory = Path.GetDirectoryName(cfg.Pcsx2Exe), UseShellExecute = false };
            Process.Start(psi);
            Close();
        } catch (Exception ex) {
            MessageBox.Show(this, "Couldn't start the game:\n" + ex.Message, "NBA 2K7 (2026)", MessageBoxButtons.OK, MessageBoxIcon.Error);
            CheckReady();
        }
    }
}
