namespace MySports.Importer.Models;

public sealed record Competition(string Id, string Sport, string Name, string ShortName)
{
    public static readonly Competition SerieA = new("serie-a", "football", "Serie A", "Serie A");
    public static readonly Competition ChampionsLeague = new("champions-league", "football", "Champions League", "Champions");
    public static readonly Competition F1 = new("f1", "motorsport", "Formula 1", "F1");
    public static readonly Competition MotoGP = new("motogp", "motorsport", "MotoGP", "MotoGP");
}
