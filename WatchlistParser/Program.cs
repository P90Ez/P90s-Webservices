using Newtonsoft.Json;
namespace WatchlistParser;

class Program
{
    static int Main(string[] args)
    {
        //read raw watchlist
        string WatchlistRawPath = args.Length >= 1 ? args[0] : "WatchlistRaw.json";

        InputStructure? InputWatchlist = InputStructure.FromFile(WatchlistRawPath);
        if (InputWatchlist == null)
        {
            Console.WriteLine($"Failed to parse input list! (File: {WatchlistRawPath})");
            return -1;
        }

        MALApi MAL = new MALApi("Credentials.json");
        OutputStructure OutputWatchlist = new OutputStructure();

        //process watchlist
        OutputWatchlist.Completed = ProcessSection(MAL, InputWatchlist.Completed);
        OutputWatchlist.Dropped = ProcessSection(MAL, InputWatchlist.Dropped);
        OutputWatchlist.ToWatch = ProcessSection(MAL, InputWatchlist.Watching);
        OutputWatchlist.ToWatch.AddRange(ProcessSection(MAL, InputWatchlist.PlanToWatch));

        //write processed watchlist
        string WatchlistOutputPath = args.Length >= 2 ? args[1] : "WatchlistProcessed.json";
        return OutputWatchlist.ToFile(WatchlistOutputPath) ? 0 : -2;
    }
    
    static List<OutputStructure.OutputEntry> ProcessSection(MALApi MAL, List<InputStructure.InputEntry> WatchlistSection)
    {
        List<OutputStructure.OutputEntry> Output = [];

        foreach (var Entry in WatchlistSection)
        {
            var OutputEntry = MAL.CreateEntryFromId(Entry.MALId);
            if (OutputEntry == null)
            {
                Console.WriteLine($"Failed to process entry {Entry.MALId} \"{Entry.Name}\"!");
                continue;
            }

            OutputEntry.Link = Entry.Link; //manually assign link to MAL page - is not included in API endpoint as link is only dependent on MALId
            Output.Add(OutputEntry);
        }

        return Output;
    }
}

class MALApi
{
    static readonly string BaseURL = "https://api.myanimelist.net/v2";
    Credentials Creds;
    HttpClient Client;
    public MALApi(string CredentialsPath)
    {
        Credentials? ParsedCreds = Credentials.FromFile(CredentialsPath);
        if (ParsedCreds == null) throw new Exception("Failed to read and parse MAL credentials from provided path!");

        Creds = ParsedCreds;
        Client = new HttpClient();
        Client.DefaultRequestHeaders.Add("X-MAL-CLIENT-ID", Creds.ClientId);
    }

    /// <summary>
    /// Requests anime details from MAL API.
    /// </summary>
    /// <param name="MALId">MAL Id of anime which to request details on.</param>
    /// <returns>Filled OutputEntry on success, false otherwise.</returns>
    public OutputStructure.OutputEntry? CreateEntryFromId(ulong MALId)
    {
        try
        {
            string Result = Client.GetStringAsync(BaseURL + $"/anime/{MALId}?fields=title,main_picture,alternative_titles").Result;

            AnimeDetails? Details = JsonConvert.DeserializeObject<AnimeDetails>(Result);
            if (Details == null) return null;

            return new OutputStructure.OutputEntry(MALId)
            {
                NameEnglish = Details.Titles.English,
                NameJapanese = Details.MainTitle,
                ImageURL = Details.Images.Medium,
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to create entry for id {MALId}:\n" + ex.Message);
        }

        return null;
    }

    private class AnimeDetails
    {
        public struct AlternativeTitles
        {
            [JsonProperty("en")]
            public string English { get; set; }

            [JsonProperty("ja")]
            public string Japanese { get; set; }
        }

        public struct MainPicture
        {
            [JsonProperty("medium")]
            public string Medium { get; set; }

            [JsonProperty("large")]
            public string Large { get; set; }
        }

        [JsonProperty("id")]
        public int? MALId { get; set; }

        [JsonProperty("title")]
        public string MainTitle { get; set; } = string.Empty;

        [JsonProperty("main_picture")]
        public MainPicture Images { get; set; }

        [JsonProperty("alternative_titles")]
        public AlternativeTitles Titles { get; set; }
    }

    private class Credentials : ParsableJsonStructure
    {
        [JsonProperty(PropertyName = "MALClientId")]
        public string ClientId = string.Empty;

        /*[JsonProperty(PropertyName = "MALClientSecret")]
        public string ClientSecret = string.Empty;*/ //not required for used API endpoint

        public static Credentials? FromFile(string Path) => FromFile<Credentials>(Path);
    }
}

class InputStructure : ParsableJsonStructure
{
    public struct InputEntry
    {
        [JsonProperty(PropertyName = "link")]
        public string Link;
        [JsonProperty(PropertyName = "name")]
        public string Name;
        [JsonProperty(PropertyName = "mal_id")]
        public ulong MALId;
        [JsonProperty(PropertyName = "watchListType")]
        public int ListType;
    }

    public List<InputEntry> Watching = [];
    public List<InputEntry> Completed = [];
    [JsonProperty(PropertyName = "Plan to Watch")]
    public List<InputEntry> PlanToWatch = [];
    public List<InputEntry> Dropped = [];

    public static InputStructure? FromFile(string Path) => FromFile<InputStructure>(Path);
}

class OutputStructure : ParsableJsonStructure
{
    public class OutputEntry
    {
        public OutputEntry(ulong MALId)
        {
            this.MALId = MALId;
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
    }

    public DateTime LastUpdated = DateTime.Now;
    public List<OutputEntry> ToWatch = [];
    public List<OutputEntry> Completed = [];
    public List<OutputEntry> Dropped = [];

    public bool ToFile(string Path) => ToFile(this, Path);
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