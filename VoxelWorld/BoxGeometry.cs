using OpenGL;
using System.Numerics;

namespace VoxelWorld
{
    internal static class BoxGeometry
    {
        private static readonly uint[] Indices = new uint[]
        {
            0, 1, 2, 1, 3, 2,
            1, 4, 3, 4, 5, 3,
            4, 7, 5, 7, 6, 5,
            7, 0, 6, 0, 2, 6,
            7, 4, 0, 4, 1, 0,
            2, 3, 6, 3, 5, 6
        };

        public static GeometryData MakeBoxGeometry(Vector3 min, Vector3 max)
        {
            Vector3[] vertices = new Vector3[]
            {
                        new Vector3(min.X, min.Y, max.Z),
                        new Vector3(max.X, min.Y, max.Z),
                        new Vector3(min.X, max.Y, max.Z),
                        new Vector3(max.X, max.Y, max.Z),
                        new Vector3(max.X, min.Y, min.Z),
                        new Vector3(max.X, max.Y, min.Z),
                        new Vector3(min.X, max.Y, min.Z),
                        new Vector3(min.X, min.Y, min.Z)
            };

            Vector3[] normals = Geometry.CalculateNormals(vertices, Indices);

            return new GeometryData(vertices, normals, Indices);
        }
    }
}
