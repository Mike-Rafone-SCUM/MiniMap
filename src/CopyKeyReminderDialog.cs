using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace ScumMiniMap {
    internal sealed class CopyKeyReminderDialog : Form {
        // Same order as AppLanguage. Keep the reminder usable before Settings opens.
        static readonly string[][] Lines={
            new[]{"Check coordinate-copy key","Please check that Copy Coordinates in SCUM is set to \\ (backslash). If you already use a different key, set the same key in MiniMap Settings.","Do not show again","OK"},
            new[]{"Comprobar tecla de coordenadas","Comprobá que la tecla para copiar coordenadas en SCUM sea \\ (barra invertida). Si ya usás otra tecla, configurá esa misma tecla en los ajustes de MiniMap.","No volver a mostrar","Aceptar"},
            new[]{"Vérifier la touche des coordonnées","Vérifiez que la touche de copie des coordonnées dans SCUM est \\ (barre oblique inverse). Si vous utilisez une autre touche, choisissez la même dans les paramètres de MiniMap.","Ne plus afficher","OK"},
            new[]{"Koordinaten-Kopiertaste prüfen","Prüfen Sie, ob die Taste zum Kopieren der Koordinaten in SCUM auf \\ (Backslash) eingestellt ist. Wenn Sie eine andere Taste verwenden, stellen Sie dieselbe Taste in den MiniMap-Einstellungen ein.","Nicht erneut anzeigen","OK"},
            new[]{"Toets voor coördinaten controleren","Controleer of de toets voor het kopiëren van coördinaten in SCUM op \\ (backslash) staat. Gebruikt u een andere toets, stel dan dezelfde toets in bij de MiniMap-instellingen.","Niet meer tonen","OK"},
            new[]{"Проверьте клавишу копирования координат","Убедитесь, что в SCUM для копирования координат назначена клавиша \\ (обратная косая черта). Если вы используете другую клавишу, назначьте её же в настройках MiniMap.","Больше не показывать","ОК"},
            new[]{"检查坐标复制按键","请确认 SCUM 中的复制坐标按键设置为 \\（反斜杠）。如果您已使用其他按键，请在 MiniMap 设置中使用相同按键。","不再显示","确定"},
            new[]{"Koordinat kopyalama tuşunu kontrol edin","SCUM'da koordinat kopyalama tuşunun \\ (ters eğik çizgi) olarak ayarlandığını kontrol edin. Başka bir tuş kullanıyorsanız MiniMap ayarlarında da aynı tuşu seçin.","Bir daha gösterme","Tamam"},
            new[]{"تحقق من مفتاح نسخ الإحداثيات","تأكد من ضبط مفتاح نسخ الإحداثيات في SCUM على \\ (الشرطة المائلة العكسية). إذا كنت تستخدم مفتاحًا آخر، فاضبط المفتاح نفسه في إعدادات MiniMap.","عدم الإظهار مرة أخرى","موافق"},
            new[]{"Verificar tecla de coordenadas","Confira se a tecla de copiar coordenadas no SCUM está definida como \\ (barra invertida). Se você já usa outra tecla, defina a mesma tecla nas configurações do MiniMap.","Não mostrar novamente","OK"}
        };
        static string Line(int index) { return Lines[(int)Localization.Current][index]; }
        static Image LoadKeyReference() {
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("copy-location-key.jpg")) {
                if(stream==null) return null;
                using(var decoded=Image.FromStream(stream)) return new Bitmap(decoded);
            }
        }
        readonly CheckBox doNotShowAgain;
        internal bool DoNotShowAgain { get { return doNotShowAgain.Checked; } }

        internal CopyKeyReminderDialog() {
            Text=Line(0);
            ClientSize=new Size(700,570);
            MinimumSize=new Size(620,570);
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
                Text=Line(1),
                AutoSize=false,Location=new Point(24,24),Size=new Size(652,92),
                TextAlign=rightToLeft?ContentAlignment.MiddleRight:ContentAlignment.MiddleLeft
            };
            var keyReference=LoadKeyReference();
            if(keyReference!=null) {
                var picture=new PictureBox {
                    Image=keyReference, SizeMode=PictureBoxSizeMode.Zoom,
                    Location=new Point(24,124), Size=new Size(652,390),
                    BorderStyle=BorderStyle.FixedSingle, BackColor=Color.Black
                };
                Controls.Add(picture);
            }
            doNotShowAgain=new CheckBox {
                Text=Line(2),
                AutoSize=false,Location=new Point(24,522),Size=new Size(520,32),
                ForeColor=ForeColor
            };
            var okay=new Button {
                Text=Line(3),
                DialogResult=DialogResult.OK,Location=new Point(554,522),Size=new Size(122,32)
            };
            Controls.Add(message); Controls.Add(doNotShowAgain); Controls.Add(okay);
            AcceptButton=okay;
        }
    }
}
