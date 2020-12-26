using OpenGL;
using System;
using System.Collections;
using System.Diagnostics;
using System.Numerics;
using System.Threading;

namespace VoxelWorld
{
    internal class VoxelGridHierarchy : IDisposable
    {
        public VoxelGridInfo Grid;
        public VoxelHierarchyInfo Hierarchy;

        public VoxelGridHierarchy(Vector3 center, int gridSize, float voxelSize)
        {
            this.Grid = new VoxelGridInfo(center);
            this.Hierarchy = new VoxelHierarchyInfo(center, gridSize, voxelSize);
        }

        public void GenerateGrid(VoxelSystemData genData)
        {
            Grid.Generate(genData, this);
        }

        public void EndGeneratingGrid(VoxelSystemData genData)
        {
            Grid.EndGenerating(genData, this);
        }

        public void EndGeneratingHierarchy(VoxelSystemData genData)
        {
            Hierarchy.EndGenerating(genData, this);
        }

        private bool IsHighEnoughResolution(Vector3 voxelCenter, ModelTransformations modelTrans, VoxelSystemData genData)
        {
            Vector3 a = modelTrans.Translation + (modelTrans.RevRotation * voxelCenter);
            Vector3 c = modelTrans.CameraPos;

            float distance = (a - c).Length();
            distance = MathF.Pow(distance, 1.2f);

            float spaceLength = MathF.Tan(modelTrans.FOV) * distance * 2.0f;

            return genData.VoxelSize / spaceLength < 0.0015f;
        }

        public void CheckAndIncreaseResolution(Frustum renderCheck, ModelTransformations modelTrans, VoxelSystemData genData)
        {
            if (Grid.IsBeingGenerated)
            {
                return;
            }
            if (Hierarchy.GenStatus == GenerationStatus.Generating)
            {
                return;
            }

            if (!Hierarchy.IsHollow && !Hierarchy.CanSee(renderCheck, modelTrans))
            {
                Hierarchy.MakeHollow(this);
                return;
            }

            if (IsHighEnoughResolution(Grid.GridCenter, modelTrans, genData))
            {
                if (Grid.CanSee(renderCheck, modelTrans))
                {
                    if (Grid.IsReadyToDraw())
                    {
                        if (!Hierarchy.IsHollow)
                        {
                            Hierarchy.MakeHollow(this);
                        }
                    }
                    else
                    {
                        if (Grid.ShouldGenerate())
                        {
                            Grid.StartGenerating(genData, this);
                        }
                    }
                }
                else
                {
                    Grid.MakeHollow(this);
                }
            }
            else
            {
                if (Hierarchy.CanSee(renderCheck, modelTrans))
                {
                    if (Hierarchy.ShouldGenerate())
                    {
                        Hierarchy.StartGenerating(genData.GetOneDown(), this);
                    }
                    else
                    {
                        Grid.MakeHollow(this);
                        Hierarchy.CheckAndIncreaseResolution(renderCheck, modelTrans, genData);
                    }
                }
            }
        }

        public void MakeHollow()
        {
            Grid.MakeHollow(this);
            Hierarchy.MakeHollow(this);
        }

        public void Dispose()
        {
            Grid.Dispose(this);
            Hierarchy.Dispose(this);
        }
    }

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

        public void Generate(VoxelSystemData genData, VoxelGridHierarchy gridHir)
        {
            Debug.Assert(IsBeingGenerated == false);

            IsBeingGenerated = true;
            IsHollow = false;

            EndGenerating(genData, gridHir);
        }

        public void StartGenerating(VoxelSystemData genData, VoxelGridHierarchy gridHir)
        {
            Debug.Assert(IsBeingGenerated == false);

            IsBeingGenerated = true;
            IsHollow = false;

            WorkLimiter.QueueWork(new WorkInfo(gridHir, genData, VoxelType.Grid));
        }

        public void EndGenerating(VoxelSystemData genData, VoxelGridHierarchy gridHir)
        {
            //no need to do the work if it's already hollow again
            if (IsHollow)
            {
                IsBeingGenerated = false;
                return;
            }

            VoxelGrid grid = VoxelGridStorage.GetGrid(GridCenter, genData);
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
                    VoxelGridStorage.StoreForReuse(grid);
                    return;
                }

                grid.Interpolate();
                VoxelsAtEdge = grid.EdgePointsUsed();
                BoundingCircleRadius = grid.GetBoundingCircle().Radius;
                CompressedGrid = grid.GetCompressed();
            }
            else
            {
                grid.Restore(CompressedGrid);
                grid.PreCalculateGeometryData();
                grid.Interpolate();
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
