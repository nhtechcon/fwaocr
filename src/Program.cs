using FreeWindowsAutoOCR.Fonts;
using FreeWindowsAutoOCR.UI;
using PdfSharpCore.Fonts;

namespace FreeWindowsAutoOCR;

static class Program
{
    [STAThread]
    static void Main()
    {
        // Register Tesseract's glyphless font for invisible OCR text layers
        GlobalFontSettings.FontResolver = new GlyphlessFontResolver();

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // Ensure only one instance runs at a time
        using var mutex = new Mutex(true, "FreeWindowsAutoOCR_SingleInstance", out bool isNewInstance);
        if (!isNewInstance)
        {
            MessageBox.Show(
                "Free Windows Auto OCR is already running.",
                "Already Running",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        Application.Run(new TrayApplicationContext());
    }
}
