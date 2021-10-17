using OpenGL;
using System;
using System.Numerics;
using System.Threading;
using VoxelWorld.Voxel;
using VoxelWorld.Voxel.Grid;
using VoxelWorld.Voxel.System;

namespace VoxelWorld.Voxel.Hierarchy
{
    internal class VoxelHierarchy : IDisposable
    {
        private static readonly Vector3[] GridLocations = new Vector3[]
        {
            new Vector3(-1, -1, -1),
            new Vector3( 1, -1, -1),
            new Vector3(-1,  1, -1),
            new Vector3( 1,  1, -1),
            new Vector3(-1, -1,  1),
            new Vector3( 1, -1,  1),
            new Vector3(-1,  1,  1),
            new Vector3( 1,  1,  1)
        };
        public static readonly GridOffset[] GridPosOffsets = new GridOffset[]
        {
            new GridOffset(0, 0, 0),
            new GridOffset(1, 0, 0),
            new GridOffset(0, 1, 0),
            new GridOffset(1, 1, 0),
            new GridOffset(0, 0, 1),
            new GridOffset(1, 0, 1),
            new GridOffset(0, 1, 1),
            new GridOffset(1, 1, 1)
        };

        //keeps track of sub hierarchies
        private readonly VoxelGridHierarchy[] SubHierarchyGrids = new VoxelGridHierarchy[GridLocations.Length];

        public bool IsHollow = false;

        public VoxelHierarchy(Vector3 center, VoxelSystemData genData, GridPos gridPos)
        {
            for (int i = 0; i < SubHierarchyGrids.Length; i++)
            {
                Vector3 gridCenter = GetGridCenter(i, center, genData);
                SubHierarchyGrids[i] = new VoxelGridHierarchy(gridCenter, genData.GridSize, genData.VoxelSize);

                GridPos subGridPos = gridPos.Move(in GridPosOffsets[i]);
                genData.AddVoxelGridHir(in subGridPos, SubHierarchyGrids[i]);
            }
        }

        private Vector3 GetGridCenter(int index, Vector3 center, VoxelSystemData genData)
        {
            return center + GridLocations[index] * 0.5f * (genData.GridSize - 2) * genData.VoxelSize;
        }

        public BoundingCircle Generate(Vector3 center, VoxelSystemData genData, VoxelGrid grid, GridPos gridPos)
        {
            Span<bool> isUsingSubHir = stackalloc bool[GridPosOffsets.Length];
            isUsingSubHir.Fill(true);
            return Generate(center, genData, grid, gridPos, isUsingSubHir);
        }

        public BoundingCircle Generate(Vector3 center, VoxelSystemData genData, VoxelGrid grid, GridPos gridPos, Span<bool> isUsingSubHir)
        {
            BoundingCircle circle = new BoundingCircle(center, 0);
            for (int i = 0; i < SubHierarchyGrids.Length; i++)
            {
                //isUsingSubHir is a guess made from the parent grids information
                //about whether it's empty or not. But the guess might be wrong
                //which is why it also checks if it should ignore that it's empty.
                if (!isUsingSubHir[i] && !SubHierarchyGrids[i].IgnoreIsEmpty())
                {
                    SubHierarchyGrids[i].InitializeGridAsEmpty();
                    continue;
                }

                GridPos subGridPos = gridPos.Move(in GridPosOffsets[i]);
                SubHierarchyGrids[i].GenerateGrid(genData, grid, subGridPos);
                if (!SubHierarchyGrids[i].IsEmpty())
                {
                    circle = circle.AddBoundingCircle(SubHierarchyGrids[i].GetBoundingCircle());
                }
            }

            return circle;
        }

        public bool IsEmpty()
        {
            for (int i = 0; i < SubHierarchyGrids.Length; i++)
            {
                if (!SubHierarchyGrids[i].IsEmpty())
                {
                    return false;
                }
            }

            return true;
        }

        public void CheckAndIncreaseResolution(Frustum renderCheck, ModelTransformations modelTrans, VoxelSystemData genData, in GridPos gridPos)
        {
            IsHollow = false;

            GridPos subGridPos = gridPos.GoDownTree();
            for (int i = 0; i < SubHierarchyGrids.Length; i++)
            {
                GridPos movedSubGridPos = subGridPos.Move(in GridPosOffsets[i]);
                SubHierarchyGrids[i].CheckAndIncreaseResolution(renderCheck, modelTrans, genData, in movedSubGridPos);
            }
        }

        public void MakeHollow()
        {
            if (IsHollow)
            {
                return;
            }

            IsHollow = true;
            for (int i = 0; i < SubHierarchyGrids.Length; i++)
            {
                SubHierarchyGrids[i].MakeHollow();
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < SubHierarchyGrids.Length; i++)
            {
                SubHierarchyGrids[i].Dispose();
                SubHierarchyGrids[i] = null;
            }
        }
    }
}
