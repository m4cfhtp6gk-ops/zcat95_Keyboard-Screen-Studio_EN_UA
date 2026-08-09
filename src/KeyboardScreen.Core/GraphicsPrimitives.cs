using SkiaSharp;
namespace KeyboardScreen.Core;
public readonly record struct Point(double X,double Y);
public readonly record struct Rect(double X,double Y,double Width,double Height){public double Left=>X;public double Top=>Y;public double Right=>X+Width;public double Bottom=>Y+Height;}
public readonly record struct Color(byte A,byte R,byte G,byte B){public static Color FromRgb(byte r,byte g,byte b)=>new(255,r,g,b);public static Color FromArgb(byte a,byte r,byte g,byte b)=>new(a,r,g,b);internal SKColor Skia=>new(R,G,B,A);}
public static class Colors{public static Color White=>Color.FromRgb(255,255,255);public static Color Black=>Color.FromRgb(0,0,0);}
public readonly record struct FontWeight(int Value);
public static class FontWeights{public static FontWeight Normal=>new(400);public static FontWeight Medium=>new(500);public static FontWeight SemiBold=>new(600);public static FontWeight Bold=>new(700);}
public enum TextAlignment{Left,Center,Right}
public sealed record FontFamily(string Name,string? FilePath=null);
public class Geometry{internal SKPath Path{get;}=new();}
public sealed class StreamGeometry:Geometry{public StreamGeometryContext Open()=>new(Path);public void Freeze(){}}
public sealed class StreamGeometryContext:IDisposable{private readonly SKPath _path;internal StreamGeometryContext(SKPath path)=>_path=path;public void BeginFigure(Point p,bool isFilled,bool isClosed)=>_path.MoveTo((float)p.X,(float)p.Y);public void BezierTo(Point a,Point b,Point c,bool isStroked,bool isSmoothJoin)=>_path.CubicTo((float)a.X,(float)a.Y,(float)b.X,(float)b.Y,(float)c.X,(float)c.Y);public void LineTo(Point p,bool isStroked,bool isSmoothJoin)=>_path.LineTo((float)p.X,(float)p.Y);public void Dispose(){}}
