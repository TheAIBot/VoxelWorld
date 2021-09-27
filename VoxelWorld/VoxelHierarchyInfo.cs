using OpenGL;
using System;
using System.Diagnostics;
using System.Numerics;

namespace VoxelWorld
{
    internal struct VoxelHierarchyInfo
    {
        private Vector3 Center;
        private VoxelHierarchy VoxelHir;
        public bool IsEmpty;
        public GenerationStatus GenStatus { get; private set; }
        public bool IsHollow { get; private set; }
        private float BoundingCircleRadius;

        private bool HasBeenDisposed;

        public VoxelHierarchyInfo(Vector3 center, int gridSize, float voxelSize)
        {
            this.Center = center;
            this.VoxelHir = null;
            this.IsEmpty = false;
            this.GenStatus = GenerationStatus.NotGenerated;
            this.IsHollow = true;
            this.BoundingCircleRadius = (gridSize / 2) * voxelSize;
            this.HasBeenDisposed = false;
        }

        public void StartGenerating(VoxelSystemData genData, VoxelGridHierarchy gridHir)
        {
            Debug.Assert(GenStatus == GenerationStatus.NotGenerated);
            Debug.Assert(VoxelHir == null);

            GenStatus = GenerationStatus.Generating;
            IsHollow = false;

            WorkLimiter.QueueWork(new WorkInfo(gridHir, genData, VoxelType.Hierarchy));
        }

        public void EndGenerating(VoxelSystemData genData, VoxelGridHierarchy gridHir, VoxelGrid grid)
        {
            if (IsHollow)
            {
                GenStatus = GenerationStatus.NotGenerated;
                return;
            }


            VoxelHierarchy hir = new VoxelHierarchy(Center, genData);
            var hirData = hir.Generate(Center, genData, grid);
            BoundingCircleRadius = hirData.Radius;

            if (hir.IsEmpty())
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

        public bool ShouldGenerate()
        {
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
            if (IsEmpty)
            {
                return false;
            }

            Vector3 newCenter = modelTrans.RevRotation * Center + modelTrans.Translation;
            if (!onScreenCheck.Intersects(new BoundingCircle(newCenter, BoundingCircleRadius)))
            {
                return false;
            }

            return true;
        }

        public void CheckAndIncreaseResolution(Frustum renderCheck, ModelTransformations modelTrans, VoxelSystemData genData)
        {
            IsHollow = false;
            VoxelHir.CheckAndIncreaseResolution(renderCheck, modelTrans, genData.GetOneDown());
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
