using System.Text;
using System.Xml;
using System.Xml.Linq;
using IrsEaser.Models;

namespace IrsEaser.Services;

/// <summary>
/// Generates the IRS Modelo 3 import XML for Anexo J, Quadro 9.2-A.
/// </summary>
public class XmlExportService
{
    public void Export(IReadOnlyList<TradeLine> lines, string outputPath)
    {
        var root = new XElement("AnexoJq092AT01",
            lines.Select((line, idx) => BuildLine(line, idx + 1)),
            new XElement("AnexoJq092AT01SomaC01", FormatDecimal(lines.Sum(l => l.SellValue))),
            new XElement("AnexoJq092AT01SomaC02", FormatDecimal(lines.Sum(l => l.BuyValue))),
            new XElement("AnexoJq092AT01SomaC03", FormatDecimal(lines.Sum(l => l.Fees))),
            new XElement("AnexoJq092AT01SomaC04", FormatDecimal(lines.Sum(l => l.TaxesPaid))));

        var settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            Indent = true,
            Encoding = new UTF8Encoding(false),
        };

        using var writer = XmlWriter.Create(outputPath, settings);
        root.WriteTo(writer);
    }

    private static XElement BuildLine(TradeLine line, int numero)
    {
        var el = new XElement($"AnexoJq092AT01-Linha",
            new XAttribute("numero", numero),
            new XElement("NLinha",          line.LineNumber),
            new XElement("CodPais",         line.CountryCode),
            new XElement("Codigo",          "G01"),
            new XElement("AnoRealizacao",   line.SellDate.Year),
            new XElement("MesRealizacao",   line.SellDate.Month),
            new XElement("DiaRealizacao",   line.SellDate.Day),
            new XElement("ValorRealizacao", FormatDecimal(line.SellValue)),
            new XElement("AnoAquisicao",    line.BuyDate.Year),
            new XElement("MesAquisicao",    line.BuyDate.Month),
            new XElement("DiaAquisicao",    line.BuyDate.Day),
            new XElement("ValorAquisicao",  FormatDecimal(line.BuyValue)),
            new XElement("DespesasEncargos", FormatDecimal(line.Fees))
        );

        // Only include tax element when there are taxes to report
        if (line.TaxesPaid != 0)
            el.Add(new XElement("ImpostoPagoNoEstrangeiro", FormatDecimal(line.TaxesPaid)));

        return el;
    }

    private static string FormatDecimal(decimal value) =>
        value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
}
