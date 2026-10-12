using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ScumMiniMap {

    internal enum TeleportDirection { Up, Down, Left, Right, Forward, Back }

    internal static class RelativeTeleport {
        internal const double MinimumDistanceMeters=0.1;
        internal const double MaximumDistanceMeters=1000;
        const double MaximumCoordinate=2000000;

        internal static string BuildCommand(Position source,TeleportDirection direction,double meters) {
            if(source==null) return null;
            double x=source.X,y=source.Y,z=source.Z,yaw=source.Yaw;
            if(!ValidCoordinate(x) || !ValidCoordinate(y) || !ValidCoordinate(z)
                || !Finite(yaw) || Math.Abs(yaw)>360)
                return null;
            if(!Finite(meters) || meters<MinimumDistanceMeters || meters>MaximumDistanceMeters)
                return null;

            double distance=meters*100;
            double radians=yaw*Math.PI/180;
            double forwardX=Math.Cos(radians),forwardY=Math.Sin(radians);
            switch(direction) {
                case TeleportDirection.Up: z+=distance;break;
                case TeleportDirection.Down: z-=distance;break;
                case TeleportDirection.Forward: x+=forwardX*distance;y+=forwardY*distance;break;
                case TeleportDirection.Back: x-=forwardX*distance;y-=forwardY*distance;break;
                case TeleportDirection.Right: x-=forwardY*distance;y+=forwardX*distance;break;
                case TeleportDirection.Left: x+=forwardY*distance;y-=forwardX*distance;break;
                default: return null;
            }
            if(!ValidCoordinate(x) || !ValidCoordinate(y) || !ValidCoordinate(z))
                return null;
            return String.Format(CultureInfo.InvariantCulture,"#Teleport {0:0.###} {1:0.###} {2:0.###}",x,y,z);
        }

        static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
        static bool ValidCoordinate(double value) { return Finite(value) && Math.Abs(value)<=MaximumCoordinate; }
    }

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
