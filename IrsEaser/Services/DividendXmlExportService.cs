using System.Text;
using System.Xml.Linq;
using IrsEaser.Models;

namespace IrsEaser.Services;

/// <summary>
/// Generates the IRS Modelo 3 fragment for Anexo J, Quadro 8A (dividend income).
/// Output is an XML fragment (no declaration) ready to paste inside &lt;Quadro08&gt;.
/// </summary>
public class DividendXmlExportService
{
    public void Export(IReadOnlyList<DividendLine> lines, string outputPath)
    {
        using var writer = new StreamWriter(outputPath, append: false, new UTF8Encoding(false));

        var container = new XElement("AnexoJq08AT01",
            lines.Select((line, idx) => BuildLine(line, idx + 1)));

        writer.WriteLine(container.ToString());
        writer.WriteLine(new XElement("AnexoJq08AT01SomaC01", Fmt(lines.Sum(l => l.GrossAmount))));
        writer.WriteLine(new XElement("AnexoJq08AT01SomaC02", Fmt(lines.Sum(l => l.TaxPaid))));
        writer.WriteLine(new XElement("AnexoJq08AT01SomaC03", "0.00"));
        writer.WriteLine(new XElement("AnexoJq08AT01SomaC04", "0.00"));
        writer.WriteLine(new XElement("AnexoJq08B01", "N"));
    }

    private static XElement BuildLine(DividendLine line, int numero)
    {
        var el = new XElement("AnexoJq08AT01-Linha",
            new XAttribute("numero", numero),
            new XElement("NLinha",          line.LineNumber),
            new XElement("CodRendimento",   line.IncomeCode),
            new XElement("CodPais",         line.CountryCode),
            new XElement("RendimentoBruto", Fmt(line.GrossAmount)));

        if (line.TaxPaid != 0)
            el.Add(new XElement("ImpostoPagoEstrangeiroPaisFonte", Fmt(line.TaxPaid)));

        return el;
    }

    private static string Fmt(decimal value) =>
        value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
}
