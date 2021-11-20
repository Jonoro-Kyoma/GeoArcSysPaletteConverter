using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using ArcSysAPI.Common.Enum;
using ArcSysAPI.Component.IO;
using ArcSysAPI.Component.IO.Adobe;
using ArcSysAPI.Component.IO.ArcSys;
using GeoArcSysPaletteConverter.Models;
using GeoArcSysPaletteConverter.Utils;
using GeoArcSysPaletteConverter.Utils.Extensions;

namespace GeoArcSysPaletteConverter
{
    internal class Program
    {
        [Flags]
        public enum Options
        {
            Endianness = 0x8000000,
            Output = 0x10000000,
            Replace = 0x20000000,
            Continue = 0x40000000
        }

        public enum PaletteFormat
        {
            HPL = 0x0,
            ACT = 0x1
        }

        public static ConsoleOption[] ConsoleOptions =
        {
            new ConsoleOption
            {
                Name = "Endianness",
                ShortOp = "-en",
                LongOp = "--endianness",
                Description = "If the output is HPL, sets the output file's endianness. {LittleEndian|BigEndian}",
                HasArg = true,
                Flag = Options.Endianness
            },
            new ConsoleOption
            {
                Name = "Output",
                ShortOp = "-o",
                LongOp = "--output",
                Description = "Specifies the output directory for the output files.",
                HasArg = true,
                Flag = Options.Output
            },
            new ConsoleOption
            {
                Name = "Replace",
                ShortOp = "-r",
                LongOp = "--replace",
                Description = "Don't create a backup and replace same named files in output directory.",
                Flag = Options.Replace
            },
            new ConsoleOption
            {
                Name = "Continue",
                ShortOp = "-c",
                LongOp = "--continue",
                Description = "Don't pause the application when finished.",
                Flag = Options.Continue
            }
        };

        public static string assemblyPath = string.Empty;
        public static string assemblyDir = string.Empty;

        public static string initFilePath = string.Empty;
        public static string currentFile = string.Empty;

        public static Options options;
        public static PaletteFormat paletteFormat;
        public static ByteOrder endianness = ByteOrder.LittleEndian;

        public static string pathsFile = string.Empty;
        public static string outputPath = string.Empty;

        public static string[] BBTAGObfuscatedFiles =
            {string.Empty, ".pac", ".pacgz", ".hip", ".abc", ".txt", ".pat", ".ha6", ".fod"};

        private static readonly string[] supportedImageExtensions =
        {
            ".bmp", ".dib", ".gif", ".jpeg", ".jpg", ".jpe", ".jfif",
            ".png", ".tiff", ".tif", ".wmp", ".hip", ".dds"
        };

