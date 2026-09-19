using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ScumMiniMap {

    public sealed class Position {



        public double X, Y, Z, Yaw;



        static readonly Regex Format = new Regex(@"^\s*\{X=(?<x>-?\d+(?:\.\d+)?)\s+Y=(?<y>-?\d+(?:\.\d+)?)\s+Z=(?<z>-?\d+(?:\.\d+)?)\|P=(?<p>-?\d+(?:\.\d+)?)\s+Y=(?<yaw>-?\d+(?:\.\d+)?)\s+R=(?<r>-?\d+(?:\.\d+)?)\}\s*$", RegexOptions.CultureInvariant);



        public static Position Parse(string text) {



            if (text == null || text.Length > 512) return null;



            Match m = Format.Match(text);



            if (!m.Success) return null;



            double x, y, z, yaw;



            if (!Read(m,"x",out x) || !Read(m,"y",out y) || !Read(m,"z",out z) || !Read(m,"yaw",out yaw)) return null;



            if (Math.Abs(x)>2000000 || Math.Abs(y)>2000000 || Math.Abs(z)>2000000 || Math.Abs(yaw)>360) return null;



            return new Position { X=x,Y=y,Z=z,Yaw=yaw };



        }



        static bool Read(Match m,string key,out double value) {



            return double.TryParse(m.Groups[key].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out value) && !double.IsInfinity(value) && !double.IsNaN(value);



        }



    }

}
