using System.Drawing.Imaging;
using System.Text.RegularExpressions;
using Tesseract;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;

namespace TextCrate;

internal static class OcrService
{
    public static async Task<string> ReadScreenRegionAsync(Rectangle region, AppSettings settings)
    {
        using var bitmap = Capture(region);
        var candidates = new List<OcrCandidate>();

        try
        {
            candidates.AddRange(RecognizeWithTesseract(bitmap, settings.EnhancedOcr));
        }
        catch
        {
            candidates.Add(await RecognizeWithWindowsAsync(bitmap, "windows-original"));
        }

        if (settings.EnhancedOcr)
        {
            candidates.Add(await RecognizeWithWindowsAsync(bitmap, "windows-original"));

            using var processed = BuildHighContrastVariant(bitmap);
            candidates.Add(await RecognizeWithWindowsAsync(processed, "windows-contrast"));

            using var scaled = Scale(processed, 3);
            candidates.Add(await RecognizeWithWindowsAsync(scaled, "windows-contrast-scaled"));

            using var badge = BuildColoredBadgeVariant(bitmap);
            candidates.Add(await RecognizeWithWindowsAsync(badge, "windows-badge"));

            using var badgeScaled = Scale(badge, 4);
            candidates.Add(await RecognizeWithWindowsAsync(badgeScaled, "windows-badge-scaled"));
        }

        var best = candidates
            .OrderByDescending(ScoreOcrCandidate)
            .FirstOrDefault()?.Text ?? string.Empty;
        best = CleanupCommonOcrText(best);

        return settings.OcrCleanupMode == OcrCleanupMode.CodeAndEnvironmentText
            ? CleanupStructuredText(best)
            : best;
    }

    private static IEnumerable<OcrCandidate> RecognizeWithTesseract(Bitmap bitmap, bool enhanced)
    {
        var tessdata = Path.Combine(AppContext.BaseDirectory, "tessdata");
        using var engine = new TesseractEngine(tessdata, "eng", EngineMode.LstmOnly);
        engine.SetVariable("preserve_interword_spaces", "1");
        engine.SetVariable("user_defined_dpi", "300");

        var candidates = new List<OcrCandidate>();
        var whitelist = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-=:/.,+@#$%()[]{}<>|\\'\" ";
        engine.SetVariable("tessedit_char_whitelist", whitelist);

        candidates.Add(RecognizeTesseractVariant(engine, bitmap, PageSegMode.SparseText, "tess-original-sparse"));
        candidates.Add(RecognizeTesseractVariant(engine, bitmap, PageSegMode.Auto, "tess-original-auto"));

        if (enhanced)
        {
            using var originalScaled = Scale(bitmap, 3);
            candidates.Add(RecognizeTesseractVariant(engine, originalScaled, PageSegMode.SparseText, "tess-original-scaled-sparse"));
            candidates.Add(RecognizeTesseractVariant(engine, originalScaled, PageSegMode.SingleBlock, "tess-original-scaled-block"));

            using var originalSmoothScaled = ScaleSmooth(bitmap, 3);
            candidates.Add(RecognizeTesseractVariant(engine, originalSmoothScaled, PageSegMode.Auto, "tess-original-smooth-scaled-auto"));
            candidates.Add(RecognizeTesseractVariant(engine, originalSmoothScaled, PageSegMode.SingleBlock, "tess-original-smooth-scaled-block"));

            using var darkUi = BuildDarkUiTextVariant(bitmap);
            using var darkUiScaled = Scale(darkUi, 4);
            candidates.Add(RecognizeTesseractVariant(engine, darkUiScaled, PageSegMode.SparseText, "tess-dark-ui-scaled-sparse"));
            candidates.Add(RecognizeTesseractVariant(engine, darkUiScaled, PageSegMode.SingleBlock, "tess-dark-ui-scaled-block"));

            using var processed = BuildHighContrastVariant(bitmap);
            candidates.Add(RecognizeTesseractVariant(engine, processed, PageSegMode.SparseText, "tess-contrast-sparse"));

            using var processedScaled = Scale(processed, 3);
            candidates.Add(RecognizeTesseractVariant(engine, processedScaled, PageSegMode.SparseText, "tess-contrast-scaled-sparse"));
            candidates.Add(RecognizeTesseractVariant(engine, processedScaled, PageSegMode.SingleBlock, "tess-contrast-scaled-block"));

            using var processedSmoothScaled = ScaleSmooth(processed, 3);
            candidates.Add(RecognizeTesseractVariant(engine, processedSmoothScaled, PageSegMode.SingleBlock, "tess-contrast-smooth-scaled-block"));

            using var badge = BuildColoredBadgeVariant(bitmap);
            using var badgeScaled = Scale(badge, 4);
            candidates.Add(RecognizeTesseractVariant(engine, badgeScaled, PageSegMode.SingleBlock, "tess-badge-scaled-block"));
            candidates.Add(RecognizeTesseractVariant(engine, badgeScaled, PageSegMode.SparseText, "tess-badge-scaled-sparse"));
        }

        return candidates;
    }

