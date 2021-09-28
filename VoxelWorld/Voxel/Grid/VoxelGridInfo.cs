using OpenGL;
using System.Collections;
using System.Diagnostics;
using System.Numerics;
using System.Threading;

namespace VoxelWorld
{
    internal struct VoxelGridInfo
    {
        public readonly Vector3 GridCenter;
        public bool IsBeingGenerated { get; private set; }
        public bool IsEmpty { get; private set; }
        public bool VoxelsAtEdge { get; private set; }
        public BoundingCircle BoundingBox { get { return new BoundingCircle(GridCenter, BoundingCircleRadius); } }

        private float BoundingCircleRadius;
        private bool MadeDrawable;
        private bool IsHollow;
        private bool Initialized;
        private bool HasBeenDisposed;
        private BitArray CompressedGrid;

        public static int DrawCalls = 0;

        public VoxelGridInfo(Vector3 center)
        {
            this.GridCenter = center;
            this.IsBeingGenerated = false;
            this.IsEmpty = false;
            this.VoxelsAtEdge = false;
            this.BoundingCircleRadius = 0.0f;
            this.MadeDrawable = false;
            this.IsHollow = true;
            this.Initialized = false;
            this.HasBeenDisposed = false;
            this.CompressedGrid = null;
        }

        public void Generate(VoxelSystemData genData, VoxelGridHierarchy gridHir, VoxelGrid grid, GridPos gridPos)
        {
            Debug.Assert(IsBeingGenerated == false);

            IsBeingGenerated = true;
            IsHollow = false;

            EndGenerating(genData, gridHir, grid, gridPos);
        }

        public void StartGenerating(VoxelSystemData genData, VoxelGridHierarchy gridHir, GridPos gridPos)
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

                grid.PreCalculateGeometryData();
                if (grid.IsEmpty())
                {
                    IsEmpty = true;
                    Initialized = true;
                    IsBeingGenerated = false;
                    return;
                }

                grid.Interpolate();

                BoundingCircleRadius = grid.GetBoundingCircle().Radius;
                CompressedGrid = grid.GetCompressed();

                GridSidePointsUsed sidesUsed = grid.EdgePointsUsed();
                VoxelsAtEdge = sidesUsed.IsAnyUsed();
                genData.MarkMustGenerateSurroundings(sidesUsed, gridPos);
            }
            else
            {
                grid.Restore(CompressedGrid);
                grid.PreCalculateGeometryData();
                grid.Interpolate();
            }

            var meshData = grid.Triangulize();
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
                    MainThreadWork.MakeGridDrawable(gridHir, meshData);
                    MadeDrawable = true;
                }
            }

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

            Vector3 newCenter = modelTrans.RevRotation * GridCenter + modelTrans.Translation;
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
                    MainThreadWork.RemoveDrawableGrid(gridHir);
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
                    MainThreadWork.RemoveDrawableGrid(gridHir);
                    MadeDrawable = false;
                }
            }
        }
    }
}
