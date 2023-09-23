using System;
using System.Numerics;
using VoxelWorld.Voxel.Grid;
using VoxelWorld.Voxel.System;

namespace VoxelWorld.Voxel.Hierarchy
{
    internal sealed class VoxelGridHierarchy : IDisposable
    {
        private VoxelGridInfo Grid;
        private VoxelHierarchyInfo Hierarchy;

        public VoxelGridHierarchy(Vector3 center, int gridSize, float voxelSize)
        {
            Grid = new VoxelGridInfo(center);
            Hierarchy = new VoxelHierarchyInfo(center, gridSize, voxelSize);
        }

        public void GenerateGrid(VoxelSystemData genData, VoxelGrid grid, GridPos gridPos)
        {
            Grid.Generate(genData, this, grid, gridPos);
        }

        public void EndGeneratingGrid(VoxelSystemData genData, VoxelGrid grid, GridPos gridPos)
        {
            Grid.EndGenerating(genData, this, grid, gridPos);
        }

        public void InitializeGridAsEmpty()
        {
            Grid.InitializeAsEmpty();
        }

        public void EndGeneratingHierarchy(VoxelSystemData genData, VoxelGrid grid, GridPos gridPos)
        {
            bool[] isUsingSubHir = null;
            if (Grid.HasBeenGenerated)
            {
                isUsingSubHir = Grid.IsSubGridUsed;
            }
            else
            {
                isUsingSubHir = new bool[VoxelHierarchy.GridPosOffsets.Length];
                Array.Fill(isUsingSubHir, true);
            }
            Hierarchy.EndGenerating(genData, this, grid, gridPos, isUsingSubHir);
        }

        private bool IsHighEnoughResolution(Vector3 voxelCenter, ModelTransformations modelTrans, VoxelSystemData genData)
        {
            Vector3 a = modelTrans.Translation + Vector3.Transform(voxelCenter, modelTrans.Rotation);
            Vector3 c = modelTrans.CameraPos;

            float distance = (a - c).Length();
            distance = MathF.Pow(distance, 1.2f);

            float spaceLength = MathF.Tan(modelTrans.FOV) * distance * 2.0f;

            return genData.VoxelSize / spaceLength < 0.0015f;
        }

        public void CheckAndIncreaseResolution(Frustum renderCheck, ModelTransformations modelTrans, VoxelSystemData genData, in GridPos gridPos)
        {
            if (Grid.IsBeingGenerated)
            {
                return;
            }
            if (Hierarchy.GenStatus == GenerationStatus.Generating)
            {
                return;
            }

            //This right here actually sucks. Basically i have no way
            //of making sure that the hierarchy knows if it must be
            //generated, so instead i just check every time
            if (Hierarchy.IsEmpty && genData.IsMustGenerate(in gridPos))
            {
                MarkMustGenerate();
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
                            Grid.StartGenerating(genData, this, in gridPos);
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
                    var subHirGenData = genData.GetWithHalfVoxelSize();
                    GridPos subGridHirPos = gridPos.GoDownTree();
                    if (Hierarchy.ShouldGenerate(this))
                    {
                        Hierarchy.StartGenerating(subHirGenData, this, in subGridHirPos);
                    }
                    else
                    {
                        Grid.MakeHollow(this);
                        Hierarchy.CheckAndIncreaseResolution(renderCheck, modelTrans, subHirGenData, in subGridHirPos);
                    }
                }
            }
        }

        public void MakeHollow()
        {
            Grid.MakeHollow(this);
            Hierarchy.MakeHollow(this);
        }

        public void MarkMustGenerate()
        {
            Hierarchy.MarkMustGenerate();
        }

        public bool IsEmpty()
        {
            if (Hierarchy.IgnoreIsEmpty)
            {
                return false;
            }

            return Grid.IsEmpty;
        }

        public bool IgnoreIsEmpty() => Hierarchy.IgnoreIsEmpty;

        public BoundingCircle GetBoundingCircle()
        {
            return Grid.BoundingBox;
        }

        public void Dispose()
        {
            Grid.Dispose(this);
            Hierarchy.Dispose(this);
        }
    }
}
