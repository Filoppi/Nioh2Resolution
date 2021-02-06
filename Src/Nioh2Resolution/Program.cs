using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Steamless.API.Model;
using Steamless.API.Services;
using Steamless.Unpacker.Variant31.x64;

namespace Nioh2Resolution
{
    public class Program
    {
        private const string EXE_FILE = "nioh2.exe";
        private const string EXE_FILE_BACKUP = "nioh2.exe.backup.exe";
        private const string EXE_FILE_UNPACKED = "nioh2.exe.unpacked.exe";

        public static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");

            Console.WriteLine("Welcome to the Nioh 2 Resolution patcher!");

            Console.WriteLine("\nPlease enter your desired resolution.\n");

            int width = ReadInt("Width", 3440);
            int height = ReadInt("Height", 1440);

            if (File.Exists(EXE_FILE_BACKUP))
            {
                Console.WriteLine($"\nA backup of {EXE_FILE} has been found, it was created the last time this patcher ran succesfully.\n");

                if (ReadBool("Do you want to restore this backup before patching?", false))
                {
                    File.Copy(EXE_FILE_BACKUP, EXE_FILE, true);
                }
            }

            if (!File.Exists(EXE_FILE))
            {
                Console.WriteLine($"\nCould not find {EXE_FILE}!");

                Exit();

                return;
            }

            Console.WriteLine($"\nUnpacking {EXE_FILE}...");

            var result = UnpackExe();
            if (!result)
            {
                Console.WriteLine($"\nUnpacking of {EXE_FILE} failed, this could mean the file is already unpacked and ready for patching.");

                File.Copy(EXE_FILE, EXE_FILE_UNPACKED, true);
            }

            Console.WriteLine($"\nPatching resolution to {width}x{height}...");

            var buffer = File.ReadAllBytes(EXE_FILE_UNPACKED);

            result = PatchExe(ref buffer, width, height);
            if (!result)
            {
                Console.WriteLine("\nPatching failed, consider restoring a backup and try again.");

                File.Delete(EXE_FILE_UNPACKED);

                Exit();

                return;
            }

            Console.WriteLine($"\nBacking up {EXE_FILE}...");

            File.Copy(EXE_FILE, EXE_FILE_BACKUP, true);

            Console.WriteLine($"\nReplacing {EXE_FILE}...");

            File.WriteAllBytes(EXE_FILE, buffer);
            File.Delete(EXE_FILE_UNPACKED);

            Console.WriteLine("\nDone! Don't forget to set the game resolution to 3440x1440 (you can also do it from the config).");

            Exit();
        }

        private static bool UnpackExe()
        {
            LoggingService loggingService = new LoggingService();
            loggingService.AddLogMessage += (sender, eventArgs) =>
            {
                Console.WriteLine(eventArgs.Message);
            };

            SteamlessPlugin plugin = new Main();
            plugin.Initialize(loggingService);

            var result = plugin.CanProcessFile(EXE_FILE);

            if (!result)
            {
                return false;
            }

            result = plugin.ProcessFile(EXE_FILE, new SteamlessOptions
            {
                VerboseOutput = false,
                KeepBindSection = true
            });

            if (!result)
            {
                Console.WriteLine($"-> Processing {EXE_FILE} failed!");

                return false;
            }

            return true;
        }

        private static bool PatchExe(ref byte[] buffer, int width, int height)
        {
            return PatchAspectRatio(ref buffer, width, height) && PatchResolution(ref buffer, width, height);
        }

