using OpenGL.Constructs;
using System.Collections.Generic;

namespace VoxelWorld
{
    internal class IndirectDrawCmdManager
    {
        private readonly Dictionary<VoxelGridHierarchy, DrawElementsIndirectCommand> DrawCommands = new Dictionary<VoxelGridHierarchy, DrawElementsIndirectCommand>();
        public int Count => DrawCommands.Count;

        public void Add(VoxelGridHierarchy grid, GeometryData geometry, int firstIndice, int firstVertex)
        {
            DrawCommands.Add(grid, new DrawElementsIndirectCommand(geometry.Indices.Length, 1, firstIndice, firstVertex, 0));
        }

        public bool Remove(VoxelGridHierarchy grid)
        {
            return DrawCommands.Remove(grid);
        }

        public IEnumerable<DrawElementsIndirectCommand> GetCommands()
        {
            return DrawCommands.Values;
        }

        public void Clear()
        {
            DrawCommands.Clear();
        }
    }
}
