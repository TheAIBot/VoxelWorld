﻿using System;
using System.Collections.Concurrent;

namespace VoxelWorld
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
            this.GridSize = gridSize;
            this.VoxelSize = voxelSize;
            this.WeightGen = generator;
            this.PosToVoxelGridHir = posToVoxelGridHir;
            this.MustGenerate = mustGenerate;
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

        public void MarkMustGenerateSurroundings(GridSidePointsUsed sidesUsed, GridPos pos)
        {
            Span<GridOffset> offsets = stackalloc GridOffset[GridSidePointsUsed.AdjacentOffsets];
            sidesUsed.FillWithAdjacentGridOffsets(offsets);

            for (int i = 0; i < offsets.Length; i++)
            {
                if (pos.TryMove(offsets[i], out var adjacent))
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

        public void AddVoxelGridHir(GridPos gridPos, VoxelGridHierarchy gridHir)
        {
            PosToVoxelGridHir.TryAdd(gridPos, gridHir);
        }

        public bool IsMustGenerate(GridPos gridPos)
        {
            return MustGenerate.ContainsKey(gridPos);
        }
    }
}
