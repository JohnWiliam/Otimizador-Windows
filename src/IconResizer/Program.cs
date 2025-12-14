using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

// Simple resizer to generate multiple PNGs for ICO packing
string inputFile = args[0];
string outputDir = args[1];

int[] sizes = new[] { 16, 32, 48, 64, 256 };

if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

using var srcImage = Image.FromFile(inputFile);

foreach (var size in sizes)
{
    using var destImage = new Bitmap(size, size);
    using var graphics = Graphics.FromImage(destImage);
    
    graphics.CompositingMode = CompositingMode.SourceCopy;
    graphics.CompositingQuality = CompositingQuality.HighQuality;
    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
    graphics.SmoothingMode = SmoothingMode.HighQuality;
    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

    using var wrapMode = new ImageAttributes();
    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
    
    graphics.DrawImage(srcImage, new Rectangle(0, 0, size, size), 0, 0, srcImage.Width, srcImage.Height, GraphicsUnit.Pixel, wrapMode);
    
    string outFile = Path.Combine(outputDir, $"{size}.png");
    destImage.Save(outFile, ImageFormat.Png);
    Console.WriteLine($"Generated {outFile}");
}
