using System;

/// <summary>A host-only track of tangent-connected arcs with a continuous travel speed.</summary>
internal sealed class StormTornadoRoute
{
	readonly record struct Arc( Vector3 Start, float Yaw, float Turn, float From, float Until, float Length )
	{
		internal Vector3 PositionAt( float fraction )
		{
			if ( MathF.Abs( Turn ) < .001f )
				return Start + new Vector3( MathF.Cos( Yaw ), MathF.Sin( Yaw ), 0 ) * (Length * fraction);
			var angle = Yaw + Turn * fraction;
			return Start + new Vector3( MathF.Sin( angle ) - MathF.Sin( Yaw ), MathF.Cos( Yaw ) - MathF.Cos( angle ), 0 ) * (Length / Turn);
		}

		internal bool Fits( Vector3 min, Vector3 max )
		{
			bool Inside( Vector3 p ) => p.x >= min.x && p.x <= max.x && p.y >= min.y && p.y <= max.y;
			if ( !Inside( PositionAt( 1 ) ) ) return false;
			// The only interior XY extrema occur where the heading is a multiple of 90 degrees.
			if ( MathF.Abs( Turn ) < .001f ) return true;
			var sign = MathF.Sign( Turn );
			for ( var i = 0; i < 4; i++ )
			{
				var angle = Wrap( sign * (i * MathF.PI * .5f - Yaw) );
				if ( angle < MathF.Abs( Turn ) && !Inside( PositionAt( angle / MathF.Abs( Turn ) ) ) ) return false;
			}
			return true;
		}
	}

	readonly record struct Pace( float Cruise, float Swing, float Frequency, float Phase )
	{
		internal float SpeedAt( float time ) => Cruise + Swing * MathF.Sin( Frequency * time + Phase );
		internal float Distance( float from, float until ) => Cruise * (until - from)
			+ Swing / Frequency * (MathF.Cos( Frequency * from + Phase ) - MathF.Cos( Frequency * until + Phase ));
	}

	readonly Pace pace;
	readonly Arc[] arcs;

	StormTornadoRoute( Pace pace, Arc[] arcs )
	{
		this.pace = pace;
		this.arcs = arcs;
	}

	internal static StormTornadoRoute Plan( Random random, Vector3 origin, Vector3 min, Vector3 max, float lifetime )
	{
		var pace = new Pace( Roll( random, 43, 74 ), Roll( random, 10, 16 ), MathF.Tau / Roll( random, 28, 55 ), Roll( random, 0, MathF.Tau ) );
		var loopAfter = random.NextSingle() < .42f ? Roll( random, 24, 52 ) : float.PositiveInfinity;
		var loopDuration = Roll( random, 34, 48 );
		var loopSign = random.Next( 2 ) == 0 ? -1 : 1;
		for ( var attempt = 0; attempt < 10; ++attempt )
		{
			var arcs = new List<Arc>();
			var point = origin;
			var yaw = Roll( random, 0, MathF.Tau );
			var now = 0f;
			var looped = false;
			while ( now < lifetime )
			{
				if ( !looped && now >= loopAfter && now + loopDuration + 12 < lifetime )
				{
					var until = now + loopDuration;
					var loop = new Arc( point, yaw, loopSign * MathF.Tau, now, until, pace.Distance( now, until ) );
					if ( !loop.Fits( min, max ) ) loop = loop with { Turn = -loop.Turn };
					if ( loop.Fits( min, max ) )
					{
						arcs.Add( loop );
						looped = true;
						now = until;
						continue; // A full revolution returns to the same point and heading.
					}
				}

				var next = now + Roll( random, 12, 23 );
				var end = next + 2f >= lifetime ? lifetime : next;
				var duration = end - now;
				var length = pace.Distance( now, end );
				var limit = MathF.Min( 2.2f, duration * .13f );
				var turn = Roll( random, .22f, .76f ) * limit * (random.Next( 2 ) == 0 ? -1 : 1);
				var arc = new Arc( point, yaw, turn, now, end, length );
				for ( var alternative = 0; alternative < 6 && !arc.Fits( min, max ); ++alternative )
				{
					// Try both sides, including progressively tighter bends, rather than snapping the heading.
					var magnitude = alternative == 0 ? MathF.Abs( turn ) : limit * (alternative < 3 ? .82f : alternative < 5 ? 1f : .12f);
					arc = arc with { Turn = magnitude * (alternative % 2 == 0 ? -MathF.Sign( turn ) : MathF.Sign( turn )) };
				}
				if ( !arc.Fits( min, max ) ) break;
				arcs.Add( arc );
				point = arc.PositionAt( 1 );
				yaw = Wrap( yaw + arc.Turn );
				now = end;
			}
			if ( now >= lifetime ) return new( pace, arcs.ToArray() );
		}
		return null;
	}

	internal (Vector3 Position, Rotation Rotation, float Speed) Sample( float seconds )
	{
		var time = Math.Clamp( seconds, 0, arcs[^1].Until );
		var index = 0;
		while ( index < arcs.Length - 1 && time >= arcs[index].Until ) ++index;
		var arc = arcs[index];
		var fraction = Math.Clamp( pace.Distance( arc.From, time ) / arc.Length, 0, 1 );
		return (arc.PositionAt( fraction ), Rotation.FromYaw( (arc.Yaw + arc.Turn * fraction) * (180f / MathF.PI) ), pace.SpeedAt( time ));
	}

	static float Wrap( float angle ) => angle - MathF.Tau * MathF.Floor( angle / MathF.Tau );
	static float Roll( Random random, float min, float max ) => min + (max - min) * random.NextSingle();
}
