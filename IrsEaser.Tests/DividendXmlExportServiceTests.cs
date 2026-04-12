using System.Xml.Linq;
using IrsEaser.Models;
using IrsEaser.Services;
using Xunit;

namespace IrsEaser.Tests;

public class DividendXmlExportServiceTests : IDisposable
{
    private static readonly DividendXmlExportService Svc = new();
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

    private static DividendLine MakeLine(int lineNumber = 801, decimal gross = 100m,
        decimal tax = 15m, string country = "840", string code = "E21")
        => new(lineNumber, code, country, gross, tax);

    [Fact]
    public void Export_ProducesCorrectLineStructure()
    {
        var path = GetTempPath();
        Svc.Export([MakeLine()], path);

        var root  = ParseOutput(path);
        var linha = root.Element("AnexoJq08AT01")!
                        .Element("AnexoJq08AT01-Linha")!;

        Assert.Equal("1",      linha.Attribute("numero")!.Value);
        Assert.Equal("801",    linha.Element("NLinha")!.Value);
        Assert.Equal("E21",    linha.Element("CodRendimento")!.Value);
        Assert.Equal("840",    linha.Element("CodPais")!.Value);
        Assert.Equal("100.00", linha.Element("RendimentoBruto")!.Value);
        Assert.Equal("15.00",  linha.Element("ImpostoPagoEstrangeiroPaisFonte")!.Value);
    }

    [Fact]
    public void Export_OmitsImpostoEstrangeiro_WhenTaxZero()
    {
        var path = GetTempPath();
        Svc.Export([MakeLine(tax: 0m)], path);

        var linha = ParseOutput(path)
            .Element("AnexoJq08AT01")!
            .Element("AnexoJq08AT01-Linha")!;

        Assert.Null(linha.Element("ImpostoPagoEstrangeiroPaisFonte"));
    }

    [Fact]
    public void Export_SumElements_CorrectTotals()
    {
        var lines = new List<DividendLine>
        {
            MakeLine(801, gross: 100m, tax: 15m),
            MakeLine(802, gross:  50m, tax:  7.50m),
        };
        var path = GetTempPath();
        Svc.Export(lines, path);

        var root = ParseOutput(path);
        Assert.Equal("150.00", root.Element("AnexoJq08AT01SomaC01")!.Value); // gross total
        Assert.Equal("22.50",  root.Element("AnexoJq08AT01SomaC02")!.Value); // tax total
        Assert.Equal("0.00",   root.Element("AnexoJq08AT01SomaC03")!.Value);
        Assert.Equal("0.00",   root.Element("AnexoJq08AT01SomaC04")!.Value);
    }

    [Fact]
    public void Export_AnexoJq08B01_IsN()
    {
        var path = GetTempPath();
        Svc.Export([MakeLine()], path);

        Assert.Equal("N", ParseOutput(path).Element("AnexoJq08B01")!.Value);
    }

    [Fact]
    public void Export_MultipleLines_NumeroAttributeIncrements()
    {
        var lines = new List<DividendLine> { MakeLine(801), MakeLine(802) };
        var path = GetTempPath();
        Svc.Export(lines, path);

        var linhas = ParseOutput(path)
            .Element("AnexoJq08AT01")!
            .Elements("AnexoJq08AT01-Linha")
            .ToList();

        Assert.Equal(2, linhas.Count);
        Assert.Equal("1", linhas[0].Attribute("numero")!.Value);
        Assert.Equal("2", linhas[1].Attribute("numero")!.Value);
    }
}
