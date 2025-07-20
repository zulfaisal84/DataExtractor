using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DocumentExtractor.Core.Models;

namespace DocumentExtractor.Desktop.Services;

/// <summary>
/// Document Similarity Service - Advanced algorithms for comparing document structures
/// This is the intelligence behind our local pattern matching that enables 95% token-free processing
/// Uses multiple similarity metrics to find the best matching patterns
/// </summary>
public class DocumentSimilarityService
{
    private readonly ILogger<DocumentSimilarityService>? _logger;
    private readonly Dictionary<string, DocumentFingerprint> _fingerprintCache;

    public DocumentSimilarityService(ILogger<DocumentSimilarityService>? logger = null)
    {
        _logger = logger;
        _fingerprintCache = new Dictionary<string, DocumentFingerprint>();
        
        _logger?.LogInformation("DocumentSimilarityService initialized");
    }

    /// <summary>
    /// Calculate comprehensive document fingerprint for similarity matching
    /// This creates a unique signature that helps identify similar document types
    /// </summary>
    public DocumentFingerprint CalculateDocumentFingerprint(string documentText, string documentPath = "")
    {
        var cacheKey = !string.IsNullOrEmpty(documentPath) ? documentPath : documentText.GetHashCode().ToString();
        
        if (_fingerprintCache.TryGetValue(cacheKey, out var cachedFingerprint))
        {
            _logger?.LogDebug("Using cached fingerprint for {DocumentPath}", documentPath);
            return cachedFingerprint;
        }

        var fingerprint = new DocumentFingerprint
        {
            DocumentPath = documentPath,
            // Basic structural metrics
            StructuralMetrics = CalculateStructuralMetrics(documentText),
            
            // Pattern-based metrics  
            PatternMetrics = CalculatePatternMetrics(documentText),
            
            // Content-based metrics
            ContentMetrics = CalculateContentMetrics(documentText),
            
            // Layout metrics
            LayoutMetrics = CalculateLayoutMetrics(documentText),
            
            // Keyword fingerprint
            KeywordFingerprint = ExtractKeywordFingerprint(documentText),
            
            // Hash signatures for fast lookup
            HashSignatures = GenerateHashSignatures(documentText),
            
            CreatedAt = DateTime.UtcNow
        };

        _fingerprintCache[cacheKey] = fingerprint;
        _logger?.LogInformation("Generated fingerprint for document with {KeywordCount} keywords and {PatternCount} patterns", 
            fingerprint.KeywordFingerprint.Count, fingerprint.PatternMetrics.TotalPatterns);
        
        return fingerprint;
    }

