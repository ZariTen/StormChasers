using System;

/// <summary>Client-side clouds, condensation, ground dust and visual debris around the host-driven vortex.</summary>
public sealed class StormTornadoFunnel : Component
{
	[Property, Sync] public int Rating { get; set; }
	[Property, Sync] public float SizeRoll { get; set; } = .5f;
	public StormTornadoProfile Profile => StormTornadoProfile.ForSpawn( Rating, SizeRoll );
	static float WedgeScale( StormTornadoProfile profile ) => profile.WidthScale / StormTornadoProfile.ForRating( 5 ).WidthScale;
	public float OpenCoreRadius => Rating >= 5 ? 480f * WedgeScale( Profile ) : 0f;
	const float DebrisLiftHeight = 1200f;
	const float DriftSpeed = 78f;
	const int CoreCount = 52;
	const int VaporCount = 192;
	const int CloudCount = 74;
	const int DustCount = 36;
	const int DebrisCount = 22;
	const float GoldenAngle = 2.399963f;
	static Sprite crownMask;

	readonly SpriteRenderer[] vapor = new SpriteRenderer[VaporCount];
	readonly SpriteRenderer[] clouds = new SpriteRenderer[CloudCount];
	readonly SpriteRenderer[] dust = new SpriteRenderer[DustCount];
	readonly GameObject[] debris = new GameObject[DebrisCount];
	SpriteRenderer eyeCrown;
	SpriteRenderer eyeSkylight;
	TimeSince age;

	protected override void OnStart()
	{
		age = 0;
		if ( Application.IsDedicatedServer ) return;

		CreateSmokeLayer( "Condensation Funnel", vapor, 27, 22 );
		CreateSmokeLayer( "Storm Cloud Base", clouds, 110, 80 );
		CreateSmokeLayer( "Ground Dust", dust, 48, 20 );

		var debrisLayer = VisualObject( GameObject, "Airborne Debris" );
		var model = Model.Load( "models/dev/box.vmdl" );
		for ( var i = 0; i < debris.Length; i++ )
		{
			var piece = VisualObject( debrisLayer, "Debris" );
			var renderer = piece.Components.Create<ModelRenderer>();
			renderer.Model = model;
			renderer.Tint = i % 5 == 0 ? new Color( .43f, .36f, .27f ) : new Color( .19f, .16f, .13f );
			debris[i] = piece;
		}
		Animate();
	}

	GameObject VisualObject( GameObject parent, string name )
	{
		var obj = Scene.CreateObject( true );
		obj.NetworkMode = NetworkMode.Never;
		obj.Parent = parent;
		obj.Name = name;
		return obj;
	}

	void CreateSmokeLayer( string name, SpriteRenderer[] layer, float feather, float nearFade )
	{
		var group = VisualObject( GameObject, name );
		var smokeSprites = StormSmokeTexture.Sprites;
		for ( var i = 0; i < layer.Length; i++ )
		{
			var obj = VisualObject( group, "Smoke" );
			var renderer = obj.Components.Create<SpriteRenderer>();
			renderer.Sprite = smokeSprites[i % smokeSprites.Length];
			renderer.Billboard = SpriteRenderer.BillboardMode.Always;
			renderer.Opaque = false;
			// The stock sprite shader can still clip translucent smoke at the default cutoff.
			renderer.AlphaCutoff = .008f;
			renderer.Additive = false;
			renderer.Lighting = false;
			renderer.IsSorted = true;
			renderer.DepthFeather = feather;
			renderer.CameraFadeNear = 4;
			renderer.CameraFadeFar = nearFade;
			layer[i] = renderer;
		}
	}

	protected override void OnUpdate()
	{
		if ( Networking.IsHost ) WorldPosition += WorldRotation.Forward * (DriftSpeed * Time.Delta);
		if ( !Application.IsDedicatedServer ) Animate();
	}

