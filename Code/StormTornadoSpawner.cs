/// <summary>Places one tornado at a time at the child markers in the current map.</summary>
public sealed class StormTornadoSpawner : Component
{
	[Property] public float SpawnDelay { get; set; } = 3f;
	[Property] public float Lifetime { get; set; } = 18f;

	GameObject tornado;
	GameObject weather;
	TimeUntil nextSpawn;
	TimeUntil expires;
	int nextMarker;

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

		if ( !nextSpawn || GameObject.Children.Count == 0 ) return;
		var marker = GameObject.Children[nextMarker++ % GameObject.Children.Count];
		tornado = Scene.CreateObject( false );
		tornado.Name = "Tornado";
		tornado.Tags.Add( "storm_tornado" );
		tornado.WorldPosition = marker.WorldPosition;
		tornado.WorldRotation = marker.WorldRotation;
		var funnel = tornado.Components.Create<StormTornadoFunnel>();
		funnel.Rating = StormTornadoProfile.RatingFromRoll( System.Random.Shared.Next( 100 ) );
		funnel.SizeRoll = System.Random.Shared.NextSingle();
		tornado.NetworkSpawn();
		expires = Lifetime;
	}

	protected override void OnDisabled()
	{
		if ( tornado.IsValid() ) tornado.Destroy();
		if ( weather.IsValid() ) weather.Destroy();
	}
}
