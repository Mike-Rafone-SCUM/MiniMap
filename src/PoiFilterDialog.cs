using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace ScumMiniMap {
    public sealed class PoiFilterDialog : Form {
        readonly ScumMapStore store;
        readonly Action onFiltersChanged;
        readonly TreeView tree;
        readonly TextBox searchBox;
        readonly Label summaryLabel;
        bool updatingChecks;

        public PoiFilterDialog(ScumMapStore store, Action onFiltersChanged) {
            this.store = store;
            this.onFiltersChanged = onFiltersChanged;

            Text = Localization.Get("PoiFilterTitle");
            ClientSize = new Size(540, 640);
            MinimumSize = new Size(460, 500);
            StartPosition = FormStartPosition.CenterParent;
            TopMost = true;
            BackColor = OverlayTheme.Background;
            ForeColor = OverlayTheme.Ink;
            Font = new Font("Segoe UI", 9f);

            // Top control panel
            Panel topPanel = new Panel { Dock = DockStyle.Top, Height = 112, Padding = new Padding(12, 10, 12, 6), BackColor = OverlayTheme.Surface };

            Label searchLbl = new Label { Text = Localization.Get("SearchFilterPrompt"), Dock = DockStyle.Top, Height = 20, ForeColor = OverlayTheme.InkMuted };
            searchBox = new TextBox { Dock = DockStyle.Top, Height = 24, BackColor = OverlayTheme.Background, ForeColor = Color.WhiteSmoke };
            searchBox.TextChanged += (s, e) => FilterTree(searchBox.Text);

            FlowLayoutPanel quickButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 28, FlowDirection = FlowDirection.LeftToRight };
            Button btnSelectAll = new Button { Text = Localization.Get("SelectAll"), Width = 100, Height = 25, FlatStyle = FlatStyle.Flat };
            btnSelectAll.Click += (s, e) => ToggleAll(true);
            Button btnDeselectAll = new Button { Text = Localization.Get("DeselectAll"), Width = 110, Height = 25, FlatStyle = FlatStyle.Flat };
            btnDeselectAll.Click += (s, e) => ToggleAll(false);
            Button btnDefaults = new Button { Text = Localization.Get("ResetDefaults"), Width = 120, Height = 25, FlatStyle = FlatStyle.Flat };
            btnDefaults.Click += (s, e) => {
                store.ResetToDefaults();
                PopulateTree(searchBox.Text);
                UpdateSummary();
            };

            quickButtons.Controls.Add(btnSelectAll);
            quickButtons.Controls.Add(btnDeselectAll);
            quickButtons.Controls.Add(btnDefaults);

            topPanel.Controls.Add(quickButtons);
            topPanel.Controls.Add(searchBox);
            topPanel.Controls.Add(searchLbl);
            FlowLayoutPanel activities=new FlowLayoutPanel { Dock=DockStyle.Bottom,Height=30 };
            foreach(string activity in new[]{"Fishing","Hunting"}) {
                string selectedActivity=activity;
                Button activityButton=new Button { Text=Localization.GetSectionName(activity),Width=120,Height=26 };
                activityButton.Click+=(s,e)=> { searchBox.Text=selectedActivity; };
                activities.Controls.Add(activityButton);
            }
            topPanel.Controls.Add(activities);
            activities.BringToFront();
            Button habitatGuide=new Button { Text=Localization.Get("HabitatGuide"),Width=140,Height=26 };
            habitatGuide.Click+=(s,e)=>ShowHabitatGuide();
            activities.Controls.Add(habitatGuide);

            // Center TreeView
            tree = new TreeView {
                Dock = DockStyle.Fill,
                CheckBoxes = true,
                BackColor = Color.FromArgb(22, 27, 32),
                ForeColor = Color.WhiteSmoke,
                LineColor = OverlayTheme.Border,
                Font = new Font("Segoe UI", 9.5f),
                ItemHeight = 22,
                ShowLines = true,
                ShowPlusMinus = true,
                ShowRootLines = true
            };
            tree.AfterCheck += Tree_AfterCheck;

            // Bottom action panel
            Panel bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 50, Padding = new Padding(12, 10, 12, 10), BackColor = OverlayTheme.Surface };
            summaryLabel = new Label { Dock = DockStyle.Left, Width = 300, AutoEllipsis = true, Padding = new Padding(0, 5, 0, 0), ForeColor = OverlayTheme.InkMuted };

            Button btnApply = new Button { Text = Localization.Get("ApplyClose"), Width = 120, Height = 28, Dock = DockStyle.Right, DialogResult = DialogResult.OK };
            btnApply.Click += (s, e) => {
                ApplySelections();
                if (onFiltersChanged != null) onFiltersChanged();
                Close();
            };

            bottomPanel.Controls.Add(summaryLabel);
            bottomPanel.Controls.Add(btnApply);

            Controls.Add(tree);
            Controls.Add(topPanel);
            Controls.Add(bottomPanel);

            OverlayTheme.Style(this);
            btnApply.BackColor = OverlayTheme.Accent;
            btnApply.ForeColor = Color.FromArgb(14, 17, 20);
            PopulateTree("");
            UpdateSummary();
        }

        void Tree_AfterCheck(object sender, TreeViewEventArgs e) {
            if (updatingChecks) return;
            updatingChecks = true;
            try {
                if (e.Node.Nodes.Count > 0) {
                    // Parent (Section) node checked/unchecked: apply to all children
                    foreach (TreeNode child in e.Node.Nodes) {
                        child.Checked = e.Node.Checked;
                        ScumMapCategory cat = child.Tag as ScumMapCategory;
                        if (cat != null) store.SetCategoryEnabled(cat.Id, child.Checked);
                    }
                } else if (e.Node.Parent != null) {
                    // Child (Category) node checked/unchecked
                    ScumMapCategory cat = e.Node.Tag as ScumMapCategory;
                    if (cat != null) store.SetCategoryEnabled(cat.Id, e.Node.Checked);

                    bool allChecked = true;
                    foreach (TreeNode sibling in e.Node.Parent.Nodes) {
                        if (!sibling.Checked) { allChecked = false; break; }
                    }
                    e.Node.Parent.Checked = allChecked;
                }
            } finally {
                updatingChecks = false;
                UpdateSummary();
            }
        }

        void ToggleAll(bool enabled) {
            updatingChecks = true;
            try {
                store.SetAllEnabled(enabled);
                foreach (TreeNode root in tree.Nodes) {
                    root.Checked = enabled;
                    foreach (TreeNode child in root.Nodes) {
                        child.Checked = enabled;
                    }
                }
            } finally {
                updatingChecks = false;
                UpdateSummary();
            }
        }

        void FilterTree(string query) {
            PopulateTree(query);
            UpdateSummary();
        }

        void PopulateTree(string query) {
            updatingChecks = true;
            tree.BeginUpdate();
            tree.Nodes.Clear();

            string q = (query ?? "").Trim().ToLowerInvariant();

            foreach (string section in store.Sections) {
                List<ScumMapCategory> cats = store.CategoriesBySection[section];
                List<ScumMapCategory> filtered = string.IsNullOrEmpty(q)
                    ? cats
                    : cats.Where(c => c.Name.ToLowerInvariant().Contains(q) || section.ToLowerInvariant().Contains(q) ||
                        Localization.GetCategoryName(c.Name).ToLowerInvariant().Contains(q) || Localization.GetSectionName(section).ToLowerInvariant().Contains(q)).ToList();

                if (filtered.Count == 0) continue;

                int secTotalMarkers = filtered.Sum(c => c.MarkerCount);
                string secTitle = Localization.T("PoiSectionSummary", Localization.GetSectionName(section), filtered.Count, secTotalMarkers);

                TreeNode secNode = new TreeNode(secTitle) { Tag = section };
                bool allChecked = true;

                foreach (ScumMapCategory cat in filtered.OrderBy(c => c.Name)) {
                    string catTitle = string.Format(CultureInfo.InvariantCulture, "{0} ({1})", Localization.GetCategoryName(cat.Name), cat.MarkerCount);
                    bool isEnabled = store.IsCategoryEnabled(cat.Id);
                    TreeNode catNode = new TreeNode(catTitle) {
                        Tag = cat,
                        Checked = isEnabled
                    };
                    if (!isEnabled) allChecked = false;
                    secNode.Nodes.Add(catNode);
                }

                secNode.Checked = allChecked && filtered.Count > 0;
                tree.Nodes.Add(secNode);
                secNode.Expand();
            }

            tree.EndUpdate();
            updatingChecks = false;
        }

        void ApplySelections() {
            foreach (TreeNode root in tree.Nodes) {
                foreach (TreeNode child in root.Nodes) {
                    ScumMapCategory cat = child.Tag as ScumMapCategory;
                    if (cat != null) {
                        store.SetCategoryEnabled(cat.Id, child.Checked);
                    }
                }
            }
        }

        void UpdateSummary() {
            int enabledCount = store.Categories.Count(c => store.IsCategoryEnabled(c.Id));
            int totalCats = store.Categories.Count;
            int enabledMarkers = store.Markers.Count(m=>store.IsCategoryEnabled(m.CategoryId));
            int enabledHabitats = store.Habitats.Count(store.IsHabitatEnabled);

            summaryLabel.Text = Localization.T("PoiSummary", enabledCount, totalCats, enabledMarkers,enabledHabitats);
        }
        void ShowHabitatGuide() {
            using(Form guide=new Form { Text=Localization.Get("HabitatGuide"),ClientSize=new Size(680,520),StartPosition=FormStartPosition.CenterParent,TopMost=true }) {
                TextBox text=new TextBox { Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill,Font=new Font("Segoe UI",10),BackColor=Color.FromArgb(22,27,32),ForeColor=Color.WhiteSmoke };
                var lines=new List<string> { Localization.Get("HabitatHelp"), "", Localization.Get("GuideFishingSource")+": https://davoonline.com/scummap/", Localization.Get("GuideHuntingSource")+": https://scum-map.com/en/catalog/scum/island", Localization.T("GuideRetrieved","2026-09-14"), "" };
                foreach(var category in store.Categories.Where(c=>c.Id<0).OrderBy(c=>c.Section).ThenBy(c=>c.Name)) {
                    lines.Add(Localization.GetCategoryName(category.Name)+" — "+Localization.GetSectionName(category.Section));
                    foreach(var habitat in store.Habitats.Where(h=>h.CategoryIds.Contains(category.Id)).GroupBy(h=>h.Name).Select(group=>group.First()))
                        lines.Add("  "+habitat.Name+": "+habitat.Summary);
                    lines.Add("");
                }
                text.Text=string.Join(Environment.NewLine,lines); text.Select(0,0);
                guide.Controls.Add(text);guide.ShowDialog(this);
            }
        }
    }
}
