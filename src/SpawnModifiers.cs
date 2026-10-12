using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ScumMiniMap {
    // Property names and documented hints come from SCUM Database's SpawnItem command metadata.
    // Location formats are documented in Gamepires' official Steam admin-command update.
    internal static class SpawnModifiers {
        internal static readonly IList<string> PropertyNames=Array.AsReadOnly(new[]{
            "Health","Uses","Dirtiness","Location","KeyCardSector","StackCount","Radiation","Weight"
        });

        internal static string GetHint(string property) {
            switch(CanonicalProperty(property)) {
                case "Health":return "Item condition: 0–100 (%)";
                case "Dirtiness":return "Dirtiness: 0–100 (%)";
                case "Location":return "Coordinates, copied transform or 17-digit SteamID64";
                case "Uses":return "Uses remaining: 0 to the item's maximum";
                case "StackCount":return "Stack size: 1 to the item's maximum";
                case "Weight":return "Weight override: number (kg)";
                case "Radiation":return "Radiation level: number";
                case "KeyCardSector":return "Keycard sector: A1–A4, B1–B4, C1–C4 or D1–D4";
                default:throw new FormatException(Localization.T("SpawnCartModifierUnknown",property??""));
            }
        }

        static string CanonicalProperty(string value) {
            foreach(string property in PropertyNames)if(string.Equals(value,property,StringComparison.OrdinalIgnoreCase))return property;
            return null;
        }

        // Returns a read-only dictionary; use Normalize for stable ordering in commands and saved kits.
        internal static IDictionary<string,string> Parse(string text) {
            var values=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            if(text==null)text="";
            if(text.Length>512)throw new FormatException(Localization.Get("SpawnCartModifierLength"));
            foreach(char c in text)if(char.IsControl(c))throw new FormatException(Localization.Get("SpawnCartModifierPairs"));
            List<Token> tokens=Tokenize(text);
            for(int i=0;i<tokens.Count;) {
                Token name=tokens[i++];
                if(name.Quoted)throw new FormatException(Localization.Get("SpawnCartModifierPairs"));
                string property=CanonicalProperty(name.Text);
                if(property==null)throw new FormatException(Localization.T("SpawnCartModifierUnknown",name.Text));
                if(values.ContainsKey(property))throw new FormatException(Localization.T("SpawnCartModifierDuplicate",property));
                if(i>=tokens.Count)throw new FormatException(Localization.Get("SpawnCartModifierPairs"));
                Token value=tokens[i++];
                if(property=="Location") {
                    string location=value.Quoted?"\""+value.Text+"\"":value.Text;
                    bool coordinateTriple=i+1<tokens.Count && !tokens[i].Quoted && !tokens[i+1].Quoted &&
                        PlainNumber(tokens[i].Text) && PlainNumber(tokens[i+1].Text);
                    if(!value.Quoted && (!SteamId(value.Text) || coordinateTriple)) {
                        if(!PlainNumber(value.Text) || i+1>=tokens.Count || tokens[i].Quoted || tokens[i+1].Quoted)
                            throw new FormatException(Localization.T("SpawnCartModifierValue",property));
                        location+=" "+tokens[i++].Text+" "+tokens[i++].Text;
                    }
                    values.Add(property,NormalizeLocation(location));
                } else {
                    if(value.Quoted)throw new FormatException(Localization.T("SpawnCartModifierValue",property));
                    values.Add(property,NormalizeValue(property,value.Text));
                }
            }
            return new ReadOnlyDictionary<string,string>(values);
        }

        internal static string Normalize(string text) {
            var values=Parse(text);var tokens=new List<string>();
            foreach(string property in PropertyNames) {
                string value;
                if(values.TryGetValue(property,out value)) { tokens.Add(property);tokens.Add(value); }
            }
            return string.Join(" ",tokens);
        }

        sealed class Token {
            internal readonly string Text;
            internal readonly bool Quoted;
            internal Token(string text,bool quoted) { Text=text;Quoted=quoted; }
        }

        static List<Token> Tokenize(string text) {
            var tokens=new List<Token>();int position=0;
            while(position<text.Length) {
                if(text[position]==' ') { position++;continue; }
                bool quoted=text[position]=='"';int start=quoted?++position:position;
                if(quoted) {
                    while(position<text.Length && text[position]!='"')position++;
                    if(position>=text.Length)throw new FormatException(Localization.Get("SpawnCartModifierPairs"));
                    tokens.Add(new Token(text.Substring(start,position-start),true));position++;
                    if(position<text.Length && text[position]!=' ')throw new FormatException(Localization.Get("SpawnCartModifierPairs"));
                } else {
                    while(position<text.Length && text[position]!=' ') {
                        if(text[position]=='"')throw new FormatException(Localization.Get("SpawnCartModifierPairs"));
                        position++;
                    }
                    tokens.Add(new Token(text.Substring(start,position-start),false));
                }
            }
            return tokens;
        }

        static bool SteamId(string text) { return text.Length==17 && AsciiDigits(text); }

        // The editor may supply an unquoted vector/transform; commands always serialize those with quotes.
        internal static string NormalizeLocation(string value) {
            if(value==null || value.Length>512)throw new FormatException(Localization.T("SpawnCartModifierValue","Location"));
            foreach(char c in value)if(char.IsControl(c))throw new FormatException(Localization.T("SpawnCartModifierValue","Location"));
            string location=value.Trim(' ');bool quoted=location.StartsWith("\"",StringComparison.Ordinal);
            if(quoted) {
                if(location.Length<2 || !location.EndsWith("\"",StringComparison.Ordinal))throw new FormatException(Localization.T("SpawnCartModifierValue","Location"));
                location=location.Substring(1,location.Length-2).Trim(' ');
            }
            if(location.IndexOf('"')>=0 || location.IndexOf('\\')>=0)throw new FormatException(Localization.T("SpawnCartModifierValue","Location"));
            if(!quoted && SteamId(location))return location;
            const string number=@"([+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+))";
            string vector=@"X="+number+@" +Y="+number+@" +Z="+number;
            Match transform=Regex.Match(location,@"^\{"+vector+@"\|P="+number+@" +Y="+number+@" +R="+number+@"\}$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
            if(transform.Success)return "\"{X="+LocationNumber(transform.Groups[1].Value)+" Y="+LocationNumber(transform.Groups[2].Value)+
                " Z="+LocationNumber(transform.Groups[3].Value)+"|P="+LocationNumber(transform.Groups[4].Value)+
                " Y="+LocationNumber(transform.Groups[5].Value)+" R="+LocationNumber(transform.Groups[6].Value)+"}\"";
            Match keyed=Regex.Match(location,"^"+vector+"$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
            if(keyed.Success)return "\"X="+LocationNumber(keyed.Groups[1].Value)+" Y="+LocationNumber(keyed.Groups[2].Value)+" Z="+LocationNumber(keyed.Groups[3].Value)+"\"";
            if(!quoted) {
                string[] coordinates=location.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);
                if(coordinates.Length==3)return LocationNumber(coordinates[0])+" "+LocationNumber(coordinates[1])+" "+LocationNumber(coordinates[2]);
            }
            throw new FormatException(Localization.T("SpawnCartModifierValue","Location"));
        }

        static string LocationNumber(string text) {
            decimal value;
            if(!PlainNumber(text) || !decimal.TryParse(text,NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out value))
                throw new FormatException(Localization.T("SpawnCartModifierValue","Location"));
            return value.ToString("0.############################",CultureInfo.InvariantCulture);
        }

        static string NormalizeValue(string property,string text) {
            if(property=="KeyCardSector") {
                string sector=text.ToUpperInvariant();
                if(sector.Length==2 && sector[0]>='A' && sector[0]<='D' && sector[1]>='1' && sector[1]<='4')return sector;
                throw new FormatException(Localization.T("SpawnCartModifierValue",property));
            }
            if(property=="Uses" || property=="StackCount") {
                int count;
                if(!AsciiDigits(text) || !int.TryParse(text,NumberStyles.None,CultureInfo.InvariantCulture,out count) ||
                    count<(property=="StackCount"?1:0))throw new FormatException(Localization.T("SpawnCartModifierValue",property));
                return count.ToString(CultureInfo.InvariantCulture);
            }
            decimal number;
            if(!PlainNumber(text) || !decimal.TryParse(text,NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out number) ||
                ((property=="Health" || property=="Dirtiness") && (number<0 || number>100)))
                throw new FormatException(Localization.T("SpawnCartModifierValue",property));
            return number.ToString("0.############################",CultureInfo.InvariantCulture);
        }

        static bool AsciiDigits(string value) {
            if(value.Length==0)return false;
            foreach(char c in value)if(c<'0' || c>'9')return false;
            return true;
        }

        static bool PlainNumber(string value) {
            if(value.Length==0)return false;
            int start=value[0]=='-' || value[0]=='+'?1:0;bool decimalPoint=false,digit=false;
            for(int i=start;i<value.Length;i++) {
                char c=value[i];
                if(c>='0' && c<='9') { digit=true;continue; }
                if(c=='.' && !decimalPoint) { decimalPoint=true;continue; }
                return false;
            }
            return digit;
        }
    }
}
