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

        public void GenerateGrid(VoxelSystemData genData, Vector3 rotatedLookDir)
        {
            Grid.Generate(genData, rotatedLookDir, this);
        }

        public void EndGeneratingGrid(VoxelSystemData genData, Vector3 rotatedLookDir)
        {
            Grid.EndGenerating(genData, rotatedLookDir, this);
        }

        public void EndGeneratingHierarchy(VoxelSystemData genData, Vector3 rotatedLookDir)
        {
            Hierarchy.EndGenerating(genData, rotatedLookDir, this);
        }

        private bool IsHighEnoughResolution(Vector3 voxelCenter, ModelTransformations modelTrans, VoxelSystemData genData)
        {
            Vector3 a = modelTrans.Translation + (modelTrans.RevRotation * voxelCenter);
            Vector3 c = modelTrans.CameraPos; // rotate cameraPos instead of center because rotate center need inverse modelRotate

            float resolution = (genData.VoxelSize * 100.0f) / (a - c).Length();
            return resolution < 0.3f;
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
                            Grid.StartGenerating(genData, modelTrans.RotatedLookDir, this);
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
                        Hierarchy.StartGenerating(genData.GetOneDown(), modelTrans.RotatedLookDir, this);
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
        public GridNormal Normal { get; private set; }
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
            this.Normal = new GridNormal();
            this.BoundingCircleRadius = 0.0f;
            this.MadeDrawable = false;
            this.IsHollow = true;
            this.Initialized = false;
            this.HasBeenDisposed = false;
            this.CompressedGrid = null;
        }

        public void Generate(VoxelSystemData genData, Vector3 rotatedLookDir, VoxelGridHierarchy gridHir)
        {
            Debug.Assert(IsBeingGenerated == false);

            IsBeingGenerated = true;
            IsHollow = false;

            EndGenerating(genData, rotatedLookDir, gridHir);
        }

        public void StartGenerating(VoxelSystemData genData, Vector3 rotatedLookDir, VoxelGridHierarchy gridHir)
        {
            Debug.Assert(IsBeingGenerated == false);

            IsBeingGenerated = true;
            IsHollow = false;

            WorkLimiter.QueueWork(new WorkInfo(gridHir, genData, rotatedLookDir, VoxelType.Grid));
        }

        public void EndGenerating(VoxelSystemData genData, Vector3 rotatedLookDir, VoxelGridHierarchy gridHir)
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
                Normal = grid.GetGridNormal();
                CompressedGrid = grid.GetCompressed();
            }
            else
            {
                grid.Restore(CompressedGrid);
                grid.PreCalculateGeometryData();
                grid.Interpolate();
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

            if (!Normal.CanSee(modelTrans.RotatedLookDir))
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
