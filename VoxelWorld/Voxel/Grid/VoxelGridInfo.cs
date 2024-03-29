using System;
using System.Collections;
using System.Diagnostics;
using System.Numerics;
using VoxelWorld.Render.Box;
using VoxelWorld.Render.VoxelGrid;
using VoxelWorld.Voxel.Hierarchy;
using VoxelWorld.Voxel.System;

namespace VoxelWorld.Voxel.Grid
{
    internal struct VoxelGridInfo
    {
        public readonly Vector3 GridCenter;
        public bool IsBeingGenerated { get; private set; }
        public bool IsEmpty { get; private set; }
        public bool VoxelsAtEdge { get; private set; }
        public BoundingCircle BoundingBox { get { return new BoundingCircle(GridCenter, BoundingCircleRadius); } }
        public readonly bool[] IsSubGridUsed;
        public bool HasBeenGenerated => Initialized;

        private float BoundingCircleRadius;
        private bool MadeDrawable;
        private bool IsHollow;
        private bool Initialized;
        private bool HasBeenDisposed;
        private BitArray CompressedGrid;
        private int VertexCount;
        private int TriangleCount;

        public static int DrawCalls = 0;
        public static int GeneratedNotEmpty = 0;
        public static int GeneratedEmpty = 0;

        public VoxelGridInfo(Vector3 center)
        {
            GridCenter = center;
            IsBeingGenerated = false;
            IsEmpty = false;
            VoxelsAtEdge = false;
            IsSubGridUsed = new bool[VoxelHierarchy.GridPosOffsets.Length];
            Array.Fill(IsSubGridUsed, true);
            BoundingCircleRadius = 0.0f;
            MadeDrawable = false;
            IsHollow = true;
            Initialized = false;
            HasBeenDisposed = false;
            CompressedGrid = null;
            VertexCount = 0;
            TriangleCount = 0;
        }

        public void Generate(VoxelSystemData genData, VoxelGridHierarchy gridHir, VoxelGrid grid, GridPos gridPos)
        {
            Debug.Assert(IsBeingGenerated == false);

            IsBeingGenerated = true;
            IsHollow = false;

            EndGenerating(genData, gridHir, grid, gridPos);
        }

        public void StartGenerating(VoxelSystemData genData, VoxelGridHierarchy gridHir, in GridPos gridPos)
        {
            Debug.Assert(IsBeingGenerated == false);

            IsBeingGenerated = true;
            IsHollow = false;

            WorkLimiter.QueueWork(new WorkInfo(gridHir, genData, gridPos, VoxelType.Grid));
        }

        public void EndGenerating(VoxelSystemData genData, VoxelGridHierarchy gridHir, VoxelGrid grid, GridPos gridPos)
        {
            //no need to do the work if it's already hollow again
            if (IsHollow)
            {
                IsBeingGenerated = false;
                return;
            }

            grid.Repurpose(GridCenter, genData);

            if (!Initialized)
            {
                Initialized = true;

                grid.Randomize();

                (VertexCount, TriangleCount) = grid.PreCalculateGeometryData();
                if (TriangleCount == 0)
                {
                    //Interlocked.Increment(ref GeneratedEmpty);
                    InitializeAsEmpty();
                    return;
                }

                //Interlocked.Increment(ref GeneratedNotEmpty);
                grid.Interpolate();

                BoundingCircleRadius = grid.GetBoundingCircle().Radius;
                CompressedGrid = grid.GetCompressed();

                GridSidePointsUsed sidesUsed = grid.EdgePointsUsed();
                VoxelsAtEdge = sidesUsed.IsAnyUsed();

                for (int i = 0; i < VoxelHierarchy.GridPosOffsets.Length; i++)
                {
                    IsSubGridUsed[i] = grid.SubGridEdgePointsUsed(VoxelHierarchy.GridPosOffsets[i]);
                }

                genData.MarkMustGenerateSurroundings(sidesUsed, in gridPos);
            }
            else
            {
                grid.Restore(CompressedGrid);
                grid.Interpolate();
            }

            var meshData = grid.Triangulize(VertexCount, TriangleCount);
            //var boxData = BoxGeometry.MakeBoxGeometry(BoundingBox.Min, BoundingBox.Max);

            //no need to make vaos if the grid is already hollow again
            if (IsHollow)
            {
                IsBeingGenerated = false;
                meshData.Reuse();
                return;
            }

            lock (gridHir)
            {
                if (HasBeenDisposed || IsHollow)
                {
                    meshData.Reuse();
                }
                else
                {
                    VoxelGridRenderManager.MakeGridDrawable(gridHir, meshData);
                    BoxRenderManager.AddBox(in GridCenter, genData.VoxelSize * genData.GridSize);
                    MadeDrawable = true;
                }
            }

            IsBeingGenerated = false;
        }

        public void InitializeAsEmpty()
        {
            IsEmpty = true;
            Initialized = true;
            IsBeingGenerated = false;
        }

        public bool ShouldGenerate()
        {
            if (IsBeingGenerated)
            {
                return false;
            }

            if (IsEmpty)
            {
                return false;
            }

            if (!IsHollow)
            {
                return false;
            }

            return true;
        }

        public bool CanSee(Frustum onScreenCheck, ModelTransformations modelTrans)
        {
            if (IsEmpty)
            {
                return false;
            }

            Vector3 newCenter = Vector3.Transform(GridCenter, modelTrans.Rotation) + modelTrans.Translation;
            if (!onScreenCheck.Intersects(new BoundingCircle(newCenter, BoundingCircleRadius)))
            {
                return false;
            }

            return true;
        }

        public bool IsReadyToDraw()
        {
            return !IsHollow;
        }

        public void MakeHollow(VoxelGridHierarchy gridHir)
        {
            if (IsHollow)
            {
                return;
            }

            lock (gridHir)
            {
                IsHollow = true;

                if (MadeDrawable)
                {
                    VoxelGridRenderManager.RemoveDrawableGrid(gridHir);
                    BoxRenderManager.RemoveBox(in GridCenter);
                    MadeDrawable = false;
                }
            }
        }

        public void Dispose(VoxelGridHierarchy gridHir)
        {
            lock (gridHir)
            {
                HasBeenDisposed = true;

                if (MadeDrawable)
                {
                    VoxelGridRenderManager.RemoveDrawableGrid(gridHir);
                    BoxRenderManager.RemoveBox(in GridCenter);
                    MadeDrawable = false;
                }
            }
        }
    }
}
