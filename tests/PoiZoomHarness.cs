using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
class PoiZoomHarness {
 const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static void Set(object target,string field,object value){target.GetType().GetField(field,Hidden).SetValue(target,value);}
 [STAThread]static void Main(string[] args){
  var assembly=Assembly.LoadFrom(args[0]);var storeType=assembly.GetType("ScumMiniMap.ScumMapStore");
  Directory.CreateDirectory(args[1]);var store=storeType.GetMethod("Load",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,new object[]{args[1]});
  storeType.GetMethod("SetAllEnabled").Invoke(store,new object[]{true});
  if(args.Contains("--no-habitats"))((System.Collections.IList)storeType.GetField("Habitats").GetValue(store)).Clear();
  var state=Activator.CreateInstance(assembly.GetType("ScumMiniMap.WpfMapState"),true);
  Set(state,"Store",store);Set(state,"PoiClustering",false);Set(state,"Zoom",1f);Set(state,"Pan",new System.Drawing.PointF(.5f,.5f));Set(state,"Opacity",100);
  Set(state,"Saved",Array.CreateInstance(assembly.GetType("ScumMiniMap.MapZone"),0));Set(state,"Labels",true);Set(state,"LabelSize",12);
  var surfaceType=assembly.GetType("ScumMiniMap.WpfMapSurface");var surface=(FrameworkElement)Activator.CreateInstance(surfaceType,true);Set(surface,"State",state);
  surface.Measure(new Size(1200,900));surface.Arrange(new Rect(0,0,1200,900));
  var render=surfaceType.GetMethod("OnRender",Hidden);var timings=new System.Collections.Generic.List<double>();
  var layout=surfaceType.GetMethod("PoiClusters",BindingFlags.Static|BindingFlags.NonPublic);
  Array firstLayout=null,lastLayout=null;bool reused=true,glyphsReused=true;object[] firstGlyphs=null;
  int before=GC.CollectionCount(0);
  foreach(float zoom in new[]{1f,1.2f,1.44f,1.728f,2.0736f,1.728f,1.44f,1.2f,1f}){
   Set(state,"Zoom",zoom);var drawing=new DrawingGroup();var clock=Stopwatch.StartNew();using(var dc=drawing.Open())render.Invoke(surface,new object[]{dc});clock.Stop();timings.Add(clock.Elapsed.TotalMilliseconds);
   lastLayout=(Array)layout.Invoke(null,new object[]{state,1200d,900d,false});
   if(firstLayout==null){firstLayout=lastLayout;firstGlyphs=lastLayout.Cast<object>().Select(item=>item.GetType().GetField("Icon",Hidden).GetValue(item)).ToArray();}
   else {reused&=Object.ReferenceEquals(firstLayout,lastLayout);for(int i=0;i<firstGlyphs.Length;i++)if(firstGlyphs[i]!=null)glyphsReused&=Object.ReferenceEquals(firstGlyphs[i],lastLayout.GetValue(i).GetType().GetField("Icon",Hidden).GetValue(lastLayout.GetValue(i)));}
  }
  Console.WriteLine("All POIs enabled, clustering off, labels on; markers="+((System.Collections.ICollection)storeType.GetField("Markers").GetValue(store)).Count);
  Console.WriteLine("Zoom frame milliseconds: "+string.Join(", ",timings.Select(ms=>ms.ToString("F1",System.Globalization.CultureInfo.InvariantCulture))));
  Console.WriteLine("Total="+timings.Sum().ToString("F1")+" ms; max="+timings.Max().ToString("F1")+" ms; Gen0 collections="+(GC.CollectionCount(0)-before));
  var icons=new System.Collections.Generic.HashSet<object>();foreach(var item in lastLayout){var icon=item.GetType().GetField("Icon",Hidden).GetValue(item);if(icon!=null)icons.Add(icon);}
  Console.WriteLine("POI layout reused across zoom="+reused+"; glyph assets reused="+glyphsReused+"; individual POIs="+lastLayout.Length+"; distinct icon drawings="+icons.Count);
  var labelClock=Stopwatch.StartNew();surfaceType.GetMethod("SelectLabels",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{state,1200d,900d,false});labelClock.Stop();
  Console.WriteLine("Repeated label placement="+labelClock.Elapsed.TotalMilliseconds.ToString("F1")+" ms");
  if(args.Length>2&&args[2]=="--require-reuse"&&(!reused||!glyphsReused||icons.Count>=lastLayout.Length||icons.Count>512))throw new Exception("Unclustered zoom rebuilt marker/icon assets.");
 }
}
