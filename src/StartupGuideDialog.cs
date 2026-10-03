using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ScumMiniMap {
    public delegate void KeybindSaveHandler(int mapKey, int chatKey, int copyModKey, int copyKey, int copyScanCode, int mapScanCode, int chatScanCode);

    public sealed class StartupGuideDialog : Form {
        readonly KeybindSaveHandler onSaveKeybinds;
        readonly Action onSettingsChanged;
        readonly Action<AppControlBindings> onSaveAppControls;

        int currentSlide = 0;
        bool bindingEditorMode;
        public bool LayoutRequested { get; private set; }
        const int TotalSlides = 5;

        // Keybind calibration state
        int mapKey = 0x4D;       // 'M'
        int chatKey = 0x54;      // 'T'
        int mapScanCode,chatScanCode;
        AppControlBindings appControls;
        int copyModKey = Program.DefaultCopyModifierKey;
        int copyKey = Program.DefaultCopyKey;
        int copyScanCode;
        int captureTarget = 0;   // 0=none, 1=map, 2=chat, 3=mod, 4=copy, 10+=app action

        Panel headerPanel;
        Label titleLabel;
        Label subtitleLabel;
        ComboBox langCombo;
        FlowLayoutPanel tabStrip;
        Button[] tabButtons;
        Panel contentPanel;
        Panel footerPanel;
        Button prevButton;
        Button nextButton;
        Button skipButton;
        Button finishButton;
        Label pageIndicator;
        bool updatingLanguage;

        // Keybind segment UI refs
        Label mapValLabel, chatValLabel, modValLabel, copyValLabel, captureStatusLabel;
        Button mapCapBtn, chatCapBtn, modCapBtn, copyCapBtn;
        CheckBox chkNoMod;
        readonly Label[] appValLabels = new Label[AppControlBindings.AllActions.Length];
        readonly Button[] appCapButtons = new Button[AppControlBindings.AllActions.Length];

        // Tactical survival color palette (Gunmetal, Steel, Survival Amber/Hazard)
        static readonly Color BgDark = Color.FromArgb(14, 17, 20);
        static readonly Color HeaderBg = Color.FromArgb(20, 24, 28);
        static readonly Color CardBg = Color.FromArgb(22, 27, 32);
        static readonly Color CardBorder = Color.FromArgb(48, 56, 66);
        static readonly Color AmberAccent = Color.FromArgb(245, 158, 11);
        static readonly Color AmberBright = Color.FromArgb(255, 184, 77);
        static readonly Color TextWhite = Color.FromArgb(243, 239, 230);
        static readonly Color TextMuted = Color.FromArgb(168, 178, 188);

        public StartupGuideDialog() : this(null) {}
        public StartupGuideDialog(Action onSettingsChanged) : this(0x4D, 0x54, Program.DefaultCopyModifierKey, Program.DefaultCopyKey, null, onSettingsChanged) {}

        public StartupGuideDialog(int mapKey, int chatKey, int copyModKey, int copyKey,
                                  KeybindSaveHandler onSaveKeybinds, Action onSettingsChanged = null, int copyScanCode = 0, int mapScanCode = 0, int chatScanCode = 0, int settingsKey = 0x24, int waypointKey = 0x2D, int searchKey = 0x2E, int settingsScan = 0, int waypointScan = 0, int searchScan = 0, AppControlBindings appControls = null, Action<AppControlBindings> onSaveAppControls = null) {
            this.appControls = appControls != null ? appControls.Clone() : new AppControlBindings();
            if(appControls == null) {
                this.appControls.Set(AppControlAction.Settings, settingsKey, settingsScan);
                this.appControls.Set(AppControlAction.AddWaypoint, waypointKey, waypointScan);
                this.appControls.Set(AppControlAction.Search, searchKey, searchScan);
            }
            this.mapKey = mapKey > 0 ? mapKey : 0x4D;
            this.chatKey = chatKey > 0 ? chatKey : 0x54;
            this.copyModKey = (copyModKey == 0 || PhysicalKeyCapture.IsModifierKey(copyModKey)) ? copyModKey : Program.DefaultCopyModifierKey;
            this.copyKey = copyKey > 0 && copyKey < 256 && !PhysicalKeyCapture.IsModifierKey(copyKey) ? copyKey : Program.DefaultCopyKey;
            this.copyScanCode = PhysicalKeyCapture.Valid(copyScanCode) ? copyScanCode : 0;
            this.mapScanCode = PhysicalKeyCapture.Valid(mapScanCode) ? mapScanCode : 0;
            this.chatScanCode = PhysicalKeyCapture.Valid(chatScanCode) ? chatScanCode : 0;
            this.onSaveKeybinds = onSaveKeybinds;
            this.onSettingsChanged = onSettingsChanged;
            this.onSaveAppControls = onSaveAppControls;

            Text = Localization.Get("WizardTitle");
            ClientSize = new Size(920, 640);
            MinimumSize = new Size(840, 580);
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            KeyPreview = true;
            BackColor = BgDark;
            ForeColor = TextWhite;
            Font = new Font("Segoe UI", 9f);
            ShowIcon = false;
            DoubleBuffered = true;

            KeyDown += HandleFormKeyDown;

            BuildLayout();
            UpdateSlide();
        }

        void HandleFormKeyDown(object sender, KeyEventArgs e) {
            if (captureTarget == 0) return;
            e.Handled = true;
            e.SuppressKeyPress = true;

            int key = (int)e.KeyCode;
            if (key == 0x1B) { // Escape cancels capture
                captureTarget = 0;
                UpdateKeybindValues();
                if (captureStatusLabel != null) captureStatusLabel.Text = Localization.Get("KeyWizardReady");
                e.Handled = true;
                return;
            }

            if(captureTarget != 3 && (PhysicalKeyCapture.IsModifierKey(key) || e.Modifiers != Keys.None)) {
                if(captureStatusLabel!=null) captureStatusLabel.Text=Localization.Get("ShortcutSingleKey");
                return;
            }
            if((captureTarget == 1 || captureTarget == 2 || captureTarget == 4) && !ValidGameKey(key)) {
                if(captureStatusLabel!=null) captureStatusLabel.Text=Localization.Get("ShortcutReserved");
                return;
            }

            if(captureTarget >= 10) {
                if(!AppControlBindings.ValidKey(key)) {
                    if(captureStatusLabel!=null) captureStatusLabel.Text=Localization.Get("ShortcutReserved");
                    return;
                }
                int scan;
                if(!PhysicalKeyCapture.TryGet(key,out scan)) {
                    if(captureStatusLabel!=null) captureStatusLabel.Text=Localization.Get("ShortcutRetry");
                    return;
                }
                appControls.Set((AppControlAction)(captureTarget - 10), key, scan);
                captureTarget = 0;
                UpdateKeybindValues();
                if(captureStatusLabel!=null) captureStatusLabel.Text=Localization.Get("KeyWizardReady");
                return;
            }

            int capturedScan;
            switch (captureTarget) {
                case 1:
                    if(!PhysicalKeyCapture.TryGet(key,out capturedScan)) {
                        if(captureStatusLabel!=null) captureStatusLabel.Text=Localization.Get("KeyWizardPressMap");
                        e.Handled=true; e.SuppressKeyPress=true; return;
                    }
                    mapKey = key;
                    mapScanCode = capturedScan;
                    break;
                case 2:
                    if(!PhysicalKeyCapture.TryGet(key,out capturedScan)) {
                        if(captureStatusLabel!=null) captureStatusLabel.Text=Localization.Get("KeyWizardPressChat");
                        e.Handled=true; e.SuppressKeyPress=true; return;
                    }
                    chatKey = key;
                    chatScanCode = capturedScan;
                    break;
                case 3:
                    if (key == 0x08 || key == 0x2E) {
                        copyModKey = 0;
                    } else if (PhysicalKeyCapture.IsModifierKey(key)) {
                        copyModKey = key;
                    } else {
                        if(captureStatusLabel!=null) captureStatusLabel.Text=Localization.Get("WizardPressKeyPrompt");
                        return;
                    }
                    break;
                case 4:
                    if (!PhysicalKeyCapture.IsModifierKey(key)) {
                        int scan;
                        if (!PhysicalKeyCapture.TryGet(key,out scan)) {
                            if (captureStatusLabel != null) captureStatusLabel.Text = Localization.Get("WizardPressKeyPrompt");
                            e.Handled = true; e.SuppressKeyPress = true;
                            return;
                        }
                        copyKey = key;
                        copyScanCode = scan;
                    }
                    break;
            }

            captureTarget = 0;
            UpdateKeybindValues();
            if (captureStatusLabel != null) captureStatusLabel.Text = Localization.Get("KeyWizardReady");
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData) {
            if(captureTarget != 0) {
                HandleFormKeyDown(this, new KeyEventArgs(keyData));
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }


        void BuildLayout() {
            // Header: Tactical Gunmetal Panel with Survival Amber Accent Line
            headerPanel = new Panel {
                Dock = DockStyle.Top,
                Height = 84,
                BackColor = HeaderBg,
                Padding = new Padding(24, 14, 24, 10)
            };
            headerPanel.Paint += (s, e) => {
                using (Pen borderPen = new Pen(Color.FromArgb(44, 52, 62))) {
                    e.Graphics.DrawLine(borderPen, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
                }
                using (Pen amberPen = new Pen(AmberAccent, 2f)) {
                    e.Graphics.DrawLine(amberPen, 0, 0, headerPanel.Width, 0);
                }
            };

            titleLabel = new Label {
                Text = Localization.Get("WizardBranding"),
                Font = new Font("Consolas", 14f, FontStyle.Bold),
                ForeColor = AmberAccent,
                AutoSize = true,
                Location = new Point(24, 14)
            };
            headerPanel.Controls.Add(titleLabel);

            subtitleLabel = new Label {
                Text = Localization.Get("WizardTagline"),
                Font = new Font("Segoe UI", 9f),
                ForeColor = TextMuted,
                AutoSize = true,
                Location = new Point(26, 44)
            };
            headerPanel.Controls.Add(subtitleLabel);

            // Language Selector: Industrial Dropdown
            langCombo = new ComboBox {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 150,
                Height = 28,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(headerPanel.Width - 174, 26),
                BackColor = Color.FromArgb(28, 34, 40),
                ForeColor = TextWhite,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            langCombo.Items.AddRange(new object[] {
                "English (US)",
                "Español (AR)",
                "Français",
                "Deutsch",
                "Nederlands",
                "Русский",
                "中文 (简体)",
                "Türkçe",
                "العربية",
                "Português (BR)"
            });
            switch (Localization.Current) {
                case AppLanguage.SpanishArgentina: langCombo.SelectedIndex = 1; break;
                case AppLanguage.French: langCombo.SelectedIndex = 2; break;
                case AppLanguage.German: langCombo.SelectedIndex = 3; break;
                case AppLanguage.Dutch: langCombo.SelectedIndex = 4; break;
                case AppLanguage.Russian: langCombo.SelectedIndex = 5; break;
                case AppLanguage.Chinese: langCombo.SelectedIndex = 6; break;
                case AppLanguage.Turkish: langCombo.SelectedIndex = 7; break;
                case AppLanguage.Arabic: langCombo.SelectedIndex = 8; break;
                case AppLanguage.Portuguese: langCombo.SelectedIndex = 9; break;
                default: langCombo.SelectedIndex = 0; break;
            }
            langCombo.SelectedIndexChanged += (s, e) => {
                if (updatingLanguage) return;
                AppLanguage chosen;
                switch (langCombo.SelectedIndex) {
                    case 1: chosen = AppLanguage.SpanishArgentina; break;
                    case 2: chosen = AppLanguage.French; break;
                    case 3: chosen = AppLanguage.German; break;
                    case 4: chosen = AppLanguage.Dutch; break;
                    case 5: chosen = AppLanguage.Russian; break;
                    case 6: chosen = AppLanguage.Chinese; break;
                    case 7: chosen = AppLanguage.Turkish; break;
                    case 8: chosen = AppLanguage.Arabic; break;
                    case 9: chosen = AppLanguage.Portuguese; break;
                    default: chosen = AppLanguage.English; break;
                }
                if (Localization.Current != chosen) {
                    Localization.Current = chosen;
                    RefreshTranslations();
                    if (onSettingsChanged != null) onSettingsChanged();
                }
            };
            headerPanel.Controls.Add(langCombo);

            // Tab Strip: Square Mechanical Indicator Strip
            tabStrip = new FlowLayoutPanel {
                Dock = DockStyle.Top,
                Height = 42,
                BackColor = Color.FromArgb(16, 20, 24),
                Padding = new Padding(24, 5, 24, 0)
            };
            tabStrip.Paint += (s, e) => {
                using (Pen borderPen = new Pen(Color.FromArgb(36, 44, 52))) {
                    e.Graphics.DrawLine(borderPen, 0, tabStrip.Height - 1, tabStrip.Width, tabStrip.Height - 1);
                }
            };

            string[] tabKeys = new string[] {
                "WizardTabOverview",
                "WizardTabControls",
                "WizardTabKeybinds",
                "WizardTabSafety",
                "WizardTabFeatures"
            };

            tabButtons = new Button[TotalSlides];
            for (int i = 0; i < TotalSlides; i++) {
                int index = i;
                Button tabBtn = new Button {
                    Text = Localization.Get(tabKeys[i]),
                    Width = 162,
                    Height = 32,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.Transparent,
                    ForeColor = TextMuted,
                    Font = new Font("Consolas", 8.8f, FontStyle.Bold),
                    Cursor = Cursors.Hand,
                    Margin = new Padding(0, 0, 8, 0)
                };
                tabBtn.FlatAppearance.BorderSize = 0;
                tabBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, 38, 46);
                tabBtn.Click += (s, e) => {
                    captureTarget = 0;
                    currentSlide = index;
                    UpdateSlide();
                };
                tabButtons[i] = tabBtn;
                tabStrip.Controls.Add(tabBtn);
            }

            // Footer: Sharp Industrial Action Bar
            footerPanel = new Panel {
                Dock = DockStyle.Bottom,
                Height = 96,
                BackColor = HeaderBg,
                Padding = new Padding(24, 12, 24, 12)
            };
            footerPanel.Paint += (s, e) => {
                using (Pen borderPen = new Pen(Color.FromArgb(44, 52, 62))) {
                    e.Graphics.DrawLine(borderPen, 0, 0, footerPanel.Width, 0);
                }
            };

            pageIndicator = new Label {
                AutoSize = true,
                Location = new Point(24, 22),
                ForeColor = TextMuted,
                Font = new Font("Consolas", 9f, FontStyle.Bold)
            };
            footerPanel.Controls.Add(pageIndicator);

            // Previous Button
            prevButton = new Button {
                Text = Localization.Get("WizardBtnPrev"),
                Width = 110,
                Height = 36,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(footerPanel.Width - 460, 13),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(28, 34, 40),
                ForeColor = TextWhite,
                Cursor = Cursors.Hand,
                Font = new Font("Consolas", 9f, FontStyle.Bold)
            };
            prevButton.FlatAppearance.BorderColor = CardBorder;
            prevButton.Click += (s, e) => {
                captureTarget = 0;
                if (currentSlide > 0) {
                    currentSlide--;
                    UpdateSlide();
                }
            };
            footerPanel.Controls.Add(prevButton);

            // Skip Button (Featured prominently on Keybinds step or any step)
            skipButton = new Button {
                Text = Localization.Get("WizardBtnSkip"),
                Width = 130,
                Height = 36,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(footerPanel.Width - 340, 13),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(22, 26, 30),
                ForeColor = AmberBright,
                Cursor = Cursors.Hand,
                Font = new Font("Consolas", 9f, FontStyle.Bold)
            };
            skipButton.FlatAppearance.BorderColor = AmberAccent;
            skipButton.Click += (s, e) => {
                // Skips without capturing or changing keys
                captureTarget = 0;
                if (currentSlide < TotalSlides - 1) {
                    currentSlide++;
                    UpdateSlide();
                } else {
                    FinishWizard();
                }
            };
            footerPanel.Controls.Add(skipButton);

            // Next Button
            nextButton = new Button {
                Text = Localization.Get("WizardBtnNext"),
                Width = 110,
                Height = 36,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(footerPanel.Width - 200, 13),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(44, 52, 62),
                ForeColor = TextWhite,
                Cursor = Cursors.Hand,
                Font = new Font("Consolas", 9f, FontStyle.Bold)
            };
            nextButton.FlatAppearance.BorderColor = Color.FromArgb(70, 82, 96);
            nextButton.Click += (s, e) => {
                captureTarget = 0;
                if (currentSlide < TotalSlides - 1) {
                    currentSlide++;
                    UpdateSlide();
                }
            };
            footerPanel.Controls.Add(nextButton);

            // Finish Button: Tactical Amber Block
            finishButton = new Button {
                Text = Localization.Get("WizardBtnFinish"),
                Width = 160,
                Height = 36,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(footerPanel.Width - 180, 13),
                FlatStyle = FlatStyle.Flat,
                BackColor = AmberAccent,
                ForeColor = Color.Black,
                Cursor = Cursors.Hand,
                Font = new Font("Consolas", 9.5f, FontStyle.Bold)
            };
            finishButton.FlatAppearance.BorderColor = Color.FromArgb(217, 119, 6);
            finishButton.Click += (s, e) => FinishWizard();
            footerPanel.Controls.Add(finishButton);

            captureStatusLabel = new Label {
                Name = "KeybindCaptureStatus",
                Text = Localization.Get("KeyWizardReady"),
                Font = new Font("Consolas", 9.5f, FontStyle.Bold),
                ForeColor = AmberBright,
                AutoSize = false,
                Width = Math.Max(1, footerPanel.Width - 48),
                Height = 34,
                Location = new Point(24, 56),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                TextAlign = ContentAlignment.TopLeft
            };
            footerPanel.Controls.Add(captureStatusLabel);

            // Content Panel: Dark Matte Charcoal
            contentPanel = new Panel {
                Dock = DockStyle.Fill,
                BackColor = BgDark,
                Padding = new Padding(30, 20, 30, 20),
                AutoScroll = true
            };

            Controls.Add(contentPanel);
            Controls.Add(tabStrip);
            Controls.Add(headerPanel);
            Controls.Add(footerPanel);
        }

        void FinishWizard(bool layoutRequested = false) {
            if(!CommitKeybindsIfApplicable(true)) return;
            LayoutRequested = layoutRequested;
            DialogResult = DialogResult.OK;
            Close();
        }

        bool BindingsConflict() {
            return GameBindingsConflict()
                || appControls.BindingError(mapKey,mapScanCode,chatKey,chatScanCode,copyKey,copyScanCode) != null;
        }

        bool GameBindingsConflict() {
            return PhysicalKeyCapture.HasConflicts(
                new[]{mapKey,chatKey,copyKey},
                new[]{mapScanCode,chatScanCode,copyScanCode});
        }

        bool CommitKeybindsIfApplicable(bool requireValid=false) {
            bool validGameKeys = ValidGameKey(mapKey) && ValidGameKey(chatKey) && ValidGameKey(copyKey)
                && (copyModKey == 0 || PhysicalKeyCapture.IsModifierKey(copyModKey));
            string error = !validGameKeys ? Localization.Get("KeyWizardNonModifier")
                : GameBindingsConflict() ? Localization.Get("KeyWizardDifferentKeys")
                : appControls.BindingError(mapKey,mapScanCode,chatKey,chatScanCode,copyKey,copyScanCode);
            if(error != null) {
                if(requireValid) MessageBox.Show(this,error,Localization.Get("KeyWizardTitle"),MessageBoxButtons.OK,MessageBoxIcon.Warning);
                return false;
            }
            if (onSaveKeybinds != null) {
                onSaveKeybinds(mapKey, chatKey, copyModKey, copyKey, copyScanCode, mapScanCode, chatScanCode);
            }
            if(onSaveAppControls != null) onSaveAppControls(appControls.Clone());
            return true;
        }

        static bool ValidGameKey(int key) {
            return key > 0 && key < 256 && !PhysicalKeyCapture.IsModifierKey(key)
                && key != 0x1B && key != 0x0D && key != 0x09;
        }

        internal void ShowKeyBindings() {
            captureTarget = 0;
            bindingEditorMode = true;
            currentSlide = 2;
            UpdateSlide();
        }

        void UpdateSlide() {
            captureTarget = 0;
            // Update tab button highlights
            for (int i = 0; i < TotalSlides; i++) {
                if (i == currentSlide) {
                    tabButtons[i].BackColor = Color.FromArgb(28, 34, 42);
                    tabButtons[i].ForeColor = AmberAccent;
                } else {
                    tabButtons[i].BackColor = Color.Transparent;
                    tabButtons[i].ForeColor = TextMuted;
                }
            }

            pageIndicator.Text = bindingEditorMode ? "" : string.Format(Localization.Get("WizardStepIndicator"), currentSlide + 1, TotalSlides);
            prevButton.Enabled = currentSlide > 0;
            prevButton.Visible = !bindingEditorMode;
            nextButton.Visible = !bindingEditorMode && currentSlide < TotalSlides - 1;
            finishButton.Visible = bindingEditorMode || currentSlide == TotalSlides - 1;
            finishButton.Text = Localization.Get(bindingEditorMode ? "KeyWizardSave" : "WizardBtnFinish");
            tabStrip.Visible = !bindingEditorMode;

            // Highlight skip button on Keybinds step (Slide 2)
            skipButton.Visible = !bindingEditorMode && (currentSlide == 2 || currentSlide < TotalSlides - 1);
            footerPanel.Height = currentSlide == 2 ? 96 : 62;
            captureStatusLabel.Visible = currentSlide == 2;
            captureStatusLabel.Text = Localization.Get("KeyWizardReady");

            contentPanel.SuspendLayout();
            while(contentPanel.Controls.Count>0) contentPanel.Controls[0].Dispose();
            mapValLabel = chatValLabel = modValLabel = copyValLabel = null;
            mapCapBtn = chatCapBtn = modCapBtn = copyCapBtn = null;
            chkNoMod = null;
            Array.Clear(appValLabels, 0, appValLabels.Length);
            Array.Clear(appCapButtons, 0, appCapButtons.Length);

            switch (currentSlide) {
                case 0: RenderSlideOverview(); break;
                case 1: RenderSlideControls(); break;
                case 2: RenderSlideKeybinds(); break;
                case 3: RenderSlideSafety(); break;
                case 4: RenderSlideFeatures(); break;
            }

            contentPanel.ResumeLayout(true);
        }

        void RenderSlideOverview() {
            FlowLayoutPanel flow = CreateContentFlow();

            AddTacticalCard(flow, Localization.Get("WizardLayoutTitle"), Localization.Get("WizardLayoutDescription"));
            Button layoutButton = new Button {
                Text=Localization.Get("WizardAdjustLayout"), AutoSize=true, MinimumSize=new Size(280,36),
                FlatStyle=FlatStyle.Flat, BackColor=AmberAccent, ForeColor=Color.Black,
                Margin=new Padding(0,0,0,14), Cursor=Cursors.Hand
            };
            layoutButton.Click+=(s,e)=> FinishWizard(true);
            flow.Controls.Add(layoutButton);

            AddTacticalCard(flow, "[ SYS // 01 ] " + Localization.Get("WzOvTitle1"), Localization.Get("WzOvDesc1"));
            AddTacticalCard(flow, "[ DISP // 02 ] " + Localization.Get("WzOvTitle2"), Localization.Get("WzOvDesc2"));
            AddTacticalCard(flow, "[ NAV // 04 ] " + Localization.Get("WzOvTitle4"), Localization.Get("WzOvDesc4"));

            contentPanel.Controls.Add(flow);
        }

        void RenderSlideControls() {
            FlowLayoutPanel flow = CreateContentFlow();

            AddTacticalKeyCard(flow, PhysicalKeyCapture.KeyName(mapKey), Localization.Get("WzKeyMTitle"), Localization.Get("WzKeyMDesc"));
            AddTacticalKeyCard(flow, PhysicalKeyCapture.KeyName(appControls.Key(AppControlAction.Settings)), Localization.Get("WzKeyHomeTitle"), Localization.Get("WzKeyHomeDesc"));
            AddTacticalKeyCard(flow, PhysicalKeyCapture.KeyName(appControls.Key(AppControlAction.AddWaypoint)), Localization.Get("WzKeyInsertTitle"), Localization.Get("WzKeyInsertDesc"));
            AddTacticalKeyCard(flow, PhysicalKeyCapture.KeyName(appControls.Key(AppControlAction.Search)), Localization.Get("WzKeyDeleteTitle"), Localization.Get("WzKeyDeleteDesc"));
            AddTacticalKeyCard(flow, PhysicalKeyCapture.KeyName(appControls.Key(AppControlAction.ToggleOverlay)), Localization.Get("WzKeyEndTitle"), Localization.Get("WzKeyEndDesc"));
            string copyBindingLabel = copyModKey > 0 ? (PhysicalKeyCapture.KeyName(copyModKey) + "+" + PhysicalKeyCapture.KeyName(copyKey)) : PhysicalKeyCapture.KeyName(copyKey);
            AddTacticalKeyCard(flow, copyBindingLabel, Localization.Get("WzKeyCopyTitle"), Localization.Get("WzKeyCopyDesc"));
            foreach(AppControlAction action in AppControlBindings.AllActions) {
                if(action == AppControlAction.Settings || action == AppControlAction.AddWaypoint
                    || action == AppControlAction.Search || action == AppControlAction.ToggleOverlay) continue;
                AddTacticalKeyCard(flow, PhysicalKeyCapture.KeyName(appControls.Key(action)),
                    Localization.Get(AppControlBindings.LabelKey(action)), Localization.Get("ShortcutPhysicalHelp"));
            }

            contentPanel.Controls.Add(flow);
        }

        void RenderSlideKeybinds() {
            FlowLayoutPanel flow = CreateContentFlow();
            flow.Controls.Add(new Label { Text=Program.CopyBindingHelp,ForeColor=TextWhite,Width=Math.Max(700,contentPanel.Width-80),Height=70 });
            var illustration=CopyKeyIllustration.Create(Math.Max(740,contentPanel.Width-60),290);
            if(illustration!=null) flow.Controls.Add(illustration);

            // Header Banner Card
            Panel banner = new Panel {
                Width = Math.Max(740, contentPanel.Width - 60),
                Height = 86,
                BackColor = CardBg,
                Margin = new Padding(0, 0, 0, 14),
                Padding = new Padding(16, 10, 16, 10)
            };
            banner.Paint += (s, e) => {
                using (Pen pen = new Pen(AmberAccent, 1.5f)) {
                    e.Graphics.DrawRectangle(pen, 0, 0, banner.Width - 1, banner.Height - 1);
                }
                using (Brush b = new SolidBrush(AmberAccent)) {
                    e.Graphics.FillRectangle(b, 0, 0, 4, banner.Height);
                }
            };

            Label bannerTitle = new Label {
                Text = Localization.Get("WizardKeybindsTitle"),
                Font = new Font("Consolas", 11f, FontStyle.Bold),
                ForeColor = AmberAccent,
                AutoSize = true,
                Location = new Point(14, 10)
            };
            banner.Controls.Add(bannerTitle);

            Label bannerDesc = new Label {
                Text = Localization.Get("WizardKeybindsDesc") + "\r\n" + Localization.Get("WizardKeybindsSkipNotice"),
                Font = new Font("Segoe UI", 9f),
                ForeColor = TextWhite,
                Width = banner.Width - 30,
                Height = 44,
                Location = new Point(14, 34),
                AutoSize = false
            };
            banner.Controls.Add(bannerDesc);
            flow.Controls.Add(banner);

            // 4 Key Calibration Rows
            flow.Controls.Add(CreateKeybindRow(1, Localization.Get("WizardKeyMap"), PhysicalKeyCapture.KeyName(mapKey), out mapValLabel, out mapCapBtn));
            flow.Controls.Add(CreateKeybindRow(2, Localization.Get("WizardKeyChat"), PhysicalKeyCapture.KeyName(chatKey), out chatValLabel, out chatCapBtn));
            flow.Controls.Add(CreateKeybindRow(3, Localization.Get("WizardKeyModifier"), PhysicalKeyCapture.KeyName(copyModKey), out modValLabel, out modCapBtn));

            Panel noModPanel = new Panel {
                Width = Math.Max(740, contentPanel.Width - 60),
                Height = 30,
                BackColor = Color.Transparent,
                Margin = new Padding(0, -2, 0, 4)
            };
            chkNoMod = new CheckBox {
                Text = Localization.Get("KeyWizardNoModifier"),
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = AmberBright,
                AutoSize = true,
                Location = new Point(16, 2),
                Checked = (copyModKey == 0),
                Cursor = Cursors.Hand
            };
            if (copyModKey == 0 && modCapBtn != null) modCapBtn.Enabled = false;
            chkNoMod.CheckedChanged += (s, e) => {
                if (chkNoMod.Checked) {
                    copyModKey = 0;
                    if (modValLabel != null) modValLabel.Text = Localization.Get("KeyNone");
                    if (modCapBtn != null) modCapBtn.Enabled = false;
                    if (captureTarget == 3) {
                        captureTarget = 0;
                        if (captureStatusLabel != null) captureStatusLabel.Text = Localization.Get("KeyWizardReady");
                    }
                } else {
                    if (copyModKey == 0) copyModKey = 0x11;
                    if (modValLabel != null) modValLabel.Text = PhysicalKeyCapture.KeyName(copyModKey);
                    if (modCapBtn != null) modCapBtn.Enabled = true;
                }
            };
            noModPanel.Controls.Add(chkNoMod);
            flow.Controls.Add(noModPanel);

            flow.Controls.Add(CreateKeybindRow(4, Localization.Get("WizardKeyCopy"), PhysicalKeyCapture.KeyName(copyKey), out copyValLabel, out copyCapBtn));

            // Reset controls scroll with the bindings; capture feedback stays in the footer.
            Panel statusRow = new Panel {
                Width = Math.Max(740, contentPanel.Width - 60),
                Height = 48,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 8, 0, 0)
            };

            Button resetBtn = new Button {
                Name = "ResetKeybindDefaults",
                Text = Localization.Get("WizardBtnResetDefaults"),
                Width = 170,
                Height = 34,
                Location = new Point(statusRow.Width - 180, 6),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(28, 34, 40),
                ForeColor = TextWhite,
                Font = new Font("Consolas", 8.8f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            resetBtn.FlatAppearance.BorderColor = CardBorder;
            resetBtn.Click += (s, e) => {
                mapKey = 0x4D;
                chatKey = 0x54;
                copyModKey = Program.DefaultCopyModifierKey;
                copyKey = Program.DefaultCopyKey;
                copyScanCode = 0;
                mapScanCode = 0; chatScanCode = 0;
                appControls = new AppControlBindings();
                captureTarget = 0;
                if (chkNoMod != null) chkNoMod.Checked = true;
                if (modCapBtn != null) modCapBtn.Enabled = false;
                UpdateKeybindValues();
                captureStatusLabel.Text = Localization.Get("KeyWizardReady");
            };
            statusRow.Controls.Add(resetBtn);

            flow.Controls.Add(statusRow);

            flow.Controls.Add(new Label {
                Text = Localization.Get("ShortcutHeading"), ForeColor = AmberAccent,
                Font = new Font("Consolas", 11f, FontStyle.Bold), AutoSize = true,
                Margin = new Padding(0, 18, 0, 8)
            });
            flow.Controls.Add(new Label {
                Text = Localization.Get("ShortcutPrompt") + "\r\n" + Localization.Get("ShortcutPhysicalHelp"),
                ForeColor = TextWhite, AutoSize = true,
                MaximumSize = new Size(Math.Max(700, contentPanel.Width - 80), 0)
            });
            foreach(AppControlAction action in AppControlBindings.AllActions) {
                int index = (int)action;
                Label value;
                Button capture;
                Panel row = CreateKeybindRow(10 + index, Localization.Get(AppControlBindings.LabelKey(action)),
                    PhysicalKeyCapture.KeyName(appControls.Key(action)), out value, out capture);
                row.Name = "AppControlRow" + action;
                value.Name = "AppControlValue" + action;
                capture.Name = "AppControlCapture" + action;
                capture.Location = new Point(498, 9);
                capture.Width = 116;
                appValLabels[index] = value;
                appCapButtons[index] = capture;
                Button unbind = new Button {
                    Name = "AppControlUnbind" + action, Text = Localization.Get("ControlUnbind"),
                    Width = 112, Height = 30, Location = new Point(622, 9),
                    FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(28, 34, 40), ForeColor = TextWhite,
                    Font = new Font("Segoe UI", 8.5f), Cursor = Cursors.Hand
                };
                unbind.FlatAppearance.BorderColor = CardBorder;
                AppControlAction capturedAction = action;
                unbind.Click += (s, e) => {
                    captureTarget = 0;
                    appControls.Set(capturedAction, 0, 0);
                    UpdateKeybindValues();
                    if(captureStatusLabel != null) captureStatusLabel.Text = Localization.Get("KeyWizardReady");
                };
                row.Controls.Add(unbind);
                flow.Controls.Add(row);
            }
            contentPanel.Controls.Add(flow);
        }

        Panel CreateKeybindRow(int targetId, string labelText, string keyString, out Label valLabel, out Button capBtn) {
            Panel row = new Panel {
                Width = Math.Max(740, contentPanel.Width - 60),
                Height = 48,
                BackColor = CardBg,
                Margin = new Padding(0, 0, 0, 6),
                Padding = new Padding(12, 6, 12, 6)
            };
            row.Paint += (s, e) => {
                using (Pen pen = new Pen(CardBorder)) {
                    e.Graphics.DrawRectangle(pen, 0, 0, row.Width - 1, row.Height - 1);
                }
            };

            Label title = new Label {
                Text = labelText,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = TextWhite,
                Width = 320,
                Location = new Point(16, 13),
                AutoSize = false
            };
            row.Controls.Add(title);

            valLabel = new Label {
                Text = keyString,
                Font = new Font("Consolas", 10.5f, FontStyle.Bold),
                ForeColor = AmberAccent,
                BackColor = Color.FromArgb(16, 20, 24),
                TextAlign = ContentAlignment.MiddleCenter,
                Width = 140,
                Height = 30,
                Location = new Point(350, 9)
            };
            row.Controls.Add(valLabel);

            capBtn = new Button {
                Text = Localization.Get("WizardBtnCapture"),
                Width = 140,
                Height = 30,
                Location = new Point(510, 9),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(34, 42, 52),
                ForeColor = TextWhite,
                Font = new Font("Consolas", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            capBtn.FlatAppearance.BorderColor = CardBorder;
            capBtn.Click += (s, e) => {
                captureTarget = targetId;
                if (captureStatusLabel != null) captureStatusLabel.Text = Localization.Get("WizardPressKeyPrompt");
                UpdateKeybindValues();
                Focus();
            };
            row.Controls.Add(capBtn);

            return row;
        }

        void UpdateKeybindValues() {
            if (mapValLabel != null) mapValLabel.Text = PhysicalKeyCapture.KeyName(mapKey);
            if (chatValLabel != null) chatValLabel.Text = PhysicalKeyCapture.KeyName(chatKey);
            if (modValLabel != null) modValLabel.Text = PhysicalKeyCapture.KeyName(copyModKey);
            if (copyValLabel != null) copyValLabel.Text = PhysicalKeyCapture.KeyName(copyKey);
            if (chkNoMod != null && chkNoMod.Checked != (copyModKey == 0)) chkNoMod.Checked = (copyModKey == 0);
            if (modCapBtn != null) modCapBtn.Enabled = (copyModKey != 0);
            foreach(AppControlAction action in AppControlBindings.AllActions) {
                int index = (int)action;
                if(appValLabels[index] != null) appValLabels[index].Text = PhysicalKeyCapture.KeyName(appControls.Key(action));
                if(appCapButtons[index] != null) appCapButtons[index].Text = captureTarget == 10 + index
                    ? Localization.Get("WizardPressKeyPrompt") : Localization.Get("WizardBtnCapture");
            }
        }



        void RenderSlideSafety() {
            FlowLayoutPanel flow = CreateContentFlow();

            AddTacticalCard(flow, "[ COMM // 01 ] " + Localization.Get("WzSafeTitle1"), Localization.Get("WzSafeDesc1"));
            AddTacticalCard(flow, "[ LOCK // 02 ] " + Localization.Get("WzSafeTitle2"), Localization.Get("WzSafeDesc2"));
            AddTacticalCard(flow, "[ SYNC // 03 ] " + Localization.Get("WzSafeTitle3"), Localization.Get("WzSafeDesc3"));

            contentPanel.Controls.Add(flow);
        }

        void RenderSlideFeatures() {
            FlowLayoutPanel flow = CreateContentFlow();

            AddTacticalCard(flow, "[ DB // 01 ] " + Localization.Get("WzFeatTitle1"), Localization.Get("WzFeatDesc1"));
            AddTacticalCard(flow, "[ CALIB // 02 ] " + Localization.Get("WzFeatTitle2"), Localization.Get("WzFeatDesc2"));
            AddTacticalCard(flow, "[ DATA // 03 ] " + Localization.Get("WzFeatTitle3"), Localization.Get("WzFeatDesc3"));

            contentPanel.Controls.Add(flow);
        }

        FlowLayoutPanel CreateContentFlow() {
            return new FlowLayoutPanel {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(4, 4, 16, 16)
            };
        }

        void AddTacticalCard(FlowLayoutPanel flow, string title, string description) {
            Panel card = new Panel {
                Width = Math.Max(740, contentPanel.Width - 60),
                Height = 84,
                BackColor = CardBg,
                Margin = new Padding(0, 0, 0, 10),
                Padding = new Padding(16, 10, 16, 10)
            };
            card.Paint += (s, e) => {
                using (Pen pen = new Pen(CardBorder)) {
                    e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
                }
                using (Brush b = new SolidBrush(AmberAccent)) {
                    e.Graphics.FillRectangle(b, 0, 0, 3, card.Height);
                }
            };

            Label titleLbl = new Label {
                Text = title,
                Font = new Font("Consolas", 10f, FontStyle.Bold),
                ForeColor = AmberAccent,
                AutoSize = true,
                Location = new Point(14, 10)
            };
            card.Controls.Add(titleLbl);

            Label descLbl = new Label {
                Text = description,
                Font = new Font("Segoe UI", 9f),
                ForeColor = TextWhite,
                Width = card.Width - 30,
                Height = 44,
                Location = new Point(14, 34),
                AutoSize = false
            };
            card.Controls.Add(descLbl);

            flow.Controls.Add(card);
        }

        void AddTacticalKeyCard(FlowLayoutPanel flow, string key, string title, string description) {
            Panel card = new Panel {
                Width = Math.Max(740, contentPanel.Width - 60),
                Height = 74,
                BackColor = CardBg,
                Margin = new Padding(0, 0, 0, 8),
                Padding = new Padding(12, 8, 12, 8)
            };
            card.Paint += (s, e) => {
                using (Pen pen = new Pen(CardBorder)) {
                    e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
                }
                using (Brush b = new SolidBrush(AmberAccent)) {
                    e.Graphics.FillRectangle(b, 0, 0, 3, card.Height);
                }
            };

            // Sharp Mechanical Key Badge
            Panel keyBadge = new Panel {
                Width = 120,
                Height = 38,
                Location = new Point(14, 18),
                BackColor = Color.FromArgb(16, 20, 24)
            };
            keyBadge.Paint += (s, e) => {
                using (Pen p = new Pen(AmberAccent, 1.5f)) {
                    e.Graphics.DrawRectangle(p, 0, 0, keyBadge.Width - 1, keyBadge.Height - 1);
                }
                using (Font kFont = new Font("Consolas", 9.5f, FontStyle.Bold))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }) {
                    e.Graphics.DrawString("[ " + key + " ]", kFont, Brushes.White, new RectangleF(0, 0, keyBadge.Width, keyBadge.Height), sf);
                }
            };
            card.Controls.Add(keyBadge);

            Label titleLbl = new Label {
                Text = title,
                Font = new Font("Consolas", 10f, FontStyle.Bold),
                ForeColor = AmberAccent,
                AutoSize = true,
                Location = new Point(144, 12)
            };
            card.Controls.Add(titleLbl);

            Label descLbl = new Label {
                Text = description,
                Font = new Font("Segoe UI", 9f),
                ForeColor = TextWhite,
                Width = card.Width - 156,
                Height = 34,
                Location = new Point(144, 32),
                AutoSize = false
            };
            card.Controls.Add(descLbl);

            flow.Controls.Add(card);
        }

        void RefreshTranslations() {
            updatingLanguage = true;
            try {
                Text = Localization.Get("WizardTitle");
                titleLabel.Text = Localization.Get("WizardBranding");
                subtitleLabel.Text = Localization.Get("WizardTagline");
                prevButton.Text = Localization.Get("WizardBtnPrev");
                nextButton.Text = Localization.Get("WizardBtnNext");
                skipButton.Text = Localization.Get("WizardBtnSkip");
                finishButton.Text = Localization.Get("WizardBtnFinish");

                string[] tabKeys = new string[] {
                    "WizardTabOverview",
                    "WizardTabControls",
                    "WizardTabKeybinds",
                    "WizardTabSafety",
                    "WizardTabFeatures"
                };
                for (int i = 0; i < TotalSlides; i++) {
                    tabButtons[i].Text = Localization.Get(tabKeys[i]);
                }

                UpdateSlide();
            } finally {
                updatingLanguage = false;
            }
        }
    }
}
