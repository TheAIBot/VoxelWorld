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

        private bool Initialized = false;
        private GridVAO MeshVao = null;
        private GridVAO PointsVao = null;
        private readonly object DisposeLock = new object();
        private bool HasBeenDisposed = false;

        public static int DrawCalls = 0;

        public VoxelGridInfo(Vector3 center)
        {
            this.GridCenter = center;
        }

        public Action GenerateGridAction(int size, float voxelSize, Func<Vector3, float> gen, Matrix4 model_rot, Vector3 lookDir)
        {
            Debug.Assert(MeshVao == null);
            Debug.Assert(PointsVao == null);
            Debug.Assert(IsBeingGenerated == false);

            IsBeingGenerated = true;
            return () =>
            {
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

                if (!Normal.CanSee(model_rot, lookDir))
                {
                    IsBeingGenerated = false;
                    VoxelGridStorage.StoreForReuse(grid);
                    return;
                }


                var meshData = grid.Triangulize();
                var boxData = BoxGeometry.MakeBoxGeometry(BoundingBox.Min, BoundingBox.Max);

                //set grid to null here to make sure it isn't captured in the lambda in the future
                //as using the grid after storing it would be a problem
                VoxelGridStorage.StoreForReuse(grid);
                grid = null;

                MainThreadWork.QueueWork(new Action<WorkOptimizer>(x =>
                {
                    GridVAO meshVao = x.MakeGridVAO(meshData.points, meshData.normals, meshData.indices);
                    GridVAO boxVao = x.MakeGridVAO(boxData.points, boxData.normals, boxData.indices);

                    lock (DisposeLock)
                    {
                        if (HasBeenDisposed)
                        {
                            x.StoreGridVAOForReuse(meshVao);
                            x.StoreGridVAOForReuse(boxVao);
                        }
                        else
                        {
                            Debug.Assert(MeshVao == null);
                            Debug.Assert(PointsVao == null);

                            MeshVao = meshVao;
                            PointsVao = boxVao;
                        }
                    }

                    IsBeingGenerated = false;
                }));
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

            if (IsReadyToDraw())
            {
                return false;
            }

            return true;
        }

        public bool CanSee(Frustum onScreenCheck, Matrix4 model_rot, Vector3 lookDir)
        {
            if (IsEmpty)
            {
                return false;
            }

            if (!Normal.CanSee(model_rot, lookDir))
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
            return MeshVao != null && PointsVao != null;
        }

        public void MakeHollow()
        {
            lock (DisposeLock)
            {
                if (!HasBeenDisposed)
                {
                    if (MeshVao != null || PointsVao != null)
                    {
                        MainThreadWork.QueueWork(x =>
                        {
                            if (MeshVao != null)
                            {
                                x.StoreGridVAOForReuse(MeshVao);
                            }
                            if (PointsVao != null)
                            {
                                x.StoreGridVAOForReuse(PointsVao);
                            }

                            MeshVao = null;
                            PointsVao = null;
                        });
                    }
                }
            }
        }

        public bool DrawMesh()
        {
            if (MeshVao == null)
            {
                return false;
            }
            DrawCalls++;

            MeshVao.Program.Use();
            MeshVao.Draw();

            return true;
        }

        public bool DrawPoints()
        {
            if (PointsVao == null)
            {
                return false;
            }
            DrawCalls++;

            PointsVao.Program.Use();
            PointsVao.Program["mat_diff"].SetValue(new Vector4(Vector3.Abs(GridCenter.Normalize()), 0.2f));
            PointsVao.Program["mat_spec"].SetValue(new Vector4(Vector3.Abs(GridCenter.Normalize()), 0.2f));
            PointsVao.Draw();

            return true;
        }

        public void Dispose()
        {
            lock (DisposeLock)
            {
                HasBeenDisposed = true;
            }

            if (MeshVao != null || PointsVao != null)
            {
                MainThreadWork.QueueWork(x =>
                {
                    if (MeshVao != null)
                    {
                        x.StoreGridVAOForReuse(MeshVao);
                    }
                    if (PointsVao != null)
                    {
                        x.StoreGridVAOForReuse(PointsVao);
                    }

                    MeshVao = null;
                    PointsVao = null;
                });
            }
        }
    }
}
