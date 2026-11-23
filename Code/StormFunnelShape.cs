/// <summary>Visible condensation silhouettes, independent of the storm's game rating.</summary>
public enum StormFunnelShape
{
	Cone, VShape, Cylinder, Hourglass, Rope, Wedge
}

internal readonly record struct StormFunnelSections( Vector4 LowerRadii, float TopRadius )
{
	// Radii keyed at ground, quarter, half, three quarters, and cloud base.
	internal static StormFunnelSections For( StormFunnelShape shape ) => shape switch
	{
		StormFunnelShape.VShape => new( new( 58, 129, 213, 306 ), 403 ),
		StormFunnelShape.Wedge => new( new( 220, 242, 276, 325 ), 407 ),
		StormFunnelShape.Cylinder => new( new( 108, 117, 137, 201 ), 312 ),
		StormFunnelShape.Hourglass => new( new( 173, 113, 91, 145 ), 298 ),
		StormFunnelShape.Rope => new( new( 56, 61, 66, 82 ), 155 ),
		_ => new( new( 67, 97, 149, 227 ), 372 )
	};

	internal static StormFunnelSections WalkInWedge => new( new( 1550, 1700, 1880, 2100 ), 2470 );

	// Linear sampling is enough for the debris trajectories and feathered sprite fallback.
	internal float Sample( float h )
	{
		if ( h >= 1 ) return TopRadius;
		var k = h * 4;
		var a = k < 1 ? LowerRadii.x : k < 2 ? LowerRadii.y : k < 3 ? LowerRadii.z : LowerRadii.w;
		var b = k < 1 ? LowerRadii.y : k < 2 ? LowerRadii.z : k < 3 ? LowerRadii.w : TopRadius;
		return a + (b - a) * (k - (int)k);
	}
}
