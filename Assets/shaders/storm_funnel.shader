HEADER
{
	Description = "Storm Chasers: continuous rotating condensation volume";
}

FEATURES
{
	#include "common/features.hlsl"
}

MODES
{
	Forward();
}

COMMON
{
	#define S_TRANSLUCENT 1
	#define CUSTOM_MATERIAL_INPUTS
	#include "common/shared.hlsl"
}

struct VertexInput
{
	#include "common/vertexinput.hlsl"
};

struct PixelInput
{
	#include "common/pixelinput.hlsl"
};

VS
{
	#include "common/vertex.hlsl"
	PixelInput MainVs( VertexInput v )
	{
		return FinalizeVertex( ProcessVertex( v ) );
	}
}

PS
{
	#define DEPTH_STATE_ALREADY_SET 1
	#define BLEND_MODE_ALREADY_SET 1
	#define CULL_MODE_ALREADY_SET 1
	#include "common/pixel.hlsl"

	// Use just the exit faces. That works both outside and inside the proxy.
	BoolAttribute( renderbackfaces, true );
	RenderState( CullMode, FRONT );
	RenderState( DepthEnable, false );
	RenderState( DepthWriteEnable, false );
	RenderState( BlendEnable, true );
	RenderState( SrcBlend, ONE );
	RenderState( DstBlend, INV_SRC_ALPHA );

	float3 VortexOrigin < Attribute( "VortexOrigin" ); >;
	float2 VortexForward < Attribute( "VortexForward" ); >;
	float4 VortexBounds < Attribute( "VortexBounds" ); >; // horizontal extent, full height, profile height, inner radius
	float VortexWedge < Attribute( "VortexWedge" ); >; // weighted transition to the hollow F5
	float4 VortexRadii < Attribute( "VortexRadii" ); >;
	float4 VortexPhase < Attribute( "VortexPhase" ); >; // top radius, visible floor, opacity, animation time
	float4 VortexDustProfile < Attribute( "VortexDustProfile" ); >; // lift, radial spread, density, turbulence
	float VortexDustOpacity < Attribute( "VortexDustOpacity" ); >; // surface circulation, independent of condensation
	float4 VortexAxisX < Attribute( "VortexAxisX" ); >;
	float4 VortexAxisY < Attribute( "VortexAxisY" ); >;
	float2 VortexTop < Attribute( "VortexTop" ); >;
	float3 StormCloudCenter < Attribute( "StormCloudCenter" ); >;
	float4 StormCloudBounds < Attribute( "StormCloudBounds" ); >;
	Texture3D VortexNoise < Attribute( "VortexNoise" ); >;
	SamplerState VortexSampler < Filter( MIN_MAG_LINEAR_MIP_POINT ); AddressU( WRAP ); AddressV( WRAP ); AddressW( WRAP ); >;

	float Curve( float4 lower, float top, float h )
	{
		float k = min( saturate( h ) * 4.0, 3.9999 );
		int i = (int)k;
		float t = k - i;
		float a = lower[max(0, i - 1)];
		float b = lower[i];
		float c = i < 3 ? lower[min(3, i + 1)] : top;
		float d = i < 2 ? lower[min(3, i + 2)] : top;
		return b + 0.5 * t * (c - a + t * (2*a - 5*b + 4*c - d + t * (3*(b-c) + d-a)));
	}

	float3 Local( float3 p )
	{
		return float3( dot( p.xy, VortexForward ), dot( p.xy, float2( -VortexForward.y, VortexForward.x ) ), p.z );
	}

	float2 Interval( float3 origin, float3 ray, float3 mins, float3 maxs )
	{
		float3 signRay = lerp( -1.0, 1.0, step( 0, ray ) );
		float3 invRay = signRay / max( abs( ray ), 0.00001 );
		float3 t0 = (mins - origin) * invRay;
		float3 t1 = (maxs - origin) * invRay;
		float3 lo = min( t0, t1 );
		float3 hi = max( t0, t1 );
		return float2( max( max( lo.x, lo.y ), lo.z ), min( min( hi.x, hi.y ), hi.z ) );
	}

	float4 SampleFunnel( float3 p )
	{
		float h = p.z / VortexBounds.z;
		float edge = smoothstep( VortexPhase.y - .01, VortexPhase.y + .115, h ) * (1 - smoothstep( .965, 1.105, h ));
		float dustH = p.z / max( 1, VortexDustProfile.x );
		float dustMix = VortexDustProfile.x > 0 && VortexDustOpacity > 0 ? 1 - smoothstep( .16, 1, dustH ) : 0;
		if ( edge < .002 && dustMix <= 0 ) return 0;
		float radius = Curve( VortexRadii, VortexPhase.x, h );
		// An elevated condensation tip tapers inward; it is not a slice through a solid tube.
		radius *= 1 - (1 - VortexWedge) * .60 * smoothstep( 0, .24, VortexPhase.y ) * (1 - smoothstep( -.01, .21, h - VortexPhase.y ));
		float2 center = float2( Curve( VortexAxisX, VortexTop.x, h ), Curve( VortexAxisY, VortexTop.y, h ) );
		float2 crossSection = (p.xy - center) / max( radius, 10 );
		float radial = length( crossSection );
		// Let the wedge's skirt sweep over the surface without expanding its eye or upper funnel.
		float surfaceSkirt = VortexWedge * (1 - smoothstep( .04, .38, dustH ));
		float dustRadius = radius + VortexDustProfile.y * (lerp( 1.10, .66, saturate( dustH ) ) + surfaceSkirt * lerp( .55, 2.65, VortexDustProfile.w ));
		float dustR = length( p.xy - center ) / max( 1, dustRadius );
		if ( radial > lerp( 1.39, 1.20, VortexWedge ) && (dustMix <= 0 || dustR > 1.24) ) return 0;
		// Rotate continuously, but keep the twist between heights bounded. A persistent
	// difference in spin rate would wind a multi-minute funnel into tight stripes.
	float twist = 3.7 + 1.25 * sin( VortexPhase.w * .29 + .8 );
	float turn = twist * saturate( h ) - VortexPhase.w * .78;
		float s, c;
		sincos( turn, s, c );
		float2 spun = float2( dot( crossSection, float2(c, -s) ), dot( crossSection, float2(s, c) ) );
		float3 uv = float3( spun * .32 + .5, h * .92 - VortexPhase.w * .072 );
		float3 coarse = VortexNoise.SampleLevel( VortexSampler, uv, 0 ).rgb;
		// Let uneven condensation ride the flow. No periodic brightness wave around the axis.
		float3 detail = VortexNoise.SampleLevel( VortexSampler, uv * float3( 1.62, 1.72, 1.20 ) + coarse.bgr * .12, 0 ).rgb;
		float ridge = .55 * coarse.r + .28 * detail.g + .17 * detail.b;
		float outer = 1 - smoothstep( .63, 1.11, radial - (ridge - .51) * .50 );
		float inner = lerp( 1, smoothstep( VortexBounds.w, VortexBounds.w + radius * .23, radial * radius ), VortexWedge );
		// A broken veil softens the core without adding another layer of billboard smoke.
		float veil = (1 - VortexWedge) * (1 - smoothstep( .86, 1.36, radial )) * smoothstep( .35, .74, coarse.r * .7 + detail.g * .3 ) * .35;
		float density = max( outer, veil ) * inner * edge * (.77 + .70 * ridge) * VortexPhase.z;
		// Give the add-on a dense condensation core without hardening the silhouette.
		// The inner mask still keeps the walk-in wedge's eye hollow.
		if ( VortexDustProfile.x > 0 )
		{
			float core = 1 - smoothstep( .54, .91, radial );
			density *= 1 + core * lerp( 1.10, 2.15, VortexDustProfile.w );
		}
		float sideLight = smoothstep( -.85, 1.0, dot( crossSection, float2( -.65, .76 ) ) );
		float silver = smoothstep( .64, 1, radial ) * sideLight;
		float3 color = lerp( float3( .023, .029, .037 ), float3( .153, .167, .185 ), saturate( .12 + .48*sideLight + .35*ridge + .14*silver ) );
		// Dust wraps the foot; it does not replace the bottom of the condensation column.
		// The moving condensation edge still controls when the tip can reach the surface.
		float foot = 1 - smoothstep( .07, .42, dustH );
		float body = density * 4.4 / max( 52, radius ) * lerp( 1, lerp( .45, .90, foot ), dustMix );
		color *= 1 - .30 * foot * dustMix;
		if ( dustMix <= 0 || dustR > 1.24 ) return float4( color, body );
		// Mix the spinning dust into the same volume so condensation cannot draw over it.
		float turbulence = VortexDustProfile.w;
		float2 dustLocal = spun * (radius / max( 1, dustRadius ));
		float3 dirt = VortexNoise.SampleLevel( VortexSampler, float3( dustLocal * .58 + .5, dustH * .57 - VortexPhase.w * .19 ), 0 ).rgb;
		float3 wisps = VortexNoise.SampleLevel( VortexSampler, float3( (dustLocal + (dirt.gb - .5) * .23) * 2.16 + .47, dustH * 1.1 - VortexPhase.w * .41 ), 0 ).rgb;
		float roll = .5 + .5 * sin( 4 * atan2( dustLocal.y, dustLocal.x + .0001 ) + 10 * dustH - VortexPhase.w * 2.9 + dirt.g * 4 );
		float billow = lerp( .66 * dirt.r + .34 * dirt.g, .38 * dirt.r + .35 * wisps.r + .27 * roll, turbulence );
		float folds = lerp( .34 + 1.12 * billow, .12 + 1.85 * smoothstep( .31, .68, billow ), turbulence );
		float envelope = 1 - smoothstep( .38, 1.04, dustR + lerp( .38, .70, turbulence ) * (.52 - billow) );
		float eye = lerp( 1, smoothstep( VortexBounds.w, VortexBounds.w + radius * .25, dustR * dustRadius ), VortexWedge );
		float ground = smoothstep( -.035, .14, dustH + .13 * (billow - .5) );
		ground = max( ground, surfaceSkirt * lerp( .62, 2.10, turbulence ) );
		float dust = VortexDustProfile.z * dustMix * ground * envelope * eye * folds * VortexDustOpacity / max( 120, dustRadius );
		float grit = lerp( billow, smoothstep( .31, .76, .67 * billow + .33 * wisps.g ), turbulence );
		float3 dustColor = lerp( float3( .057, .047, .037 ), float3( .275, .232, .179 ), grit );
		return float4( (body * color + dust * dustColor) / max( .000001, body + dust ), body + dust );
	}

	// Density per world unit and local scattering color.
	float4 SampleWallCloud( float3 p )
	{
		float3 q = (p - StormCloudCenter) / StormCloudBounds.xyz;
		float rho = length( q.xy );
		float eye = 1;
		if ( VortexBounds.w > 0 )
		{
			float eyeRadius = VortexBounds.w + 560 * saturate( .62 + q.z );
			eye = lerp( 1, smoothstep( eyeRadius, eyeRadius + 480, length( p.xy - VortexTop ) ), VortexWedge );
			if ( eye < .002 ) return 0;
		}
		float s, c;
		sincos( -.19 * VortexPhase.w, s, c );
		float2 spun = float2( dot( q.xy, float2(c,-s) ), dot( q.xy, float2(s,c) ) );
		float3 uv = float3( spun * .57, q.z * .42 ) + float3( .45, .69, .25 );
		float3 macro = VortexNoise.SampleLevel( VortexSampler, uv, 0 ).rgb;
		float3 detail = VortexNoise.SampleLevel( VortexSampler, uv * 2.03 + macro.bgr * .16, 0 ).rgb;
		// Low at the updraft, lifting away unevenly along the flanks.
		float lower = .38 * smoothstep( .19, .93, rho ) + .27 * q.x + .26 * (macro.g - .5);
		float envelope = length( float3( q.xy, (q.z - lower) * 1.18 ) );
		float billow = (.64 * macro.r + .36 * detail.g - .50) * .72;
		float shell = 1 - smoothstep( .51, 1.01, envelope - billow );
		float boundary = 1 - smoothstep( .82, 1, max( max( abs(q.x), abs(q.y) ), abs(q.z) ) );
		float density = shell * boundary * eye * StormCloudBounds.w * (.0011 + .0027 * smoothstep( .24, .73, macro.r * .65 + detail.g * .35 ));
		float lighting = saturate( .15 + .32 * q.z - .25 * q.x + .48 * macro.r + .26 * detail.r );
		float3 color = lerp( float3( .009, .013, .020 ), float3( .112, .126, .147 ), lighting );
		return float4( color, density );
	}

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		float3 camera = g_vCameraPositionWs.xyz;
		float3 proxy = i.vPositionWithOffsetWs + g_vHighPrecisionLightingOffsetWs.xyz;
		float3 worldRay = normalize( proxy - camera );
		float3 origin = Local( camera - VortexOrigin );
		float3 ray = Local( worldRay );
		float farDist = distance( proxy, camera ) + 2;
		if ( Depth::Get( i.vPositionSs.xy ) > 0.0000001 )
			farDist = min( farDist, dot( Depth::GetWorldPosition( i.vPositionSs.xy ) - camera, worldRay ) );
		bool hasDust = VortexDustProfile.x > 0 && VortexDustOpacity >= .004;
		float lowerZ = hasDust ? 0 : VortexBounds.z * max( 0, VortexPhase.y - .012 );
		float2 funnel = Interval( origin, ray, float3( -VortexBounds.xx, lowerZ ), float3( VortexBounds.xx, VortexBounds.z * 1.108 ) );
		float2 cap = Interval( origin, ray, StormCloudCenter - StormCloudBounds.xyz, StormCloudCenter + StormCloudBounds.xyz );
		funnel = float2( max( funnel.x, 4 ), min( funnel.y, farDist ) );
		cap = float2( max( cap.x, 4 ), min( cap.y, farDist ) );
		bool hasFunnel = funnel.y > funnel.x && (VortexPhase.z >= .004 || hasDust);
		bool hasCloud = cap.y > cap.x && StormCloudBounds.w >= .004;
		if ( !hasFunnel && !hasCloud ) discard;
		float march = min( hasFunnel ? funnel.x : farDist, hasCloud ? cap.x : farDist );
		float funnelStride = (funnel.y - funnel.x) / (hasDust ? 52 + 12 * abs( ray.z ) : (abs( ray.z ) > .48 ? 48 : 32));
		float capStride = (cap.y - cap.x) / 28;
		// Gradually spend more samples near the ground and central column. A hard
		// step-size or jitter switch across a height plane draws a seam in the funnel.
		float detailHeight = VortexDustProfile.x * 2.2;
		float detailH = saturate( detailHeight / max( 1, VortexBounds.z ) );
		float detailRadius = max( VortexRadii.x, max( VortexRadii.y, Curve( VortexRadii, VortexPhase.x, detailH ) ) );
		float2 lowerBend = max( abs( VortexAxisX.yz ), abs( VortexAxisY.yz ) );
		float detailExtent = min( VortexBounds.x, 1.8 * detailRadius + max( lowerBend.x, lowerBend.y ) + VortexDustProfile.y * 1.5 );
		float fineStride = min( funnelStride, lerp( 27, 18, VortexDustProfile.w ) );
		const int stepBudget = 192;
		// Bound the number of full steps even on a long, translucent wedge ray.
		// Reserve iterations for partial steps at interval boundaries and empty-space jumps.
		float marchEnd = max( hasFunnel ? funnel.y : march, hasCloud ? cap.y : march );
		float minimumStride = hasDust ? max( .25, (marchEnd - march) / (stepBudget - 8) ) : .25;
		float dither = frac( 52.9829189 * frac( dot( floor( i.vPositionSs.xy ), float2( .06711056, .00583715 ) ) ) );
		float sampleFraction = hasDust ? lerp( .25, .75, dither ) : .5;
		float transmittance = 1;
		float3 scattered = 0;
		float weightedDepth = 0;
		// March both fields in distance order, skipping gaps between the bounding boxes.
		[loop] for ( int stepIndex = 0; stepIndex < stepBudget; ++stepIndex )
		{
			bool inFunnel = hasFunnel && march >= funnel.x && march < funnel.y;
			bool inCloud = hasCloud && march >= cap.x && march < cap.y;
			float next = farDist;
			if ( hasFunnel && march < funnel.x ) next = min( next, funnel.x );
			if ( hasCloud && march < cap.x ) next = min( next, cap.x );
			if ( !inFunnel && !inCloud )
			{
				if ( next >= farDist ) break;
				march = next;
				continue;
			}
			if ( inFunnel )
			{
				float stride = funnelStride;
				if ( hasDust )
				{
					float3 stepCenter = origin + ray * (march + .5 * funnelStride);
					float vertical = 1 - smoothstep( .35, 1, stepCenter.z / max( 1, detailHeight ) );
					float lateral = 1 - smoothstep( .62, 1, length( stepCenter.xy ) / max( 1, detailExtent ) );
					stride = lerp( funnelStride, fineStride, vertical * lateral );
				}
				next = min( next, min( funnel.y, march + max( minimumStride, stride ) ) );
			}
			if ( inCloud ) next = min( next, min( cap.y, march + max( minimumStride, capStride ) ) );
			float stepSize = next - march;
			float dist = march + stepSize * sampleFraction;
			march = next;
			float3 p = origin + ray * dist;
			float4 cloud = inCloud ? SampleWallCloud( p ) : 0;
			float4 body = inFunnel ? SampleFunnel( p ) : 0;
			float density = cloud.a + body.a;
			if ( density < .000001 ) continue;
			float absorb = 1 - exp2( -density * stepSize );
			float weight = transmittance * absorb;
			scattered += weight * (cloud.rgb * cloud.a + body.rgb * body.a) / density;
			weightedDepth += weight * dist;
			transmittance *= 1 - absorb;
			// The previous cutoff left visible background contrast through a dense core.
			if ( transmittance < (hasDust ? .002 : .015) ) break;
		}
		float opacity = 1 - transmittance;
		if ( opacity < .005 ) discard;
		float3 cloudPosition = camera + worldRay * (weightedDepth / opacity);
		return float4( Fog::Apply( cloudPosition, i.vPositionSs.xy, scattered / opacity, .27 ) * opacity, opacity );
	}
}
