using System.IO;
using SkiaSharp;
using ReportDesigner.Core.Model;
using ReportDesigner.Core.Rendering;

namespace ReportDesigner.Core.Export
{
    public sealed class PdfExporter : IReportExporter
    {
        public PdfExportOptions Options { get; }

        public string FileExtension => ".pdf";
        public string MimeType => "application/pdf";

        public PdfExporter(PdfExportOptions? options = null)
        {
            Options = options ?? new PdfExportOptions();
        }

        public byte[] Export(ReportDocument document)
        {
            using var ms = new MemoryStream();
            using var pdf = SKDocument.CreatePdf(ms);

            foreach (var page in document.Pages)
            {
                // Page width/height are in points (1/72 inch). Skia uses points for PDF pages.
                var canvas = pdf.BeginPage((float)page.Width, (float)page.Height);
                RenderPageToCanvas(canvas, page);
                pdf.EndPage();
            }

            pdf.Close();
            return ms.ToArray();
        }

        public void ExportToFile(ReportDocument document, string filePath)
        {
            var bytes = Export(document);
            File.WriteAllBytes(filePath, bytes);
        }

        private void RenderPageToCanvas(SKCanvas canvas, RenderedPage page)
        {
            foreach (var element in page.Elements)
            {
                switch (element)
                {
                    case RenderedRectElement rect:
                        DrawRect(canvas, rect);
                        break;
                    case RenderedLineElement line:
                        DrawLine(canvas, line);
                        break;
                    case RenderedEllipseElement ellipse:
                        DrawEllipse(canvas, ellipse);
                        break;
                    case RenderedImageElement image:
                        DrawImage(canvas, image);
                        break;
                    case RenderedTextElement text:
                        DrawText(canvas, text);
                        break;
                }
            }
        }

        private static void DrawEllipse(SKCanvas canvas, RenderedEllipseElement el)
        {
            var rect = new SKRect(el.X, el.Y, el.X + el.Width, el.Y + el.Height);
            if (!string.IsNullOrWhiteSpace(el.FillColor))
            {
                using var paint = new SKPaint { IsAntialias = true, Color = ParseColor(el.FillColor), Style = SKPaintStyle.Fill };
                canvas.DrawOval(rect, paint);
            }
            if (!string.IsNullOrWhiteSpace(el.StrokeColor) && el.StrokeWidth > 0)
            {
                using var paint = new SKPaint { IsAntialias = true, Color = ParseColor(el.StrokeColor), Style = SKPaintStyle.Stroke, StrokeWidth = el.StrokeWidth };
                canvas.DrawOval(rect, paint);
            }
        }

        private static void DrawImage(SKCanvas canvas, RenderedImageElement el)
        {
            // Only handle data URIs (base64) — URL images not available in server-side PDF
            if (string.IsNullOrWhiteSpace(el.Src) || !el.Src.StartsWith("data:")) return;
            try
            {
                var comma = el.Src.IndexOf(',');
                if (comma < 0) return;
                var b64 = el.Src[(comma + 1)..];
                var bytes = Convert.FromBase64String(b64);
                using var bmp = SKBitmap.Decode(bytes);
                if (bmp is null) return;
                using var img = SKImage.FromBitmap(bmp);
                var dest = new SKRect(el.X, el.Y, el.X + el.Width, el.Y + el.Height);
                canvas.DrawImage(img, dest);
            }
            catch { /* skip unrenderable images */ }
        }

        private static void DrawRect(SKCanvas canvas, RenderedRectElement rect)
        {
            canvas.Save();
            var skRect = new SKRect(rect.X, rect.Y, rect.X + rect.Width, rect.Y + rect.Height);
            float rx = rect.BorderRadius > 0 ? rect.Width  * rect.BorderRadius / 100f : 0f;
            float ry = rect.BorderRadius > 0 ? rect.Height * rect.BorderRadius / 100f : 0f;

            if (!string.IsNullOrWhiteSpace(rect.FillColor))
            {
                using var paint = new SKPaint { IsAntialias = true, Color = ParseColor(rect.FillColor), Style = SKPaintStyle.Fill };
                if (rx > 0) canvas.DrawRoundRect(skRect, rx, ry, paint);
                else        canvas.DrawRect(skRect, paint);
            }
            if (!string.IsNullOrWhiteSpace(rect.StrokeColor) && rect.StrokeWidth > 0)
            {
                using var paint = new SKPaint { IsAntialias = true, Color = ParseColor(rect.StrokeColor), Style = SKPaintStyle.Stroke, StrokeWidth = rect.StrokeWidth };
                if (rx > 0) canvas.DrawRoundRect(skRect, rx, ry, paint);
                else        canvas.DrawRect(skRect, paint);
            }
            canvas.Restore();
        }

