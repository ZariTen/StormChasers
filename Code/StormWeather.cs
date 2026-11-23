using System;

/// <summary>Map-wide rain and fog intensify under an active storm, with a heavier band near the funnel.</summary>
public sealed class StormWeather : Component
{
	const int DropCount = 1800;
	const int NearDropCount = 1330;
	const float BackgroundRain = .72f;
	const float RainBandRadius = 4000f;
	static Sprite[] denseCloudMaps;
	static Sprite eyeShelf;
	static Sprite rainStreak;

	readonly SpriteRenderer[] deck = new SpriteRenderer[2];
	readonly SpriteRenderer[] shelf = new SpriteRenderer[3];
	readonly VolumetricFogVolume[] rainHaze = new VolumetricFogVolume[3];
	SpriteRenderer[] drops = Array.Empty<SpriteRenderer>();
	GameObject rainPool;
	StormTornadoFunnel tornado;
	Fog backgroundFog;
	StormTornadoProfile stormProfile = StormTornadoProfile.ForRating( 0 );
	float openCoreRadius;
	float severity;
	bool integratedCloud;
	Vector3 rainBearing = Vector3.Forward;
	float windTravel;
	float windTravelTime;
	TimeUntil nextSearch;
	TimeUntil nextShelterProbe;
	TimeSince age;
	bool sheltered;

	public Vector3 StormCenter { get; private set; }
	public float StormStrength { get; private set; }

	protected override void OnStart()
	{
		age = 0;
		var maps = DenseCloudMaps();
		var overcast = CreateObject( GameObject, "Overcast Deck" );
		for ( var i = 0; i < deck.Length; i++ )
			deck[i] = CloudLayer( overcast, "Overcast Stratum", maps[i] );
		var stormShelf = CreateObject( GameObject, "Storm Shelf" );
		for ( var i = 0; i < shelf.Length; i++ )
			shelf[i] = CloudLayer( stormShelf, "Rain-bearing Shelf", maps[i + deck.Length] );

		var haze = CreateObject( GameObject, "Rain Haze" );
		backgroundFog = CreateObject( GameObject, "Map Rain Fog" ).Components.Create<Fog>();
		backgroundFog.FogEnabled = true;
		backgroundFog.DistanceEnabled = true;
		backgroundFog.Mode = Sandbox.Rendering.FogMode.Distance;
		backgroundFog.ColorSource = FogColorSource.Constant;
		backgroundFog.Color = new Color( .26f, .30f, .35f );
		backgroundFog.SkyAffect = .08f;
		backgroundFog.DistanceRange = new Vector2( 850, 10300 );
		backgroundFog.MaximumOpacity = .21f;
		for ( var i = 0; i < rainHaze.Length; i++ )
		{
			var fog = CreateObject( haze, "Precipitation Volume" ).Components.Create<VolumetricFogVolume>();
			fog.Color = new Color( .32f, .37f, .43f );
			fog.FalloffExponent = 1.6f;
			fog.Strength = 0;
			rainHaze[i] = fog;
		}
		EnsureRainPool();
		UpdateClouds( 0 );
	}

	static Sprite[] DenseCloudMaps() => denseCloudMaps ??= new[]
	{
		StormWeatherTextures.CloudSheet( 83, false, blanket: true ), StormWeatherTextures.CloudSheet( 219, false, blanket: true ),
		StormWeatherTextures.CloudSheet( 312, true ), StormWeatherTextures.CloudSheet( 901, true, blanket: true ),
		StormWeatherTextures.CloudSheet( 666, false, blanket: true )
	};

	void EnsureRainPool()
	{
		if ( drops?.Length == DropCount && rainPool.IsValid() && rainStreak is not null ) return;
		rainStreak ??= StormWeatherTextures.RainDrop();
		if ( !rainPool.IsValid() )
		{
			rainPool = GameObject.Children.FirstOrDefault( o => o.Name == "Nearby Rain" );
			if ( rainPool.IsValid() )
				drops = rainPool.Children.SelectMany( o => o.Components.GetAll<SpriteRenderer>( FindMode.EverythingInSelf ) ).ToArray();
			else
			{
				rainPool = CreateObject( GameObject, "Nearby Rain" );
				drops = Array.Empty<SpriteRenderer>();
			}
		}
		drops ??= Array.Empty<SpriteRenderer>();
		var previousCount = drops.Length;
		Array.Resize( ref drops, DropCount );
		for ( var i = previousCount; i < drops.Length; i++ )
		{
			drops[i] = CreateSprite( rainPool, "Raindrop", rainStreak );
			drops[i].Enabled = false;
		}
		foreach ( var drop in drops )
		{
			drop.Sprite = rainStreak;
			drop.Billboard = SpriteRenderer.BillboardMode.None;
			drop.AlphaCutoff = 0;
			drop.DepthFeather = 4;
		}
	}

