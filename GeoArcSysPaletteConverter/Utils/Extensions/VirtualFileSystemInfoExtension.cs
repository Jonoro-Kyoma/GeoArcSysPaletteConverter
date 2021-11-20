using System;
using ArcSysAPI.Component.IO;
using static ArcSysAPI.Component.IO.VirtualFileSystemInfo;

namespace GeoArcSysPaletteConverter.Utils.Extensions
{
    public static class VirtualFileSystemInfoExtension
    {
        public static ConsoleColor GetTextColor(this VirtualFileSystemInfo vfsi)
        {
            switch (vfsi.Obfuscation)
            {
                case FileObfuscation.BBTAGEncryption:
                case FileObfuscation.FPACEncryption:
                    return ConsoleColor.Green;
                case FileObfuscation.FPACDeflation:
                case FileObfuscation.SwitchCompression:
                    return ConsoleColor.Cyan;
                case FileObfuscation.FPACEncryption |
                     FileObfuscation.FPACDeflation:
                    return ConsoleColor.Magenta;
                default:
                    return ConsoleColor.White;
            }
        }
    }
}