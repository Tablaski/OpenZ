using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenZVK.Update
{
    public sealed class SourceWindow : WizardForm
    {
        readonly GitHub github=new GitHub();CancellationTokenSource operation=new CancellationTokenSource();
        readonly ComboBox branch=new ComboBox(),commits=new ComboBox();readonly TextBox revision,workspace,existing,unity,details,activity;
        readonly Label gitInfo,unityInfo,resourceInfo,buildInfo;readonly Button getGit,getUnity,refreshTools,older,browseWorkspace,browseExisting,browseUnity,openBuild;
        readonly string localResources,evidence;string requiredVersion="";int historyPage=1;bool loading,building;
        public SourceWindow(string evidenceFolder=null,string proofWorkspace=null,string resourcesIndex=null):base("Build from sources","A guided way to retrieve a version and build it with Unity.",new[]{"Tools","Choose version","Game resources","Build your game"})
        {
            evidence=evidenceFolder;localResources=resourcesIndex;StartPosition=FormStartPosition.CenterParent;
            var tools=Pages[0];WizardUi.Label(tools,"First, check your tools",0,0,980,38,22,true);
            var gitCard=WizardUi.Card(tools,WizardIcon.Git,"Git · optional","",65,145);gitInfo=WizardUi.Label(gitCard,"Checking Git…",96,60,600,60,11,false,WizardUi.Muted);
            getGit=WizardUi.Button(gitCard,"Install Git",720,48,230,44,()=>Process.Start("https://git-scm.com/download/win"));
            var unityCard=WizardUi.Card(tools,WizardIcon.Unity,"Unity · required for compiling","",230,175);unityInfo=WizardUi.Label(unityCard,"Checking the required editor…",96,60,600,100,11,false,WizardUi.Muted);
            getUnity=WizardUi.Button(unityCard,"Install Unity / Hub",720,47,230,44,GetUnity);
            unity=new TextBox();browseUnity=WizardUi.Button(unityCard,"Choose another editor",720,111,230,38,()=>{using(var dialog=new OpenFileDialog{Filter="Unity editor|Unity.exe"})if(dialog.ShowDialog(this)==DialogResult.OK){unity.Text=dialog.FileName;ApplyToolState();}});
            refreshTools=WizardUi.Button(tools,"Check tools again",0,434,245,42,async()=>await Execute(RefreshTools));
            WizardUi.Label(tools,"Already installed tools are marked ready. Setup buttons are disabled once detected.\nYou may continue without Git; source snapshots will be used instead.",275,432,705,67,11,false,WizardUi.Muted);
            var version=Pages[1];WizardUi.Label(version,"Choose the version you want",0,0,980,38,22,true);WizardUi.Label(version,"Stable sources (main) or development sources (dev)",0,48,980,28,11,false,WizardUi.Muted);
            branch.SetBounds(0,90,260,35);branch.Items.AddRange(new object[]{"main","dev"});branch.SelectedIndex=0;WizardUi.Combo(branch);version.Controls.Add(branch);
            commits.SetBounds(0,150,735,35);WizardUi.Combo(commits);version.Controls.Add(commits);older=WizardUi.Button(version,"Older versions",755,146,225,42,async()=>await Execute(()=>LoadHistory(true)));
            WizardUi.Label(version,"About this version",0,205,980,25,11,true);details=WizardUi.Text(version,0,237,980,91,true,true,11);
            WizardUi.Label(version,"Specific commit ID (optional)",0,346,980,25,10,false,WizardUi.Muted);revision=WizardUi.Text(version,0,375,980,29);
            WizardUi.Label(version,"Working folder · choose an empty folder on first use",0,429,980,26,11,true);workspace=WizardUi.Text(version,0,467,735,29);workspace.Text=proofWorkspace??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OpenZ-VK","Source");browseWorkspace=WizardUi.Button(version,"Choose folder",755,461,225,42,()=>Browse(workspace));
            var resources=Pages[2];AddIcon(resources,WizardIcon.Folder,0,0);WizardUi.Label(resources,"Keep the large files, change the sources",75,3,890,38,21,true);
            WizardUi.Label(resources,"Unchanged models, textures, maps and sound are reused. If a version needs new\nbase assets, this step prepares its matching package before you build.",0,72,980,70,12,false,WizardUi.Muted);
            resourceInfo=WizardUi.Label(resources,"",0,160,980,66,13,true,WizardUi.Green);
            WizardUi.Label(resources,"Optional: existing full Unity project to reuse",0,247,980,28,11,true);existing=WizardUi.Text(resources,0,286,735,29);browseExisting=WizardUi.Button(resources,"Choose project",755,281,225,42,()=>Browse(existing));
            WizardUi.Label(resources,"Leave this empty to use the published resource bundle. A local review package\nuses its bundled resources automatically. Existing projects are only read.",0,342,980,65,11,false,WizardUi.Muted);
            WizardUi.Label(resources,"Your personal game settings and saved profiles are stored separately and kept.",0,447,980,50,11,false,WizardUi.Muted);
            var build=Pages[3];AddIcon(build,WizardIcon.Build,0,0);WizardUi.Label(build,"Build your own game",75,4,890,38,22,true);
            buildInfo=WizardUi.Label(build,"",0,77,980,100,12,true);WizardUi.Label(build,"Activity",0,199,980,25,11,true);activity=WizardUi.Text(build,0,236,980,195,true,true,10);
            openBuild=WizardUi.Button(build,"Open the build folder",0,454,300,44,()=>Process.Start(Path.Combine(workspace.Text,"builds","OpenZ-VK-Local")));
            WizardUi.Button(build,"Review tools",330,454,230,44,()=>ShowPage(0));
            branch.SelectedIndexChanged+=async(s,e)=>{if(!loading)await Execute(()=>LoadHistory(false));};commits.SelectedIndexChanged+=(s,e)=>{var c=commits.SelectedItem as CommitChoice;if(c!=null){revision.Text=c.sha;details.Text=c.commit.message;}};
            Shown+=async(s,e)=>
            {
                await Execute(async()=>{await RefreshTools();await LoadHistory(false);});
                if(evidence!=null)await CaptureWizard();
            };
            FormClosing+=(s,e)=>{if(Busy){e.Cancel=true;if(!building)operation.Cancel();Footer.Text=building?"Unity is still working. The window can close when the build finishes.":"Stopping safely…";}};
            FormClosed+=(s,e)=>{operation.Cancel();operation.Dispose();github.Dispose();};RefreshNavigation();
        }
        protected override async void OnNext()
        {
            if(Busy)return;
            if(Page==0){ShowPage(1);return;}
            if(Page==1)
            {
                string target=workspace.Text,source=branch.Text,commit=revision.Text;
                await Execute(async()=>{await new SourceManager(github).Retrieve(target,source,commit,SourceManager.FindGit()!=null,Log,operation.Token);await RefreshTools();ShowPage(2);RefreshResourceState();});return;
            }
            if(Page==2)
            {
                if(ResourcesReady()){ShowPage(3);RefreshBuildState();return;}
                string target=workspace.Text,seed=existing.Text;
                await Execute(async()=>{await Task.Run(()=>new SourceManager(github).Resources(target,seed,Log,operation.Token,localResources));RefreshResourceState();ShowPage(3);RefreshBuildState();});return;
            }
            if(Page==3)
            {
                if(!File.Exists(unity.Text)){ShowPage(0);await Execute(RefreshTools);return;}
                string target=workspace.Text,editor=unity.Text;
                await Execute(async()=>{building=true;try{await new SourceManager(github).Build(target,editor,Log,operation.Token);}finally{building=false;}RefreshBuildState();});
            }
        }
        protected override void OnBack(){if(Busy&&!building){operation.Cancel();return;}base.OnBack();if(Page==2)RefreshResourceState();if(Page==3)RefreshBuildState();}
        protected override void RefreshNavigation()
        {
            if(Next==null)return;Back.Enabled=Busy?!building:Page>0;Back.Text=Busy?"Cancel":"Back";
            Next.Text=Busy?(building?"Building…":"Working…"):Page==0?"Choose a version":Page==1?"Get this version":Page==2?(ResourcesReady()?"Continue to build":"Prepare resources"):File.Exists(unity==null?"":unity.Text)?"Build game":"Set up Unity";
            Next.Enabled=!Busy&&!loading&&(Page!=1||revision!=null&&!String.IsNullOrWhiteSpace(revision.Text));
            if(older!=null)older.Enabled=!Busy;if(branch!=null)branch.Enabled=!Busy;if(commits!=null)commits.Enabled=!Busy;
            if(browseWorkspace!=null)browseWorkspace.Enabled=!Busy;if(browseExisting!=null)browseExisting.Enabled=!Busy;if(browseUnity!=null)browseUnity.Enabled=!Busy;
            if(workspace!=null)workspace.ReadOnly=Busy;if(revision!=null)revision.ReadOnly=Busy;if(existing!=null)existing.ReadOnly=Busy;
            if(refreshTools!=null)refreshTools.Enabled=!Busy;if(openBuild!=null)openBuild.Enabled=!Busy&&File.Exists(Path.Combine(workspace.Text,"builds/OpenZ-VK-Local/openz-vk.exe"));
            if(getGit!=null)getGit.Enabled=!Busy&&SourceManager.FindGit()==null;if(getUnity!=null)getUnity.Enabled=!Busy&&!File.Exists(unity.Text);
        }
        async Task Execute(Func<Task> run)
        {
            if(Busy)return;if(operation.IsCancellationRequested){operation.Dispose();operation=new CancellationTokenSource();}Busy=true;RefreshNavigation();
            try{await run();}catch(OperationCanceledException){Log("Cancelled safely. Completed downloads and the previous source files are retained.");}catch(Exception error){Log("Could not finish: "+error.Message);Footer.Text=error.Message;}
            finally{Busy=false;ApplyToolState();RefreshNavigation();}
        }
        async Task RefreshTools()
        {
            requiredVersion=SourceManager.RequiredUnity(workspace.Text);
            if(!Regex.IsMatch(requiredVersion,@"^\d+\.\d+\.\d+"))
            {
                try{string text=await github.Text("https://raw.githubusercontent.com/Tablaski/OpenZ/"+branch.Text+"/source/ProjectSettings/ProjectVersion.txt",65536,operation.Token);var m=Regex.Match(text,@"m_EditorVersion:\s*([^\r\n]+)");if(m.Success)requiredVersion=m.Groups[1].Value.Trim();}catch{}
            }
            string found=SourceManager.FindUnity(requiredVersion);if(found!=null)unity.Text=found;ApplyToolState();
        }
        void ApplyToolState()
        {
            if(gitInfo==null)return;bool git=SourceManager.FindGit()!=null,editor=File.Exists(unity.Text);
            gitInfo.Text=git?"Ready. Git was detected and will be used to retrieve sources.":"Optional. Install Git for clone/fetch, or continue without it and use source snapshots.";gitInfo.ForeColor=git?WizardUi.Green:WizardUi.Muted;
            getGit.Text=git?"Git is ready":"Install Git · optional";getGit.Enabled=!Busy&&!git;
            unityInfo.Text=editor?"Ready: Unity "+requiredVersion+".\n"+unity.Text:"Required editor: "+requiredVersion+".\nInstall it through Unity Hub with Windows Build Support, then activate your license.";unityInfo.ForeColor=editor?WizardUi.Green:WizardUi.Muted;
            getUnity.Text=editor?"Unity is ready":"Install Unity / Hub";getUnity.Enabled=!Busy&&!editor;
        }
        void GetUnity()
        {
            string path=Path.Combine(workspace.Text,"source/ProjectSettings/ProjectVersion.txt");
            if(File.Exists(path)){var m=Regex.Match(File.ReadAllText(path),@"m_EditorVersionWithRevision:\s*(\S+)\s*\(([^)]+)\)");if(m.Success)try{Process.Start("unityhub://"+m.Groups[1].Value+"/"+m.Groups[2].Value);return;}catch(System.ComponentModel.Win32Exception){}}
            Process.Start("https://unity.com/download");Footer.Text="Install Unity Hub, then the required editor with Windows Build Support.";
        }
        async Task LoadHistory(bool olderPage)
        {
            loading=true;historyPage=olderPage?historyPage+1:1;RefreshNavigation();
            try{var list=await new SourceManager(github).Commits(branch.Text,historyPage,operation.Token);if(!olderPage)commits.Items.Clear();foreach(var item in list)commits.Items.Add(item);if(commits.Items.Count>0&&commits.SelectedIndex<0)commits.SelectedIndex=0;Footer.Text="Choose a commit from main/dev, or paste its exact ID. Nothing is pushed to GitHub.";}
            finally{loading=false;RefreshNavigation();}
        }
        bool ResourcesReady()
        {
            if(workspace==null)return false;
            try{
                string pointer=Path.Combine(workspace.Text,"SOURCE_RESOURCES.json"),package=Path.Combine(workspace.Text,"source/Assets/OpenZ/Resources/OpenZ/resource_package.json");
                if(File.Exists(pointer)){
                    var pin=Format.Json().Deserialize<Dictionary<string,object>>(File.ReadAllText(pointer));
                    if(Convert.ToInt32(pin["schema"])>=2&&(!File.Exists(package)||(string)pin["version"]!=(string)Format.Json().Deserialize<Dictionary<string,object>>(File.ReadAllText(package))["version"]))return false;
                    string completed=Path.Combine(workspace.Text,".local/resources-state.json");
                    if(Convert.ToInt32(pin["schema"])>=2&&(!File.Exists(completed)||Format.Json().Deserialize<ResourceState>(File.ReadAllText(completed)).version!=(string)pin["version"]))return false;
                }
                return File.Exists(Path.Combine(workspace.Text,".local/resources-ready.txt"))&&File.Exists(Path.Combine(workspace.Text,"source/Assets/OpenZ/Scenes/MainMenu.unity"))&&File.Exists(Path.Combine(workspace.Text,"source/Packages/org.openz.datachannel/Plugins/x86_64/datachannel.dll"));
            }catch{return false;}
        }
        void RefreshResourceState(){resourceInfo.Text=ResourcesReady()?"The matching resource package is ready. No resource download is needed.":localResources!=null?"The required resource package will be prepared from this local review bundle.":"Prepare the package required by this version. Unchanged resources are kept.";}
        void RefreshBuildState()
        {
            string result=Path.Combine(workspace.Text,"builds/OpenZ-VK-Local/BUILD_RESULT.txt");bool done=File.Exists(result)&&File.ReadAllText(result).StartsWith("Succeeded");
            buildInfo.Text=done?"Your build finished successfully.\nOpen the build folder to find the game and keep the complete folder together.":"Selected Unity: "+requiredVersion+"\nUnity will compile a release player. This can take a few minutes; the game will not be launched automatically.";
            openBuild.Enabled=done&&!Busy;
        }
        static void Browse(TextBox field){using(var dialog=new FolderBrowserDialog{Description="Choose a regular folder for the Unity project"})if(dialog.ShowDialog()==DialogResult.OK)field.Text=dialog.SelectedPath;}
        void Log(string text){if(InvokeRequired){BeginInvoke(new Action<string>(Log),text);return;}activity.AppendText(text+Environment.NewLine);activity.SelectionStart=activity.TextLength;activity.ScrollToCaret();Footer.Text=text.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).LastOrDefault()??"";}
        async Task Save(string name){await Task.Delay(350);NativeCapture.Save(this,Path.Combine(evidence,name));}
        async Task CaptureWizard()
        {
            Directory.CreateDirectory(evidence);
            try
            {
                if(commits.Items.Count==0)throw new IOException("The actual GitHub version history did not load.");
                ShowPage(0);await Save("installer-source-tools.png");if(SourceManager.FindGit()!=null&&getGit.Enabled)throw new Exception("Installed Git setup button was not disabled.");if(File.Exists(unity.Text)&&getUnity.Enabled)throw new Exception("Installed Unity setup button was not disabled.");
                ShowPage(1);await Save("installer-sources-main.png");branch.SelectedIndex=1;while(Busy||loading)await Task.Delay(100);await Save("installer-sources-dev.png");
                ShowPage(2);RefreshResourceState();await Save("installer-source-resources.png");ShowPage(3);RefreshBuildState();await Save("installer-source-build.png");
                File.WriteAllText(Path.Combine(evidence,"source-ui-result.txt"),"PASS: real source wizard pages, main/dev history, resources and build state; installed Git/Unity setup buttons disabled.");
            }
            catch(Exception error){File.WriteAllText(Path.Combine(evidence,"source-ui-result.txt"),"FAIL: "+error);Environment.ExitCode=1;}
            Close();
        }
    }
}