    private static OcrCandidate RecognizeTesseractVariant(TesseractEngine engine, Bitmap bitmap, PageSegMode pageSegMode, string source)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        using var pix = Pix.LoadFromMemory(stream.ToArray());
        using var page = engine.Process(pix, pageSegMode);
        var confidence = page.GetMeanConfidence();
        if (confidence <= 1)
        {
            confidence *= 100;
        }

        var text = BuildTesseractLayoutText(page, source);
        if (string.IsNullOrWhiteSpace(text))
        {
            text = page.GetText() ?? string.Empty;
        }

        return new OcrCandidate(text, confidence, source);
    }

    private static async Task<OcrCandidate> RecognizeWithWindowsAsync(Bitmap bitmap, string source)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"TextCrate-OCR-{Guid.NewGuid():N}.png");

        try
        {
            bitmap.Save(tempPath, System.Drawing.Imaging.ImageFormat.Png);
            var file = await StorageFile.GetFileFromPathAsync(tempPath);
            await using var stream = await file.OpenStreamForReadAsync();
            var randomAccessStream = stream.AsRandomAccessStream();
            var decoder = await BitmapDecoder.CreateAsync(randomAccessStream);
            using var softwareBitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

            var engine = OcrEngine.TryCreateFromUserProfileLanguages();
            if (engine is null)
            {
                throw new InvalidOperationException("Windows OCR is not available for the current user language. Install an OCR-capable Windows language pack and try again.");
            }

            var result = await engine.RecognizeAsync(softwareBitmap);
            return new OcrCandidate(BuildLayoutText(result), 45, source);
        }
        finally
        {
            try
            {
                File.Delete(tempPath);
            }
            catch
            {
                // Temporary capture cleanup should not hide OCR results.
            }
        }
    }

    private static string BuildTesseractLayoutText(Page page, string source)
    {
        using var iterator = page.GetIterator();
        iterator.Begin();

        var words = new List<RecognizedWord>();
        do
        {
            var text = iterator.GetText(PageIteratorLevel.Word)?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var confidence = iterator.GetConfidence(PageIteratorLevel.Word);
            if (confidence <= 1)
            {
                confidence *= 100;
            }

            var minimumConfidence = source.Contains("badge", StringComparison.OrdinalIgnoreCase) ? 22 : 34;
            if (confidence < minimumConfidence && !LooksImportant(text))
            {
                continue;
            }

            if (!iterator.TryGetBoundingBox(PageIteratorLevel.Word, out var bounds))
            {
                continue;
            }

            words.Add(new RecognizedWord(
                NormalizeOcrToken(text),
                bounds.X1,
                bounds.Y1,
                bounds.Width,
                bounds.Height,
                confidence));
        }
        while (iterator.Next(PageIteratorLevel.Word));

        return BuildLayoutText(words);
    }

    private static string BuildLayoutText(List<RecognizedWord> words)
    {
        words = words
            .Where(word => !string.IsNullOrWhiteSpace(word.Text))
            .OrderBy(word => word.Y)
            .ThenBy(word => word.X)
            .ToList();

        if (words.Count == 0)
        {
            return string.Empty;
        }

        var medianHeight = words.Select(word => word.Height).OrderBy(height => height).ElementAt(words.Count / 2);
        var lineTolerance = Math.Max(8, medianHeight * 0.65);
        var lines = new List<List<RecognizedWord>>();

        foreach (var word in words)
        {
            var centerY = word.Y + (word.Height / 2);
            var line = lines.FirstOrDefault(existing =>
            {
                var existingCenter = existing.Average(item => item.Y + (item.Height / 2));
                return Math.Abs(existingCenter - centerY) <= lineTolerance;
            });

            if (line is null)
            {
                lines.Add([word]);
            }
            else
            {
                line.Add(word);
            }
        }

        return string.Join(Environment.NewLine, lines
            .OrderBy(line => line.Average(word => word.Y))
            .Select(RenderLine));
    }

    private static string BuildLayoutText(OcrResult result)
    {
        var words = result.Lines
            .SelectMany(line => line.Words)
            .Where(word => !string.IsNullOrWhiteSpace(word.Text))
            .Select(word => new RecognizedWord(
                word.Text,
                word.BoundingRect.X,
                word.BoundingRect.Y,
                word.BoundingRect.Width,
                word.BoundingRect.Height,
                45))
            .OrderBy(word => word.Y)
            .ThenBy(word => word.X)
            .ToList();

        if (words.Count == 0)
        {
            return string.Join(Environment.NewLine, result.Lines.Select(line => line.Text));
        }

        return BuildLayoutText(words);
    }

    private static string RenderLine(List<RecognizedWord> words)
    {
        var ordered = words.OrderBy(word => word.X).ToList();
        var result = ordered[0].Text;
        var previous = ordered[0];

        foreach (var word in ordered.Skip(1))
        {
            var gap = word.X - (previous.X + previous.Width);
            var averageCharWidth = Math.Max(4, previous.Width / Math.Max(1, previous.Text.Length));
            var spaces = gap > averageCharWidth * 2.5 ? Math.Min(8, Math.Max(2, (int)Math.Round(gap / averageCharWidth))) : 1;
            result += new string(' ', spaces) + word.Text;
            previous = word;
        }

        return result;
    }

    private static bool LooksImportant(string text)
    {
        return Regex.IsMatch(text, @"^(?:running|healthy|starting|stopped)$", RegexOptions.IgnoreCase)
            || Regex.IsMatch(text, @"\d{1,5}:\d{1,5}")
            || Regex.IsMatch(text, @"\d{4}-\d{2}-\d{2}")
            || text.Contains('_')
            || text.Contains('-');
    }

    private static string NormalizeOcrToken(string text)
    {
        return text.Trim()
            .Replace("|", "I")
            .Replace("`", "'")
            .Replace("：", ":")
            .Replace("–", "-")
            .Replace("—", "-");
    }

    private static string CleanupCommonOcrText(string text)
    {
        var result = Regex.Replace(
            text,
            @"\bHight(?=\s+Light\b)",
            "Night",
            RegexOptions.IgnoreCase);

        result = Regex.Replace(
            result,
            @"\b([0-9OIlSB]{2}):([0-9OIlSB]{2})(?::([0-9OIlSB]{2}))?\b",
            match =>
            {
                var hour = NormalizeTimePart(match.Groups[1].Value);
                var minute = NormalizeTimePart(match.Groups[2].Value);
                var second = match.Groups[3].Success ? NormalizeTimePart(match.Groups[3].Value) : null;

                if (int.TryParse(hour, out var h) && h > 23)
                {
                    var adjusted = hour.ToCharArray();
                    if (adjusted[0] is '6' or '8')
                    {
                        adjusted[0] = '0';
                        var candidate = new string(adjusted);
                        if (int.TryParse(candidate, out h) && h <= 23)
                        {
                            hour = candidate;
                        }
                    }
                }

                minute = RepairInvalidMinuteOrSecond(minute);
                if (second is not null)
                {
                    second = RepairInvalidMinuteOrSecond(second);
                }

                return second is null ? $"{hour}:{minute}" : $"{hour}:{minute}:{second}";
            });

        return result;
    }

    private static string NormalizeTimePart(string value)
    {
        return value
            .Replace('O', '0')
            .Replace('I', '1')
            .Replace('l', '1')
            .Replace('S', '5')
            .Replace('B', '8');
    }

    private static string RepairInvalidMinuteOrSecond(string value)
    {
        if (!int.TryParse(value, out var number) || number <= 59)
        {
            return value;
        }

        var adjusted = value.ToCharArray();
        for (var i = 0; i < adjusted.Length; i++)
        {
            if (adjusted[i] is '6' or '8')
            {
                adjusted[i] = '0';
            }
        }

        var candidate = new string(adjusted);
        return int.TryParse(candidate, out number) && number <= 59 ? candidate : value;
    }

    private static double ScoreOcrCandidate(OcrCandidate candidate)
    {
        var text = candidate.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        var words = Regex.Matches(text, @"[A-Za-z0-9_:/.-]+")
            .Select(match => match.Value)
            .ToList();
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var characters = text.Count(ch => !char.IsWhiteSpace(ch));
        var statusCount = Regex.Matches(text, @"\b(?:running|healthy|starting|stopped|web|api|db|tunnel)\b", RegexOptions.IgnoreCase).Count;

        double score = candidate.Confidence * 4;
        score += words.Count(word => word.Length >= 4) * 8;
        score += Math.Min(90, characters * 1.4);
        score += text.Count(ch => ch is '_' or '=' or ':' or '/' or '-' or '.') * 3;
        score += statusCount * 25;
        score += Regex.Matches(text, @"\b\d{4}-\d{2}-\d{2}\b").Count * 20;
        score += Regex.Matches(text, @"\b\d{1,5}:\d{1,5}(?::\d{2})?\b").Count * 20;

        var shortWordCount = words.Count(word => word.Length <= 2);
        var oneCharWordCount = words.Count(word => word.Length == 1);
        var averageWordLength = words.Count == 0 ? 0 : words.Average(word => word.Length);
        var consonantBlobCount = words.Count(word =>
            word.Length >= 4
            && word.Any(char.IsLetter)
            && !Regex.IsMatch(word, "[aeiouAEIOU]"));

        score -= shortWordCount * 8;
        score -= oneCharWordCount * 15;
        score -= consonantBlobCount * 10;
        score -= Regex.Matches(text, @"\b[a-zA-Z]{1,2}\s+[a-zA-Z]{1,2}\b").Count * 12;
        score -= text.Count(ch => ch is 'ï' or '¿' or '½' or 'Â' or '©' or '§') * 25;

        if (averageWordLength > 0 && averageWordLength < 3.2 && words.Count > 4)
        {
            score -= 80;
        }

        if (candidate.Source.Contains("badge", StringComparison.OrdinalIgnoreCase)
            && lines.Length > 3
            && statusCount == 0)
        {
            score -= 180;
        }

        if (candidate.Source.Contains("dark-ui", StringComparison.OrdinalIgnoreCase)
            && (statusCount > 0 || Regex.IsMatch(text, @"\b[a-z0-9]+(?:[-_][a-z0-9]+)+\b", RegexOptions.IgnoreCase)))
        {
            score += 45;
        }

        return score;
    }

    private static int ScoreOcrText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        var score = text.Length;
        score += text.Count(char.IsLetterOrDigit) * 2;
        score += text.Count(ch => ch is '_' or '=' or ':' or '/' or '-' or '.') * 3;
        score -= text.Count(ch => ch is '�' or '©' or '§') * 8;
        return score;
    }

    private static string CleanupStructuredText(string text)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var cleaned = lines.Select(line =>
        {
            var result = line.TrimEnd();

            // Windows OCR often reads ENV_VAR=value as ENV VAR=value on dark editor screenshots.
            var equalsIndex = result.IndexOf('=');
            if (equalsIndex > 0)
            {
                var key = result[..equalsIndex].Trim();
                if (Regex.IsMatch(key, @"^[A-Z0-9][A-Z0-9 _-]*$"))
                {
                    key = Regex.Replace(key, @"\s+", "_");
                    result = key + result[equalsIndex..];
                }
            }

            result = Regex.Replace(result, @"\bDATABASE\s+URL\b", "DATABASE_URL");
            result = Regex.Replace(result, @"\bREDIS\s+HOST\b", "REDIS_HOST");
            result = Regex.Replace(result, @"\bJWT\s+SECRET\b", "JWT_SECRET");
            result = Regex.Replace(result, @"\bJWT\s+REFRESH\s+SECRET\b", "JWT_REFRESH_SECRET");
            result = Regex.Replace(result, @"\bJWT\s+EXPIRATION\b", "JWT_EXPIRATION");
            result = Regex.Replace(result, @"\bENCRYPTION\s+KEY\b", "ENCRYPTION_KEY");
            result = Regex.Replace(result, @"\bNEXT\s+PUBLIC\s+API\s+URL\b", "NEXT_PUBLIC_API_URL");
            result = Regex.Replace(result, @"\bFRONTEND\s+URL\b", "FRONTEND_URL");
            result = Regex.Replace(result, @"\bALLOWED\s+ORIGINS\b", "ALLOWED_ORIGINS");
            result = Regex.Replace(result, @"\bCOOKIE\s+SECURE\b", "COOKIE_SECURE");
            result = Regex.Replace(result, @"\bGITHUB\s+TOKEN\b", "GITHUB_TOKEN");
            result = Regex.Replace(result, @"\bGIT\s+BRANCH\b", "GIT_BRANCH");
            result = Regex.Replace(result, @"\bENABLE\s+SWAGGER\b", "ENABLE_SWAGGER");

            result = RepairDashboardPunctuation(result);

            return result;
        });

        return string.Join(Environment.NewLine, cleaned).Trim();
    }

    private static string RepairDashboardPunctuation(string line)
    {
        var result = Regex.Replace(
            line,
            @"\b(\d{4}-\d{2}-\d{2})\s+(\d{2})(\d{2})(:\d{2})\b",
            "$1 $2:$3$4");

        result = Regex.Replace(
            result,
            @"\b(\d{4}-\d{2}-\d{2})\s+(\d{2})(\d{2})(\d{2})\b",
            "$1 $2:$3:$4");

        result = Regex.Replace(
            result,
            @"(?<![\d.])(\d{2,5})(\d{2,5})(?![\d.])",
            match =>
            {
                var left = match.Groups[1].Value;
                var right = match.Groups[2].Value;
                return left == right ? $"{left}:{right}" : match.Value;
            });

        return result;
    }

    private static Bitmap BuildHighContrastVariant(Bitmap source)
    {
        var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb);
        var invert = AverageBrightness(source) < 110;

        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                var color = source.GetPixel(x, y);
                var brightness = (int)((color.R * 0.299) + (color.G * 0.587) + (color.B * 0.114));
                if (invert)
                {
                    brightness = 255 - brightness;
                }

                var value = brightness > 150 ? 255 : 0;
                bitmap.SetPixel(x, y, Color.FromArgb(value, value, value));
            }
        }

        return bitmap;
    }

    private static Bitmap BuildColoredBadgeVariant(Bitmap source)
    {
        var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb);

        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                var color = source.GetPixel(x, y);
                var hue = color.GetHue();
                var saturation = color.GetSaturation();
                var brightness = color.GetBrightness();

                var isBadgeBackground = saturation > 0.35f
                    && brightness > 0.25f
                    && ((hue >= 20 && hue <= 55) || (hue >= 80 && hue <= 165));
                var isLikelyWhiteText = saturation < 0.22f && brightness > 0.62f;
                var isLikelyBlueText = saturation > 0.35f && hue >= 175 && hue <= 220 && brightness > 0.35f;

                if (isLikelyWhiteText || isLikelyBlueText)
                {
                    bitmap.SetPixel(x, y, Color.Black);
                }
                else if (isBadgeBackground || brightness < 0.22f)
                {
                    bitmap.SetPixel(x, y, Color.White);
                }
                else
                {
                    var gray = brightness > 0.55f ? 0 : 255;
                    bitmap.SetPixel(x, y, Color.FromArgb(gray, gray, gray));
                }
            }
        }

        return bitmap;
    }

    private static Bitmap BuildDarkUiTextVariant(Bitmap source)
    {
        var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb);

        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                var color = source.GetPixel(x, y);
                var hue = color.GetHue();
                var saturation = color.GetSaturation();
                var brightness = color.GetBrightness();

                var isWhiteText = saturation < 0.25f && brightness > 0.58f;
                var isCyanText = saturation > 0.35f && hue >= 175 && hue <= 220 && brightness > 0.30f;
                var isBadgeText = saturation < 0.30f && brightness > 0.75f;

                if (isWhiteText || isCyanText || isBadgeText)
                {
                    bitmap.SetPixel(x, y, Color.Black);
                }
                else
                {
                    bitmap.SetPixel(x, y, Color.White);
                }
            }
        }

        return bitmap;
    }

    private static int AverageBrightness(Bitmap source)
    {
        long total = 0;
        var samples = 0;
        var stepX = Math.Max(1, source.Width / 80);
        var stepY = Math.Max(1, source.Height / 80);

        for (var y = 0; y < source.Height; y += stepY)
        {
            for (var x = 0; x < source.Width; x += stepX)
            {
                var color = source.GetPixel(x, y);
                total += (int)((color.R * 0.299) + (color.G * 0.587) + (color.B * 0.114));
                samples++;
            }
        }

        return samples == 0 ? 255 : (int)(total / samples);
    }

    private static Bitmap Scale(Bitmap source, int factor)
    {
        var bitmap = new Bitmap(source.Width * factor, source.Height * factor, PixelFormat.Format24bppRgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
        graphics.DrawImage(source, new Rectangle(Point.Empty, bitmap.Size));
        return bitmap;
    }

    private static Bitmap ScaleSmooth(Bitmap source, int factor)
    {
        var bitmap = new Bitmap(source.Width * factor, source.Height * factor, PixelFormat.Format24bppRgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        graphics.DrawImage(source, new Rectangle(Point.Empty, bitmap.Size));
        return bitmap;
    }

    private static Bitmap Capture(Rectangle region)
    {
        var bitmap = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(region.Location, Point.Empty, region.Size);
        return bitmap;
    }

    private sealed record OcrCandidate(string Text, float Confidence, string Source);
    private sealed record RecognizedWord(string Text, double X, double Y, double Width, double Height, float Confidence);
}
