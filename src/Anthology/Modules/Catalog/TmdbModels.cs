namespace Anthology.Modules.Catalog;

public sealed record TmdbPagedResult<T>(List<T> Results);

public sealed record TmdbMovie(
    int Id, string Title, string? Overview, string? Release_Date, string? Poster_Path);

public sealed record TmdbTvShow(
    int Id, string Name, string? Overview, string? First_Air_Date, string? Poster_Path,
    int Number_Of_Seasons, int Number_Of_Episodes);

public sealed record TmdbSeason(
    int Id, int Season_Number, List<TmdbEpisode> Episodes, string? Air_Date, string? Poster_Path);

public sealed record TmdbEpisode(
    int Id, string? Name, int Season_Number, int Episode_Number, string? Air_Date,
    string? Still_Path, string? Overview);
