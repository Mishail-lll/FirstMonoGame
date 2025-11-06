using System;
using System.IO;
using SkiaSharp;
using Svg.Skia;

class Program
{
    static readonly int[] SCALES = new[] { 1, 2, 3 };

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

                        using (var bitmap = new SKBitmap(info))
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

                            using (var img = SKImage.FromBitmap(bitmap))
                            using (var data = img.Encode(SKEncodedImageFormat.Png, 100))
                            {
                                string outName = $"{name}{scale}.png";
                                string outPath = Path.Combine(outputDir, outName);
                                string tmp = outPath + ".tmp";
                                using (var fs = File.Open(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                                {
                                    data.SaveTo(fs);
                                }
                                if (File.Exists(outPath)) File.Delete(outPath);
                                File.Move(tmp, outPath);

                                Console.WriteLine($"  -> Saved {outName} ({outW}x{outH})");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ERROR processing {name}: {ex.Message}");
            }
        }

        Console.WriteLine("Done.");
        return 0;
    }
}
