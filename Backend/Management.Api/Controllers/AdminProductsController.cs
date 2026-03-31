using Management.Domain.Entities;
using Management.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using ClosedXML.Excel;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Advanced;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace Management.Api.Controllers
{
    [ApiController]
    [Route("api/admin/products")]
    [Authorize(Roles = "admin")]
    public class AdminProductsController : ControllerBase
    {
        private readonly IProductRepository _products;
        private readonly ProductAffiliateLinkRepository _productAffiliateLinks;
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpClientFactory;

        public AdminProductsController(IProductRepository products, ProductAffiliateLinkRepository productAffiliateLinks, IConfiguration config, IHttpClientFactory httpClientFactory)
        {
            _products = products;
            _productAffiliateLinks = productAffiliateLinks;
            _config = config;
            _httpClientFactory = httpClientFactory;
        }

        private string GetShopeeCdnImagePrefix()
        {
            var prefix = _config["Shopee:CdnImagePrefix"] ?? string.Empty;
            return prefix.Trim();
        }

        private string GetShopeeCdnVideoPrefix()
        {
            var prefix = _config["Shopee:CdnVideoPrefix"] ?? string.Empty;
            prefix = prefix.Trim();
            if (string.IsNullOrWhiteSpace(prefix)) prefix = "https://down-tx-sg.vod.susercontent.com/";
            if (!prefix.EndsWith("/")) prefix += "/";
            return prefix;
        }

        private static string? BuildCdnUrl(string prefix, string? key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            if (Uri.TryCreate(key, UriKind.Absolute, out _)) return key;

            var p = (prefix ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(p)) return key;
            if (!p.EndsWith("/")) p += "/";
            return p + key.TrimStart('/');
        }

        private static decimal? ToVnd(long? raw)
        {
            if (raw == null) return null;
            return ToVnd(raw, null);
        }

        private static decimal? ToVnd(long? raw, string? platform)
        {
            if (raw == null) return null;
            var r = raw.Value;
            decimal vnd;
            // Shopee prices are commonly returned in 1e-5 units (price * 100000).
            if (string.Equals(platform, "Shopee", StringComparison.OrdinalIgnoreCase))
            {
                vnd = r / 100000m;
            }
            // AccessTrade is persisted as VND * 1000 in our ingestion code.
            else if (string.Equals(platform, "AccessTrade", StringComparison.OrdinalIgnoreCase))
            {
                vnd = r / 1000m;
            }
            else if (r % 1000L == 0L)
            {
                vnd = r / 1000m;
            }
            else
            {
                // Some historical data may already be persisted in VND (no scaling).
                vnd = r;
            }
            return decimal.Round(vnd, 0, MidpointRounding.AwayFromZero);
        }

        private static float? ExtractRatingStar(string? itemRatingJson)
        {
            if (string.IsNullOrWhiteSpace(itemRatingJson)) return null;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(itemRatingJson);
                if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object) return null;
                if (!doc.RootElement.TryGetProperty("rating_star", out var el) || el.ValueKind == System.Text.Json.JsonValueKind.Null) return null;
                if (el.TryGetSingle(out var s)) return s;
                if (el.TryGetDouble(out var d)) return (float)d;
                return null;
            }
            catch
            {
                return null;
            }
        }

        private static int ComputeTemplate2Height(IReadOnlyList<Image<Rgba32>> images, int width)
        {
            if (images.Count == 0) return 1280;

            var outerPadding = (int)Math.Round(width * 0.035);
            var thumbGapX = (int)Math.Round(width * 0.018);
            var gapY = 8;

            var visibleCount = Math.Min(5, images.Count);
            var thumbSize = (int)Math.Round((width - outerPadding * 2 - thumbGapX * (visibleCount - 1)) / (double)visibleCount);
            var thumbStripHeight = thumbSize;

            var mainW = Math.Max(1, width - outerPadding * 2);
            var maxMainH = 1;
            foreach (var img in images)
            {
                if (img.Width <= 0 || img.Height <= 0) continue;
                var scale = mainW / (double)img.Width;
                var h = Math.Max(1, (int)Math.Round(img.Height * scale));
                if (h > maxMainH) maxMainH = h;
            }

            var height = outerPadding * 2 + maxMainH + gapY + thumbStripHeight;
            return Math.Max(200, height);
        }

        private static Image<Rgba32> ComposeSingleSlideTemplate2(IReadOnlyList<Image<Rgba32>> images, int activeIdx, int width, int height)
        {
            // Colors taken from frontend preview/canvas logic.
            var bg = new Rgba32(0x0f, 0x17, 0x2a);
            var bg2 = new Rgba32(0x11, 0x18, 0x27);
            var borderInactive = new Rgba32(255, 255, 255, 64);
            var borderActive = new Rgba32(0xef, 0x44, 0x44);

            var outerPadding = (int)Math.Round(width * 0.035);
            var thumbGapX = (int)Math.Round(width * 0.018);
            var gapY = 8;

            var visibleCount = Math.Min(5, images.Count);
            var start = 0;
            if (images.Count > visibleCount)
            {
                start = Math.Max(0, Math.Min(activeIdx - 2, images.Count - visibleCount));
            }

            var thumbSize = (int)Math.Round((width - outerPadding * 2 - thumbGapX * (visibleCount - 1)) / (double)visibleCount);
            var thumbStripHeight = thumbSize;
            var mainHeight = Math.Max(1, height - outerPadding * 2 - thumbStripHeight - gapY);
            var mainRect = new Rectangle(outerPadding, outerPadding, width - outerPadding * 2, mainHeight);
            var stripY = height - gapY - 136;

            var canvas = new Image<Rgba32>(width, height);
            FillVerticalGradient(canvas, bg, bg2);

            // Snapshot background so we can restore it when applying rounded-corner masks.
            using var backgroundSnapshot = canvas.Clone();

            // Main image: fit by width (contain), height auto.
            using (var mainImg = images[activeIdx].Clone())
            {
                var srcW = mainImg.Width;
                var srcH = mainImg.Height;
                DrawContain(canvas, mainImg, mainRect, cornerRadius: 28);

                // Border should hug the actual image area (exclude contain padding), not the whole mainRect.
                if (srcW > 0 && srcH > 0)
                {
                    var scale = Math.Min(mainRect.Width / (double)srcW, mainRect.Height / (double)srcH);
                    var w = Math.Max(1, (int)Math.Round(srcW * scale));
                    var h = Math.Max(1, (int)Math.Round(srcH * scale));
                    var x0 = (mainRect.Width - w) / 2;
                    var y0 = (mainRect.Height - h) / 2;
                    var imageRect = new Rectangle(mainRect.X + x0, mainRect.Y + y0, w, h);
                    ApplyRoundedCornersRegionToBackground(canvas, backgroundSnapshot, imageRect, radius: 28);
                }
            }

            // MP4 doesn't support alpha; restore background pixels at the corners to make rounding visible.
            ApplyRoundedCornersRegionToBackground(canvas, backgroundSnapshot, mainRect, radius: 28);

            // Thumbnails: cover (crop) for nicer look.
            for (var i = 0; i < visibleCount; i++)
            {
                var idx = start + i;
                var x = outerPadding + i * (thumbSize + thumbGapX);
                var thumbRect = new Rectangle(x, stripY, thumbSize, thumbSize);
                var isActive = idx == activeIdx;

                using (var thumbImg = images[idx].Clone())
                {
                    DrawCover(canvas, thumbImg, thumbRect, cornerRadius: 18);
                }

                var stroke = isActive ? 4 : 1;
                var color = isActive ? borderActive : borderInactive;
                DrawRoundedRectStroke(canvas, thumbRect, stroke, color, radius: 18);
                ApplyRoundedCornersRegionToBackground(canvas, backgroundSnapshot, thumbRect, radius: 18);
            }

            return canvas;
        }

        [HttpGet("export-affiliate-links")]
        public async Task<IActionResult> ExportAffiliateLinks([FromQuery] string? search, [FromQuery] string? filter = null)
        {
            var rows = await _productAffiliateLinks.GetForExportAsync(search, filter);

            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Sheet1");

            ws.Cell(1, 1).Value = "Liên kết gốc";
            ws.Cell(1, 2).Value = "Sub_id1";
            ws.Cell(1, 3).Value = "Sub_id2";
            ws.Cell(1, 4).Value = "Sub_id3";
            ws.Cell(1, 5).Value = "Sub_id4";
            ws.Cell(1, 6).Value = "Sub_id5";

            var r = 2;
            foreach (var x in rows)
            {
                ws.Cell(r, 1).Value = x.ProductLink;
                ws.Cell(r, 2).Value = x.ExternalItemId;
                ws.Cell(r, 3).Value = x.SubId2;
                ws.Cell(r, 4).Value = x.SubId3;
                ws.Cell(r, 5).Value = x.SubId4;
                ws.Cell(r, 6).Value = x.SubId5;
                r++;
            }

            ws.Range(1, 1, Math.Max(1, r - 1), 6).CreateTable();
            ws.Columns().AdjustToContents();

            await using var ms = new MemoryStream();
            wb.SaveAs(ms);
            ms.Position = 0;

            var fileName = $"affiliate_links_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        private static float? ParseCommissionRate(string? defaultCommissionRateRaw)
        {
            if (string.IsNullOrWhiteSpace(defaultCommissionRateRaw)) return null;
            var s = defaultCommissionRateRaw.Trim().Replace("%", "").Replace(",", ".");
            if (!float.TryParse(s, out var pct)) return null;
            return pct / 100f;
        }

        [HttpGet]
        public async Task<IActionResult> GetList([FromQuery] string? search, [FromQuery] int limit = 200, [FromQuery] string? filter = null)
        {
            var list = await _products.GetLatestAsync(search, limit, filter);
            var imagePrefix = GetShopeeCdnImagePrefix();
            var result = list.Select(p => new
            {
                id = p.Id,
                name = p.Name,
                description = p.Description,
                imageUrl = BuildCdnUrl(imagePrefix, p.ImageUrl),
                price = ToVnd(p.PriceRaw, p.Platform),
                originalPrice = ToVnd(p.PriceBeforeDiscountRaw, p.Platform),
                rating = ExtractRatingStar(p.ItemRatingJson),
                sold = p.SoldRaw,
                commissionRate = ParseCommissionRate(p.DefaultCommissionRateRaw),
                productLink = p.ProductLink,
                isProcessed = p.IsProcessed,
                updatedAt = p.UpdatedAt,
                createdAt = p.CreatedAt,
            });

            return Ok(result);
        }

        public class UpdateProcessedRequest
        {
            public bool IsProcessed { get; set; }
        }

        [HttpPut("{id:long}/processed")]
        public async Task<IActionResult> UpdateProcessed([FromRoute] long id, [FromBody] UpdateProcessedRequest req)
        {
            var p = await _products.GetByIdAsync(id);
            if (p == null) return NotFound();

            p.IsProcessed = req.IsProcessed;
            await _products.UpdateAsync(p);
            return Ok(new { success = true });
        }

        [HttpPost("import-affiliate-links")]
        [RequestSizeLimit(52428800)]
        public async Task<IActionResult> ImportAffiliateLinks(IFormFile file)
        {
            if (file == null || file.Length <= 0) return BadRequest("File is required.");

            int success = 0;
            int skipped = 0;
            var errors = new List<string>();

            static string NormalizeHeader(string s)
            {
                var t = (s ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(t)) return string.Empty;

                t = RemoveDiacritics(t);
                t = t.Replace(" ", string.Empty).Replace("_", string.Empty);
                return t;
            }

            static string RemoveDiacritics(string text)
            {
                if (string.IsNullOrEmpty(text)) return string.Empty;
                var normalized = text.Normalize(NormalizationForm.FormD);
                var sb = new StringBuilder();
                foreach (var ch in normalized)
                {
                    var uc = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
                    if (uc != System.Globalization.UnicodeCategory.NonSpacingMark)
                        sb.Append(ch);
                }
                return sb.ToString().Normalize(NormalizationForm.FormC);
            }
            static string? TrimOrNull(string? s)
            {
                if (s == null) return null;
                var t = s.Trim();
                return string.IsNullOrWhiteSpace(t) ? null : t;
            }

            async Task UpsertRow(int rowNumber, string productLink, string? sub1, string? sub2, string? sub3, string? sub4, string? sub5, string? affiliateLink)
            {
                if (string.IsNullOrWhiteSpace(productLink)) { skipped++; return; }

                var product = await _products.GetByProductLinkAsync(productLink);
                if (product == null || string.IsNullOrWhiteSpace(product.ExternalItemId))
                {
                    skipped++;
                    return;
                }

                var model = new ProductAffiliateLink
                {
                    ExternalItemId = product.ExternalItemId,
                    ProductLink = productLink,
                    SubId1 = TrimOrNull(sub1),
                    SubId2 = TrimOrNull(sub2),
                    SubId3 = TrimOrNull(sub3),
                    SubId4 = TrimOrNull(sub4),
                    SubId5 = TrimOrNull(sub5),
                    AffiliateLink = TrimOrNull(affiliateLink),
                };

                await _productAffiliateLinks.UpsertAsync(model);
                success++;
            }

            try
            {
                var ext = Path.GetExtension(file.FileName ?? string.Empty).Trim().ToLowerInvariant();
                if (ext == ".csv")
                {
                    using var sr = new StreamReader(file.OpenReadStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
                    var headerLine = await sr.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(headerLine)) return BadRequest("CSV is empty.");

                    var headers = SplitCsvLine(headerLine).Select(NormalizeHeader).ToList();
                    int Col(string name)
                    {
                        var n = NormalizeHeader(name);
                        for (var i = 0; i < headers.Count; i++)
                        {
                            if (string.Equals(headers[i], n, StringComparison.OrdinalIgnoreCase)) return i;
                        }
                        return -1;
                    }

                    int FindColCsv(params string[] names)
                    {
                        foreach (var n in names)
                        {
                            var c = Col(n);
                            if (c >= 0) return c;
                        }
                        return -1;
                    }

                    var iProductLink = FindColCsv("Liên kết gốc", "Lien ket goc", "ProductLink");
                    if (iProductLink < 0) return BadRequest("Missing required column: ProductLink");

                    var iSub1 = FindColCsv("Subid1", "SubID1", "Sub_id1");
                    var iSub2 = FindColCsv("Subid2", "SubID2", "Sub_id2");
                    var iSub3 = FindColCsv("Subid3", "SubID3", "Sub_id3");
                    var iSub4 = FindColCsv("Subid4", "SubID4", "Sub_id4");
                    var iSub5 = FindColCsv("Subid5", "SubID5", "Sub_id5");
                    var iAffiliateLink = FindColCsv("AffiliateLink", "Lien ket chuyen doi", "Liên kết chuyển đổi");

                    var rowNumber = 1;
                    while (!sr.EndOfStream)
                    {
                        var line = await sr.ReadLineAsync();
                        rowNumber++;
                        if (string.IsNullOrWhiteSpace(line)) { skipped++; continue; }

                        var cols = SplitCsvLine(line);
                        string Get(int idx) => idx >= 0 && idx < cols.Count ? cols[idx] : string.Empty;

                        try
                        {
                            await UpsertRow(
                                rowNumber,
                                (Get(iProductLink) ?? string.Empty).Trim(),
                                Get(iSub1),
                                Get(iSub2),
                                Get(iSub3),
                                Get(iSub4),
                                Get(iSub5),
                                Get(iAffiliateLink));
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"Row {rowNumber}: {ex.Message}");
                        }
                    }

                    return Ok(new { success = true, imported = success, skipped, errors });
                }

                await using var stream = file.OpenReadStream();
                using var wb = new XLWorkbook(stream);
                var ws = wb.Worksheets.FirstOrDefault();
                if (ws == null) return BadRequest("Excel has no worksheet.");

                var headerRow = ws.FirstRowUsed();
                if (headerRow == null) return BadRequest("Excel is empty.");

                var headersXlsx = headerRow.CellsUsed().ToDictionary(
                    c => NormalizeHeader(c.GetString() ?? string.Empty),
                    c => c.Address.ColumnNumber,
                    StringComparer.OrdinalIgnoreCase);

                int GetCol(string name)
                {
                    return headersXlsx.TryGetValue(NormalizeHeader(name), out var idx) ? idx : -1;
                }

                int FindColXlsx(params string[] names)
                {
                    foreach (var n in names)
                    {
                        var c = GetCol(n);
                        if (c > 0) return c;
                    }
                    return -1;
                }

                var colProductLink = FindColXlsx("ProductLink", "Liên kết gốc", "Lien ket goc");
                if (colProductLink <= 0) return BadRequest("Missing required column: ProductLink");

                var colSub1 = FindColXlsx("Subid1", "SubID1", "Sub_id1");
                var colSub2 = FindColXlsx("Subid2", "SubID2", "Sub_id2");
                var colSub3 = FindColXlsx("Subid3", "SubID3", "Sub_id3");
                var colSub4 = FindColXlsx("Subid4", "SubID4", "Sub_id4");
                var colSub5 = FindColXlsx("Subid5", "SubID5", "Sub_id5");
                var colAffiliateLink = FindColXlsx("AffiliateLink", "Liên kết chuyển đổi", "Lien ket chuyen doi");

                var firstDataRow = headerRow.RowBelow();
                var lastRow = ws.LastRowUsed();
                if (firstDataRow == null || lastRow == null) return Ok(new { success = true, imported = 0, skipped = 0, errors = Array.Empty<string>() });

                for (var r = firstDataRow.RowNumber(); r <= lastRow.RowNumber(); r++)
                {
                    var row = ws.Row(r);
                    string Read(int col) => col > 0 ? (row.Cell(col).GetString() ?? string.Empty) : string.Empty;
                    var productLink = (Read(colProductLink) ?? string.Empty).Trim();

                    try
                    {
                        await UpsertRow(
                            r,
                            productLink,
                            Read(colSub1),
                            Read(colSub2),
                            Read(colSub3),
                            Read(colSub4),
                            Read(colSub5),
                            Read(colAffiliateLink));
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Row {r}: {ex.Message}");
                    }
                }

                return Ok(new { success = true, imported = success, skipped, errors });
            }
            catch (Exception ex)
            {
                return BadRequest($"Import failed: {ex.Message}");
            }
        }

        private static List<string> SplitCsvLine(string line)
        {
            var result = new List<string>();
            if (line == null) return result;

            var sb = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < line.Length; i++)
            {
                var ch = line[i];
                if (ch == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                        continue;
                    }

                    inQuotes = !inQuotes;
                    continue;
                }

                if (ch == ',' && !inQuotes)
                {
                    result.Add(sb.ToString());
                    sb.Clear();
                    continue;
                }

                sb.Append(ch);
            }

            result.Add(sb.ToString());
            return result;
        }

        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetDetail([FromRoute] long id)
        {
            var p = await _products.GetByIdAsync(id);
            if (p == null) return NotFound();

            var imagePrefix = GetShopeeCdnImagePrefix();

            return Ok(new
            {
                id = p.Id,
                platform = p.Platform,
                externalItemId = p.ExternalItemId,
                shopeeItemId = p.ShopeeItemId,
                name = p.Name,
                description = p.Description,
                slug = p.Slug,
                imageUrl = BuildCdnUrl(imagePrefix, p.ImageUrl),
                price = ToVnd(p.PriceRaw, p.Platform),
                originalPrice = ToVnd(p.PriceBeforeDiscountRaw, p.Platform),
                rating = ExtractRatingStar(p.ItemRatingJson),
                sold = p.SoldRaw,
                createdAt = p.CreatedAt,
                updatedAt = p.UpdatedAt,
            });
        }

        private static void ApplyRoundedCornersRegion(Image<Rgba32> img, Rectangle region, int radius)
        {
            if (radius <= 0) return;

            var x0 = Math.Max(0, region.X);
            var y0 = Math.Max(0, region.Y);
            var x1 = Math.Min(img.Width, region.X + region.Width);
            var y1 = Math.Min(img.Height, region.Y + region.Height);
            if (x1 <= x0 || y1 <= y0) return;

            var w = x1 - x0;
            var h = y1 - y0;
            var r = Math.Min(radius, Math.Min(w, h) / 2);
            if (r <= 0) return;

            var rr = r * r;

            img.ProcessPixelRows(accessor =>
            {
                for (var y = y0; y < y1; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    var ly = y - y0;

                    for (var x = x0; x < x1; x++)
                    {
                        var lx = x - x0;

                        var left = lx < r;
                        var right = lx >= w - r;
                        var top = ly < r;
                        var bottom = ly >= h - r;

                        if (!((left && top) || (right && top) || (left && bottom) || (right && bottom)))
                        {
                            continue;
                        }

                        int cx = left ? r : (w - r - 1);
                        int cy = top ? r : (h - r - 1);
                        var dx = lx - cx;
                        var dy = ly - cy;
                        if (dx * dx + dy * dy > rr)
                        {
                            var p = row[x];
                            p.A = 0;
                            row[x] = p;
                        }
                    }
                }
            });
        }

        private static void DrawRoundedRectStroke(Image<Rgba32> canvas, Rectangle rect, int strokeWidth, Rgba32 color, int radius)
        {
            if (strokeWidth <= 0) return;

            var x0 = Math.Max(0, rect.X);
            var y0 = Math.Max(0, rect.Y);
            var x1 = Math.Min(canvas.Width, rect.X + rect.Width);
            var y1 = Math.Min(canvas.Height, rect.Y + rect.Height);
            if (x1 <= x0 || y1 <= y0) return;

            var outer = new Rectangle(x0, y0, x1 - x0, y1 - y0);
            var inner = Rectangle.Inflate(outer, -strokeWidth, -strokeWidth);

            var outerR = Math.Min(radius, Math.Min(outer.Width, outer.Height) / 2);
            var innerR = Math.Max(0, Math.Min(outerR - strokeWidth, Math.Min(inner.Width, inner.Height) / 2));

            static bool InRoundedRect(int x, int y, Rectangle r, int rad)
            {
                if (x < r.X || y < r.Y || x >= r.X + r.Width || y >= r.Y + r.Height) return false;
                if (rad <= 0) return true;

                var lx = x - r.X;
                var ly = y - r.Y;
                var w = r.Width;
                var h = r.Height;
                var rr = rad * rad;

                var left = lx < rad;
                var right = lx >= w - rad;
                var top = ly < rad;
                var bottom = ly >= h - rad;

                if (!((left && top) || (right && top) || (left && bottom) || (right && bottom))) return true;

                int cx = left ? rad : (w - rad - 1);
                int cy = top ? rad : (h - rad - 1);
                var dx = lx - cx;
                var dy = ly - cy;
                return dx * dx + dy * dy <= rr;
            }

            canvas.ProcessPixelRows(accessor =>
            {
                for (var y = outer.Y; y < outer.Y + outer.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (var x = outer.X; x < outer.X + outer.Width; x++)
                    {
                        if (!InRoundedRect(x, y, outer, outerR)) continue;
                        if (inner.Width > 0 && inner.Height > 0 && InRoundedRect(x, y, inner, innerR)) continue;
                        row[x] = color;
                    }
                }
            });
        }

        private static void ApplyRoundedCornersRegionToBackground(Image<Rgba32> img, Image<Rgba32> background, Rectangle region, int radius)
        {
            if (radius <= 0) return;

            var x0 = Math.Max(0, region.X);
            var y0 = Math.Max(0, region.Y);
            var x1 = Math.Min(img.Width, region.X + region.Width);
            var y1 = Math.Min(img.Height, region.Y + region.Height);
            if (x1 <= x0 || y1 <= y0) return;

            var w = x1 - x0;
            var h = y1 - y0;
            var r = Math.Min(radius, Math.Min(w, h) / 2);
            if (r <= 0) return;

            var rr = r * r;
            // Do not use nested ProcessPixelRows here: the ref-like accessor cannot be captured by inner lambdas.
            for (var y = y0; y < y1; y++)
            {
                var row = img.Frames.RootFrame.DangerousGetPixelRowMemory(y).Span;
                var bgRow = background.Frames.RootFrame.DangerousGetPixelRowMemory(y).Span;
                var ly = y - y0;

                for (var x = x0; x < x1; x++)
                {
                    var lx = x - x0;
                    var left = lx < r;
                    var right = lx >= w - r;
                    var top = ly < r;
                    var bottom = ly >= h - r;

                    if (!((left && top) || (right && top) || (left && bottom) || (right && bottom)))
                    {
                        continue;
                    }

                    int cx = left ? r : (w - r - 1);
                    int cy = top ? r : (h - r - 1);
                    var dx = lx - cx;
                    var dy = ly - cy;
                    if (dx * dx + dy * dy > rr)
                    {
                        row[x] = bgRow[x];
                    }
                }
            }
        }

        [HttpGet("{id:long}/media")]
        public async Task<IActionResult> GetMedia([FromRoute] long id)
        {
            var p = await _products.GetByIdAsync(id);
            if (p == null) return NotFound();

            var imagePrefix = GetShopeeCdnImagePrefix();
            var videoPrefix = GetShopeeCdnVideoPrefix();

            var imageUrls = ParseStringArrayJson(p.ImagesJson)
                .Select(x => BuildCdnUrl(imagePrefix, x))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var videoUrls = ExtractVideoUrls(p.VideoInfoListJson, videoPrefix);

            string? affiliateLink = null;
            if (!string.IsNullOrWhiteSpace(p.ExternalItemId))
            {
                var affiliate = await _productAffiliateLinks.GetByExternalItemIdAsync(p.ExternalItemId);
                affiliateLink = affiliate?.AffiliateLink;
            }

            return Ok(new
            {
                productId = p.Id,
                productName = p.Name,
                affiliateLink,
                imageUrls,
                videoUrls,
            });
        }

        [HttpGet("{id:long}/affiliate-data")]
        public async Task<IActionResult> ExportAffiliateData([FromRoute] long id)
        {
            var p = await _products.GetByIdAsync(id);
            if (p == null) return NotFound();

            var imagePrefix = GetShopeeCdnImagePrefix();
            var videoPrefix = GetShopeeCdnVideoPrefix();

            var imageUrls = ParseStringArrayJson(p.ImagesJson)
                .Select(x => BuildCdnUrl(imagePrefix, x))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var videoUrls = ExtractVideoUrls(p.VideoInfoListJson, videoPrefix);

            string? affiliateLink = null;
            if (!string.IsNullOrWhiteSpace(p.ExternalItemId))
            {
                var affiliate = await _productAffiliateLinks.GetByExternalItemIdAsync(p.ExternalItemId);
                affiliateLink = affiliate?.AffiliateLink;
            }

            var prompt = BuildAffiliatePrompt(p.Name) + BuildTitlePrompt(p.Name, affiliateLink);

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(60);

            await using var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                var promptEntry = zip.CreateEntry("prompt.txt", CompressionLevel.Optimal);
                await using (var s = promptEntry.Open())
                await using (var sw = new StreamWriter(s, Encoding.UTF8))
                {
                    await sw.WriteAsync(prompt);
                }

                await AddRemoteFiles(zip, client, imageUrls, "images");
                await AddRemoteFiles(zip, client, videoUrls, "videos");
            }

            ms.Position = 0;
            var safeProductName = MakeSafeFileName(p.Name);
            var fileName = $"{safeProductName}.zip";
            return File(ms.ToArray(), "application/zip", fileName);
        }

        public class RenderReelsRequest
        {
            public string? ProductName { get; set; }
            public string[] ImageUrls { get; set; } = Array.Empty<string>();
            public int Template { get; set; } = 1;
            public int Width { get; set; } = 1080;
            public int Height { get; set; } = 1920;
            public int Fps { get; set; } = 30;
            public double SecondsPerSlide { get; set; } = 3;
            public double TransitionSeconds { get; set; } = 0.35;
            public string? AudioPreviewUrl { get; set; }
            public double AudioVolume { get; set; } = 0.6;
            public List<TextOverlay>? TextOverlays { get; set; }
        }

        public class TextOverlay
        {
            public string? Text { get; set; }
            public double Start { get; set; } = 0;
            public double End { get; set; } = 3;
            public string? Animation { get; set; }
            public double AnimPeriodSec { get; set; } = 1.5;
            public double AnimAmplitudePx { get; set; } = 18;
            public string? X { get; set; }
            public string? Y { get; set; }
            public string? Font { get; set; }
            public int FontSize { get; set; } = 60;
            public string? FontColor { get; set; } = "white";
            public bool Box { get; set; } = false;
            public string? BoxColor { get; set; } = "black@0.5";
            public int BoxBorderW { get; set; } = 20;
        }

        [HttpPost("render-reels")]
        public async Task<IActionResult> RenderReels([FromBody] RenderReelsRequest req)
        {
            if (req == null) return BadRequest("Invalid request");

            var urls = (req.ImageUrls ?? Array.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (urls.Length == 0) return BadRequest("ImageUrls is required");
            if (urls.Length < 2) return BadRequest("Cần ít nhất 2 ảnh để render transition bằng FFmpeg.");

            var width = req.Width <= 0 ? 1080 : req.Width;
            var height = req.Height <= 0 ? 1920 : req.Height;
            var fps = req.Fps <= 0 ? 30 : Math.Min(60, req.Fps);

            var secondsPerSlide = req.SecondsPerSlide <= 0 ? 3 : Math.Min(10, req.SecondsPerSlide);
            var transition = req.TransitionSeconds <= 0 ? 0.35 : Math.Min(2, req.TransitionSeconds);
            if (transition >= secondsPerSlide) transition = Math.Max(0.05, secondsPerSlide * 0.25);

            var ffmpegPath = (_config["Ffmpeg:Path"] ?? _config["TelegramVideoDemo:FfmpegPath"] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(ffmpegPath)) return StatusCode(500, "Missing config: Ffmpeg:Path");
            if (!Path.IsPathRooted(ffmpegPath))
            {
                ffmpegPath = Path.Combine(AppContext.BaseDirectory, ffmpegPath.Replace('/', Path.DirectorySeparatorChar));
            }
            if (!System.IO.File.Exists(ffmpegPath)) return StatusCode(500, $"FFmpeg not found: {ffmpegPath}");

            var workDir = Path.Combine(Path.GetTempPath(), "affiliate-platform", "render-reels", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workDir);

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromMinutes(3);

            try
            {
                var downloadedFiles = new List<(string File, bool IsGif)>();
                var hasGif = false;
                for (var i = 0; i < urls.Length; i++)
                {
                    var url = urls[i];
                    var extension = Path.GetExtension(new Uri(url).AbsolutePath).ToLowerInvariant();
                    var isGif = extension == ".gif";
                    if (isGif) hasGif = true;
                    var local = Path.Combine(workDir, $"img_{i + 1:000}{(isGif ? ".gif" : ".jpg")}");
                    
                    // Download original file
                    await DownloadToFile(client, url, local);
                    
                    downloadedFiles.Add((local, isGif));
                }

                string? audioFile = null;
                var audioUrl = (req.AudioPreviewUrl ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(audioUrl))
                {
                    audioFile = Path.Combine(workDir, "audio.mp3");
                    await DownloadToFile(client, audioUrl, audioFile);
                }

                IReadOnlyList<string> slideFiles;
                if (hasGif)
                {
                    // Compose still slides from first frames for ALL inputs, so GIF slides get the same thumbnail layout.
                    // Then, for GIF slides, overlay the animated GIF onto the composed still slide in the main image area.
                    var stillInputs = new List<string>(downloadedFiles.Count);
                    for (var i = 0; i < downloadedFiles.Count; i++)
                    {
                        var stillPath = Path.Combine(workDir, $"still_{i + 1:000}.png");
                        using (var img = await Image.LoadAsync<Rgba32>(downloadedFiles[i].File))
                        {
                            await img.SaveAsPngAsync(stillPath);
                        }
                        stillInputs.Add(stillPath);
                    }

                    var composedStillSlides = await ComposeSlides(stillInputs, workDir, width, height, req.Template);

                    var mixed = new List<string>(downloadedFiles.Count);
                    var segmentSeconds = secondsPerSlide + transition;
                    for (var i = 0; i < downloadedFiles.Count; i++)
                    {
                        if (!downloadedFiles[i].IsGif)
                        {
                            mixed.Add(composedStillSlides[i]);
                            continue;
                        }

                        var mainRect = req.Template == 2
                            ? ComputeTemplate2MainRect(width, height, downloadedFiles.Count)
                            : ComputeTemplate1MainRect(width, height, downloadedFiles.Count, i);
                        var gifSlideMp4 = Path.Combine(workDir, $"gif_slide_{i + 1:000}.mp4");
                        var overlayArgs = BuildGifOnSlideArgs(downloadedFiles[i].File, composedStillSlides[i], gifSlideMp4, mainRect, segmentSeconds, fps);
                        var (codeOv, errOv) = await RunFfmpeg(ffmpegPath, overlayArgs);
                        if (codeOv != 0 || !System.IO.File.Exists(gifSlideMp4))
                        {
                            // Fallback to still slide if overlay fails
                            mixed.Add(composedStillSlides[i]);
                        }
                        else
                        {
                            mixed.Add(gifSlideMp4);
                        }
                    }

                    slideFiles = mixed;
                }
                else
                {
                    // Compose per-slide frames to match UI preview layout (main image + thumbnail strip + active highlight)
                    slideFiles = await ComposeSlides(downloadedFiles.Select(x => x.File).ToList(), workDir, width, height, req.Template);
                }

                var outputPath = Path.Combine(workDir, "output.mp4");
                var audioVolume = Math.Clamp(req.AudioVolume, 0, 2.0);
                var args = hasGif
                    ? BuildFfmpegArgsMixed(slideFiles, outputPath, width, height, fps, secondsPerSlide, transition, audioFile, audioVolume, req.TextOverlays)
                    : BuildFfmpegArgs(slideFiles, outputPath, fps, secondsPerSlide, transition, audioFile, audioVolume, req.TextOverlays);

                var psi = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = args,
                    WorkingDirectory = workDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using var p = Process.Start(psi);
                if (p == null) return StatusCode(500, "Không khởi chạy được ffmpeg");

                var stderrTask = p.StandardError.ReadToEndAsync();
                await p.WaitForExitAsync();
                var stderr = await stderrTask;

                if (p.ExitCode != 0)
                {
                    return StatusCode(500, string.IsNullOrWhiteSpace(stderr) ? "FFmpeg render failed" : stderr);
                }

                if (!System.IO.File.Exists(outputPath))
                {
                    return StatusCode(500, "FFmpeg không tạo ra file output.");
                }

                var bytes = await System.IO.File.ReadAllBytesAsync(outputPath);
                var safeProductName = MakeSafeFileName(req.ProductName ?? "render-reels");
                var fileName = string.IsNullOrWhiteSpace(safeProductName) ? "render-reels.mp4" : $"{safeProductName}.mp4";
                return File(bytes, "video/mp4", fileName);
            }
            finally
            {
                try { if (Directory.Exists(workDir)) Directory.Delete(workDir, recursive: true); } catch { }
            }
        }

        [HttpPost("render-reels-overlay")]
        [RequestSizeLimit(200_000_000)]
        public async Task<IActionResult> RenderReelsOverlay([FromForm] string req, [FromForm] IFormFile? overlayPng, [FromForm] IFormFile? overlayWebm, [FromForm] string? overlaySegments)
        {
            if (string.IsNullOrWhiteSpace(req)) return BadRequest("Missing form field: req");
            var hasOverlayWebm = overlayWebm != null && overlayWebm.Length > 0;
            var hasSingleOverlay = overlayPng != null && overlayPng.Length > 0;
            var overlayPngs = Request?.Form?.Files?.Where(x => x != null && x.Length > 0 && (x.Name ?? string.Empty).Equals("overlayPngs", StringComparison.OrdinalIgnoreCase)).ToList()
                            ?? new List<IFormFile>();

            var hasSegments = overlayPngs.Count > 0 && !string.IsNullOrWhiteSpace(overlaySegments);
            if (!hasOverlayWebm && !hasSegments && !hasSingleOverlay) return BadRequest("Missing overlay input");

            RenderReelsRequest? payload;
            try
            {
                payload = JsonSerializer.Deserialize<RenderReelsRequest>(req, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                return BadRequest("Invalid req JSON");
            }
            if (payload == null) return BadRequest("Invalid request");

            var urls = (payload.ImageUrls ?? Array.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (urls.Length == 0) return BadRequest("ImageUrls is required");
            if (urls.Length < 2) return BadRequest("Cần ít nhất 2 ảnh để render transition bằng FFmpeg.");

            var width = payload.Width <= 0 ? 1080 : payload.Width;
            var height = payload.Height <= 0 ? 1920 : payload.Height;
            var fps = payload.Fps <= 0 ? 30 : Math.Min(60, payload.Fps);

            var secondsPerSlide = payload.SecondsPerSlide <= 0 ? 3 : Math.Min(10, payload.SecondsPerSlide);
            var transition = payload.TransitionSeconds <= 0 ? 0.35 : Math.Min(2, payload.TransitionSeconds);
            if (transition >= secondsPerSlide) transition = Math.Max(0.05, secondsPerSlide * 0.25);

            var ffmpegPath = (_config["Ffmpeg:Path"] ?? _config["TelegramVideoDemo:FfmpegPath"] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(ffmpegPath)) return StatusCode(500, "Missing config: Ffmpeg:Path");
            if (!Path.IsPathRooted(ffmpegPath))
            {
                ffmpegPath = Path.Combine(AppContext.BaseDirectory, ffmpegPath.Replace('/', Path.DirectorySeparatorChar));
            }
            if (!System.IO.File.Exists(ffmpegPath)) return StatusCode(500, $"FFmpeg not found: {ffmpegPath}");

            var workDir = Path.Combine(Path.GetTempPath(), "affiliate-platform", "render-reels-overlay", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workDir);

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromMinutes(3);

            try
            {
                var downloadedFiles = new List<string>();
                for (var i = 0; i < urls.Length; i++)
                {
                    var url = urls[i];
                    var local = Path.Combine(workDir, $"img_{i + 1:000}.jpg");
                    await DownloadToFile(client, url, local);
                    downloadedFiles.Add(local);
                }

                string? audioFile = null;
                var audioUrl = (payload.AudioPreviewUrl ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(audioUrl))
                {
                    audioFile = Path.Combine(workDir, "audio.mp3");
                    await DownloadToFile(client, audioUrl, audioFile);
                }

                var slideFiles = await ComposeSlides(downloadedFiles, workDir, width, height, payload.Template);

                var basePath = Path.Combine(workDir, "base.mp4");
                var audioVolume = Math.Clamp(payload.AudioVolume, 0, 2.0);
                var args = BuildFfmpegArgs(slideFiles, basePath, fps, secondsPerSlide, transition, audioFile, audioVolume, payload.TextOverlays);

                var psi = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = args,
                    WorkingDirectory = workDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using (var p = Process.Start(psi))
                {
                    if (p == null) return StatusCode(500, "Không khởi chạy được ffmpeg");
                    var stderrTask = p.StandardError.ReadToEndAsync();
                    await p.WaitForExitAsync();
                    var stderr = await stderrTask;
                    if (p.ExitCode != 0)
                    {
                        return StatusCode(500, string.IsNullOrWhiteSpace(stderr) ? "FFmpeg render failed" : stderr);
                    }
                }

                if (!System.IO.File.Exists(basePath)) return StatusCode(500, "FFmpeg không tạo ra file base.");

                var outFinal = Path.Combine(workDir, "output.mp4");
                var overlayDir = Path.Combine(workDir, "overlay");
                Directory.CreateDirectory(overlayDir);

                string overlayArgs;
                if (hasOverlayWebm)
                {
                    var overlayFile = Path.Combine(overlayDir, "overlay.webm");
                    await using (var s = overlayWebm!.OpenReadStream())
                    await using (var fs = System.IO.File.Create(overlayFile))
                    {
                        await s.CopyToAsync(fs);
                    }
                    overlayArgs = BuildOverlayWebmAlphaLoopArgs(basePath, overlayFile, outFinal, width, height, fps);
                }
                else if (hasSegments)
                {
                    List<OverlaySegment> segments;
                    try
                    {
                        segments = JsonSerializer.Deserialize<List<OverlaySegment>>(overlaySegments!, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<OverlaySegment>();
                    }
                    catch
                    {
                        return BadRequest("Invalid overlaySegments JSON");
                    }

                    if (segments.Count == 0) return BadRequest("overlaySegments is empty");
                    if (segments.Count != overlayPngs.Count) return BadRequest("overlaySegments count must match overlayPngs count");

                    var saved = new List<(string File, double From, double To)>();
                    for (var i = 0; i < overlayPngs.Count; i++)
                    {
                        var seg = segments[i];
                        var from = Math.Max(0, seg.FromSec);
                        var to = Math.Max(from, seg.ToSec);
                        var file = Path.Combine(overlayDir, $"overlay_{i + 1:000}.png");
                        await using (var s = overlayPngs[i].OpenReadStream())
                        await using (var fs = System.IO.File.Create(file))
                        {
                            await s.CopyToAsync(fs);
                        }
                        saved.Add((file, from, to));
                    }

                    overlayArgs = BuildOverlayTimedPngArgs(basePath, saved, outFinal, fps);
                }
                else
                {
                    if (!hasSingleOverlay) return BadRequest("Missing form file: overlayPng");
                    var overlayFile = Path.Combine(overlayDir, "overlay.png");
                    await using (var s = overlayPng!.OpenReadStream())
                    await using (var fs = System.IO.File.Create(overlayFile))
                    {
                        await s.CopyToAsync(fs);
                    }
                    overlayArgs = BuildOverlaySinglePngArgs(basePath, overlayFile, outFinal, fps);
                }

                var psi2 = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = overlayArgs,
                    WorkingDirectory = workDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using (var p2 = Process.Start(psi2))
                {
                    if (p2 == null) return StatusCode(500, "Không khởi chạy được ffmpeg overlay");
                    var stderrTask2 = p2.StandardError.ReadToEndAsync();
                    await p2.WaitForExitAsync();
                    var stderr2 = await stderrTask2;
                    if (p2.ExitCode != 0)
                    {
                        return StatusCode(500, string.IsNullOrWhiteSpace(stderr2) ? "FFmpeg overlay failed" : stderr2);
                    }
                }

                if (!System.IO.File.Exists(outFinal)) return StatusCode(500, "FFmpeg không tạo ra file output.");

                var bytes = await System.IO.File.ReadAllBytesAsync(outFinal);
                var safeProductName = MakeSafeFileName(payload.ProductName ?? "render-reels");
                var fileName = string.IsNullOrWhiteSpace(safeProductName) ? "render-reels.mp4" : $"{safeProductName}.mp4";
                return File(bytes, "video/mp4", fileName);
            }
            finally
            {
                try { if (Directory.Exists(workDir)) Directory.Delete(workDir, recursive: true); } catch { }
            }
        }

        private sealed class RenderReelsReupVideoRequest
        {
            public string? ProductName { get; set; }
            public int Width { get; set; } = 1080;
            public int Height { get; set; } = 1920;
            public int Fps { get; set; } = 30;
        }

        [HttpPost("render-reels-reupvideo")]
        [RequestSizeLimit(500_000_000)]
        public async Task<IActionResult> RenderReelsReupVideo([FromForm] string req, [FromForm] IFormFile? mainVideo, [FromForm] IFormFile? overlayWebm, [FromForm] string? overlaySegments)
        {
            if (string.IsNullOrWhiteSpace(req)) return BadRequest("Missing form field: req");
            if (mainVideo == null || mainVideo.Length <= 0) return BadRequest("Missing form file: mainVideo");
            var overlayPngs = Request?.Form?.Files?.Where(x => x != null && x.Length > 0 && (x.Name ?? string.Empty).Equals("overlayPngs", StringComparison.OrdinalIgnoreCase)).ToList()
                            ?? new List<IFormFile>();
            var hasOverlayWebm = overlayWebm != null && overlayWebm.Length > 0;
            var hasOverlaySegments = overlayPngs.Count > 0 && !string.IsNullOrWhiteSpace(overlaySegments);
            if (!hasOverlayWebm && !hasOverlaySegments) return BadRequest("Missing overlay input");

            RenderReelsReupVideoRequest? payload;
            try
            {
                payload = JsonSerializer.Deserialize<RenderReelsReupVideoRequest>(req, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                return BadRequest("Invalid req JSON");
            }
            if (payload == null) return BadRequest("Invalid request");

            var width = payload.Width <= 0 ? 1080 : payload.Width;
            var height = payload.Height <= 0 ? 1920 : payload.Height;
            var fps = payload.Fps <= 0 ? 30 : Math.Min(60, payload.Fps);

            var ffmpegPath = (_config["Ffmpeg:Path"] ?? _config["TelegramVideoDemo:FfmpegPath"] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(ffmpegPath)) return StatusCode(500, "Missing config: Ffmpeg:Path");
            if (!Path.IsPathRooted(ffmpegPath))
            {
                ffmpegPath = Path.Combine(AppContext.BaseDirectory, ffmpegPath.Replace('/', Path.DirectorySeparatorChar));
            }
            if (!System.IO.File.Exists(ffmpegPath)) return StatusCode(500, $"FFmpeg not found: {ffmpegPath}");

            var workDir = Path.Combine(Path.GetTempPath(), "affiliate-platform", "render-reels-reupvideo", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workDir);

            try
            {
                var mainPath = Path.Combine(workDir, "main.mp4");
                await using (var s = mainVideo.OpenReadStream())
                await using (var fs = System.IO.File.Create(mainPath))
                {
                    await s.CopyToAsync(fs);
                }

                var outFinal = Path.Combine(workDir, "output.mp4");

                async Task<(int ExitCode, string Stderr)> RunFfmpeg(string args)
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = args,
                        WorkingDirectory = workDir,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    };

                    using var p = Process.Start(psi);
                    if (p == null) return (-1, "Không khởi chạy được ffmpeg");
                    var stderrTask = p.StandardError.ReadToEndAsync();
                    await p.WaitForExitAsync();
                    var stderr = await stderrTask;
                    return (p.ExitCode, stderr);
                }

                string args;
                if (hasOverlaySegments)
                {
                    List<OverlaySegment> segments;
                    try
                    {
                        segments = JsonSerializer.Deserialize<List<OverlaySegment>>(overlaySegments!, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<OverlaySegment>();
                    }
                    catch
                    {
                        return BadRequest("Invalid overlaySegments JSON");
                    }

                    if (segments.Count == 0) return BadRequest("overlaySegments is empty");
                    if (segments.Count != overlayPngs.Count) return BadRequest("overlaySegments count must match overlayPngs count");

                    var overlayDir = Path.Combine(workDir, "overlay");
                    Directory.CreateDirectory(overlayDir);

                    var saved = new List<(string File, double From, double To)>();
                    for (var i = 0; i < overlayPngs.Count; i++)
                    {
                        var seg = segments[i];
                        var from = Math.Max(0, seg.FromSec);
                        var to = Math.Max(from, seg.ToSec);
                        var originalExt = Path.GetExtension(overlayPngs[i].FileName ?? string.Empty);
                        var ext = string.IsNullOrWhiteSpace(originalExt) ? ".png" : originalExt.Trim().ToLowerInvariant();
                        if (ext != ".png" && ext != ".gif") ext = ".png";
                        var file = Path.Combine(overlayDir, $"overlay_{i + 1:0000}{ext}");
                        await using (var s = overlayPngs[i].OpenReadStream())
                        await using (var fs = System.IO.File.Create(file))
                        {
                            await s.CopyToAsync(fs);
                        }
                        saved.Add((file, from, to));
                    }

                    // Build overlay.mov from segment clips to support animated GIF overlay files.
                    var overlayMov = Path.Combine(workDir, "overlay.mov");
                    var segClips = new List<(string File, double From, double To)>(saved.Count);
                    for (var i = 0; i < saved.Count; i++)
                    {
                        var src = saved[i].File;
                        var dur = Math.Max(0.0001, saved[i].To - saved[i].From);
                        var durText = dur.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        var clipPath = Path.Combine(overlayDir, $"seg_{i + 1:0000}.mov");

                        var srcExt = Path.GetExtension(src).ToLowerInvariant();
                        string clipArgs;
                        if (srcExt == ".gif")
                        {
                            // Keep GIF animation, loop if needed to cover the segment duration
                            clipArgs = $"-y -hide_banner -stream_loop -1 -t {durText} -i \"{src}\" -vf fps={fps},format=argb -fps_mode cfr -r {fps} -c:v qtrle -pix_fmt argb \"{clipPath}\"";
                        }
                        else
                        {
                            // Treat as a still image overlay (PNG) and loop it for the segment duration
                            clipArgs = $"-y -hide_banner -loop 1 -t {durText} -i \"{src}\" -vf fps={fps},format=argb -fps_mode cfr -r {fps} -c:v qtrle -pix_fmt argb \"{clipPath}\"";
                        }

                        var (codeClip, errClip) = await RunFfmpeg(clipArgs);
                        if (codeClip != 0) return StatusCode(500, string.IsNullOrWhiteSpace(errClip) ? "FFmpeg overlay clip render failed" : errClip);
                        segClips.Add((clipPath, saved[i].From, saved[i].To));
                    }

                    var concatFile = Path.Combine(overlayDir, "overlay.ffconcat");
                    WriteFfconcat(concatFile, segClips);
                    var makeMovArgs = BuildConcatToMovArgs(concatFile, overlayMov, fps);
                    var (code1, err1) = await RunFfmpeg(makeMovArgs);
                    if (code1 != 0) return StatusCode(500, string.IsNullOrWhiteSpace(err1) ? "FFmpeg overlay.mov render failed" : err1);

                    args = BuildOverlayMovAlphaArgs(mainPath, overlayMov, outFinal, width, height, fps);
                }
                else
                {
                    var overlayPath = Path.Combine(workDir, "overlay.webm");
                    await using (var s = overlayWebm!.OpenReadStream())
                    await using (var fs = System.IO.File.Create(overlayPath))
                    {
                        await s.CopyToAsync(fs);
                    }

                    args = BuildOverlayWebmAlphaArgs(mainPath, overlayPath, outFinal, width, height, fps);
                }

                var (code, err) = await RunFfmpeg(args);
                if (code != 0) return StatusCode(500, string.IsNullOrWhiteSpace(err) ? "FFmpeg render failed" : err);

                if (!System.IO.File.Exists(outFinal)) return StatusCode(500, "FFmpeg không tạo ra file output.");

                var bytes = await System.IO.File.ReadAllBytesAsync(outFinal);
                var safeProductName = MakeSafeFileName(payload.ProductName ?? "render-reels");
                var fileName = string.IsNullOrWhiteSpace(safeProductName) ? "render-reels.mp4" : $"{safeProductName}.mp4";
                return File(bytes, "video/mp4", fileName);
            }
            finally
            {
                //try { if (Directory.Exists(workDir)) Directory.Delete(workDir, recursive: true); } catch { }
            }
        }

        private static string BuildOverlayWebmAlphaArgs(string baseVideo, string overlayWebm, string outputPath, int width, int height, int fps)
        {
            var sb = new StringBuilder();
            sb.Append("-y -hide_banner ");
            sb.Append($"-i \"{baseVideo}\" ");
            sb.Append($"-i \"{overlayWebm}\" ");

            // Overlay WebM is expected to be full-frame (same WxH) with alpha and the PNGs already positioned on the right.
            // We scale/pad main video to requested WxH to ensure consistent output.
            sb.Append("-filter_complex \"");
            sb.Append($"[0:v]scale={width}:{height}:force_original_aspect_ratio=decrease,pad={width}:{height}:(ow-iw)/2:(oh-ih)/2,setsar=1[base];");
            sb.Append("[1:v]setsar=1[ov];");
            sb.Append("[base][ov]overlay=0:0:format=auto:shortest=1[v]\" ");

            sb.Append("-map \"[v]\" -map 0:a? -c:a aac -b:a 192k ");
            sb.Append($"-r {fps} -c:v libx264 -preset veryfast -pix_fmt yuv420p -movflags +faststart \"{outputPath}\"");
            return sb.ToString();
        }

        private static string BuildOverlayWebmAlphaLoopArgs(string baseVideo, string overlayWebm, string outputPath, int width, int height, int fps)
        {
            var sb = new StringBuilder();
            sb.Append("-y -hide_banner ");
            sb.Append($"-i \"{baseVideo}\" ");
            sb.Append($"-stream_loop -1 -i \"{overlayWebm}\" ");

            sb.Append("-filter_complex \"");
            sb.Append($"[0:v]scale={width}:{height}:force_original_aspect_ratio=decrease,pad={width}:{height}:(ow-iw)/2:(oh-ih)/2,setsar=1[base];");
            sb.Append("[1:v]setsar=1[ov];");
            sb.Append("[base][ov]overlay=0:0:format=auto:shortest=1[v]\" ");

            sb.Append("-map \"[v]\" -map 0:a? -c:a aac -b:a 192k ");
            sb.Append($"-r {fps} -c:v libx264 -preset veryfast -pix_fmt yuv420p -movflags +faststart \"{outputPath}\"");
            return sb.ToString();
        }

        private static Rectangle ComputeTemplate1MainRect(int width, int height, int totalCount, int activeIdx)
        {
            var outerPadding = (int)Math.Round(width * 0.035);
            var thumbGapX = (int)Math.Round(width * 0.018);
            var gapY = 8;

            var visibleCount = Math.Min(5, totalCount);
            var thumbSize = (int)Math.Round((width - outerPadding * 2 - thumbGapX * (visibleCount - 1)) / (double)visibleCount);
            var thumbStripHeight = thumbSize;
            var mainHeight = height - outerPadding * 2 - thumbStripHeight - gapY;
            if (mainHeight < 50) mainHeight = Math.Max(50, height - outerPadding * 2 - thumbSize);

            return new Rectangle(outerPadding, outerPadding, width - outerPadding * 2, mainHeight);
        }

        private static Rectangle ComputeTemplate2MainRect(int width, int height, int totalCount)
        {
            var outerPadding = (int)Math.Round(width * 0.035);
            var thumbGapX = (int)Math.Round(width * 0.018);
            var gapY = 8;

            var visibleCount = Math.Min(5, totalCount);
            var thumbSize = (int)Math.Round((width - outerPadding * 2 - thumbGapX * (visibleCount - 1)) / (double)visibleCount);
            var thumbStripHeight = thumbSize;
            var mainHeight = Math.Max(1, height - outerPadding * 2 - thumbStripHeight - gapY);
            return new Rectangle(outerPadding, outerPadding, width - outerPadding * 2, mainHeight);
        }

        private static string BuildGifOnSlideArgs(string gifFile, string backgroundPng, string outputMp4, Rectangle mainRect, double segmentSeconds, int fps)
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            var tText = Math.Max(0.05, segmentSeconds).ToString(ci);

            var sb = new StringBuilder();
            sb.Append("-y -hide_banner ");
            sb.Append($"-loop 1 -t {tText} -i \"{backgroundPng}\" ");
            sb.Append($"-stream_loop -1 -t {tText} -i \"{gifFile}\" ");
            sb.Append("-filter_complex \"");
            sb.Append($"[1:v]fps={fps},scale={mainRect.Width}:{mainRect.Height}:force_original_aspect_ratio=decrease[gif];");
            sb.Append($"[0:v][gif]overlay=x={mainRect.X}+( {mainRect.Width}-w)/2:y={mainRect.Y}+( {mainRect.Height}-h)/2:shortest=1[v]\" ");
            sb.Append("-map \"[v]\" -an ");
            sb.Append($"-r {fps} -c:v libx264 -preset veryfast -pix_fmt yuv420p -movflags +faststart \"{outputMp4}\"");
            return sb.ToString();
        }

        private static string BuildFfmpegArgsMixed(IReadOnlyList<string> inputFiles, string outputPath, int width, int height, int fps, double secondsPerSlide, double transitionSeconds, string? audioFile, double audioVolume, IReadOnlyList<TextOverlay>? textOverlays)
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            var segmentSeconds = secondsPerSlide + transitionSeconds;
            var totalSeconds = inputFiles.Count * secondsPerSlide + Math.Max(0, inputFiles.Count - 1) * transitionSeconds;

            var sb = new StringBuilder();
            sb.Append("-y -hide_banner ");

            for (var i = 0; i < inputFiles.Count; i++)
            {
                var ext = Path.GetExtension(inputFiles[i]).ToLowerInvariant();
                var isGif = ext == ".gif";
                var segText = segmentSeconds.ToString(ci);

                if (isGif)
                {
                    // Treat GIF as an animated video source.
                    // stream_loop -1 will loop forever; -t limits duration.
                    sb.Append($"-stream_loop -1 -t {segText} -i \"{inputFiles[i]}\" ");
                }
                else if (ext == ".mp4" || ext == ".mov" || ext == ".webm")
                {
                    // Video source already has motion; we will trim in the filter graph.
                    sb.Append($"-i \"{inputFiles[i]}\" ");
                }
                else
                {
                    sb.Append($"-loop 1 -t {segText} -i \"{inputFiles[i]}\" ");
                }
            }

            var hasAudio = !string.IsNullOrWhiteSpace(audioFile);
            if (hasAudio)
            {
                sb.Append($"-stream_loop -1 -i \"{audioFile}\" ");
            }

            var filter = new StringBuilder();
            for (var i = 0; i < inputFiles.Count; i++)
            {
                filter.Append($"[{i}:v]");
                var ext = Path.GetExtension(inputFiles[i]).ToLowerInvariant();
                if (ext == ".mp4" || ext == ".mov" || ext == ".webm")
                {
                    // Ensure each video segment matches expected duration.
                    var segText = segmentSeconds.ToString(ci);
                    filter.Append($"trim=0:{segText},setpts=PTS-STARTPTS,");
                }
                // Normalize all sources (images and GIF) to the same size and pixel format.
                filter.Append($"scale={width}:{height}:force_original_aspect_ratio=increase,crop={width}:{height},setsar=1,fps={fps},format=yuv420p");
                filter.Append($"[v{i}];");
            }

            var last = "v0";
            for (var i = 0; i < inputFiles.Count - 1; i++)
            {
                var off = Math.Max(0, (i + 1) * secondsPerSlide - transitionSeconds);
                var offText = off.ToString(ci);
                var durText = transitionSeconds.ToString(ci);
                var next = $"v{i + 1}";
                var outLabel = $"x{i + 1}";
                filter.Append($"[{last}][{next}]xfade=transition=fade:duration={durText}:offset={offText}[{outLabel}];");
                last = outLabel;
            }

            if (textOverlays != null && textOverlays.Count > 0)
            {
                var draw = new StringBuilder();
                var input = last;
                for (var i = 0; i < textOverlays.Count; i++)
                {
                    var outLabel = $"dt{i}";
                    draw.Append(BuildDrawTextFilter(input, outLabel, textOverlays[i]));
                    input = outLabel;
                }
                filter.Append(draw);
                last = input;
            }

            if (hasAudio)
            {
                var volText = audioVolume.ToString(ci);
                var totalText = totalSeconds.ToString(ci);
                filter.Append($"[{inputFiles.Count}:a]volume={volText},atrim=0:{totalText},asetpts=N/SR/TB[a0];");
            }

            sb.Append($"-filter_complex \"{filter}\" ");
            sb.Append($"-map \"[{last}]\" ");
            if (hasAudio) sb.Append("-map \"[a0]\" -c:a aac -b:a 192k ");
            sb.Append($"-r {fps} -c:v libx264 -preset veryfast -pix_fmt yuv420p -movflags +faststart \"{outputPath}\"");
            return sb.ToString();
        }

        private static string BuildPngSequenceToMovArgs(string inputPattern, string outputMov, int fps)
        {
            var sb = new StringBuilder();
            sb.Append("-y -hide_banner ");
            sb.Append($"-framerate {fps} -i \"{inputPattern}\" ");
            // qtrle supports alpha reliably; argb keeps alpha channel.
            sb.Append($"-r {fps} -c:v qtrle -pix_fmt argb \"{outputMov}\"");
            return sb.ToString();
        }

        private static void WriteFfconcat(string concatPath, IReadOnlyList<(string File, double From, double To)> overlays)
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine("ffconcat version 1.0");

            // Use per-frame durations from (To-From). The last file is repeated without duration as ffconcat requires.
            for (var i = 0; i < overlays.Count; i++)
            {
                var file = overlays[i].File;
                var dur = Math.Max(0.0001, overlays[i].To - overlays[i].From);
                sb.AppendLine($"file '{file.Replace("'", "'\\''")}'");
                sb.AppendLine($"duration {dur.ToString(ci)}");
            }

            // Repeat last file to ensure its duration is honored.
            if (overlays.Count > 0)
            {
                var last = overlays[^1].File;
                sb.AppendLine($"file '{last.Replace("'", "'\\''")}'");
            }

            System.IO.File.WriteAllText(concatPath, sb.ToString());
        }

        private static string BuildConcatToMovArgs(string concatFile, string outputMov, int fps)
        {
            var sb = new StringBuilder();
            sb.Append("-y -hide_banner ");
            sb.Append($"-safe 0 -f concat -i \"{concatFile}\" ");
            // Convert concat timestamps to a CFR stream to avoid timing glitches when later overlaying onto the base video.
            // Keep alpha and encode to MOV with qtrle.
            sb.Append($"-vf fps={fps},format=argb -fps_mode cfr -r {fps} -c:v qtrle -pix_fmt argb \"{outputMov}\"");
            return sb.ToString();
        }

        private static string BuildOverlayMovAlphaArgs(string baseVideo, string overlayMov, string outputPath, int width, int height, int fps)
        {
            var sb = new StringBuilder();
            sb.Append("-y -hide_banner ");
            sb.Append($"-i \"{baseVideo}\" ");
            sb.Append($"-i \"{overlayMov}\" ");

            sb.Append("-filter_complex \"");
            sb.Append($"[0:v]scale={width}:{height}:force_original_aspect_ratio=decrease,pad={width}:{height}:(ow-iw)/2:(oh-ih)/2,setsar=1[base];");
            sb.Append("[1:v]setsar=1[ov];");
            sb.Append("[base][ov]overlay=0:0:format=auto:shortest=1[v]\" ");

            sb.Append("-map \"[v]\" -map 0:a? -c:a aac -b:a 192k ");
            sb.Append($"-r {fps} -c:v libx264 -preset veryfast -pix_fmt yuv420p -movflags +faststart \"{outputPath}\"");
            return sb.ToString();
        }

        private static async Task DownloadToFile(HttpClient client, string url, string localPath)
        {
            using var res = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            res.EnsureSuccessStatusCode();
            await using var s = await res.Content.ReadAsStreamAsync();
            await using var fs = System.IO.File.Create(localPath);
            await s.CopyToAsync(fs);
        }

        private static async Task<(int ExitCode, string Stderr)> RunFfmpeg(string ffmpegPath, string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using var p = Process.Start(psi);
            if (p == null) return (-1, "Không khởi chạy được ffmpeg");

            var stderrTask = p.StandardError.ReadToEndAsync();
            await p.WaitForExitAsync();
            var stderr = await stderrTask;

            return (p.ExitCode, stderr);
        }

        private static async Task<IReadOnlyList<string>> ComposeSlides(IReadOnlyList<string> inputFiles, string workDir, int width, int height, int template)
        {
            // Load all images once; we will clone per usage for resizing/cropping.
            var loaded = new List<Image<Rgba32>>();
            try
            {
                foreach (var file in inputFiles)
                {
                    loaded.Add(await Image.LoadAsync<Rgba32>(file));
                }

                var outFiles = new List<string>(loaded.Count);

                if (template == 2)
                {
                    var t2Height = ComputeTemplate2Height(loaded, width);
                    for (var activeIdx = 0; activeIdx < loaded.Count; activeIdx++)
                    {
                        var slidePath = Path.Combine(workDir, $"slide_{activeIdx + 1:000}.png");
                        using var slide = ComposeSingleSlideTemplate2(loaded, activeIdx, width, t2Height);
                        await slide.SaveAsPngAsync(slidePath);
                        outFiles.Add(slidePath);
                    }
                }
                else
                {
                    for (var activeIdx = 0; activeIdx < loaded.Count; activeIdx++)
                    {
                        var slidePath = Path.Combine(workDir, $"slide_{activeIdx + 1:000}.png");
                        using var slide = ComposeSingleSlide(loaded, activeIdx, width, height);
                        await slide.SaveAsPngAsync(slidePath);
                        outFiles.Add(slidePath);
                    }
                }

                return outFiles;
            }
            finally
            {
                foreach (var img in loaded) img.Dispose();
            }
        }

        private static Image<Rgba32> ComposeSingleSlide(IReadOnlyList<Image<Rgba32>> images, int activeIdx, int width, int height)
        {
            // Colors taken from frontend preview/canvas logic.
            var bg = new Rgba32(0x0f, 0x17, 0x2a);
            var bg2 = new Rgba32(0x11, 0x18, 0x27);
            var borderInactive = new Rgba32(255, 255, 255, 64);
            var borderActive = new Rgba32(0xef, 0x44, 0x44);

            var outerPadding = (int)Math.Round(width * 0.035);
            var thumbGapX = (int)Math.Round(width * 0.018);
            var gapY = 8;

            // Show max 5 thumbnails to match UI preview.
            var visibleCount = Math.Min(5, images.Count);
            var start = 0;
            if (images.Count > visibleCount)
            {
                start = Math.Max(0, Math.Min(activeIdx - 2, images.Count - visibleCount));
            }

            var thumbSize = (int)Math.Round((width - outerPadding * 2 - thumbGapX * (visibleCount - 1)) / (double)visibleCount);
            var thumbStripHeight = thumbSize;
            var mainHeight = height - outerPadding * 2 - thumbStripHeight - gapY;
            if (mainHeight < 50) mainHeight = Math.Max(50, height - outerPadding * 2 - thumbSize);

            var mainRect = new Rectangle(outerPadding, outerPadding, width - outerPadding * 2, mainHeight);
            var overlapPx = 180;
            var stripY = (mainRect.Y + mainRect.Height) - overlapPx;

            var canvas = new Image<Rgba32>(width, height);
            FillVerticalGradient(canvas, bg, bg2);

            // Snapshot background so we can restore it when applying rounded-corner masks.
            using var backgroundSnapshot = canvas.Clone();

            // Main image: contain (no crop) to match UI request: fit width, keep aspect ratio
            using (var mainImg = images[activeIdx].Clone())
            {
                var srcW = mainImg.Width;
                var srcH = mainImg.Height;
                DrawContain(canvas, mainImg, mainRect, cornerRadius: 28);

                // Border should hug the actual image area (exclude contain padding), not the whole mainRect.
                if (srcW > 0 && srcH > 0)
                {
                    var scale = Math.Min(mainRect.Width / (double)srcW, mainRect.Height / (double)srcH);
                    var w = Math.Max(1, (int)Math.Round(srcW * scale));
                    var h = Math.Max(1, (int)Math.Round(srcH * scale));
                    var x0 = (mainRect.Width - w) / 2;
                    var y0 = (mainRect.Height - h) / 2;
                    var imageRect = new Rectangle(mainRect.X + x0, mainRect.Y + y0, w, h);
                    ApplyRoundedCornersRegionToBackground(canvas, backgroundSnapshot, imageRect, radius: 28);
                }
            }

            // MP4 doesn't support alpha; restore background pixels at the corners to make rounding visible.
            ApplyRoundedCornersRegionToBackground(canvas, backgroundSnapshot, mainRect, radius: 28);

            // Thumbnails: cover (crop) for nicer look.
            for (var i = 0; i < visibleCount; i++)
            {
                var idx = start + i;
                var x = outerPadding + i * (thumbSize + thumbGapX);
                var thumbRect = new Rectangle(x, stripY, thumbSize, thumbSize);
                var isActive = idx == activeIdx;

                using (var thumbImg = images[idx].Clone())
                {
                    DrawCover(canvas, thumbImg, thumbRect, cornerRadius: 18);
                }

                var stroke = isActive ? 4 : 1;
                var color = isActive ? borderActive : borderInactive;
                DrawRoundedRectStroke(canvas, thumbRect, stroke, color, radius: 18);

                // Important: border is drawn as a rectangle, which would visually remove rounded corners.
                // Mask the entire thumbnail region (image + border) to keep rounded corners like UI.
                ApplyRoundedCornersRegionToBackground(canvas, backgroundSnapshot, thumbRect, radius: 18);
            }

            return canvas;
        }

        private static void FillVerticalGradient(Image<Rgba32> img, Rgba32 top, Rgba32 bottom)
        {
            img.ProcessPixelRows(accessor =>
            {
                var h = accessor.Height;
                for (var y = 0; y < h; y++)
                {
                    var t = h <= 1 ? 0f : (float)y / (h - 1);
                    var r = (byte)(top.R + (bottom.R - top.R) * t);
                    var g = (byte)(top.G + (bottom.G - top.G) * t);
                    var b = (byte)(top.B + (bottom.B - top.B) * t);
                    var a = (byte)(top.A + (bottom.A - top.A) * t);
                    accessor.GetRowSpan(y).Fill(new Rgba32(r, g, b, a));
                }
            });
        }

        private static void FillSolid(Image<Rgba32> img, Rgba32 color)
        {
            img.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < accessor.Height; y++)
                {
                    accessor.GetRowSpan(y).Fill(color);
                }
            });
        }

        private static void DrawContain(Image<Rgba32> canvas, Image<Rgba32> src, Rectangle dest, int cornerRadius)
        {
            var srcW = src.Width;
            var srcH = src.Height;
            if (srcW <= 0 || srcH <= 0) return;

            var scale = Math.Min(dest.Width / (double)srcW, dest.Height / (double)srcH);
            var w = Math.Max(1, (int)Math.Round(srcW * scale));
            var h = Math.Max(1, (int)Math.Round(srcH * scale));
            src.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(w, h),
                Mode = ResizeMode.Stretch,
                Sampler = KnownResamplers.Lanczos3,
            }));

            using var composed = new Image<Rgba32>(dest.Width, dest.Height, new Rgba32(0, 0, 0, 0));
            var x0 = (dest.Width - w) / 2;
            var y0 = (dest.Height - h) / 2;
            composed.Mutate(ctx => ctx.DrawImage(src, new Point(x0, y0), 1f));
            ApplyRoundedCorners(composed, cornerRadius);

            canvas.Mutate(ctx => ctx.DrawImage(composed, new Point(dest.X, dest.Y), 1f));
        }

        private static void DrawCover(Image<Rgba32> canvas, Image<Rgba32> src, Rectangle dest, int cornerRadius)
        {
            var srcW = src.Width;
            var srcH = src.Height;
            if (srcW <= 0 || srcH <= 0) return;

            var scale = Math.Max(dest.Width / (double)srcW, dest.Height / (double)srcH);
            var w = Math.Max(1, (int)Math.Round(srcW * scale));
            var h = Math.Max(1, (int)Math.Round(srcH * scale));
            src.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(w, h),
                Mode = ResizeMode.Stretch,
                Sampler = KnownResamplers.Lanczos3,
            }));

            var cropX = Math.Max(0, (w - dest.Width) / 2);
            var cropY = Math.Max(0, (h - dest.Height) / 2);
            src.Mutate(x => x.Crop(new Rectangle(cropX, cropY, dest.Width, dest.Height)));

            ApplyRoundedCorners(src, cornerRadius);
            canvas.Mutate(ctx => ctx.DrawImage(src, new Point(dest.X, dest.Y), 1f));
        }

        private static void ApplyRoundedCorners(Image<Rgba32> img, int radius)
        {
            if (radius <= 0) return;

            var r = Math.Min(radius, Math.Min(img.Width, img.Height) / 2);
            if (r <= 0) return;

            // Pixel-based alpha mask for rounded corners (no dependency on ImageSharp.Drawing APIs).
            // Keep pixels inside rounded-rect; set alpha=0 outside.
            img.ProcessPixelRows(accessor =>
            {
                var w = accessor.Width;
                var h = accessor.Height;
                var rr = r * r;
                var feather = Math.Max(1, r / 8); // soft edge thickness

                for (var y = 0; y < h; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (var x = 0; x < w; x++)
                    {
                        var transparent = false;

                        var left = x < r;
                        var right = x >= w - r;
                        var top = y < r;
                        var bottom = y >= h - r;

                        if ((left && top) || (right && top) || (left && bottom) || (right && bottom))
                        {
                            int cx = left ? r : (w - r - 1);
                            int cy = top ? r : (h - r - 1);
                            var dx = x - cx;
                            var dy = y - cy;
                            var dist = dx * dx + dy * dy;
                            if (dist > rr)
                            {
                                transparent = true;
                            }
                            else
                            {
                                // Simple anti-alias: soften alpha close to the edge of the circle.
                                var edge = rr - dist;
                                if (edge >= 0 && edge < feather * feather)
                                {
                                    // edge==0 => alpha 0, edge==feather^2 => alpha 255
                                    var alpha = (byte)Math.Clamp((int)(255.0 * edge / (feather * feather)), 0, 255);
                                    var p = row[x];
                                    p.A = (byte)Math.Min(p.A, alpha);
                                    row[x] = p;
                                }
                            }
                        }

                        if (transparent)
                        {
                            var p = row[x];
                            p.A = 0;
                            row[x] = p;
                        }
                    }
                }
            });
        }

        private static void DrawRectStroke(Image<Rgba32> canvas, Rectangle rect, int strokeWidth, Rgba32 color)
        {
            if (strokeWidth <= 0) return;
            var x0 = Math.Max(0, rect.X);
            var y0 = Math.Max(0, rect.Y);
            var x1 = Math.Min(canvas.Width, rect.X + rect.Width);
            var y1 = Math.Min(canvas.Height, rect.Y + rect.Height);
            if (x1 <= x0 || y1 <= y0) return;

            var s = strokeWidth;
            canvas.ProcessPixelRows(accessor =>
            {
                for (var y = y0; y < y1; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (var x = x0; x < x1; x++)
                    {
                        var isTop = y < y0 + s;
                        var isBottom = y >= y1 - s;
                        var isLeft = x < x0 + s;
                        var isRight = x >= x1 - s;
                        if (isTop || isBottom || isLeft || isRight)
                        {
                            row[x] = color;
                        }
                    }
                }
            });
        }

        private static string BuildFfmpegArgs(IReadOnlyList<string> inputFiles, string outputPath, int fps, double secondsPerSlide, double transitionSeconds, string? audioFile, double audioVolume, IReadOnlyList<TextOverlay>? textOverlays)
        {
            var segmentSeconds = secondsPerSlide + transitionSeconds;
            var totalSeconds = inputFiles.Count * secondsPerSlide + Math.Max(0, inputFiles.Count - 1) * transitionSeconds;

            var sb = new StringBuilder();
            sb.Append("-y -hide_banner ");

            for (var i = 0; i < inputFiles.Count; i++)
            {
                sb.Append($"-loop 1 -t {segmentSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)} -i \"{inputFiles[i]}\" ");
            }

            var hasAudio = !string.IsNullOrWhiteSpace(audioFile);
            if (hasAudio)
            {
                // Loop audio to cover the whole video; later we trim to exact duration.
                sb.Append($"-stream_loop -1 -i \"{audioFile}\" ");
            }

            var filter = new StringBuilder();
            for (var i = 0; i < inputFiles.Count; i++)
            {
                filter.Append($"[{i}:v]");
                filter.Append("setsar=1,format=yuv420p");
                filter.Append($"[v{i}];");
            }

            var last = "v0";
            for (var i = 0; i < inputFiles.Count - 1; i++)
            {
                // xfade offset is in timeline seconds. If each segment is (slide + transition),
                // the transition from clip i->i+1 should start near the end of slide i.
                var off = Math.Max(0, (i + 1) * secondsPerSlide - transitionSeconds);
                var offText = off.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var durText = transitionSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var next = $"v{i + 1}";
                var outLabel = $"x{i + 1}";
                filter.Append($"[{last}][{next}]xfade=transition=fade:duration={durText}:offset={offText}[{outLabel}];");
                last = outLabel;
            }

            if (hasAudio)
            {
                var volText = audioVolume.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var totalText = totalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
                filter.Append($"[{inputFiles.Count}:a]volume={volText},atrim=0:{totalText},asetpts=N/SR/TB[a0];");
            }

            sb.Append($"-filter_complex \"{filter}\" ");
            sb.Append($"-map \"[{last}]\" ");
            if (hasAudio) sb.Append("-map \"[a0]\" -c:a aac -b:a 192k ");
            sb.Append($"-r {fps} -c:v libx264 -preset veryfast -pix_fmt yuv420p -movflags +faststart \"{outputPath}\"");
            return sb.ToString();
        }

        private static string BuildOverlaySinglePngArgs(string baseVideo, string overlayPngFile, string outputPath, int fps)
        {
            var sb = new StringBuilder();
            sb.Append("-y -hide_banner ");
            sb.Append($"-i \"{baseVideo}\" ");
            sb.Append($"-i \"{overlayPngFile}\" ");
            sb.Append("-filter_complex \"[1:v]format=rgba[ov];[0:v][ov]overlay=0:0:format=auto[v]\" ");
            sb.Append("-map \"[v]\" -map 0:a? -c:a copy ");
            sb.Append($"-r {fps} -c:v libx264 -preset veryfast -pix_fmt yuv420p -movflags +faststart \"{outputPath}\"");
            return sb.ToString();
        }

        private sealed class OverlaySegment
        {
            public double FromSec { get; set; }
            public double ToSec { get; set; }
        }

        private static string BuildOverlayTimedPngArgs(string baseVideo, IReadOnlyList<(string File, double From, double To)> overlays, string outputPath, int fps)
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;

            var sb = new StringBuilder();
            sb.Append("-y -hide_banner ");
            sb.Append($"-i \"{baseVideo}\" ");
            for (var i = 0; i < overlays.Count; i++)
            {
                sb.Append($"-i \"{overlays[i].File}\" ");
            }

            var f = new StringBuilder();
            var last = "[0:v]";
            for (var i = 0; i < overlays.Count; i++)
            {
                var inputIdx = i + 1;
                var from = Math.Max(0, overlays[i].From);
                var to = Math.Max(from, overlays[i].To);
                var fromText = from.ToString(ci);
                var toText = to.ToString(ci);
                var ovLabel = $"ov{i}";
                var outLabel = $"v{i}";
                f.Append($"[{inputIdx}:v]format=rgba[{ovLabel}];");
                f.Append($"{last}[{ovLabel}]overlay=0:0:format=auto:enable='between(t,{fromText},{toText})'[{outLabel}];");
                last = $"[{outLabel}]";
            }

            sb.Append($"-filter_complex \"{f}\" ");
            sb.Append($"-map \"{last}\" -map 0:a? -c:a copy ");
            sb.Append($"-r {fps} -c:v libx264 -preset veryfast -pix_fmt yuv420p -movflags +faststart \"{outputPath}\"");
            return sb.ToString();
        }

        private static string BuildDrawTextFilter(string inputLabel, string outputLabel, TextOverlay ov)
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;

            var text = EscapeDrawtextText((ov.Text ?? string.Empty).Trim());
            var start = Math.Max(0, ov.Start);
            var end = Math.Max(start, ov.End);
            var startText = start.ToString(ci);
            var endText = end.ToString(ci);

            var anim = (ov.Animation ?? string.Empty).Trim().ToLowerInvariant();
            var enable = $"between(t,{startText},{endText})";

            var x = string.IsNullOrWhiteSpace(ov.X) ? "(w-text_w)/2" : ov.X.Trim();
            var y = string.IsNullOrWhiteSpace(ov.Y) ? "h*0.15" : ov.Y.Trim();

            var fontSize = Math.Max(8, ov.FontSize);
            var fontColor = string.IsNullOrWhiteSpace(ov.FontColor) ? "white" : ov.FontColor.Trim();
            var fontFile = ResolveFontFile(ov.Font);

            var sb = new StringBuilder();
            sb.Append($"[{inputLabel}]drawtext=");
            sb.Append($"text='{text}':");
            sb.Append($"x={x}:");
            sb.Append($"y={y}:");
            sb.Append($"fontsize={fontSize}:");
            sb.Append($"fontcolor={fontColor}:");

            if (!string.IsNullOrWhiteSpace(fontFile))
            {
                sb.Append($"fontfile='{EscapeDrawtextValue(fontFile)}':");
            }

            if (ov.Box)
            {
                var boxColor = string.IsNullOrWhiteSpace(ov.BoxColor) ? "black@0.5" : ov.BoxColor.Trim();
                var boxBorderW = Math.Max(0, ov.BoxBorderW);
                sb.Append("box=1:");
                sb.Append($"boxcolor={boxColor}:");
                sb.Append($"boxborderw={boxBorderW}:");
            }

            if (anim == "fade")
            {
                var fd = 0.5;
                var fdText = fd.ToString(ci);
                var endMinusFdText = Math.Max(start, end - fd).ToString(ci);
                sb.Append($"alpha='if(lt(t,{startText}),0,if(lt(t,{startText}+{fdText}),(t-{startText})/{fdText},if(lt(t,{endMinusFdText}),1,if(lt(t,{endText}),({endText}-t)/{fdText},0))))':");
            }
            else if (anim == "slideup" || anim == "slide-up")
            {
                var d = 0.6;
                var dText = d.ToString(ci);
                sb.Append($"y='if(lt(t,{startText}),h,if(lt(t,{startText}+{dText}),h-(t-{startText})/{dText}*(h-({y})),({y})))':");
            }
            else if (anim == "pop")
            {
                var d = 0.35;
                var dText = d.ToString(ci);
                var extra = Math.Max(10, (int)Math.Round(fontSize * 0.8));
                sb.Append($"fontsize='if(lt(t,{startText}),{fontSize},if(lt(t,{startText}+{dText}),{fontSize}+{extra}*(t-{startText})/{dText},{fontSize}))':");
            }
            else if (anim == "float" || anim == "bob" || anim == "updown" || anim == "up-down")
            {
                var period = Math.Max(0.1, ov.AnimPeriodSec);
                var amp = Math.Max(0, ov.AnimAmplitudePx);
                var periodText = period.ToString(ci);
                var ampText = amp.ToString(ci);
                // y(t) = baseY + amp * sin(2*pi*(t-start)/period)
                // Use PI constant supported by FFmpeg expression evaluator.
                sb.Append($"y='({y})+({ampText})*sin(2*PI*(t-{startText})/{periodText})':");
            }

            sb.Append($"enable='{enable}'[{outputLabel}];");
            return sb.ToString();
        }

        private static string? ResolveFontFile(string? fontName)
        {
            var name = (fontName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Equals("default", StringComparison.OrdinalIgnoreCase)) return null;

            // Windows common fonts. We try multiple filenames to avoid breaking render.
            var winFonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            if (string.IsNullOrWhiteSpace(winFonts) || !Directory.Exists(winFonts))
            {
                winFonts = @"C:\\Windows\\Fonts";
            }

            static string Combine(string dir, string file) => Path.Combine(dir, file);

            var candidates = new List<string>();
            var n = name.ToLowerInvariant();

            if (n is "arial")
            {
                candidates.Add(Combine(winFonts, "arial.ttf"));
            }
            else if (n is "calibri")
            {
                candidates.Add(Combine(winFonts, "calibri.ttf"));
            }
            else if (n is "tahoma")
            {
                candidates.Add(Combine(winFonts, "tahoma.ttf"));
            }
            else if (n is "times" or "timesnewroman" or "times new roman")
            {
                candidates.Add(Combine(winFonts, "times.ttf"));
                candidates.Add(Combine(winFonts, "timesnewroman.ttf"));
            }
            else if (n is "segoeui" or "segoe ui")
            {
                candidates.Add(Combine(winFonts, "segoeui.ttf"));
            }
            else if (n is "svn")
            {
                // SVN-Headliner No. 45 font in project Fonts folder
                var projectFontDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fonts");
                candidates.Add(Combine(projectFontDir, "SVN-Headliner No 45.ttf"));
                candidates.Add(Combine(projectFontDir, "SVN-Headliner No.45.ttf"));
                candidates.Add(Combine(projectFontDir, "SVN-Headliner No 45.otf"));
                candidates.Add(Combine(projectFontDir, "SVN-Headliner No.45.otf"));
            }
            else
            {
                // Allow direct filename (e.g. "arial.ttf")
                candidates.Add(Combine(winFonts, name));
                candidates.Add(Combine(winFonts, name + ".ttf"));
            }

            foreach (var p in candidates)
            {
                try
                {
                    if (System.IO.File.Exists(p)) return p;
                }
                catch { }
            }

            return null;
        }

        private static string EscapeDrawtextValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            // drawtext uses ':' as separator. Use forward slashes and escape ':' for Windows drive.
            var s = value.Replace('\\', '/');
            s = s.Replace(":", "\\:");
            s = s.Replace("'", "\\'");
            return s;
        }

        private static string EscapeDrawtextText(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text
                .Replace("\\", "\\\\")
                .Replace("'", "\\'")
                .Replace(":", "\\:")
                .Replace("\r", string.Empty)
                .Replace("\n", "\\n");
        }

        public class UpdateProductRequest
        {
            public string? Name { get; set; }
            public string? Description { get; set; }
        }

        private static string BuildAffiliatePrompt(string productName)
        {
            var n = string.IsNullOrWhiteSpace(productName) ? "[Tên sản phẩm]" : productName.Trim();
            return
$"Bạn là một chuyên gia viết kịch bản audio review bán hàng cho TikTok/Reel.\n\n" +
$"Hãy viết cho tôi một kịch bản audio dài 25–30 giây để review sản phẩm: {n}\n" +
$"Mục tiêu: ...\n" +
$"Yêu cầu:\n\n" +
$"1. 3 giây đầu phải có HOOK mạnh khiến người nghe dừng lại.\n" +
$"2. Có cấu trúc:\n" +
$"   - Hook\n" +
$"   - Nỗi đau / vấn đề\n" +
$"   - Giải pháp + lợi ích nổi bật\n" +
$"   - Cảm nhận cá nhân chân thật\n" +
$"   - CTA mềm (không lộ quảng cáo)\n" +
$"3. Viết theo phong cách nói tự nhiên, giống người thật chia sẻ.\n" +
$"4. Câu ngắn, dễ đọc bằng AI Text-to-Speech.\n" +
$"5. Chèn ký hiệu nhấn nhá như:\n" +
$"   - Dấu \"...\" để tạo khoảng dừng\n" +
$"   - Xuống dòng để chia nhịp\n" +
$"   - In hoa từ cần nhấn mạnh\n" +
$"6. Tổng độ dài khoảng 75–100 từ.\n" +
$"7. Không được viết quá lố hoặc giống quảng cáo truyền thống.\n\n" +
$"Phong cách giọng: [Nữ ngọt]\n\n" +
$"Viết ngay nội dung hoàn chỉnh.";
        }

        private static string BuildTitlePrompt(string productName, string? affiliateLink)
        {
            var n = string.IsNullOrWhiteSpace(productName) ? "[Tên sản phẩm]" : productName.Trim();
            var link = string.IsNullOrWhiteSpace(affiliateLink) ? "[Link Affiliate]" : affiliateLink.Trim();

            return
$"\n\nprompt cho tiêu đề:\n" +
$"Tiêu đề thu hút, \"Giật tít\" (Cho TikTok Shop/Facebook)\n" +
$" cho {n} kèm hashtag\n\n" +
$"Ở đây nè:  {link}\n";
        }

        private static string MakeSafeFileName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;

            var s = name.Trim();
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                s = s.Replace(c, '-');
            }

            s = s.Trim().Trim('.');
            if (s.Length > 120) s = s.Substring(0, 120).Trim();
            return s;
        }

        private static string[] ParseStringArrayJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) return Array.Empty<string>();
                var list = new List<string>();
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    if (el.ValueKind == JsonValueKind.String)
                    {
                        var s = el.GetString();
                        if (!string.IsNullOrWhiteSpace(s)) list.Add(s);
                    }
                    else if (el.ValueKind == JsonValueKind.Number)
                    {
                        if (el.TryGetInt64(out var n)) list.Add(n.ToString());
                    }
                }
                return list.ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private static string[] ExtractVideoUrls(string? videoInfoListJson, string videoPrefix)
        {
            if (string.IsNullOrWhiteSpace(videoInfoListJson)) return Array.Empty<string>();
            try
            {
                using var doc = JsonDocument.Parse(videoInfoListJson);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) return Array.Empty<string>();

                var urls = new List<string>();
                foreach (var v in doc.RootElement.EnumerateArray())
                {
                    if (v.ValueKind != JsonValueKind.Object) continue;
                    if (!v.TryGetProperty("default_format", out var defaultFormat) || defaultFormat.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    if (defaultFormat.TryGetProperty("url", out var urlEl) && urlEl.ValueKind == JsonValueKind.String)
                    {
                        var u = urlEl.GetString();
                        if (!string.IsNullOrWhiteSpace(u)) urls.Add(u);
                        continue;
                    }

                    if (defaultFormat.TryGetProperty("path", out var pathEl) && pathEl.ValueKind == JsonValueKind.String)
                    {
                        var p = pathEl.GetString();
                        if (!string.IsNullOrWhiteSpace(p))
                        {
                            urls.Add(BuildCdnUrl(videoPrefix, p) ?? string.Empty);
                        }
                    }
                }

                return urls.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private static async Task AddRemoteFiles(ZipArchive zip, HttpClient client, IReadOnlyList<string> urls, string folder)
        {
            for (var i = 0; i < urls.Count; i++)
            {
                var url = urls[i];
                if (string.IsNullOrWhiteSpace(url)) continue;

                try
                {
                    using var res = await client.GetAsync(url);
                    if (!res.IsSuccessStatusCode) continue;

                    var contentType = res.Content.Headers.ContentType?.MediaType;
                    var ext = GuessFileExtension(url, contentType);
                    var name = $"{folder}/{i + 1:000}{ext}";

                    var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
                    await using var entryStream = entry.Open();
                    await using var remoteStream = await res.Content.ReadAsStreamAsync();
                    await remoteStream.CopyToAsync(entryStream);
                }
                catch
                {
                    // ignore
                }
            }
        }

        private static string GuessFileExtension(string url, string? contentType)
        {
            try
            {
                var uri = new Uri(url);
                var ext = Path.GetExtension(uri.AbsolutePath);
                if (!string.IsNullOrWhiteSpace(ext) && ext.Length <= 6) return ext;
            }
            catch
            {
                // ignore
            }

            if (!string.IsNullOrWhiteSpace(contentType))
            {
                if (contentType.Contains("jpeg", StringComparison.OrdinalIgnoreCase)) return ".jpg";
                if (contentType.Contains("png", StringComparison.OrdinalIgnoreCase)) return ".png";
                if (contentType.Contains("webp", StringComparison.OrdinalIgnoreCase)) return ".webp";
                if (contentType.Contains("mp4", StringComparison.OrdinalIgnoreCase)) return ".mp4";
            }

            return ".bin";
        }

        [HttpPut("{id:long}")]
        public async Task<IActionResult> Update([FromRoute] long id, [FromBody] UpdateProductRequest req)
        {
            var p = await _products.GetByIdAsync(id);
            if (p == null) return NotFound();

            if (req.Name != null)
            {
                var n = req.Name.Trim();
                if (string.IsNullOrWhiteSpace(n)) return BadRequest("Name is required");
                p.Name = n;
            }

            if (req.Description != null)
            {
                var d = req.Description.Trim();
                p.Description = string.IsNullOrWhiteSpace(d) ? null : d;
            }

            p.UpdatedAt = DateTime.UtcNow;

            // Persist
            var updated = await _products.UpdateAsync(p);

            return Ok(new
            {
                id = updated.Id,
                name = updated.Name,
                description = updated.Description,
                updatedAt = updated.UpdatedAt,
            });
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete([FromRoute] long id)
        {
            var ok = await _products.DeleteAsync(id);
            if (!ok) return NotFound();
            return NoContent();
        }
    }
}
