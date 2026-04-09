# Description

This application facilitates filling out IRS Modelo 3. This is the Portuguese tax return form for individuals.
The application is focused on Anexo J, which is for Rendimentos no Estrangeiro (Income from Abroad), namely Quadro 9.2-A (Alienação Onerosa de Partes Sociais e Outros Valores Mobiliários [art.º 10.º, n.º 1, al. b), do CIRS]).

To this end, the user shall load a CSV file with all transactions, with the following columns:
- Date	
- Type	
- Security	
- Shares	
- Quote	
- Amount	
- Fees	
- Taxes	
- Net Transaction Value
- Account	
- Offset Account	
- Note	
- Source

The application must create a distinct list of securities from the transactions, and determine the **Source Country** for each security — the country of the primary exchange where the security is listed. This information is represented as an **ISO 3166-1 numeric code** (e.g. 276 for Germany, 840 for the USA) and must be obtained from online sources, such as financial APIs or databases. The application must also allow the user to manually input the Source Country for any security that cannot be automatically identified. Resolved country codes are cached locally so they do not need to be looked up again on subsequent runs.

This is taken from Portfolio Performance, which is a free and open-source software for tracking investments. The user can export the transactions from Portfolio Performance in CSV format, and then load it into this application.

The user shall also be able to specify the tax year for which they want to fill out the form, and the application will filter the transactions accordingly. The application shall always assume the previous year as the default tax year, since the user will most likely be filling out the form for the previous year.

The application must then:
1. Load the transactions from the CSV file to memory, using adequate data structures.
2. Arrange the transactions into trades, which are defined as a buy transaction followed by a sell transaction for the same security. Only **Buy** and **Sell** transaction types are processed; dividends, transfers, and all other types are ignored. Lot matching uses **FIFO (First In, First Out)**: the oldest buy lots are consumed first. The application must be able to handle multiple trades for the same security, and also partial sells (i.e., selling only a portion of the shares bought in a previous transaction).
3. Calculate the capital gains or losses for each trade, taking into account the fees and taxes associated with each transaction. The application must also be able to handle the case where the user has multiple trades for the same security, and calculate the total capital gains or losses for that security.
4. For the selected tax year, generate a report that can be used to fill out the IRS Modelo 3, specifically the Anexo J, Quadro 9.2-A. For each trade which was executed/sold during the tax year, generate as many lines as there are buy lots matched to that sell, irrespective of the tax year in which the buy transaction occurred. Each line should include the following information:
- Line number (from 951 to infinity, depending on the number of trades)
- Source Country
- Operation Code (always G01)
- Year of the sell transaction
- Month of the sell transaction
- Day of the sell transaction
- Value of the sell transaction (Net)
- Year of the buy transaction
- Month of the buy transaction
- Day of the buy transaction
- Value of the buy transaction (Net)
- Fees associated with both the sell and buy transactions, split proportionally by lot share (lot shares ÷ total shares sold)
- Taxes paid for both the sell and buy transactions, split by the same proportion

The sell value assigned to each lot line = total net sell value × (lot shares ÷ total shares sold). The same proportion applies to fees and taxes from each respective transaction.

These lines shall be exported in a special XML format, which can be imported into the IRS Modelo 3 software. The application must ensure that the generated XML file adheres to the required schema for the IRS Modelo 3 import. An example of the XML structure for a single line might look like this:

```xml
<AnexoJq092AT01>
    <AnexoJq092AT01-Linha numero="1">
        <NLinha>951</NLinha>
        <CodPais>276</CodPais>
        <Codigo>G01</Codigo>
        <AnoRealizacao>2024</AnoRealizacao>
        <MesRealizacao>11</MesRealizacao>
        <DiaRealizacao>14</DiaRealizacao>
        <ValorRealizacao>259.20</ValorRealizacao>
        <AnoAquisicao>2024</AnoAquisicao>
        <MesAquisicao>3</MesAquisicao>
        <DiaAquisicao>3</DiaAquisicao>
        <ValorAquisicao>319.46</ValorAquisicao>
        <DespesasEncargos>2.00</DespesasEncargos>
        <!-- ImpostoPagoNoEstrangeiro is included only when taxes > 0 -->
    </AnexoJq092AT01-Linha>
    <AnexoJq092AT01-Linha numero="2">
        <NLinha>952</NLinha>
        <CodPais>276</CodPais>
        <Codigo>G01</Codigo>
        <AnoRealizacao>2024</AnoRealizacao>
        <MesRealizacao>10</MesRealizacao>
        <DiaRealizacao>21</DiaRealizacao>
        <ValorRealizacao>483.40</ValorRealizacao>
        <AnoAquisicao>2024</AnoAquisicao>
        <MesAquisicao>7</MesAquisicao>
        <DiaAquisicao>9</DiaAquisicao>
        <ValorAquisicao>481.96</ValorAquisicao>
        <DespesasEncargos>2.00</DespesasEncargos>
        <ImpostoPagoNoEstrangeiro>1.50</ImpostoPagoNoEstrangeiro>
    </AnexoJq092AT01-Linha>
</AnexoJq092AT01>
```

## Further details

Regarding the generation of lines, here's an example:

Imagine you sold 100 shares of a company (for example, Siemens — traded in Germany) on 2024-11-14 for a total of €1,000. You bought those 100 shares in two lots:
- Lot A: 60 shares purchased on 2024-03-03 for €400.
- Lot B: 40 shares purchased on 2024-07-09 for €300.

To fill out Table 9.2A, create one line per purchase (lot). Example:

| Line | Sale Date  | Sale Value | Purchase Date | Purchase Value |
| ---- | ---------- | ---------: | ------------: | -------------: |
| 1    | 2024-11-14 | €600 (60%) |    2024-03-03 |           €400 |
| 2    | 2024-11-14 | €400 (40%) |    2024-07-09 |           €300 |

Each line corresponds to the portion of the sale attributable to that specific purchase lot.