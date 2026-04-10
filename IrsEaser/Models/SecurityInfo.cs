namespace IrsEaser.Models;

public record SecurityInfo(
    string Name,
    string Symbol,
    string Isin,
    string SourceCountry
);
