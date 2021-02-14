namespace VideoConverter.Storage.Models
{
	public abstract class VideoCriteria
	{
		public int Id { get; set; }
		public string? NewName { get; set; }
	}
}
