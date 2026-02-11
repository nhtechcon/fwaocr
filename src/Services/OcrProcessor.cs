using System.Runtime.InteropServices.WindowsRuntime;
using Docnet.Core;
using Docnet.Core.Models;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace FreeWindowsAutoOCR.Services;

public class OcrProcessor
{
    // Max render dimensions — A4 at ~300 DPI. Pages are scaled to fit while keeping aspect ratio.
    private const int RenderMaxWidth = 2480;
    private const int RenderMaxHeight = 3508;

    public async Task ProcessAsync(string pdfPath)
    {
        var ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();
        if (ocrEngine == null)
            throw new InvalidOperationException(
                "No OCR language pack installed. Install one via Windows Settings > Language.");

        var pdfBytes = await File.ReadAllBytesAsync(pdfPath);

        // Phase 1: Render pages to images and run OCR
        var pageResults = await RunOcrOnPages(pdfBytes, ocrEngine);

        // Phase 2: Open PDF in memory and overlay invisible text layer
        using var stream = new MemoryStream(pdfBytes);
        using var pdfDoc = PdfReader.Open(stream, PdfDocumentOpenMode.Modify);

        for (int i = 0; i < pdfDoc.PageCount && i < pageResults.Count; i++)
        {
            var result = pageResults[i];
            if (result.OcrResult == null || string.IsNullOrWhiteSpace(result.OcrResult.Text))
                continue;

            AddTextLayer(pdfDoc.Pages[i], result.OcrResult, result.ImageWidth, result.ImageHeight);
        }

        pdfDoc.Save(pdfPath);
    }

    private static async Task<List<PageOcrResult>> RunOcrOnPages(byte[] pdfBytes, OcrEngine ocrEngine)
    {
        var results = new List<PageOcrResult>();

        using var docReader = DocLib.Instance.GetDocReader(
            pdfBytes, new PageDimensions(RenderMaxWidth, RenderMaxHeight));

        var pageCount = docReader.GetPageCount();

        for (int i = 0; i < pageCount; i++)
        {
            using var pageReader = docReader.GetPageReader(i);
            var rawBytes = pageReader.GetImage();
            var width = pageReader.GetPageWidth();
            var height = pageReader.GetPageHeight();

            if (rawBytes == null || rawBytes.Length == 0 || width == 0 || height == 0)
            {
                results.Add(new PageOcrResult(null, 0, 0));
                continue;
            }

            // Docnet returns BGRA8 pixel data — convert to SoftwareBitmap for Windows OCR
            var bitmap = new SoftwareBitmap(
                BitmapPixelFormat.Bgra8, width, height, BitmapAlphaMode.Premultiplied);
            bitmap.CopyFromBuffer(rawBytes.AsBuffer());

            var ocrResult = await ocrEngine.RecognizeAsync(bitmap);
            bitmap.Dispose();

            results.Add(new PageOcrResult(ocrResult, width, height));
        }

        return results;
    }

    /// <summary>
    /// Draws invisible (transparent) text on the PDF page at OCR-detected word positions.
    /// Makes the PDF searchable and text-selectable without altering its visual appearance.
    /// </summary>
    private static void AddTextLayer(PdfPage pdfPage, OcrResult ocrResult, int imageWidth, int imageHeight)
    {
        using var gfx = XGraphics.FromPdfPage(pdfPage, XGraphicsPdfPageOptions.Append);

        // Map rendered-image pixel coords → PDF point coords
        double scaleX = pdfPage.Width.Point / imageWidth;
        double scaleY = pdfPage.Height.Point / imageHeight;

        // Alpha-0 brush: text is invisible but present in the PDF content stream
        var brush = new XSolidBrush(XColor.FromArgb(0, 0, 0, 0));

        foreach (var line in ocrResult.Lines)
        {
            foreach (var word in line.Words)
            {
                var rect = word.BoundingRect;
                double x = rect.X * scaleX;
                double y = rect.Y * scaleY;
                double w = rect.Width * scaleX;
                double h = rect.Height * scaleY;

                // Font size derived from bounding box height
                double fontSize = Math.Max(h * 0.85, 1.0);
                var font = new XFont("Arial", fontSize);

                gfx.DrawString(word.Text, font, brush,
                    new XRect(x, y, w, h), XStringFormats.CenterLeft);
            }
        }
    }

    private record PageOcrResult(OcrResult? OcrResult, int ImageWidth, int ImageHeight);
}
