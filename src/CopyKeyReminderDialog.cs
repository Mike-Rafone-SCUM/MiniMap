using System;
using System.Drawing;
using System.Windows.Forms;

namespace ScumMiniMap {
    internal static class CopyKeyIllustration {
        internal static PictureBox Create(int width, int height) {
            using (var stream=typeof(CopyKeyIllustration).Assembly.GetManifestResourceStream("keyboard-setup.png")) {
                if(stream==null) return null;
                using(var original=Image.FromStream(stream)) {
                    var picture=new PictureBox { Width=width,Height=height,SizeMode=PictureBoxSizeMode.Zoom,
                        Image=new Bitmap(original),BackColor=Color.White,Margin=new Padding(0,0,0,14),TabStop=false };
                    picture.Disposed+=(s,e)=>picture.Image.Dispose();
                    return picture;
                }
            }
        }
    }
    internal sealed class CopyKeyReminderDialog : Form {

        readonly CheckBox doNotShowAgain;
        internal bool DoNotShowAgain { get { return doNotShowAgain.Checked; } }

        internal CopyKeyReminderDialog() {
            Text=Localization.Get("CopyReminderTitle");
            ClientSize=new Size(700,490);
            MinimumSize=new Size(700,490);
            StartPosition=FormStartPosition.CenterScreen;
            FormBorderStyle=FormBorderStyle.FixedDialog;
            MaximizeBox=false; MinimizeBox=false; ShowIcon=false;
            TopMost=true;
            BackColor=Color.FromArgb(20,24,28);
            ForeColor=Color.FromArgb(243,239,230);
            Font=new Font("Segoe UI",10f);
            bool rightToLeft=Localization.Current==AppLanguage.Arabic;
            RightToLeft=rightToLeft?RightToLeft.Yes:RightToLeft.No;

            var message=new Label {
                Text=Localization.Get("CopyReminderBody"),
                AutoSize=false,Location=new Point(24,24),Size=new Size(652,118),
                TextAlign=rightToLeft?ContentAlignment.MiddleRight:ContentAlignment.MiddleLeft
            };
            var keyLabel=new Label {
                Text="Num /",
                AutoSize=false,Location=new Point(24,148),Size=new Size(150,44),
                TextAlign=ContentAlignment.MiddleCenter,BackColor=Color.FromArgb(40,48,56),
                ForeColor=Color.FromArgb(255,184,77),Font=new Font("Segoe UI",16f,FontStyle.Bold)
            };
            var illustration=CopyKeyIllustration.Create(652,235);
            if(illustration!=null) { illustration.Location=new Point(24,198); Controls.Add(illustration); }
            doNotShowAgain=new CheckBox {
                Text=Localization.Get("CopyReminderHide"),
                AutoSize=false,Location=new Point(24,448),Size=new Size(520,28),
                ForeColor=ForeColor
            };
            var okay=new Button {
                Text=Localization.Get("CopyReminderOkay"),
                DialogResult=DialogResult.OK,Location=new Point(554,446),Size=new Size(122,32)
            };
            Controls.Add(message); Controls.Add(keyLabel); Controls.Add(doNotShowAgain); Controls.Add(okay);
            AcceptButton=okay;
        }
    }
}
