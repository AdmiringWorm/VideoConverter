namespace VideoConverter.Core.Models
{
	public enum StereoScopicMode
	{
		None = 0,
		Mono,
		SideBySide,
		SideBySideLeft = SideBySide,
		SideBySideRight,
		SideBySideHalf,
		SideBySideLeftHalf = SideBySideHalf,
		SideBySideRightHalf,
		BottomTop,
		AboveBelowRight = BottomTop,
		TopBottom,
		AboveBelowLeft = TopBottom,
		TopBottomHalf,
		AboveBelowLeftHalf = TopBottomHalf,
		BottomTopHalf,
		AboveBelowRightHalf = BottomTopHalf,
	}
}
