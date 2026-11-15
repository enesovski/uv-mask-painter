using System;
using UnityEngine.Rendering;

namespace Archura.UVMaskPainter.Editor.Document
{
    [Flags]
    public enum MaskChannels
    {
        None = 0,
        R = 1,
        G = 2,
        B = 4,
        A = 8,
        RGB = R | G | B,
        RGBA = RGB | A
    }

    public static class MaskChannelsExtensions
    {
        public static ColorWriteMask ToColorWriteMask(this MaskChannels channels)
        {
            ColorWriteMask mask = 0;
            if ((channels & MaskChannels.R) != 0) mask |= ColorWriteMask.Red;
            if ((channels & MaskChannels.G) != 0) mask |= ColorWriteMask.Green;
            if ((channels & MaskChannels.B) != 0) mask |= ColorWriteMask.Blue;
            if ((channels & MaskChannels.A) != 0) mask |= ColorWriteMask.Alpha;
            return mask;
        }
    }
}