        // Patching of Aspect Ratio has been disabled as it does not seem strictly necessary.
        // UI scales decently (not perfectly as some things end up scaled uncorrectly or anchored to wrong places,
        // but overall it's decent, so for now just keep this off, until someone can bother to actually find the hex values.
        //Source: http://www.wsgf.org/forums/viewtopic.php?f=64&t=32376&start=110
        private static bool PatchAspectRatio(ref byte[] buffer, int width, int height)
        {
            // These values are from Nioh 1
            const string PATTERN_ASPECTRATIO1 = "C7 43 50 39 8E E3 3F";
            const int PATTERN_ASPECTRATIO1_OFFSET = 3;

            const string PATTERN_ASPECTRATIO2 = "00 00 87 44 00 00 F0 44";

            const string PATTERN_MAGIC1 = "0F 95 C0 88 46 34";
            const string PATTERN_MAGIC1_PATCH = "32 C0 90 88 46 34";

            const string PATTERN_MAGIC2_A = "45 85 D2 7E 1A";
            const string PATTERN_MAGIC2_A_PATCH = "45 85 D2 EB 1A";

            const string PATTERN_MAGIC2_B = "C3 79 14";
            const string PATTERN_MAGIC2_B_PATCH = "C3 EB 14";

            float ratio = width / (float)height;
            double doubleRatio = width / (double)height;
            double ratio_16_9 = 1920.0 / 1080.0;
            float ratioWidth = 1920;
            float ratioHeight = 1080;

            if (ratio < 1.77777)
            {
                ratioHeight = ratioWidth / ratio;
            }
            else
            {
                ratioWidth = ratioHeight * ratio;
            }

            //To review: There seems to be a double hardcoded as 16:9 in the code, but
            /*//changing it seems to have no effect. Better leave it alone.
            //While for float 16:9, there are like 40 hardcoded instances.
            //Aspect Ratio Fix #1
            var positions = FindSequence(ref buffer, ConvertToBytes(ratio_16_9), 0);

            if (!AssertEquals("16:9 position", 1, positions.Count))
            {
                return false;
            }

            var ratio1Patch = ConvertToBytes(doubleRatio);
            Patch(ref buffer, positions, ratio1Patch);*/

            //To review: In Nioh 1, this pattern was found once, while it is now found 4 times,
            /*//I don't know what it represents nor if it's just a chance that it's found 4 times,
            //but replacing any of them does not seem to make any difference on UI nor FOV.
            //Aspect Ratio Fix #2
            positions = FindSequence(ref buffer, StringToPattern(PATTERN_ASPECTRATIO1), 0);

            if (!AssertEquals(nameof(PATTERN_ASPECTRATIO1), 4, positions.Count))
            {
                return false;
            }

            var ratio2Patch = ConvertToBytes(ratio);
            foreach (var position in positions)
            {
                Patch(ref buffer, position + PATTERN_ASPECTRATIO1_OFFSET, ratio1Patch);
            }*/

            //To review: Patching this crashes the game on startup
            /*//Aspect Ratio Fix #3
            positions = FindSequence(ref buffer, StringToPattern(PATTERN_ASPECTRATIO2), 0);

            if (!AssertEquals(nameof(PATTERN_ASPECTRATIO2), 1, positions.Count))
            {
                return false;
            }

            var ratio3Patch = ConvertToBytes(ratioHeight).Concat(ConvertToBytes(ratioWidth)).ToArray();
            Patch(ref buffer, positions, ratio2Patch);*/

            //To review: not found
            /*//Magic Fix #1
            positions = FindSequence(ref buffer, StringToPattern(PATTERN_MAGIC1), 0);

            if (!AssertEquals(nameof(PATTERN_MAGIC1), 1, positions.Count))
            {
                return false;
            }

            Patch(ref buffer, positions, StringToPattern(PATTERN_MAGIC1_PATCH));*/

            //To review: needed?
            /*//Magic Fix #2 - A
            positions = FindSequence(ref buffer, StringToPattern(PATTERN_MAGIC2_A), 0);

            if (!AssertEquals(nameof(PATTERN_MAGIC2_A), 1, positions.Count))
            {
                return false;
            }

            Patch(ref buffer, positions, StringToPattern(PATTERN_MAGIC2_A_PATCH));

            //Magic Fix #2 - B
            positions = FindSequence(ref buffer, StringToPattern(PATTERN_MAGIC2_B), positions.First());

            if (!AssertEquals(nameof(PATTERN_MAGIC2_B), 1, positions.Count))
            {
                return false;
            }

            Patch(ref buffer, positions.First(), StringToPattern(PATTERN_MAGIC2_B_PATCH));*/

            return true;
        }

        private static bool PatchResolution(ref byte[] buffer, int width, int height)
        {
            // Experimenting with resolutions to replace.
            // Some of them are hardcoded in the exe more than once, as maybe they represent the resolution of
            // other textures or effects, so better leave them alone.
            // The values we are looking to change are next to each other, and the first one
            // is the window resolution, the second is the internal resolution.
            var patternResolution720p = ConvertToBytes(1280).Concat(ConvertToBytes(720)).ToArray(); // Found 3 (index 0 and 1 are the good ones)
            var patternResolution1080p = ConvertToBytes(1920).Concat(ConvertToBytes(1080)).ToArray(); // Found 4 (index 1 and 2 are the good ones)
            var patternResolution1440p = ConvertToBytes(2560).Concat(ConvertToBytes(1440)).ToArray(); // Found 2
            var patternResolution1440pUltrawide = ConvertToBytes(3440).Concat(ConvertToBytes(1440)).ToArray(); // Found 2
            var patternResolution2160p = ConvertToBytes(3840).Concat(ConvertToBytes(2160)).ToArray(); // Found 2

            // Replace a resolution that is already ultrawide, in case there are some more hardcoded checks (you never know...)
            // which only calculate the FOV based on the resolution if you selected an ultrawide one.
            // Plus, at least we already have a base 21:9 aspect ration to begin win, in case we failed to
            // patch some aspect ratio values for the UI.
            var positions = FindSequence(ref buffer, patternResolution1440pUltrawide, 0);

            if (!AssertEquals("patternResolution", 2, positions.Count))
            {
                return false;
            }

            var resolution = ConvertToBytes(width).Concat(ConvertToBytes(height)).ToArray();
            var windowResolution = resolution;
            var internalResolution = windowResolution;

            // Window resolution
            Patch(ref buffer, positions[0], windowResolution);

            // Internal resolution (don't scale it by any value as it can already been scaled from the game settings, and it seems to work even after overwriting resolutions)
            Patch(ref buffer, positions[1], internalResolution);

            return true;
        }