        [STAThread]
        private static void Main(string[] args)
        {
            var codeBase = Assembly.GetExecutingAssembly().CodeBase;
            var uri = new UriBuilder(codeBase);
            assemblyPath = Path.GetFullPath(Uri.UnescapeDataString(uri.Path));
            assemblyDir = Path.GetDirectoryName(assemblyPath);

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("\nArcSystemWorks Palette Converter\nprogrammed by: Geo\n\n");
            Console.ForegroundColor = ConsoleColor.White;

            try
            {
                if (args.Length == 0 ||
                    args.Length > 0 && (args[0] == "-h" || args[0] == "--help"))
                {
                    ShowUsage();
                    Pause();
                    return;
                }

                var formatFound = false;

                if (args.Length > 0)
                    if (Enum.TryParse(args[0], true, out PaletteFormat p) ||
                        (args.Length > 1 && Enum.TryParse(args[1], true, out p)))
                    {
                        paletteFormat = p;
                        formatFound = true;
                    }

                var firstArgNullWhitespace = string.IsNullOrWhiteSpace(args[0]);
                if (firstArgNullWhitespace || args[0].First() == '-' || (firstArgNullWhitespace && formatFound))
                {
                    var inputPath = Dialogs.OpenFileDialog("Select input file...", "Supported Files|*.act;*.pal;*.aco;*.ase;*.hpl;*.hip;*.pac;*.paccs;*.pacgz|Palette Files|*.act;*.pal|Swatches|*.aco;*.ase|ArcSys Palettes|*.hpl;*pal.pac|ArcSys Images|*.hip;*img.pac;*vri.pac|ArcSys Files|*.pac;*.paccs;*.pacgz");
                    if (string.IsNullOrWhiteSpace(inputPath))
                    {
                        inputPath = Dialogs.OpenFolderDialog("Select input folder...");
                        if (string.IsNullOrWhiteSpace(inputPath))
                        {
                            ShowUsage();
                            Pause();
                            return;
                        }
                    }

                    if (firstArgNullWhitespace)
                    {
                        args[0] = inputPath;
                    }
                    else
                    {
                        var argsList = new List<string>(args);
                        argsList.Insert(0, inputPath);
                        args = argsList.ToArray();
                    }
                }

                initFilePath = Path.GetFullPath(args[0].Replace("\"", "\\"));

                ProcessOptions(args);

                if (!File.Exists(initFilePath) && !Directory.Exists(initFilePath) ||
                    string.IsNullOrWhiteSpace(initFilePath) || initFilePath.First() == '-')
                {
                    Console.WriteLine("The given file/folder does not exist.\n");
                    Pause();
                    return;
                }

                var noOutputPath = string.IsNullOrWhiteSpace(outputPath);

                var pacFileInfo = new PACFileInfo(initFilePath);
                if (File.GetAttributes(initFilePath).HasFlag(FileAttributes.Directory))
                {
                    if (noOutputPath) outputPath = $"{initFilePath}_Converted";
                    var files = DirSearch(initFilePath).Select(f => new VirtualFileSystemInfo(f));
                    foreach (var file in files)
                    {
                        pacFileInfo = new PACFileInfo(file.FullName);
                        if (pacFileInfo.IsValidPAC)
                        {
                            var cfiles = RecursivePACExplore(pacFileInfo);
                            foreach (var cfile in cfiles) ProcessFile(cfile, initFilePath);
                        }
                        else
                        {
                            ProcessFile(file, initFilePath);
                        }
                    }
                }
                else if (pacFileInfo.IsValidPAC)
                {
                    if (noOutputPath)
                        outputPath = Path.Combine(Path.GetDirectoryName(initFilePath),
                            Path.GetFileNameWithoutExtension(initFilePath));
                    else
                        outputPath = Path.Combine(outputPath,
                            Path.GetFileNameWithoutExtension(initFilePath));
                    var files = RecursivePACExplore(pacFileInfo);
                    foreach (var file in files) ProcessFile(file, initFilePath);
                }
                else
                {
                    if (noOutputPath) outputPath = Path.GetDirectoryName(initFilePath);
                    ProcessFile(new VirtualFileSystemInfo(initFilePath), outputPath);
                }

                Console.WriteLine("Complete.");
                Pause();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                if (!string.IsNullOrWhiteSpace(currentFile))
                    Console.WriteLine($"Current File: {currentFile}");
                Console.WriteLine(ex);
                Console.WriteLine("Something went wrong!");
                Console.ForegroundColor = ConsoleColor.White;
                Pause();
            }
        }

