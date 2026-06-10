namespace Anthology.Modules.Catalog;

public sealed record TmdbPagedResult<T>(List<T> Results, int Total_Pages = 1, int Total_Results = 0);

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

public sealed record TmdbGenre(int Id, string Name);
public sealed record TmdbKeyword(int Id, string Name);
public sealed record TmdbKeywordsResponse(List<TmdbKeyword> Keywords);
public sealed record TmdbCastMember(int Id, string Name, string? Known_For_Department, int Order);
public sealed record TmdbCrewMember(int Id, string Name, string Job, string Department);
public sealed record TmdbCreditsResponse(List<TmdbCastMember> Cast, List<TmdbCrewMember> Crew);

public sealed record TmdbMovieDetail(
    int Id, string Title, string? Overview, string? Release_Date, string? Poster_Path,
    double Popularity, double Vote_Average,
    List<TmdbGenre> Genres, TmdbKeywordsResponse Keywords, TmdbCreditsResponse Credits);

public sealed record TmdbTvShowDetail(
    int Id, string Name, string? Overview, string? First_Air_Date, string? Poster_Path,
    int Number_Of_Seasons, int Number_Of_Episodes,
    double Popularity, double Vote_Average,
    List<TmdbGenre> Genres);
