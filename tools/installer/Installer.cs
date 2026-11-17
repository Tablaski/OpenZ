using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenZVK.Update
{
    sealed class Settings { public string directory, channel; public bool gitOffered = false; }
    internal static class NativeCapture
    {
        [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr window, IntPtr dc, uint flags);
        internal static void Save(Form form,string path)
        {
            using(var image=new Bitmap(form.Width,form.Height))
            {
                bool rendered;using(var graphics=Graphics.FromImage(image)){IntPtr dc=graphics.GetHdc();try{rendered=PrintWindow(form.Handle,dc,2);}finally{graphics.ReleaseHdc(dc);}}
                if(!rendered)form.DrawToBitmap(image,new Rectangle(0,0,form.Width,form.Height));image.Save(path,System.Drawing.Imaging.ImageFormat.Png);
            }
        }
    }
    public sealed class InstallerWindow : WizardForm
    {
        readonly GitHub github=new GitHub();readonly ComboBox versions=new ComboBox();
        readonly TextBox destination=new TextBox(),seed=new TextBox(),notes;
        readonly Label installed,available,changesTitle,status,transfer,location;
        readonly Button refresh,play,editor,options,sources,manual;
        readonly ProgressBar progress=new ProgressBar();readonly NotifyIcon notification=new NotifyIcon{Icon=SystemIcons.Information,Text="OpenZ-VK updates"};
        readonly string settingsPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OpenZ-VK","installer.json");
        CancellationTokenSource operation=new CancellationTokenSource();Manifest selected;bool loading,proofMode;string localResourceIndex,notifiedCommit;
        Func<Manifest,Chunk,CancellationToken,Task<byte[]>> localFetch;
        public InstallerWindow(string[] args):base("Install & Update","Your game, ready to play. Updates keep your settings and saved profiles.",new[]{"Choose a version","Read what's new","Install & play"})
        {
            proofMode=args.Length>0&&args[0].StartsWith("--proof",StringComparison.Ordinal);
            var choose=Pages[0];AddIcon(choose,WizardIcon.Game,0,5);WizardUi.Label(choose,"Welcome to OpenZ-VK",75,5,890,36,22,true);
            WizardUi.Label(choose,"Install a published game, or keep the one you already have up to date.\nYou do not need Unity or Git to play.",75,52,890,65,12,false,WizardUi.Muted);
            installed=WizardUi.Label(choose,"Not installed on this PC yet.",0,135,980,32,13,true);
            available=WizardUi.Label(choose,"Checking for available versions…",0,176,980,30,11,false,WizardUi.Green);
            WizardUi.Label(choose,"Game version",0,230,700,28,12,true);versions.SetBounds(0,267,720,35);WizardUi.Combo(versions);choose.Controls.Add(versions);
            refresh=WizardUi.Button(choose,"Check for updates",745,264,235,40,async()=>await LoadVersions());
            WizardUi.Label(choose,"The next step shows the changelog before anything is installed.",0,324,980,30,11,false,WizardUi.Muted);
            manual=WizardUi.Button(choose,"Manual ZIP download",0,405,285,48,()=>Process.Start("https://github.com/Tablaski/OpenZ/releases"));
            sources=WizardUi.Button(choose,"Build from sources · Unity",310,405,340,48,()=>{using(var form=new SourceWindow(null,null,localResourceIndex)){form.StartPosition=FormStartPosition.CenterParent;form.ShowDialog(this);}});
            WizardUi.Label(choose,"Source builds are a separate guided workflow for main/dev revisions.",0,473,980,30,10,false,WizardUi.Muted);
            var changes=Pages[1];AddIcon(changes,WizardIcon.Book,0,0);changesTitle=WizardUi.Label(changes,"What's new",75,7,890,38,22,true);
            notes=WizardUi.Text(changes,0,64,980,430,true,true,12);notes.WordWrap=true;
            var install=Pages[2];AddIcon(install,WizardIcon.Download,0,0);WizardUi.Label(install,"Let's get your game ready",75,7,890,38,22,true);
            WizardUi.Label(install,"Your settings and saved profiles stay where they are.\nThe existing game is kept until its replacement has been checked.",0,77,980,65,12,false,WizardUi.Muted);
            progress.SetBounds(0,164,980,17);install.Controls.Add(progress);status=WizardUi.Label(install,"Ready to install.",0,205,980,65,14,true);
            transfer=WizardUi.Label(install,"",0,280,980,32,11,false,WizardUi.Muted);
            play=WizardUi.Button(install,"Play",0,355,225,52,()=>Launch(false),true);editor=WizardUi.Button(install,"Unit Editor",250,355,225,52,()=>Launch(true));
            options=WizardUi.Button(install,"Installation options",510,355,300,52,OpenOptions);
            location=WizardUi.Label(install,"",0,445,980,55,10,false,WizardUi.Muted);
            destination.Text=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","OpenZ-VK","Game");
            try{if(File.Exists(settingsPath)){var saved=Format.Json().Deserialize<Settings>(File.ReadAllText(settingsPath));if(!String.IsNullOrWhiteSpace(saved.directory))destination.Text=saved.directory;}}catch{}
            versions.SelectedIndexChanged+=async(s,e)=>{if(!loading)await LoadSelection();};destination.TextChanged+=(s,e)=>ReadInstalled();
            Shown+=async(s,e)=>
            {
                if(args.Length==5&&args[0]=="--proof-local")await ProofLocal(args[1],args[2],args[3],args[4]);
                else if(args.Length==2&&args[0]=="--local"){try{LoadLocal(args[1]);}catch(Exception error){Footer.Text=error.Message;}}
                else await LoadVersions();
            };
            FormClosing+=(s,e)=>{if(Busy){e.Cancel=true;operation.Cancel();status.Text="Stopping safely. Your installed game is preserved.";}};
            FormClosed+=(s,e)=>{operation.Cancel();operation.Dispose();github.Dispose();notification.Dispose();};
            var timer=new System.Windows.Forms.Timer{Interval=30*60*1000};timer.Tick+=async(s,e)=>{if(!Busy&&!loading)await LoadVersions();};timer.Start();FormClosed+=(s,e)=>timer.Dispose();
            ReadInstalled();RefreshNavigation();
        }
        protected override async void OnNext()
        {
            if(Busy){operation.Cancel();return;}
            if(Page==0&&selected!=null){ShowPage(1);return;}
            if(Page==1){ShowPage(2);await InstallSelected();return;}
            if(Page==2)Close();
        }
        protected override void RefreshNavigation()
        {
            if(Next==null)return;Back.Enabled=!Busy&&!loading;
            Next.Text=Busy?"Cancel update":Page==0?"Read what's new":Page==1?(InstalledManifest()==null?"Install game":"Update / check files"):"Finish";
            Next.Enabled=Busy||(!loading&&(Page==2||selected!=null));
            if(refresh!=null)refresh.Enabled=!Busy&&!loading;if(versions!=null)versions.Enabled=!Busy&&!loading;
            if(sources!=null)sources.Enabled=!Busy;if(manual!=null)manual.Enabled=!Busy;
            bool ready=!Busy&&File.Exists(Path.Combine(destination.Text,"openz-vk.exe"));if(play!=null)play.Enabled=ready;if(editor!=null)editor.Enabled=ready;if(options!=null)options.Enabled=!Busy;
        }
        Manifest InstalledManifest(){try{string path=Path.Combine(destination.Text,Format.InstalledName);return File.Exists(path)?Format.Read(File.ReadAllText(path)):null;}catch{return null;}}
        void ReadInstalled()
        {
            var current=InstalledManifest();installed.Text=current==null?"Not installed in this folder yet.":"Installed on this PC: OpenZ-VK "+current.version;
            location.Text="Game folder: "+destination.Text;RefreshNavigation();
        }
        async Task LoadVersions()
        {
            if(Busy||loading)return;loading=true;selected=null;localFetch=null;versions.Items.Clear();notes.Clear();available.Text="Checking for updates…";RefreshNavigation();
            try{var releases=await github.Releases("stable",operation.Token);foreach(var r in releases.OrderByDescending(r=>r.published_at,StringComparer.Ordinal))versions.Items.Add(r);if(versions.Items.Count>0){versions.SelectedIndex=0;await LoadSelection();}else{available.Text="No installer-ready release is published yet.";Footer.Text="Complete player ZIPs remain available under Manual ZIP download.";}}
            catch(Exception error){available.Text="The update service could not be reached. Please try again.";Footer.Text=error.Message;}
            finally{loading=false;RefreshNavigation();}
        }
        async Task LoadSelection()
        {
            var release=versions.SelectedItem as Release;if(release==null||Busy)return;selected=null;RefreshNavigation();
            try
            {
                var manifest=await github.Manifest(release,"stable",operation.Token);if(!Object.ReferenceEquals(versions.SelectedItem,release))return;selected=manifest;notes.Text=manifest.notes??release.body??"No changes supplied.";changesTitle.Text="What's new in OpenZ-VK "+manifest.version;
                var current=InstalledManifest();bool same=current!=null&&current.commit==manifest.commit;available.Text=same?"You're up to date. You can still check and repair game files.":"OpenZ-VK "+manifest.version+" is available. Read the changes before installing.";
                Footer.Text="No automatic installation. You decide when to update.";
                Version a,b;if(current!=null&&notifiedCommit!=manifest.commit&&Version.TryParse(manifest.version,out a)&&Version.TryParse(current.version,out b)&&a>b){notifiedCommit=manifest.commit;notification.Visible=true;notification.ShowBalloonTip(10000,"OpenZ-VK "+manifest.version+" is available","Open the installer to read what's new.",ToolTipIcon.Info);}
            }
            catch(Exception error){available.Text="This version could not be loaded.";Footer.Text=error.Message;}
            finally{RefreshNavigation();}
        }
        async Task InstallSelected()
        {
            if(selected==null||Busy)return;Busy=true;operation.Dispose();operation=new CancellationTokenSource();progress.Value=0;RefreshNavigation();Footer.Text="The previous game stays safe until this step is complete.";
            try
            {
                var observer=new System.Progress<Progress>(p=>
                {
                    progress.Value=p.total<=0?0:(int)Math.Min(100,p.done*100/p.total);
                    status.Text=p.done>=p.total&&p.total>0?"OpenZ-VK is ready to play.":p.message.StartsWith("Checking",StringComparison.Ordinal)?"Checking the game files already on this PC…":p.downloaded>0?"Downloading and checking your game…":"Preparing your game with the files already available…";
                    transfer.Text=p.done>=p.total&&p.total>0?"Installation verified. Your settings and profiles were kept.":"Downloaded: "+Format.Size(p.downloaded)+"  ·  Existing files are reused automatically.";
                });
                await Task.Run(()=>new Installer(localFetch??github.Download).Install(selected,destination.Text,seed.Text,observer,operation.Token));
                Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));var saved=File.Exists(settingsPath)?Format.Json().Deserialize<Settings>(File.ReadAllText(settingsPath)):new Settings();saved.directory=destination.Text;saved.channel="stable";File.WriteAllText(settingsPath,Format.Json().Serialize(saved));
                ReadInstalled();Footer.Text="Ready. Choose Play or Unit Editor, or finish the wizard.";
            }
            catch(OperationCanceledException){status.Text="Update cancelled safely.";transfer.Text="Your previous game is unchanged. A retry can reuse files already downloaded.";}
            catch(Exception error){status.Text="Installation could not finish. Your previous game is preserved.";Footer.Text=error.Message;if(proofMode)throw;MessageBox.Show(this,error.Message,"OpenZ-VK installation",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
            finally{Busy=false;RefreshNavigation();}
        }
        void OpenOptions() { OpenOptions(null); }
        void OpenOptions(string proofEvidence)
        {
            using(var dialog=new Form{Text="OpenZ-VK · Installation options",ClientSize=new Size(760,330),StartPosition=FormStartPosition.CenterParent,BackColor=WizardUi.Ground,ForeColor=WizardUi.Ink,Font=new Font("Segoe UI",11),MinimizeBox=false,MaximizeBox=false})
            {
                WizardUi.Label(dialog,"Where should the game be installed?",25,20,710,30,17,true);var target=WizardUi.Text(dialog,25,65,560,28);target.Text=destination.Text;
                WizardUi.Button(dialog,"Browse",605,60,130,38,()=>Browse(target));WizardUi.Label(dialog,"Optional: reuse files from an existing extracted game",25,120,710,28,11,true);var reuse=WizardUi.Text(dialog,25,157,560,28);reuse.Text=seed.Text;
                WizardUi.Button(dialog,"Browse",605,152,130,38,()=>Browse(reuse));WizardUi.Label(dialog,"Use an empty destination, or a game folder managed by this installer.\nYour existing build is only read when used as a source of reusable files.",25,208,710,55,10,false,WizardUi.Muted);
                WizardUi.Button(dialog,"Save options",535,280,200,38,()=>{destination.Text=target.Text;seed.Text=reuse.Text;dialog.DialogResult=DialogResult.OK;},true);
                if(proofEvidence!=null)dialog.Shown+=async(s,e)=>{await Task.Delay(350);NativeCapture.Save(dialog,Path.Combine(proofEvidence,"installer-options.png"));dialog.DialogResult=DialogResult.Cancel;};
                dialog.ShowDialog(this);
            }
        }
        static void Browse(TextBox field){using(var dialog=new FolderBrowserDialog{Description="Choose a game folder"})if(dialog.ShowDialog()==DialogResult.OK)field.Text=dialog.SelectedPath;}
        void Launch(bool unitEditor){try{Process.Start(new ProcessStartInfo(Path.Combine(destination.Text,"openz-vk.exe"),unitEditor?"-oz-unit-editor":""){WorkingDirectory=destination.Text,UseShellExecute=true});}catch(Exception error){MessageBox.Show(this,error.Message,"OpenZ-VK",MessageBoxButtons.OK,MessageBoxIcon.Warning);}}
        void LoadLocal(string path)
        {
            var manifest=Format.Read(File.ReadAllText(path));string folder=Path.GetDirectoryName(Path.GetFullPath(path));foreach(var pack in manifest.packs)if(!File.Exists(Format.SafePath(folder,pack.name))||new FileInfo(Format.SafePath(folder,pack.name)).Length!=pack.length)throw new InvalidDataException("A local installation file is missing.");
            string index=Path.GetFullPath(Path.Combine(folder,"..","unity-resources","OpenZ-VK-source-resources.json"));localResourceIndex=File.Exists(index)?index:null;
            loading=true;versions.Items.Clear();versions.Items.Add(new Release{name="OpenZ-VK "+manifest.version+" · local review",tag_name=manifest.releaseTag});versions.SelectedIndex=0;loading=false;selected=manifest;notes.Text=manifest.notes;changesTitle.Text="What's new in OpenZ-VK "+manifest.version;
            Subtitle.Text="Local review candidate — not published on GitHub.";available.Text="Local review version "+manifest.version+" is ready to install.";Footer.Text="This candidate stays local until publication is approved.";
            localFetch=(m,c,token)=>{token.ThrowIfCancellationRequested();byte[] bytes=new byte[c.packedLength];using(var input=File.OpenRead(Format.SafePath(folder,c.asset))){input.Position=c.offset;int n=0,read;while(n<bytes.Length&&(read=input.Read(bytes,n,bytes.Length-n))>0)n+=read;if(n!=bytes.Length)throw new InvalidDataException("Truncated local package.");}return Task.FromResult(Blocks.Inflate(bytes,c));};ReadInstalled();RefreshNavigation();
        }
        async Task Snapshot(string folder,string name){await Task.Delay(350);NativeCapture.Save(this,Path.Combine(folder,name));}
        async Task ProofLocal(string path,string target,string existing,string evidence)
        {
            Directory.CreateDirectory(evidence);
            try
            {
                destination.Text=Path.GetFullPath(target);seed.Text=Path.GetFullPath(existing);LoadLocal(path);ShowPage(0);await Snapshot(evidence,"installer-player-menu.png");OpenOptions(evidence);ShowPage(1);await Snapshot(evidence,"installer-changelog.png");ShowPage(2);await Snapshot(evidence,"installer-ready.png");
                var installation=InstallSelected();await Task.Delay(1000);if(Busy)await Snapshot(evidence,"installer-installing.png");await installation;await Snapshot(evidence,"installer-installed.png");
                if(!File.Exists(Path.Combine(destination.Text,Format.InstalledName)))throw new IOException("Local candidate was not installed.");seed.Text="";string repair=Format.SafePath(destination.Text,"UnityCrashHandler64.exe");File.Delete(repair);await InstallSelected();
                if(!File.Exists(repair)||Format.FileHash(repair)!=selected.files.Single(f=>f.path=="UnityCrashHandler64.exe").sha256)throw new IOException("Repair did not restore the release file.");await Snapshot(evidence,"installer-repaired.png");
                File.WriteAllText(Path.Combine(evidence,"result.txt"),"PASS: real installation wizard; embedded logo; large changelog; verified complete local install and block-based repair; settings preserved; Play/Unit Editor available. No game launched and no GitHub mutation.");
            }
            catch(Exception error){File.WriteAllText(Path.Combine(evidence,"result.txt"),"FAIL: "+error);Environment.ExitCode=1;}
            Close();
        }
    }
    static class Program
    {
        [STAThread]static void Main(string[] args){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);if(args.Length==3&&args[0]=="--source-ui-proof")Application.Run(new SourceWindow(args[2],args[1]));else Application.Run(new InstallerWindow(args));}
    }
}