    /// <summary>
    /// Calculate similarity score between two documents (0.0 to 1.0)
    /// Uses weighted combination of multiple similarity metrics
    /// </summary>
    public SimilarityResult CalculateSimilarity(DocumentFingerprint doc1, DocumentFingerprint doc2)
    {
        var result = new SimilarityResult();

        try
        {
            // 1. Structural similarity (25% weight)
            result.StructuralSimilarity = CalculateStructuralSimilarity(doc1.StructuralMetrics, doc2.StructuralMetrics);
            
            // 2. Pattern similarity (30% weight)
            result.PatternSimilarity = CalculatePatternSimilarity(doc1.PatternMetrics, doc2.PatternMetrics);
            
            // 3. Content similarity (20% weight)
            result.ContentSimilarity = CalculateContentSimilarity(doc1.ContentMetrics, doc2.ContentMetrics);
            
            // 4. Layout similarity (15% weight)
            result.LayoutSimilarity = CalculateLayoutSimilarity(doc1.LayoutMetrics, doc2.LayoutMetrics);
            
            // 5. Keyword similarity (10% weight)
            result.KeywordSimilarity = CalculateKeywordSimilarity(doc1.KeywordFingerprint, doc2.KeywordFingerprint);

            // Calculate weighted overall similarity
            result.OverallSimilarity = 
                (result.StructuralSimilarity * 0.25) +
                (result.PatternSimilarity * 0.30) +
                (result.ContentSimilarity * 0.20) +
                (result.LayoutSimilarity * 0.15) +
                (result.KeywordSimilarity * 0.10);

            // Determine confidence level
            result.ConfidenceLevel = result.OverallSimilarity switch
            {
                >= 0.90 => ConfidenceLevel.VeryHigh,
                >= 0.85 => ConfidenceLevel.High,
                >= 0.70 => ConfidenceLevel.Medium,
                >= 0.50 => ConfidenceLevel.Low,
                _ => ConfidenceLevel.VeryLow
            };

            result.Success = true;
            
            _logger?.LogDebug("Similarity calculated: Overall={Overall:P1}, Structural={Structural:P1}, Pattern={Pattern:P1}", 
                result.OverallSimilarity, result.StructuralSimilarity, result.PatternSimilarity);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error calculating document similarity");
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Find the most similar documents from a collection
    /// Optimized for fast searching through large pattern databases
    /// </summary>
    public async Task<List<SimilarDocumentMatch>> FindSimilarDocumentsAsync(
        DocumentFingerprint targetDocument, 
        IEnumerable<DocumentFingerprint> candidateDocuments,
        double minimumSimilarity = 0.5,
        int maxResults = 10)
    {
        var matches = new List<SimilarDocumentMatch>();

        await Task.Run(() =>
        {
            foreach (var candidate in candidateDocuments)
            {
                var similarity = CalculateSimilarity(targetDocument, candidate);
                
                if (similarity.Success && similarity.OverallSimilarity >= minimumSimilarity)
                {
                    matches.Add(new SimilarDocumentMatch
                    {
                        Document = candidate,
                        SimilarityResult = similarity,
                        MatchReason = GenerateMatchReason(similarity)
                    });
                }
            }
        });

        // Sort by similarity score and return top matches
        var sortedMatches = matches
            .OrderByDescending(m => m.SimilarityResult.OverallSimilarity)
            .Take(maxResults)
            .ToList();

        _logger?.LogInformation("Found {MatchCount} similar documents above {MinSimilarity:P1} threshold", 
            sortedMatches.Count, minimumSimilarity);

        return sortedMatches;
    }

    #region Similarity Calculation Methods

    private double CalculateStructuralSimilarity(StructuralMetrics metrics1, StructuralMetrics metrics2)
    {
        // Compare line counts, lengths, etc.
        var lineCountSim = 1.0 - Math.Abs(metrics1.LineCount - metrics2.LineCount) / (double)Math.Max(metrics1.LineCount, metrics2.LineCount);
        var lengthSim = 1.0 - Math.Abs(metrics1.TotalLength - metrics2.TotalLength) / (double)Math.Max(metrics1.TotalLength, metrics2.TotalLength);
        var wordCountSim = 1.0 - Math.Abs(metrics1.UniqueWordCount - metrics2.UniqueWordCount) / (double)Math.Max(metrics1.UniqueWordCount, metrics2.UniqueWordCount);

        return (lineCountSim + lengthSim + wordCountSim) / 3.0;
    }

    private double CalculatePatternSimilarity(PatternMetrics metrics1, PatternMetrics metrics2)
    {
        // Compare pattern distributions
        var numberSim = CalculatePatternCountSimilarity(metrics1.NumberPatterns, metrics2.NumberPatterns);
        var dateSim = CalculatePatternCountSimilarity(metrics1.DatePatterns, metrics2.DatePatterns);
        var currencySim = CalculatePatternCountSimilarity(metrics1.CurrencyPatterns, metrics2.CurrencyPatterns);
        var emailSim = CalculatePatternCountSimilarity(metrics1.EmailPatterns, metrics2.EmailPatterns);
        var phoneSim = CalculatePatternCountSimilarity(metrics1.PhonePatterns, metrics2.PhonePatterns);

        return (numberSim + dateSim + currencySim + emailSim + phoneSim) / 5.0;
    }

    private double CalculateContentSimilarity(ContentMetrics metrics1, ContentMetrics metrics2)
    {
        // Compare content characteristics
        var densitySim = 1.0 - Math.Abs(metrics1.TextDensity - metrics2.TextDensity);
        var avgLineSim = 1.0 - Math.Abs(metrics1.AverageLineLength - metrics2.AverageLineLength) / Math.Max(metrics1.AverageLineLength, metrics2.AverageLineLength);
        var languageSim = metrics1.PrimaryLanguage == metrics2.PrimaryLanguage ? 1.0 : 0.5;

        return (densitySim + avgLineSim + languageSim) / 3.0;
    }

    private double CalculateLayoutSimilarity(LayoutMetrics metrics1, LayoutMetrics metrics2)
    {
        // Compare layout characteristics
        var columnSim = CalculatePatternCountSimilarity(metrics1.EstimatedColumns, metrics2.EstimatedColumns);
        var sectionSim = CalculatePatternCountSimilarity(metrics1.EstimatedSections, metrics2.EstimatedSections);
        var tableSim = CalculatePatternCountSimilarity(metrics1.TableLikeStructures, metrics2.TableLikeStructures);

        return (columnSim + sectionSim + tableSim) / 3.0;
    }

    private double CalculateKeywordSimilarity(List<string> keywords1, List<string> keywords2)
    {
        if (!keywords1.Any() && !keywords2.Any()) return 1.0;
        if (!keywords1.Any() || !keywords2.Any()) return 0.0;

        var intersection = keywords1.Intersect(keywords2, StringComparer.OrdinalIgnoreCase).Count();
        var union = keywords1.Union(keywords2, StringComparer.OrdinalIgnoreCase).Count();

        return union > 0 ? (double)intersection / union : 0.0; // Jaccard similarity
    }

    private double CalculatePatternCountSimilarity(int count1, int count2)
    {
        if (count1 == 0 && count2 == 0) return 1.0;
        var maxCount = Math.Max(count1, count2);
        return 1.0 - Math.Abs(count1 - count2) / (double)maxCount;
    }

    #endregion

    #region Metric Calculation Methods

    private StructuralMetrics CalculateStructuralMetrics(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return new StructuralMetrics
        {
            LineCount = lines.Length,
            TotalLength = text.Length,
            UniqueWordCount = words.Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            TotalWordCount = words.Length,
            EmptyLineCount = text.Split('\n').Length - lines.Length,
            MaxLineLength = lines.Any() ? lines.Max(l => l.Length) : 0,
            MinLineLength = lines.Any() ? lines.Min(l => l.Length) : 0
        };
    }

    private PatternMetrics CalculatePatternMetrics(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        return new PatternMetrics
        {
            NumberPatterns = lines.Count(l => System.Text.RegularExpressions.Regex.IsMatch(l, @"\d+")),
            DatePatterns = lines.Count(l => ContainsDatePattern(l)),
            CurrencyPatterns = lines.Count(l => ContainsCurrencyPattern(l)),
            EmailPatterns = lines.Count(l => l.Contains("@") && l.Contains(".")),
            PhonePatterns = lines.Count(l => ContainsPhonePattern(l)),
            UrlPatterns = lines.Count(l => l.Contains("http") || l.Contains("www"))
            // TotalPatterns is computed automatically
        };
    }

    private ContentMetrics CalculateContentMetrics(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var totalChars = text.Length;
        var nonWhitespaceChars = text.Count(c => !char.IsWhiteSpace(c));

        return new ContentMetrics
        {
            TextDensity = totalChars > 0 ? (double)nonWhitespaceChars / totalChars : 0,
            AverageLineLength = lines.Any() ? lines.Average(l => l.Length) : 0,
            PrimaryLanguage = DetectPrimaryLanguage(text),
            SpecialCharacterCount = text.Count(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c)),
            UppercaseRatio = text.Any() ? text.Count(char.IsUpper) / (double)text.Length : 0,
            NumericRatio = text.Any() ? text.Count(char.IsDigit) / (double)text.Length : 0
        };
    }

    private LayoutMetrics CalculateLayoutMetrics(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        return new LayoutMetrics
        {
            EstimatedColumns = EstimateColumnCount(lines),
            EstimatedSections = EstimateSectionCount(lines),
            TableLikeStructures = CountTableLikeStructures(lines),
            IndentationLevels = CountIndentationLevels(lines),
            AverageWordsPerLine = lines.Any() ? lines.Average(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length) : 0
        };
    }

    private List<string> ExtractKeywordFingerprint(string text)
    {
        var keywords = new List<string>();
        var lowerText = text.ToLowerInvariant();

        // Document type keywords
        var documentKeywords = new[]
        {
            "invoice", "receipt", "bill", "statement", "account", "payment", "order",
            "tnb", "electricity", "electric", "utility", "water", "gas", "internet", "phone", "mobile",
            "bank", "credit", "debit", "balance", "transaction", "deposit", "withdrawal",
            "total", "amount", "due", "date", "number", "customer", "client", "vendor",
            "tax", "vat", "gst", "discount", "subtotal", "service", "charge", "fee"
        };

        foreach (var keyword in documentKeywords)
        {
            if (lowerText.Contains(keyword))
            {
                keywords.Add(keyword);
            }
        }

        // Add comprehensive currency indicators
        var currencyIndicators = new Dictionary<string, string>
        {
            // Major world currencies
            ["malaysian_currency"] = "rm ringgit myr malaysia",
            ["us_currency"] = "$ usd dollar america usa united states",
            ["euro_currency"] = "€ eur euro europe european",
            ["british_currency"] = "£ gbp pound sterling uk britain british",
            ["japanese_currency"] = "¥ jpy yen japan japanese",
            ["chinese_currency"] = "¥ cny yuan rmb china chinese",
            ["indian_currency"] = "₹ inr rupee india indian",
            ["russian_currency"] = "₽ rub ruble russia russian",
            ["korean_currency"] = "₩ krw won korea korean",
            ["canadian_currency"] = "cad dollar canada canadian",
            ["australian_currency"] = "aud dollar australia australian",
            ["swiss_currency"] = "chf franc switzerland swiss",
            ["swedish_currency"] = "sek krona sweden swedish",
            ["norwegian_currency"] = "nok krone norway norwegian",
            ["danish_currency"] = "dkk krone denmark danish",
            ["polish_currency"] = "pln zloty poland polish",
            ["czech_currency"] = "czk koruna czech republic",
            ["hungarian_currency"] = "huf forint hungary hungarian",
            ["turkish_currency"] = "₺ try lira turkey turkish",
            ["israeli_currency"] = "₪ ils shekel israel israeli",
            ["south_african_currency"] = "zar rand south africa",
            ["brazilian_currency"] = "brl real brazil brazilian",
            ["mexican_currency"] = "mxn peso mexico mexican",
            ["thai_currency"] = "thb baht thailand thai",
            ["singapore_currency"] = "sgd dollar singapore",
            ["indonesian_currency"] = "idr rupiah indonesia indonesian",
            ["philippine_currency"] = "₱ php peso philippines filipino",
            ["vietnamese_currency"] = "₫ vnd dong vietnam vietnamese",
            ["crypto_currency"] = "₿ btc bitcoin eth ethereum crypto cryptocurrency blockchain"
        };

        foreach (var currency in currencyIndicators)
        {
            var terms = currency.Value.Split(' ');
            if (terms.Any(term => lowerText.Contains(term)))
            {
                keywords.Add(currency.Key);
            }
        }

        return keywords.Distinct().Take(15).ToList();
    }

    private Dictionary<string, string> GenerateHashSignatures(string text)
    {
        var signatures = new Dictionary<string, string>();

        // Simple content hash
        signatures["content"] = text.GetHashCode().ToString();
        
        // Structure hash (based on line patterns)
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var structurePattern = string.Join("", lines.Select(l => l.Length > 50 ? "L" : l.Length > 20 ? "M" : "S"));
        signatures["structure"] = structurePattern.GetHashCode().ToString();

        // Keyword hash
        var keywords = ExtractKeywordFingerprint(text);
        signatures["keywords"] = string.Join(",", keywords).GetHashCode().ToString();

        return signatures;
    }

    #endregion

    #region Helper Methods

    private bool ContainsDatePattern(string text)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(text, 
            @"\d{1,2}[\/\-\.]\d{1,2}[\/\-\.]\d{2,4}|\d{4}[\/\-\.]\d{1,2}[\/\-\.]\d{1,2}");
    }

