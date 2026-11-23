using System.Collections.Generic;

/// <summary>Shared, double-sided splinters and torn sheet fragments; no physics shapes.</summary>
internal static class StormDebrisMeshes
{
	static Model[] models;
	internal static Material LoadMaterial()
	{
		var shader = Shader.Load( "shaders/storm_debris.shader" );
		return shader is { IsValid: true, IsError: false } ? Material.FromShader( shader ) : null;
	}

	internal static Model[] Models => models ??= new[]
	{
		Model.Load( "models/dev/box.vmdl" ),
		Strip( new(-25, -5, 0), new(-8, -3, 2), new(8, 2, -1), new(25, 8, 2), 1.2f, 3, 2, .35f ),
		Strip( new(-25, 6, 0), new(-10, 2, 2), new(9, -3, -3), new(25, 2, 4), 3, 9, 5, 2 ),
		Strip( new(-25, -8, 1), new(-9, -4, 0), new(8, -3, -2), new(24, 10, 3), 2, 4, 6, 1.2f )
	};

	static Model Strip( Vector3 a, Vector3 b, Vector3 c, Vector3 d, float wa, float wb, float wc, float wd )
	{
		var points = new[] { a, b, c, d };
		var widths = new[] { wa, wb, wc, wd };
		var vertices = new List<Vertex>( 36 );
		for ( var i = 0; i < points.Length - 1; i++ )
		{
			var l0 = points[i] - Vector3.Left * widths[i];
			var r0 = points[i] + Vector3.Left * widths[i] * .8f;
			var l1 = points[i + 1] - Vector3.Left * widths[i + 1];
			var r1 = points[i + 1] + Vector3.Left * widths[i + 1] * .8f;
			Face( l0, r0, l1 );
			Face( r0, r1, l1 );
		}
		void Face( Vector3 x, Vector3 y, Vector3 z )
		{
			var normal = Vector3.Cross( y - x, z - x ).Normal;
			var tangent = (z - x).Normal;
			vertices.Add( new Vertex( x, normal, tangent, new Vector4( 0, 0, 0, 0 ) ) );
			vertices.Add( new Vertex( y, normal, tangent, new Vector4( 0, 1, 0, 0 ) ) );
			vertices.Add( new Vertex( z, normal, tangent, new Vector4( 1, 0, 0, 0 ) ) );
			vertices.Add( new Vertex( x, -normal, tangent, new Vector4( 0, 0, 0, 0 ) ) );
			vertices.Add( new Vertex( z, -normal, tangent, new Vector4( 1, 0, 0, 0 ) ) );
			vertices.Add( new Vertex( y, -normal, tangent, new Vector4( 0, 1, 0, 0 ) ) );
		}
		var mesh = new Mesh( Material.Load( "materials/dev/primary_white.vmat" ), MeshPrimitiveType.Triangles );
		mesh.CreateVertexBuffer<Vertex>( vertices.Count, vertices );
		var bounds = new BBox( new Vector3( -26, -22, -8 ), new Vector3( 26, 22, 8 ) );
		mesh.Bounds = bounds;
		return new ModelBuilder().AddMesh( mesh ).WithViewBounds( bounds ).Create();
	}
}
