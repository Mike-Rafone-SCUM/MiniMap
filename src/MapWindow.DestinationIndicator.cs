using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ScumMiniMap {
    public sealed partial class MapWindow {
        internal static bool TryDestinationEdge(RectangleF bounds,bool circular,PointF player,PointF destination,out PointF tip,out PointF direction) {
            tip=direction=PointF.Empty;
            double dx=destination.X-player.X,dy=destination.Y-player.Y;
            double length=Math.Sqrt(dx*dx+dy*dy);
            if(double.IsNaN(length) || double.IsInfinity(length) || length<0.000001 || bounds.Width<=24 || bounds.Height<=24) return false;
            direction=new PointF((float)(dx/length),(float)(dy/length));
            float rx=bounds.Width/2-10,ry=bounds.Height/2-10;
            double distance=circular
                ? 1/Math.Sqrt(direction.X*direction.X/(rx*rx)+direction.Y*direction.Y/(ry*ry))
                : Math.Min(Math.Abs(direction.X)<0.000001?double.MaxValue:rx/Math.Abs(direction.X),Math.Abs(direction.Y)<0.000001?double.MaxValue:ry/Math.Abs(direction.Y));
            tip=new PointF(bounds.Left+bounds.Width/2+(float)distance*direction.X,bounds.Top+bounds.Height/2+(float)distance*direction.Y);
            return true;
        }

        void DrawDestinationIndicator(Graphics g,Rectangle mapBounds,bool boundedCircle) {
            if(fullMapActive || position==null || searchTarget==null) return;
            bool circular=overlayShape=="Circle";
            RectangleF ring=circular?OverlayWindow.CircleBounds(overlayFrame.Size,boundedCircle,mapBounds):mapBounds;
            PointF player=motion!=null && motion.Point.X>0?motion.Point:ToMap(position);
            PointF tip,direction;
            if(!TryDestinationEdge(ring,circular,player,searchTarget.Centroid,out tip,out direction)) return;
            GraphicsState state=g.Save();
            try {
                g.SetClip(mapBounds,CombineMode.Intersect);
                g.SmoothingMode=SmoothingMode.AntiAlias;
                int alpha=(int)Math.Round(255*Math.Max(30,Math.Min(100,mapOpacity))/100.0);
                if(circular) {
                    RectangleF arc=RectangleF.Inflate(ring,-8,-8);
                    float angle=(float)(Math.Atan2(direction.Y,direction.X)*180/Math.PI);
                    using(var glow=new Pen(Color.FromArgb(alpha/3,routeGuidanceColor),9))
                    using(var edge=new Pen(Color.FromArgb(alpha,routeGuidanceColor),3)) {
                        glow.StartCap=glow.EndCap=edge.StartCap=edge.EndCap=LineCap.Round;
                        g.DrawArc(glow,arc,angle-12,24);
                        g.DrawArc(edge,arc,angle-12,24);
                    }
                }
                PointF rear=new PointF(tip.X-direction.X*13,tip.Y-direction.Y*13);
                PointF[] arrow={tip,new PointF(rear.X-direction.Y*6,rear.Y+direction.X*6),new PointF(rear.X+direction.Y*6,rear.Y-direction.X*6)};
                using(var halo=new Pen(Color.FromArgb(alpha/3,routeGuidanceColor),7))
                using(var outline=new Pen(Color.FromArgb(alpha,10,14,18),3))
                using(var fill=new SolidBrush(Color.FromArgb(alpha,routeGuidanceColor))) {
                    halo.LineJoin=outline.LineJoin=LineJoin.Round;
                    g.DrawPolygon(halo,arrow);
                    g.DrawPolygon(outline,arrow);
                    g.FillPolygon(fill,arrow);
                }
            } finally { g.Restore(state); }
        }
    }
}