    private bool ContainsCurrencyPattern(string text)
    {
        // Major world currencies with symbols and codes
        var currencyPatterns = new[]
        {
            // Symbols
            @"[\$][\d,]+\.?\d*",                    // Dollar ($)
            @"[€][\d,]+\.?\d*",                     // Euro (€)
            @"[£][\d,]+\.?\d*",                     // British Pound (£)
            @"[¥][\d,]+\.?\d*",                     // Japanese Yen/Chinese Yuan (¥)
            @"[₹][\d,]+\.?\d*",                     // Indian Rupee (₹)
            @"[₽][\d,]+\.?\d*",                     // Russian Ruble (₽)
            @"[₩][\d,]+\.?\d*",                     // Korean Won (₩)
            @"[₴][\d,]+\.?\d*",                     // Ukrainian Hryvnia (₴)
            @"[₡][\d,]+\.?\d*",                     // Costa Rican Colón (₡)
            @"[₦][\d,]+\.?\d*",                     // Nigerian Naira (₦)
            @"[₨][\d,]+\.?\d*",                     // Pakistani Rupee (₨)
            @"[₱][\d,]+\.?\d*",                     // Philippine Peso (₱)
            @"[₫][\d,]+\.?\d*",                     // Vietnamese Dong (₫)
            @"[₪][\d,]+\.?\d*",                     // Israeli Shekel (₪)
            @"[₯][\d,]+\.?\d*",                     // Greek Drachma (₯)
            @"[₲][\d,]+\.?\d*",                     // Paraguayan Guaraní (₲)
            @"[₵][\d,]+\.?\d*",                     // Ghanaian Cedi (₵)
            @"[₶][\d,]+\.?\d*",                     // Livre Tournois (₶)
            @"[₷][\d,]+\.?\d*",                     // Spesmilo (₷)
            @"[₸][\d,]+\.?\d*",                     // Kazakhstani Tenge (₸)
            @"[₹][\d,]+\.?\d*",                     // Indian Rupee (₹)
            @"[₺][\d,]+\.?\d*",                     // Turkish Lira (₺)
            @"[₻][\d,]+\.?\d*",                     // Nordic Mark (₻)
            @"[₼][\d,]+\.?\d*",                     // Azerbaijani Manat (₼)
            @"[₽][\d,]+\.?\d*",                     // Russian Ruble (₽)
            @"[₾][\d,]+\.?\d*",                     // Georgian Lari (₾)
            @"[₿][\d,]+\.?\d*",                     // Bitcoin (₿)
            @"[＄][\d,]+\.?\d*",                     // Fullwidth Dollar (＄)
            
            // Currency codes (3-letter ISO codes)
            @"\b(USD|EUR|GBP|JPY|CNY|INR|RUB|KRW|CAD|AUD|CHF|SEK|NOK|DKK|PLN|CZK|HUF|BGN|RON|HRK|TRY|ILS|ZAR|BRL|MXN|ARS|CLP|COP|PEN|UYU|VEF|THB|SGD|MYR|IDR|PHP|VND|KRW|TWD|HKD|NZD|FJD|WST|TOP|PGK|SBD|VUV|NCF|XPF|AED|SAR|QAR|OMR|KWD|BHD|JOD|LBP|SYP|IQD|IRR|AFN|PKR|LKR|NPR|BTN|BDT|MMK|LAK|KHR|MOP|BND|EGP|LYD|TND|DZD|MAD|XOF|XAF|NGN|GHS|SLL|LRD|GMD|GNF|CIV|BFA|MLI|NER|TCD|CMR|CAF|GNQ|GAB|COG|COD|RWF|BIF|SOS|DJF|ETB|KES|TZS|UGX|MWK|ZMW|BWP|SZL|LSL|ZAR|NAD|MZN|AOA|STD|CVE|GWP|XAG|XAU|XPD|XPT|BTC|ETH|LTC|XRP|BCH|ADA|DOT|SOL|DOGE|MATIC|SHIB|AVAX|TRX|ATOM|LINK|XLM|ALGO|VET|ICP|FIL|ETC|THETA|XMR|BSV|EOS|XTZ|NEO|MIOTA|MKR|AAVE|UNI|SUSHI|CRV|YFI|COMP|SNX|BAL|REN|KNC|ZRX|LRC|OMG|BAT|ENJ|MANA|SAND|AXS|CHZ|FTT|SRM|RAY|COPE|STEP|GMT|APE|LOOKS|X2Y2|BLUR)\\s*[\\d,]+\\.?\\d*",
            
            // Currency words/names
            @"\\b(RM|Ringgit|Dollar|Euro|Pound|Yen|Yuan|Rupee|Ruble|Won|Peso|Real|Franc|Krona|Krone|Zloty|Forint|Leu|Kuna|Lira|Shekel|Rand|Baht|Dong|Rupiah|Dinar|Dirham|Riyal)\\s*[\\d,]+\\.?\\d*",
            
            // Regional formats
            @"\\d+[,.]\\d{2}\\s*(USD|EUR|GBP|JPY|CNY|INR|RUB|KRW|CAD|AUD|CHF|SEK|NOK|DKK|PLN|CZK|HUF|RM|THB|SGD|MYR|IDR|PHP|VND)",
            @"(USD|EUR|GBP|JPY|CNY|INR|RUB|KRW|CAD|AUD|CHF|SEK|NOK|DKK|PLN|CZK|HUF|RM|THB|SGD|MYR|IDR|PHP|VND)\\s*\\d+[,.]?\\d*"
        };

        return currencyPatterns.Any(pattern => 
            System.Text.RegularExpressions.Regex.IsMatch(text, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase));
    }

