using IrsEaser.Services;
using Xunit;

namespace IrsEaser.Tests;

public class SupplementaryDividendImportServiceTests : IDisposable
{
    private static readonly SupplementaryDividendImportService Svc = new();
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
            "Date,IncomeCode,CountryCode,GrossAmount,TaxPaid\n" +
            "2025-03-15,E21,840,50.00,7.50\n" +
            "2025-06-20,E21,756,30.00,4.50\n";

        var result = Svc.Load(WriteTempCsv(content));

        Assert.Equal(2, result.Count);

        Assert.Equal(new DateOnly(2025, 3, 15), result[0].Date);
        Assert.Equal("E21",  result[0].IncomeCode);
        Assert.Equal("840",  result[0].CountryCode);
        Assert.Equal(50.00m, result[0].GrossAmount);
        Assert.Equal(7.50m,  result[0].TaxPaid);

        Assert.Equal("756",  result[1].CountryCode);
        Assert.Equal(30.00m, result[1].GrossAmount);
        Assert.Equal(4.50m,  result[1].TaxPaid);
    }

    [Fact]
    public void Load_SkipsRowsWithInvalidDate()
    {
        var content =
            "Date,IncomeCode,CountryCode,GrossAmount,TaxPaid\n" +
            "not-a-date,E21,840,50.00,7.50\n" +
            "2025-01-01,E21,840,100.00,15.00\n";

        var result = Svc.Load(WriteTempCsv(content));

        Assert.Single(result);
    }

    [Fact]
    public void Load_SkipsRowsWithEmptyIncomeCode()
    {
        var content =
            "Date,IncomeCode,CountryCode,GrossAmount,TaxPaid\n" +
            "2025-01-01,,840,50.00,7.50\n" +
            "2025-01-01,E21,840,100.00,15.00\n";

        var result = Svc.Load(WriteTempCsv(content));

        Assert.Single(result);
    }

    [Fact]
    public void Load_SkipsRowsWithEmptyCountryCode()
    {
        var content =
            "Date,IncomeCode,CountryCode,GrossAmount,TaxPaid\n" +
            "2025-01-01,E21,,50.00,7.50\n" +
            "2025-01-01,E21,840,100.00,15.00\n";

        var result = Svc.Load(WriteTempCsv(content));

        Assert.Single(result);
    }

    [Fact]
    public void Load_ZeroTaxPaid_ParsesAsZero()
    {
        var content =
            "Date,IncomeCode,CountryCode,GrossAmount,TaxPaid\n" +
            "2025-01-01,E21,840,100.00,0.00\n";

        var result = Svc.Load(WriteTempCsv(content));

        var row = Assert.Single(result);
        Assert.Equal(0m, row.TaxPaid);
    }
}
