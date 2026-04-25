using Microsoft.VisualBasic;
using Newtonsoft.Json;
using P90Ez.MALApi;
using P90Ez.MALApi.Structures;
namespace WatchlistParser;

class Program
{
    static int Main(string[] args)
    {
        string WatchlistOutputPath = args.Length >= 1 ? args[0] : "WatchlistProcessed.json";
        string CredentialsFilename = args.Length >= 2 ? args[1] : "Credentials.json";

        //attempt to read credentials from file
        AccessTokenHandler? MalTokenHandler = AccessTokenHandler.FromFile(CredentialsFilename);

        //manually enter credentials and create token
        if (MalTokenHandler == null)
        {
            Console.WriteLine("Failed to read credentials from file. Restart or enter MAL Api Client information below.");

            Console.Write("Client Id: ");
            string ClientId = Console.ReadLine() ?? "";

            Console.Write("Client Secret: ");
            string ClientSecret = Console.ReadLine() ?? "";

            Console.Write("Redirect URI (default: http://127.0.0.1:9876/): ");
            string RedirectURI = Console.ReadLine() ?? "";

            if (RedirectURI != string.Empty) MalTokenHandler = new AccessTokenHandler(ClientId, ClientSecret, RedirectURI, CredentialsFilename);
            else MalTokenHandler = new AccessTokenHandler(ClientId, ClientSecret, CredentialsFilename: CredentialsFilename);
        }

        MALUserAuth MALApi = new MALUserAuth(MalTokenHandler, true);

        Console.WriteLine("Requesting watchlist from MAL...");
        List<AnimeDetails> Watchlist = MALApi.GetUserWatchlist();

        OutputStructure OutputWatchlist = new OutputStructure();

        //sort shows into categories and only save necessary information
        Console.WriteLine("Parsing watchlist...");
        foreach (AnimeDetails Show in Watchlist)
        {
            OutputStructure.Entry OutputItem = new OutputStructure.Entry(Show);
            switch (Show.WatchListStatus.Status)
            {
                case "completed":
                    OutputWatchlist.Completed.Add(OutputItem);
                    break;
                case "dropped":
                    OutputWatchlist.Dropped.Add(OutputItem);
                    break;
                default:
                    OutputWatchlist.ToWatch.Add(OutputItem);
                    break;
            }
        }

        //sort by recent activity
        var Sorter = Comparer<OutputStructure.Entry>.Create((Left, Right) => Right.UpdatedAt.CompareTo(Left.UpdatedAt)); //recently watched/added first
        OutputWatchlist.Completed.Sort(Sorter);
        OutputWatchlist.Dropped.Sort(Sorter);
        OutputWatchlist.ToWatch.Sort(Sorter);

        Console.WriteLine("Writing watchlist to file...");
        return OutputWatchlist.ToFile(WatchlistOutputPath) ? 0 : -2;
    }
}

class OutputStructure : ParsableJsonStructure
{
    public class Entry
    {
        public Entry(AnimeDetails Info)
        {
            NameEnglish = Info.AltTitles.English;
            NameJapanese = Info.Title;
            MALId = Info.Id;
            Link = $"https://myanimelist.net/anime/{MALId}";
            ImageURL = Info.MainPicture.Medium;
            StartDate = Info.StartDate;
            Rating = Info.Mean;
            NumberOfEpisodes = Info.NumEpisodes;

            foreach (var Genre in Info.Genres)
            {
                Genres.Add(Genre.Name);
            }
            
            //date from last activity
            if(Info.WatchListStatus.FinishedAt != null)
            {
                UpdatedAt = (DateOnly)Info.WatchListStatus.FinishedAt;
            } else
            {
                if(Info.WatchListStatus.StartedAt != null)
                {
                    UpdatedAt = (DateOnly)Info.WatchListStatus.StartedAt;
                } else
                {
                    UpdatedAt = DateOnly.FromDateTime(Info.WatchListStatus.UpdatedAt);
                }
            }
        }

        [JsonProperty(PropertyName = "name_en")]
        public string NameEnglish = string.Empty;

        [JsonProperty(PropertyName = "name_jp")]
        public string NameJapanese = string.Empty;

        [JsonProperty(PropertyName = "mal_id")]
        public ulong MALId;

        [JsonProperty(PropertyName = "link")]
        public string Link = string.Empty;

        [JsonProperty(PropertyName = "image_url")]
        public string ImageURL = string.Empty;

        [JsonProperty("start_date")]
        public DateOnly StartDate;

        [JsonProperty("rating")]
        public double Rating;

        [JsonProperty("genres")]
        public List<string> Genres = [];

        [JsonProperty("num_episodes")]
        public int NumberOfEpisodes;
        
        [JsonIgnore]
        public DateOnly UpdatedAt { get; }
    }

    public DateTime LastUpdated = DateTime.Now;
    public List<Entry> ToWatch = [];
    public List<Entry> Completed = [];
    public List<Entry> Dropped = [];

    public bool ToFile(string Path) => ToFile(this, Path);
    public static OutputStructure? FromFile(string Path) => FromFile<OutputStructure>(Path);
}

abstract class ParsableJsonStructure
{
    /// <summary>
    /// Reads content from file at provided path and parses it to provided type.
    /// </summary>
    /// <typeparam name="T">Type to parse read json content to.</typeparam>
    /// <param name="Path">Path to read json content from.</param>
    /// <returns>T on success, null otherwise.</returns>
    protected static T? FromFile<T>(string Path) where T : ParsableJsonStructure //not pretty, but good enough for this simple use-case
    {
        if (!File.Exists(Path)) return default;

        try
        {
            string? Content = File.ReadAllText(Path);
            if (Content == null) return default;

            return JsonConvert.DeserializeObject<T>(Content);
        }
        catch { }

        return default;
    }

    /// <summary>
    /// Parses provided object to a json string and writes it to file at provided path.
    /// </summary>
    /// <typeparam name="T">Type to parse json content from.</typeparam>
    /// <param name="Object">Object to parse json content from.</param>
    /// <param name="Path">Path to write json content to.</param>
    /// <param name="Pretty">Human readable - formats output using indentation.</param>
    /// <returns>True on success, false otherwise.</returns>
    protected static bool ToFile<T>(T Object, string Path, bool Pretty = false) where T : ParsableJsonStructure
    {
        try
        {
            string Content = JsonConvert.SerializeObject(Object, Pretty ? Formatting.Indented : Formatting.None);
            File.WriteAllText(Path, Content);
            return true;
        }
        catch { return false; }
    }
}