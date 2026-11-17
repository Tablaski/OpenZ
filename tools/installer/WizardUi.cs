using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace OpenZVK.Update
{
    public enum WizardIcon { Game, Download, Refresh, Git, Unity, Folder, Book, Build, Check }
    internal sealed class Pictogram : Control
    {
        internal WizardIcon Kind; internal Color Ink = WizardUi.Orange;
        internal Pictogram(WizardIcon kind) { Kind=kind; Size=new Size(54,54); SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias; e.Graphics.ScaleTransform(Width/54f,Height/54f);
            using(var pen=new Pen(Ink,2.6f)){pen.StartCap=pen.EndCap=LineCap.Round;
                if(Kind==WizardIcon.Download){e.Graphics.DrawLine(pen,27,8,27,33);e.Graphics.DrawLines(pen,new[]{new Point(17,25),new Point(27,35),new Point(37,25)});e.Graphics.DrawLines(pen,new[]{new Point(10,35),new Point(10,44),new Point(44,44),new Point(44,35)});}
                else if(Kind==WizardIcon.Refresh){e.Graphics.DrawArc(pen,10,10,34,34,35,285);e.Graphics.DrawLines(pen,new[]{new Point(34,7),new Point(43,12),new Point(34,18)});}
                else if(Kind==WizardIcon.Git){e.Graphics.DrawLine(pen,16,16,16,39);e.Graphics.DrawBezier(pen,16,30,16,23,38,30,38,15);foreach(var p in new[]{new Point(16,11),new Point(16,43),new Point(38,10)})e.Graphics.DrawEllipse(pen,p.X-5,p.Y-5,10,10);}
                else if(Kind==WizardIcon.Unity){e.Graphics.DrawPolygon(pen,new[]{new Point(27,7),new Point(44,17),new Point(44,37),new Point(27,47),new Point(10,37),new Point(10,17)});e.Graphics.DrawLines(pen,new[]{new Point(10,17),new Point(27,28),new Point(44,17)});e.Graphics.DrawLine(pen,27,28,27,47);}
                else if(Kind==WizardIcon.Folder){e.Graphics.DrawPolygon(pen,new[]{new Point(7,16),new Point(22,16),new Point(27,22),new Point(47,22),new Point(43,43),new Point(7,43)});e.Graphics.DrawLine(pen,8,23,45,23);}
                else if(Kind==WizardIcon.Book){e.Graphics.DrawLines(pen,new[]{new Point(7,10),new Point(27,15),new Point(47,10),new Point(47,41),new Point(27,46),new Point(7,41),new Point(7,10)});e.Graphics.DrawLine(pen,27,15,27,46);e.Graphics.DrawLine(pen,12,21,21,23);e.Graphics.DrawLine(pen,33,23,42,21);}
                else if(Kind==WizardIcon.Check){e.Graphics.DrawEllipse(pen,7,7,40,40);e.Graphics.DrawLines(pen,new[]{new Point(16,28),new Point(24,36),new Point(39,19)});}
                else if(Kind==WizardIcon.Build){e.Graphics.DrawArc(pen,25,6,23,23,35,285);e.Graphics.DrawLines(pen,new[]{new Point(31,26),new Point(10,46),new Point(5,40),new Point(26,21)});e.Graphics.DrawLine(pen,38,8,32,15);e.Graphics.DrawLine(pen,32,15,39,21);}
                else {e.Graphics.DrawArc(pen,7,18,40,28,165,210);e.Graphics.DrawLines(pen,new[]{new Point(7,32),new Point(10,43),new Point(20,34),new Point(34,34),new Point(44,43),new Point(47,32)});e.Graphics.DrawLine(pen,14,25,24,25);e.Graphics.DrawLine(pen,19,20,19,30);e.Graphics.DrawEllipse(pen,35,23,3,3);}
            }
        }
    }
    internal sealed class StepStrip : Control
    {
        internal string[] Steps; internal int Current;
        internal StepStrip(string[] steps){Steps=steps;DoubleBuffered=true;}
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;float span=Width/(float)Steps.Length;
            using(var font=new Font("Segoe UI",10,FontStyle.Bold))using(var small=new Font("Segoe UI",10))
            for(int i=0;i<Steps.Length;i++)
            {
                float x=i*span+4;Color color=i==Current?WizardUi.Orange:i<Current?WizardUi.Green:WizardUi.Muted;
                using(var brush=new SolidBrush(color))e.Graphics.FillEllipse(brush,x,11,28,28);
                TextRenderer.DrawText(e.Graphics,(i+1).ToString(),font,new Rectangle((int)x,11,28,28),Color.FromArgb(15,20,18),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
                TextRenderer.DrawText(e.Graphics,Steps[i],small,new Rectangle((int)x+40,7,(int)span-55,36),i==Current?WizardUi.Ink:WizardUi.Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);
            }
        }
    }
    internal static class WizardUi
    {
        internal static readonly Color Ground=Color.FromArgb(17,23,21),Plate=Color.FromArgb(28,37,33),Ink=Color.FromArgb(241,237,218),Muted=Color.FromArgb(161,174,162),Orange=Color.FromArgb(210,108,40),Green=Color.FromArgb(123,193,139);
        internal static Label Label(Control parent,string text,int x,int y,int width,int height,float size=11,bool bold=false,Color? color=null)
        {var l=new Label{Text=text,Left=x,Top=y,Width=width,Height=height,UseMnemonic=false,ForeColor=color??Ink,Font=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular)};parent.Controls.Add(l);return l;}
        internal static Button Button(Control parent,string text,int x,int y,int width,int height,Action click,bool primary=false)
        {
            var b=new WizardButton{Text=text,Left=x,Top=y,Width=width,Height=height,UseMnemonic=false,FlatStyle=FlatStyle.Flat,BackColor=primary?Orange:Plate,ForeColor=Ink,Font=new Font("Segoe UI",10,FontStyle.Bold),Cursor=Cursors.Hand};
            b.FlatAppearance.BorderColor=primary?Orange:Color.FromArgb(68,86,73);b.FlatAppearance.MouseOverBackColor=primary?Color.FromArgb(231,135,65):Color.FromArgb(42,57,48);
            b.EnabledChanged+=(s,e)=>{b.BackColor=b.Enabled?(primary?Orange:Plate):Color.FromArgb(36,42,38);b.ForeColor=b.Enabled?Ink:Color.FromArgb(118,128,121);b.FlatAppearance.BorderColor=b.Enabled?(primary?Orange:Color.FromArgb(68,86,73)):Color.FromArgb(51,61,54);};
            b.Click+=(s,e)=>click();parent.Controls.Add(b);return b;
        }
        internal static TextBox Text(Control parent,int x,int y,int width,int height,bool multi=false,bool readOnly=false,float size=11)
        {
            var t=new TextBox{Left=x,Top=y,Width=width,Height=height,Multiline=multi,ReadOnly=readOnly,BackColor=Plate,ForeColor=Ink,BorderStyle=BorderStyle.FixedSingle,Font=new Font("Segoe UI",size),ScrollBars=multi?ScrollBars.Vertical:ScrollBars.None};
            if(multi)t.TextChanged+=(s,e)=>{string normalized=t.Text.Replace("\r\n","\n").Replace('\r','\n').Replace("\n","\r\n");if(t.Text!=normalized)t.Text=normalized;};
            parent.Controls.Add(t);return t;
        }
        internal static void Combo(ComboBox combo){combo.BackColor=Plate;combo.ForeColor=Ink;combo.FlatStyle=FlatStyle.Flat;combo.DropDownStyle=ComboBoxStyle.DropDownList;combo.DrawMode=DrawMode.OwnerDrawFixed;combo.ItemHeight=25;combo.DrawItem+=(s,e)=>{using(var b=new SolidBrush(Plate))e.Graphics.FillRectangle(b,e.Bounds);string text=e.Index>=0?combo.Items[e.Index].ToString():combo.Text;TextRenderer.DrawText(e.Graphics,text,combo.Font,e.Bounds,Ink,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);};}
        internal static Panel Card(Control parent,WizardIcon icon,string title,string body,int y,int height=150)
        {var p=new Panel{Left=0,Top=y,Width=980,Height=height,BackColor=Plate};parent.Controls.Add(p);p.Controls.Add(new Pictogram(icon){Left=22,Top=25});Label(p,title,96,20,600,30,15,true);if(!String.IsNullOrEmpty(body))Label(p,body,96,56,600,height-65,11,false,Muted);return p;}
    }
    internal sealed class WizardButton : Button
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            using(var brush=new SolidBrush(BackColor))e.Graphics.FillRectangle(brush,ClientRectangle);
            using(var pen=new Pen(FlatAppearance.BorderColor))e.Graphics.DrawRectangle(pen,0,0,Width-1,Height-1);
            TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Enabled?ForeColor:Color.FromArgb(153,163,155),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
            if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(4,4,Width-8,Height-8));
        }
    }
    public class WizardForm : Form
    {
        protected readonly Panel Body;protected readonly Button Back,Next;protected readonly Label Footer,Subtitle;protected readonly Panel[] Pages;readonly StepStrip strip;
        protected int Page;protected bool Busy;
        protected WizardForm(string title,string subtitle,string[] steps)
        {
            Text="OpenZ-VK · "+title;ClientSize=new Size(1040,850);MinimumSize=MaximumSize=Size;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;BackColor=WizardUi.Ground;ForeColor=WizardUi.Ink;Font=new Font("Segoe UI",11);AutoScaleMode=AutoScaleMode.Dpi;
            var picture=new PictureBox{Left=30,Top=15,Width=300,Height=110,SizeMode=PictureBoxSizeMode.Zoom};using(var r=Assembly.GetExecutingAssembly().GetManifestResourceStream("OpenZVK.Logo"))using(var image=Image.FromStream(r))picture.Image=new Bitmap(image);Controls.Add(picture);
            WizardUi.Label(this,title,370,29,640,42,25,true);Subtitle=WizardUi.Label(this,subtitle,372,80,638,46,11,false,WizardUi.Muted);
            strip=new StepStrip(steps){Left=30,Top=145,Width=980,Height=50};Controls.Add(strip);
            Body=new Panel{Left=30,Top=215,Width=980,Height=525};Controls.Add(Body);Pages=new Panel[steps.Length];for(int i=0;i<Pages.Length;i++){Pages[i]=new Panel{Dock=DockStyle.Fill};Body.Controls.Add(Pages[i]);}
            Footer=WizardUi.Label(this,"",30,746,980,26,10,false,WizardUi.Muted);
            Back=WizardUi.Button(this,"Back",30,790,140,42,()=>OnBack());Next=WizardUi.Button(this,"Continue",780,790,230,42,()=>OnNext(),true);ShowPage(0);
        }
        protected virtual void OnBack(){if(!Busy&&Page>0)ShowPage(Page-1);}
        protected virtual void OnNext(){}
        protected void ShowPage(int page){Page=page;for(int i=0;i<Pages.Length;i++)Pages[i].Visible=i==page;Pages[page].BringToFront();strip.Current=page;strip.Invalidate();Back.Visible=page>0;RefreshNavigation();}
        protected virtual void RefreshNavigation(){Back.Enabled=!Busy;Next.Enabled=!Busy;}
        protected void AddIcon(Control parent,WizardIcon icon,int x,int y){parent.Controls.Add(new Pictogram(icon){Left=x,Top=y});}
    }
}
