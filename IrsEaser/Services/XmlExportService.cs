using System.Text;
using System.Xml.Linq;
using IrsEaser.Models;

namespace IrsEaser.Services;

/// <summary>
/// Generates the IRS Modelo 3 import XML fragment for Anexo J, Quadro 9.2-A.
/// Output has no XML declaration and the SomaC elements are siblings of
/// AnexoJq092AT01, matching the schema expected inside &lt;Quadro09&gt;.
/// </summary>
public class XmlExportService
{
    public void Export(IReadOnlyList<TradeLine> lines, string outputPath)
    {
        using var writer = new StreamWriter(outputPath, append: false, new UTF8Encoding(false));

        var container = new XElement("AnexoJq092AT01",
            lines.Select((line, idx) => BuildLine(line, idx + 1)));

        writer.WriteLine(container.ToString());
        writer.WriteLine(new XElement("AnexoJq092AT01SomaC01", FormatDecimal(lines.Sum(l => l.SellValue))));
        writer.WriteLine(new XElement("AnexoJq092AT01SomaC02", FormatDecimal(lines.Sum(l => l.BuyValue))));
        writer.WriteLine(new XElement("AnexoJq092AT01SomaC03", FormatDecimal(lines.Sum(l => l.Fees))));
        writer.WriteLine(new XElement("AnexoJq092AT01SomaC04", FormatDecimal(lines.Sum(l => l.TaxesPaid))));
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
