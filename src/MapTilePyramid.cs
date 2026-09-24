using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace ScumMiniMap {
    // Keep the bundled map compressed; decode only the visible 512-pixel tiles.
    internal sealed class MapTilePyramid : IDisposable {
        sealed class Level {
            internal int Width,Height,Columns,Rows;
            internal long[] Offsets;
            internal int[] Lengths;
        }
        sealed class Cached { internal long Key; internal Bitmap Image; }
        readonly Stream stream;
        readonly Level[] levels;
        readonly Dictionary<long,LinkedListNode<Cached>> cache=new Dictionary<long,LinkedListNode<Cached>>();
        readonly LinkedList<Cached> recent=new LinkedList<Cached>();
        const int TileSize=512,Gutter=2,CacheLimit=64;
        internal MapTilePyramid(Stream source) {
            if(source==null || !source.CanSeek) throw new InvalidDataException("Map tiles are missing.");
            stream=source;
            using(var reader=new BinaryReader(stream,System.Text.Encoding.ASCII,true)) {
                if(new string(reader.ReadChars(4))!="MTL2" || reader.ReadInt32()!=TileSize)
                    throw new InvalidDataException("Invalid map tile header.");
                int count=reader.ReadInt32();
                if(count<1 || count>16) throw new InvalidDataException("Invalid map tile level count.");
                levels=new Level[count];
                for(int i=0;i<count;i++) {
                    var level=new Level { Width=reader.ReadInt32(),Height=reader.ReadInt32(),Columns=reader.ReadInt32(),Rows=reader.ReadInt32() };
                    if(level.Width<1 || level.Height<1 || level.Columns!=(level.Width+TileSize-1)/TileSize
                        || level.Rows!=(level.Height+TileSize-1)/TileSize || (long)level.Columns*level.Rows>4096)
                        throw new InvalidDataException("Invalid map tile dimensions.");
                    int tiles=level.Columns*level.Rows;
                    level.Offsets=new long[tiles]; level.Lengths=new int[tiles];
                    for(int j=0;j<tiles;j++) {
                        level.Offsets[j]=reader.ReadInt64(); level.Lengths[j]=reader.ReadInt32();
                        if(level.Offsets[j]<12 || level.Lengths[j]<1 || level.Lengths[j]>8*1024*1024
                            || level.Offsets[j]>stream.Length-level.Lengths[j])
                            throw new InvalidDataException("Invalid map tile offset.");
                    }
                    levels[i]=level;
                }
            }
        }
        Bitmap Tile(int levelIndex,int column,int row) {
            long key=((long)levelIndex<<32)|((long)row<<16)|(uint)column;
            LinkedListNode<Cached> node;
            if(cache.TryGetValue(key,out node)) { recent.Remove(node); recent.AddFirst(node); return node.Value.Image; }
            Level level=levels[levelIndex]; int index=row*level.Columns+column;
            byte[] encoded=new byte[level.Lengths[index]];
            stream.Position=level.Offsets[index]; int read=0;
            while(read<encoded.Length) {
                int count=stream.Read(encoded,read,encoded.Length-read);
                if(count==0) throw new EndOfStreamException("Map tile is truncated.");
                read+=count;
            }
            Bitmap bitmap;
            using(var memory=new MemoryStream(encoded,false))
            using(var decoded=Image.FromStream(memory,false,true)) bitmap=new Bitmap(decoded);
            node=new LinkedListNode<Cached>(new Cached { Key=key,Image=bitmap });
            recent.AddFirst(node); cache.Add(key,node);
            if(cache.Count>CacheLimit) {
                var oldest=recent.Last; recent.RemoveLast(); cache.Remove(oldest.Value.Key); oldest.Value.Image.Dispose();
            }
            return bitmap;
        }
        internal Image Overview() {
            // The zone editor displays the map at roughly 700 pixels across.
            int index=levels.Length-1;
            if(index>0 && levels[index].Width<768) index--;
            var overview=new Bitmap(levels[index].Width,levels[index].Height);
            using(var graphics=Graphics.FromImage(overview))
                Draw(graphics,new RectangleF(0,0,overview.Width,overview.Height),0,0,overview.Width);
            return overview;
        }
        internal void Draw(Graphics graphics,RectangleF visible,float left,float top,float side) {
            int index=0;
            for(int i=1;i<levels.Length;i++) { if(levels[i].Width<side || levels[i].Height<side) break; index=i; }
            Level level=levels[index];
            float scaleX=level.Width/side,scaleY=level.Height/side;
            float x1=Math.Max(0,(visible.Left-left)*scaleX),y1=Math.Max(0,(visible.Top-top)*scaleY);
            float x2=Math.Min(level.Width,(visible.Right-left)*scaleX),y2=Math.Min(level.Height,(visible.Bottom-top)*scaleY);
            if(x2<=x1 || y2<=y1) return;
            int firstColumn=Math.Max(0,(int)Math.Floor((x1-Gutter)/TileSize));
            int lastColumn=Math.Min(level.Columns-1,(int)Math.Ceiling((x2+Gutter)/TileSize)-1);
            int firstRow=Math.Max(0,(int)Math.Floor((y1-Gutter)/TileSize));
            int lastRow=Math.Min(level.Rows-1,(int)Math.Ceiling((y2+Gutter)/TileSize)-1);
            for(int row=firstRow;row<=lastRow;row++) for(int column=firstColumn;column<=lastColumn;column++) {
                float tileX=column*TileSize,tileY=row*TileSize;
                float leftEdge=Math.Max(0,tileX-Gutter),topEdge=Math.Max(0,tileY-Gutter);
                float sx1=Math.Max(x1,leftEdge),sy1=Math.Max(y1,topEdge);
                float sx2=Math.Min(x2,Math.Min(tileX+TileSize+Gutter,level.Width));
                float sy2=Math.Min(y2,Math.Min(tileY+TileSize+Gutter,level.Height));
                if(sx2<=sx1 || sy2<=sy1) continue;
                var source=new RectangleF(sx1-leftEdge,sy1-topEdge,sx2-sx1,sy2-sy1);
                var destination=new RectangleF(left+sx1/scaleX,top+sy1/scaleY,(sx2-sx1)/scaleX,(sy2-sy1)/scaleY);
                graphics.DrawImage(Tile(index,column,row),destination,source,GraphicsUnit.Pixel);
            }
        }
        public void Dispose() {
            foreach(var entry in recent) entry.Image.Dispose();
            recent.Clear(); cache.Clear(); stream.Dispose();
        }
    }
}
