using System;

/// <summary>Host-only plan for one tornado. Clients receive the current stage, not future upgrades.</summary>
internal sealed record StormTornadoEvolution( float Lifetime, StormTornadoEvolution.Stage[] Stages )
{
	internal readonly record struct Stage( int Rating, float At );

	internal static StormTornadoEvolution Roll( Random random )
	{
		var lifetime = 120f + 60f * random.NextSingle();
		// An F0 is a starting stage, not a peak: keep the existing odds for F2 and above.
		var peak = Math.Max( 1, StormTornadoProfile.RatingFromRoll( random.Next( 100 ) ) );
		var rating = random.Next( peak );
		var ratings = new List<int> { rating };
		while ( rating < peak )
		{
			rating = peak - rating == 1 || random.NextSingle() < .65f
				? rating + 1 : random.Next( rating + 2, peak + 1 );
			ratings.Add( rating );
		}

		var stages = new Stage[ratings.Count];
		stages[0] = new( ratings[0], 0 );
		var time = 0f;
		for ( var i = 1; i < stages.Length; i++ )
		{
			// Always leave at least 20 seconds at peak, even with five upgrades on a short track.
			var maxWait = MathF.Min( 30f, lifetime - 20f - time - 5f * (stages.Length - 1 - i) );
			time += 5f + (maxWait - 5f) * random.NextSingle();
			stages[i] = new( ratings[i], time );
		}
		return new( lifetime, stages );
	}
}
