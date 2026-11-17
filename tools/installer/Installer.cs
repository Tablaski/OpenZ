using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenZVK.Update
{
    sealed class Settings { public string directory, channel; public bool gitOffered; }
    public sealed class InstallerWindow : Form
    {
        readonly Color ground = Color.FromArgb(18, 22, 21), plate = Color.FromArgb(33, 39, 37), ink = Color.FromArgb(235, 231, 212), muted = Color.FromArgb(160, 166, 150), orange = Color.FromArgb(200, 90, 32);
        readonly ComboBox channel = new ComboBox(), versions = new ComboBox();
        readonly TextBox destination = new TextBox(), seed = new TextBox();
        readonly RichTextBox notes = new RichTextBox();
        readonly Label installed = new Label(), status = new Label(), transfer = new Label();
        readonly ProgressBar progress = new ProgressBar();
        readonly Button refresh, install, play, editor, browse, reuse;
        readonly GitHub github = new GitHub();
        readonly NotifyIcon notification = new NotifyIcon { Icon = SystemIcons.Information, Text = "OpenZ-VK updates" };
        string notifiedCommit;
        CancellationTokenSource operation = new CancellationTokenSource();
        Manifest selected;
        Func<Manifest, Chunk, CancellationToken, Task<byte[]>> localFetch;
        string localResourceIndex;
        bool loading, installing;
        readonly bool proofMode;
        readonly string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenZ-VK", "installer.json");
        public InstallerWindow(string[] arguments)
        {
            proofMode = arguments.Length > 0 && arguments[0].StartsWith("--proof", StringComparison.Ordinal);
            Text = "OpenZ-VK · Installer & Updates"; ClientSize = new Size(1000, 780); MinimumSize = MaximumSize = Size;
            StartPosition = FormStartPosition.CenterScreen; BackColor = ground; ForeColor = ink;
            Font = new Font("Segoe UI", 10); AutoScaleMode = AutoScaleMode.Dpi; MaximizeBox = false;
            var picture = new PictureBox { Left = 30, Top = 15, Width = 350, Height = 120, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };
            using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("OpenZVK.Logo"))
                if (resource != null) using (var image = Image.FromStream(resource)) picture.Image = new Bitmap(image);
            Controls.Add(picture);
            var sourceButton = ButtonAt("SOURCES & UNITY", 785, 110, 185, 34, false);
            sourceButton.Click += (s,e) => { using (var sources = new SourceWindow(null,null,localResourceIndex)) sources.ShowDialog(this); };
            var zipButton = ButtonAt("COMPLETE ZIPS", 580, 110, 185, 34, false);
            zipButton.Click += (s,e) => Process.Start("https://github.com/Tablaski/OpenZ/releases");
            LabelAt("INSTALLER & UPDATES", 425, 35, 540, 36, 22, orange);
            LabelAt("Choose your version. Keep your settings. Reuse your resources.", 427, 83, 535, 40, 10, muted);
            LabelAt("CHANNEL", 30, 148, 160, 24, 10, muted); LabelAt("VERSION", 250, 148, 540, 24, 10, muted);
            channel.SetBounds(30, 177, 195, 32); channel.DropDownStyle = ComboBoxStyle.DropDownList;
            channel.Items.AddRange(new object[] { "Published Windows players" }); channel.SelectedIndex = 0;
            versions.SetBounds(250, 177, 530, 32); versions.DropDownStyle = ComboBoxStyle.DropDownList;
            Controls.Add(channel); Controls.Add(versions);
            refresh = ButtonAt("CHECK UPDATES", 802, 174, 168, 38, false);
            installed.SetBounds(30, 223, 940, 26); installed.ForeColor = muted; Controls.Add(installed);
            LabelAt("WHAT CHANGES IN THIS VERSION", 30, 262, 940, 28, 11, orange);
            notes.SetBounds(30, 298, 940, 230); notes.ReadOnly = true; notes.BorderStyle = BorderStyle.FixedSingle;
            notes.BackColor = plate; notes.ForeColor = ink; notes.Font = new Font("Segoe UI", 10); notes.DetectUrls = false; Controls.Add(notes);
            LabelAt("GAME FOLDER", 30, 545, 820, 22, 9, muted);
            destination.SetBounds(30, 571, 760, 29); Controls.Add(destination); browse = ButtonAt("BROWSE", 810, 567, 160, 36, false);
            LabelAt("REUSE FILES FROM AN EXISTING BUILD (OPTIONAL)", 30, 613, 850, 22, 9, muted);
            seed.SetBounds(30, 639, 760, 29); Controls.Add(seed); reuse = ButtonAt("CHOOSE BUILD", 810, 635, 160, 36, false);
            progress.SetBounds(30, 690, 940, 8); progress.Style = ProgressBarStyle.Continuous; Controls.Add(progress);
            status.SetBounds(30, 703, 940, 24); status.ForeColor = ink; Controls.Add(status);
            transfer.SetBounds(30, 730, 410, 32); transfer.ForeColor = muted; transfer.Font = new Font("Segoe UI", 9); Controls.Add(transfer);
            install = ButtonAt("INSTALL / UPDATE", 450, 730, 220, 38, true);
            play = ButtonAt("PLAY", 688, 730, 120, 38, false); editor = ButtonAt("UNIT EDITOR", 825, 730, 145, 38, false);
            destination.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "OpenZ-VK", "Game");
            try { if (File.Exists(settingsPath)) { var saved = Format.Json().Deserialize<Settings>(File.ReadAllText(settingsPath)); if (!String.IsNullOrWhiteSpace(saved.directory)) destination.Text = saved.directory; } } catch { }
            foreach (var field in new[] { destination, seed }) { field.BackColor = plate; field.ForeColor = ink; field.BorderStyle = BorderStyle.FixedSingle; }
            channel.SelectedIndexChanged += async (s, e) => await LoadVersions();
            versions.SelectedIndexChanged += async (s, e) => { if (!loading) await LoadSelection(); };
            refresh.Click += async (s, e) => await LoadVersions();
            browse.Click += (s, e) => Choose(destination); reuse.Click += (s, e) => Choose(seed);
            destination.TextChanged += (s, e) => ReadInstalled();
            install.Click += async (s, e) => await InstallSelected();
            play.Click += (s, e) => Launch(false); editor.Click += (s, e) => Launch(true);
            Shown += async (s, e) =>
            {
                if (arguments.Length == 5 && arguments[0] == "--proof-local") await ProofLocal(arguments[1], arguments[2], arguments[3], arguments[4]);
                else if (arguments.Length == 2 && arguments[0] == "--local") LoadLocal(arguments[1]);
                else if (arguments.Length == 4 && arguments[0] == "--proof") await Proof(arguments[1], arguments[2], arguments[3]);
                else await LoadVersions();
                if (arguments.Length == 0 && SourceManager.FindGit() == null)
                {
                    Settings saved = File.Exists(settingsPath) ? Format.Json().Deserialize<Settings>(File.ReadAllText(settingsPath)) : new Settings();
                    if (!saved.gitOffered)
                    {
                        if (MessageBox.Show(this,"Git is optional. It will be used for source updates when installed. Open the official Git download page now? Choose No to continue with source archives and Unity.","Optional Git installation",MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes) Process.Start("https://git-scm.com/download/win");
                        saved.gitOffered=true;Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));File.WriteAllText(settingsPath,Format.Json().Serialize(saved));
                    }
                }
            };
            FormClosing += (s, e) => { if (installing) { e.Cancel = true; operation.Cancel(); status.Text = "Cancelling safely; the installed game stays available."; } };
            FormClosed += (s, e) => { operation.Cancel(); operation.Dispose(); github.Dispose(); notification.Dispose(); };
            var timer = new System.Windows.Forms.Timer { Interval = 30 * 60 * 1000 }; timer.Tick += async (s, e) => { if (!installing && !loading) await LoadVersions(); }; timer.Start();
            ReadInstalled(); SetEnabled();
        }
        string Channel { get { return "stable"; } }
        Label LabelAt(string text, int x, int y, int w, int h, float size, Color color)
        { var label = new Label { Text = text, Left = x, Top = y, Width = w, Height = h, ForeColor = color, Font = new Font("Segoe UI", size, FontStyle.Bold) }; Controls.Add(label); return label; }
        Button ButtonAt(string text, int x, int y, int w, int h, bool primary)
        {
            var button = new Button { Text = text, Left = x, Top = y, Width = w, Height = h, FlatStyle = FlatStyle.Flat, BackColor = primary ? orange : plate,
                ForeColor = ink, Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand, UseVisualStyleBackColor = false };
            button.FlatAppearance.BorderColor = primary ? orange : Color.FromArgb(80, 91, 80); Controls.Add(button); return button;
        }
        void Choose(TextBox field) { using (var dialog = new FolderBrowserDialog { Description = field == seed ? "Choose an existing extracted OpenZ-VK build. It will only be read." : "Choose a dedicated empty or installer-managed game folder.", ShowNewFolderButton = field != seed }) if (dialog.ShowDialog(this) == DialogResult.OK) field.Text = dialog.SelectedPath; }
        void ReadInstalled()
        {
            try { string path = Path.Combine(destination.Text, Format.InstalledName); if (File.Exists(path)) { var m = Format.Read(File.ReadAllText(path)); installed.Text = "INSTALLED  " + m.version + " · " + m.channel.ToUpperInvariant() + " · " + m.commit.Substring(0, 10); } else installed.Text = "Not installed in this folder. Existing preferences and profiles are kept in their original location."; }
            catch { installed.Text = "Could not read installation information. Choose a valid game folder."; }
            SetEnabled();
        }
        void SetEnabled()
        {
            bool ready = !loading && !installing;
            channel.Enabled = versions.Enabled = refresh.Enabled = destination.Enabled = seed.Enabled = browse.Enabled = reuse.Enabled = ready;
            install.Enabled = ready && selected != null;
            play.Enabled = editor.Enabled = ready && File.Exists(Path.Combine(destination.Text, "openz-vk.exe"));
        }
        async Task LoadVersions()
        {
            if (loading || installing) return;
            loading = true; selected = null; localFetch = null; versions.Items.Clear(); notes.Clear(); status.Text = "Checking published " + Channel + " versions…"; SetEnabled();
            try
            {
                var releases = await github.Releases(Channel, operation.Token);
                foreach (var release in releases.OrderByDescending(r => r.published_at, StringComparer.Ordinal)) versions.Items.Add(release);
                if (versions.Items.Count == 0) status.Text = "No installer-ready builds in this channel yet. Source-only commits are not playable builds.";
                else { versions.SelectedIndex = 0; await LoadSelection(); }
            }
            catch (Exception error) { status.Text = "Update check failed. " + error.Message; }
            finally { loading = false; SetEnabled(); }
        }
        async Task LoadSelection()
        {
            var release = versions.SelectedItem as Release; if (release == null || installing) return;
            selected = null; install.Enabled = false; status.Text = "Loading changelog and verified file list…";
            try
            {
                string wantedChannel = Channel;
                var manifest = await github.Manifest(release, wantedChannel, operation.Token);
                if (!Object.ReferenceEquals(versions.SelectedItem, release) || wantedChannel != Channel) return;
                selected = manifest; notes.Text = manifest.notes ?? release.body ?? "No changelog supplied.";
                string current = Path.Combine(destination.Text, Format.InstalledName);
                bool same = File.Exists(current) && Format.Read(File.ReadAllText(current)).commit == manifest.commit;
                status.Text = same ? "This version is installed. You can verify or repair it without redownloading unchanged files." : "VERSION AVAILABLE  " + manifest.version + " · Review the changelog, then install when ready.";
                transfer.Text = "Complete player: " + Format.Size(manifest.files.Sum(f => f.length)) + " · Git is not required";
                if (File.Exists(current) && Object.ReferenceEquals(versions.Items[0], release) && notifiedCommit != manifest.commit)
                {
                    Version available, previous;
                    if (Version.TryParse(manifest.version, out available) && Version.TryParse(Format.Read(File.ReadAllText(current)).version, out previous) && available > previous)
                    {
                        notifiedCommit = manifest.commit; notification.Visible = true;
                        notification.ShowBalloonTip(10000, "OpenZ-VK " + manifest.version + " is available", "Read the changelog in the installer before updating.", ToolTipIcon.Info);
                    }
                }
            }
            catch (Exception error) { status.Text = "Could not load this version. " + error.Message; }
            finally { SetEnabled(); }
        }
        async Task InstallSelected()
        {
            if (selected == null || installing) return;
            var manifest = selected; string path = destination.Text, source = seed.Text;
            installing = true; operation.Dispose(); operation = new CancellationTokenSource(); SetEnabled(); progress.Value = 0;
            try
            {
                var observer = new System.Progress<Progress>(p =>
                {
                    status.Text = p.message; progress.Value = p.total <= 0 ? 0 : (int)Math.Min(100, p.done * 100 / p.total);
                    transfer.Text = "Downloaded " + Format.Size(p.downloaded) + " · Reused " + Format.Size(p.reused);
                });
                var engine = new Installer(localFetch ?? github.Download);
                await Task.Run(() => engine.Install(manifest, path, source, observer, operation.Token));
                Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
                File.WriteAllText(settingsPath, Format.Json().Serialize(new Settings { directory = path, channel = manifest.channel }));
                ReadInstalled();
            }
            catch (OperationCanceledException) { status.Text = "Update cancelled. The installed version is unchanged; downloaded blocks can be reused next time."; }
            catch (Exception error) { status.Text = "Installation stopped; the previous version is preserved."; if (proofMode) throw; MessageBox.Show(this, error.Message, "OpenZ-VK installation", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            finally { installing = false; SetEnabled(); }
        }
        void Launch(bool unitEditor)
        {
            try { Process.Start(new ProcessStartInfo(Path.Combine(destination.Text, "openz-vk.exe"), unitEditor ? "-oz-unit-editor" : "") { WorkingDirectory = destination.Text, UseShellExecute = true }); }
            catch (Exception error) { MessageBox.Show(this, error.Message, "OpenZ-VK", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
        void LoadLocal(string manifestPath)
        {
            var manifest = Format.Read(File.ReadAllText(manifestPath));
            string folder = Path.GetDirectoryName(Path.GetFullPath(manifestPath));
            string resourcesIndex = Path.GetFullPath(Path.Combine(folder, "..", "unity-resources", "OpenZ-VK-source-resources.json"));
            localResourceIndex = File.Exists(resourcesIndex) ? resourcesIndex : null;
            foreach (var pack in manifest.packs)
                if (!File.Exists(Format.SafePath(folder, pack.name)) || Format.FileHash(Format.SafePath(folder, pack.name)) != pack.sha256) throw new InvalidDataException("Local payload verification failed.");
            loading = true; versions.Items.Clear(); versions.Items.Add(new Release { name = "OpenZ-VK " + manifest.version + " · LOCAL REVIEW CANDIDATE", tag_name = manifest.releaseTag }); versions.SelectedIndex = 0; loading = false;
            selected = manifest; notes.Text = manifest.notes; status.Text = "LOCAL REVIEW CANDIDATE · not published on GitHub. Install or repair from the verified local package.";
            localFetch = (m, chunk, token) =>
            {
                token.ThrowIfCancellationRequested(); byte[] packed = new byte[chunk.packedLength];
                using (var input = File.OpenRead(Format.SafePath(folder, chunk.asset)))
                {
                    input.Position = chunk.offset; int offset = 0, count;
                    while (offset < packed.Length && (count = input.Read(packed, offset, packed.Length - offset)) > 0) offset += count;
                    if (offset != packed.Length) throw new InvalidDataException("Truncated local payload.");
                }
                return Task.FromResult(Blocks.Inflate(packed, chunk));
            };
            ReadInstalled(); SetEnabled();
        }
        async Task ProofLocal(string manifestPath, string target, string existingBuild, string evidence)
        {
            Directory.CreateDirectory(evidence);
            try
            {
                destination.Text = Path.GetFullPath(target); seed.Text = Path.GetFullPath(existingBuild); LoadLocal(manifestPath);
                SaveCapture(Path.Combine(evidence, "installer-player-menu.png"));
                await InstallSelected(); await Task.Delay(150);
                if (!File.Exists(Path.Combine(destination.Text, Format.InstalledName))) throw new IOException("The local release candidate was not installed.");
                SaveCapture(Path.Combine(evidence, "installer-installed.png"));
                seed.Text = "";
                string repair = Format.SafePath(destination.Text, "UnityCrashHandler64.exe"); File.Delete(repair);
                await InstallSelected(); await Task.Delay(150);
                if (!File.Exists(repair) || Format.FileHash(repair) != selected.files.Single(f => f.path == "UnityCrashHandler64.exe").sha256) throw new IOException("Repair did not restore the exact release file.");
                SaveCapture(Path.Combine(evidence, "installer-repaired.png"));
                File.WriteAllText(Path.Combine(evidence, "result.txt"), "PASS: real native installer UI; embedded logo and changelog; complete local release installed using existing resources; damaged installation repaired from verified payload blocks; game/editor launchers enabled. No game launched; no GitHub mutation.");
            }
            catch (Exception error) { File.WriteAllText(Path.Combine(evidence, "result.txt"), "FAIL: " + error); Environment.ExitCode = 1; }
            Close();
        }
        // Explicit native UI/release proof. It renders this form's own controls,
        // never captures other windows or launches the game.
        async Task Proof(string target, string existingBuild, string evidence)
        {
            Directory.CreateDirectory(evidence);
            try
            {
                channel.SelectedIndex = 0; destination.Text = Path.GetFullPath(target); seed.Text = Path.GetFullPath(existingBuild);
                await LoadVersions();
                if (selected == null) throw new IOException("No stable manifest loaded: " + status.Text);
                SaveCapture(Path.Combine(evidence, "installer-ready.png"));
                await InstallSelected(); await Task.Delay(150);
                string marker = Path.Combine(destination.Text, Format.InstalledName);
                if (!File.Exists(marker) || Format.Read(File.ReadAllText(marker)).commit != selected.commit) throw new IOException("Native installer did not install the selected release.");
                SaveCapture(Path.Combine(evidence, "installer-installed.png"));
                File.WriteAllText(Path.Combine(evidence, "result.txt"), "PASS: public stable version discovery, displayed changelog, embedded logo, complete verified install from existing build, enabled game/editor launchers. Game not launched.");
            }
            catch (Exception error) { File.WriteAllText(Path.Combine(evidence, "result.txt"), "FAIL: " + error); Environment.ExitCode = 1; }
            Close();
        }
        void SaveCapture(string path)
        {
            using (var bitmap = new Bitmap(Width, Height)) { DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height)); bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png); }
        }
    }
    static class Program
    {
        [STAThread] static void Main(string[] args)
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            if(args.Length==3&&args[0]=="--source-ui-proof") Application.Run(new SourceWindow(args[2],args[1]));
            else Application.Run(new InstallerWindow(args));
        }
    }
}
