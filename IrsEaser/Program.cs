using IrsEaser.Models;
using IrsEaser.Services;

// ── Argument parsing ──────────────────────────────────────────────────────────

string csvPath = string.Empty;
int taxYear = DateTime.Today.Year - 1;  // default: previous year
string outputPath = string.Empty;
string countriesPath = string.Empty;
bool noLookup = false;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "-c": case "--csv":        csvPath       = args[++i]; break;
        case "-y": case "--year":       taxYear       = int.Parse(args[++i]); break;
        case "-o": case "--output":     outputPath    = args[++i]; break;
        case "--countries":             countriesPath = args[++i]; break;
        case "--no-lookup":             noLookup      = true; break;
        case "-h": case "--help":       PrintUsage(); return 0;
    }
}

if (string.IsNullOrEmpty(csvPath))
{
    PrintUsage();
    return 1;
}

if (!File.Exists(csvPath))
{
    Console.Error.WriteLine($"Error: CSV file not found: {csvPath}");
    return 1;
}

if (string.IsNullOrEmpty(outputPath))
    outputPath = $"irs-anexo-j-{taxYear}.xml";

if (string.IsNullOrEmpty(countriesPath))
    countriesPath = Path.Combine(
        Path.GetDirectoryName(Path.GetFullPath(csvPath))!,
        "security-countries.json");

// ── Load CSV ──────────────────────────────────────────────────────────────────

Console.WriteLine($"Loading transactions from: {csvPath}");
var csvService = new CsvImportService();
var transactions = csvService.Load(csvPath);

var buys  = transactions.Count(t => t.Type == TransactionType.Buy);
var sells = transactions.Count(t => t.Type == TransactionType.Sell);
Console.WriteLine($"Loaded {transactions.Count} transactions  ({buys} buys, {sells} sells).");

var securities = transactions
    .Select(t => t.Security)
    .Distinct(StringComparer.Ordinal)
    .OrderBy(s => s)
    .ToList();

Console.WriteLine($"Distinct securities: {securities.Count}");

// ── Country resolution ────────────────────────────────────────────────────────

var countryService = new CountryLookupService(countriesPath);
await countryService.ResolveCountriesAsync(securities, noLookup);

// ── FIFO matching ─────────────────────────────────────────────────────────────

Console.WriteLine($"\nMatching trades for tax year {taxYear} (FIFO)...");
var fifoService = new FifoMatchingService();
var tradeLines = fifoService.Match(transactions, countryService.GetCountry, taxYear);

if (tradeLines.Count == 0)
{
    Console.WriteLine($"No sell transactions found for {taxYear}. Nothing to export.");
    return 0;
}

Console.WriteLine($"\nGenerated {tradeLines.Count} Anexo J lines (starting at NLinha 951).");
PrintSummary(tradeLines);

// ── XML export ────────────────────────────────────────────────────────────────

var xmlService = new XmlExportService();
xmlService.Export(tradeLines, outputPath);
Console.WriteLine($"\nXML written to: {outputPath}");
return 0;

// ── Helpers ───────────────────────────────────────────────────────────────────

static void PrintUsage()
{
    Console.WriteLine("""
        IRS Easer — Anexo J, Quadro 9.2-A XML generator

        Usage:
          irs-easer --csv <transactions.csv> [options]

        Options:
          -c, --csv <path>         Path to Portfolio Performance CSV export (required)
          -y, --year <year>        Tax year to report (default: previous calendar year)
          -o, --output <path>      Output XML file (default: irs-anexo-j-<year>.xml)
              --countries <path>   Path to security-countries.json cache
                                   (default: same directory as the CSV)
              --no-lookup          Skip the OpenFIGI API lookup; prompt only
          -h, --help               Show this help
        """);
}

static void PrintSummary(List<TradeLine> lines)
{
    Console.WriteLine();
    Console.WriteLine($"  {"#",-5} {"Security",-35} {"Sell Date",-12} {"Sell Value",12} {"Buy Date",-12} {"Buy Value",12} {"Fees",8} {"Taxes",8}");
    Console.WriteLine(new string('-', 112));
    foreach (var l in lines)
    {
        Console.WriteLine(
            $"  {l.LineNumber,-5} {l.SecurityName,-35} {l.SellDate,-12} {l.SellValue,12:F2} {l.BuyDate,-12} {l.BuyValue,12:F2} {l.Fees,8:F2} {l.TaxesPaid,8:F2}");
    }
}
