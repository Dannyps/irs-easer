using IrsEaser.Models;
using IrsEaser.Services;
using MenuCLI;

namespace IrsEaser.Menu;

[Menu("IRS Easer — Modelo 3, Anexo J", Description = "Select what to generate:")]
public class MainMenu(
    CsvImportService csvService,
    SecuritiesImportService securitiesService,
    FifoMatchingService fifoService,
    XmlExportService xmlExportService,
    DividendService dividendService,
    DividendXmlExportService dividendXmlExportService,
    SupplementaryDividendImportService supplementaryDividendService)
{
    private static readonly string InputDir =
        Path.Combine(Directory.GetCurrentDirectory(), "input");

    // ── Capital Gains ──────────────────────────────────────────────────────────

    [Choice("Capital Gains  —  Quadro 9.2-A  (sell transactions)")]
    public async Task CapitalGainsAsync()
    {
        Console.WriteLine();
        var txPath = PickFile("Select transactions CSV:", "transaction");
        if (txPath is null) { Pause(); return; }

        var secPath = PickFile("Select securities CSV:", "securit");
        if (secPath is null) { Pause(); return; }

        var taxYear  = PromptYear();
        var noLookup = PromptBool("Skip OpenFIGI lookup? [y/N] ");

        var (transactions, countryService) = await LoadCoreAsync(txPath, secPath, taxYear, noLookup);
        if (transactions is null) { Pause(); return; }

        Console.WriteLine($"\nMatching trades (FIFO) for {taxYear}...");
        var lines = fifoService.Match(transactions, countryService!.GetCountry, taxYear);

        if (lines.Count == 0)
        {
            Console.WriteLine("No sell transactions found for the selected year.");
            Pause();
            return;
        }

        PrintCapitalGainsSummary(lines);

        var output = $"irs-quadro9-2a-{taxYear}.xml";
        xmlExportService.Export(lines, output);
        Console.WriteLine($"\nXML written to: {Path.GetFullPath(output)}");
        Pause();
    }

    // ── Dividends ──────────────────────────────────────────────────────────────

    [Choice("Dividends  —  Quadro 8A  (dividend income)")]
    public async Task DividendsAsync()
    {
        Console.WriteLine();
        var txPath = PickFile("Select dividends/earnings CSV:", "earning");
        if (txPath is null) { Pause(); return; }

        var secPath = PickFile("Select securities CSV:", "securit");
        if (secPath is null) { Pause(); return; }

        var suppPath = PickOptionalFile("Select supplementary dividends CSV (optional):", "supplement");
        List<SupplementaryDividend>? suppDividends = suppPath is not null
            ? supplementaryDividendService.Load(suppPath)
            : null;

        var taxYear  = PromptYear();
        var noLookup = PromptBool("Skip OpenFIGI lookup? [y/N] ");

        // Only resolve countries for securities that paid dividends in the tax year
        var (transactions, countryService) = await LoadCoreAsync(
            txPath, secPath, taxYear, noLookup,
            relevantTypes: [TransactionType.Dividend]);
        if (transactions is null) { Pause(); return; }

        Console.WriteLine($"\nBuilding dividend lines for {taxYear}...");
        var lines = dividendService.Build(transactions, countryService!.GetCountry, taxYear, suppDividends);

        if (lines.Count == 0)
        {
            Console.WriteLine("No dividend transactions found for the selected year.");
            Pause();
            return;
        }

        PrintDividendSummary(lines);

        var output = $"irs-quadro8a-{taxYear}.xml";
        dividendXmlExportService.Export(lines, output);
        Console.WriteLine($"\nXML written to: {Path.GetFullPath(output)}");
        Pause();
    }

    // ── Shared loading ─────────────────────────────────────────────────────────

    private async Task<(List<Transaction>? transactions, CountryLookupService? countryService)> LoadCoreAsync(
        string txPath, string secPath, int taxYear, bool noLookup,
        TransactionType[]? relevantTypes = null)
    {
        Console.WriteLine("\nLoading transactions...");
        var transactions = csvService.Load(txPath);
        var buys  = transactions.Count(t => t.Type == TransactionType.Buy);
        var sells = transactions.Count(t => t.Type == TransactionType.Sell);
        var divs  = transactions.Count(t => t.Type == TransactionType.Dividend);
        Console.WriteLine($"  {transactions.Count} rows  ({buys} buys, {sells} sells, {divs} dividends)");

        var allSecurities = transactions
            .Select(t => t.Security)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(s => s)
            .ToList();

        Console.WriteLine("\nLoading securities...");
        var securitiesDict = securitiesService.Load(secPath);
        Console.WriteLine($"  {securitiesDict.Count} securities loaded");

        var missing = allSecurities.Where(s => !securitiesDict.ContainsKey(s)).ToList();
        if (missing.Count > 0)
        {
            Console.Error.WriteLine($"\nError: {missing.Count} security(ies) not found in securities file:");
            foreach (var m in missing)
                Console.Error.WriteLine($"  - {m}");
            return (null, null);
        }

        // Resolve countries only for the securities relevant to the selected feature
        var toResolve = relevantTypes is { Length: > 0 }
            ? transactions
                .Where(t => relevantTypes.Contains(t.Type) && t.Date.Year == taxYear)
                .Select(t => t.Security)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(s => s)
                .ToList()
            : allSecurities;

        var countriesPath = Path.Combine(InputDir, "security-countries.json");
        var countryService = new CountryLookupService(countriesPath, securitiesDict);
        await countryService.ResolveCountriesAsync(toResolve, noLookup);

        return (transactions, countryService);
    }

    // ── File / prompt helpers ──────────────────────────────────────────────────

    private static string? PickOptionalFile(string prompt, string hint = "")
    {
        if (!Directory.Exists(InputDir))
            return null;

        var files = Directory.GetFiles(InputDir, "*.csv")
                             .OrderBy(f => f)
                             .ToList();

        int defaultIndex = -1;
        if (!string.IsNullOrEmpty(hint))
            defaultIndex = files.FindIndex(f =>
                Path.GetFileName(f).Contains(hint, StringComparison.OrdinalIgnoreCase));

        Console.WriteLine($"\n{prompt}");
        Console.WriteLine("  0. (skip)");
        for (int i = 0; i < files.Count; i++)
        {
            var marker = i == defaultIndex ? " [default]" : "";
            Console.WriteLine($"  {i + 1}. {Path.GetFileName(files[i])}{marker}");
        }

        var defaultLabel = defaultIndex >= 0 ? $" [{defaultIndex + 1}]" : " [0]";
        Console.Write($"Choice{defaultLabel}: ");
        var input = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(input))
            return defaultIndex >= 0 ? files[defaultIndex] : null;

        if (input == "0")
            return null;

        if (int.TryParse(input, out var n) && n >= 1 && n <= files.Count)
            return files[n - 1];

        Console.WriteLine("Invalid selection, skipping.");
        return null;
    }

    private static string? PickFile(string prompt, string hint = "")
    {
        if (!Directory.Exists(InputDir))
        {
            Console.WriteLine($"Input directory not found: {InputDir}");
            return null;
        }

        var files = Directory.GetFiles(InputDir, "*.csv")
                             .OrderBy(f => f)
                             .ToList();

        if (files.Count == 0)
        {
            Console.WriteLine("No CSV files found in the input directory.");
            return null;
        }

        int defaultIndex = -1;
        if (!string.IsNullOrEmpty(hint))
            defaultIndex = files.FindIndex(f =>
                Path.GetFileName(f).Contains(hint, StringComparison.OrdinalIgnoreCase));

        Console.WriteLine($"\n{prompt}");
        for (int i = 0; i < files.Count; i++)
        {
            var marker = i == defaultIndex ? " [default]" : "";
            Console.WriteLine($"  {i + 1}. {Path.GetFileName(files[i])}{marker}");
        }

        var defaultLabel = defaultIndex >= 0 ? $" [{defaultIndex + 1}]" : "";
        Console.Write($"Choice{defaultLabel}: ");
        var input = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(input) && defaultIndex >= 0)
            return files[defaultIndex];

        if (int.TryParse(input, out var n) && n >= 1 && n <= files.Count)
            return files[n - 1];

        Console.WriteLine("Invalid selection.");
        return null;
    }

    private static int PromptYear()
    {
        var defaultYear = DateTime.Today.Year - 1;
        Console.Write($"\nTax year [{defaultYear}]: ");
        var input = Console.ReadLine()?.Trim();
        return int.TryParse(input, out var y) ? y : defaultYear;
    }

    private static bool PromptBool(string prompt)
    {
        Console.Write(prompt);
        var input = Console.ReadLine()?.Trim().ToLowerInvariant();
        return input is "y" or "yes";
    }

    private static void Pause()
    {
        Console.Write("\nPress any key to return to menu...");
        Console.ReadKey(intercept: true);
        Console.WriteLine();
    }

    // ── Summary printers ───────────────────────────────────────────────────────

    private static void PrintCapitalGainsSummary(List<TradeLine> lines)
    {
        Console.WriteLine();
        Console.WriteLine($"  {"#",-5} {"Security",-35} {"Sell Date",-12} {"Sell Value",12} {"Buy Date",-12} {"Buy Value",12} {"Fees",8} {"Taxes",8}");
        Console.WriteLine(new string('-', 112));
        foreach (var l in lines)
            Console.WriteLine($"  {l.LineNumber,-5} {l.SecurityName,-35} {l.SellDate,-12} {l.SellValue,12:F2} {l.BuyDate,-12} {l.BuyValue,12:F2} {l.Fees,8:F2} {l.TaxesPaid,8:F2}");
        Console.WriteLine(new string('-', 112));
        Console.WriteLine($"\n  {lines.Count} lines  (NLinha 951–{950 + lines.Count})");
        Console.WriteLine($"  SomaC01 (sell)  : {lines.Sum(l => l.SellValue),10:F2}");
        Console.WriteLine($"  SomaC02 (buy)   : {lines.Sum(l => l.BuyValue),10:F2}");
        Console.WriteLine($"  SomaC03 (fees)  : {lines.Sum(l => l.Fees),10:F2}");
        Console.WriteLine($"  SomaC04 (taxes) : {lines.Sum(l => l.TaxesPaid),10:F2}");
    }

    private static void PrintDividendSummary(List<DividendLine> lines)
    {
        Console.WriteLine();
        Console.WriteLine($"  {"NLinha",-8} {"Code",-6} {"Country",8} {"Gross Amount",14} {"Tax Paid",10}");
        Console.WriteLine(new string('-', 52));
        foreach (var l in lines)
            Console.WriteLine($"  {l.LineNumber,-8} {l.IncomeCode,-6} {l.CountryCode,8} {l.GrossAmount,14:F2} {l.TaxPaid,10:F2}");
        Console.WriteLine(new string('-', 52));
        Console.WriteLine($"\n  {lines.Count} lines  (NLinha 801–{800 + lines.Count})");
        Console.WriteLine($"  SomaC01 (gross) : {lines.Sum(l => l.GrossAmount),10:F2}");
        Console.WriteLine($"  SomaC02 (taxes) : {lines.Sum(l => l.TaxPaid),10:F2}");
    }
}
