using OpenGL;
using System;
using System.Diagnostics;
using System.Numerics;

namespace VoxelWorld
{
    internal enum GenerationStatus : byte
    {
        NotGenerated,
        Generating,
        HasBeenGenerated
    }

    internal struct VoxelHierarchyInfo
    {
        private Vector3 Center;
        private VoxelHierarchy VoxelHir;
        public bool IsEmpty;
        public GenerationStatus GenStatus { get; private set; }
        public bool IsHollow { get; private set; }
        private float BoundingCircleRadius;
        private GridNormal Normal;

        private bool HasBeenDisposed;

        public VoxelHierarchyInfo(Vector3 center, int gridSize, float voxelSize)
        {
            this.Center = center;
            this.VoxelHir = null;
            this.IsEmpty = false;
            this.GenStatus = GenerationStatus.NotGenerated;
            this.IsHollow = true;
            this.BoundingCircleRadius = (gridSize / 2) * voxelSize;
            this.Normal = new GridNormal();
            this.HasBeenDisposed = false;
        }

        public void StartGenerating(VoxelSystemData genData, Vector3 rotatedLookDir, VoxelGridHierarchy gridHir)
        {
            Debug.Assert(GenStatus == GenerationStatus.NotGenerated);
            Debug.Assert(VoxelHir == null);

            GenStatus = GenerationStatus.Generating;
            IsHollow = false;

            WorkLimiter.QueueWork(new WorkInfo(gridHir, genData, rotatedLookDir, VoxelType.Hierarchy));
        }

        public void EndGenerating(VoxelSystemData genData, Vector3 rotatedLookDir, VoxelGridHierarchy gridHir)
        {
            if (IsHollow)
            {
                GenStatus = GenerationStatus.NotGenerated;
                return;
            }


            VoxelHierarchy hir = new VoxelHierarchy(Center, genData);
            var hirData = hir.Generate(Center, rotatedLookDir, genData);
            BoundingCircleRadius = hirData.Item1.Radius;
            Normal = hirData.Item2;

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

            if (GenStatus == GenerationStatus.HasBeenGenerated && !Normal.CanSee(modelTrans.RotatedLookDir))
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
