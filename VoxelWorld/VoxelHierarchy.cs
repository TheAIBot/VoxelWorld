using OpenGL;
using System;
using System.Numerics;

namespace VoxelWorld
{
    internal class VoxelHierarchyInfo : IDisposable
    {
        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }

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
        private readonly Vector3 Center;
        private readonly float VoxelSize;
        private readonly int GridSize;
        private readonly Func<Vector3, float> WeightGen;

        //keeps track of grids
        private readonly VoxelGridInfo[] Grids = new VoxelGridInfo[GridLocations.Length];

        private AxisAlignedBoundingBox BoundingBox = null;
        private GridNormal HirNormal = new GridNormal();
        public bool IsHollow = false;
        private readonly int HierarchyDepth;

        //keeps track of sub hierarchies
        private readonly VoxelHierarchy[] SubHierarchies = new VoxelHierarchy[GridLocations.Length];
        private readonly bool[] IsEmptyHierarchies = new bool[GridLocations.Length];
        private readonly bool[] IsGeneratingHierarchy = new bool[GridLocations.Length];
        private readonly AxisAlignedBoundingBox[] SubHirBoundBoxes = new AxisAlignedBoundingBox[GridLocations.Length];
        private readonly GridNormal[] SubHirNormals = new GridNormal[GridLocations.Length];
        

        //make sure all grids and hierarchies are disposed of
        //no matter if they are being generated
        private readonly object DisposeLock = new object();
        private bool HasBeenDisposed = false;

        public VoxelHierarchy(int gridSize, Vector3 center, float voxelSize, Func<Vector3, float> generator, int hierarchyDepth)
        {
            this.Center = center;
            this.VoxelSize = voxelSize / 2.0f;
            this.GridSize = gridSize;// + 2;
            this.WeightGen = generator;
            this.HierarchyDepth = hierarchyDepth;

            for (int i = 0; i < Grids.Length; i++)
            {
                Vector3 gridCenter = Center + GridLocations[i].AsFloatVector3() * 0.5f * (GridSize - 2) * VoxelSize;
                Grids[i] = new VoxelGridInfo(gridCenter);
            }
        }

        public void Generate(Matrix4 model_rot, Vector3 lookDir)
        {
            for (int i = 0; i < GridLocations.Length; i++)
            {
                Grids[i].GenerateGridAction(GridSize, VoxelSize, WeightGen, model_rot, lookDir)();
                if (!Grids[i].IsEmpty)
                {
                    if (BoundingBox == null)
                    {
                        BoundingBox = Grids[i].BoundingBox;
                    }
                    else
                    {
                        BoundingBox.AddBoundingBox(Grids[i].BoundingBox);
                    }

                    HirNormal.AddNormal(Grids[i].Normal);
                }
            }
        }

        private bool IsEmpty()
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

        private bool IsHighEnoughResolution(Vector3 voxelCenter, Vector3 cameraPos)
        {
            Matrix4 model = Matrix4.Identity;
            Vector3 a = model * voxelCenter;
            Vector3 c = model * cameraPos;

            float resolution = (VoxelSize * 100.0f) / (a - c).Length();
            return resolution < 0.7f;
        }

        private void QueueGridGen(int index, Matrix4 model_rot, Vector3 lookDir)
        {
            WorkLimiter.QueueWork(Grids[index].GenerateGridAction(GridSize, VoxelSize, WeightGen, model_rot, lookDir));
        }

        private void QueueHierarchyGen(int index, Matrix4 model_rot, Vector3 lookDir)
        {
            Vector3 gridCenter = Grids[index].GridCenter;

            IsGeneratingHierarchy[index] = true;
            WorkLimiter.QueueWork(() =>
            {
                lock (DisposeLock)
                {
                    if (HasBeenDisposed)
                    {
                        return;
                    }
                }

                VoxelHierarchy subHireachy = new VoxelHierarchy(GridSize, gridCenter, VoxelSize, WeightGen, HierarchyDepth + 1);
                subHireachy.Generate(model_rot, lookDir);

                if (subHireachy.IsEmpty())
                {
                    IsEmptyHierarchies[index] = true;
                    subHireachy.Dispose();
                    subHireachy = null;
                }

                lock (DisposeLock)
                {
                    if (HasBeenDisposed)
                    {
                        //Console.WriteLine("Wasted work");
                        subHireachy?.Dispose();
                        subHireachy = null;
                    }
                    else
                    {
                        //Console.WriteLine("Done work");
                    }

                    if (subHireachy != null)
                    {
                        SubHirBoundBoxes[index] = subHireachy.BoundingBox;
                        SubHirNormals[index] = subHireachy.HirNormal;
                    }

                    SubHierarchies[index] = subHireachy;
                    IsGeneratingHierarchy[index] = false;
                }
            });
        }

