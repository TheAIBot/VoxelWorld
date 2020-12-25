using OpenGL;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Threading;

namespace VoxelWorld
{

    internal class VoxelHierarchy : IDisposable
    {
        private static readonly Vector3I[] GridLocations = new Vector3I[]
        {
            new Vector3I(-1, -1, -1),
            new Vector3I( 1, -1, -1),
            new Vector3I(-1,  1, -1),
            new Vector3I( 1,  1, -1),
            new Vector3I(-1, -1,  1),
            new Vector3I( 1, -1,  1),
            new Vector3I(-1,  1,  1),
            new Vector3I( 1,  1,  1)
        };

        //keeps track of sub hierarchies
        private readonly VoxelGridHierarchy[] SubHierarchyGrids = new VoxelGridHierarchy[GridLocations.Length];

        public bool IsHollow = false;

        public VoxelHierarchy(Vector3 center, VoxelSystemData genData)
        {
            for (int i = 0; i < SubHierarchyGrids.Length; i++)
            {
                Vector3 gridCenter = GetGridCenter(i, center, genData);
                SubHierarchyGrids[i] = new VoxelGridHierarchy(gridCenter, genData.GridSize, genData.VoxelSize);
            }
        }

        private Vector3 GetGridCenter(int index, Vector3 center, VoxelSystemData genData)
        {
            return center + GridLocations[index].AsFloatVector3() * 0.5f * (genData.GridSize - 2) * genData.VoxelSize;
        }

        public BoundingCircle Generate(Vector3 center, VoxelSystemData genData)
        {
            BoundingCircle circle = new BoundingCircle(center, 0);
            for (int i = 0; i < GridLocations.Length; i++)
            {
                SubHierarchyGrids[i].GenerateGrid(genData);
                if (!SubHierarchyGrids[i].Grid.IsEmpty)
                {
                    circle = circle.AddBoundingCircle(SubHierarchyGrids[i].Grid.BoundingBox);
                }
            }

            return circle;
        }

        public bool IsEmpty()
        {
            for (int i = 0; i < SubHierarchyGrids.Length; i++)
            {
                if (!SubHierarchyGrids[i].Grid.IsEmpty)
                {
                    return false;
                }
            }

            return true;
        }

        public void CheckAndIncreaseResolution(Frustum renderCheck, ModelTransformations modelTrans, VoxelSystemData genData)
        {
            IsHollow = false;

            for (int i = 0; i < GridLocations.Length; i++)
            {
                SubHierarchyGrids[i].CheckAndIncreaseResolution(renderCheck, modelTrans, genData);
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
