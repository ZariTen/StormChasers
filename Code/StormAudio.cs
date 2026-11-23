using System;
using Layer = StormAudioLoops.Layer;

/// <summary>One client-local weather mix, driven by the same exposure and gusts as the visual weather.</summary>
internal sealed class StormAudio : Component
{
	SoundHandle wind;
	SoundHandle rain;
	SoundHandle coveredRain;
	SoundHandle roar;
	Vector3 roarPosition;
	bool hasRoarPosition;
	int activeRevision;
	float windGain, rainGain, coveredGain, roarGain;

	internal void UpdateMix( Vector3 eye, float precipitation, float gust, bool sheltered, StormTornadoFunnel tornado, float time, float delta )
	{
		if ( !Enabled || Application.IsDedicatedServer ) return;
		if ( activeRevision != StormAudioLoops.Revision )
		{
			StopLayers();
			activeRevision = StormAudioLoops.Revision;
		}
		var intensity = Math.Clamp( precipitation / 2.6f, 0, 1 );
		var pulse = .81f + .13f * MathF.Sin( time * .81f ) + .06f * MathF.Sin( time * 1.83f + .5f );
		var windTarget = (.052f + .54f * gust) * pulse * (sheltered ? .30f : 1f);
		var rainTarget = MathF.Pow( intensity, 1.28f ) * .65f * (sheltered ? .06f : 1f);
		var coveredTarget = sheltered ? intensity * .47f : 0f;
		var roarTarget = 0f;
		var roarPitch = 1f;
		if ( tornado.IsValid() )
		{
			var severity = Math.Clamp( tornado.VisualSeverity, 0, 1 );
			var position = tornado.WorldPosition + Vector3.Up * 110;
			if ( !hasRoarPosition || roarGain < .016f ) roarPosition = position;
			else roarPosition += (position - roarPosition) * (1 - MathF.Exp( -delta * 7 ));
			hasRoarPosition = true;
			var horizontalDistance = (eye - position).WithZ( 0 ).Length;
			var reach = 4800 + 3600 * severity;
			var proximity = 1 - StormWeatherTextures.Smooth( (horizontalDistance - 550) / reach );
			roarTarget = proximity * (.68f + .32f * severity) * (sheltered ? .57f : 1);
			roarPitch = .98f - .17f * severity + .025f * MathF.Sin( time * 1.12f );
		}

		wind = EnsureLoop( wind, Layer.Wind, 0 );
		rain = EnsureLoop( rain, Layer.Rain, 0 );
		coveredRain = EnsureLoop( coveredRain, Layer.CoveredRain, 0 );
		if ( tornado.IsValid() ) roar = EnsureLoop( roar, Layer.Roar, .88f );
		windGain = Approach( windGain, windTarget, delta );
		rainGain = Approach( rainGain, rainTarget, delta );
		coveredGain = Approach( coveredGain, coveredTarget, delta );
		roarGain = Approach( roarGain, roarTarget, delta );
		if ( wind is { IsValid: true } )
		{
			wind.Volume = windGain;
			wind.Pitch = .93f + .075f * gust;
		}
		if ( rain is { IsValid: true } ) rain.Volume = rainGain;
		if ( coveredRain is { IsValid: true } ) coveredRain.Volume = coveredGain;
		if ( roar is { IsValid: true } )
		{
			roar.Position = roarPosition;
			roar.Volume = roarGain;
			if ( tornado.IsValid() ) roar.Pitch = roarPitch;
			else if ( roarGain < .001f ) Stop( ref roar );
		}
	}

	static float Approach( float gain, float target, float delta ) => gain + (target - gain) * (1 - MathF.Exp( -delta * 4.5f ));

	static SoundHandle EnsureLoop( SoundHandle current, Layer layer, float spatial )
	{
		if ( current is { IsValid: true, Finished: false } ) return current;
		var handle = Sound.PlayFile( StormAudioLoops.Get( layer ), volume: .001f, pitch: 1, delay: 0, fadeInTime: .22f );
		handle.SpacialBlend = spatial;
		handle.DistanceAttenuation = false; // The weather mix already computes distance and shelter.
		handle.OcclusionEnabled = false;
		handle.ReverbEnabled = false;
		handle.DopplerScale = 0;
		return handle;
	}

	static void Stop( ref SoundHandle handle )
	{
		if ( handle is { IsValid: true } ) handle.Stop( .13f );
		handle = null;
	}

	protected override void OnDisabled() => StopLayers();

	void StopLayers()
	{
		Stop( ref wind );
		Stop( ref rain );
		Stop( ref coveredRain );
		Stop( ref roar );
		windGain = rainGain = coveredGain = roarGain = 0;
		hasRoarPosition = false;
	}
}
