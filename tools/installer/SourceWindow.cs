using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenZVK.Update
{
    public sealed class SourceWindow : Form
    {
        readonly GitHub github = new GitHub();
        readonly ComboBox branch = new ComboBox(), commits = new ComboBox();
        readonly TextBox revision = new TextBox(), workspace = new TextBox(), resources = new TextBox(), unity = new TextBox();
        readonly RichTextBox log = new RichTextBox(); readonly Label detected = new Label();
        readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        readonly System.Collections.Generic.List<Button> actions = new System.Collections.Generic.List<Button>();
        bool busy; int page = 1;
        public SourceWindow(string evidence=null,string proofWorkspace=null,string resourcesIndex=null)
        {
            Text = "OpenZ-VK · Sources & Unity"; ClientSize = new Size(1000, 820); StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(18,22,21); ForeColor = Color.Beige; Font = new Font("Segoe UI",10); MaximizeBox = false;
            var logo = new PictureBox { Left=25,Top=10,Width=320,Height=100,SizeMode=PictureBoxSizeMode.Zoom };
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("OpenZVK.Logo")) using(var image=Image.FromStream(stream)) logo.Image=new Bitmap(image); Controls.Add(logo);
            LabelAt("SOURCES & UNITY BUILDS",380,28,580); LabelAt("Select any main/dev commit. Unity is required; Git is optional.",380,68,580);
            LabelAt("BRANCH",25,125,160); branch.SetBounds(25,153,160,30); branch.Items.AddRange(new object[]{"main","dev"}); branch.SelectedIndex=0; branch.DropDownStyle=ComboBoxStyle.DropDownList; Controls.Add(branch);
            commits.SetBounds(205,153,590,30); commits.DropDownStyle=ComboBoxStyle.DropDownList; Controls.Add(commits);
            Add("OLDER COMMITS",810,150,160,()=>LoadCommits(true));
            LabelAt("COMMIT (select above or paste any commit ID)",25,198,700); revision.SetBounds(25,225,770,28); Controls.Add(revision); Add("REFRESH",810,221,160,()=>LoadCommits(false));
            detected.SetBounds(25,270,760,28); Controls.Add(detected); Add("GET GIT",810,265,160,()=>{Process.Start("https://git-scm.com/download/win");return Task.FromResult(0);});
            LabelAt("SOURCE WORKSPACE (empty folder on first use)",25,307,750); workspace.SetBounds(25,335,770,28); Controls.Add(workspace); workspace.Text=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OpenZ-VK","Source"); Browse(workspace,810,331,false);
            if(proofWorkspace!=null)workspace.Text=proofWorkspace;
            LabelAt("EXISTING UNITY PROJECT TO REUSE RESOURCES (optional; otherwise download once)",25,377,945); resources.SetBounds(25,405,770,28); Controls.Add(resources); Browse(resources,810,401,false);
            LabelAt("UNITY EDITOR — required version is read from the selected sources",25,447,945); unity.SetBounds(25,475,770,28); Controls.Add(unity); Browse(unity,810,471,true);
            Add("1 · GET SOURCES",25,525,220,async()=>{var manager=new SourceManager(github); await manager.Retrieve(workspace.Text,branch.Text,revision.Text,SourceManager.FindGit()!=null,Log,cancellation.Token); Detect();});
            Add("2 · PREPARE RESOURCES",265,525,240,async()=>{string target=workspace.Text,seed=resources.Text;await Task.Run(()=>new SourceManager(github).Resources(target,seed,Log,cancellation.Token,resourcesIndex));});
            Add("3 · BUILD RELEASE",525,525,220,()=>new SourceManager(github).Build(workspace.Text,unity.Text,Log,cancellation.Token));
            Add("GET UNITY",765,525,205,()=>
            {
                string path=Path.Combine(workspace.Text,"source/ProjectSettings/ProjectVersion.txt");
                if(File.Exists(path)) {var match=System.Text.RegularExpressions.Regex.Match(File.ReadAllText(path),@"m_EditorVersionWithRevision:\s*(\S+)\s*\(([^)]+)\)"); if(match.Success){try{Process.Start("unityhub://"+match.Groups[1].Value+"/"+match.Groups[2].Value);return Task.FromResult(0);}catch(System.ComponentModel.Win32Exception){Log("Install Unity Hub first, activate its license, then install the required editor with Windows Build Support.");}}}
                Process.Start("https://unity.com/download");return Task.FromResult(0);
            });
            log.SetBounds(25,585,945,210); log.ReadOnly=true; log.BackColor=Color.FromArgb(30,36,33);log.ForeColor=Color.Beige;Controls.Add(log);
            commits.SelectedIndexChanged+=(s,e)=>{var item=commits.SelectedItem as CommitChoice;if(item!=null)revision.Text=item.sha;};
            branch.SelectedIndexChanged+=async(s,e)=>await Action(()=>LoadCommits(false));
            Shown+=async(s,e)=>
            {
                Detect();await Action(()=>LoadCommits(false));
                if(evidence!=null)
                {
                    Directory.CreateDirectory(evidence);SaveCapture(Path.Combine(evidence,"installer-sources-main.png"));
                    branch.SelectedIndex=1;
                    while(busy)await Task.Delay(100);
                    SaveCapture(Path.Combine(evidence,"installer-sources-dev.png"));Close();
                }
            };
            FormClosing+=(s,e)=>{if(busy){e.Cancel=true;Log("Wait for the current operation to finish; Unity builds are not interrupted.");}};
            FormClosed+=(s,e)=>{cancellation.Cancel();cancellation.Dispose();github.Dispose();};
        }
        void LabelAt(string text,int x,int y,int width){Controls.Add(new Label{Text=text,Left=x,Top=y,Width=width,Height=26,ForeColor=Color.FromArgb(220,150,75)});}
        void Add(string text,int x,int y,int width,Func<Task> run){var b=new Button{Text=text,Left=x,Top=y,Width=width,Height=38,FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(57,47,35),ForeColor=Color.Beige};b.Click+=async(s,e)=>await Action(run);Controls.Add(b);actions.Add(b);}
        void Browse(TextBox field,int x,int y,bool executable){Add("BROWSE",x,y,160,()=>{if(executable){using(var d=new OpenFileDialog{Filter="Unity editor|Unity.exe"})if(d.ShowDialog(this)==DialogResult.OK)field.Text=d.FileName;}else using(var d=new FolderBrowserDialog())if(d.ShowDialog(this)==DialogResult.OK)field.Text=d.SelectedPath;return Task.FromResult(0);});}
        async Task Action(Func<Task> run){if(busy)return;busy=true;foreach(var b in actions)b.Enabled=false;branch.Enabled=commits.Enabled=false;try{await run();}catch(Exception error){Log("STOPPED: "+error.Message);}finally{busy=false;foreach(var b in actions)b.Enabled=true;branch.Enabled=commits.Enabled=true;}}
        async Task LoadCommits(bool older){page=older?page+1:1;var list=await new SourceManager(github).Commits(branch.Text,page,cancellation.Token);if(!older)commits.Items.Clear();foreach(var item in list)commits.Items.Add(item);if(commits.Items.Count>0&&commits.SelectedIndex<0)commits.SelectedIndex=0;Log("Choose a commit, retrieve sources, prepare resources, then build. Unity Hub needs a valid local license and Windows Build Support.");}
        void Detect(){string git=SourceManager.FindGit();string version=SourceManager.RequiredUnity(workspace.Text);detected.Text=(git==null?"Git not found · archives will be used; GET GIT is optional.":"Git detected · source retrieval will use Git.")+"   Unity: "+version;var path=SourceManager.FindUnity(version);if(path!=null)unity.Text=path;}
        void Log(string text){if(InvokeRequired){BeginInvoke(new Action<string>(Log),text);return;}log.AppendText(text+Environment.NewLine);log.SelectionStart=log.TextLength;log.ScrollToCaret();}
        void SaveCapture(string path){using(var image=new Bitmap(Width,Height)){DrawToBitmap(image,new Rectangle(0,0,Width,Height));image.Save(path,System.Drawing.Imaging.ImageFormat.Png);}}
    }
}