        private static void Exit()
        {
            Console.WriteLine("\nPress any key to exit...");

            Console.ReadKey();
        }

        private static int ReadInt(string name, int defaultValue)
        {
            int input;

            do
            {
                Console.Write($"-> {name} [default = {defaultValue}]: ");

                string inputString = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(inputString))
                {
                    return defaultValue;
                }

                int.TryParse(inputString, out input);

                if (input <= 0)
                {
                    Console.WriteLine("--> Invalid value, try again!");
                }
            } while (input <= 0);

            return input;
        }

        private static float ReadFloat(string name, float defaultValue)
        {
            float input;

            do
            {
                Console.Write($"-> {name} [default = {defaultValue:F1}]: ");

                string inputString = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(inputString))
                {
                    return defaultValue;
                }

                float.TryParse(inputString, out input);

                if (input <= 0)
                {
                    Console.WriteLine("--> Invalid value, try again!");
                }
            } while (input <= 0);

            return input;
        }

        private static bool ReadBool(string name, bool defaultValue)
        {
            while (true)
            {
                Console.Write($"-> {name} [default = {(defaultValue ? "Yes" : "No")}]: ");

                string inputString = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(inputString))
                {
                    return defaultValue;
                }

                if (inputString.StartsWith("Y", true, CultureInfo.CurrentCulture))
                {
                    return true;
                }

                if (inputString.StartsWith("N", true, CultureInfo.CurrentCulture))
                {
                    return false;
                }

                Console.WriteLine("--> Invalid value, try again!");
            }
        }

        private static byte[] ConvertToBytes(int value)
        {
            byte[] bytes = BitConverter.GetBytes(value);

            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return bytes;
        }

        private static byte[] ConvertToBytes(float value)
        {
            byte[] bytes = BitConverter.GetBytes(value);

            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return bytes;
        }

        private static byte[] ConvertToBytes(double value)
        {
            byte[] bytes = BitConverter.GetBytes(value);

            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return bytes;
        }

        private static byte[] StringToPattern(string pattern)
        {
            return pattern
                .Split(' ')
                .Select(x => Convert.ToByte(x, 16))
                .ToArray();
        }

        //Source: https://stackoverflow.com/questions/283456/byte-array-pattern-search
        private static List<int> FindSequence(ref byte[] buffer, byte[] pattern, int startIndex)
        {
            List<int> positions = new List<int>();

            int i = Array.IndexOf(buffer, pattern[0], startIndex);

            while (i >= 0 && i <= buffer.Length - pattern.Length)
            {
                byte[] segment = new byte[pattern.Length];

                Buffer.BlockCopy(buffer, i, segment, 0, pattern.Length);

                if (segment.SequenceEqual(pattern))
                {
                    positions.Add(i);

                    i = Array.IndexOf(buffer, pattern[0], i + pattern.Length);
                }
                else
                {
                    i = Array.IndexOf(buffer, pattern[0], i + 1);
                }
            }

            return positions;
        }

        private static bool CompareSequence(ref byte[] buffer, byte[] pattern, int startIndex)
        {
            if (startIndex > buffer.Length - pattern.Length)
            {
                return false;
            }

            byte[] segment = new byte[pattern.Length];
            Buffer.BlockCopy(buffer, startIndex, segment, 0, pattern.Length);

            return segment.SequenceEqual(pattern);
        }

        private static bool AssertEquals<T>(string name, T expected, T value)
        {
            if (!value.Equals(expected))
            {
                Console.WriteLine($"-> {name} expected {expected}, but got {value}!");

                return false;
            }

            return true;
        }

        private static void Patch(ref byte[] buffer, List<int> positions, byte[] patchBytes)
        {
            foreach (int position in positions)
            {
                Patch(ref buffer, position, patchBytes);
            }
        }

        private static void Patch(ref byte[] buffer, int position, byte[] patchBytes)
        {
            Console.WriteLine($"-> Patching offset {position}");

            for (int i = 0; i < patchBytes.Length; i++)
            {
                buffer[position + i] = patchBytes[i];
            }
        }
    }
}
