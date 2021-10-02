using System;

namespace VoxelWorld
{
    public struct GridSidePointsUsed
    {
        public bool PlusX;
        public bool MinusX;
        public bool PlusY;
        public bool MinusY;
        public bool PlusZ;
        public bool MinusZ;

        public const int AdjacentOffsets = 6;

        public void FillWithAdjacentGridOffsets(Span<GridOffset> offsets)
        {
            if (offsets.Length != AdjacentOffsets)
            {
                throw new ArgumentOutOfRangeException(nameof(offsets), $"Must have a length of {AdjacentOffsets}.");
            }

            offsets[0] = new GridOffset(PlusX ? 1 : 0, 0, 0);
            offsets[1] = new GridOffset(MinusX ? -1 : 0, 0, 0);
            offsets[2] = new GridOffset(0, PlusY ? 1 : 0, 0);
            offsets[3] = new GridOffset(0, MinusY ? -1 : 0, 0);
            offsets[4] = new GridOffset(0, 0, PlusZ ? 1 : 0);
            offsets[5] = new GridOffset(0, 0, MinusZ ? -1 : 0);
        }

        public bool IsAnyUsed()
        {
            return PlusX || MinusX || PlusY || MinusY || PlusZ || MinusZ;
        }
    }
}
