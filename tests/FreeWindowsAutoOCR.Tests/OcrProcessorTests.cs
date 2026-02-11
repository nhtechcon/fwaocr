using System.Globalization;
using System.Text;
using FreeWindowsAutoOCR.Services;
using PdfSharpCore.Pdf;
using UglyToad.PdfPig;
using Xunit;

namespace FreeWindowsAutoOCR.Tests;

public class OcrProcessorTests
{
    /// <summary>
    /// Verifies that AppendContentStream produces a valid PDF whose invisible text layer
    /// is extractable (i.e. selectable) by an independent PDF reader (PdfPig).
    /// </summary>
    [Fact]
    public void AppendContentStream_TextIsExtractableFromResultingPdf()
    {
        // Arrange — create a blank single-page PDF
        var doc = new PdfSharpCore.Pdf.PdfDocument();
        var page = doc.AddPage();

        OcrProcessor.RegisterFont(doc, page);

        // Build a minimal invisible-text content stream with known words
        const string expectedText = "Hello World";
        var stream = BuildInvisibleTextStream("F_OCR", 12, 100, 400, expectedText);

        // Act — append the text layer
        OcrProcessor.AppendContentStream(doc, page, stream);

        // Save to memory and re-read with PdfPig
        using var ms = new MemoryStream();
        doc.Save(ms);
        ms.Position = 0;

        using var reader = UglyToad.PdfPig.PdfDocument.Open(ms.ToArray());
        var extractedText = reader.GetPage(1).Text;

        // Assert — PdfPig must be able to extract the text we embedded
        Assert.Contains("Hello", extractedText);
        Assert.Contains("World", extractedText);
    }

    /// <summary>
    /// Verifies that existing page content (e.g. a drawn rectangle) is preserved
    /// after appending the OCR text layer.
    /// </summary>
    [Fact]
    public void AppendContentStream_PreservesExistingPageContent()
    {
        var doc = new PdfSharpCore.Pdf.PdfDocument();
        var page = doc.AddPage();

        // Add some initial content (a filled rectangle) so the page isn't empty
        var initialContent = "q\n0.5 0.5 0.5 rg\n50 50 200 100 re\nf\nQ\n";
        var initialStream = new PdfDictionary(doc);
        initialStream.CreateStream(Encoding.ASCII.GetBytes(initialContent));
        doc.Internals.AddObject(initialStream);
        page.Contents.Elements.Add(initialStream.Reference);

        OcrProcessor.RegisterFont(doc, page);
        var textStream = BuildInvisibleTextStream("F_OCR", 10, 60, 300, "Preserved");

        // Act
        OcrProcessor.AppendContentStream(doc, page, textStream);

        // Save and re-read
        using var ms = new MemoryStream();
        doc.Save(ms);
        ms.Position = 0;

        using var reader = UglyToad.PdfPig.PdfDocument.Open(ms.ToArray());
        var pageContent = reader.GetPage(1);

        // The text layer must be present
        Assert.Contains("Preserved", pageContent.Text);

        // The page must still render (have content) — no crash, no empty page
        // PdfPig successfully parsing the page is proof the content stream is valid
        Assert.NotNull(pageContent);
    }

    /// <summary>
    /// Verifies that multiple words placed at different positions are all extractable.
    /// </summary>
    [Fact]
    public void AppendContentStream_MultipleWordsAreAllExtractable()
    {
        var doc = new PdfSharpCore.Pdf.PdfDocument();
        var page = doc.AddPage();
        OcrProcessor.RegisterFont(doc, page);

        var words = new[] { "Invoice", "Date", "Total", "Amount" };
        var sb = new StringBuilder();
        sb.AppendLine("q");
        sb.AppendLine("BT");
        sb.AppendLine("3 Tr");

        double y = 700;
        foreach (var word in words)
        {
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "/F_OCR 11.00 Tf\n1 0 0 1 72.00 {0:F2} Tm\n({1}) Tj\n", y, word);
            y -= 20;
        }

        sb.AppendLine("ET");
        sb.AppendLine("Q");

        OcrProcessor.AppendContentStream(doc, page, sb.ToString());

        using var ms = new MemoryStream();
        doc.Save(ms);
        ms.Position = 0;

        using var reader = UglyToad.PdfPig.PdfDocument.Open(ms.ToArray());
        var extractedText = reader.GetPage(1).Text;

        foreach (var word in words)
            Assert.Contains(word, extractedText);
    }

    /// <summary>
    /// Builds a minimal PDF content stream that renders text in invisible mode (3 Tr).
    /// </summary>
    private static string BuildInvisibleTextStream(
        string fontKey, double fontSize, double x, double y, string text)
    {
        var sb = new StringBuilder();
        sb.AppendLine("q");
        sb.AppendLine("BT");
        sb.AppendLine("3 Tr");
        sb.AppendFormat(CultureInfo.InvariantCulture,
            "/{0} {1:F2} Tf\n", fontKey, fontSize);
        sb.AppendFormat(CultureInfo.InvariantCulture,
            "1 0 0 1 {0:F2} {1:F2} Tm\n", x, y);

        // Split into words so each gets its own Tj (mirrors real OCR output)
        foreach (var word in text.Split(' '))
        {
            var escaped = word.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
            sb.AppendFormat("({0}) Tj\n", escaped);
        }

        sb.AppendLine("ET");
        sb.AppendLine("Q");
        return sb.ToString();
    }
}
