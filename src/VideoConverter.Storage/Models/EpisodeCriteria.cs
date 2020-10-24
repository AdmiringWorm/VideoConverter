namespace VideoConverter.Storage.Models
{
    public sealed class EpisodeCriteria : VideoCriteria
    {
        public int? OldSeason { get; set; }
        public int? OldEpisode { get; set; }
        public int? NewSeason { get; set; }
        public int? NewEpisode { get; set; }
    }
}
