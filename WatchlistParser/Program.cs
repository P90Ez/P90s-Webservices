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

        //load previous output if exists
        string WatchlistOutputPath = args.Length >= 2 ? args[1] : "WatchlistProcessed.json";
        OutputStructure? PreviousOutput = OutputStructure.FromFile(WatchlistOutputPath);

        List<OutputStructure.Entry> ParsedDataPool = [];
        if(PreviousOutput != null)
        {
            ParsedDataPool.AddRange(PreviousOutput.Completed);
            ParsedDataPool.AddRange(PreviousOutput.Dropped);
            ParsedDataPool.AddRange(PreviousOutput.ToWatch);
        }

        MALApi MAL = new MALApi("Credentials.json");
        OutputStructure OutputWatchlist = new OutputStructure();

        //process watchlist
        OutputWatchlist.Completed = ProcessSection(MAL, ParsedDataPool, InputWatchlist.Completed);
        OutputWatchlist.Dropped = ProcessSection(MAL, ParsedDataPool, InputWatchlist.Dropped);
        OutputWatchlist.ToWatch = ProcessSection(MAL, ParsedDataPool, InputWatchlist.Watching);
        OutputWatchlist.ToWatch.AddRange(ProcessSection(MAL, ParsedDataPool, InputWatchlist.PlanToWatch));

        //write processed watchlist
        return OutputWatchlist.ToFile(WatchlistOutputPath) ? 0 : -2;
    }
    
    static List<OutputStructure.Entry> ProcessSection(MALApi MAL, List<OutputStructure.Entry> ParsedDataPool, List<InputStructure.Entry> WatchlistSection)
    {
        List<OutputStructure.Entry> Output = [];

        foreach (var InputEntry in WatchlistSection)
        {
            OutputStructure.Entry? OutputEntry = ParsedDataPool.Where(x => x.MALId == InputEntry.MALId).FirstOrDefault();

            if (OutputEntry == null) //only request data from MAL if item is new on watchlist
            {
                OutputEntry = MAL.CreateEntryFromId(InputEntry.MALId);
                if (OutputEntry == null)
                {
                    Console.WriteLine($"Failed to process entry {InputEntry.MALId} \"{InputEntry.Name}\"!");
                    continue;
                }

                OutputEntry.Link = InputEntry.Link; //manually assign link to MAL page - is not included in API endpoint as link is only dependent on MALId
            }

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
    public OutputStructure.Entry? CreateEntryFromId(ulong MALId)
    {
        const int MaxRetries = 3;
        OutputStructure.Entry? Output = null;

        RetryCatch(
            () => {
                string Result = Client.GetStringAsync(BaseURL + $"/anime/{MALId}?fields=title,main_picture,alternative_titles").Result;

                AnimeDetails? Details = JsonConvert.DeserializeObject<AnimeDetails>(Result);
                if (Details == null) return false;

                Output = new OutputStructure.Entry(MALId)
                {
                    NameEnglish = Details.Titles.English,
                    NameJapanese = Details.MainTitle,
                    ImageURL = Details.Images.Medium,
                };
                return true;
            },
            (Exception ex) =>
            {
                Console.WriteLine($"Failed to create entry for id {MALId}:\n" + ex.Message);
            },
            MaxRetries);

        return Output;
    }

    /// <summary>
    /// Utilizes Try-Catch to retry a function for a given number of times. On the final catch, the provided Catch function is called.
    /// </summary>
    /// <param name="Try">Code to try a provided number of times.</param>
    /// <param name="Catch">Catch function to be called on the final catch.</param>
    /// <param name="MaxRetries">Number of retries.</param>
    /// <param name="Delay">Delay between retries.</param>
    /// <returns>True if provided function was successful, false of max retries are reached without success.</returns>
    private static bool RetryCatch(Func<bool> Try, Action<Exception> Catch, int MaxRetries, TimeSpan? Delay = null)
    {
        int TryCycle = 0;
        bool Success = false;

        while (!Success && TryCycle < MaxRetries)
        {
            TryCycle++;
            try
            {
                Success = Try();
            }
            catch (Exception ex)
            {
                if (TryCycle >= MaxRetries) Catch(ex);
                else if (Delay != null) Thread.Sleep((TimeSpan)Delay);
            }
        }

        return Success;
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
    public struct Entry
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

    public List<Entry> Watching = [];
    public List<Entry> Completed = [];
    [JsonProperty(PropertyName = "Plan to Watch")]
    public List<Entry> PlanToWatch = [];
    public List<Entry> Dropped = [];

    public static InputStructure? FromFile(string Path) => FromFile<InputStructure>(Path);
}

class OutputStructure : ParsableJsonStructure
{
    public class Entry
    {
        public Entry(ulong MALId)
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