	GameObject CreateObject( GameObject parent, string name )
	{
		var obj = Scene.CreateObject( true );
		obj.NetworkMode = NetworkMode.Never;
		obj.Parent = parent;
		obj.Name = name;
		return obj;
	}

	SpriteRenderer CreateSprite( GameObject parent, string name, Sprite sprite )
	{
		var renderer = CreateObject( parent, name ).Components.Create<SpriteRenderer>();
		renderer.Sprite = sprite;
		renderer.Color = Color.Transparent;
		renderer.Opaque = false;
		renderer.AlphaCutoff = .006f;
		renderer.IsSorted = true;
		return renderer;
	}

	SpriteRenderer CloudLayer( GameObject parent, string name, Sprite sprite )
	{
		var cloud = CreateSprite( parent, name, sprite );
		cloud.Billboard = SpriteRenderer.BillboardMode.None;
		cloud.DepthFeather = 80;
		cloud.FogStrength = .14f;
		return cloud;
	}

	protected override void OnUpdate()
	{
		if ( deck is null || !deck[0].IsValid() ) return;
		if ( nextSearch && !tornado.IsValid() )
		{
			tornado = Scene.GetAllComponents<StormTornadoFunnel>().FirstOrDefault( f => f.GameObject.Tags.Has( "storm_tornado" ) );
			nextSearch = .10f;
		}
		if ( tornado.IsValid() )
		{
			StormCenter = tornado.WorldPosition;
			stormProfile = tornado.Profile;
			openCoreRadius = tornado.OpenCoreRadius;
			severity = Math.Clamp( tornado.Rating, 0, 5 ) / 5f;
			rainBearing = tornado.RainBearing;
		}
		var target = tornado.IsValid() ? 1f : 0f;
		if ( tornado.IsValid() ) integratedCloud = tornado.HasIntegratedCloud;
		var step = Time.Delta * (tornado.IsValid() ? .46f : .70f);
		StormStrength += Math.Clamp( target - StormStrength, -step, step );
		float t = age;
		windTravel += (t - windTravelTime) * 465 * VisualGust;
		windTravelTime = t;
		UpdateClouds( t );
		UpdateHaze();

		var camera = Scene.Camera;
		var eye = camera.IsValid() ? camera.WorldPosition : Vector3.Zero;
		var exposure = camera.IsValid() && eye.z < 2600 ? PrecipitationAt( eye ) : 0f;
		if ( nextShelterProbe )
		{
			sheltered = exposure > .015f && IsSheltered( eye );
			nextShelterProbe = .12f;
		}
		UpdateDrops( eye, sheltered ? 0 : exposure, t );
	}

	public float PrecipitationAt( Vector3 position )
	{
		// Rating drives the broad weather; rolled funnel width controls the tighter band, not its severity.
		var rain = BackgroundRain + (.96f + .26f * severity) * StormStrength + (.62f + .22f * severity) * CellStrengthAt( position );
		if ( openCoreRadius <= 0 ) return rain;
		var distance = (position - StormCenter).WithZ( 0 ).Length;
		var opening = 1 - StormWeatherTextures.Smooth( (distance - openCoreRadius) / 460 );
		return rain * (1 - .87f * opening * StormStrength);
	}

	float CellStrengthAt( Vector3 position )
	{
		var radius = RainBandRadius * (.78f + .28f * MathF.Sqrt( stormProfile.WidthScale ));
		var delta = (position - StormCenter).WithZ( 0 );
		var core = StormWeatherTextures.Smooth( (radius - delta.Length) / (radius * .64f) );
		var downwind = Vector3.Dot( delta, rainBearing );
		var sideways = (delta - rainBearing * downwind).Length;
		var along = (downwind / radius - .47f) / .89f;
		var across = sideways / (radius * .68f);
		var curtain = StormWeatherTextures.Smooth( (1 - MathF.Sqrt( along * along + across * across )) * 1.55f );
		return StormStrength * (.53f * core + .47f * curtain);
	}

	public bool IsSheltered( Vector3 position ) => Scene.Trace.Ray( position, position + Vector3.Up * 3500 )
		.WithoutTags( "player" ).Run().Hit;

