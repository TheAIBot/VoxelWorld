using System;
using System.Collections.Concurrent;
using VoxelWorld.ShapeGenerators;
using VoxelWorld.Voxel.Grid;
using VoxelWorld.Voxel.Hierarchy;

namespace VoxelWorld.Voxel.System
{
    internal class VoxelSystemData
    {
        public readonly int GridSize;
        public readonly float VoxelSize;
        public readonly PlanetGen WeightGen;
        private readonly ConcurrentDictionary<GridPos, VoxelGridHierarchy> PosToVoxelGridHir;
        private readonly ConcurrentDictionary<GridPos, bool> MustGenerate;
        private VoxelSystemData OneDown = null;

        private const int MaxDepth = 10;

        public VoxelSystemData(int gridSize, float voxelSize, PlanetGen generator)
            : this(gridSize, voxelSize, generator, new ConcurrentDictionary<GridPos, VoxelGridHierarchy>(), new ConcurrentDictionary<GridPos, bool>())
        { }

        private VoxelSystemData(int gridSize, float voxelSize, PlanetGen generator, ConcurrentDictionary<GridPos, VoxelGridHierarchy> posToVoxelGridHir, ConcurrentDictionary<GridPos, bool> mustGenerate)
        {
            GridSize = gridSize;
            VoxelSize = voxelSize;
            WeightGen = generator;
            PosToVoxelGridHir = posToVoxelGridHir;
            MustGenerate = mustGenerate;
        }

        public VoxelSystemData GetWithHalfVoxelSize()
        {
            if (OneDown == null)
            {
                OneDown = new VoxelSystemData(GridSize, VoxelSize / 2.0f, WeightGen, PosToVoxelGridHir, MustGenerate);
            }

            return OneDown;
        }

        public VoxelSystemData GetWithDoubleVoxelSize()
        {
            return new VoxelSystemData(GridSize, VoxelSize * 2.0f, WeightGen, PosToVoxelGridHir, MustGenerate);
        }

        public void MarkMustGenerateSurroundings(GridSidePointsUsed sidesUsed, in GridPos pos)
        {
            Span<(bool useAdjacent, GridOffset offset)> offsets = stackalloc (bool, GridOffset)[GridSidePointsUsed.AdjacentOffsets];
            sidesUsed.FillWithAdjacentGridOffsets(offsets);

            for (int i = 0; i < offsets.Length; i++)
            {
                if (offsets[i].useAdjacent && pos.TryMove(in offsets[i].offset, out var adjacent))
                {
                    //Go up the tree until it finds a gridHir that has been made
                    VoxelGridHierarchy gridHir;
                    while (!PosToVoxelGridHir.TryGetValue(adjacent, out gridHir) && !adjacent.IsRootLevel())
                    {
                        //While it can't find anything generated, mark it as must
                        //be generated so the future know not to discard the path
                        //to grids that are necessary
                        MustGenerate.TryAdd(adjacent, true);
                        adjacent = adjacent.GoUpTree();
                    }

                    MustGenerate.TryAdd(adjacent, true);
                    gridHir?.MarkMustGenerate();
                }
            }
        }

        public void AddVoxelGridHir(in GridPos gridPos, VoxelGridHierarchy gridHir)
        {
            PosToVoxelGridHir.TryAdd(gridPos, gridHir);
        }

        public bool IsMustGenerate(in GridPos gridPos)
        {
            return MustGenerate.ContainsKey(gridPos);
        }
    }
}
