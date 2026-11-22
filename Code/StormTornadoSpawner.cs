using System;

/// <summary>Places one tornado at a time at the child markers in the current map.</summary>
public sealed class StormTornadoSpawner : Component
{
	[Property] public float SpawnDelay { get; set; } = 3f;
	[Property] public float Lifetime { get; set; } = 18f;

	GameObject tornado;
	TimeUntil nextSpawn;
	TimeUntil expires;
	int nextMarker;

	protected override void OnStart() => nextSpawn = SpawnDelay;

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying || !Networking.IsHost || !Networking.IsActive ) return;

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
		tornado.Components.Create<StormTornadoFunnel>();
		tornado.NetworkSpawn();
		expires = Lifetime;
	}

	protected override void OnDisabled()
	{
		if ( tornado.IsValid() ) tornado.Destroy();
	}
}

/// <summary>A visual-only funnel. The host moves it; each client renders its own dust.</summary>
public sealed class StormTornadoFunnel : Component
{
	const int Levels = 12;
	const int PuffsPerLevel = 4;
	readonly GameObject[] puffs = new GameObject[Levels * PuffsPerLevel];
	TimeSince age;

	protected override void OnStart()
	{
		age = 0;
		var model = Model.Load( "models/dev/sphere.vmdl" );
		var diameter = model.Bounds.Size.x;
		for ( var i = 0; i < puffs.Length; i++ )
		{
			var height = (i / PuffsPerLevel) / (Levels - 1f);
			var puff = Scene.CreateObject( false );
			puff.Name = "Dust";
			puff.NetworkMode = NetworkMode.Never;
			puff.Parent = GameObject;
			var width = (24f + height * height * 105f) / diameter;
			puff.LocalScale = new Vector3( width, width, width * 0.75f );
			var renderer = puff.Components.Create<ModelRenderer>();
			renderer.Model = model;
			var shade = 0.29f + height * 0.14f + (i % PuffsPerLevel) * 0.025f;
			renderer.Tint = new Color( shade * 1.06f, shade, shade * 0.94f );
			puffs[i] = puff;
		}
		AnimateFunnel();
		foreach ( var puff in puffs ) puff.Enabled = true;
	}

	protected override void OnUpdate()
	{
		if ( Networking.IsHost ) WorldPosition += WorldRotation.Forward * (18f * Time.Delta);
		AnimateFunnel();
	}

	void AnimateFunnel()
	{
		for ( var i = 0; i < puffs.Length; i++ )
		{
			var puff = puffs[i];
			if ( !puff.IsValid() ) continue;
			var height = (i / PuffsPerLevel) / (Levels - 1f);
			var angle = age * (5.5f - height * 2f) + height * 7f + (i % PuffsPerLevel) * (MathF.Tau / PuffsPerLevel);
			var radius = 7f + height * height * 55f;
			var lean = MathF.Sin( age * 0.8f + height * 3f ) * height * 24f;
			puff.LocalPosition = new Vector3( MathF.Cos( angle ) * radius + lean, MathF.Sin( angle ) * radius, 7f + height * 250f );
		}
	}
}