	void Animate()
	{
		float t = age;
		var profile = Profile;
		var widthScale = profile.WidthScale;
		var groundScale = MathF.Sqrt( widthScale );
		var wedge = Rating >= 5;
		var wedgeScale = wedge ? WedgeScale( profile ) : 1f;
		var formation = Smooth( t / .9f );
		for ( var i = 0; i < vapor.Length; i++ )
		{
			var renderer = vapor[i];
			var core = i < CoreCount;
			var phase = core ? i / (float)CoreCount : (i - CoreCount) / (float)(VaporCount - CoreCount);
			var h = Wrap( phase + t * (core ? .052f : .045f + .018f * Seed( i + 54 )) );
			var fluff = Seed( i + 9 );
			var wisp = !core && i % 4 == 0;
			if ( wedge )
			{
				PlaceWedgeWall( renderer, i, h, t, fluff, core, wisp, formation, profile.FunnelHeight, wedgeScale );
				continue;
			}
			renderer.FogStrength = 1;
			var low = 1 - h;
			var width = (66 + 200 * h + 342 * h * h) * widthScale + 36 * (widthScale - 1) * low * low * low;
			var angle = i * GoldenAngle + t * (2.2f - 1.15f * h) + 5.4f * h;
			var radius = width * (core ? .10f + .13f * fluff : wisp ? .76f + .38f * fluff : .26f + .43f * fluff);
			var size = width * (core ? 1.8f + .21f * fluff : 1.22f + .68f * fluff) + (wisp ? 34f : 72f);
			var edge = Smooth( (h + .018f) / .063f ) * Smooth( (1 - h) / .095f );
			var shade = (core ? .075f : .12f) + h * .125f + .075f * fluff;
			renderer.LocalPosition = Axis( h, t ) * groundScale + Orbit( angle, radius, .84f ) + Vector3.Up * (18 + h * profile.FunnelHeight);
			renderer.Size = new Vector2( size, size * (.89f + .31f * fluff) );
			renderer.Color = new Color( shade * .91f, shade * .96f, shade * 1.02f, edge * formation * (core ? 1 : wisp ? .32f : .83f) );
		}

		AnimateClouds( t, profile, groundScale );
		UpdateEyeCrown( t, profile.FunnelHeight, wedge ? wedgeScale : 0 );

		for ( var i = 0; i < dust.Length; i++ )
		{
			var renderer = dust[i];
			var v = Seed( i + 202 );
			var cycle = Wrap( i / (float)dust.Length + t * (.14f + .09f * v) );
			var angle = i * GoldenAngle + t * (1.65f + .83f * v) + cycle;
			var radius = (64 + 202 * cycle * (.61f + .44f * v)) * groundScale + (wedge ? 830 * wedgeScale : 0);
			var size = (99 + 136 * cycle + 57 * v) * groundScale;
			var opacity = Smooth( cycle / .12f ) * Smooth( (1 - cycle) / .53f );
			renderer.LocalPosition = Orbit( angle, radius, .77f ) + Vector3.Up * (24 + (1 - cycle) * 74 * v);
			renderer.Size = new Vector2( size, size * (.39f + .17f * v) );
			renderer.Color = new Color( .33f + .13f * v, .26f + .10f * v, .20f + .075f * v, .55f * opacity * formation );
		}

		for ( var i = 0; i < debris.Length; i++ )
		{
			var piece = debris[i];
			var v = Seed( i + 410 );
			var lift = Wrap( i / (float)debris.Length + t * (.13f + .10f * v) );
			var h = lift * (.12f + .51f * v);
			var angle = i * GoldenAngle + t * (2.35f - v) + lift * 3.6f;
			var radius = (78 + 131 * lift + 64 * v) * groundScale + (wedge ? 660 * wedgeScale : 0);
			var size = (4 + 13 * v) * formation * Smooth( lift / .12f ) * Smooth( (1 - lift) / .17f ) / 50f;
			var height = h * DebrisLiftHeight * (1 + (groundScale - 1) * .42f);
			piece.LocalPosition = Axis( height / profile.FunnelHeight, t ) * groundScale + Orbit( angle, radius, .85f ) + Vector3.Up * (14 + height);
			piece.LocalRotation = Rotation.From( new Angles( t * (136 + 145 * v) + i * 23, t * (93 + 61 * v), t * 159 + i * 17 ) );
			piece.LocalScale = new Vector3( size, size * (.27f + .45f * v), size * .14f );
		}
	}

	void UpdateEyeCrown( float t, float height, float scale )
	{
		if ( scale <= 0 )
		{
			if ( eyeCrown.IsValid() ) eyeCrown.Enabled = false;
			if ( eyeSkylight.IsValid() ) eyeSkylight.Enabled = false;
			return;
		}
		if ( !eyeCrown.IsValid() )
		{
			crownMask ??= StormWeatherTextures.CloudSheet( 1081, true, true );
			eyeCrown = VisualObject( GameObject, "Eye Crown" ).Components.Create<SpriteRenderer>();
			eyeCrown.Sprite = crownMask;
			eyeCrown.Billboard = SpriteRenderer.BillboardMode.None;
			eyeCrown.Opaque = false;
			eyeCrown.AlphaCutoff = .006f;
			eyeCrown.IsSorted = true;
			eyeCrown.FogStrength = .17f;

			eyeSkylight = VisualObject( GameObject, "Thin Cloud Through Eye" ).Components.Create<SpriteRenderer>();
			eyeSkylight.Sprite = StormSmokeTexture.Sprites[1];
			eyeSkylight.Billboard = SpriteRenderer.BillboardMode.None;
			eyeSkylight.Opaque = false;
			eyeSkylight.AlphaCutoff = .006f;
			eyeSkylight.IsSorted = true;
			eyeSkylight.FogStrength = .07f;
		}

		eyeCrown.Size = new Vector2( 7200, 6500 ) * scale;
		eyeSkylight.Size = new Vector2( 3200, 2850 ) * scale;
		eyeCrown.LocalPosition = Axis( 1, t ) * .42f + Vector3.Up * (height - 30);
		eyeCrown.LocalRotation = Rotation.From( new Angles( 90, t * 7, 0 ) );
		var formation = Smooth( t / 2.4f );
		eyeCrown.Color = new Color( .12f, .15f, .21f, formation );
		eyeCrown.Enabled = true;
		eyeSkylight.LocalPosition = Axis( 1, t ) * .42f + Vector3.Up * (height + 460);
		eyeSkylight.LocalRotation = Rotation.From( new Angles( 90, 71 + t * 4, 0 ) );
		eyeSkylight.Color = new Color( .73f, .81f, .92f, .88f * formation );
		eyeSkylight.Enabled = true;
	}