    private bool ContainsPhonePattern(string text)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(text, 
            @"\(?\d{3}\)?[-.\s]?\d{3}[-.\s]?\d{4}|\+\d{1,3}[-.\s]?\d{1,3}[-.\s]?\d{3,4}[-.\s]?\d{3,4}");
    }

    private string DetectPrimaryLanguage(string text)
    {
        var lowerText = text.ToLowerInvariant();
        var languageScores = new Dictionary<string, int>();

        // Major world languages with common words
        var languageKeywords = new Dictionary<string, string[]>
        {
            ["English"] = new[] { "and", "or", "with", "for", "is", "on", "from", "not", "the", "this", "that", "total", "bill", "number", "amount", "date", "account", "payment" },
            ["Spanish"] = new[] { "y", "o", "con", "para", "es", "en", "de", "no", "el", "la", "este", "total", "factura", "número", "cantidad", "fecha", "cuenta", "pago" },
            ["French"] = new[] { "et", "ou", "avec", "pour", "est", "sur", "de", "non", "le", "la", "ce", "total", "facture", "numéro", "montant", "date", "compte", "paiement" },
            ["German"] = new[] { "und", "oder", "mit", "für", "ist", "auf", "von", "nicht", "der", "die", "das", "gesamt", "rechnung", "nummer", "betrag", "datum", "konto", "zahlung" },
            ["Italian"] = new[] { "e", "o", "con", "per", "è", "su", "da", "non", "il", "la", "questo", "totale", "fattura", "numero", "importo", "data", "conto", "pagamento" },
            ["Portuguese"] = new[] { "e", "ou", "com", "para", "é", "em", "de", "não", "o", "a", "este", "total", "fatura", "número", "quantia", "data", "conta", "pagamento" },
            ["Russian"] = new[] { "и", "или", "с", "для", "это", "на", "от", "не", "этот", "общий", "счет", "номер", "сумма", "дата", "счёт", "платеж" },
            ["Chinese"] = new[] { "和", "或", "与", "为", "是", "在", "从", "不", "这", "总", "账单", "号码", "金额", "日期", "账户", "付款" },
            ["Japanese"] = new[] { "と", "または", "で", "ため", "です", "に", "から", "ない", "この", "合計", "請求書", "番号", "金額", "日付", "口座", "支払い" },
            ["Korean"] = new[] { "그리고", "또는", "와", "위해", "입니다", "에", "부터", "아닌", "이", "총", "청구서", "번호", "금액", "날짜", "계정", "결제" },
            ["Arabic"] = new[] { "و", "أو", "مع", "ل", "هو", "على", "من", "لا", "هذا", "المجموع", "فاتورة", "رقم", "مبلغ", "تاريخ", "حساب", "دفع" },
            ["Hindi"] = new[] { "और", "या", "के साथ", "के लिए", "है", "पर", "से", "नहीं", "यह", "कुल", "बिल", "संख्या", "राशि", "दिनांक", "खाता", "भुगतान" },
            ["Dutch"] = new[] { "en", "of", "met", "voor", "is", "op", "van", "niet", "de", "het", "dit", "totaal", "rekening", "nummer", "bedrag", "datum", "account", "betaling" },
            ["Swedish"] = new[] { "och", "eller", "med", "för", "är", "på", "från", "inte", "det", "denna", "total", "räkning", "nummer", "belopp", "datum", "konto", "betalning" },
            ["Norwegian"] = new[] { "og", "eller", "med", "for", "er", "på", "fra", "ikke", "det", "denne", "total", "regning", "nummer", "beløp", "dato", "konto", "betaling" },
            ["Danish"] = new[] { "og", "eller", "med", "for", "er", "på", "fra", "ikke", "det", "denne", "total", "regning", "nummer", "beløb", "dato", "konto", "betaling" },
            ["Polish"] = new[] { "i", "lub", "z", "dla", "jest", "na", "od", "nie", "to", "ta", "łącznie", "rachunek", "numer", "kwota", "data", "konto", "płatność" },
            ["Turkish"] = new[] { "ve", "veya", "ile", "için", "dir", "üzerinde", "dan", "değil", "bu", "toplam", "fatura", "numara", "miktar", "tarih", "hesap", "ödeme" },
            ["Greek"] = new[] { "και", "ή", "με", "για", "είναι", "σε", "από", "όχι", "αυτό", "σύνολο", "λογαριασμός", "αριθμός", "ποσό", "ημερομηνία", "λογαριασμός", "πληρωμή" },
            ["Hebrew"] = new[] { "ו", "או", "עם", "עבור", "הוא", "על", "מ", "לא", "זה", "סה\"כ", "חשבון", "מספר", "סכום", "תאריך", "חשבון", "תשלום" },
            ["Thai"] = new[] { "และ", "หรือ", "กับ", "สำหรับ", "คือ", "บน", "จาก", "ไม่", "นี้", "รวม", "บิล", "หมายเลข", "จำนวน", "วันที่", "บัญชี", "การชำระเงิน" },
            ["Vietnamese"] = new[] { "và", "hoặc", "với", "cho", "là", "trên", "từ", "không", "này", "tổng", "hóa đơn", "số", "số tiền", "ngày", "tài khoản", "thanh toán" },
            ["Indonesian"] = new[] { "dan", "atau", "dengan", "untuk", "adalah", "di", "dari", "tidak", "ini", "total", "tagihan", "nomor", "jumlah", "tanggal", "akun", "pembayaran" },
            ["Malay"] = new[] { "dan", "atau", "dengan", "untuk", "adalah", "pada", "dari", "tidak", "yang", "ini", "itu", "jumlah", "bil", "nombor", "amaun", "tarikh", "akaun", "bayaran" },
            ["Finnish"] = new[] { "ja", "tai", "kanssa", "varten", "on", "päällä", "alkaen", "ei", "tämä", "yhteensä", "lasku", "numero", "summa", "päivämäärä", "tili", "maksu" },
            ["Hungarian"] = new[] { "és", "vagy", "val", "számára", "az", "on", "tól", "nem", "ez", "összesen", "számla", "szám", "összeg", "dátum", "számla", "fizetés" },
            ["Czech"] = new[] { "a", "nebo", "s", "pro", "je", "na", "od", "ne", "to", "celkem", "účet", "číslo", "částka", "datum", "účet", "platba" },
            ["Slovak"] = new[] { "a", "alebo", "s", "pre", "je", "na", "od", "nie", "to", "celkom", "účet", "číslo", "suma", "dátum", "účet", "platba" }
        };

        // Count occurrences for each language
        foreach (var language in languageKeywords)
        {
            var count = language.Value.Count(word => lowerText.Contains(word));
            if (count > 0)
            {
                languageScores[language.Key] = count;
            }
        }

        // Return the language with highest score
        if (languageScores.Any())
        {
            var topLanguages = languageScores.OrderByDescending(x => x.Value).Take(2).ToList();
            
            // If top two scores are close, return "Mixed"
            if (topLanguages.Count > 1 && topLanguages[0].Value == topLanguages[1].Value)
                return "Mixed";
                
            return topLanguages[0].Key;
        }

        return "Unknown";
    }

    private int EstimateColumnCount(string[] lines)
    {
        // Simple heuristic: look for consistent spacing patterns
        var spacingPatterns = lines
            .Where(l => l.Contains("  ")) // Lines with multiple spaces
            .Select(l => l.Split(new[] { "  " }, StringSplitOptions.RemoveEmptyEntries).Length)
            .ToList();

        return spacingPatterns.Any() ? (int)spacingPatterns.Average() : 1;
    }

    private int EstimateSectionCount(string[] lines)
    {
        // Count blank lines and header-like patterns
        var blankLineCount = 0;
        var headerLikeCount = 0;

        for (int i = 0; i < lines.Length - 1; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) blankLineCount++;
            if (lines[i].Length < 50 && char.IsUpper(lines[i][0])) headerLikeCount++;
        }

        return Math.Max(1, blankLineCount + headerLikeCount);
    }

    private int CountTableLikeStructures(string[] lines)
    {
        // Look for lines with consistent column separators
        return lines.Count(l => l.Count(c => c == '|') > 2 || l.Count(c => c == '\t') > 2);
    }

    private int CountIndentationLevels(string[] lines)
    {
        var indentations = lines
            .Select(l => l.TakeWhile(char.IsWhiteSpace).Count())
            .Distinct()
            .Count();

        return Math.Max(1, indentations);
    }

    private string GenerateMatchReason(SimilarityResult similarity)
    {
        var reasons = new List<string>();

        if (similarity.StructuralSimilarity > 0.8) reasons.Add("similar structure");
        if (similarity.PatternSimilarity > 0.8) reasons.Add("matching patterns");
        if (similarity.ContentSimilarity > 0.8) reasons.Add("similar content");
        if (similarity.KeywordSimilarity > 0.8) reasons.Add("shared keywords");

        return reasons.Any() ? string.Join(", ", reasons) : "general similarity";
    }

    #endregion
}