        public bool IsGenerating()
        {
            for (int i = 0; i < IsGeneratingHierarchy.Length; i++)
            {
                if (IsGeneratingHierarchy[i])
                {
                    return true;
                }
            }

            return false;
        }

        public void CheckAndIncreaseResolution(PlayerCamera camera, Frustum renderCheck)
        {
            if (IsGenerating())
            {
                return;
            }

            IsHollow = false;

            for (int i = 0; i < GridLocations.Length; i++)
            {
                if (Grids[i].IsBeingGenerated)
                {
                    continue;
                }

                if (SubHierarchies[i] != null && !SubHierarchies[i].IsHollow)
                {
                    if (!SubHirNormals[i].CanSee(Matrix4.Identity, camera.LookDirection) ||
                        !renderCheck.Intersects(SubHirBoundBoxes[i]))
                    {
                        SubHierarchies[i].MakeHollow();
                    }
                }

                if (IsHighEnoughResolution(Grids[i].GridCenter, camera.CameraPos))
                {
                    if (Grids[i].IsReadyToDraw())
                    {
                        if (SubHierarchies[i] != null && !SubHierarchies[i].IsHollow)
                        {
                            SubHierarchies[i]?.MakeHollow();
                        }
                    }
                    else
                    {
                        if (Grids[i].ShouldGenerate(renderCheck, Matrix4.Identity, camera.LookDirection))
                        {
                            QueueGridGen(i, Matrix4.Identity, camera.LookDirection);
                        }
                        else
                        {
                            Grids[i].MakeHollow();
                        }
                    }
                }
                else
                {
                    if (SubHierarchies[i] == null)
                    {
                        if (!Grids[i].IsEmpty)
                        {
                            if (!IsEmptyHierarchies[i])
                            {
                                QueueHierarchyGen(i, Matrix4.Identity, camera.LookDirection);
                            }
                        }
                    }
                    else
                    {
                        if (SubHirNormals[i].CanSee(Matrix4.Identity, camera.LookDirection) &&
                            renderCheck.Intersects(SubHirBoundBoxes[i]))
                        {
                            if (SubHierarchies[i].IsHollow)
                            {
                                if (Grids[i].ShouldGenerate(renderCheck, Matrix4.Identity, camera.LookDirection))
                                {
                                    QueueGridGen(i, Matrix4.Identity, camera.LookDirection);
                                }
                                else
                                {
                                    SubHierarchies[i].CheckAndIncreaseResolution(camera, renderCheck);
                                }
                            }
                            else
                            {
                                if (!SubHierarchies[i].IsGenerating() && HierarchyDepth > 0)
                                {
                                    Grids[i].MakeHollow();
                                }

                                SubHierarchies[i].CheckAndIncreaseResolution(camera, renderCheck);
                            }
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
                SubHierarchies[i]?.MakeHollow();
            }
        }

        public bool DrawMesh()
        {
            bool drewSomething = false;
            for (int i = 0; i < Grids.Length; i++)
            {
                if (!IsGeneratingHierarchy[i])
                {
                    VoxelHierarchy hir = SubHierarchies[i];
                    if (hir != null && !hir.IsHollow)
                    {
                        if (hir.DrawMesh())
                        {
                            drewSomething = true;
                            continue;
                        }
                    }
                }

                if (Grids[i].IsReadyToDraw())
                {
                    drewSomething |= Grids[i].DrawMesh();
                    continue;
                }
            }

            return drewSomething;
        }

        public bool DrawPoints()
        {
            bool drewSomething = false;
            for (int i = 0; i < Grids.Length; i++)
            {
                if (!IsGeneratingHierarchy[i])
                {
                    VoxelHierarchy hir = SubHierarchies[i];
                    if (hir != null && !hir.IsHollow)
                    {
                        if (hir.DrawPoints())
                        {
                            drewSomething = true;
                            continue;
                        }
                    }
                }

                if (Grids[i].IsReadyToDraw())
                {
                    drewSomething |= Grids[i].DrawPoints();
                    continue;
                }
            }

            return drewSomething;
        }

        public void Dispose()
        {
            lock (DisposeLock)
            {
                HasBeenDisposed = true;

                for (int i = 0; i < Grids.Length; i++)
                {
                    Grids[i].Dispose();
                    Grids[i] = null;
                }

                for (int i = 0; i < SubHierarchies.Length; i++)
                {
                    SubHierarchies[i]?.Dispose();
                    SubHierarchies[i] = null;
                }
            }
        }
    }
}
