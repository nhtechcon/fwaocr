using FreeWindowsAutoOCR.Fonts;
using PdfSharpCore.Drawing;
using PdfSharpCore.Fonts;
using UglyToad.PdfPig;
using Xunit;

namespace FreeWindowsAutoOCR.Tests;

public class OcrProcessorTests
{
    /// <summary>
    /// Ensure the glyphless font resolver is registered before any test runs.
    /// </summary>
    static OcrProcessorTests()
    {
        if (GlobalFontSettings.FontResolver == null)
            GlobalFontSettings.FontResolver = new GlyphlessFontResolver();
    }

    [Fact]
    public void GlyphlessFontResolver_ResolvesTypeface()
    {
        var resolver = new GlyphlessFontResolver();
        var info = resolver.ResolveTypeface(GlyphlessFontResolver.FontFamily, false, false);
        Assert.NotNull(info);
    }

    [Fact]
    public void GlyphlessFontResolver_ReturnsFontData()
    {
        var resolver = new GlyphlessFontResolver();
        var data = resolver.GetFont(GlyphlessFontResolver.FontFamily);
        Assert.NotNull(data);
        Assert.True(data.Length > 0);
    }

    /// <summary>
    /// Verifies that text drawn with the glyphless font via XGraphics
    /// is extractable by an independent PDF reader (PdfPig).
    /// </summary>
    [Fact]
    public void DrawString_WithGlyphlessFont_TextIsExtractable()
    {
        var doc = new PdfSharpCore.Pdf.PdfDocument();
        var page = doc.AddPage();

        using (var gfx = XGraphics.FromPdfPage(page))
        {
            var font = new XFont(GlyphlessFontResolver.FontFamily, 12);
            gfx.DrawString("Hello World", font, XBrushes.Black,
                new XRect(100, 400, 200, 20), XStringFormats.CenterLeft);
        }

        using var ms = new MemoryStream();
        doc.Save(ms);
        ms.Position = 0;

        using var reader = UglyToad.PdfPig.PdfDocument.Open(ms.ToArray());
        var extractedText = reader.GetPage(1).Text;

        Assert.Contains("Hello", extractedText);
        Assert.Contains("World", extractedText);
    }

    /// <summary>
    /// Verifies that multiple words placed at different positions are all extractable.
    /// </summary>
    [Fact]
    public void DrawString_WithGlyphlessFont_MultipleWordsAreAllExtractable()
    {
        var doc = new PdfSharpCore.Pdf.PdfDocument();
        var page = doc.AddPage();

        var words = new[] { "Invoice", "Date", "Total", "Amount" };

        using (var gfx = XGraphics.FromPdfPage(page))
        {
            var font = new XFont(GlyphlessFontResolver.FontFamily, 11);
            double y = 100;
            foreach (var word in words)
            {
                gfx.DrawString(word, font, XBrushes.Black,
                    new XRect(72, y, 200, 20), XStringFormats.CenterLeft);
                y += 25;
            }
        }

        using var ms = new MemoryStream();
        doc.Save(ms);
        ms.Position = 0;

        using var reader = UglyToad.PdfPig.PdfDocument.Open(ms.ToArray());
        var extractedText = reader.GetPage(1).Text;

        foreach (var word in words)
            Assert.Contains(word, extractedText);
    }
}
