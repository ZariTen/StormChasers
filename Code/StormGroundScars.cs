using System;

/// <summary>Cosmetic ground marks persist across tornado replacements, then fade and return to a fixed pool.</summary>
internal sealed class StormGroundScars : Component
{
	// Covers five minutes of normal drift, including the first mark of each tornado.
	const int Capacity = 256;
	const float FadeSeconds = 20f;
	const float HoldSeconds = 300f - FadeSeconds;
	const float StepDistance = 108f;
	readonly Mark[] marks = new Mark[Capacity];
	StormTornadoFunnel tracked;
	Vector3 last;
	float nextTick;
	int sequence;

	struct Mark
	{
		internal Decal Decal;
		internal float Created;
		internal float Opacity;
	}

	internal void Follow( StormTornadoFunnel tornado, float time )
	{
		if ( time < nextTick ) return;
		nextTick = time + .15f;
		FadeMarks( time );
		if ( !tornado.IsValid() || tornado.ElapsedSample < 1.25f ) return;
		var position = tornado.WorldPosition;
		var first = tornado != tracked;
		var movement = (position - last).WithZ( 0 );
		if ( !first && movement.Length < StepDistance ) return;
		var heading = first || movement.Length > 480 ? tornado.WorldRotation.Forward.WithZ( 0 ).Normal : movement.Normal;
		if ( heading.LengthSquared < .1f ) return;
		tracked = tornado;
		last = position;
		var width = .83f + .35f * MathF.Sqrt( tornado.VisualProfile.WidthScale );
		var offset = Vector3.Cross( Vector3.Up, heading ) * (46 * width * MathF.Sin( sequence * 1.91f ));
		Stamp( position + offset, heading, tornado.Rating, width, time );
	}

	bool Stamp( Vector3 position, Vector3 heading, int rating, float width, float time )
	{
		var surface = Scene.Trace.Ray( position + Vector3.Up * 192, position + Vector3.Down * 112 )
			.WithoutTags( "player" ).Run();
		if ( !surface.Hit || !surface.GameObject.IsValid() || surface.Normal.z < .88f ) return false;
		var road = surface.GameObject.Tags.Has( "storm_road" );
		if ( !road && !surface.GameObject.Tags.Has( "storm_grass" ) && surface.GameObject.Name != "Flat Terrain" ) return false;

		var slot = 0;
		for ( var i = 0; i < marks.Length; i++ )
		{
			if ( !marks[i].Decal.IsValid() || !marks[i].Decal.GameObject.Enabled ) { slot = i; break; }
			if ( marks[i].Created < marks[slot].Created ) slot = i;
		}
		ref var mark = ref marks[slot];
		if ( !mark.Decal.IsValid() )
		{
			var obj = Scene.CreateObject( false );
			obj.NetworkMode = NetworkMode.Never;
			obj.Parent = GameObject;
			mark.Decal = obj.Components.Create<Decal>();
			mark.Decal.Rotation = 0;
			mark.Decal.LifeTime = 0;
			mark.Decal.Parallax = 0;
			mark.Decal.AttenuationAngle = .8f;
			// Asphalt is only one unit above the grass on this map. A shallow projection separates them.
			mark.Decal.Depth = .65f;
		}
		var decal = mark.Decal;
		var variation = StormSmokeTexture.Hash( sequence, 9, 514 );
		var tangent = (heading - surface.Normal * Vector3.Dot( heading, surface.Normal )).Normal;
		decal.GameObject.Name = road ? "Scuffed Asphalt" : "Scoured Grass";
		decal.WorldPosition = surface.HitPosition;
		decal.WorldRotation = Rotation.LookAt( -surface.Normal, tangent );
		var size = width * (.91f + variation * .15f);
		decal.Size = new Vector2( (road ? 240 : 295) * size, (road ? 322 : 374) * size );
		var definition = StormScarTextures.Get( road, sequence++ % 3 );
		if ( decal.Decals.Count == 0 ) decal.Decals.Add( definition );
		else decal.Decals[0] = definition;
		mark.Created = time;
		mark.Opacity = Math.Clamp( rating, 0, 5 ) * .043f + (road ? .48f : .63f);
		decal.ColorTint = Color.White.WithAlpha( mark.Opacity );
		decal.GameObject.Enabled = true;
		return true;
	}

	void FadeMarks( float time )
	{
		foreach ( ref var mark in marks.AsSpan() )
		{
			var decal = mark.Decal;
			if ( !decal.IsValid() || !decal.GameObject.Enabled ) continue;
			var age = time - mark.Created;
			if ( age < HoldSeconds ) continue;
			if ( age >= HoldSeconds + FadeSeconds ) decal.GameObject.Enabled = false;
			else decal.ColorTint = Color.White.WithAlpha( mark.Opacity * (1 - StormWeatherTextures.Smooth( (age - HoldSeconds) / FadeSeconds )) );
		}
	}
}
