using OpenGL.Constructs;
using System.Collections.Generic;
using VoxelWorld.Voxel;
using VoxelWorld.Voxel.Hierarchy;

namespace VoxelWorld.Render.VoxelGrid
{
    internal class IndirectDrawCmdManager
    {
        private readonly Dictionary<VoxelGridHierarchy, DrawElementsIndirectCommand> DrawCommands = new Dictionary<VoxelGridHierarchy, DrawElementsIndirectCommand>();
        private readonly Dictionary<VoxelGridHierarchy, DrawCommandInfo> DrawCmdInfos = new Dictionary<VoxelGridHierarchy, DrawCommandInfo>();
        public int Count => DrawCommands.Count;

        public void Add(VoxelGridHierarchy grid, GeometryData geometry, int firstIndice, int firstVertex)
        {
            Add(grid, firstIndice, geometry.Indices.Length, firstVertex, geometry.Vertices.Length);
        }

        public void Add(VoxelGridHierarchy grid, int firstIndice, int indiceCount, int firstVertex, int vertexCount)
        {
            DrawCommands.Add(grid, new DrawElementsIndirectCommand(indiceCount, 1, firstIndice, firstVertex, 0));
            DrawCmdInfos.Add(grid, new DrawCommandInfo(firstIndice, indiceCount, firstVertex, vertexCount));
        }

        public bool Remove(VoxelGridHierarchy grid)
        {
            if (DrawCommands.Remove(grid))
            {
                DrawCmdInfos.Remove(grid);
                return true;
            }
            return false;
        }

        public IEnumerable<VoxelGridHierarchy> GetGrids()
        {
            return DrawCommands.Keys;
        }

        public IEnumerable<DrawElementsIndirectCommand> GetCommands()
        {
            return DrawCommands.Values;
        }

        public IEnumerable<DrawCommandInfo> GetCommandsInfo()
        {
            return DrawCmdInfos.Values;
        }

        public IEnumerable<KeyValuePair<VoxelGridHierarchy, DrawCommandInfo>> GetGridCommandsInfo()
        {
            return DrawCmdInfos;
        }

        public void Clear()
        {
            DrawCommands.Clear();
            DrawCmdInfos.Clear();
        }
    }
}