	void UpdateClouds( float t )
	{
		var maps = DenseCloudMaps();
		for ( var i = 0; i < deck.Length; i++ )
		{
			var cloud = deck[i];
			if ( cloud.Sprite != maps[i] ) cloud.Sprite = maps[i];
			cloud.WorldPosition = new Vector3( MathF.Sin( t * .013f ) * 520 - i * 1600, MathF.Sin( t * .009f ) * 710 + i * 1100, 9200 + i * 1800 );
			cloud.WorldRotation = Rotation.From( new Angles( 90, -14 + i * 36, 0 ) );
			cloud.Size = new Vector2( 99000 - i * 4000, 84000 + i * 7000 );
			// Keep the low, rain-heavy underside charcoal while leaving lighter texture in the high deck.
			var dim = StormStrength * (.15f + .04f * severity);
			cloud.Color = i == 0
				? new Color( .30f - dim, .33f - dim, .38f - dim, .86f - .14f * StormStrength )
				: new Color( .46f - dim, .50f - dim, .57f - dim, .80f );
			cloud.FogStrength = .04f;
		}
		for ( var i = 0; i < shelf.Length; i++ )
		{
			var cloud = shelf[i];
			// The low card is only a fallback. The main wall cloud shares the funnel's depth march.
			cloud.Enabled = (i != 0 || !integratedCloud) && StormStrength > .004f;
			var spread = 1 + (MathF.Sqrt( stormProfile.WidthScale ) - 1) * .67f;
			var eye = openCoreRadius > 0;
			if ( i == 0 )
			{
				var offset = eye ? new Vector3( -70, 170, 0 ) : rainBearing * 1160;
				cloud.WorldPosition = StormCenter + offset * spread + Vector3.Up * (stormProfile.FunnelHeight + 130);
				cloud.WorldRotation = Rotation.From( new Angles( 90, Rotation.LookAt( rainBearing, Vector3.Up ).Angles().yaw + MathF.Sin( t * .15f ) * 6, 0 ) );
				cloud.Size = new Vector2( 10800, 5550 ) * spread;
			}
			else
			{
				// Separate large, slower-moving rain bands cover the playing field instead of following the funnel's exact track.
				var drift = MathF.Sin( t * (.027f + .008f * i) + i );
				var anchor = StormCenter.WithZ( 0 ) * .21f;
				cloud.WorldPosition = anchor + new Vector3( -1000 * i + 680 * drift, 1700 - 1400 * i + 530 * drift, 7200 + 760 * i );
				cloud.WorldRotation = Rotation.From( new Angles( 90, -35 + 29 * i + 4 * drift, 0 ) );
				cloud.Size = new Vector2( 58000 + 4600 * i, 49000 + 3300 * i ) * (1 + .16f * severity);
			}
			var tone = .14f + .067f * i - .027f * severity;
			cloud.Color = new Color( tone * .89f, tone, tone * 1.15f, StormStrength * (i == 0 ? integratedCloud ? 0 : .92f : .65f - i * .065f) );
			cloud.FogStrength = .04f;
			var sprite = eye && i == 0 ? eyeShelf ??= StormWeatherTextures.CloudSheet( 552, true, true ) : maps[i + deck.Length];
			if ( cloud.Sprite != sprite ) cloud.Sprite = sprite;
		}
	}

	void UpdateHaze()
	{
		var spread = MathF.Sqrt( stormProfile.WidthScale );
		if ( backgroundFog.IsValid() )
		{
			backgroundFog.MaximumOpacity = .21f + (.21f + .13f * severity) * StormStrength;
			backgroundFog.DistanceRange = new Vector2( 850 - 320 * StormStrength, 10300 - (3600 + 2350 * severity) * StormStrength );
		}
		for ( var i = 0; i < rainHaze.Length; i++ )
		{
			var fog = rainHaze[i];
			if ( openCoreRadius > 0 )
			{
				var rotation = Rotation.FromYaw( 24 + 120 * i );
				var eyeScale = openCoreRadius / 480;
				fog.WorldPosition = StormCenter + rotation.Forward * (1760 * eyeScale) + Vector3.Up * 1100;
				fog.WorldRotation = rotation;
				fog.Bounds = BBox.FromPositionAndSize( Vector3.Zero, new Vector3( 1940 * eyeScale, 3500 * eyeScale, 2780 ) );
			}
			else
			{
				var curtainRotation = Rotation.LookAt( rainBearing, Vector3.Up );
				var along = (1160 + 730 * i) * spread;
				var offset = rainBearing * along + curtainRotation.Right * ((i - 1) * 660 * spread);
				fog.WorldPosition = StormCenter + offset + Vector3.Up * (stormProfile.FunnelHeight * .42f);
				fog.WorldRotation = curtainRotation;
				fog.Bounds = BBox.FromPositionAndSize( Vector3.Zero, new Vector3( (1950 + i * 250) * spread, (1350 + i * 210) * spread, stormProfile.FunnelHeight * .90f ) );
			}
			fog.Strength = StormStrength * (.73f - .09f * i + .27f * severity);
			fog.Enabled = fog.Strength > .004f;
		}
	}

