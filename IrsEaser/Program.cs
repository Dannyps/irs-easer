using IrsEaser.Menu;
using IrsEaser.Services;
using MenuCLI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(l => l.ClearProviders())
    .ConfigureServices((_, services) =>
    {
        services.AddSingleton<CsvImportService>();
        services.AddSingleton<SecuritiesImportService>();
        services.AddSingleton<FifoMatchingService>();
        services.AddSingleton<XmlExportService>();
        services.AddSingleton<DividendService>();
        services.AddSingleton<DividendXmlExportService>();
        services.AddSingleton<SupplementaryDividendImportService>();
        services.AddMenuCLI<MainMenu>();
    })
    .Build();

await host.Services.StartMenu();