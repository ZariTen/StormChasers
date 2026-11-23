using System;

/// <summary>Client-side clouds, condensation, ground dust and visual debris around the host-driven vortex.</summary>
public sealed class StormTornadoFunnel : Component
{
	[Property, Sync] public int Rating { get; set; }
	[Property, Sync] public float SizeRoll { get; set; } = .5f;
	[Property, Sync] public float ShapeRoll { get; set; } = .5f;
	[Property, Sync] public float Lifetime { get; set; } = 18f;
	[Sync] public float ElapsedSample { get; private set; }
	public StormTornadoProfile Profile => StormTornadoProfile.ForSpawn( Rating, SizeRoll );
	public StormFunnelShape Silhouette => Rating >= 5 ? StormFunnelShape.Wedge : ShapeRoll switch
	{
		< .30f => StormFunnelShape.Cone,
		< .46f => StormFunnelShape.VShape,
		< .60f => StormFunnelShape.Cylinder,
		< .74f => StormFunnelShape.Hourglass,
		< .90f => StormFunnelShape.Rope,
		_ => StormFunnelShape.Wedge
	};
	internal float VisibleFloor { get; private set; }
	internal float CondensationOpacity { get; private set; }
	static float WedgeScale( StormTornadoProfile profile ) => profile.WidthScale / StormTornadoProfile.ForRating( 5 ).WidthScale;
	public float OpenCoreRadius => Rating >= 5 ? 480f * WedgeScale( Profile ) : 0f;
	internal const float DriftSpeed = 78f;
	const int CoreCount = 52;
	const int VaporCount = 192;
	const int CloudCount = 74;
	const int DustCount = 36;
	const int DebrisCount = 56;
	const float GoldenAngle = 2.399963f;
	static Sprite crownMask;
	// Broad skylight on the clearer flank, opposite the storm-relative rain curtain.
	const float RainAlong = .65f, RainAcross = .76f;
	static readonly float ClearFlank = MathF.Atan2( RainAcross, -RainAlong );
	public Vector3 RainBearing => (WorldRotation.Forward * RainAlong + WorldRotation.Right * RainAcross).WithZ( 0 ).Normal;
	internal bool HasIntegratedCloud => volume.IsValid();

	readonly SpriteRenderer[] vapor = new SpriteRenderer[VaporCount];
	readonly SpriteRenderer[] clouds = new SpriteRenderer[CloudCount];
	readonly SpriteRenderer[] dust = new SpriteRenderer[DustCount];
	readonly GameObject[] debris = new GameObject[DebrisCount];
	ModelRenderer volume;
	SpriteRenderer eyeCrown;
	SpriteRenderer eyeSkylight;
	TimeSince age;

