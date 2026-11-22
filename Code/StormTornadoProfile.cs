/// <summary>Game presets inspired by F0–F5. Size is visual, not a real damage rating.</summary>
public readonly record struct StormTornadoProfile( float WidthScale, float FunnelHeight )
{
	public static StormTornadoProfile ForRating( int rating ) => rating switch
	{
		<= 0 => new( 1f, 3200f ),
		1 => new( 1.70f, 3480f ),
		2 => new( 2.25f, 3820f ),
		3 => new( 3.00f, 4150f ),
		4 => new( 3.85f, 4490f ),
		_ => new( 5.25f, 5280f )
	};

	public static StormTornadoProfile ForSpawn( int rating, float sizeRoll )
	{
		var typical = ForRating( rating );
		var halfRange = rating switch { <= 0 => .28f, 1 => .70f, 2 => .85f, 3 => 1f, 4 => 1.10f, _ => .95f };
		return typical with { WidthScale = typical.WidthScale + (System.Math.Clamp( sizeRoll, 0, 1 ) * 2 - 1) * halfRange };
	}

	// Half-open ranges over [0, 100): 45 / 27 / 15 / 8 / 4 / 1 percent.
	public static int RatingFromRoll( int roll ) => roll switch
	{
		< 45 => 0,
		< 72 => 1,
		< 87 => 2,
		< 95 => 3,
		< 99 => 4,
		_ => 5
	};
}
