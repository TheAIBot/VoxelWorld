using System.Collections.Generic;
using System.Linq;
using VoxelWorld.Voxel;
using VoxelWorld.Voxel.Hierarchy;

namespace VoxelWorld.Render.VoxelGrid
{
    internal sealed class IndirectDrawCmdManager
    {
        private readonly Dictionary<VoxelGridHierarchy, DrawInformation> Drawinformations = new Dictionary<VoxelGridHierarchy, DrawInformation>();
        public int Count => Drawinformations.Count;

        public void Add(VoxelGridHierarchy grid, GeometryData geometry, int firstIndice, int firstVertex)
        {
            Add(grid, firstIndice, geometry.Indices.Length, firstVertex, geometry.Vertices.Length);
        }

        public void Add(VoxelGridHierarchy grid, int firstIndice, int indiceCount, int firstVertex, int vertexCount)
        {
            Drawinformations.Add(grid, new DrawInformation(new DrawElementsIndirectCommand((uint)indiceCount, 1u, (uint)firstIndice, (uint)firstVertex, 0u),
                                                           new DrawCommandInfo(firstIndice, indiceCount, firstVertex, vertexCount)));
        }

        public bool Remove(VoxelGridHierarchy grid)
        {
            return Drawinformations.Remove(grid);
        }

        public IEnumerable<VoxelGridHierarchy> GetGrids()
        {
            return Drawinformations.Keys;
        }

        public IEnumerable<DrawElementsIndirectCommand> GetCommands()
        {
            return Drawinformations.Values.Select(x => x.Command);
        }

        public IEnumerable<DrawCommandInfo> GetCommandsInfo()
        {
            return Drawinformations.Values.Select(x => x.Information);
        }

        public IEnumerable<KeyValuePair<VoxelGridHierarchy, DrawInformation>> GetGridCommandsInfo()
        {
            return Drawinformations;
        }

        public void Clear()
        {
            Drawinformations.Clear();
        }

        internal sealed record DrawInformation(DrawElementsIndirectCommand Command, DrawCommandInfo Information);
    }
}