	float VisualGust => StormStrength * (.38f + .62f * severity);

	void UpdateDrops( Vector3 eye, float exposure, float t )
	{
		EnsureRainPool();
		// The displacement and its time derivative drive placement and streak direction together.
		var gust = VisualGust;
		const float frequency = .84f;
		var windDirection = new Vector3( .42f, .91f, 0 );
		var pulse = 105 * MathF.Sin( t * frequency );
		var pulseSpeed = 105 * frequency * MathF.Cos( t * frequency );
		var windDistance = t * 320 + windTravel + (t - windTravelTime) * 465 * gust + pulse;
		var windSpeed = 320 + 465 * gust + pulseSpeed;
		var density = MathF.Min( 1, .18f + exposure * .33f );
		var heavy = Math.Clamp( (exposure - .72f) / 1.15f, 0, 1 );
		for ( var i = 0; i < drops.Length; i++ )
		{
			var drop = drops[i];
			var v = Seed( i, 214 );
			if ( exposure <= .035f || v > density )
			{
				drop.Enabled = false;
				continue;
			}
			// Wrap in world coordinates, with a second, taller field fading in beyond the near drops.
			var near = i < NearDropCount;
			var span = near ? 660 : 1830;
			var height = near ? 660 : 1660;
			var fallSpeed = 850 + 600 * v;
			var airCoupling = 1.15f - .42f * v;
			var travel = windDirection * (airCoupling * windDistance);
			var velocity = windDirection * (airCoupling * windSpeed) + Vector3.Down * fallSpeed;
			var x = WrapNear( Seed( i, 302 ) * span + travel.x, eye.x, span );
			var y = WrapNear( Seed( i, 413 ) * span + travel.y, eye.y, span );
			var z = WrapNear( Seed( i, 523 ) * height - t * fallSpeed, eye.z + (near ? 110 : 350), height );
			var position = new Vector3( x, y, z );
			var distance = (position - eye).Length;
			var fade = near ? StormWeatherTextures.Smooth( (distance - 60) / 108 ) * StormWeatherTextures.Smooth( (430 - distance) / 155 )
				: StormWeatherTextures.Smooth( (distance - 270) / 250 ) * StormWeatherTextures.Smooth( (1060 - distance) / 320 );
			// Fade before wrapped drops reach the box seam, so no visible streak jumps across the view.
			var delta = position - eye;
			var seam = MathF.Max( MathF.Abs( delta.x ), MathF.Abs( delta.y ) );
			fade *= StormWeatherTextures.Smooth( (span * .5f - seam) / (span * .14f) );
			fade *= StormWeatherTextures.Smooth( (height * .5f - MathF.Abs( delta.z - (near ? 110 : 350) )) / (height * .12f) );
			var wave = .83f + .17f * MathF.Sin( (x * .91f - y * .42f) * .011f + z * .004f + t * .92f );
			var opacity = MathF.Min( near ? .35f : .23f, exposure * (near ? .11f + .12f * v : .065f + .075f * v) ) * fade * wave;
			if ( opacity <= .007f || z < 7 )
			{
				drop.Enabled = false;
				continue;
			}
			drop.WorldPosition = position;
			var up = -velocity.Normal;
			var normal = -delta - up * Vector3.Dot( -delta, up );
			drop.WorldRotation = Rotation.LookAt( normal.LengthSquared > .001f ? normal.Normal : Vector3.Cross( up, Vector3.Forward ).Normal, up );
			// A nearby drop should not turn into a long stripe across the viewport.
			var lengthScale = near ? .56f + .44f * StormWeatherTextures.Smooth( (distance - 100) / 175 ) : 1;
			drop.Size = new Vector2( near ? 1.12f + .78f * v : 1.8f + 1.2f * v, (9 + 12 * v + 6 * heavy) * lengthScale );
			var shade = .40f + .24f * v;
			drop.Color = new Color( shade * .80f, shade * .90f, shade, opacity );
			drop.Enabled = true;
		}
	}

	static float WrapNear( float v, float center, float span )
	{
		var f = (v - center) / span + .5f;
		return center + (f - MathF.Floor( f ) - .5f) * span;
	}

	static float Seed( int index, int channel ) => StormSmokeTexture.Hash( index, channel, 945 );
}