#region Data Models

public class DocumentFingerprint
{
    public string DocumentPath { get; set; } = "";
    public StructuralMetrics StructuralMetrics { get; set; } = new();
    public PatternMetrics PatternMetrics { get; set; } = new();
    public ContentMetrics ContentMetrics { get; set; } = new();
    public LayoutMetrics LayoutMetrics { get; set; } = new();
    public List<string> KeywordFingerprint { get; set; } = new();
    public Dictionary<string, string> HashSignatures { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

public class StructuralMetrics
{
    public int LineCount { get; set; }
    public int TotalLength { get; set; }
    public int UniqueWordCount { get; set; }
    public int TotalWordCount { get; set; }
    public int EmptyLineCount { get; set; }
    public int MaxLineLength { get; set; }
    public int MinLineLength { get; set; }
}

public class PatternMetrics
{
    public int NumberPatterns { get; set; }
    public int DatePatterns { get; set; }
    public int CurrencyPatterns { get; set; }
    public int EmailPatterns { get; set; }
    public int PhonePatterns { get; set; }
    public int UrlPatterns { get; set; }
    public int TotalPatterns => NumberPatterns + DatePatterns + CurrencyPatterns + EmailPatterns + PhonePatterns + UrlPatterns;
}

public class ContentMetrics
{
    public double TextDensity { get; set; }
    public double AverageLineLength { get; set; }
    public string PrimaryLanguage { get; set; } = "Unknown";
    public int SpecialCharacterCount { get; set; }
    public double UppercaseRatio { get; set; }
    public double NumericRatio { get; set; }
}

public class LayoutMetrics
{
    public int EstimatedColumns { get; set; }
    public int EstimatedSections { get; set; }
    public int TableLikeStructures { get; set; }
    public int IndentationLevels { get; set; }
    public double AverageWordsPerLine { get; set; }
}

public class SimilarityResult
{
    public bool Success { get; set; } = true;
    public string ErrorMessage { get; set; } = "";
    public double OverallSimilarity { get; set; }
    public double StructuralSimilarity { get; set; }
    public double PatternSimilarity { get; set; }
    public double ContentSimilarity { get; set; }
    public double LayoutSimilarity { get; set; }
    public double KeywordSimilarity { get; set; }
    public ConfidenceLevel ConfidenceLevel { get; set; }
}

public class SimilarDocumentMatch
{
    public DocumentFingerprint Document { get; set; } = new();
    public SimilarityResult SimilarityResult { get; set; } = new();
    public string MatchReason { get; set; } = "";
}

public enum ConfidenceLevel
{
    VeryLow,    // < 50%
    Low,        // 50-69%
    Medium,     // 70-84%
    High,       // 85-89%
    VeryHigh    // 90%+
}

#endregion