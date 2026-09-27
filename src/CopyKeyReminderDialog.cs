using System;
using System.Drawing;
using System.Windows.Forms;

namespace ScumMiniMap {
    internal sealed class CopyKeyReminderDialog : Form {
        // Same order as AppLanguage. Keep the reminder usable before Settings opens.
        static readonly string[][] Lines={
            new[]{"Check coordinate-copy key","The default is Num / (numpad divide). Set Copy location in SCUM to the same key. If you use a different key or have no numpad, capture it in MiniMap Settings.","Do not show again","OK"},
            new[]{"Comprobar tecla de coordenadas","La tecla predeterminada es Num / (división del teclado numérico). Asigná Copiar ubicación en SCUM a la misma tecla. Si usás otra tecla o no tenés teclado numérico, capturala en los ajustes de MiniMap.","No volver a mostrar","Aceptar"},
            new[]{"Vérifier la touche des coordonnées","La touche par défaut est Num / (division du pavé numérique). Affectez Copier la position dans SCUM à la même touche. Si vous utilisez une autre touche ou n’avez pas de pavé numérique, capturez-la dans les paramètres de MiniMap.","Ne plus afficher","OK"},
            new[]{"Koordinaten-Kopiertaste prüfen","Standard ist Num / (Division auf dem Ziffernblock). Legen Sie in SCUM dieselbe Taste für Position kopieren fest. Wenn Sie eine andere Taste verwenden oder keinen Ziffernblock haben, erfassen Sie sie in den MiniMap-Einstellungen.","Nicht erneut anzeigen","OK"},
            new[]{"Toets voor coördinaten controleren","Standaard is Num / (delen op het numerieke toetsenblok). Stel in SCUM dezelfde toets in voor Locatie kopiëren. Gebruik je een andere toets of heb je geen numeriek toetsenblok, leg die dan vast in de MiniMap-instellingen.","Niet meer tonen","OK"},
            new[]{"Проверьте клавишу копирования координат","По умолчанию используется Num / (деление на цифровом блоке). Назначьте эту же клавишу для копирования координат в SCUM. Если используете другую клавишу или у вас нет цифрового блока, задайте ее в настройках MiniMap.","Больше не показывать","ОК"},
            new[]{"检查坐标复制按键","默认按键为 Num /（数字键盘除号）。请在 SCUM 中将复制位置设为同一按键。如果您使用其他按键或键盘没有数字键盘，请在 MiniMap 设置中捕获该按键。","不再显示","确定"},
            new[]{"Koordinat kopyalama tuşunu kontrol edin","Varsayılan tuş Num / (sayısal tuş takımında bölme). SCUM'da Konumu kopyala işlevini aynı tuşa atayın. Farklı bir tuş kullanıyorsanız veya sayısal tuş takımınız yoksa tuşu MiniMap Ayarları'nda yakalayın.","Bir daha gösterme","Tamam"},
            new[]{"تحقق من مفتاح نسخ الإحداثيات","المفتاح الافتراضي هو Num / (القسمة على لوحة الأرقام). عيّن المفتاح نفسه لنسخ الموقع في SCUM. إذا كنت تستخدم مفتاحًا آخر أو لا توجد لوحة أرقام، فالتقطه في إعدادات MiniMap.","عدم الإظهار","موافق"},
            new[]{"Verificar tecla de coordenadas","A tecla padrão é Num / (divisão do teclado numérico). Atribua Copiar localização à mesma tecla no SCUM. Se usar outra tecla ou não tiver teclado numérico, capture-a nas configurações do MiniMap.","Não mostrar novamente","OK"}
        };
        static string Line(int index) { return Lines[(int)Localization.Current][index]; }
        readonly CheckBox doNotShowAgain;
        internal bool DoNotShowAgain { get { return doNotShowAgain.Checked; } }

        internal CopyKeyReminderDialog() {
            Text=Line(0);
            ClientSize=new Size(700,250);
            MinimumSize=new Size(620,250);
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
                AutoSize=false,Location=new Point(24,24),Size=new Size(652,118),
                TextAlign=rightToLeft?ContentAlignment.MiddleRight:ContentAlignment.MiddleLeft
            };
            var keyLabel=new Label {
                Text="Num /",
                AutoSize=false,Location=new Point(24,148),Size=new Size(150,44),
                TextAlign=ContentAlignment.MiddleCenter,BackColor=Color.FromArgb(40,48,56),
                ForeColor=Color.FromArgb(255,184,77),Font=new Font("Segoe UI",16f,FontStyle.Bold)
            };
            doNotShowAgain=new CheckBox {
                Text=Line(2),
                AutoSize=false,Location=new Point(24,202),Size=new Size(520,28),
                ForeColor=ForeColor
            };
            var okay=new Button {
                Text=Line(3),
                DialogResult=DialogResult.OK,Location=new Point(554,200),Size=new Size(122,32)
            };
            Controls.Add(message); Controls.Add(keyLabel); Controls.Add(doNotShowAgain); Controls.Add(okay);
            AcceptButton=okay;
        }
    }
}
