using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace IconResizer
{
    class Program
    {
        static void Main(string[] args)
        {
            // Parâmetros esperados: [0] = Caminho imagem fonte (png), [1] = Caminho destino (ico)
            if (args.Length < 2)
            {
                Console.WriteLine("Uso: IconResizer.exe <input.png> <output.ico>");
                return;
            }

            string inputFile = args[0];
            string outputFile = args[1];

            // Resoluções desejadas. O Windows Explorer usa 256x256 para ícones Extra Grandes.
            // 512x512 é suportado, mas raramente usado pelo Shell, porém incluiremos conforme solicitado.
            int[] sizes = new[] { 512, 256, 128, 64, 48, 32, 16 };

            try
            {
                Console.WriteLine($"Processando ícone de: {inputFile}");
                using (var srcImage = Image.FromFile(inputFile))
                using (var fs = new FileStream(outputFile, FileMode.Create))
                using (var writer = new BinaryWriter(fs))
                {
                    // 1. Escreve o Cabeçalho do ICO (ICONDIR)
                    // Reservado (2 bytes) = 0
                    writer.Write((short)0);
                    // Tipo (2 bytes) = 1 (Ícone)
                    writer.Write((short)1);
                    // Contagem de Imagens (2 bytes)
                    writer.Write((short)sizes.Length);

                    var imagesData = new List<byte[]>();
                    int offset = 6 + (16 * sizes.Length); // 6 bytes header + 16 bytes por entrada de diretório

                    // 2. Prepara os dados e escreve as Entradas de Diretório (ICONDIRENTRY)
                    foreach (var size in sizes)
                    {
                        using (var resized = ResizeImage(srcImage, size, size))
                        using (var ms = new MemoryStream())
                        {
                            // Salvamos como PNG. O Windows Vista+ suporta PNG dentro de ICO.
                            // Isso garante transparência perfeita e arquivo menor para resoluções grandes.
                            resized.Save(ms, ImageFormat.Png);
                            byte[] buffer = ms.ToArray();
                            imagesData.Add(buffer);

                            // Largura (1 byte). 0 significa 256 ou mais.
                            writer.Write((byte)(size >= 256 ? 0 : size));
                            // Altura (1 byte)
                            writer.Write((byte)(size >= 256 ? 0 : size));
                            // Paleta de cores (1 byte) - 0 se não usar paleta indexada
                            writer.Write((byte)0);
                            // Reservado (1 byte)
                            writer.Write((byte)0);
                            // Planos de cor (2 bytes)
                            writer.Write((short)1);
                            // Bits por pixel (2 bytes)
                            writer.Write((short)32);
                            // Tamanho da imagem em bytes (4 bytes)
                            writer.Write((int)buffer.Length);
                            // Offset onde os dados da imagem começam (4 bytes)
                            writer.Write((int)offset);

                            offset += buffer.Length;
                        }
                    }

                    // 3. Escreve os dados binários das imagens (PNGs) sequencialmente
                    foreach (var data in imagesData)
                    {
                        writer.Write(data);
                    }
                }
                Console.WriteLine($"Ícone gerado com sucesso em: {outputFile}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro crítico ao gerar ícone: {ex.Message}");
                Environment.Exit(1);
            }
        }

        static Bitmap ResizeImage(Image image, int width, int height)
        {
            var destRect = new Rectangle(0, 0, width, height);
            var destImage = new Bitmap(width, height);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }

            return destImage;
        }
    }
}
