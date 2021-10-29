using OpenGL;
using System;
using System.Diagnostics;
using System.Numerics;
using VoxelWorld.Voxel.Grid;
using VoxelWorld.Voxel.System;

namespace VoxelWorld.Voxel.Hierarchy
{
    internal struct VoxelHierarchyInfo
    {
        private Vector3 Center;
        private VoxelHierarchy VoxelHir;
        public bool IsEmpty;
        public bool IgnoreIsEmpty;
        public GenerationStatus GenStatus { get; private set; }
        public bool IsHollow { get; private set; }
        private float BoundingCircleRadius;

        private bool HasBeenDisposed;

        public VoxelHierarchyInfo(Vector3 center, int gridSize, float voxelSize)
        {
            Center = center;
            VoxelHir = null;
            IsEmpty = false;
            IgnoreIsEmpty = false;
            GenStatus = GenerationStatus.NotGenerated;
            IsHollow = true;
            BoundingCircleRadius = gridSize / 2 * voxelSize;
            HasBeenDisposed = false;
        }

        public void StartGenerating(VoxelSystemData genData, VoxelGridHierarchy gridHir, in GridPos gridPos)
        {
            Debug.Assert(GenStatus == GenerationStatus.NotGenerated);
            Debug.Assert(VoxelHir == null);

            GenStatus = GenerationStatus.Generating;
            IsHollow = false;

            WorkLimiter.QueueWork(new WorkInfo(gridHir, genData, in gridPos, VoxelType.Hierarchy));
        }

        public void EndGenerating(VoxelSystemData genData, VoxelGridHierarchy gridHir, VoxelGrid grid, GridPos gridPos, bool[] isUsingSubHir)
        {
            if (IsHollow)
            {
                GenStatus = GenerationStatus.NotGenerated;
                return;
            }


            VoxelHierarchy hir = new VoxelHierarchy(Center, genData, gridPos);
            var hirData = hir.Generate(Center, genData, grid, gridPos, isUsingSubHir);
            BoundingCircleRadius = hirData.Radius;

            if (!IgnoreIsEmpty && hir.IsEmpty())
            {
                IsEmpty = true;
                hir.Dispose();
                GenStatus = GenerationStatus.HasBeenGenerated;
                return;
            }

            lock (gridHir)
            {
                if (HasBeenDisposed)
                {
                    hir?.Dispose();
                    hir = null;
                }
                else
                {
                    if (IsHollow)
                    {
                        hir.MakeHollow();
                    }

                    Debug.Assert(VoxelHir == null);
                    VoxelHir = hir;
                }

                GenStatus = GenerationStatus.HasBeenGenerated;
            }
        }

        public bool ShouldGenerate(VoxelGridHierarchy gridHir)
        {
            //It has been generated as empty but now needs to be generated
            //again while ignoring that it's empty
            if (IgnoreIsEmpty && IsEmpty && GenStatus == GenerationStatus.HasBeenGenerated)
            {
                IsEmpty = false;
                GenStatus = GenerationStatus.NotGenerated;
                lock (gridHir)
                {
                    VoxelHir?.Dispose();
                    VoxelHir = null;
                }
                return true;
            }

            if (GenStatus != GenerationStatus.NotGenerated)
            {
                return false;
            }

            if (IsEmpty)
            {
                return false;
            }

            return true;
        }

        public bool CanSee(Frustum onScreenCheck, ModelTransformations modelTrans)
        {
            if (!IgnoreIsEmpty && IsEmpty)
            {
                return false;
            }

            //Vector3 newCenter = modelTrans.RevRotation * Center + modelTrans.Translation;
            //if (!onScreenCheck.Intersects(new BoundingCircle(newCenter, BoundingCircleRadius)))
            //{
            //    return false;
            //}

            return true;
        }

        public void CheckAndIncreaseResolution(Frustum renderCheck, ModelTransformations modelTrans, VoxelSystemData genData, in GridPos gridPos)
        {
            IsHollow = false;
            VoxelHir.CheckAndIncreaseResolution(renderCheck, modelTrans, genData, in gridPos);
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

                VoxelHir?.MakeHollow();
            }
        }

        public void MarkMustGenerate()
        {
            IgnoreIsEmpty = true;
        }

        public void Dispose(VoxelGridHierarchy gridHir)
        {
            lock (gridHir)
            {
                HasBeenDisposed = true;
                VoxelHir?.Dispose();
            }
        }
    }
}
