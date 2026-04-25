using Newtonsoft.Json;

namespace P90Ez.MALApi.Structures
{
    class DefaultAPIResponse<T>
    {
        [JsonProperty("data")]
        public List<Wrapper> Data;

        public class Wrapper
        {
            [JsonProperty("node")]
            public T Node;
        }

        [JsonProperty("paging")]
        public Paging? Pagination { get; set; }
        public class Paging
        {
            [JsonProperty("next")]
            public string? Next { get; set; }
        }

        public string GetNextPageURI()
        {
            if (Pagination == null || Pagination.Next == null) return "";

            return Pagination.Next;
        }
    }

    class BaseAnimeDetails
    {
        [JsonProperty("id")]
        public ulong Id { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("main_picture")]
        public Picture MainPicture { get; set; }
        public class Picture
        {
            [JsonProperty("medium")]
            public string Medium { get; set; }

            [JsonProperty("large")]
            public string Large { get; set; }
        }
    }

    class AnimeDetails : BaseAnimeDetails
    {
        public class AlternativeTitles
        {
            [JsonProperty("synonyms")]
            public List<string> Synonyms { get; set; }

            [JsonProperty("en")]
            public string English { get; set; }

            [JsonProperty("ja")]
            public string Japanese { get; set; }
        }

        public class Broadcast
        {
            [JsonProperty("day_of_the_week")]
            public string DayOfTheWeek { get; set; }

            [JsonProperty("start_time")]
            public string StartTime { get; set; }
        }

        public class Genre
        {
            [JsonProperty("id")]
            public int Id { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }
        }

        public class MyListStatus
        {
            [JsonProperty("status")]
            public string Status { get; set; }

            [JsonProperty("score")]
            public int Score { get; set; }

            [JsonProperty("num_episodes_watched")]
            public int NumEpisodesWatched { get; set; }

            [JsonProperty("is_rewatching")]
            public bool IsRewatching { get; set; }

            [JsonProperty("updated_at")]
            public DateTime UpdatedAt { get; set; }

            [JsonProperty("start_date")]
            public DateOnly? StartedAt { get; set; }

            [JsonProperty("finish_date")]
            public DateOnly? FinishedAt { get; set; }
        }

        public class Recommendation
        {
            [JsonProperty("node")]
            public BaseAnimeDetails Node { get; set; }

            [JsonProperty("num_recommendations")]
            public int NumRecommendations { get; set; }
        }

        public class RelatedMedia
        {
            [JsonProperty("node")]
            public BaseAnimeDetails Node { get; set; }

            [JsonProperty("relation_type")]
            public string RelationType { get; set; }

            [JsonProperty("relation_type_formatted")]
            public string RelationTypeFormatted { get; set; }
        }

        [JsonProperty("alternative_titles")]
        public AlternativeTitles AltTitles { get; set; }

        [JsonProperty("start_date")]
        public DateOnly StartDate { get; set; }

        [JsonProperty("end_date")]
        public DateOnly EndDate { get; set; }

        [JsonProperty("synopsis")]
        public string Synopsis { get; set; }

        [JsonProperty("mean")]
        public double Mean { get; set; }

        [JsonProperty("rank")]
        public int Rank { get; set; }

        [JsonProperty("popularity")]
        public int Popularity { get; set; }

        [JsonProperty("num_list_users")]
        public int NumListUsers { get; set; }

        [JsonProperty("num_scoring_users")]
        public int NumScoringUsers { get; set; }

        [JsonProperty("nsfw")]
        public string Nsfw { get; set; }

        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [JsonProperty("media_type")]
        public string MediaType { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("genres")]
        public List<Genre> Genres { get; set; }

        [JsonProperty("my_list_status")]
        public MyListStatus WatchListStatus { get; set; }

        [JsonProperty("num_episodes")]
        public int NumEpisodes { get; set; }

        [JsonProperty("start_season")]
        public Season StartSeason { get; set; }

        [JsonProperty("broadcast")]
        public Broadcast BroadcastDetails { get; set; }

        [JsonProperty("source")]
        public string SourceMaterial { get; set; }

        [JsonProperty("average_episode_duration")]
        public double AverageEpisodeDuration { get; set; }

        [JsonProperty("rating")]
        public string Rating { get; set; }

        [JsonProperty("pictures")]
        public List<Picture> Pictures { get; set; }

        [JsonProperty("background")]
        public string Background { get; set; }

        [JsonProperty("related_anime")]
        public List<RelatedMedia> RelatedAnime { get; set; }

        [JsonProperty("related_manga")]
        public List<object> RelatedManga { get; set; }

        [JsonProperty("recommendations")]
        public List<Recommendation> Recommendations { get; set; }

        [JsonProperty("studios")]
        public List<Studio> Studios { get; set; }

        [JsonProperty("statistics")]
        public ListStatistics Statistics { get; set; }

        public class Season
        {
            [JsonProperty("year")]
            public int Year { get; set; }

            [JsonProperty("season")]
            public string SeasonName { get; set; }
        }

        public class ListStatistics
        {
            [JsonProperty("status")]
            public ListStatus Status { get; set; }

            [JsonProperty("num_list_users")]
            public int NumListUsers { get; set; }
        }

        public class ListStatus
        {
            [JsonProperty("watching")]
            public string Watching { get; set; }

            [JsonProperty("completed")]
            public string Completed { get; set; }

            [JsonProperty("on_hold")]
            public string OnHold { get; set; }

            [JsonProperty("dropped")]
            public string Dropped { get; set; }

            [JsonProperty("plan_to_watch")]
            public string PlanToWatch { get; set; }
        }

        public class Studio
        {
            [JsonProperty("id")]
            public int Id { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }
        }
    }
}