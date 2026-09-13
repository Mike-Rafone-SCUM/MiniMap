using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
namespace ScumMiniMap {
    internal static class DestinationSearch {
        internal static string Normalize(string value) {
            StringBuilder text=new StringBuilder();
            foreach(char c in (value??"").Normalize(NormalizationForm.FormD)) {
                if(CharUnicodeInfo.GetUnicodeCategory(c)==UnicodeCategory.NonSpacingMark) continue;
                text.Append(char.IsLetterOrDigit(c)?char.ToLowerInvariant(c):' ');
            }
            return Regex.Replace(text.ToString(),@"\s+"," ").Trim();
        }
        static string Alias(string word) {
            switch(word) {
                case "airport": case "aeropuerto": case "aerodromo": return "airfield";
                case "gas": case "petrol": case "nafta": case "combustible": case "estacion": return "fuel";
                case "harbor": case "puerto": return "harbour";
                case "trader": case "traders": case "comerciante": case "comerciantes": case "vendor": case "vendors": case "trade": case "safezone": case "puesto": return "outpost";
                default:return word;
            }
        }
        static readonly string[] TraderSynonyms = new string[] {
            "trader", "traders", "outpost", "outposts", "comerciante", "comerciantes", "vendor", "vendors", "safezone", "puesto"
        };
        static bool IsTraderToken(string token, out int dist) {
            dist = 1000;
            if(string.IsNullOrEmpty(token) || token.Length < 3) return false;
            foreach(string syn in TraderSynonyms) {
                if(token == syn) { dist = 0; return true; }
                if(syn.StartsWith(token, StringComparison.Ordinal) && token.Length >= 4) { dist = 1; return true; }
                int ed = EditDistance(token, syn);
                if(ed <= (token.Length >= 7 ? 2 : 1)) {
                    if(ed < dist) dist = ed;
                }
            }
            return dist <= 2;
        }
        internal static string Sector(PointF p) {
            double worldX=616818-p.X*1521618, worldY=618818-p.Y*1523618;
            int col=Math.Max(0,Math.Min(4,(int)Math.Floor((617505-worldX)/304132)));
            int row=Math.Max(0,Math.Min(4,(int)Math.Floor((617953-worldY)/304356)));
            return "DCBAZ"[row].ToString()+(4-col);
        }
        static int EditDistance(string a,string b) {
            int[,] d=new int[a.Length+1,b.Length+1];
            for(int i=0;i<=a.Length;i++) d[i,0]=i;
            for(int j=0;j<=b.Length;j++) d[0,j]=j;
            for(int i=1;i<=a.Length;i++) for(int j=1;j<=b.Length;j++) {
                d[i,j]=Math.Min(Math.Min(d[i-1,j]+1,d[i,j-1]+1),d[i-1,j-1]+(a[i-1]==b[j-1]?0:1));
                if(i>1 && j>1 && a[i-1]==b[j-2] && a[i-2]==b[j-1]) d[i,j]=Math.Min(d[i,j],d[i-2,j-2]+1);
            }
            return d[a.Length,b.Length];
        }
        static int Rank(string name,string query) {
            if(query.Length==0)return 0;
            if(name==query)return 0;
            if(name.StartsWith(query,StringComparison.Ordinal))return 10;
            if(name.IndexOf(query,StringComparison.Ordinal)>=0)return 20;
            string[] words=name.Split(' '); int total=30;
            bool isOutpostZone = name.IndexOf("outpost", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("puesto", StringComparison.OrdinalIgnoreCase) >= 0;
            foreach(string token in query.Split(' ')) {
                int best=1000;
                int traderDist;
                if(isOutpostZone && IsTraderToken(token, out traderDist)) {
                    best = Math.Min(best, 4 + traderDist);
                }
                foreach(string word in words) {
                    if(word==token) best=0;
                    else if(word.StartsWith(token,StringComparison.Ordinal)) best=Math.Min(best,2);
                    else if(Alias(word)==Alias(token)) best=Math.Min(best,4);
                    else if(token.Length>=4 && word.Length<=80 && Math.Abs(token.Length-word.Length)<=2) {
                        int distance=EditDistance(token,word);
                        if(distance<=(token.Length>=7?2:1)) best=Math.Min(best,10+distance);
                    }
                }
                if(best==1000)return -1;
                total+=best;
            }
            return total;
        }
        internal static double Metres(PointF a,PointF b) {
            double x=(a.X-b.X)*15216.18,y=(a.Y-b.Y)*15236.18; return Math.Sqrt(x*x+y*y);
        }
        internal static string Distance(double metres) { return metres>=1000?(metres/1000).ToString("F1",CultureInfo.InvariantCulture)+" km":Math.Round(metres).ToString(CultureInfo.InvariantCulture)+" m"; }
        sealed class Match { internal MapZone Zone; internal int Score,Index; internal double Distance; }
        internal static List<MapZone> Find(List<MapZone> places,string query,PointF? origin) {
            string filter=Normalize(query); bool nearest=Regex.IsMatch(filter,@"\b(nearest|near|cerca|cercano|cercana)\b") && origin.HasValue; List<MapZone> result=new List<MapZone>();
            if(filter.Length>120)return result;
            bool factionOnly=Regex.IsMatch(filter,@"\b(factions?|faccion(es)?)\b");
            if(factionOnly) {
                filter=Regex.Replace(filter,@"\b(factions?|faccion(es)?)\b","");
                filter=Regex.Replace(filter,@"\s+"," ").Trim();
            }
            MatchCollection grids=Regex.Matches(filter,@"\b[dcbaz]\s*[0-4]\b");
            string sector=null;
            if(grids.Count==1) { sector=grids[0].Value.Replace(" ","").ToUpperInvariant(); filter=filter.Remove(grids[0].Index,grids[0].Length).Trim(); }
            filter=Regex.Replace(filter,@"\b(grid|sector|square|in|near|nearest|cuadricula|en|cerca|cercano|cercana)\b",""); filter=Regex.Replace(filter,@"\s+"," ").Trim();
            List<Match> matches=new List<Match>(); int index=0;
            foreach(MapZone zone in places??new List<MapZone>()) {
                if(zone==null || string.IsNullOrWhiteSpace(zone.Name) || zone.Points==null || zone.Points.Length==0)continue;
                if(factionOnly && !zone.IsFaction) continue;
                if(sector!=null && Sector(zone.Centroid)!=sector)continue;
                int score=Rank(Normalize(zone.Name),filter);
                string locName=Localization.GetZoneName(zone.Name);
                if(!string.IsNullOrEmpty(locName) && locName!=zone.Name) {
                    int locScore=Rank(Normalize(locName),filter);
                    if(locScore>=0 && (score<0 || locScore<score)) score=locScore;
                }
                if(score>=0) matches.Add(new Match { Zone=zone,Score=score,Index=index,Distance=origin.HasValue?Metres(zone.Centroid,origin.Value):0 });
                index++;
            }
            matches.Sort((a,b)=> { int c=nearest?a.Distance.CompareTo(b.Distance):a.Score.CompareTo(b.Score); if(c==0)c=nearest?a.Score.CompareTo(b.Score):a.Distance.CompareTo(b.Distance); if(c==0)c=string.Compare(a.Zone.Name,b.Zone.Name,StringComparison.OrdinalIgnoreCase); return c==0?a.Index.CompareTo(b.Index):c; });
            foreach(Match match in matches)result.Add(match.Zone);
            return result;
        }
        internal static void SelfTest() {
            MapZone air=new MapZone { Name="Military Airfield",Points=new[]{new PointF(.1f,.1f)} };
            MapZone town=new MapZone { Name="Šibenik",Points=new[]{new PointF(.3f,.3f)} };
            MapZone fw=new MapZone { Name="D4 Clock house",Argb=-1,Points=new[]{new PointF(.1f,.02f)} };
            MapZone outpost=new MapZone { Name="Z3 Outpost",Points=new[]{new PointF(.2f,.8f)} };
            List<MapZone> places=new List<MapZone>{air,town,fw,outpost};
            foreach(string q in new[]{"airfield military","airport","airfeild","mil air","military D4"}) if(Find(places,q,null).Count!=1 || Find(places,q,null)[0]!=air)throw new Exception("Search regression: "+q);
            if(Find(places,"sibenik",null)[0]!=town || Find(places,"airfield C3",null).Count!=0 || Find(places,"zzzzzz",null).Count!=0)throw new Exception("Search filters failed.");
            if(Find(places,"",new PointF(.3f,.3f))[0]!=town)throw new Exception("Nearby ranking failed.");
            if(Find(places,"faction",null).Count!=1 || Find(places,"faction",null)[0]!=fw)throw new Exception("Faction search failed.");
            if(Find(places,"faction D4",null).Count!=1 || Find(places,"faction D3",null).Count!=0)throw new Exception("Faction sector search failed.");
            if(Find(places,"aeropuerto",null).Count!=1 || Find(places,"aeropuerto",null)[0]!=air)throw new Exception("Spanish alias failed.");
            if(Find(places,"facción D4",null).Count!=1 || Find(places,"faccion",null).Count!=1)throw new Exception("Spanish faction search failed.");
            if(Find(places,"cuadricula D4",null).Count!=2)throw new Exception("Spanish grid search failed.");
            if(Find(places,"trader",null).Count!=1 || Find(places,"trader",null)[0]!=outpost)throw new Exception("Trader search failed.");
            if(Find(places,"traider",null).Count!=1 || Find(places,"comerciante",null).Count!=1)throw new Exception("Trader fuzzy search failed.");
        }
    }
}
