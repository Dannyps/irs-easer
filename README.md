# IRS Easer

A command-line tool that generates the IRS Modelo 3 — Anexo J, Quadro 9.2-A XML import file from a [Portfolio Performance](https://www.portfolio-performance.info/) transaction export.

## Background

Portuguese tax residents who sell foreign securities must declare the gains in **Anexo J, Quadro 9.2-A** of IRS Modelo 3. Each sale must be broken down into one line per purchase lot (FIFO), which is tedious to do by hand. This tool automates that process.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Installation

Clone the repository and build:

```
git clone <repo-url>
cd irs-easer
dotnet build IrsEaser/IrsEaser.csproj
```

Or run directly without building first (see Usage below).

## Usage

```
dotnet run --project IrsEaser -- --csv <transactions.csv> [options]
```

### Options

| Flag | Shorthand | Description |
|------|-----------|-------------|
| `--csv <path>` | `-c` | Path to Portfolio Performance CSV export **(required)** |
| `--year <year>` | `-y` | Tax year to report (default: previous calendar year) |
| `--output <path>` | `-o` | Output XML file path (default: `irs-anexo-j-<year>.xml`) |
| `--countries <path>` | | Path to the country code cache JSON file (default: same directory as the CSV) |
| `--no-lookup` | | Skip the OpenFIGI API lookup and go straight to the manual prompt |
| `--help` | `-h` | Show usage information |

### Example

```
dotnet run --project IrsEaser -- --csv ~/exports/transactions.csv --year 2025
```

## Input: Portfolio Performance CSV Export

Export your transactions from Portfolio Performance via **File → Export → Transactions (CSV)**. The expected columns are:

| Column | Description |
|--------|-------------|
| Date | Transaction date |
| Type | `Buy` or `Sell` (all other types are ignored) |
| Security | Security name |
| Shares | Number of shares |
| Quote | Price per share |
| Amount | Gross amount |
| Fees | Brokerage fees |
| Taxes | Taxes paid at source |
| Net Transaction Value | Net amount after fees and taxes |
| Account | Portfolio account |
| Offset Account | Cash account |
| Note | Free-text note |
| Source | Data source |

> Portfolio Performance can export with either `,` or `;` as the delimiter and with either `.` or `,` as the decimal separator. Both formats are handled automatically.

## How It Works

1. **CSV import** — Transactions are loaded and sorted chronologically. Only `Buy` and `Sell` rows are processed.
2. **Country resolution** — For each distinct security, the tool determines the ISO 3166-1 numeric country code of the primary exchange where it is listed:
   - First checks the local cache (`security-countries.json`).
   - If not cached, queries the [OpenFIGI API](https://www.openfigi.com/) (no API key required; rate-limited to 1 request/second).
   - Any security still unresolved is prompted interactively.
   - All resolved codes are saved to the cache for future runs.
3. **FIFO lot matching** — Buy lots are consumed in order (oldest first). Partial sells are supported. Sells outside the selected tax year still consume lots so that the FIFO state remains correct.
4. **Line generation** — For each sell in the tax year, one output line is produced per matched buy lot. The sell value, fees, and taxes are allocated proportionally: `lot shares ÷ total shares sold`.
5. **XML export** — The lines are written to an XML file ready to import into the IRS Modelo 3 application.

## Output: XML Format

The generated XML follows the schema expected by the AT (Autoridade Tributária) Modelo 3 import tool:

```xml
<AnexoJq092AT01>
    <AnexoJq092AT01-Linha numero="1">
        <NLinha>951</NLinha>
        <CodPais>276</CodPais>
        <Codigo>G01</Codigo>
        <AnoRealizacao>2025</AnoRealizacao>
        <MesRealizacao>11</MesRealizacao>
        <DiaRealizacao>14</DiaRealizacao>
        <ValorRealizacao>600.00</ValorRealizacao>
        <AnoAquisicao>2024</AnoAquisicao>
        <MesAquisicao>3</MesAquisicao>
        <DiaAquisicao>3</DiaAquisicao>
        <ValorAquisicao>400.00</ValorAquisicao>
        <DespesasEncargos>2.00</DespesasEncargos>
        <!-- ImpostoPagoNoEstrangeiro is only included when taxes > 0 -->
    </AnexoJq092AT01-Linha>
</AnexoJq092AT01>
```

- `NLinha` starts at **951** and increments for each line.
- `CodPais` is the ISO 3166-1 numeric country code.
- `DespesasEncargos` combines fees from both the buy and the sell, proportionally allocated.
- `ImpostoPagoNoEstrangeiro` is only emitted when foreign taxes were paid.

## Country Code Cache

Resolved country codes are stored in `security-countries.json` next to the CSV file (or at the path given by `--countries`). The file is a simple JSON object:

```json
{
  "Siemens AG": "276",
  "Apple Inc": "840"
}
```

You can edit this file manually to correct or pre-populate entries.

## Common ISO 3166-1 Numeric Codes

| Country | Code |
|---------|------|
| Germany | 276 |
| USA | 840 |
| UK | 826 |
| France | 250 |
| Netherlands | 528 |
| Switzerland | 756 |
| Sweden | 752 |
| Ireland | 372 |
| Portugal | 620 |
| Japan | 392 |

A full list is available at [iso.org](https://www.iso.org/iso-3166-country-codes.html) or [Wikipedia](https://en.wikipedia.org/wiki/ISO_3166-1_numeric).