	void PlaceWedgeWall( SpriteRenderer renderer, int index, float h, float t, float fluff, bool dense, bool wisp, float formation, float height, float scale )
	{
		// Leave the middle free of condensation sprites. The dark wall overtakes the viewer from every side.
		var azimuth = index * GoldenAngle + t * (.93f - .47f * h) + h * 5.1f;
		var radius = 1350 + 660 * h + 140 * h * h + (fluff - .5f) * 240;
		radius += MathF.Sin( azimuth * 3 + h * 6 - t * .7f ) * 115 + (wisp ? 230 : dense ? -80 : 65);
		var width = (1090 + 430 * h + fluff * 170) * scale;
		var shade = .105f + .10f * h + .16f * fluff;
		var opacity = (dense ? .91f : wisp ? .31f : .76f) * Smooth( (h + .035f) / .097f ) * Smooth( (1 - h) / .085f );
		renderer.LocalPosition = Axis( h, t ) * .42f + Orbit( azimuth, radius * scale, .96f ) + Vector3.Up * (28 + h * height);
		renderer.Size = new Vector2( width, width * (.89f + .35f * fluff) );
		renderer.FogStrength = .30f;
		renderer.Color = new Color( shade * .87f, shade * .94f, shade * 1.05f, opacity * formation );
	}

	void AnimateClouds( float t, StormTornadoProfile profile, float groundScale )
	{
		// A low rotating wall cloud wraps the funnel and merges into a broad, slower-moving cloud deck.
		const int loweringCount = 18;
		var hasCore = OpenCoreRadius > 0;
		var center = Axis( 1, t ) * (hasCore ? .42f : groundScale);
		var formation = Smooth( t / 2.4f );
		for ( var i = 0; i < clouds.Length; i++ )
		{
			var cloud = clouds[i];
			var v = Seed( i + 616 );
			var j = Seed( i + 846 );
			var lowering = i < loweringCount;
			var radial = lowering ? (i + .5f) / loweringCount : (i - loweringCount + .5f) / (CloudCount - loweringCount);
			var radius = lowering ? 90 + 630 * radial : 660 + 1710 * MathF.Sqrt( radial );
			var angle = i * GoldenAngle + t * (lowering ? .23f + .08f * v : .07f + .036f * v);
			var ripple = MathF.Sin( t * .47f + i * GoldenAngle ) * (lowering ? 56 : 102);
			var altitude = profile.FunnelHeight + (lowering ? -340 + 350 * v : 60 + 850 * v + 185 * radial) + ripple;
			var cloudRadius = radius * (.85f + .23f * j) * groundScale;
			if ( hasCore ) cloudRadius += lowering ? 1300 : 930;
			var offset = Orbit( angle, cloudRadius, lowering ? .92f : 1.25f );
			var width = (lowering ? 770 + 570 * j : 1290 + 1100 * j) * groundScale;
			var shade = lowering ? .10f + .10f * v : .18f + .17f * v;
			var alpha = lowering ? .60f + .26f * j : .56f - .28f * radial + .16f * j;
			cloud.LocalPosition = center + offset + Vector3.Up * altitude;
			cloud.Size = new Vector2( width, width * (.40f + .27f * v) );
			cloud.FogStrength = .18f;
			cloud.Color = new Color( shade * .91f, shade * .97f, shade * 1.06f, alpha * formation );
		}
	}

	static Vector3 Axis( float h, float t )
	{
		var bend = MathF.Sin( 4.2f * h - t * .65f );
		return new Vector3( h * (bend * 222f - 65f), h * h * 400 + MathF.Sin( .42f * t + h * 6.2f ) * 102 * h, 0 );
	}

	static Vector3 Orbit( float angle, float radius, float aspect ) => new( MathF.Cos( angle ) * radius, MathF.Sin( angle ) * radius * aspect, 0 );
	static float Wrap( float v ) => v - MathF.Floor( v );
	static float Smooth( float v ) { v = Math.Clamp( v, 0, 1 ); return v * v * (3 - 2 * v); }
	static float Seed( int index ) => StormSmokeTexture.Hash( index, 31, 851 );
}
