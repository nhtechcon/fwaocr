using System.Reflection;
using PdfSharpCore.Fonts;

namespace FreeWindowsAutoOCR.Fonts;

/// <summary>
/// Custom font resolver that provides OCRmyPDF's Occulta glyphless TrueType font
/// for invisible OCR text layers. The font has zero-area glyph outlines covering the
/// entire Basic Multilingual Plane (65K codepoints), so nothing is ever visible
/// on screen regardless of rendering mode.
/// </summary>
public class GlyphlessFontResolver : IFontResolver
{
    /// <summary>
    /// Font family name used when constructing XFont instances for the OCR layer.
    /// Must match the name table inside the embedded TTF.
    /// </summary>
    public const string FontFamily = "Occulta";

    private static readonly byte[] FontData = LoadEmbeddedFont();

    public string DefaultFontName => FontFamily;

    public byte[] GetFont(string faceName)
    {
        // All face names resolve to the same glyphless font
        return FontData;
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        // Map every request for our font family to the single embedded face
        if (familyName.Equals(FontFamily, StringComparison.OrdinalIgnoreCase))
            return new FontResolverInfo(FontFamily);

        // Fallback: return the glyphless font for any unknown family too,
        // since this app only generates OCR text — no decorative fonts needed.
        return new FontResolverInfo(FontFamily);
    }

    private static byte[] LoadEmbeddedFont()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("pdf.ttf")
            ?? throw new InvalidOperationException(
                "Embedded resource 'pdf.ttf' not found. Ensure it is included as an EmbeddedResource.");

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
