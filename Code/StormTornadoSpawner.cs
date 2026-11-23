using System;

/// <summary>Places one tornado at a time on valid ground across the playable map.</summary>
public sealed class StormTornadoSpawner : Component
{
	[Property] public float SpawnDelay { get; set; } = 3f;
	[Property] public float Lifetime { get; set; } = 18f;

	GameObject tornado;
	GameObject weather;
	TimeUntil nextSpawn;
	TimeUntil expires;
	MeshComponent terrain;

	protected override void OnStart() => nextSpawn = SpawnDelay;

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying ) return;
		if ( !Application.IsDedicatedServer && !weather.IsValid() )
		{
			weather = Scene.CreateObject( false );
			weather.Name = "Map Weather";
			weather.NetworkMode = NetworkMode.Never;
			weather.Components.Create<StormWeather>();
			weather.Enabled = true;
		}
		if ( !Networking.IsHost || !Networking.IsActive ) return;

		if ( tornado.IsValid() )
		{
			if ( expires )
			{
				tornado.Destroy();
				tornado = null;
				nextSpawn = SpawnDelay;
			}
			return;
		}

		if ( !nextSpawn ) return;
		var random = Random.Shared;
		if ( !TryChooseSpawn( random, out var position, out var rotation ) )
		{
			nextSpawn = SpawnDelay;
			return;
		}
		tornado = Scene.CreateObject( false );
		tornado.Name = "Tornado";
		tornado.Tags.Add( "storm_tornado" );
		tornado.WorldPosition = position;
		tornado.WorldRotation = rotation;
		var funnel = tornado.Components.Create<StormTornadoFunnel>();
		funnel.Rating = StormTornadoProfile.RatingFromRoll( random.Next( 100 ) );
		funnel.SizeRoll = random.NextSingle();
		funnel.ShapeRoll = random.NextSingle();
		funnel.Lifetime = Lifetime;
		tornado.NetworkSpawn();
		expires = Lifetime;
	}

	bool TryChooseSpawn( Random random, out Vector3 position, out Rotation rotation )
	{
		position = default;
		rotation = Rotation.Identity;
		if ( !terrain.IsValid() )
			terrain = Scene.GetAllComponents<MeshComponent>().FirstOrDefault( mesh => mesh.GameObject.Name == "Flat Terrain" );
		if ( !terrain.IsValid() || terrain.Model is not { IsError: false } model ) return false;

		var bounds = model.Bounds.Transform( terrain.WorldTransform );
		var ground = terrain.GameObject;
		const float edge = 8f;
		var min = bounds.Mins + new Vector3( edge, edge, 0 );
		var max = bounds.Maxs - new Vector3( edge, edge, 0 );
		var drift = StormTornadoFunnel.DriftSpeed * (Lifetime + .25f);
		for ( var i = 0; i < 24; i++ )
		{
			var x = min.x + (max.x - min.x) * random.NextSingle();
			var y = min.y + (max.y - min.y) * random.NextSingle();
			var hit = Scene.Trace.Ray( new Vector3( x, y, bounds.Maxs.z + 2048 ), new Vector3( x, y, bounds.Mins.z - 128 ) )
				.WithoutTags( "player" ).Run();
			if ( !hit.Hit || !hit.GameObject.IsValid() || hit.Normal.z < .88f ||
				(hit.GameObject != ground && !hit.GameObject.Tags.Has( "storm_road" )) ) continue;

			// Re-roll the heading, not the origin, so edge and corner spawns aren't penalized for pointing out of bounds.
			var yaw = random.NextSingle() * 360f;
			for ( var j = 0; j < 12; j++ )
			{
				var heading = Rotation.FromYaw( yaw + j * 30f );
				var end = hit.HitPosition + heading.Forward * drift;
				if ( end.x < min.x || end.x > max.x || end.y < min.y || end.y > max.y ) continue;
				position = hit.HitPosition;
				rotation = heading;
				return true;
			}
		}
		return false;
	}

	protected override void OnDisabled()
	{
		if ( tornado.IsValid() ) tornado.Destroy();
		if ( weather.IsValid() ) weather.Destroy();
	}
}