        private static void DrawLine(SKCanvas canvas, RenderedLineElement line)
        {
            using var paint = new SKPaint { IsAntialias = true, Color = ParseColor(line.StrokeColor), Style = SKPaintStyle.Stroke, StrokeWidth = (float)line.StrokeWidth };
            canvas.DrawLine((float)line.X, (float)line.Y, (float)line.X2, (float)line.Y2, paint);
        }

        private void DrawText(SKCanvas canvas, RenderedTextElement text)
        {
            if (string.IsNullOrEmpty(text.Text)) return;

            var style = text.Style;

            using var paint = new SKPaint
            {
                IsAntialias = true,
                Color = ParseColor(style?.ForeColor ?? Options.DefaultForeColor),
            };

            var weight = style?.Bold == true ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
            var slant = style?.Italic == true ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;

            using var typeface = SKTypeface.FromFamilyName(style?.FontFamily ?? Options.DefaultFontFamily, weight, SKFontStyleWidth.Normal, slant);
            using var font = new SKFont(typeface, style?.FontSize ?? Options.DefaultFontSize);

            paint.Typeface = typeface;
            paint.TextSize = style?.FontSize ?? Options.DefaultFontSize;

            // Clip to field bounds
            var clipRect = new SKRect((float)text.X, (float)text.Y, (float)(text.X + text.Width), (float)(text.Y + text.Height));
            canvas.Save();
            canvas.ClipRect(clipRect);

            var metrics = font.Metrics;
            var textY = (float)text.Y + ((float)text.Height - (metrics.Descent - metrics.Ascent)) / 2f - metrics.Ascent;

            var textWidth = paint.MeasureText(text.Text);
            float textX;
            switch (text.Alignment)
            {
                case FieldAlignment.Right:
                    textX = (float)(text.X + text.Width) - (style?.PaddingRight ?? 2f) - textWidth;
                    break;
                case FieldAlignment.Center:
                    textX = (float)text.X + ((float)text.Width - textWidth) / 2f;
                    break;
                default:
                    textX = (float)text.X + (style?.PaddingLeft ?? 2f);
                    break;
            }

            canvas.DrawText(text.Text, textX, textY, paint);

            if (style?.Underline == true)
            {
                using var uPaint = new SKPaint { Color = paint.Color, StrokeWidth = 0.5f, IsAntialias = true };
                var uy = textY + metrics.Descent * 0.5f;
                canvas.DrawLine(textX, uy, textX + textWidth, uy, uPaint);
            }

            if (style?.Border != BorderSides.None && style?.Border is not null)
                DrawFieldBorder(canvas, text, style);

            canvas.Restore();
        }

        private static void DrawFieldBorder(SKCanvas canvas, RenderedTextElement text, FieldStyle style)
        {
            var color = ParseColor(style.BorderColor ?? "#000000");
            var w = (float)(style.BorderWidth);
            var x1 = (float)text.X;
            var y1 = (float)text.Y;
            var x2 = (float)(text.X + text.Width);
            var y2 = (float)(text.Y + text.Height);

            using var paint = new SKPaint { IsAntialias = true, Color = color, Style = SKPaintStyle.Stroke, StrokeWidth = w };

            if ((style.Border & BorderSides.Top) != 0)
                canvas.DrawLine(x1, y1, x2, y1, paint);
            if ((style.Border & BorderSides.Bottom) != 0)
                canvas.DrawLine(x1, y2, x2, y2, paint);
            if ((style.Border & BorderSides.Left) != 0)
                canvas.DrawLine(x1, y1, x1, y2, paint);
            if ((style.Border & BorderSides.Right) != 0)
                canvas.DrawLine(x2, y1, x2, y2, paint);
        }

        private static SKColor ParseColor(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return SKColors.Black;
            return SKColor.TryParse(hex, out var c) ? c : SKColors.Black;
        }
    }

    /// <summary>Options for the PDF exporter.</summary>
    public sealed class PdfExportOptions
    {
        public string DefaultFontFamily { get; set; } = "Arial";
        public float DefaultFontSize { get; set; } = 9f;
        public string DefaultForeColor { get; set; } = "#000000";
    }

}
