using CoreGraphics;
using CoreImage;
using CoreText;
using Foundation;
using UIKit;
using System.Text.Json;

namespace HyperLane.iOS;

// Images and atlas slices live for the lifetime of the screen, not a draw call.
internal sealed class IosVisuals : IDisposable
{
    private readonly Dictionary<string, UIImage?> images = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,UIImage> textImages=new();
    internal void CachedText(string text,CGRect rect,double size,bool center=false,double opacity=1)
    {
        if(string.IsNullOrEmpty(text))return;
        string key=$"{text}\n{rect.Width}:{rect.Height}:{size}:{center}";
        if(!textImages.TryGetValue(key,out var image))
        {
            UIGraphics.BeginImageContextWithOptions(rect.Size,false,1);
            try{Text(text,new CGRect(0,0,rect.Width,rect.Height),size,center:center);image=UIGraphics.GetImageFromCurrentImageContext();}
            finally{UIGraphics.EndImageContext();}
            if(image==null)return;
            if(textImages.Count>=96){string oldest=textImages.Keys.First();textImages[oldest].Dispose();textImages.Remove(oldest);}
            textImages[key]=image;
        }
        Image(image,rect,opacity);
    }
    internal readonly Dictionary<string, CGImage> Sprites = new();
    private static string? checkpointFace;
    internal IosVisuals()
    {
        Console.Error.WriteLine("HL startup: visual assets");
        foreach (string file in Directory.EnumerateFiles(Path.Combine(NSBundle.MainBundle.BundlePath, "fonts"), "*.otf"))
        { using var url = NSUrl.FromFilename(file); CTFontManager.RegisterFontsForUrl(url, CTFontManagerScope.Process);
          if(Path.GetFileName(file).Equals("Checkpoint.otf",StringComparison.OrdinalIgnoreCase)){using var provider=new CGDataProvider(file);using var font=CGFont.CreateFromProvider(provider);checkpointFace=font.PostScriptName;} }
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(NSBundle.MainBundle.BundlePath, "skin/skin.json")));
        Console.Error.WriteLine("HL startup: fonts ready");
        CGImage? atlas = Asset("skin/atlas.png")?.CGImage;
        if (atlas is not null) foreach (var entry in json.RootElement.GetProperty("sprites").EnumerateObject())
        {
            int[] a = entry.Value.EnumerateArray().Select(x => x.GetInt32()).ToArray();
            if (a.Length == 4) Sprites[entry.Name] = atlas.WithImageInRect(new CGRect(a[0], a[1], a[2], a[3]));
        }
    }
    internal UIImage? Asset(string name) => FileImage(Path.Combine(NSBundle.MainBundle.BundlePath, name));
    internal UIImage? Judgement(string name)
    {
        string key="judgement:"+name;if(images.TryGetValue(key,out var cached))return cached;
        var image=Asset("judgements/"+name+".png");if(image?.CGImage is not {} source)return null;
        int w=(int)source.Width,h=(int)source.Height;byte[] pixels=new byte[w*h*4];
        using var space=CGColorSpace.CreateDeviceRGB();
        using(var bitmap=new CGBitmapContext(pixels,w,h,8,w*4,space,CGImageAlphaInfo.PremultipliedLast))bitmap.DrawImage(new CGRect(0,0,w,h),source);
        int left=w,top=h,right=0,bottom=0;
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(pixels[(y*w+x)*4+3]>8){left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);}
        if(left>right)return null;
        using var cropped=source.WithImageInRect(new CGRect(left,h-1-bottom,right-left+1,bottom-top+1));
        return images[key]=UIImage.FromImage(cropped);
    }
    internal UIImage? GearImage()
    {
        const string key="ui:gear";
        if(images.TryGetValue(key,out var cached))return cached;
        UIGraphics.BeginImageContextWithOptions(new CGSize(72,72),false,1);
        try
        {
            using var c=UIGraphics.GetCurrentContext();
            c.SetFillColor(UIColor.White.CGColor);c.BeginPath();
            for(int i=0;i<32;i++)
            {
                double angle=(i+.5)*Math.PI/16,r=i%4 is 1 or 2?34:26;
                nfloat x=(nfloat)(36+Math.Cos(angle)*r),y=(nfloat)(36+Math.Sin(angle)*r);
                if(i==0)c.MoveTo(x,y);else c.AddLineToPoint(x,y);
            }
            c.ClosePath();c.AddEllipseInRect(new CGRect(23,23,26,26));
            c.DrawPath(CGPathDrawingMode.EOFill);
            return images[key]=UIGraphics.GetImageFromCurrentImageContext();
        }
        finally{UIGraphics.EndImageContext();}
    }
    internal UIImage? FileImage(string path)
    {
        if (!images.TryGetValue(path, out var image)) images[path] = image = File.Exists(path) ? UIImage.FromFile(path) : null;
        return image;
    }
    internal UIImage? Blur(UIImage? source, string key)
    {
        if (source?.CGImage is null) return null;
        if (images.TryGetValue("blur:" + key, out var cached)) return cached;
        // Downsample before reading pixels: the blur only consumes a small proxy.
        int w=240,h=136;byte[] pixels=new byte[w*h*4];
        using var space=CGColorSpace.CreateDeviceRGB();
        using(var bitmap=new CGBitmapContext(pixels,w,h,8,w*4,space,CGImageAlphaInfo.PremultipliedLast))bitmap.DrawImage(new CGRect(0,0,w,h),source.CGImage);
        int[] argb=new int[w*h];for(int i=0;i<argb.Length;i++)argb[i]=unchecked((int)0xff000000)|(pixels[i*4]<<16)|(pixels[i*4+1]<<8)|pixels[i*4+2];
        int[] blurred=HyperLane.MobileBlurPixels.Create(argb,w,h);byte[] rgba=new byte[1920*1080*4];for(int i=0;i<blurred.Length;i++){rgba[i*4]=(byte)(blurred[i]>>16);rgba[i*4+1]=(byte)(blurred[i]>>8);rgba[i*4+2]=(byte)blurred[i];rgba[i*4+3]=255;}
        using var target=new CGBitmapContext(rgba,1920,1080,8,1920*4,space,CGImageAlphaInfo.PremultipliedLast);using var image=target.ToImage();
        // The stage may still reference a previous image during its crossfade.
        // Drop cache ownership rather than disposing an image still being drawn.
        foreach(string old in images.Keys.Where(k=>k.StartsWith("blur:",StringComparison.Ordinal)).Take(Math.Max(0,images.Keys.Count(k=>k.StartsWith("blur:",StringComparison.Ordinal))-2)).ToArray())images.Remove(old);
        return images["blur:"+key]=image==null?null:UIImage.FromImage(image);
    }
    internal static CGRect Rect(double x, double y, double w, double h) => new(x, y, w, h);
    internal static UIColor Color(int r, int g, int b, double a = 1) => UIColor.FromRGBA(r / 255f, g / 255f, b / 255f, (float)Math.Clamp(a, 0, 1));
    internal static UIColor Difficulty(string name) => name.ToLowerInvariant() switch
    {
        "easy" => Color(119,220,80), "normal" => Color(79,181,239), "hard" => Color(255,185,66),
        "expert" => Color(255,80,92), "master" => Color(151,77,248), "special" or "custom" or "" => Color(245,115,220), _ => Color(178,211,229)
    };
    internal static string DifficultyName(string name) => string.IsNullOrWhiteSpace(name) || name.Equals("Custom", StringComparison.OrdinalIgnoreCase) ? "SPECIAL" : name;
    internal static string Rank(int score) => score >= 1000000 ? "Psi" : score >= 995000 ? "Ex+" : score >= 990000 ? "Ex" : score >= 950000 ? "SS" : score >= 900000 ? "S" : score >= 860000 ? "A" : score >= 820000 ? "B" : score >= 780000 ? "C" : "";
    internal static double Ease(double p) => (1 - Math.Pow(2, -10 * Math.Clamp(p, 0, 1))) / (1 - Math.Pow(2, -10));
    internal static void Fill(CGContext c, CGRect r, UIColor color) { c.SetFillColor(color.CGColor); c.FillRect(r); }
    internal static void Stroke(CGContext c, CGRect r, UIColor color, double width = 2) { c.SetStrokeColor(color.CGColor); c.SetLineWidth((nfloat)width); c.StrokeRect(r); }
    internal static void Polygon(CGContext c, double[] points, UIColor? color = null, bool clip = false)
    {
        c.BeginPath(); c.MoveTo((nfloat)points[0], (nfloat)points[1]);
        for (int i = 2; i < points.Length; i += 2) c.AddLineToPoint((nfloat)points[i], (nfloat)points[i+1]);
        c.ClosePath(); if (clip) c.Clip(); else { c.SetFillColor((color ?? UIColor.Black).CGColor); c.FillPath(); }
    }
    internal static void Image(UIImage? image, CGRect rect, double opacity = 1, bool crop = false)
    {
        if (image is null || opacity <= 0) return;
        using var c = UIGraphics.GetCurrentContext(); c.SaveState(); if(crop)c.ClipToRect(rect);
        double fit = crop ? Math.Max((double)rect.Width / (double)image.Size.Width, (double)rect.Height / (double)image.Size.Height)
            : Math.Min((double)rect.Width / (double)image.Size.Width, (double)rect.Height / (double)image.Size.Height);
        double w = (double)image.Size.Width * fit, h = (double)image.Size.Height * fit;
        image.Draw(new CGRect((double)rect.GetMidX() - w/2, (double)rect.GetMidY() - h/2, w,h), CGBlendMode.Normal, (nfloat)Math.Clamp(opacity,0,1)); c.RestoreState();
    }
    internal static void Text(string text, CGRect r, double size, UIColor? color = null, bool center = false, double opacity = 1, bool decorative = false)
    {
        if (opacity <= 0 || text.Length == 0) return;
        decorative |= text is "MASTER" or "EXPERT" or "HARD" or "NORMAL" or "EASY" or "SPECIAL";
        using var font = decorative&&checkpointFace!=null?UIFont.FromName(checkpointFace,(nfloat)size)??UIFont.SystemFontOfSize((nfloat)size):UIFont.SystemFontOfSize((nfloat)size);
        using var value = new NSString(text);
        var attr = new UIStringAttributes { Font = font, ForegroundColor = (color ?? Color(250,250,252)).ColorWithAlpha((nfloat)Math.Clamp(opacity,0,1)) };
        CGSize measured = value.GetSizeUsingAttributes(attr);
        nfloat fittedSize=(nfloat)(size*Math.Min(1,(double)r.Width/Math.Max(1,(double)measured.Width)));
        using var fitted = decorative&&checkpointFace!=null?UIFont.FromName(checkpointFace,fittedSize)??UIFont.SystemFontOfSize(fittedSize):UIFont.SystemFontOfSize(fittedSize);
        attr.Font = fitted; measured = value.GetSizeUsingAttributes(attr);
        value.DrawString(new CGPoint(center ? r.GetMidX() - measured.Width/2 : r.X, r.GetMidY() - measured.Height/2), attr);
    }
    internal static void Button(CGContext c, string label, CGRect r, double size = 35, bool active = false, double opacity = 1)
    {
        double x=(double)r.X,y=(double)r.Y,w=(double)r.Width,h=(double)r.Height;
        Polygon(c,[x+w/10,y,x+w*9/10,y,x+w,y+h/2,x+w*9/10,y+h,x+w/10,y+h,x,y+h/2],active?Color(46,24,65,.82*opacity):Color(5,4,9,.63*opacity));
        Text(label,r,size,center:true,opacity:opacity);
    }
    internal static void TitleTwoLines(string text, CGRect rect, double size)
    {
        using var value=new NSString(text);using var font=UIFont.SystemFontOfSize((nfloat)size);
        var attributes=new UIStringAttributes{Font=font};
        if(value.GetSizeUsingAttributes(attributes).Width<=rect.Width){Text(text,rect,size);return;}
        int split=0;double best=double.MaxValue;
        for(int i=1;i<text.Length;i++)
        {
            if(char.IsLowSurrogate(text[i]))continue;
            using var a=new NSString(text[..i]);using var b=new NSString(text[i..]);
            double width=Math.Max((double)a.GetSizeUsingAttributes(attributes).Width,(double)b.GetSizeUsingAttributes(attributes).Width);
            if(width<best){best=width;split=i;}
        }
        if(split==0){Text(text,rect,size);return;}
        double fitted=size*Math.Min(1,(double)rect.Width/Math.Max(1,best));
        Text(text[..split].Trim(),new CGRect(rect.X,rect.Y,rect.Width,rect.Height/2),fitted);
        Text(text[split..].Trim(),new CGRect(rect.X,rect.Y+rect.Height/2,rect.Width,rect.Height/2),fitted);
    }
    public void Dispose() { foreach(var v in Sprites.Values)v.Dispose();foreach(var v in images.Values)v?.Dispose();foreach(var v in textImages.Values)v.Dispose(); }
}
