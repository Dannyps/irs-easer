using IrsEaser.Models;
using IrsEaser.Services;
using Xunit;

namespace IrsEaser.Tests;

public class CsvImportServiceTests : IDisposable
{
    private static readonly CsvImportService Svc = new();
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

    private const string CommaHeader =
        "Date,Type,Security,Shares,Quote,Amount,Fees,Taxes,Net Transaction Value,Account,Offset Account,Note,Source\n";

    [Fact]
    public void Load_CommaSeparated_ParsesAllFields()
    {
        var content = CommaHeader +
            "2025-03-01,Buy,ACME Inc,100,5.00,500.00,1.50,0.00,501.50,Broker,Cash,,\n";

        var txs = Svc.Load(WriteTempCsv(content));

        var tx = Assert.Single(txs);
        Assert.Equal(TransactionType.Buy, tx.Type);
        Assert.Equal("ACME Inc", tx.Security);
        Assert.Equal(100m,     tx.Shares);
        Assert.Equal(5.00m,    tx.Quote);
        Assert.Equal(500.00m,  tx.Amount);
        Assert.Equal(1.50m,    tx.Fees);
        Assert.Equal(0m,       tx.Taxes);
        Assert.Equal(501.50m,  tx.NetTransactionValue);
        Assert.Equal(new DateOnly(2025, 3, 1), tx.Date);
    }

    [Fact]
    public void Load_SemicolonSeparated_GermanDecimals()
    {
        var content =
            "Date;Type;Security;Shares;Quote;Amount;Fees;Taxes;Net Transaction Value;Account;Offset Account;Note;Source\n" +
            "01.03.2025;Kauf;Siemens AG;50;20,00;1.000,00;2,00;0,00;1.002,00;Depot;Konto;;\n";

        var txs = Svc.Load(WriteTempCsv(content));

        var tx = Assert.Single(txs);
        Assert.Equal(TransactionType.Buy, tx.Type);
        Assert.Equal("Siemens AG", tx.Security);
        Assert.Equal(50m,     tx.Shares);
        Assert.Equal(1002.00m, tx.NetTransactionValue);
    }

    [Fact]
    public void Load_ParsesSellType()
    {
        var content = CommaHeader +
            "2025-06-01,Sell,ACME Inc,50,6.00,300.00,1.00,0.00,299.00,Broker,Cash,,\n";

        var txs = Svc.Load(WriteTempCsv(content));

        Assert.Equal(TransactionType.Sell, txs[0].Type);
    }

    [Fact]
    public void Load_ParsesDividendType()
    {
        var content = CommaHeader +
            "2025-03-01,Dividend,ACME Inc,0,0.00,20.00,0.00,3.00,17.00,Broker,Cash,,\n";

        var txs = Svc.Load(WriteTempCsv(content));

        Assert.Equal(TransactionType.Dividend, txs[0].Type);
    }

    [Fact]
    public void Load_ParsesGermanTypeNames()
    {
        var content = CommaHeader +
            "2025-01-01,Kauf,A,10,1.00,10.00,0,0,10.00,,,,\n" +
            "2025-02-01,Verkauf,A,10,1.00,10.00,0,0,10.00,,,,\n" +
            "2025-03-01,Dividende,A,0,0,5.00,0,0,5.00,,,,\n";

        var txs = Svc.Load(WriteTempCsv(content));

        Assert.Equal(3, txs.Count);
        Assert.Equal(TransactionType.Buy,      txs[0].Type);
        Assert.Equal(TransactionType.Sell,     txs[1].Type);
        Assert.Equal(TransactionType.Dividend, txs[2].Type);
    }

    [Theory]
    [InlineData("01.03.2025")] // dd.MM.yyyy
    [InlineData("3/1/2025")]   // M/d/yyyy
    [InlineData("2025-03-01")] // yyyy-MM-dd
    public void Load_HandlesMultipleDateFormats(string dateStr)
    {
        var content = CommaHeader +
            $"{dateStr},Buy,A,1,1.00,1.00,0,0,1.00,,,,\n";

        var txs = Svc.Load(WriteTempCsv(content));

        Assert.Single(txs);
        Assert.Equal(2025, txs[0].Date.Year);
    }

    [Fact]
    public void Load_DateWithTimestamp_Stripped()
    {
        var content = CommaHeader +
            "2025-03-27 11:55:00,Buy,A,1,1.00,1.00,0,0,1.00,,,,\n";

        var txs = Svc.Load(WriteTempCsv(content));

        Assert.Single(txs);
        Assert.Equal(new DateOnly(2025, 3, 27), txs[0].Date);
    }

    [Fact]
    public void Load_SkipsRowsWithInvalidDate()
    {
        var content = CommaHeader +
            "not-a-date,Buy,A,10,1,10,0,0,10,,,,\n" +
            "2025-01-15,Buy,A,10,1,10,0,0,10,,,,\n";

        var txs = Svc.Load(WriteTempCsv(content));

        Assert.Single(txs);
    }

    [Fact]
    public void Load_SortsChronologically()
    {
        var content = CommaHeader +
            "2025-06-01,Buy,A,1,1,1,0,0,1,,,,\n" +
            "2025-01-01,Buy,A,1,1,1,0,0,1,,,,\n" +
            "2025-03-15,Buy,A,1,1,1,0,0,1,,,,\n";

        var txs = Svc.Load(WriteTempCsv(content));

        Assert.Equal(new DateOnly(2025, 1,  1),  txs[0].Date);
        Assert.Equal(new DateOnly(2025, 3, 15),  txs[1].Date);
        Assert.Equal(new DateOnly(2025, 6,  1),  txs[2].Date);
    }

    [Fact]
    public void Load_NetTransactionValue_IsAbsolute()
    {
        var content = CommaHeader +
            "2025-01-01,Sell,A,10,1,10,0,0,-9.50,,,,\n";

        var txs = Svc.Load(WriteTempCsv(content));

        Assert.Equal(9.50m, txs[0].NetTransactionValue);
    }

    [Fact]
    public void Load_UnknownType_MapsToOther()
    {
        var content = CommaHeader +
            "2025-01-01,SomeUnknownType,A,1,1,1,0,0,1,,,,\n";

        var txs = Svc.Load(WriteTempCsv(content));

        Assert.Equal(TransactionType.Other, txs[0].Type);
    }

    [Fact]
    public void Load_MultipleRows_ReturnsAll()
    {
        var content = CommaHeader +
            "2025-01-01,Buy,A,10,5,50,0,0,50,,,,\n" +
            "2025-02-01,Buy,B,20,3,60,0,0,60,,,,\n" +
            "2025-03-01,Sell,A,10,6,60,1,0,59,,,,\n";

        var txs = Svc.Load(WriteTempCsv(content));

        Assert.Equal(3, txs.Count);
    }
}