        public static void ProcessOptions(string[] args)
        {
            var newArgsList = new List<string>();

            for (var i = 1; i < args.Length; i++)
            {
                var arg = args[i];
                if (arg.First() != '-')
                    continue;

                newArgsList.Add(arg);

                foreach (var co in ConsoleOptions)
                    if (arg == co.ShortOp || arg == co.LongOp)
                    {
                        options |= (Options) co.Flag;
                        if (co.HasArg)
                        {
                            var subArgsList = new List<string>();
                            var lastArg = string.Empty;
                            for (var j = i; j < args.Length - 1; j++)
                            {
                                var subArg = args[j + 1];

                                if (subArg.First() == '-')
                                    break;

                                if (string.IsNullOrWhiteSpace(lastArg) || subArg.ToLower() != lastArg.ToLower())
                                    subArgsList.Add(subArg);
                                i++;
                            }

                            co.SpecialObject = subArgsList.ToArray();
                        }
                    }
            }

            foreach (var co in ConsoleOptions)
            {
                if (co.Flag == null)
                    continue;

                if (co.HasArg)
                {
                    var subArgs = (string[]) co.SpecialObject;
                    if ((Options)co.Flag == Options.Endianness &&
                        options.HasFlag(Options.Endianness))
                    {
                        if (subArgs.Length > 0)
                        {
                            if (Enum.TryParse(subArgs[0], true, out ByteOrder endian)) endianness = endian;
                        }
                        else
                        {
                            endianness = ByteOrder.LittleEndian;
                        }
                    }

                    if ((Options) co.Flag == Options.Output &&
                        options.HasFlag(Options.Output))
                    {
                        if (subArgs.Length == 0)
                        {
                            subArgs = new string[1];
                            subArgs[0] = Dialogs.OpenFolderDialog("Select output folder...");
                        }

                        foreach (var arg in subArgs)
                        {
                            var subArg = Path.GetFullPath(arg.Replace("\"", "\\"));
                            if (subArgs.Length > 1)
                                WarningMessage(
                                    $"Too many arguments for output path. Defaulting to \"{subArg}\"...");
                            outputPath = Path.GetFullPath(subArg);
                            break;
                        }

                        var defaultPath = assemblyDir;

                        if (string.IsNullOrWhiteSpace(outputPath))
                        {
                            if (subArgs.Length > 1)
                                WarningMessage(
                                    "None of the given output paths exist or could be created. Ignoring...");
                            else if (subArgs.Length == 1)
                                WarningMessage(
                                    "Given output path does not exist. Ignoring...");
                            else if (subArgs.Length == 0)
                                WarningMessage(
                                    "No output path was given. Ignoring...");

                            if (Directory.Exists(defaultPath))
                            {
                                InfoMessage(
                                    "Using default output path...");

                                outputPath = defaultPath;
                            }
                        }
                        else
                        {
                            if (!Directory.Exists(outputPath))
                            {
                                WarningMessage(
                                    "Given output path does not exist. Ignoring...");
                                if (Directory.Exists(defaultPath))
                                {
                                    InfoMessage(
                                        "Using default output path...");

                                    outputPath = defaultPath;
                                }
                            }
                        }
                    }
                }
            }
        }

        public static VirtualFileSystemInfo[] RecursivePACExplore(PACFileInfo pfi, int level = 0)
        {
            Console.Write(new string(' ', level * 4) + "Scanning ");
            Console.ForegroundColor = pfi.GetTextColor();
            Console.Write($"{pfi.Name}");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("...");

            pfi.Active = true;
            currentFile = pfi.FullName;

            var vfiles = new List<VirtualFileSystemInfo>();
            vfiles.AddRange(pfi.GetFiles());

            var len = vfiles.Count;

            for (var i = 0; i < len; i++)
                if (vfiles[i] is PACFileInfo pacFileInfo)
                    vfiles.AddRange(RecursivePACExplore(pacFileInfo, level + 1));

            pfi.Active = false;

            return vfiles.ToArray();
        }

