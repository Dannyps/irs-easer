using IrsEaser.Services;
using Xunit;

namespace IrsEaser.Tests;

public class SecuritiesImportServiceTests : IDisposable
{
    private static readonly SecuritiesImportService Svc = new();
    private readonly List<string> _tempFiles = [];

    private string WriteTempCsv(string content)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var f in _tempFiles)
            if (File.Exists(f)) File.Delete(f);
    }

    [Fact]
    public void Load_ParsesAllFields()
    {
        var content =
            "Name;Symbol;ISIN;Source Country (Level 3)\n" +
            "Apple Inc;AAPL;US0378331005;United States\n" +
            "Siemens AG;SIE;DE0007236101;Germany\n";

        var result = Svc.Load(WriteTempCsv(content));

        Assert.Equal(2, result.Count);

        Assert.True(result.ContainsKey("Apple Inc"));
        Assert.Equal("AAPL",          result["Apple Inc"].Symbol);
        Assert.Equal("US0378331005",  result["Apple Inc"].Isin);
        Assert.Equal("United States", result["Apple Inc"].SourceCountry);

        Assert.True(result.ContainsKey("Siemens AG"));
        Assert.Equal("DE0007236101", result["Siemens AG"].Isin);
        Assert.Equal("Germany",      result["Siemens AG"].SourceCountry);
    }

    [Fact]
    public void Load_IgnoresRowsWithEmptyName()
    {
        var content =
            "Name;Symbol;ISIN;Source Country (Level 3)\n" +
            ";AAPL;US0378331005;United States\n" +
            "Apple Inc;AAPL;US0378331005;United States\n";

        var result = Svc.Load(WriteTempCsv(content));

        Assert.Single(result);
    }

    [Fact]
    public void Load_HandlesEmptySourceCountry()
    {
        var content =
            "Name;Symbol;ISIN;Source Country (Level 3)\n" +
            "Mystery Corp;MYST;XY1234567890;\n";

        var result = Svc.Load(WriteTempCsv(content));

        Assert.Single(result);
        Assert.Equal("", result["Mystery Corp"].SourceCountry);
    }

    [Fact]
    public void Load_KeyedByName_CaseSensitive()
    {
        var content =
            "Name;Symbol;ISIN;Source Country (Level 3)\n" +
            "Apple Inc;AAPL;US0378331005;United States\n";

        var result = Svc.Load(WriteTempCsv(content));

        Assert.True(result.ContainsKey("Apple Inc"));
        Assert.False(result.ContainsKey("apple inc"));
    }
}
