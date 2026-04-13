# IRS Easer

A command-line tool for Portuguese tax residents that generates ready-to-import XML files for **IRS Modelo 3 — Anexo J** from [Portfolio Performance](https://www.portfolio-performance.info/) exports.

Covers two sections of Anexo J:
- **Quadro 9.2-A** — capital gains from selling foreign securities
- **Quadro 8A** — dividend income from foreign securities

## Background

If you hold foreign securities, Portuguese tax law requires you to declare capital gains and dividend income in Anexo J of IRS Modelo 3. Filling this out by hand is painful: each sale must be split into one line per purchase lot (FIFO), and dividends must be grouped and totalled by source country. IRS Easer automates both.

## Installation

### Option A — Pre-built release (no .NET required)

1. Download the latest archive for your platform from the [Releases](../../releases/latest) page (`irs-easer-linux-x64.zip` or `irs-easer-win-x64.zip`).
2. Extract the archive. The `input/` folder with template files is included.
3. Run the executable:
   - **Linux/macOS:** `./IrsEaser`
   - **Windows:** `IrsEaser.exe`

### Option B — Run from source

**Requirements:** [.NET 10 SDK](https://dotnet.microsoft.com/download)

```
git clone <repo-url>
cd irs-easer
```

No build step needed — `dotnet run` compiles and runs in one go.

## Quick Start

1. Drop your CSV exports into the `input/` folder (see [Input Files](#input-files) below).
2. Run the tool:
   - **Pre-built release:** `./IrsEaser` (Linux/macOS) or `IrsEaser.exe` (Windows)
   - **From source:** `dotnet run --project IrsEaser`
3. An interactive menu appears. Choose what to generate, select your files, confirm the tax year. Done.

The tool defaults to the **previous calendar year**, which is almost always the right choice when filing.

## Input Files

All files go in the `input/` folder. Template files for each format are included in that folder.

### 1. Transactions CSV

Export from Portfolio Performance via **File → Export → Transactions (CSV)**. The tool reads these columns:

| Column                | Description                                                |
| --------------------- | ---------------------------------------------------------- |
| Date                  | Transaction date                                           |
| Type                  | `Buy`, `Sell`, or `Dividend` (all other types are ignored) |
| Security              | Security name (must match the securities file exactly)     |
| Shares                | Number of shares                                           |
| Quote                 | Price per share                                            |
| Amount                | Gross amount                                               |
| Fees                  | Brokerage fees                                             |
| Taxes                 | Taxes withheld at source                                   |
| Net Transaction Value | Net amount after fees and taxes                            |
| Account               | Portfolio account                                          |
| Offset Account        | Cash account                                               |
| Note                  | Free-text note                                             |
| Source                | Data source                                                |

> Both `,` and `;` delimiters and both `.` and `,` decimal separators are detected automatically.

### 2. Securities CSV

Export from Portfolio Performance via **File → Export → Securities (CSV)**. The tool reads:

| Column                   | Description                                              |
| ------------------------ | -------------------------------------------------------- |
| Name                     | Security name (must match the transactions file exactly) |
| Symbol                   | Ticker symbol                                            |
| ISIN                     | ISIN code (used for country lookup)                      |
| Source Country (Level 3) | Pre-filled country code, if set in Portfolio Performance |

> This file must use `;` as the delimiter, which is the Portfolio Performance default for securities exports.

### 3. Supplementary Dividends CSV *(optional)*

For dividend income received outside of Portfolio Performance — for example, interest from a savings account or a broker cash account. Each row is one income entry:

| Column      | Description                                        |
| ----------- | -------------------------------------------------- |
| Date        | Date of payment (used only for tax year filtering) |
| IncomeCode  | AT income code, e.g. `E21` for dividends           |
| CountryCode | ISO 3166-1 numeric code of the source country      |
| GrossAmount | Gross amount received                              |
| TaxPaid     | Tax withheld at source                             |

```csv
Date,IncomeCode,CountryCode,GrossAmount,TaxPaid
2025-03-15,E21,840,50.00,7.50
2025-06-20,E21,756,30.00,4.50
```

## How It Works

### Capital Gains (Quadro 9.2-A)

1. Loads and sorts all transactions chronologically. Only `Buy` and `Sell` rows are processed.
2. Resolves the source country for each security (see [Country Resolution](#country-resolution)).
3. Matches buy lots to sells using **FIFO** (oldest lots first). Partial sells are supported. Sales outside the selected tax year are still processed to keep the lot state correct.
4. For each sell in the tax year, generates one output line per matched buy lot. The sell value, fees, and taxes from both sides are split proportionally by `lot shares ÷ total shares sold`.
5. Writes `irs-quadro9-2a-<year>.xml`.

### Dividends (Quadro 8A)

1. Loads all transactions and filters to `Dividend` rows for the selected tax year.
2. Resolves the source country for each security that paid a dividend (see [Country Resolution](#country-resolution)).
3. Merges any supplementary dividend entries.
4. Groups all entries by **(country, income code)** and sums gross amounts and taxes withheld.
5. Writes `irs-quadro8a-<year>.xml`.

### Country Resolution

For each security, the tool determines the ISO 3166-1 numeric country code of its primary exchange:

1. Checks `input/security-countries.json` (the local cache).
2. Queries the [OpenFIGI API](https://www.openfigi.com/) by ISIN if not cached — no API key required, rate-limited to 1 request/second.
3. Prompts you to enter the code manually for anything still unresolved.
4. Saves all resolved codes back to the cache for future runs.

## Output

### Capital Gains — `irs-quadro9-2a-<year>.xml`

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
        <!-- ImpostoPagoNoEstrangeiro only appears when taxes > 0 -->
    </AnexoJq092AT01-Linha>
</AnexoJq092AT01>
```

- `NLinha` starts at **951**.
- `DespesasEncargos` is the proportional share of fees from both the buy and the sell.
- `ImpostoPagoNoEstrangeiro` only appears when foreign taxes were paid.

### Dividends — `irs-quadro8a-<year>.xml`

```xml
<AnexoJq08AT01>
    <AnexoJq08AT01-Linha numero="1">
        <NLinha>801</NLinha>
        <CodRendimento>E21</CodRendimento>
        <CodPais>840</CodPais>
        <RendimentoBruto>80.00</RendimentoBruto>
        <ImpostoPagoEstrangeiroPaisFonte>12.00</ImpostoPagoEstrangeiroPaisFonte>
        <!-- ImpostoPagoEstrangeiroPaisFonte only appears when taxes > 0 -->
    </AnexoJq08AT01-Linha>
</AnexoJq08AT01>
```

- `NLinha` starts at **801**.
- One line per **(country, income code)** pair — amounts from all securities in the same country are summed into a single line.

## Country Code Cache

`input/security-countries.json` stores previously resolved country codes so the OpenFIGI API is only called once per security:

```json
{
  "Siemens AG": "276",
  "Apple Inc": "840"
}
```

Edit this file freely to correct or pre-populate entries.

## Common ISO 3166-1 Numeric Codes

| Country     | Code |
| ----------- | ---- |
| Germany     | 276  |
| USA         | 840  |
| UK          | 826  |
| France      | 250  |
| Netherlands | 528  |
| Switzerland | 756  |
| Sweden      | 752  |
| Ireland     | 372  |
| Portugal    | 620  |
| Japan       | 392  |

Full list: [iso.org](https://www.iso.org/iso-3166-country-codes.html) or [Wikipedia](https://en.wikipedia.org/wiki/ISO_3166-1_numeric).
