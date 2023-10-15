using Silk.NET.OpenGL;
using System;

namespace VoxelWorld.Render.VoxelGrid
{
    internal struct GpuCommandSync
    {
        private readonly GL _openGl;
        private nint? _syncFlag;

        public GpuCommandSync(GL openGl)
        {
            _openGl = openGl;
        }

        public void ReCreateFence()
        {
            if (_syncFlag.HasValue)
            {
                _openGl.DeleteSync(_syncFlag.Value);
                _syncFlag = null;
            }
            CreateFence();
        }

        public void CreateFence()
        {
            if (_syncFlag.HasValue)
            {
                throw new InvalidOperationException("Fence already exist.");
            }

            _syncFlag = _openGl.FenceSync(SyncCondition.SyncGpuCommandsComplete, SyncBehaviorFlags.None);
        }

        public void Wait()
        {
            while (_syncFlag.HasValue)
            {
                if (HasCompleted())
                {
                    break;
                }
            }
        }

        public bool HasCompleted()
        {
            if (!_syncFlag.HasValue)
            {
                return true;
            }

            GLEnum lol = _openGl.ClientWaitSync(_syncFlag.Value, SyncObjectMask.Bit, 1);
            if (lol == GLEnum.AlreadySignaled || lol == GLEnum.ConditionSatisfied)
            {
                _openGl.DeleteSync(_syncFlag.Value);
                _syncFlag = null;
                return true;
            }

            return false;
        }
    }
}
