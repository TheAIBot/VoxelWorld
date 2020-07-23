using OpenGL;
using System;
using System.Diagnostics;
using System.Numerics;

namespace VoxelWorld
{
    internal class VoxelGridInfo : IDisposable
    {
        public readonly Vector3 GridCenter;
        public bool IsBeingGenerated { get; private set; }
        public bool IsEmpty { get; private set; } = false;
        public bool VoxelsAtEdge { get; private set; } = false;
        public AxisAlignedBoundingBox BoundingBox { get; private set; } = null;
        public GridNormal Normal { get; private set; }

        private bool MadeDrawable = false;
        private bool IsHollow = true;
        private bool Initialized = false;
        private readonly object DisposeLock = new object();
        private bool HasBeenDisposed = false;

        public static int DrawCalls = 0;

        public VoxelGridInfo(Vector3 center)
        {
            this.GridCenter = center;
        }

        public Action GenerateGridAction(int size, float voxelSize, Func<Vector3, float> gen, Vector3 rotatedLookDir)
        {
            Debug.Assert(IsBeingGenerated == false);

            IsBeingGenerated = true;
            IsHollow = false;
            return () =>
            {
                //no need to do the work if it's already hollow again
                if (IsHollow)
                {
                    IsBeingGenerated = false;
                    return;
                }

                VoxelGrid grid = VoxelGridStorage.GetGrid(size, GridCenter, voxelSize, gen);
                grid.Randomize();

                grid.PreCalculateGeometryData();
                if (grid.IsEmpty())
                {
                    IsEmpty = true;
                    Initialized = true;
                    IsBeingGenerated = false;
                    VoxelGridStorage.StoreForReuse(grid);
                    return;
                }

                grid.Interpolate();
                if (!Initialized)
                {
                    Initialized = true;
                    VoxelsAtEdge = grid.EdgePointsUsed();
                    BoundingBox = grid.GetBoundingBox();
                    Normal = grid.GetGridNormal();
                }

                if (!Normal.CanSee(rotatedLookDir))
                {
                    IsBeingGenerated = false;
                    VoxelGridStorage.StoreForReuse(grid);
                    return;
                }

                var meshData = grid.Triangulize();
                //var boxData = BoxGeometry.MakeBoxGeometry(BoundingBox.Min, BoundingBox.Max);

                //set grid to null here to make sure it isn't captured in the lambda in the future
                //as using the grid after storing it would be a problem
                VoxelGridStorage.StoreForReuse(grid);
                grid = null;


                //no need to make vaos if the grid is already hollow again
                if (IsHollow)
                {
                    IsBeingGenerated = false;
                    meshData.Reuse();
                    return;
                }

                lock (DisposeLock)
                {
                    if (HasBeenDisposed || IsHollow)
                    {
                        meshData.Reuse();
                    }
                    else
                    {
                        MainThreadWork.MakeGridDrawable(this, meshData);
                        MadeDrawable = true;
                    }
                }

                IsBeingGenerated = false;
            };
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

            if (!Normal.CanSee(modelTrans.RotatedLookDir))
            {
                return false;
            }

            if (!onScreenCheck.Intersects(BoundingBox))
            {
                return false;
            }

            return true;
        }

        public bool IsReadyToDraw()
        {
            return !IsHollow;
        }

        public void MakeHollow()
        {
            if (IsHollow)
            {
                return;
            }

            lock (DisposeLock)
            {
                IsHollow = true;

                if (MadeDrawable)
                {
                    MainThreadWork.RemoveDrawableGrid(this);
                    MadeDrawable = false;
                }
            }
        }

        public void Dispose()
        {
            lock (DisposeLock)
            {
                HasBeenDisposed = true;

                if (MadeDrawable)
                {
                    MainThreadWork.RemoveDrawableGrid(this);
                    MadeDrawable = false;
                }
            }
        }
    }
}