        public static void ProcessFile(VirtualFileSystemInfo vfsi, string baseDirectory)
        {
            Console.WriteLine($"Processing {vfsi.Name}");
            var ext = vfsi.Extension;
            var path = vfsi.FullName;

            var ep = vfsi.GetExtendedPaths();
            var extPaths = new string[ep.Length + 1];
            Array.Copy(ep, 0, extPaths, 1, ep.Length);
            extPaths[0] = vfsi.GetPrimaryPath();

            if (extPaths.Length > 0)
                extPaths[0] = extPaths[0].Replace(baseDirectory, string.Empty);
            var extPath = string.Join("\\", extPaths.Length > 0 ? extPaths : new[] {vfsi.Name});
            var savePath = Path.GetFullPath((outputPath + extPath).Replace('?', '_'));

            if (vfsi.VirtualRoot != vfsi && !string.IsNullOrWhiteSpace(vfsi.VirtualRoot.Extension))
                savePath = savePath.Replace(vfsi.VirtualRoot.Extension, string.Empty);

            savePath = savePath
                .Replace(vfsi.Extension, $".{Enum.GetName(typeof(PaletteFormat), paletteFormat).ToLower()}")
                .Replace("?", "_");

            Color[] palette = null;
            var virtualFile = vfsi.VirtualRoot != vfsi ? vfsi : null;
            try
            {
                switch (ext)
                {
                    case ".hpl":
                        virtualFile ??= new HPLFileInfo(path);
                        palette = ((HPLFileInfo)virtualFile).Palette;
                        break;
                    case ".hip":
                        virtualFile ??= new HIPFileInfo(path);
                        palette = ((HIPFileInfo)virtualFile).Palette;
                        break;
                    case ".act":
                        virtualFile ??= new ACTFileInfo(path);
                        palette = ((ACTFileInfo)virtualFile).Palette;
                        break;
                    case ".aco":
                        virtualFile ??= new ACOFileInfo(path);
                        palette = ((ACOFileInfo)virtualFile).Colors;
                        break;
                    case ".ase":
                        virtualFile ??= new ASEFileInfo(path);
                        palette = ((ASEFileInfo)virtualFile).Colors;
                        break;
                    case ".pal":
                        if (virtualFile == null)
                        {
                            virtualFile = new RIFFPALFileInfo(path);
                            if (!((RIFFPALFileInfo)virtualFile).IsValidRIFFPAL)
                            {
                                virtualFile = new JSACPALFileInfo(path);
                                if (!((JSACPALFileInfo)virtualFile).IsValidJSACPAL)
                                    virtualFile = new VirtualFileSystemInfo(path);
                            }
                        }

                        switch (virtualFile.GetType())
                        {
                            case Type riffpalType when riffpalType == typeof(RIFFPALFileInfo):
                                palette = ((RIFFPALFileInfo)virtualFile).Palette;
                                break;
                            case Type jsacpalType when jsacpalType == typeof(JSACPALFileInfo):
                                palette = ((JSACPALFileInfo)virtualFile).Palette;
                                break;
                        }

                        break;
                    case ".dds":
                        virtualFile ??= new DDSFileInfo(path);
                        palette = ((DDSFileInfo)virtualFile).GetImage().Palette.Entries;
                        break;
                    case string e when supportedImageExtensions.Contains(e):
                        using (var bmp = virtualFile == null
                            ? BitmapLoader.LoadBitmap(path)
                            : BitmapLoader.LoadBitmap(virtualFile.GetBytes()))
                        {
                            palette = bmp.Palette.Entries;
                        }
                        break;
                }
            }
            catch
            {
                WarningMessage($"Retrieving palette failed. Skipping {Path.GetFileName(savePath)}...");
                return;
            }

            if (palette == null || palette.Length == 0)
            {
                Console.WriteLine($"No colors found. Skipping {Path.GetFileName(savePath)}...");
                return;
            }

            var fileBytes = paletteFormat switch
            {
                PaletteFormat.HPL => new HPLFileInfo(palette, endianness).GetBytes(),
                PaletteFormat.ACT => new ACTFileInfo(palette).GetBytes(),
                _ => new byte[0]
            };

            Directory.CreateDirectory(Path.GetDirectoryName(savePath));
            File.WriteAllBytes(savePath, fileBytes);

            Console.WriteLine($"Finished processing {Path.GetFileName(savePath)}");
        }

        private static void ShowUsage()
        {
            var shortOpMaxLength =
                ConsoleOptions.Select(co => co.ShortOp).OrderByDescending(s => s.Length).First().Length;
            var longOpMaxLength =
                ConsoleOptions.Select(co => co.LongOp).OrderByDescending(s => s.Length).First().Length;

            Console.WriteLine(
                $"Usage: {Path.GetFileName(assemblyPath)} <file/folder path> [HPL/ACT] [options...]");

            Console.WriteLine("Options:");
            foreach (var co in ConsoleOptions)
                Console.WriteLine(
                    $"{co.ShortOp.PadRight(shortOpMaxLength)}\t{co.LongOp.PadRight(longOpMaxLength)}\t{co.Description}");
        }

        private static void Pause(bool force = false)
        {
            if (options.HasFlag(Options.Continue) && !force)
                return;

            Console.WriteLine("\rPress Any Key to exit...");
            Console.ReadKey();
        }

        private static void InfoMessage(string message)
        {
            ConsoleMessage(message, ConsoleColor.Blue, "INFO");
        }

        private static void WarningMessage(string message)
        {
            ConsoleMessage(message, ConsoleColor.DarkYellow, "WARNING");
        }

        private static void ConsoleMessage(string message, ConsoleColor color, string messageType)
        {
            Console.ForegroundColor = color;
            Console.WriteLine($"[{messageType}] {message}");
            Console.ForegroundColor = ConsoleColor.White;
        }

        public static string[] DirSearch(string sDir)
        {
            var stringList = new List<string>();
            foreach (var f in Directory.GetFiles(sDir)) stringList.Add(f);
            foreach (var d in Directory.GetDirectories(sDir)) stringList.AddRange(DirSearch(d));

            return stringList.ToArray();
        }
    }
}