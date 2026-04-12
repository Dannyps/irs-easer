using System.Xml.Linq;
using IrsEaser.Models;
using IrsEaser.Services;
using Xunit;

namespace IrsEaser.Tests;

public class XmlExportServiceTests : IDisposable
{
    private static readonly XmlExportService Svc = new();
    private readonly List<string> _tempFiles = [];

    private string GetTempPath()
    {
        var path = Path.ChangeExtension(Path.GetTempFileName(), ".xml");
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var f in _tempFiles)
            if (File.Exists(f)) File.Delete(f);
    }

    // The output is multiple sibling elements with no shared root; wrap to parse.
    private static XElement ParseOutput(string path)
    {
        var raw = File.ReadAllText(path);
        return XElement.Parse("<root>" + raw + "</root>");
    }

    private static TradeLine MakeLine(int lineNumber = 951, decimal sellValue = 600m,
        decimal buyValue = 400m, decimal fees = 2m, decimal taxes = 0m)
        => new(lineNumber, "276", "ACME", new DateOnly(2025, 11, 14), sellValue,
               new DateOnly(2024, 3, 3), buyValue, fees, taxes);

    [Fact]
    public void Export_ProducesCorrectLineStructure()
    {
        var path = GetTempPath();
        Svc.Export([MakeLine()], path);

        var root   = ParseOutput(path);
        var linha  = root.Element("AnexoJq092AT01")!
                         .Element("AnexoJq092AT01-Linha")!;

        Assert.Equal("1",      linha.Attribute("numero")!.Value);
        Assert.Equal("951",    linha.Element("NLinha")!.Value);
        Assert.Equal("276",    linha.Element("CodPais")!.Value);
        Assert.Equal("G01",    linha.Element("Codigo")!.Value);
        Assert.Equal("2025",   linha.Element("AnoRealizacao")!.Value);
        Assert.Equal("11",     linha.Element("MesRealizacao")!.Value);
        Assert.Equal("14",     linha.Element("DiaRealizacao")!.Value);
        Assert.Equal("600.00", linha.Element("ValorRealizacao")!.Value);
        Assert.Equal("2024",   linha.Element("AnoAquisicao")!.Value);
        Assert.Equal("3",      linha.Element("MesAquisicao")!.Value);
        Assert.Equal("3",      linha.Element("DiaAquisicao")!.Value);
        Assert.Equal("400.00", linha.Element("ValorAquisicao")!.Value);
        Assert.Equal("2.00",   linha.Element("DespesasEncargos")!.Value);
    }

    [Fact]
    public void Export_OmitsImpostoPagoNoEstrangeiro_WhenTaxesZero()
    {
        var path = GetTempPath();
        Svc.Export([MakeLine(taxes: 0m)], path);

        var linha = ParseOutput(path)
            .Element("AnexoJq092AT01")!
            .Element("AnexoJq092AT01-Linha")!;

        Assert.Null(linha.Element("ImpostoPagoNoEstrangeiro"));
    }

    [Fact]
    public void Export_IncludesImpostoPagoNoEstrangeiro_WhenTaxesNonZero()
    {
        var path = GetTempPath();
        Svc.Export([MakeLine(taxes: 1.50m)], path);

        var linha = ParseOutput(path)
            .Element("AnexoJq092AT01")!
            .Element("AnexoJq092AT01-Linha")!;

        Assert.Equal("1.50", linha.Element("ImpostoPagoNoEstrangeiro")!.Value);
    }

    [Fact]
    public void Export_SumElements_CorrectTotals()
    {
        var lines = new List<TradeLine>
        {
            MakeLine(951, sellValue: 600m, buyValue: 400m, fees: 2m, taxes: 1m),
            MakeLine(952, sellValue: 400m, buyValue: 300m, fees: 1m, taxes: 0.50m),
        };
        var path = GetTempPath();
        Svc.Export(lines, path);

        var root = ParseOutput(path);
        Assert.Equal("1000.00", root.Element("AnexoJq092AT01SomaC01")!.Value); // sell total
        Assert.Equal("700.00",  root.Element("AnexoJq092AT01SomaC02")!.Value); // buy total
        Assert.Equal("3.00",    root.Element("AnexoJq092AT01SomaC03")!.Value); // fees total
        Assert.Equal("1.50",    root.Element("AnexoJq092AT01SomaC04")!.Value); // taxes total
    }

    [Fact]
    public void Export_MultipleLines_NumeroAttributeIncrements()
    {
        var lines = new List<TradeLine> { MakeLine(951), MakeLine(952) };
        var path = GetTempPath();
        Svc.Export(lines, path);

        var linhas = ParseOutput(path)
            .Element("AnexoJq092AT01")!
            .Elements("AnexoJq092AT01-Linha")
            .ToList();

        Assert.Equal(2, linhas.Count);
        Assert.Equal("1", linhas[0].Attribute("numero")!.Value);
        Assert.Equal("2", linhas[1].Attribute("numero")!.Value);
    }
}
