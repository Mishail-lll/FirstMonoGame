using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Linq;
using SkiaSharp;
using Svg.Skia;

class Program
{
    static readonly int[] SCALES = new[] { 4 };

    class ImgEntry
    {
        public string Name;
        public SKBitmap Bitmap;
        public int W => Bitmap.Width;
        public int H => Bitmap.Height;
        public Rect PackedRect;
    }

    struct Rect { public int X, Y, W, H; public Rect(int x, int y, int w, int h) { X = x; Y = y; W = w; H = h; } }

    class Node
    {
        public int X, Y, W, H;
        public Node Right, Down;
        public bool Used;
        public Node(int x, int y, int w, int h) { X = x; Y = y; W = w; H = h; }
        public Node Insert(int w, int h)
        {
            if (Used)
            {
                var r = Right?.Insert(w, h);
                if (r != null) return r;
                return Down?.Insert(w, h);
            }
            else
            {
                if (w > W || h > H) return null;
                if (w == W && h == H)
                {
                    Used = true;
                    return this;
                }
                Used = true;
                Right = new Node(X + w, Y, W - w, h);
                Down = new Node(X, Y + h, W, H - h);
                W = w; H = h;
                return this;
            }
        }
    }

    static int Main(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("Usage: SvgRasterizer <inputDir> <outputDir> <baseSizePx>");
            Console.WriteLine("Example: SvgRasterizer ./Content/SVG ./Content/Generated 128");
            return 1;
        }

        string inputDir = args[0];
        string outputDir = args[1];
        if (!int.TryParse(args[2], out int baseSize))
        {
            Console.WriteLine("baseSizePx must be an integer.");
            return 1;
        }

        if (!Directory.Exists(inputDir))
        {
            Console.WriteLine($"Input directory not found: {inputDir}");
            return 2;
        }
        Directory.CreateDirectory(outputDir);

        var svgFiles = Directory.GetFiles(inputDir, "*.svg", SearchOption.TopDirectoryOnly);
        Console.WriteLine($"Found {svgFiles.Length} SVG files in {inputDir}.");

        var images = new List<ImgEntry>();

        foreach (var svgPath in svgFiles)
        {
            string name = Path.GetFileNameWithoutExtension(svgPath);
            Console.WriteLine($"Processing: {name}");
            try
            {
                using (var stream = File.OpenRead(svgPath))
                {
                    var svg = new SKSvg();
                    svg.Load(stream);
                    var picture = svg.Picture;
                    if (picture == null)
                    {
                        Console.WriteLine($"  -> Warning: {name} contains no picture (skipped).");
                        continue;
                    }
                    var bounds = picture.CullRect;
                    if (bounds.Width <= 0 || bounds.Height <= 0)
                    {
                        Console.WriteLine($"  -> Warning: {name} has invalid bounds (skipped).");
                        continue;
                    }

                    foreach (var scale in SCALES)
                    {
                        int px = baseSize * scale;
                        int outW = px;
                        int outH = px;

                        var info = new SKImageInfo(outW, outH, SKColorType.Bgra8888, SKAlphaType.Premul);
                        var bitmap = new SKBitmap(info);
                        using (var canvas = new SKCanvas(bitmap))
                        {
                            canvas.Clear(SKColors.Transparent);

                            float srcW = bounds.Width;
                            float srcH = bounds.Height;
                            float scaleFactor = Math.Min(outW / srcW, outH / srcH);

                            float tx = (outW - srcW * scaleFactor) * 0.5f - bounds.Left * scaleFactor;
                            float ty = (outH - srcH * scaleFactor) * 0.5f - bounds.Top * scaleFactor;

                            canvas.Save();
                            canvas.Translate(tx, ty);
                            canvas.Scale(scaleFactor, scaleFactor);
                            canvas.DrawPicture(picture);
                            canvas.Restore();
                            canvas.Flush();
                        }

                        images.Add(new ImgEntry { Name = name, Bitmap = bitmap });
                        Console.WriteLine($"  -> Rasterized {name} to {outW}x{outH}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ERROR processing {name}: {ex.Message}");
            }
        }

        if (images.Count == 0)
        {
            Console.WriteLine("No images to pack. Exiting.");
            return 0;
        }

        images.Sort((a, b) => Math.Max(b.W, b.H).CompareTo(Math.Max(a.W, a.H)));

        int maxSide = images.Max(i => Math.Max(i.W, i.H));
        int atlasW = Math.Max(128, NextPowerOfTwo(maxSide));
        int atlasH = atlasW;
        bool packed = false;

        while (!packed)
        {
            var root = new Node(0, 0, atlasW, atlasH);
            var rects = new Dictionary<ImgEntry, Rect>();
            bool fail = false;
            foreach (var img in images)
            {
                var node = root.Insert(img.W, img.H);
                if (node == null) { fail = true; break; }
                rects[img] = new Rect(node.X, node.Y, img.W, img.H);
            }
            if (!fail)
            {
                foreach (var kv in rects)
                    kv.Key.PackedRect = kv.Value;
                packed = true;
            }
            else
            {
                atlasW *= 2;
                atlasH *= 2;
                if (atlasW > 8192 || atlasH > 8192)
                {
                    Console.WriteLine("Failed to pack images into reasonable atlas size.");
                    return 3;
                }
            }
        }

        Console.WriteLine($"Atlas size: {atlasW}x{atlasH}");

        // создаём сам атлас
        var atlasInfo = new SKImageInfo(atlasW, atlasH, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var atlasBmp = new SKBitmap(atlasInfo))
        using (var canvas = new SKCanvas(atlasBmp))
        {
            canvas.Clear(SKColors.Transparent);
            foreach (var img in images)
            {
                var r = img.PackedRect;
                canvas.DrawBitmap(img.Bitmap, new SKPoint(r.X, r.Y));
            }
            canvas.Flush();

            using (var image = SKImage.FromBitmap(atlasBmp))
            using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
            {
                string outAtlas = Path.Combine(outputDir, "atlas-texture.png");
                using (var fs = File.Open(outAtlas, FileMode.Create, FileAccess.Write, FileShare.None))
                    data.SaveTo(fs);
                Console.WriteLine($"Wrote atlas: {outAtlas}");
            }
        }

        // создаём XML в нужном формате
        var textureElement = new XElement("Texture", "Generated/atlas-texture");
        var regionsElement = new XElement("Regions");

        foreach (var img in images)
        {
            var r = img.PackedRect;
            regionsElement.Add(new XElement("Region",
                new XAttribute("name", img.Name),
                new XAttribute("x", r.X),
                new XAttribute("y", r.Y),
                new XAttribute("width", r.W),
                new XAttribute("height", r.H)
            ));
        }

        var rootXml = new XElement("TextureAtlas", textureElement, regionsElement);
        var xdoc = new XDocument(new XDeclaration("1.0", "utf-8", null), rootXml);

        string xmlPath = Path.Combine(outputDir, "atlas.xml");
        xdoc.Save(xmlPath);
        Console.WriteLine($"Wrote XML metadata: {xmlPath}");

        foreach (var im in images) im.Bitmap.Dispose();

        Console.WriteLine("Done.");
        return 0;
    }

    static int NextPowerOfTwo(int v)
    {
        int p = 1; while (p < v) p <<= 1; return p;
    }
}
