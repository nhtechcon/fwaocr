using System.Globalization;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Docnet.Core;
using Docnet.Core.Models;
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

    // Font resource key used in the PDF content stream
    private const string OcrFontKey = "F_OCR";

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

            AddTextLayer(pdfDoc, pdfDoc.Pages[i], result.OcrResult, result.ImageWidth, result.ImageHeight);
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
    /// Adds an invisible text layer using raw PDF content stream operators.
    /// Uses text rendering mode 3 (invisible) — text is hidden but searchable/selectable.
    /// </summary>
    private static void AddTextLayer(
        PdfDocument doc, PdfPage page, OcrResult ocrResult, int imageWidth, int imageHeight)
    {
        RegisterFont(doc, page);

        double scaleX = page.Width.Point / imageWidth;
        double scaleY = page.Height.Point / imageHeight;
        double pageHeight = page.Height.Point;

        var sb = new StringBuilder();
        sb.AppendLine("q");        // Save graphics state
        sb.AppendLine("BT");       // Begin text object
        sb.AppendLine("3 Tr");     // Rendering mode 3 = invisible (neither fill nor stroke)

        foreach (var line in ocrResult.Lines)
        {
            foreach (var word in line.Words)
            {
                var rect = word.BoundingRect;
                double x = rect.X * scaleX;
                // PDF origin is bottom-left; OCR origin is top-left
                double y = pageHeight - (rect.Y + rect.Height) * scaleY;
                double fontSize = Math.Max(rect.Height * scaleY * 0.85, 1.0);

                // Set font + size, then absolute position via text matrix, then draw word
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "/{0} {1:F2} Tf\n", OcrFontKey, fontSize);
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "1 0 0 1 {0:F2} {1:F2} Tm\n", x, y);
                sb.AppendFormat("({0}) Tj\n", EscapePdfString(word.Text));
            }
        }

        sb.AppendLine("ET");       // End text object
        sb.AppendLine("Q");        // Restore graphics state

        AppendContentStream(doc, page, sb.ToString());
    }

    /// <summary>
    /// Registers the Helvetica Type1 font (one of 14 standard PDF fonts — no embedding needed)
    /// in the page's resource dictionary under the key F_OCR.
    /// </summary>
    private static void RegisterFont(PdfDocument doc, PdfPage page)
    {
        var fonts = page.Resources.Elements.GetDictionary("/Font");
        if (fonts == null)
        {
            fonts = new PdfDictionary(doc);
            page.Resources.Elements.SetObject("/Font", fonts);
        }

        string key = "/" + OcrFontKey;
        if (fonts.Elements.GetObject(key) != null)
            return;

        var fontDict = new PdfDictionary(doc);
        fontDict.Elements.SetName("/Type", "/Font");
        fontDict.Elements.SetName("/Subtype", "/Type1");
        fontDict.Elements.SetName("/BaseFont", "/Helvetica");
        fontDict.Elements.SetName("/Encoding", "/WinAnsiEncoding");

        doc.Internals.AddObject(fontDict);
        fonts.Elements.SetReference(key, fontDict);
    }

    /// <summary>
    /// Appends a raw content stream to the page's Contents array.
    /// </summary>
    private static void AppendContentStream(PdfDocument doc, PdfPage page, string content)
    {
        var bytes = Encoding.ASCII.GetBytes(content);
        var streamDict = new PdfDictionary(doc);
        streamDict.CreateStream(bytes);
        doc.Internals.AddObject(streamDict);
        page.Contents.Elements.Add(streamDict.Reference);
    }

    /// <summary>
    /// Escapes special characters for a PDF literal string: backslash, parentheses.
    /// </summary>
    private static string EscapePdfString(string text)
    {
        return text
            .Replace("\\", "\\\\")
            .Replace("(", "\\(")
            .Replace(")", "\\)");
    }

    private record PageOcrResult(OcrResult? OcrResult, int ImageWidth, int ImageHeight);
}