	protected override void OnStart()
	{
		age = Networking.IsHost ? 0f : ElapsedSample;
		if ( Application.IsDedicatedServer ) return;

		var material = StormFunnelVolume.LoadMaterial();
		if ( material is { IsValid: true, IsError: false } )
		{
			volume = VisualObject( GameObject, "Condensation Volume" ).Components.Create<ModelRenderer>();
			volume.Model = Model.Load( "models/dev/box.vmdl" );
			volume.MaterialOverride = material;
			volume.RenderType = ModelRenderer.ShadowRenderType.Off;
			volume.Attributes.Set( "VortexNoise", StormFunnelVolume.Noise );
		}
		else
		{
			CreateSmokeLayer( "Condensation Funnel", vapor, 27, 22 );
			CreateSmokeLayer( "Storm Cloud Base", clouds, 110, 80 );
		}
		CreateSmokeLayer( "Ground Dust", dust, 48, 20 );

		var debrisLayer = VisualObject( GameObject, "Airborne Debris" );
		var debrisMeshes = StormDebrisMeshes.Models;
		var debrisMaterial = StormDebrisMeshes.LoadMaterial();
		for ( var i = 0; i < debris.Length; i++ )
		{
			var piece = VisualObject( debrisLayer, "Debris" );
			var renderer = piece.Components.Create<ModelRenderer>();
			var heavy = i % 4 == 0;
			renderer.Model = debrisMeshes[heavy ? (i % 8 == 0 ? 0 : 2) : (1 + i % 3)];
			renderer.RenderType = ModelRenderer.ShadowRenderType.Off;
			var shade = .038f + (heavy ? .105f : .068f) * Seed( i + 424 );
			renderer.Tint = new Color( shade * 1.08f, shade, shade * .88f );
			if ( debrisMaterial is { IsValid: true, IsError: false } )
			{
				renderer.MaterialOverride = debrisMaterial;
				renderer.Attributes.Set( "FragmentColor", new Vector3( shade * .30f, shade * .25f, shade * .19f ) );
			}
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
		if ( Networking.IsHost )
		{
			WorldPosition += WorldRotation.Forward * (DriftSpeed * Time.Delta);
			if ( GameObject.Network.Active && age >= ElapsedSample + .5f ) ElapsedSample = age;
		}
		// Late joiners catch up without sending every sprite pose or restarting the lifecycle.
		else if ( ElapsedSample > age + .4f ) age = ElapsedSample;
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
		var progress = Math.Clamp( t / MathF.Max( .1f, Lifetime ), 0, 1 );
		var cloudLife = Smooth( progress / .10f ) * Smooth( (1 - progress) / .09f );
		var condensation = Smooth( progress / .095f ) * Smooth( (1 - progress) / .10f );
		var circulation = Smooth( progress / .055f ) * Smooth( (1 - progress) / .095f );
		var rope = Smooth( (progress - .71f) / .24f );
		// Condensation follows moisture, not ground contact. The circulation can outlast the visible funnel.
		var visibleFloor = .83f * (1 - Smooth( progress / .31f )) + (.38f + .25f * ShapeRoll) * rope;
		VisibleFloor = wedge ? .34f * (1 - Smooth( progress / .20f )) + .20f * rope : visibleFloor;
		CondensationOpacity = condensation;
		if ( volume.IsValid() ) AnimateVolume( t, profile, groundScale, wedge, rope, cloudLife );
		else
		{
			AnimateFallback( t, profile, groundScale, wedge, wedgeScale, rope, visibleFloor, condensation );
			AnimateClouds( t, profile, groundScale, cloudLife );
			UpdateEyeCrown( t, profile.FunnelHeight, wedge ? wedgeScale : 0, cloudLife );
		}
		AnimateGround( t, groundScale, wedge, wedgeScale, circulation );
		AnimateDebris( t, profile, groundScale, wedge, wedgeScale, circulation, progress, rope );
	}

	Vector3 CondensationAxis( float h, float t, float groundScale, float rope )
	{
		var axis = Axis( h, t ) * groundScale + new Vector3( 240, -420, 0 ) * ((ShapeRoll - .5f) * h * groundScale);
		var contortion = rope + (Silhouette == StormFunnelShape.Rope ? .94f : 0 );
		if ( Silhouette == StormFunnelShape.Cylinder )
			axis += new Vector3( 175, -315, 0 ) * (groundScale * MathF.Sin( h * MathF.PI ) * (.66f + .34f * MathF.Cos( t * .28f )));
		var wave = MathF.Sin( h * MathF.Tau ) * MathF.Sin( h * MathF.PI ) * (.85f + .15f * MathF.Sin( t * .66f ) );
		return axis + new Vector3( 180, -640, 0 ) * (contortion * wave * groundScale);
	}

	void AnimateVolume( float t, StormTornadoProfile profile, float groundScale, bool wedge, float rope, float cloudLife )
	{
		var sections = wedge ? StormFunnelSections.WalkInWedge : StormFunnelSections.For( Silhouette );
		var width = wedge ? WedgeScale( profile ) : profile.WidthScale * (1 - .29f * rope);
		var radii = sections.LowerRadii * width;
		var topRadius = sections.TopRadius * width;
		Vector3 Center( float h ) => wedge ? Axis( h, t ) * .42f : CondensationAxis( h, t, groundScale, rope );
		var a = Center( 0 );
		var b = Center( .25f );
		var c = Center( .50f );
		var d = Center( .75f );
		var e = Center( 1 );
		var axisExtent = MathF.Max( MathF.Max( b.Length, c.Length ), MathF.Max( d.Length, e.Length ) );
		var extent = topRadius * (wedge ? 1.21f : 1.39f) + axisExtent + 95;
		var cloudScale = .78f + .22f * groundScale;
		var cloudBounds = wedge ? new Vector3( 3850, 3150, 950 ) * WedgeScale( profile ) : new Vector3( 2460, 1700, 705 ) * cloudScale;
		var cloudCenter = e + new Vector3( wedge ? 0 : -215 * cloudScale, wedge ? 0 : 135 * cloudScale, profile.FunnelHeight + 180 );
		var halfX = MathF.Max( extent, MathF.Abs( cloudCenter.x ) + cloudBounds.x );
		var halfY = MathF.Max( extent, MathF.Abs( cloudCenter.y ) + cloudBounds.y );
		var fullHeight = MathF.Max( profile.FunnelHeight * 1.11f, cloudCenter.z + cloudBounds.z );
		volume.LocalPosition = Vector3.Up * (fullHeight * .5f);
		volume.LocalScale = new Vector3( halfX * 2, halfY * 2, fullHeight ) / 50f; // dev box is 50 units wide
		volume.Attributes.Set( "VortexOrigin", WorldPosition );
		var forward = WorldRotation.Forward.WithZ( 0 ).Normal;
		volume.Attributes.Set( "VortexForward", new Vector2( forward.x, forward.y ) );
		volume.Attributes.Set( "VortexBounds", new Vector4( extent, fullHeight, profile.FunnelHeight, OpenCoreRadius ) );
		volume.Attributes.Set( "VortexRadii", radii );
		volume.Attributes.Set( "VortexPhase", new Vector4( topRadius, VisibleFloor, CondensationOpacity, t ) );
		volume.Attributes.Set( "VortexAxisX", new Vector4( a.x, b.x, c.x, d.x ) );
		volume.Attributes.Set( "VortexAxisY", new Vector4( a.y, b.y, c.y, d.y ) );
		volume.Attributes.Set( "VortexTop", new Vector2( e.x, e.y ) );
		volume.Attributes.Set( "StormCloudCenter", cloudCenter );
		volume.Attributes.Set( "StormCloudBounds", new Vector4( cloudBounds.x, cloudBounds.y, cloudBounds.z, cloudLife ) );
	}

	void AnimateFallback( float t, StormTornadoProfile profile, float groundScale, bool wedge, float wedgeScale, float rope, float visibleFloor, float condensation )
	{
		var widthScale = profile.WidthScale;
		var sections = StormFunnelSections.For( Silhouette );
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
				PlaceWedgeWall( renderer, i, h, t, fluff, core, wisp, condensation, profile.FunnelHeight, wedgeScale );
				continue;
			}
			renderer.FogStrength = .56f;
			var width = 1.46f * sections.Sample( h ) * widthScale * (1 - .29f * rope);
			width *= 1 + .12f * MathF.Sin( t * .54f + h * 11 + ShapeRoll * 6 ) * MathF.Sin( h * MathF.PI );
			// As condensation retracts, taper the suspended tip instead of clipping a wide column.
			width *= 1 - Smooth( visibleFloor / .24f ) * .60f * (1 - Smooth( (h - visibleFloor + .045f) / .24f ));
			var angle = i * GoldenAngle + t * (2.2f - 1.15f * h) + 5.4f * h;
			var radius = width * (core ? .10f + .13f * fluff : wisp ? .76f + .38f * fluff : .26f + .43f * fluff);
			var size = width * (core ? 1.8f + .21f * fluff : 1.22f + .68f * fluff) + (wisp ? 34f : 72f);
			var edge = Smooth( (h - visibleFloor + .018f) / .105f ) * Smooth( (1 - h) / .095f );
			var skylight = Smooth( .5f + .5f * MathF.Cos( angle - ClearFlank ) );
			var ribs = MathF.Sin( 4.3f * (angle - t * (2.2f - 1.15f * h)) - 14 * h ) * .022f;
			var shade = (core ? .064f : .089f) + h * .071f + .050f * fluff + (core ? .065f : .19f) * skylight + ribs;
			var bend = CondensationAxis( h, t, groundScale, rope );
			renderer.LocalPosition = bend + Orbit( angle, radius, .84f ) + Vector3.Up * (18 + h * profile.FunnelHeight);
			renderer.Size = new Vector2( size, size * (.89f + .31f * fluff) );
			renderer.Color = new Color( shade * .91f, shade * .96f, shade * 1.02f, edge * condensation * (core ? 1 : wisp ? .32f : .83f) );
		}
	}

	void AnimateGround( float t, float groundScale, bool wedge, float wedgeScale, float circulation )
	{
		for ( var i = 0; i < dust.Length; i++ )
		{
			var renderer = dust[i];
			var v = Seed( i + 202 );
			var cycle = Wrap( i / (float)dust.Length + t * (.14f + .09f * v) );
			var angle = i * GoldenAngle + t * (1.53f + .41f * v) + cycle * 1.45f;
			var skirt = i % 3 != 0;
			var radius = (skirt ? 140 + 675 * cycle * (.75f + .48f * v) : 52 + 195 * cycle) * groundScale;
			if ( wedge ) radius += 880 * wedgeScale;
			var size = (skirt ? 230 + 405 * cycle + 95 * v : 204 + 190 * cycle + 90 * v) * groundScale;
			var band = .67f + .33f * MathF.Cos( angle * 2 - t * 2.7f );
			var opacity = Smooth( cycle / .10f ) * Smooth( (1 - cycle) / .39f ) * (.63f + .37f * band);
			renderer.LocalPosition = Orbit( angle, radius, .77f ) + Vector3.Up * (skirt ? 13 + 58 * v * (1 - cycle) : 46 + 105 * v * (1 - cycle));
			renderer.Size = new Vector2( size, size * (skirt ? .24f + .16f * v : .53f + .16f * v) );
			// A dark, loamy inner swirl and a cooler rain-mixed skirt, not an orange dust cloud in a downpour.
			var shade = (skirt ? .24f : .125f) + .14f * v;
			renderer.FogStrength = .48f;
			renderer.Color = new Color( shade * 1.03f, shade, shade * (skirt ? .98f : .90f), (skirt ? .69f : .91f) * opacity * circulation );
		}
	}

	void AnimateDebris( float t, StormTornadoProfile profile, float groundScale, bool wedge, float wedgeScale, float circulation, float progress, float rope )
	{
		var sections = wedge ? StormFunnelSections.WalkInWedge : StormFunnelSections.For( Silhouette );
		var funnelWidth = wedge ? wedgeScale : profile.WidthScale * (1 - .29f * rope);
		var loft = .20f + .80f * Smooth( progress / .30f );
		for ( var i = 0; i < debris.Length; i++ )
		{
			var piece = debris[i];
			var v = Seed( i + 410 );
			var drift = Seed( i + 517 );
			var heavy = i % 4 == 0;
			var thrown = !heavy && i % 9 == 1;
			var phase = Wrap( (i + .5f) / debris.Length + t * (heavy ? .145f + .10f * v : .096f + .078f * v) );
			// Boards arc and fall; lighter sheets and splinters are carried up the flank.
			var rise = heavy ? MathF.Sin( phase * MathF.PI ) * (.055f + .19f * v)
				: MathF.Pow( phase, 1.24f ) * (.76f + .29f * v );
			var escape = thrown ? Smooth( (phase - .48f) / .48f ) : 0;
			var h = Math.Clamp( .008f + (rise - .17f * escape) * loft, 0, .97f );
			var angle = i * GoldenAngle + t * (.79f + .57f * v) + phase * (heavy ? 5.3f : 7.9f);
			var edge = sections.Sample( h ) * funnelWidth;
			var skirt = (heavy ? 145 + 420 * drift : 195 + 540 * drift + 630 * h) * groundScale;
			var radius = edge * (.83f + .32f * v) + skirt * (.45f + .55f * Smooth( phase * 3 ));
			var kick = groundScale * escape * (320 + 470 * drift);
			var axis = wedge ? Axis( h, t ) * .42f : CondensationAxis( h, t, groundScale, rope );
			var position = axis + Orbit( angle, radius, .88f ) + new Vector3( .65f * kick, -.76f * kick, h * profile.FunnelHeight );
			piece.LocalPosition = position;
			piece.LocalRotation = Rotation.From( new Angles( t * (100 + 195 * v) + i * 23, t * (75 + 138 * drift) + i * 31, t * (100 + 175 * drift) + i * 17 ) );
			var life = circulation * Smooth( phase / .085f ) * Smooth( (1 - phase) / .13f );
			var length = (heavy ? 15 + 36 * v : 8 + 20 * v) * life / 50f;
			var breadth = (.64f + .56f * drift) * (heavy ? 1 : 1 + .26f * h);
			var board = heavy && i % 8 == 0;
			piece.LocalScale = new Vector3( length, length * breadth * (board ? .38f : 1), length * (heavy ? .09f : .61f) );
		}
	}

	void UpdateEyeCrown( float t, float height, float scale, float cloudLife )
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
		eyeCrown.Color = new Color( .12f, .15f, .21f, cloudLife );
		eyeCrown.Enabled = true;
		eyeSkylight.LocalPosition = Axis( 1, t ) * .42f + Vector3.Up * (height + 460);
		eyeSkylight.LocalRotation = Rotation.From( new Angles( 90, 71 + t * 4, 0 ) );
		eyeSkylight.Color = new Color( .73f, .81f, .92f, .88f * cloudLife );
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
		var opacity = (dense ? .91f : wisp ? .31f : .76f) * Smooth( (h - VisibleFloor + .01f) / .125f ) * Smooth( (1 - h) / .085f );
		renderer.LocalPosition = Axis( h, t ) * .42f + Orbit( azimuth, radius * scale, .96f ) + Vector3.Up * (28 + h * height);
		renderer.Size = new Vector2( width, width * (.89f + .35f * fluff) );
		renderer.FogStrength = .30f;
		renderer.Color = new Color( shade * .87f, shade * .94f, shade * 1.05f, opacity * formation );
	}

	void AnimateClouds( float t, StormTornadoProfile profile, float groundScale, float cloudLife )
	{
		// A low rotating wall cloud wraps the funnel and merges into a broad, slower-moving cloud deck.
		const int loweringCount = 18;
		var hasCore = OpenCoreRadius > 0;
		var center = hasCore ? Axis( 1, t ) * .42f : CondensationAxis( 1, t, groundScale, 0 );
		for ( var i = 0; i < clouds.Length; i++ )
		{
			var cloud = clouds[i];
			var v = Seed( i + 616 );
			var j = Seed( i + 846 );
			var lowering = i < loweringCount;
			var radial = lowering ? (i + .5f) / loweringCount : (i - loweringCount + .5f) / (CloudCount - loweringCount);
			var radius = lowering ? 90 + 630 * radial : 660 + 1710 * MathF.Sqrt( radial );
			var angle = i * GoldenAngle + t * (lowering ? .23f + .08f * v : .07f + .036f * v);
			var bank = .5f + .5f * MathF.Cos( angle - ClearFlank + .48f * MathF.Sin( t * .19f ) );
			var ripple = MathF.Sin( t * .47f + i * GoldenAngle ) * (lowering ? 56 : 102);
			var descent = hasCore ? 340 : 530 + 210 * bank;
			var altitude = profile.FunnelHeight + (lowering ? -descent + 390 * v : 60 + 850 * v + 185 * radial) + ripple;
			var cloudRadius = radius * (.73f + .27f * j + .40f * bank) * groundScale;
			if ( hasCore ) cloudRadius += lowering ? 1300 : 930;
			var offset = Orbit( angle, cloudRadius, lowering ? .92f : 1.25f );
			var width = (lowering ? 770 + 570 * j : 1290 + 1100 * j) * groundScale;
			var shade = lowering ? .059f + .085f * v + .07f * bank : .18f + .15f * v + .06f * bank;
			var alpha = lowering ? .68f + .23f * j : .56f - .28f * radial + .16f * j;
			cloud.LocalPosition = center + offset + Vector3.Up * altitude;
			cloud.Size = new Vector2( width, width * (lowering ? .46f + .31f * v : .40f + .27f * v) );
			cloud.FogStrength = .18f;
			cloud.Color = new Color( shade * .91f, shade * .97f, shade * 1.06f, alpha * cloudLife );
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
