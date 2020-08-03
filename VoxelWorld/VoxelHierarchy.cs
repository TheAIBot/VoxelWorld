using OpenGL;
using System;
using System.Diagnostics;
using System.Numerics;

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

        //required to make a grid
        private readonly VoxelSystemData GenData;

        //keeps track of grids
        private readonly VoxelGridInfo[] Grids = new VoxelGridInfo[GridLocations.Length];

        //keeps track of sub hierarchies
        private readonly VoxelHierarchyInfo[] SubHierarchies = new VoxelHierarchyInfo[GridLocations.Length];

        public bool IsHollow = false;

        public VoxelHierarchy(Vector3 center, VoxelSystemData genData)
        {
            this.GenData = genData.GetOneDown();

            for (int i = 0; i < Grids.Length; i++)
            {
                Vector3 gridCenter = GetGridCenter(i, center);
                Grids[i] = new VoxelGridInfo(gridCenter);
            }
            for (int i = 0; i < SubHierarchies.Length; i++)
            {
                SubHierarchies[i] = new VoxelHierarchyInfo(Grids[i].GridCenter, GenData.GridSize, GenData.VoxelSize);
            }
        }

        private Vector3 GetGridCenter(int index, Vector3 center)
        {
            return center + GridLocations[index].AsFloatVector3() * 0.5f * (GenData.GridSize - 2) * GenData.VoxelSize;
        }

        public (BoundingCircle, GridNormal) Generate(Vector3 center, Vector3 rotatedLookDir)
        {
            BoundingCircle circle = new BoundingCircle(center, 0);
            GridNormal normal = new GridNormal();
            for (int i = 0; i < GridLocations.Length; i++)
            {
                Grids[i].Generate(GenData, rotatedLookDir);
                if (!Grids[i].IsEmpty)
                {
                    circle = circle.AddBoundingCircle(Grids[i].BoundingBox);

                    normal.AddNormal(Grids[i].Normal);
                }
            }

            return (circle, normal);
        }

        public bool IsEmpty()
        {
            for (int i = 0; i < Grids.Length; i++)
            {
                if (!Grids[i].IsEmpty)
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsHighEnoughResolution(Vector3 voxelCenter, ModelTransformations modelTrans)
        {
            Vector3 a = modelTrans.Translation + (modelTrans.RevRotation * voxelCenter);
            Vector3 c = modelTrans.CameraPos; // rotate cameraPos instead of center because rotate center need inverse modelRotate

            float resolution = (GenData.VoxelSize * 100.0f) / (a - c).Length();
            return resolution < 0.3f;
        }

        private void QueueGridGen(int index, Vector3 rotatedLookDir)
        {
            Grids[index].StartGenerating(GenData, rotatedLookDir);
        }

        private void QueueHierarchyGen(int index, Vector3 rotatedLookDir)
        {
            SubHierarchies[index].StartGenerating(GenData, rotatedLookDir);
        }

        public void CheckAndIncreaseResolution(Frustum renderCheck, ModelTransformations modelTrans)
        {
            IsHollow = false;

            for (int i = 0; i < GridLocations.Length; i++)
            {
                if (Grids[i].IsBeingGenerated)
                {
                    continue;
                }
                if (SubHierarchies[i].GenStatus == GenerationStatus.Generating)
                {
                    continue;
                }

                /*
                 * 1)   Enough resoltuion
                 *          if can see grid
                 *              If grid is available 
                 *                  make subhir hollow
                 *              else 
                 *                  generate grid
                 *          else
                 *              make grid hollow
                 * 2)   else
                 *          if subhir generated
                 *              if can see subhir
                 *                  make grid hollow
                 *                  recursive subhir
                 *              else
                 *                  make subhir hollow
                 *          else
                 *              generate subhir
                */

                if (!SubHierarchies[i].IsHollow && !SubHierarchies[i].CanSee(renderCheck, modelTrans))
                {
                    SubHierarchies[i].MakeHollow();
                    continue;
                }

                if (IsHighEnoughResolution(Grids[i].GridCenter, modelTrans))
                {
                    if (Grids[i].CanSee(renderCheck, modelTrans))
                    {
                        if (Grids[i].IsReadyToDraw())
                        {
                            if (!SubHierarchies[i].IsHollow)
                            {
                                SubHierarchies[i].MakeHollow();
                            }
                        }
                        else
                        {
                            if (Grids[i].ShouldGenerate())
                            {
                                QueueGridGen(i, modelTrans.RotatedLookDir);
                            }
                        }
                    }
                    else
                    {
                        Grids[i].MakeHollow();
                    }
                }
                else
                {
                    if (SubHierarchies[i].CanSee(renderCheck, modelTrans))
                    {
                        if (SubHierarchies[i].ShouldGenerate())
                        {
                            QueueHierarchyGen(i, modelTrans.RotatedLookDir);
                        }
                        else
                        {
                            Grids[i].MakeHollow();
                            SubHierarchies[i].CheckAndIncreaseResolution(renderCheck, modelTrans);
                        }
                    }
                }
            }
        }

        public void MakeHollow()
        {
            if (IsHollow)
            {
                return;
            }

            IsHollow = true;
            for (int i = 0; i < Grids.Length; i++)
            {
                Grids[i].MakeHollow();
            }

            for (int i = 0; i < SubHierarchies.Length; i++)
            {
                SubHierarchies[i].MakeHollow();
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < Grids.Length; i++)
            {
                Grids[i].Dispose();
                Grids[i] = null;
            }

            for (int i = 0; i < SubHierarchies.Length; i++)
            {
                SubHierarchies[i].Dispose();
                SubHierarchies[i] = null;
            }
        }
    }
}
