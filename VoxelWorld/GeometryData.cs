using System.Numerics;

namespace VoxelWorld
{
    internal readonly struct GeometryData
    {
        public readonly Vector3[] Vertices;
        public readonly Vector3[] Normals;
        public readonly uint[] Indices;

        public GeometryData(Vector3[] vertices, Vector3[] normals, uint[] indices)
        {
            this.Vertices = vertices;
            this.Normals = normals;
            this.Indices = indices;
        }
    }
}
