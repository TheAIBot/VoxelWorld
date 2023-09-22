using System.Runtime.InteropServices;

namespace VoxelWorld.Render.VoxelGrid
{
    /// <param name="Count"> Element count </param>
    /// <param name="InstanceCount"> Instance count </param>
    /// <param name="FirstIndex"> Index of first element in base array </param>
    /// <param name="BaseVertex"> Index of first vertex in base array </param>
    /// <param name="BaseInstance"> Index of the first instance in base array </param>
    [StructLayout(LayoutKind.Sequential)]
    public readonly record struct DrawElementsIndirectCommand(uint Count, uint InstanceCount, uint FirstIndex, uint BaseVertex, uint BaseInstance);
}
