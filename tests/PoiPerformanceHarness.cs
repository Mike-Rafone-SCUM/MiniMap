using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
class PoiPerformanceHarness {
 const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
 static object Get(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
 static void Set(object o,string n,object v){o.GetType().GetField(n,F).SetValue(o,v);}
 static object Call(object o,string n,params object[] a){return o.GetType().GetMethod(n,F).Invoke(o,a);}
 static void Time(string name,int count,Action action){action();GC.Collect();var timer=Stopwatch.StartNew();for(int i=0;i<count;i++)action();timer.Stop();Console.WriteLine(name+": "+(timer.Elapsed.TotalMilliseconds/count).ToString("0.000")+" ms/frame");}
 static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS: "+message);}
 [STAThread]static void Main(string[] args){
  var assembly=Assembly.LoadFrom(Path.GetFullPath(args[0]));var type=assembly.GetType("ScumMiniMap.WpfFieldConsoleRuntime");var runtime=Activator.CreateInstance(type,F,null,new object[]{Path.GetFullPath(args[1]),true},null);
  try {
   var engine=Get(runtime,"engine");Call(engine,"WpfPreviewPosition","{X=-336602.375 Y=-270302.625 Z=18853.264|P=0 Y=90 R=0}");var state=Call(engine,"WpfSnapshot",true);var store=Get(state,"Store");Call(store,"SetAllEnabled",true);Set(store,"MasterEnabled",true);Console.WriteLine("Markers: "+((System.Collections.ICollection)Get(store,"Markers")).Count+"; habitats: "+((System.Collections.ICollection)Get(store,"Habitats")).Count);
   var surface=(FrameworkElement)Get(runtime,"tacticalMap");surface.Measure(new Size(1400,900));surface.Arrange(new Rect(0,0,1400,900));Set(surface,"Terrain",null);Set(surface,"BaseMap",null);Set(surface,"State",state);Set(state,"Opacity",0);Set(state,"Zoom",2f);Set(state,"Pan",new System.Drawing.PointF(.5f,.5f));
   var labels=surface.GetType().GetMethod("SelectLabels",BindingFlags.Static|BindingFlags.NonPublic);var clusters=surface.GetType().GetMethod("PoiClusters",BindingFlags.Static|BindingFlags.NonPublic);int sample=0;
   Time("Cluster zoom rebuild",12,()=>{Set(state,"Zoom",1f+(++sample%12)*.1f);clusters.Invoke(null,new object[]{state,1400d,900d,false});});Set(state,"Zoom",2f);
   Time("All enabled labels",30,()=>labels.Invoke(null,new object[]{state,1400d,900d,false}));
   Action draw=()=>{var visual=new DrawingVisual();using(var dc=visual.RenderOpen())Call(surface,"OnRender",dc);};Time("All enabled rendering",30,draw);Set(store,"MasterEnabled",false);Time("POIs disabled rendering",30,draw);Set(store,"MasterEnabled",true);Set(state,"PoiClustering",false);Time("Unclustered labels",10,()=>labels.Invoke(null,new object[]{state,1400d,900d,false}));Time("Unclustered rendering",10,draw);
   if(surface.GetType().GetField("labelTexts",BindingFlags.Static|BindingFlags.NonPublic)==null) { Console.WriteLine("Baseline measured; cache assertions apply to optimized builds.");return; }
   var allMarkers=((System.Collections.IEnumerable)Get(store,"Markers")).Cast<object>().ToArray();var selected=((Array)clusters.Invoke(null,new object[]{state,1400d,900d,false})).Cast<object>().ToArray();Check(selected.Length==allMarkers.Length,"Unclustered mode retains every enabled POI");
   var icon=selected.Select(item=>Get(item,"Icon")).First(item=>item!=null);draw();Check(((DrawingGroup)icon).IsFrozen&&selected.Any(item=>Object.ReferenceEquals(Get(item,"Icon"),icon)),"Repeated frames reuse frozen POI drawings");
   Set(state,"PoiClustering",true);var grouped=((Array)clusters.Invoke(null,new object[]{state,1400d,900d,false})).Cast<object>().ToArray();var members=grouped.SelectMany(item=>((System.Collections.IEnumerable)Get(item,"Members")).Cast<object>()).ToArray();Check(members.Length==allMarkers.Length&&members.Distinct().Count()==allMarkers.Length,"Clustering retains each POI exactly once");
   var table=surface.GetType().GetField("poiCaches",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);object[] lookup={store,null};table.GetType().GetMethod("TryGetValue").Invoke(table,lookup);var cache=((Array)lookup[1]).GetValue(1);var ordering=Get(cache,"OrderedMarkers");Set(state,"Zoom",3f);clusters.Invoke(null,new object[]{state,1400d,900d,false});Check(Object.ReferenceEquals(ordering,Get(cache,"OrderedMarkers")),"Zoom reuses stable category ordering instead of sorting the catalog again");
   int categoryId=(int)Get(allMarkers[0],"CategoryId");Call(store,"SetCategoryEnabled",categoryId,false);var filtered=((Array)clusters.Invoke(null,new object[]{state,1400d,900d,false})).Cast<object>().SelectMany(item=>((System.Collections.IEnumerable)Get(item,"Members")).Cast<object>()).ToArray();Check(filtered.All(item=>(int)Get(item,"CategoryId")!=categoryId)&&filtered.Length==allMarkers.Count(item=>(bool)Call(store,"IsCategoryEnabled",(int)Get(item,"CategoryId"))),"Category changes invalidate cached ordering and preserve all other markers");Call(store,"SetCategoryEnabled",categoryId,true);
   var formatter=surface.GetType().GetMethod("LabelText",BindingFlags.Static|BindingFlags.NonPublic);for(int index=0;index<1200;index++)formatter.Invoke(null,new object[]{"Synthetic bounded label "+index,12d,Brushes.White});Check(((System.Collections.IDictionary)surface.GetType().GetField("labelTexts",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null)).Count<=1024,"Formatted-label cache has a fixed memory bound");
   var localization=assembly.GetType("ScumMiniMap.Localization");var language=localization.GetProperty("Current");var previousLanguage=language.GetValue(null,null);try { language.SetValue(null,Enum.Parse(assembly.GetType("ScumMiniMap.AppLanguage"),"French"),null);string translated=(string)surface.GetType().GetMethod("PoiLabel",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{state,allMarkers[0],false});string expected=(string)localization.GetMethod("GetMapObjectName",BindingFlags.Static|BindingFlags.Public).Invoke(null,new[]{Call(store,"GetMarkerDisplayName",allMarkers[0])});Check(translated==expected&&(string)Get(cache,"LabelLanguage")=="fr","Language changes invalidate localized POI names"); }finally {language.SetValue(null,previousLanguage,null);}
  }finally{((IDisposable)runtime).Dispose();}
 }
}
