namespace VideoConverter.Storage.Models
{
	using System.Collections.Generic;

	public class Criteria
	{
		public int Id { get; set; }

		public string Name { get; set; } = string.Empty;
	}

	public sealed class Criteria<TCriteria> : Criteria
		where TCriteria : VideoCriteria
	{
#pragma warning disable CA2227
		public List<TCriteria> Criterias { get; set; } = new List<TCriteria>();
	}
}
