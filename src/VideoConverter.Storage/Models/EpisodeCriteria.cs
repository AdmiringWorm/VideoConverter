namespace VideoConverter.Storage.Models
{
	public sealed class EpisodeCriteria : VideoCriteria
	{
		public int? NewEpisode { get; set; }
		public int? NewSeason { get; set; }
		public int? OldEpisode { get; set; }
		public int? OldSeason { get; set; }
	}
}
