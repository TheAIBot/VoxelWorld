using Silk.NET.OpenGL;
using System;

namespace VoxelWorld.Render.VoxelGrid
{
    public interface IGenericVBO : IDisposable
    {
        string Name { get; }
        VertexAttribPointerType PointerType { get; }
        int Length { get; }
        BufferTargetARB BufferTarget { get; }
        uint ID { get; }
        int Size { get; }
        uint Divisor { get; }
        bool Normalize { get; }
        bool CastToFloat { get; }
        bool IsIntegralType { get; }
    }
}
