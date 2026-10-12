using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScumMiniMap {
    internal sealed class SpawnModifierDialog:Form {
        readonly Dictionary<string,Control> fields=new Dictionary<string,Control>(StringComparer.Ordinal);
        readonly Dictionary<string,CheckBox> selected=new Dictionary<string,CheckBox>(StringComparer.Ordinal);
        readonly HashSet<string> changed=new HashSet<string>(StringComparer.Ordinal);
        readonly Label error;
        readonly bool bulk;
        bool changingSelection;
        internal string Result { get; private set; }
        internal IDictionary<string,string> Changes { get; private set; }

        static string[] Presets(string property) {
            switch(property) {
                case "Health":case "Dirtiness":return new[]{"0","25","50","75","100"};
                case "Uses":return new[]{"0","1","5","10","20","50","100"};
                case "StackCount":return new[]{"1","2","5","10","20","50","100"};
                case "Radiation":case "Weight":return new[]{"0","0.1","0.5","1","5","10"};
                default:return new string[0];
            }
        }

        internal SpawnModifierDialog(string itemName,string modifiers):this(itemName,new[]{modifiers},false) {}

        internal SpawnModifierDialog(string itemName,IList<string> modifierSets,bool bulk) {
            if(modifierSets==null || modifierSets.Count==0)throw new ArgumentException("Select at least one item.","modifierSets");
            this.bulk=bulk;
            var initial=modifierSets.Select(text=>SpawnModifiers.Parse(text)).ToList();
            string mixedLabel=Localization.Get("SpawnCartModifierMixed");
            Text=Localization.T("SpawnCartModifierTitle",itemName);Name="SpawnModifierDialog";
            ClientSize=new Size(590,bulk?610:550);MinimumSize=new Size(606,bulk?649:589);StartPosition=FormStartPosition.CenterParent;
            Font=new Font("Segoe UI",9);TopMost=true;MaximizeBox=false;MinimizeBox=false;
            RightToLeft=Localization.Current==AppLanguage.Arabic?RightToLeft.Yes:RightToLeft.No;
            var help=new Label { Dock=DockStyle.Top,Height=bulk?188:128,Padding=new Padding(14,10,14,0),
                Text=(bulk?Localization.T("SpawnCartModifierBulkHelp",modifierSets.Count)+"\n":"")+Localization.Get("SpawnCartModifierHelp")+
                    "\n"+Localization.Get("SpawnCartModifierValues")+"\n"+Localization.Get("SpawnCartModifierLocation") };
            var values=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=SpawnModifiers.PropertyNames.Count,Padding=new Padding(14,4,14,4) };
            int labelWidth=Math.Max(150,SpawnModifiers.PropertyNames.Max(property=>TextRenderer.MeasureText(Localization.GetSpawnModifierLabel(property),Font).Width)+28);
            values.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,Math.Min(300,labelWidth)));values.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            for(int row=0;row<SpawnModifiers.PropertyNames.Count;row++) {
                string property=SpawnModifiers.PropertyNames[row];string value;initial[0].TryGetValue(property,out value);
                bool mixed=bulk && initial.Skip(1).Any(valuesForItem=> {
                    string other;valuesForItem.TryGetValue(property,out other);return other!=value;
                });
                var input=new ComboBox { DropDownStyle=property=="KeyCardSector"?ComboBoxStyle.DropDownList:ComboBoxStyle.DropDown,
                    Dock=DockStyle.Fill,MaxLength=property=="Location"?512:32,RightToLeft=RightToLeft.No };
                input.Items.Add("");
                if(property=="KeyCardSector") {
                    for(char letter='A';letter<='D';letter++)for(int number=1;number<=4;number++)input.Items.Add(letter.ToString()+number);
                } else foreach(string preset in Presets(property))input.Items.Add(preset);
                foreach(var valuesForItem in initial) {
                    string existing;
                    if(valuesForItem.TryGetValue(property,out existing) && !input.Items.Contains(existing))input.Items.Add(existing);
                }
                if(mixed)input.Items.Add(mixedLabel);
                input.SelectedItem=mixed?mixedLabel:value??"";
                input.Name="SpawnModifier"+property;input.AccessibleName=property;fields.Add(property,input);
                var option=new CheckBox { Name="SelectSpawnModifier"+property,Text=Localization.GetSpawnModifierLabel(property),
                    Dock=DockStyle.Fill,ThreeState=bulk,AutoCheck=!bulk,AccessibleName=property,
                    CheckState=mixed?CheckState.Indeterminate:string.IsNullOrEmpty(value)?CheckState.Unchecked:CheckState.Checked };
                selected.Add(property,option);
                // Choosing a preset also selects its modifier; deselecting restores game defaults.
                input.TextChanged+=(s,e)=> {
                    if(changingSelection)return;
                    bool keepMixed=mixed && input.Text==mixedLabel;
                    if(keepMixed)changed.Remove(property);else changed.Add(property);
                    changingSelection=true;
                    try { option.CheckState=keepMixed?CheckState.Indeterminate:string.IsNullOrWhiteSpace(input.Text)?CheckState.Unchecked:CheckState.Checked; }
                    finally { changingSelection=false; }
                };
                option.CheckStateChanged+=(s,e)=> {
                    if(changingSelection)return;
                    if(option.CheckState==CheckState.Indeterminate)changed.Remove(property);else changed.Add(property);
                    changingSelection=true;
                    try {
                        if(option.CheckState==CheckState.Unchecked || (mixed && input.Text==mixedLabel && option.CheckState==CheckState.Checked))input.Text="";
                    }finally { changingSelection=false; }
                };
                if(bulk)option.Click+=(s,e)=>option.CheckState=option.CheckState==CheckState.Checked?CheckState.Unchecked:CheckState.Checked;
                values.RowStyles.Add(new RowStyle(SizeType.Percent,100f/SpawnModifiers.PropertyNames.Count));
                values.Controls.Add(option,0,row);
                values.Controls.Add(input,1,row);
            }
            error=new Label { Dock=DockStyle.Bottom,Height=52,Padding=new Padding(14,2,14,0),ForeColor=Color.Salmon };
            var buttons=new FlowLayoutPanel { Dock=DockStyle.Bottom,Height=48,Padding=new Padding(10,6,10,6),FlowDirection=FlowDirection.RightToLeft };
            var apply=new Button { Name="ApplySpawnModifiers",Text=Localization.Get("SpawnCartModifierApply"),Width=105,Height=30 };
            var cancel=new Button { Text=Localization.Get("Cancel"),Width=105,Height=30,DialogResult=DialogResult.Cancel,CausesValidation=false };
            var reset=new Button { Name="ResetSpawnModifiers",Text=Localization.Get("SpawnCartModifierClear"),Width=190,Height=30,CausesValidation=false };
            apply.Click+=(s,e)=>Apply();reset.Click+=(s,e)=> {
                changingSelection=true;
                try { foreach(string property in SpawnModifiers.PropertyNames) {
                    fields[property].Text="";selected[property].CheckState=CheckState.Unchecked;changed.Add(property);
                } }finally { changingSelection=false; }
                error.Text="";
            };
            buttons.Controls.AddRange(new Control[]{apply,cancel,reset});AcceptButton=apply;CancelButton=cancel;
            Controls.Add(values);Controls.Add(help);Controls.Add(error);Controls.Add(buttons);OverlayTheme.Style(this);error.ForeColor=Color.Salmon;
        }
        internal bool Apply() {
            try {
                var pairs=new List<string>();var changes=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
                foreach(string property in SpawnModifiers.PropertyNames) {
                    if(bulk && !changed.Contains(property))continue;
                    if(selected[property].CheckState==CheckState.Indeterminate)continue;
                    if(selected[property].CheckState==CheckState.Unchecked) { changes.Add(property,null);continue; }
                    string value=fields[property].Text.Trim();
                    if(value.Length==0)throw new FormatException(Localization.T("SpawnCartModifierValue",property));
                    var parsed=SpawnModifiers.Parse(property+" "+(property=="Location"?SpawnModifiers.NormalizeLocation(value):value));
                    if(parsed.Count!=1 || !parsed.ContainsKey(property))throw new FormatException(Localization.T("SpawnCartModifierValue",property));
                    changes.Add(property,parsed[property]);pairs.Add(property+" "+parsed[property]);
                }
                Result=SpawnModifiers.Normalize(string.Join(" ",pairs));Changes=new ReadOnlyDictionary<string,string>(changes);
                DialogResult=DialogResult.OK;return true;
            }catch(FormatException ex) { error.Text=ex.Message;return false; }
        }
    }

    internal sealed class SpawnKitDialog:Form {
        readonly bool saving;
        readonly TextBox name;
        readonly ListBox list;
        readonly Label details,error;
        readonly Button apply;
        internal string KitName { get; private set; }
        internal long KitId { get; private set; }
        internal bool ImportRequested { get; private set; }
        internal SpawnKitDialog(bool saving,IList<SpawnSavedKit> kits,string defaultName) {
            this.saving=saving;
            Text=Localization.Get(saving?"SpawnCartSave":"SpawnCartLoad");Name="SpawnKitDialog";
            ClientSize=new Size(580,430);MinimumSize=new Size(596,469);StartPosition=FormStartPosition.CenterParent;
            Font=new Font("Segoe UI",9);MaximizeBox=false;MinimizeBox=false;ShowInTaskbar=false;
            var content=new Panel { Dock=DockStyle.Fill,Padding=new Padding(16,12,16,4) };
            var heading=new Label { Text=Text,Dock=DockStyle.Top,Height=34,Font=new Font("Segoe UI",14,FontStyle.Bold),ForeColor=OverlayTheme.Accent };
            var hint=new Label { Text=Localization.Get("SpawnCartKitStorageHint"),Dock=DockStyle.Top,Height=32,ForeColor=OverlayTheme.InkMuted };
            var namePanel=new Panel { Dock=DockStyle.Top,Height=62,Visible=saving };
            name=new TextBox { Name="SpawnKitName",Dock=DockStyle.Bottom,MaxLength=80,Text=defaultName??"",AccessibleName=Localization.Get("SpawnCartKitName") };
            namePanel.Controls.Add(name);namePanel.Controls.Add(new Label { Text=Localization.Get("SpawnCartKitName"),Dock=DockStyle.Top,Height=25 });
            var listPanel=new Panel { Dock=DockStyle.Fill,Padding=new Padding(0,10,0,0) };
            list=new ListBox { Name="SpawnSavedKits",Dock=DockStyle.Fill,IntegralHeight=false,BorderStyle=BorderStyle.None,SelectionMode=SelectionMode.One,
                DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=30,AccessibleName=Localization.Get("SpawnCartSavedKits") };
            list.DrawItem+=(s,e)=> {
                if(e.Index<0 || e.Index>=list.Items.Count)return;
                bool selected=(e.State&DrawItemState.Selected)!=0;
                using(var surface=new SolidBrush(selected?FullMapSidebarStyle.SelectedSurface:list.BackColor))e.Graphics.FillRectangle(surface,e.Bounds);
                TextRenderer.DrawText(e.Graphics,list.GetItemText(list.Items[e.Index]),list.Font,
                    new Rectangle(e.Bounds.Left+10,e.Bounds.Top,Math.Max(1,e.Bounds.Width-20),e.Bounds.Height),
                    selected?OverlayTheme.Accent:OverlayTheme.Ink,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);
                e.DrawFocusRectangle();
            };
            foreach(var kit in kits)list.Items.Add(kit);
            listPanel.Controls.Add(list);listPanel.Controls.Add(new Label { Text=Localization.Get("SpawnCartSavedKits"),Dock=DockStyle.Top,Height=25 });
            details=new Label { Name="SpawnKitDetails",Dock=DockStyle.Bottom,Height=48,Padding=new Padding(0,8,0,0),ForeColor=OverlayTheme.InkMuted };
            error=new Label { Name="SpawnKitError",Dock=DockStyle.Bottom,Height=36,ForeColor=Color.Salmon };
            var buttons=new FlowLayoutPanel { Dock=DockStyle.Bottom,Height=54,Padding=new Padding(12,8,12,8),FlowDirection=FlowDirection.RightToLeft,WrapContents=false };
            apply=new Button { Name="ApplySpawnKit",Text=Text.TrimEnd('.','…'),Width=120,Height=32 };
            var cancel=new Button { Text=Localization.Get("Cancel"),Width=100,Height=32,DialogResult=DialogResult.Cancel,CausesValidation=false };
            var import=new Button { Name="ImportLegacySpawnKit",Text=Localization.Get("SpawnCartKitImport"),Width=155,Height=32,Visible=!saving,CausesValidation=false };
            apply.Click+=(s,e)=>Apply();
            import.Click+=(s,e)=> { ImportRequested=true;DialogResult=DialogResult.OK; };
            list.SelectedIndexChanged+=(s,e)=> {
                var kit=list.SelectedItem as SpawnSavedKit;
                if(saving && kit!=null)name.Text=kit.Name;
                details.Text=kit==null?(kits.Count==0?Localization.Get("SpawnCartNoSavedKits"):""):
                    Localization.T("SpawnCartKitDetails",kit.LineCount,kit.TotalQuantity,kit.UpdatedUtc.ToLocalTime().ToString("g",CultureInfo.CurrentCulture));
                RefreshApply();
            };
            list.DoubleClick+=(s,e)=> { if(!saving && list.SelectedItem!=null)Apply(); };
            name.TextChanged+=(s,e)=>RefreshApply();
            buttons.Controls.AddRange(new Control[]{apply,cancel,import});AcceptButton=apply;CancelButton=cancel;
            content.Controls.Add(listPanel);content.Controls.Add(namePanel);content.Controls.Add(hint);content.Controls.Add(heading);content.Controls.Add(details);content.Controls.Add(error);
            Controls.Add(content);Controls.Add(buttons);OverlayTheme.Style(this);
            heading.ForeColor=OverlayTheme.Accent;hint.ForeColor=details.ForeColor=OverlayTheme.InkMuted;error.ForeColor=Color.Salmon;
            list.BackColor=FullMapSidebarStyle.ButtonSurface;list.ForeColor=OverlayTheme.Ink;
            apply.BackColor=OverlayTheme.Accent;apply.ForeColor=Color.Black;apply.FlatAppearance.BorderColor=OverlayTheme.Accent;
            details.Text=kits.Count==0?Localization.Get("SpawnCartNoSavedKits"):"";
            RefreshApply();
            Shown+=(s,e)=> { if(saving) { name.Focus();name.SelectAll(); }else list.Focus(); };
        }
        void RefreshApply() { if(apply!=null)apply.Enabled=saving?!string.IsNullOrWhiteSpace(name.Text):list.SelectedItem!=null; }
        internal bool Apply() {
            if(saving) {
                string value;
                try { value=name.Text.Trim().Normalize(System.Text.NormalizationForm.FormC); }
                catch(ArgumentException) { error.Text=Localization.Get("SpawnCartKitNameInvalid");return false; }
                if(value.Length==0 || value.Length>80 || value.Any(character=>char.IsControl(character))) {
                    error.Text=Localization.Get("SpawnCartKitNameInvalid");return false;
                }
                KitName=value;
            } else {
                var kit=list.SelectedItem as SpawnSavedKit;if(kit==null)return false;
                KitId=kit.Id;KitName=kit.Name;
            }
            DialogResult=DialogResult.OK;return true;
        }
    }

    internal sealed class SpawnCartDialog:Form {
        internal Func<Func<DialogResult>,DialogResult> ModalInteraction;
        internal Action<Form,IntPtr> RestoreAfterSend;
        DialogResult RunDialogInteraction(Func<DialogResult> show) {
            Native.ReleaseCapture();Cursor.Clip=Rectangle.Empty;
            return ModalInteraction==null?show():ModalInteraction(show);
        }
        static string CategoryLabel(string category) {
            if(category=="weapon_ranged")return Localization.Get("SpawnCartRangedWeapons");
            if(category=="weapon_melee" || category=="melee_weapon")return Localization.Get("SpawnCartMeleeWeapons");
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(category.Replace('_',' '));
        }
        readonly SpawnItemCatalog catalog;
        readonly SpawnCart cart;
        readonly string cartPath;
        readonly SpawnKitDatabase kitDatabase;
        readonly Func<IList<string>,Form,Action<int>,Task<SpawnBatchResult>> send;
        readonly SpawnResultsListView items;
        readonly Dictionary<string,SpawnItemSearchResult> searchMatches=new Dictionary<string,SpawnItemSearchResult>(StringComparer.OrdinalIgnoreCase);
        readonly DataGridView grid;
        readonly TextBox search,preview;
        readonly TreeView categoryTree;
        readonly NumericUpDown quantity;
        readonly Label matches,summary,status,selectedMatches,emptyResults;
        readonly Label help;
        readonly Button add,remove,clear,save,load,submit,editModifiers,clearSearch,expandGroups,collapseGroups;
        Action<string,int> diagnostics;
        Func<SpawnModifierDialog,DialogResult> modifierEditor;
        Func<SpawnKitDialog,DialogResult> kitEditor;
        Func<string,bool> kitOverwriteConfirmation;
        Func<string> legacyKitPicker;
        IList<SpawnItem> filtered;
        bool rebuilding,sending,sendQueued;
        string chatBinding;
        readonly List<Action> refreshLabels=new List<Action>();

        void BindLabels(Control control) {
            string accessibleKey=Localization.StringsEn.Keys.FirstOrDefault(candidate=>Localization.Get(candidate)==control.AccessibleName);
            if(accessibleKey!=null)refreshLabels.Add(()=>control.AccessibleName=Localization.Get(accessibleKey));
            if(!(control is TextBox) && !(control is NumericUpDown)) {
                string key=Localization.StringsEn.Keys.FirstOrDefault(candidate=>Localization.Get(candidate)==control.Text);
                if(key!=null)refreshLabels.Add(()=>control.Text=Localization.Get(key));
            }
            if(control.ContextMenuStrip!=null)foreach(ToolStripItem item in control.ContextMenuStrip.Items) {
                string key=Localization.StringsEn.Keys.FirstOrDefault(candidate=>Localization.Get(candidate)==item.Text);
                if(key!=null)refreshLabels.Add(()=>item.Text=Localization.Get(key));
            }
            foreach(Control child in control.Controls)BindLabels(child);
        }
        static IEnumerable<TreeNode> TreeNodes(TreeNodeCollection nodes) {
            foreach(TreeNode node in nodes) { yield return node;foreach(var child in TreeNodes(node.Nodes))yield return child; }
        }
        void RefreshLanguage() {
            if(IsDisposed)return;
            if(InvokeRequired) { BeginInvoke(new Action(RefreshLanguage));return; }
            foreach(var refresh in refreshLabels)refresh();
            if(chatBinding!=null)SetChatBinding(chatBinding);
            string[] resultKeys={"SpawnCartName","SpawnCartCompatibility","SpawnCartId"};
            for(int i=0;i<resultKeys.Length;i++)items.Columns[i].Text=Localization.Get(resultKeys[i]);
            string[] cartKeys={"SpawnCartName","SpawnCartQuantity","SpawnCartId","SpawnCartModifiers"};
            for(int i=0;i<cartKeys.Length;i++)grid.Columns[i].HeaderText=Localization.Get(cartKeys[i]);
            var selectedIds=new HashSet<string>(items.SelectedResultIndices.Select(index=>filtered[index].Id),StringComparer.OrdinalIgnoreCase);
            string selectedKey=categoryTree.SelectedNode==null?null:(categoryTree.SelectedNode.Tag as SpawnCategoryNode)==null?"all":((SpawnCategoryNode)categoryTree.SelectedNode.Tag).Key;
            var expanded=new HashSet<string>(TreeNodes(categoryTree.Nodes).Where(node=>node.IsExpanded).Select(node=>node.Tag is SpawnCategoryNode?((SpawnCategoryNode)node.Tag).Key:"all"));
            ApplyLanguageLayout();
            categoryTree.BeginUpdate();
            try {
                categoryTree.Nodes.Clear();
                var all=new TreeNode(Localization.Get("SpawnCartAll")+" ("+catalog.Items.Count+")");
                foreach(var group in SpawnItemCategories.Build(catalog))all.Nodes.Add(CategoryNode(group));
                categoryTree.Nodes.Add(all);
                foreach(var node in TreeNodes(categoryTree.Nodes)) {
                    string key=node.Tag is SpawnCategoryNode?((SpawnCategoryNode)node.Tag).Key:"all";
                    if(expanded.Contains(key))node.Expand();
                    if(key==selectedKey)categoryTree.SelectedNode=node;
                }
            }finally { categoryTree.EndUpdate(); }
            FilterItems();
            foreach(ListViewItem row in items.Items)if(row.Tag is SpawnItemSearchResult && selectedIds.Contains(((SpawnItemSearchResult)row.Tag).Item.Id))row.Selected=true;
            // Keep an active quantity/modifier editor intact; only display names change.
            foreach(DataGridViewRow row in grid.Rows)row.Cells[0].Value=catalog.Find((string)row.Tag).DisplayName;
            RefreshPreview();
        }

        internal SpawnCartDialog(SpawnItemCatalog catalog,string cartPath,Func<IList<string>,Form,Action<int>,Task<SpawnBatchResult>> send) {
            this.catalog=catalog;this.cartPath=cartPath;this.send=send;cart=new SpawnCart(catalog);
            kitDatabase=new SpawnKitDatabase(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(cartPath)),"spawn-kits.db"),catalog);
            Text=Localization.Get("SpawnCartTitle");Name="SpawnCartDialog";TopMost=true;
            Rectangle available=Screen.PrimaryScreen.WorkingArea;
            ClientSize=new Size(Math.Max(984,Math.Min(1440,available.Width-40)),Math.Max(620,Math.Min(820,available.Height-70)));
            MinimumSize=new Size(1000,660);StartPosition=FormStartPosition.CenterScreen;
            Font=new Font("Segoe UI",9);BackColor=FullMapSidebarStyle.Background;KeyPreview=true;
            var header=new Panel { Dock=DockStyle.Top,Height=76,Padding=new Padding(16,8,16,4) };
            var heading=new Label { Text=Localization.Get("SpawnCartTitle"),Dock=DockStyle.Top,Height=30,Font=new Font("Segoe UI",14,FontStyle.Bold) };
            help=new Label { Dock=DockStyle.Fill,Padding=new Padding(0,3,0,0),Text=Localization.Get("SpawnCartLimits"),Font=new Font("Segoe UI",8.5f) };
            header.Controls.Add(help);header.Controls.Add(heading);
            var split=new SplitContainer { Name="SpawnCartLayout",Size=new Size(1360,620),Dock=DockStyle.Fill,SplitterWidth=10,SplitterDistance=820,
                Panel1MinSize=540,Panel2MinSize=380,FixedPanel=FixedPanel.None };
            split.Panel1.Padding=new Padding(14,6,6,8);split.Panel2.Padding=new Padding(8,6,14,8);

            var filters=new Panel { Dock=DockStyle.Top,Height=118 };
            search=new TextBox { Name="SpawnItemSearch",Dock=DockStyle.Fill,MaxLength=160,AccessibleName=Localization.Get("SpawnCartSearch") };
            clearSearch=ActionButton("SpawnCartClearSearch",118,()=> { search.Clear();search.Focus(); });clearSearch.Name="ClearSpawnItemSearch";clearSearch.Dock=DockStyle.Right;clearSearch.CausesValidation=false;
            var searchContainer=new Panel { Dock=DockStyle.Top,Height=30 };searchContainer.Controls.Add(search);searchContainer.Controls.Add(clearSearch);
            var groupActions=new FlowLayoutPanel { Dock=DockStyle.Bottom,Height=42,WrapContents=false,Padding=new Padding(0,2,0,0) };
            expandGroups=ActionButton("SpawnCartExpandGroups",130,()=> { items.SetGroupsCollapsed(false);UpdateResultSelection(); });expandGroups.Name="ExpandSpawnResultGroups";expandGroups.CausesValidation=false;
            collapseGroups=ActionButton("SpawnCartCollapseGroups",130,()=> { items.SetGroupsCollapsed(true);UpdateResultSelection(); });collapseGroups.Name="CollapseSpawnResultGroups";collapseGroups.CausesValidation=false;
            groupActions.Controls.AddRange(new Control[]{expandGroups,collapseGroups});
            var searchHint=new Label { Text=Localization.Get("SpawnCartSearchHint"),Dock=DockStyle.Bottom,Height=23,Font=new Font("Segoe UI",8),AutoEllipsis=true };
            filters.Controls.Add(searchContainer);filters.Controls.Add(new Label { Text=Localization.Get("SpawnCartSearch"),Dock=DockStyle.Top,Height=21 });
            filters.Controls.Add(searchHint);filters.Controls.Add(groupActions);
            categoryTree=new TreeView { Name="SpawnItemCategories",Dock=DockStyle.Left,Width=178,HideSelection=false,ShowLines=true,ShowRootLines=false,
                BorderStyle=BorderStyle.None,ItemHeight=27,ShowNodeToolTips=true,AccessibleName=Localization.Get("SpawnCartCategory") };
            var allCategories=new TreeNode(Localization.Get("SpawnCartAll")+" ("+catalog.Items.Count+")");
            foreach(var group in SpawnItemCategories.Build(catalog))allCategories.Nodes.Add(CategoryNode(group));
            categoryTree.Nodes.Add(allCategories);allCategories.Expand();categoryTree.SelectedNode=allCategories;
            var browser=new Panel { Dock=DockStyle.Fill };
            items=new SpawnResultsListView { Name="SpawnItemResults",Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,HideSelection=false,
                MultiSelect=true,ShowItemToolTips=true,BorderStyle=BorderStyle.None,AccessibleName=Localization.Get("SpawnCartSelect") };
            items.Columns.Add(Localization.Get("SpawnCartName"),240);items.Columns.Add(Localization.Get("SpawnCartCompatibility"),190);
            items.Columns.Add(Localization.Get("SpawnCartId"),150);
            items.SelectedIndexChanged+=(s,e)=>UpdateResultSelection();
            items.MouseDoubleClick+=(s,e)=> { if(e.Button==MouseButtons.Left && items.GetItemAt(e.X,e.Y)!=null)AddSelected(); };
            items.KeyDown+=(s,e)=> { if(e.KeyCode==Keys.Enter) { AddSelected();e.Handled=true;e.SuppressKeyPress=true; } };
            var selection=new FlowLayoutPanel { Dock=DockStyle.Bottom,Height=46,Padding=new Padding(0,6,0,0),WrapContents=false };
            selection.Controls.Add(new Label { Text=Localization.Get("SpawnCartQuantity"),AutoSize=true,Padding=new Padding(0,6,4,0) });
            quantity=new NumericUpDown { Name="SpawnItemQuantity",Minimum=1,Maximum=SpawnCart.MaximumQuantity,Value=1,Width=66,AccessibleName=Localization.Get("SpawnCartQuantity") };
            add=ActionButton("SpawnCartAdd",145,AddSelected);selection.Controls.Add(quantity);selection.Controls.Add(add);
            selectedMatches=new Label { Name="SpawnItemSelection",AutoSize=true,Padding=new Padding(8,7,0,0),Text=Localization.T("SpawnCartSelectedItems",0) };selection.Controls.Add(selectedMatches);
            matches=new Label { Dock=DockStyle.Bottom,Height=24,TextAlign=ContentAlignment.MiddleLeft };
            emptyResults=new Label { Name="SpawnItemEmptyResults",Dock=DockStyle.Fill,Text=Localization.Get("SpawnCartEmptyResults"),TextAlign=ContentAlignment.MiddleCenter,Padding=new Padding(20),Visible=false };
            browser.Controls.Add(items);browser.Controls.Add(emptyResults);browser.Controls.Add(categoryTree);
            split.Panel1.Controls.Add(browser);split.Panel1.Controls.Add(filters);split.Panel1.Controls.Add(selection);split.Panel1.Controls.Add(matches);

            var title=new Label { Text=Localization.Get("SpawnCartContents"),Dock=DockStyle.Top,Height=32,ForeColor=OverlayTheme.Accent,Font=new Font("Segoe UI",11,FontStyle.Bold) };
            grid=new DataGridView { Name="SpawnCartEntries",Dock=DockStyle.Fill,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AllowUserToResizeRows=false,
                RowHeadersVisible=false,AutoGenerateColumns=false,MultiSelect=true,SelectionMode=DataGridViewSelectionMode.FullRowSelect,
                BorderStyle=BorderStyle.None,BackgroundColor=FullMapSidebarStyle.ButtonSurface,EnableHeadersVisualStyles=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill };
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name="Item",HeaderText=Localization.Get("SpawnCartName"),ReadOnly=true,FillWeight=55 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name="Quantity",HeaderText=Localization.Get("SpawnCartQuantity"),FillWeight=18,ValueType=typeof(string) });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name="Id",HeaderText=Localization.Get("SpawnCartId"),ReadOnly=true,FillWeight=50 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name="Modifiers",HeaderText=Localization.Get("SpawnCartModifiers"),FillWeight=65,ValueType=typeof(string) });
            grid.CellFormatting+=(s,e)=> {
                if(e.ColumnIndex==0 && e.Value is string) {
                    e.Value=Localization.DisplayLocalizedText((string)e.Value);e.FormattingApplied=true;
                }
            };
            grid.CellValidating+=ValidateQuantity;
            grid.CellEndEdit+=(s,e)=> {
                if(rebuilding || e.RowIndex<0 || (e.ColumnIndex!=1 && e.ColumnIndex!=3))return;
                if(!CommitCell(e.RowIndex,e.ColumnIndex,grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value)) {
                    var entry=cart.Entries.First(item=>item.Item.Id==(string)grid.Rows[e.RowIndex].Tag);
                    grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value=e.ColumnIndex==1?entry.Quantity.ToString(CultureInfo.InvariantCulture):entry.ModifiersText;
                    return;
                }
                if(e.ColumnIndex==3)grid.Rows[e.RowIndex].Cells[3].Value=cart.Entries.First(item=>item.Item.Id==(string)grid.Rows[e.RowIndex].Tag).ModifiersText;
                Persist();RefreshPreview();
            };
            grid.SelectionChanged+=(s,e)=> { remove.Enabled=!sending && grid.SelectedRows.Count>0;if(editModifiers!=null)editModifiers.Enabled=!sending && grid.SelectedRows.Count>0; };
            var edits=new FlowLayoutPanel { Dock=DockStyle.Bottom,Height=86,WrapContents=true,Padding=new Padding(0,5,0,0) };
            remove=ActionButton("SpawnCartRemove",140,RemoveSelected);clear=ActionButton("SpawnCartClear",100,ClearCart);
            save=ActionButton("SpawnCartSave",108,SaveKit);load=ActionButton("SpawnCartLoad",108,LoadKit);
            editModifiers=ActionButton("SpawnCartEditModifiers",170,EditModifiers);editModifiers.Name="EditSpawnModifiers";editModifiers.CausesValidation=false;
            edits.Controls.AddRange(new Control[]{editModifiers,remove,clear,save,load});
            preview=new TextBox { Name="SpawnCartPreview",Dock=DockStyle.Bottom,Height=86,ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Both,WordWrap=false,BorderStyle=BorderStyle.FixedSingle,Font=new Font("Consolas",8.5f),
                AccessibleName=Localization.Get("SpawnCartPreview") };
            var previewLabel=new Label { Text=Localization.Get("SpawnCartPreview"),Dock=DockStyle.Bottom,Height=22 };
            summary=new Label { Dock=DockStyle.Bottom,Height=28,TextAlign=ContentAlignment.MiddleLeft };
            split.Panel2.Controls.Add(grid);split.Panel2.Controls.Add(title);split.Panel2.Controls.Add(summary);split.Panel2.Controls.Add(edits);
            split.Panel2.Controls.Add(previewLabel);split.Panel2.Controls.Add(preview);

            var footer=new TableLayoutPanel { Dock=DockStyle.Bottom,Height=88,Padding=new Padding(14,6,14,10),ColumnCount=2,RowCount=2 };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,180));
            footer.RowStyles.Add(new RowStyle(SizeType.Absolute,25));footer.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            var source=new LinkLabel { Text=Localization.Get("SpawnCartSource"),AutoSize=true,LinkColor=Color.SkyBlue,ActiveLinkColor=OverlayTheme.Accent };
            source.LinkClicked+=(s,e)=>Process.Start(new ProcessStartInfo("https://scum-db.com/") { UseShellExecute=true });
            status=new Label { Name="SpawnCartStatus",Dock=DockStyle.Fill,Text=Localization.Get("SpawnCartSelect"),Padding=new Padding(0,5,8,0) };
            submit=new Button { Text=Localization.Get("SpawnCartSend"),Width=170,Height=42,Name="SendSpawnCart",Dock=DockStyle.Bottom,AutoEllipsis=true,CausesValidation=false,Font=new Font("Segoe UI",10,FontStyle.Bold) };
            submit.Click+=(s,e)=>QueueSendCart();
            footer.Controls.Add(source,0,0);footer.Controls.Add(status,0,1);footer.Controls.Add(submit,1,0);footer.SetRowSpan(submit,2);
            var tips=new ToolTip { AutoPopDelay=20000 };tips.SetToolTip(submit,Localization.Get("SpawnCartUnconfirmed"));
            tips.SetToolTip(searchHint,searchHint.Text);tips.SetToolTip(status,status.Text);tips.SetToolTip(help,help.Text);
            status.TextChanged+=(s,e)=>tips.SetToolTip(status,status.Text);help.TextChanged+=(s,e)=>tips.SetToolTip(help,help.Text);Disposed+=(s,e)=>tips.Dispose();
            Controls.Add(split);Controls.Add(header);Controls.Add(footer);
            AttachCopyMenus();
            OverlayTheme.Style(this);StyleLists();submit.BackColor=OverlayTheme.Accent;submit.ForeColor=FullMapSidebarStyle.Background;title.ForeColor=OverlayTheme.Accent;
            source.LinkColor=Color.SkyBlue;heading.ForeColor=OverlayTheme.Accent;help.ForeColor=searchHint.ForeColor=matches.ForeColor=OverlayTheme.InkMuted;
            selectedMatches.ForeColor=OverlayTheme.Accent;categoryTree.BackColor=FullMapSidebarStyle.Background;categoryTree.ForeColor=OverlayTheme.Ink;
            split.BackColor=FullMapSidebarStyle.Background;preview.BackColor=OverlayTheme.Surface;emptyResults.BackColor=FullMapSidebarStyle.ButtonSurface;emptyResults.ForeColor=OverlayTheme.InkMuted;
            PerformLayout();split.SplitterDistance=Math.Max(split.Panel1MinSize,Math.Min(split.Width-split.Panel2MinSize-split.SplitterWidth,(int)(split.Width*0.60)));
            search.TextChanged+=(s,e)=>FilterItems();categoryTree.AfterSelect+=(s,e)=>FilterItems();
            KeyDown+=(s,e)=> { if(e.Control && e.KeyCode==Keys.F && !sending) { search.Focus();search.SelectAll();e.Handled=true;e.SuppressKeyPress=true; } };
            FormClosing+=(s,e)=> { if(sending && e.CloseReason==CloseReason.UserClosing) { e.Cancel=true;status.Text=Localization.Get("SpawnCartCancelClose"); } };
            Shown+=(s,e)=>search.Focus();
            try { kitDatabase.LoadCart(cart,cartPath); }catch(Exception ex) { status.Text=Localization.T("SpawnCartFileError",ex.Message); }
            FilterItems();RefreshCart();
            BindLabels(this);
            ApplyLanguageLayout();
            Localization.LanguageChanged+=RefreshLanguage;
            Disposed+=(s,e)=>Localization.LanguageChanged-=RefreshLanguage;
        }
        void ApplyLanguageLayout() {
            bool arabic=Localization.Current==AppLanguage.Arabic;
            RightToLeft=arabic?RightToLeft.Yes:RightToLeft.No;
            // Commands, IDs, coordinates and modifier values retain LTR editing
            // and copying. Translated names and category headings use RTL reading.
            preview.RightToLeft=search.RightToLeft=quantity.RightToLeft=grid.RightToLeft=RightToLeft.No;
            categoryTree.RightToLeft=items.RightToLeft=arabic?RightToLeft.Yes:RightToLeft.No;
            grid.Columns[0].DefaultCellStyle.Alignment=arabic?DataGridViewContentAlignment.MiddleRight:DataGridViewContentAlignment.MiddleLeft;
            items.Invalidate();grid.Invalidate();
        }
        static TreeNode CategoryNode(SpawnCategoryNode category) {
            string label=category.Label;
            switch(category.Key) {
                case "group/weapons":label=Localization.Get("SpawnCartGroupWeapons");break;
                case "group/clothing":label=Localization.Get("SpawnCartGroupClothing");break;
                case "group/survival":label=Localization.Get("SpawnCartGroupSurvival");break;
                case "group/crafting":label=Localization.Get("SpawnCartGroupCrafting");break;
                case "group/vehicles":label=Localization.Get("SpawnCartGroupVehicles");break;
                case "group/other":label=Localization.Get("SpawnCartGroupOther");break;
            }
            var node=new TreeNode(label+" ("+category.Items.Count+")") { Tag=category,ToolTipText=label+" ("+category.Items.Count+")" };
            foreach(var child in category.Children)node.Nodes.Add(CategoryNode(child));
            return node;
        }
        internal void SetChatBinding(string binding) {
            chatBinding=binding;
            help.Text=Localization.Get("SpawnCartLimits")+"   "+Localization.T("SpawnCartChatBinding",binding);
        }
        internal void SetDiagnostics(Action<string,int> callback) { diagnostics=callback; }
        internal void SetModifierEditor(Func<SpawnModifierDialog,DialogResult> callback) { modifierEditor=callback; }
        internal void SetKitEditor(Func<SpawnKitDialog,DialogResult> callback) { kitEditor=callback; }
        internal void SetKitOverwriteConfirmation(Func<string,bool> callback) { kitOverwriteConfirmation=callback; }
        internal void SetLegacyKitPicker(Func<string> callback) { legacyKitPicker=callback; }
        void Trace(string stage,int count=0) {
            // Use the cart's own writable folder as well as the shared diagnostics.
            // This records preparation failures even if shared state logging is unavailable.
            try {
                string path=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(cartPath)),"spawn-cart.log");
                if(!File.Exists(path) || new FileInfo(path).Length<1048576)File.AppendAllText(path,
                    DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture)+" stage="+stage+" count="+count.ToString(CultureInfo.InvariantCulture)+Environment.NewLine);
            }catch(IOException) {}catch(UnauthorizedAccessException) {}
            if(diagnostics!=null)try { diagnostics(stage,count); }catch(Exception) {}
        }
        void QueueSendCart() {
            Trace("clicked",cart.Entries.Count);
            if(sending || sendQueued) { Trace("already-sending",cart.Entries.Count);return; }
            // Finish the button's mouse-up/validation event before handing keyboard focus to SCUM.
            sendQueued=true;
            BeginInvoke(new Action(async()=> { sendQueued=false;if(!IsDisposed)await SendCart(); }));
        }
        Button ActionButton(string key,int width,Action action) {
            var button=new Button { Text=Localization.Get(key),Width=width,Height=32,AutoEllipsis=true };
            button.Click+=(s,e)=> { if(!sending)action(); };return button;
        }
        void StyleLists() {
            items.BackColor=FullMapSidebarStyle.ButtonSurface;items.ForeColor=Color.WhiteSmoke;
            grid.BackgroundColor=FullMapSidebarStyle.ButtonSurface;grid.GridColor=FullMapSidebarStyle.Border;
            grid.DefaultCellStyle.BackColor=FullMapSidebarStyle.ButtonSurface;grid.DefaultCellStyle.ForeColor=Color.WhiteSmoke;
            grid.DefaultCellStyle.SelectionBackColor=FullMapSidebarStyle.SelectedSurface;grid.DefaultCellStyle.SelectionForeColor=OverlayTheme.AccentHover;
            grid.ColumnHeadersDefaultCellStyle.BackColor=FullMapSidebarStyle.Background;grid.ColumnHeadersDefaultCellStyle.ForeColor=Color.WhiteSmoke;
            grid.RowTemplate.Height=28;
            grid.Columns[0].MinimumWidth=80;grid.Columns[1].MinimumWidth=72;
            grid.Columns[2].MinimumWidth=80;grid.Columns[3].MinimumWidth=90;
            grid.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(26,32,38);
            grid.DefaultCellStyle.Padding=new Padding(5,2,5,2);
            grid.ColumnHeadersHeight=32;grid.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.Columns[1].DefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleCenter;
            grid.Columns[2].DefaultCellStyle.Font=new Font("Consolas",8);grid.Columns[3].DefaultCellStyle.Font=new Font("Consolas",8);
        }
        void UpdateResultSelection() {
            int count=items.SelectedResultIndices.Count();
            if(selectedMatches!=null)selectedMatches.Text=Localization.T("SpawnCartSelectedItems",count);
            if(add!=null)add.Enabled=!sending && count>0;
        }
        void RefreshResultState() {
            emptyResults.Visible=filtered.Count==0;items.Visible=filtered.Count>0;
            clearSearch.Enabled=!sending && search.Text.Length>0;
            expandGroups.Enabled=collapseGroups.Enabled=!sending && items.GroupKeys.Count>0;
            UpdateResultSelection();
        }
        void FilterItems() {
            items.SelectedIndices.Clear();
            var selected=categoryTree.SelectedNode==null?null:categoryTree.SelectedNode.Tag as SpawnCategoryNode;
            var results=catalog.SearchWithRelations(search.Text,item=>selected==null || selected.Contains(item));
            searchMatches.Clear();foreach(var match in results)searchMatches.Add(match.Item.Id,match);
            filtered=results.Select(match=>match.Item).ToList();
            items.Populate(results,CompatibilityLabel);
            matches.Text=Localization.T("SpawnCartMatches",filtered.Count,catalog.Items.Count);add.Enabled=false;
            RefreshResultState();
        }
        static string CompatibilityLabel(SpawnItemSearchResult match) {
            if(match.RelatedWeapons.Count==0)return "";
            string names=string.Join(", ",match.RelatedWeapons.Select(weapon=>weapon.DisplayName));
            return Localization.T(match.RequiresAdapter?"SpawnCartCompatibilityAdapter":"SpawnCartCompatibilityFor",names);
        }
        bool FinishEdit() {
            // EndEdit alone need not raise CellValidating while focus stays inside the grid.
            // Validate the active editor before any save, removal or command snapshot.
            if(grid.IsCurrentCellInEditMode && grid.CurrentCell!=null && (grid.CurrentCell.ColumnIndex==1 || grid.CurrentCell.ColumnIndex==3)) {
                object value=grid.EditingControl==null?grid.CurrentCell.EditedFormattedValue:grid.EditingControl.Text;
                if(!CommitCell(grid.CurrentCell.RowIndex,grid.CurrentCell.ColumnIndex,value))return false;
            }
            return grid.EndEdit();
        }
        void AddSelected() {
            var selected=items.SelectedResultIndices;
            if(!FinishEdit() || selected.Count==0)return;
            try { cart.AddMany(selected.Select(index=>filtered[index].Id),(int)quantity.Value);Persist();RefreshCart(); }
            catch(FormatException ex) { status.Text=ex.Message; }
        }
        void ValidateQuantity(object sender,DataGridViewCellValidatingEventArgs e) {
            if(rebuilding || (e.ColumnIndex!=1 && e.ColumnIndex!=3) || e.RowIndex<0)return;
            e.Cancel=!CommitCell(e.RowIndex,e.ColumnIndex,e.FormattedValue);
        }
        bool CommitCell(int rowIndex,int columnIndex,object value) { return columnIndex==1?CommitQuantity(rowIndex,value):CommitModifiers(rowIndex,value); }
        bool CommitModifiers(int rowIndex,object formattedValue) {
            try {
                cart.SetModifiers((string)grid.Rows[rowIndex].Tag,Convert.ToString(formattedValue,CultureInfo.InvariantCulture));
                grid.Rows[rowIndex].ErrorText="";return true;
            }catch(FormatException ex) { grid.Rows[rowIndex].ErrorText=ex.Message;status.Text=ex.Message;return false; }
        }
        bool CommitQuantity(int rowIndex,object formattedValue) {
            int value;
            try {
                if(!int.TryParse(Convert.ToString(formattedValue,CultureInfo.InvariantCulture),NumberStyles.None,CultureInfo.InvariantCulture,out value))
                    throw new FormatException(Localization.Get("SpawnCartInvalid"));
                cart.SetQuantity((string)grid.Rows[rowIndex].Tag,value);grid.Rows[rowIndex].ErrorText="";return true;
            }catch(FormatException ex) { grid.Rows[rowIndex].ErrorText=ex.Message;status.Text=ex.Message;return false; }
        }
        void AttachCopyMenus() {
            var resultMenu=new ContextMenuStrip();var cartMenu=new ContextMenuStrip();
            Action<ContextMenuStrip,string,Func<string>> copy=(menu,key,value)=> {
                var item=new ToolStripMenuItem(Localization.Get(key));
                item.Click+=(s,e)=> { if(sending || !FinishEdit())return;try { var text=value();if(!string.IsNullOrEmpty(text)) { Clipboard.SetText(text);status.Text=Localization.Get("AdminCopied"); } }catch(Exception error) { status.Text=error.Message; } };menu.Items.Add(item);
            };
            Func<IEnumerable<SpawnItem>> selectedItems=()=>items.SelectedResultIndices.Select(index=>filtered[index]);
            copy(resultMenu,"AdminCopyId",()=>string.Join(Environment.NewLine,selectedItems().Select(item=>item.Id)));
            copy(resultMenu,"AdminCopyName",()=>string.Join(Environment.NewLine,selectedItems().Select(item=>item.DisplayName)));
            copy(resultMenu,"AdminCopyCommand",()=>string.Join(Environment.NewLine,selectedItems().Select(item=>"#SpawnItem "+item.Id+" "+quantity.Value.ToString(CultureInfo.InvariantCulture))));
            var addItem=new ToolStripMenuItem(add.Text);addItem.Click+=(s,e)=> { if(!sending)AddSelected(); };resultMenu.Items.Add(addItem);
            resultMenu.Opening+=(s,e)=>e.Cancel=sending || items.SelectedResultIndices.Count==0;
            items.MouseDown+=(s,e)=> { if(e.Button!=MouseButtons.Right)return;var hit=items.HitTest(e.Location).Item;if(hit==null) { items.SelectedIndices.Clear();return; }if(!hit.Selected) { items.SelectedIndices.Clear();hit.Selected=true; } };
            Func<IEnumerable<SpawnCartEntry>> selectedEntries=()=> { var ids=new HashSet<string>(grid.SelectedRows.Cast<DataGridViewRow>().Select(row=>(string)row.Tag),StringComparer.OrdinalIgnoreCase);return cart.Entries.Where(entry=>ids.Contains(entry.Item.Id)); };
            copy(cartMenu,"AdminCopyId",()=>string.Join(Environment.NewLine,selectedEntries().Select(entry=>entry.Item.Id)));
            copy(cartMenu,"AdminCopyName",()=>string.Join(Environment.NewLine,selectedEntries().Select(entry=>entry.Item.DisplayName)));
            copy(cartMenu,"AdminCopyCommand",()=>string.Join(Environment.NewLine,selectedEntries().Select(entry=>entry.Command)));
            copy(cartMenu,"AdminCopyDetails",()=>string.Join(Environment.NewLine,selectedEntries().Select(entry=>entry.Item.DisplayName+Environment.NewLine+entry.Command)));
            var editItem=new ToolStripMenuItem(editModifiers.Text);editItem.Click+=(s,e)=>EditModifiers();cartMenu.Items.Add(editItem);
            var removeItem=new ToolStripMenuItem(remove.Text);removeItem.Click+=(s,e)=> { if(!sending)RemoveSelected(); };cartMenu.Items.Add(removeItem);
            cartMenu.Opening+=(s,e)=>e.Cancel=sending || grid.SelectedRows.Count==0;
            grid.MouseDown+=(s,e)=> { if(e.Button!=MouseButtons.Right)return;var hit=grid.HitTest(e.X,e.Y);if(hit.RowIndex<0) { grid.ClearSelection();return; }if(!grid.Rows[hit.RowIndex].Selected) { grid.ClearSelection();grid.Rows[hit.RowIndex].Selected=true; } };
            items.ContextMenuStrip=resultMenu;grid.ContextMenuStrip=cartMenu;OverlayTheme.Menu(resultMenu);OverlayTheme.Menu(cartMenu);
            Disposed+=(s,e)=> { resultMenu.Dispose();cartMenu.Dispose(); };
        }
        void RemoveSelected() {
            if(!FinishEdit())return;
            foreach(var row in grid.SelectedRows.Cast<DataGridViewRow>())cart.Remove((string)row.Tag);
            Persist();RefreshCart();
        }
        void EditModifiers() {
            if(sending || !FinishEdit() || grid.SelectedRows.Count==0)return;
            var ids=new HashSet<string>(grid.SelectedRows.Cast<DataGridViewRow>().Select(row=>(string)row.Tag),StringComparer.OrdinalIgnoreCase);
            var entries=cart.Entries.Where(entry=>ids.Contains(entry.Item.Id)).ToList();
            string title=entries.Count==1?entries[0].Item.DisplayName:Localization.T("SpawnCartModifierSelection",entries.Count);
            using(var dialog=new SpawnModifierDialog(title,entries.Select(entry=>entry.ModifiersText).ToList(),entries.Count>1)) {
                if((modifierEditor==null?RunDialogInteraction(()=>dialog.ShowDialog(this)):modifierEditor(dialog))!=DialogResult.OK || dialog.Changes==null || dialog.Changes.Count==0)return;
                try { cart.PatchModifiers(ids,dialog.Changes); }
                catch(FormatException ex) { status.Text=ex.Message;return; }
                bool saved=Persist();RefreshCart();grid.ClearSelection();
                foreach(DataGridViewRow row in grid.Rows)row.Selected=ids.Contains((string)row.Tag);
                if(saved)status.Text=Localization.T("SpawnCartModifierApplied",entries.Count);
            }
        }
        void ClearCart() {
            if(!FinishEdit() || cart.Entries.Count==0)return;
            if(RunDialogInteraction(()=>MessageBox.Show(this,Localization.Get("SpawnCartClearConfirm"),Text,MessageBoxButtons.YesNo,MessageBoxIcon.Question))!=DialogResult.Yes)return;
            cart.Clear();Persist();RefreshCart();
        }
        bool Persist() {
            try { kitDatabase.SaveCart(cart);return true; }catch(Exception ex) { status.Text=Localization.T("SpawnCartFileError",ex.Message);return false; }
        }
        void RefreshCart() {
            rebuilding=true;
            try {
                grid.Rows.Clear();
                foreach(var entry in cart.Entries) { int row=grid.Rows.Add(entry.Item.DisplayName,entry.Quantity.ToString(CultureInfo.InvariantCulture),entry.Item.Id,entry.ModifiersText);grid.Rows[row].Tag=entry.Item.Id; }
            }finally { rebuilding=false; }
            RefreshPreview();
        }
        void RefreshPreview() {
            preview.Text=string.Join(Environment.NewLine,cart.BuildCommands());summary.Text=Localization.T("SpawnCartSummary",cart.Entries.Count,cart.TotalQuantity);
            submit.Enabled=clear.Enabled=save.Enabled=cart.Entries.Count>0 && !sending;remove.Enabled=!sending && grid.SelectedRows.Count>0;
            editModifiers.Enabled=!sending && grid.SelectedRows.Count>0;
        }
        void SaveKit() {
            if(!FinishEdit())return;
            try { SaveNamedKit(cart,null,false); }catch(Exception ex) { status.Text=Localization.T("SpawnCartFileError",ex.Message); }
        }
        void LoadKit() {
            if(!FinishEdit())return;
            try {
                using(var dialog=new SpawnKitDialog(false,kitDatabase.ListKits(),null)) {
                    if(ShowKitEditor(dialog)!=DialogResult.OK)return;
                    if(dialog.ImportRequested) { ImportLegacyKit();return; }
                    var loaded=new SpawnCart(catalog);kitDatabase.LoadKit(dialog.KitId,loaded);
                    kitDatabase.SaveCart(loaded);cart.ReplaceWith(loaded);RefreshCart();
                    status.Text=Localization.T("SpawnCartKitLoaded",dialog.KitName);
                }
            }catch(Exception ex) { status.Text=Localization.T("SpawnCartFileError",ex.Message); }
        }
        DialogResult ShowKitEditor(SpawnKitDialog dialog) { return kitEditor==null?RunDialogInteraction(()=>dialog.ShowDialog(this)):kitEditor(dialog); }
        void SaveNamedKit(SpawnCart source,string defaultName,bool imported) {
            var kits=kitDatabase.ListKits();
            using(var dialog=new SpawnKitDialog(true,kits,defaultName)) {
                if(ShowKitEditor(dialog)!=DialogResult.OK)return;
                bool exists=kits.Any(kit=>kit.Name.Normalize(System.Text.NormalizationForm.FormC).ToUpperInvariant()==dialog.KitName.ToUpperInvariant());
                if(exists && !(kitOverwriteConfirmation==null?
                    RunDialogInteraction(()=>MessageBox.Show(this,Localization.T("SpawnCartKitReplace",dialog.KitName),Localization.Get("SpawnCartSave"),MessageBoxButtons.YesNo,MessageBoxIcon.Question))==DialogResult.Yes:
                    kitOverwriteConfirmation(dialog.KitName)))return;
                kitDatabase.SaveKit(dialog.KitName,source,exists);
                status.Text=Localization.T(imported?"SpawnCartKitImported":"SpawnCartKitSaved",dialog.KitName);
            }
        }
        void ImportLegacyKit() {
            string path=null;
            if(legacyKitPicker!=null)path=legacyKitPicker();
            else using(var dialog=new OpenFileDialog { Filter=Localization.Get("SpawnCartKitFilter"),CheckFileExists=true }) {
                if(RunDialogInteraction(()=>dialog.ShowDialog(this))==DialogResult.OK)path=dialog.FileName;
            }
            if(string.IsNullOrEmpty(path))return;
            var imported=new SpawnCart(catalog);imported.Load(path);
            SaveNamedKit(imported,Path.GetFileNameWithoutExtension(path),true);
        }
        internal async Task SendCart() {
            Trace("start",cart.Entries.Count);
            if(sending) { Trace("already-sending",cart.Entries.Count);return; }
            if(!FinishEdit()) { Trace("invalid-edit",cart.Entries.Count);return; }
            var commands=cart.BuildCommands();
            if(commands.Count==0) { Trace("empty");status.Text=Localization.Get("SpawnCartEmpty");return; }
            if(!Persist()) { Trace("save-failed",commands.Count);return; }
            IntPtr originalGame=Native.AdminGameWindow();
            sending=true;int consumed=0;string persistenceError=null;
            status.Text=Localization.Get("SpawnCartSendingHelp");
            search.Enabled=categoryTree.Enabled=items.Enabled=quantity.Enabled=grid.Enabled=add.Enabled=load.Enabled=false;RefreshPreview();
            clearSearch.Enabled=expandGroups.Enabled=collapseGroups.Enabled=false;
            try {
                Trace("transport-start",commands.Count);
                var result=await send(commands,this,count=> {
                    int delta=count-consumed;
                    if(delta>0) { cart.ConsumeSubmitted(delta);consumed=count; }
                    if(!IsDisposed)status.Text=Localization.T("SpawnCartSubmittedStatus",count,commands.Count);
                    // Keep dispatched entries out of retries even if cleanup is interrupted.
                    try { kitDatabase.SaveCart(cart); }catch(Exception ex) {
                        persistenceError=Localization.Get("SpawnCartSaveStopped");
                        throw new IOException(persistenceError,ex);
                    }
                });
                Trace("result/"+(result.StopReason??"unknown"),result.SubmittedCount);
                if(!IsDisposed)status.Text=result.Completed?Localization.T("SpawnCartSubmitted",result.SubmittedCount):
                    (result.SubmittedCount==0?Localization.T("SpawnCartNotSent",result.Error??Localization.Get("SpawnCartBeforeSubmitStopped")):
                    Localization.T("SpawnCartInterrupted",result.SubmittedCount,commands.Count,result.Error??""));
            }catch(Exception ex) { Trace("exception",consumed);if(!IsDisposed)status.Text=consumed==0?Localization.T("SpawnCartNotSent",ex.Message):Localization.T("SpawnCartInterrupted",consumed,commands.Count,ex.Message); }
            finally {
                Trace("finished",consumed);
                sending=false;
                if(!IsDisposed) {
                    if(persistenceError!=null && status.Text.IndexOf(persistenceError,StringComparison.Ordinal)<0)status.Text+=" "+persistenceError;
                    search.Enabled=categoryTree.Enabled=items.Enabled=quantity.Enabled=grid.Enabled=load.Enabled=true;RefreshCart();RefreshResultState();
                    // Do not steal focus from another app if the user interrupted by switching away.
                    IntPtr foreground=Native.GetForegroundWindow();
                    if(RestoreAfterSend!=null)RestoreAfterSend(this,originalGame);
                    else if(MapWindow.CanRestoreAdminTool(foreground,originalGame,Native.IsOurWindow(foreground))) { WindowState=FormWindowState.Normal;TopMost=true;Show();BringToFront();Activate(); }
                }
            }
        }
    }

    public sealed partial class MapWindow {
        SpawnCartDialog spawnCartDialog;
        internal void ShowSpawnCart() {
            if(!adminToolsEnabled || closing || adminCommandBusy || relativeTeleportBusy)return;
            PrepareUiInput();
            if(spawnCartDialog==null || spawnCartDialog.IsDisposed) {
                try { spawnCartDialog=new SpawnCartDialog(SpawnItemCatalog.LoadBundled(),Path.Combine(dataFolder,"spawn-cart.tsv"),RunAdminSpawnCart); }
                catch(Exception ex) { RunUiModal(() => MessageBox.Show(this,Localization.T("SpawnCartCatalogError",ex.Message)));return; }
                var dialog=spawnCartDialog;Disposed+=(s,e)=>dialog.Dispose();
                ProtectToolWindow(dialog);
            }
            spawnCartDialog.SetChatBinding(PhysicalKeyCapture.KeyName(scumChatKey));
            spawnCartDialog.ModalInteraction=show=>RunUiModal(show);
            spawnCartDialog.RestoreAfterSend=RestoreAdminTool;
            spawnCartDialog.SetDiagnostics((stage,count)=>LogAdminCommandStatus("cart-ui",stage,count,0,IntPtr.Zero));
            LogAdminCommandStatus("cart-ui","opened",0,0,IntPtr.Zero);
            spawnCartDialog.Show();
            if(!diagnosticMode) { spawnCartDialog.BringToFront();spawnCartDialog.Activate();Native.ForceForeground(spawnCartDialog.Handle); }
        }
    }
}
