using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ScumMiniMap {
    internal sealed class SpawnCategoryNode {
        readonly HashSet<string> itemIds;
        internal string Key { get; private set; }
        internal string Label { get; private set; }
        internal IList<SpawnItem> Items { get; private set; }
        internal IList<SpawnCategoryNode> Children { get; private set; }
        internal bool IsLeaf { get { return Children.Count==0; } }

        internal SpawnCategoryNode(string key,string label,List<SpawnItem> items,List<SpawnCategoryNode> children) {
            Key=key;Label=label;Items=items.AsReadOnly();Children=children.AsReadOnly();
            itemIds=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var item in items)itemIds.Add(item.Id);
        }

        internal bool Contains(SpawnItem item) { return item!=null && itemIds.Contains(item.Id); }
    }

    // Groups are navigation labels. Category and subtype membership come only from source metadata.
    internal static class SpawnItemCategories {
        sealed class Builder {
            internal readonly string Key,Label;
            internal readonly List<SpawnItem> Items=new List<SpawnItem>();
            readonly Dictionary<string,Builder> children=new Dictionary<string,Builder>(StringComparer.OrdinalIgnoreCase);
            internal Builder(string key,string label) { Key=key;Label=label; }
            internal Builder Child(string key,string label) {
                Builder child;
                if(!children.TryGetValue(key,out child)) { child=new Builder(key,label);children.Add(key,child); }
                return child;
            }
            internal SpawnCategoryNode Freeze() {
                var nodes=new List<SpawnCategoryNode>();
                foreach(var child in children.Values)nodes.Add(child.Freeze());
                nodes.Sort((left,right)=>StringComparer.OrdinalIgnoreCase.Compare(left.Label,right.Label));
                return new SpawnCategoryNode(Key,Label,Items,nodes);
            }
        }

        internal static IList<SpawnCategoryNode> Build(SpawnItemCatalog catalog) {
            if(catalog==null)throw new ArgumentNullException("catalog");
            string[] groupIds={"weapons","clothing","survival","crafting","vehicles","other"};
            string[] groupLabels={"Weapons","Clothing and armour","Food, health and survival","Equipment and crafting","Vehicle parts","Other items"};
            var groups=new Dictionary<string,Builder>(StringComparer.OrdinalIgnoreCase);
            for(int i=0;i<groupIds.Length;i++)groups.Add(groupIds[i],new Builder("group/"+groupIds[i],groupLabels[i]));
            foreach(var item in catalog.Items) {
                string category=item.Category.ToLowerInvariant();
                if(category=="melee_weapon")category="weapon_melee";
                string groupId=Group(category);
                var group=groups[groupId];
                string categoryKey=groupId+"/"+Uri.EscapeDataString(category);
                var categoryNode=group.Child("category/"+categoryKey,CategoryLabel(category));
                string subcategory=string.IsNullOrEmpty(item.Subcategory)?"general":item.Subcategory;
                var subtypeNode=categoryNode.Child("subcategory/"+categoryKey+"/"+Uri.EscapeDataString(subcategory),SubcategoryLabel(subcategory));
                group.Items.Add(item);categoryNode.Items.Add(item);subtypeNode.Items.Add(item);
            }
            var result=new List<SpawnCategoryNode>();
            foreach(string id in groupIds)if(groups[id].Items.Count>0)result.Add(groups[id].Freeze());
            return result.AsReadOnly();
        }

        static string Group(string category) {
            switch(category) {
                case "weapon_ranged":case "weapon_melee":case "ammunition":case "magazine":
                case "attachment":case "weapon_part":case "throwable":return "weapons";
                case "clothing":case "armor":return "clothing";
                case "consumable":case "farming":case "fishing":case "fishing_attachment":case "hunting":return "survival";
                case "equipment":case "material":case "buildable":case "fortification":case "book":return "crafting";
                case "vehicle_part":return "vehicles";
                default:return "other";
            }
        }

        internal static string CategoryLabel(string category) {
            return Localization.GetSpawnCategoryName(category,CategoryLabelSource(category));
        }
        static string CategoryLabelSource(string category) {
            switch(category) {
                case "weapon_ranged":return "Ranged weapons";
                case "weapon_melee":return "Melee weapons";
                case "ammunition":return "Ammunition";
                case "magazine":return "Magazines";
                case "attachment":return "Weapon attachments";
                case "weapon_part":return "Weapon parts";
                case "throwable":return "Throwables and traps";
                case "armor":return "Armour";
                case "consumable":return "Food, drinks and medicine";
                case "fishing_attachment":return "Fishing attachments";
                case "material":return "Materials";
                case "buildable":return "Base expansion items";
                case "fortification":return "Fortification items";
                case "book":return "Books";
                case "vehicle_part":return "Vehicle parts";
                case "player_modification":return "Player modifications";
                case "seasonal_event":return "Seasonal items";
                case "other":return "Other items";
                default:return Humanize(category);
            }
        }

        internal static string SubcategoryLabel(string value) {
            return Localization.GetSpawnCategoryName(value,SubcategoryLabelSource(value));
        }
        static string SubcategoryLabelSource(string value) {
            switch(value.ToLowerInvariant()) {
                case "general":return "General";
                case "assault_rifle":return "Assault rifles";
                case "pistol":return "Pistols";
                case "revolver":return "Revolvers";
                case "smg":return "Submachine guns";
                case "lmg":return "Light machine guns";
                case "shotgun":return "Shotguns";
                case "sniper":return "Sniper rifles";
                case "single_shot":return "Single shot rifles";
                case "bow":return "Bows";
                case "heavy":return "Heavy weapons";
                case "axe":return "Axes";
                case "blunt":return "Blunt weapons";
                case "knife":return "Knives";
                case "spear":return "Spears";
                case "sword":return "Swords";
                case "food":return "Food";
                case "medical":return "Medicine";
                case "beverage":return "Drinks";
                case "alcoholic":return "Alcoholic drinks";
                case "soft_drink":return "Soft drinks";
                case "seed":return "Seeds";
                case "animal_part":return "Animal parts";
                case "backpack":return "Backpacks";
                case "body_armor":case "armor":return "Armour";
                case "tool":return "Tools";
                case "repair_kit":return "Repair kits";
                case "craft_station":return "Crafting stations";
                case "fire_starter":return "Fire starters";
                case "miscellaneous":return "Miscellaneous";
                case "scope":return "Scopes";
                case "sight":return "Sights";
                case "rail":return "Rails";
                case "suppressor":return "Suppressors";
                case "bayonet":return "Bayonets";
                case "flashlight":return "Flashlights";
                case "flash_hider":return "Flash hiders";
                case "ghillie":return "Ghillie wraps";
                case "stabilizer":return "Stabilizers";
                default:return Humanize(value);
            }
        }

        static string Humanize(string value) {
            string label=value.Replace('_',' ');
            foreach(char c in label)if(char.IsDigit(c))return label;
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(label);
        }
    }

    internal sealed class SpawnResultGroup {
        internal string Key { get; private set; }
        internal string Label { get; private set; }
        internal string Category { get; private set; }
        internal string Subcategory { get; private set; }
        internal IList<SpawnItemSearchResult> Matches { get; private set; }

        internal SpawnResultGroup(string key,string label,string category,string subcategory,List<SpawnItemSearchResult> matches) {
            Key=key;Label=label;Category=category;Subcategory=subcategory;Matches=matches.AsReadOnly();
        }
    }

    // Result headings use the same explicit source metadata as the category tree.
    // For example, an adapter whose source subtype is "scope" stays in Scopes.
    internal static class SpawnResultGroups {
        internal static string Key(SpawnItem item) {
            string category=item.Category.ToLowerInvariant();
            if(category=="melee_weapon")category="weapon_melee";
            return category+"/"+Uri.EscapeDataString(string.IsNullOrEmpty(item.Subcategory)?"general":item.Subcategory.ToLowerInvariant());
        }
        internal static IList<SpawnResultGroup> Build(IList<SpawnItemSearchResult> matches) {
            if(matches==null)throw new ArgumentNullException("matches");
            var grouped=new Dictionary<string,List<SpawnItemSearchResult>>(StringComparer.OrdinalIgnoreCase);
            foreach(var match in matches) {
                string key=Key(match.Item);List<SpawnItemSearchResult> rows;
                if(!grouped.TryGetValue(key,out rows)) { rows=new List<SpawnItemSearchResult>();grouped.Add(key,rows); }
                rows.Add(match);
            }
            var result=new List<SpawnResultGroup>();
            foreach(var pair in grouped) {
                var item=pair.Value[0].Item;string category=item.Category.ToLowerInvariant();
                if(category=="melee_weapon")category="weapon_melee";
                string subtype=string.IsNullOrEmpty(item.Subcategory)?"general":item.Subcategory;
                string label=SpawnItemCategories.CategoryLabel(category);
                if(subtype!="general")label+=" · "+SpawnItemCategories.SubcategoryLabel(subtype);
                result.Add(new SpawnResultGroup(pair.Key,label,category,subtype,pair.Value));
            }
            result.Sort((left,right)=> {
                int order=CategoryOrder(left.Category).CompareTo(CategoryOrder(right.Category));
                if(order!=0)return order;
                order=StringComparer.OrdinalIgnoreCase.Compare(left.Category,right.Category);
                if(order!=0)return order;
                order=SubtypeOrder(left.Category,left.Subcategory).CompareTo(SubtypeOrder(right.Category,right.Subcategory));
                return order!=0?order:StringComparer.OrdinalIgnoreCase.Compare(left.Label,right.Label);
            });
            return result.AsReadOnly();
        }
        static int CategoryOrder(string category) {
            switch(category) {
                case "weapon_ranged":return 0;
                case "weapon_melee":return 1;
                case "ammunition":return 2;
                case "magazine":return 3;
                case "attachment":return 4;
                case "weapon_part":return 5;
                case "throwable":return 6;
                default:return 7;
            }
        }
        static int SubtypeOrder(string category,string subtype) {
            if(category!="attachment")return 0;
            switch(subtype.ToLowerInvariant()) {
                case "rail":return 0;
                case "scope":return 1;
                case "sight":return 2;
                case "suppressor":return 3;
                default:return 4;
            }
        }
    }

    // Native groups preserve ListView's multi-selection and non-selectable headers.
    // LVGROUP is the documented commctrl.h layout, including its Vista fields:
    // https://learn.microsoft.com/windows/win32/api/commctrl/ns-commctrl-lvgroup
    internal sealed class SpawnResultsListView:ListView {
        const int LvmGetItem=0x1000+75,LvmGetGroupInfo=0x1000+149,LvmSetGroupInfo=0x1000+147,LvmGetGroupRect=0x1000+98;
        const uint LvgfState=0x04,LvgsCollapsed=0x01,LvgsCollapsible=0x08,LvgsFocused=0x10;
        readonly HashSet<string> collapsed=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string,int> nativeGroupIds=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        bool populating;

        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
        struct NativeGroup {
            internal uint cbSize,mask;
            internal IntPtr pszHeader;
            internal int cchHeader;
            internal IntPtr pszFooter;
            internal int cchFooter,iGroupId;
            internal uint stateMask,state,uAlign;
            internal IntPtr pszSubtitle;
            internal uint cchSubtitle;
            internal IntPtr pszTask;
            internal uint cchTask;
            internal IntPtr pszDescriptionTop;
            internal uint cchDescriptionTop;
            internal IntPtr pszDescriptionBottom;
            internal uint cchDescriptionBottom;
            internal int iTitleImage,iExtendedImage,iFirstItem;
            internal uint cItems;
            internal IntPtr pszSubsetTitle;
            internal uint cchSubsetTitle;
        }
        // https://learn.microsoft.com/windows/win32/api/commctrl/ns-commctrl-lvitemw
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
        struct NativeItem {
            internal uint mask;
            internal int iItem,iSubItem;
            internal uint state,stateMask;
            internal IntPtr pszText;
            internal int cchTextMax,iImage;
            internal IntPtr lParam;
            internal int iIndent,iGroupId;
            internal uint cColumns;
            internal IntPtr puColumns,piColFmt;
            internal int iGroup;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct NativeRect { internal int left,top,right,bottom; }
        [StructLayout(LayoutKind.Sequential)]
        struct NotifyHeader { internal IntPtr hwndFrom,idFrom;internal int code; }
        [StructLayout(LayoutKind.Sequential)]
        struct CustomDraw {
            internal NotifyHeader header;
            internal uint stage;
            internal IntPtr hdc;
            internal NativeRect bounds;
            internal IntPtr itemSpec;
            internal uint itemState;
            internal IntPtr itemParam;
        }
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wParam,ref NativeGroup group);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wParam,ref NativeItem item);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wParam,ref NativeRect rectangle);

        internal SpawnResultsListView() { DoubleBuffered=true;ShowGroups=true;OwnerDraw=true; }
        internal IList<string> GroupKeys { get { return Groups.Cast<ListViewGroup>().Select(group=>group.Name).ToList().AsReadOnly(); } }
        internal IList<string> CollapsedGroupKeys {
            get { CaptureGroupStates();return GroupKeys.Where(key=>collapsed.Contains(key)).ToList().AsReadOnly(); }
        }
        internal IList<int> SelectedResultIndices {
            get { CaptureGroupStates();return SelectedIndices.Cast<int>().Where(index=>Items[index].Group!=null && !collapsed.Contains(Items[index].Group.Name)).OrderBy(index=>index).ToList().AsReadOnly(); }
        }
        internal bool IsGroupCollapsed(string key) { CaptureGroupStates();return collapsed.Contains(key); }
        internal void SetGroupsCollapsed(bool value) {
            foreach(string key in GroupKeys)SetGroupCollapsed(key,value);
        }
        internal void SetGroupCollapsed(string key,bool value) {
            ListViewGroup group=Groups[key];
            if(group==null)throw new ArgumentException("Unknown result group.","key");
            if(value)collapsed.Add(key);else collapsed.Remove(key);
            if(IsHandleCreated)ApplyGroupState(Groups.IndexOf(group),value);
            if(value)ClearHiddenSelection();
            Invalidate();
        }
        internal void Populate(IList<SpawnItemSearchResult> matches,Func<SpawnItemSearchResult,string> compatibilityLabel) {
            CaptureGroupStates();var definition=SpawnResultGroups.Build(matches);
            populating=true;BeginUpdate();
            try {
                Items.Clear();Groups.Clear();nativeGroupIds.Clear();
                foreach(var group in definition)Groups.Add(new ListViewGroup(group.Key,group.Label+" ("+group.Matches.Count.ToString(CultureInfo.InvariantCulture)+")"));
                var rows=new List<ListViewItem>(matches.Count);
                foreach(var match in matches) {
                    string related=compatibilityLabel(match);
                    rows.Add(new ListViewItem(new[]{Localization.DisplayLocalizedText(match.Item.DisplayName),Localization.DisplayLocalizedText(related),Localization.DisplayIdentifier(match.Item.Id)}) {
                        Group=Groups[SpawnResultGroups.Key(match.Item)],Tag=match,
                        ToolTipText=Localization.Get("SpawnCartId")+": "+Localization.DisplayIdentifier(match.Item.Id)+(related.Length==0?"":"\n"+related)
                    });
                }
                Items.AddRange(rows.ToArray());
                ApplyGroupStates();
            }finally { EndUpdate();populating=false; }
            ResizeColumns();
        }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e);nativeGroupIds.Clear();ApplyGroupStates(); }
        protected override void OnHandleDestroyed(EventArgs e) { CaptureGroupStates();nativeGroupIds.Clear();base.OnHandleDestroyed(e); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e);CaptureGroupStates();ClearHiddenSelection(); }
        protected override void OnKeyDown(KeyEventArgs e) {
            // Enter/Space on a heading expands it, without adding a previously selected item.
            if(IsHandleCreated && (e.KeyCode==Keys.Enter || e.KeyCode==Keys.Space)) {
                for(int index=0;index<Groups.Count;index++) {
                    var info=GroupInfo(LvgfState);info.stateMask=LvgsFocused|LvgsCollapsed;
                    int id;
                    if(TryNativeGroupId(Groups[index],out id) && SendMessage(Handle,LvmGetGroupInfo,new IntPtr(id),ref info)!=new IntPtr(-1) && (info.state&LvgsFocused)!=0) {
                        SetGroupCollapsed(Groups[index].Name,(info.state&LvgsCollapsed)==0);
                        e.Handled=true;e.SuppressKeyPress=true;return;
                    }
                }
            }
            base.OnKeyDown(e);
        }
        protected override void OnKeyUp(KeyEventArgs e) { base.OnKeyUp(e);CaptureGroupStates();ClearHiddenSelection(); }
        protected override void OnResize(EventArgs e) { base.OnResize(e);ResizeColumns(); }
        protected override void OnDrawColumnHeader(DrawListViewColumnHeaderEventArgs e) {
            using(var background=new SolidBrush(FullMapSidebarStyle.Background))e.Graphics.FillRectangle(background,e.Bounds);
            using(var border=new Pen(FullMapSidebarStyle.Border))e.Graphics.DrawLine(border,e.Bounds.Right-1,e.Bounds.Top,e.Bounds.Right-1,e.Bounds.Bottom);
            var bounds=new Rectangle(e.Bounds.X+7,e.Bounds.Y,e.Bounds.Width-10,e.Bounds.Height);
            var direction=Localization.Current==AppLanguage.Arabic?TextFormatFlags.Right|TextFormatFlags.RightToLeft:TextFormatFlags.Left;
            TextRenderer.DrawText(e.Graphics,e.Header.Text,Font,bounds,OverlayTheme.Ink,direction|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
        }
        protected override void OnDrawItem(DrawListViewItemEventArgs e) { e.DrawDefault=true; }
        protected override void OnDrawSubItem(DrawListViewSubItemEventArgs e) { e.DrawDefault=true; }
        protected override void WndProc(ref Message message) {
            if(message.Msg==0x204E && message.LParam!=IntPtr.Zero) {
                var header=(NotifyHeader)Marshal.PtrToStructure(message.LParam,typeof(NotifyHeader));
                if(header.code==-12 && header.hwndFrom==Handle) {
                    var draw=(CustomDraw)Marshal.PtrToStructure(message.LParam,typeof(CustomDraw));
                    if(draw.stage==1) { base.WndProc(ref message);message.Result=new IntPtr(message.Result.ToInt64()|0x10);return; }
                    if(draw.stage==2) { base.WndProc(ref message);PaintGroupLabels(draw.hdc);return; }
                }
            }
            base.WndProc(ref message);
        }
        void PaintGroupLabels(IntPtr device) {
            if(device==IntPtr.Zero || populating)return;
            using(var graphics=Graphics.FromHdc(device))using(var background=new SolidBrush(BackColor)) {
                foreach(ListViewGroup group in Groups) {
                    int id;if(!TryNativeGroupId(group,out id))continue;
                    // LVGGR_LABEL asks Windows for its own text extent. Retain the
                    // native chevron, divider, hit region and focus outline.
                    var heading=new NativeRect { top=2 };
                    if(SendMessage(Handle,LvmGetGroupRect,new IntPtr(id),ref heading)==IntPtr.Zero)continue;
                    var bounds=Rectangle.FromLTRB(heading.left,heading.top,heading.right,heading.bottom);
                    if(bounds.Width==0 || bounds.Height==0 || !bounds.IntersectsWith(ClientRectangle))continue;
                    graphics.FillRectangle(background,bounds);
                    var direction=Localization.Current==AppLanguage.Arabic?TextFormatFlags.Right|TextFormatFlags.RightToLeft:TextFormatFlags.Left;
                    TextRenderer.DrawText(graphics,Localization.DisplayLocalizedText(group.Header),Font,bounds,OverlayTheme.Accent,direction|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding);
                }
            }
        }
        void ResizeColumns() {
            if(Columns.Count!=3)return;
            int width=Math.Max(330,ClientSize.Width-SystemInformation.VerticalScrollBarWidth-4);
            Columns[0].Width=Math.Max(140,width*43/100);
            Columns[1].Width=Math.Max(105,width*33/100);
            Columns[2].Width=Math.Max(85,width-Columns[0].Width-Columns[1].Width);
        }
        static NativeGroup GroupInfo(uint mask) { return new NativeGroup { cbSize=(uint)Marshal.SizeOf(typeof(NativeGroup)),mask=mask,stateMask=LvgsCollapsed|LvgsCollapsible }; }
        bool TryNativeGroupId(ListViewGroup group,out int id) {
            if(nativeGroupIds.TryGetValue(group.Name,out id))return true;
            if(!IsHandleCreated || group.Items.Count==0) { id=0;return false; }
            // WinForms also inserts a native default group, so managed and native
            // group indices differ. Ask a real member for its authoritative group ID.
            var item=new NativeItem { mask=0x100,iItem=group.Items[0].Index };
            if(SendMessage(Handle,LvmGetItem,IntPtr.Zero,ref item)==IntPtr.Zero || item.iGroupId<0) { id=0;return false; }
            id=item.iGroupId;nativeGroupIds.Add(group.Name,id);return true;
        }
        void CaptureGroupStates() {
            if(!IsHandleCreated || populating)return;
            for(int index=0;index<Groups.Count;index++) {
                var info=GroupInfo(LvgfState);int id;
                if(!TryNativeGroupId(Groups[index],out id) || SendMessage(Handle,LvmGetGroupInfo,new IntPtr(id),ref info)==new IntPtr(-1))continue;
                if((info.state&LvgsCollapsed)!=0)collapsed.Add(Groups[index].Name);else collapsed.Remove(Groups[index].Name);
            }
        }
        void ApplyGroupStates() {
            if(!IsHandleCreated)return;
            for(int index=0;index<Groups.Count;index++)ApplyGroupState(index,collapsed.Contains(Groups[index].Name));
        }
        void ApplyGroupState(int index,bool value) {
            int id;if(!TryNativeGroupId(Groups[index],out id))return;
            var info=GroupInfo(LvgfState);info.state=LvgsCollapsible|(value?LvgsCollapsed:0);
            SendMessage(Handle,LvmSetGroupInfo,new IntPtr(id),ref info);
        }
        void ClearHiddenSelection() {
            foreach(ListViewItem row in SelectedItems.Cast<ListViewItem>().ToArray())
                if(row.Group!=null && collapsed.Contains(row.Group.Name))row.Selected=false;
        }
    }
}
