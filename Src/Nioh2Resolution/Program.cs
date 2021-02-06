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

        private const int RES_TO_REPLACE_W = 3440;
        private const int RES_TO_REPLACE_H = 1440;

        public static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");

            Console.WriteLine("Welcome to the Nioh 2 Resolution patcher!");

            Console.WriteLine("\nPlease enter your desired resolution.\n");

            int width = ReadInt("Width", RES_TO_REPLACE_W);
            int height = ReadInt("Height", RES_TO_REPLACE_H);

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

            Console.WriteLine($"\nDone! Don't forget to set the game resolution to {RES_TO_REPLACE_W}x{RES_TO_REPLACE_H} (you can also do it from the config).");

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

        // UI scales decently (not perfectly as some things end up scaled uncorrectly or anchored to wrong places),
        // but overall it's decent. Hopefully someone will find the hex values responsible for anchoring/scaling based on res,
        // or make sense of the values here.
        // When at aspect ratios less wide than than 16:9 (e.g. 16:10, 4:3), we scale the UI otherwise it would be anchored
        // around 16:9 and be partially hidden. It should not cause stretching (though some menus could look wronger, in game UI will be better).
        // It's possible to stretch the UI at aspect ratios wider than 21:9 but I didn't feel like it was needed, as it would mostly look worse.
        //Source: http://www.wsgf.org/forums/viewtopic.php?f=64&t=32376&start=110
        private static bool PatchAspectRatio(ref byte[] buffer, int width, int height)
        {
            //To review: instead of RES_TO_REPLACE_W, this should probably be: MAX_ASPECT_RATIO_RES
            //float scale = (width / (float)height) / (RES_TO_REPLACE_W / (float)RES_TO_REPLACE_H);
            //width = (int)(width / scale); //To round
            float ratio = width / (float)height;
            double doubleRatio = width / (double)height;

            int default_w = 1920; // 16
            int default_h = 1080; // 9
            float ratio_default = (float)default_w / (float)default_h;
            double double_ratio_default = (double)default_w / (double)default_h;
            float ratioWidth = default_w;
            float ratioHeight = default_h;

            bool scale_UI = false;

            if (ratio < (default_w / (float)default_h))
            {
                ratioHeight = ratioWidth / ratio;
                scale_UI = true;
            }
            else
            {
                ratioWidth = ratioHeight * ratio;
            }

            if (!scale_UI)
            {
                return true;
            }

            var positions = new List<int>();

            /*//Aspect Ratio Fix #1 (double 16/9): Disabled as it seems to have no effect on UI
            positions = FindSequence(ref buffer, ConvertToBytes(double_ratio_default), 0);

            if (!AssertEquals("Aspect Ratio Pattern 1", 1, positions.Count))
            {
                return false;
            }

            var ratio1Patch = ConvertToBytes(doubleRatio);
            Patch(ref buffer, positions, ratio1Patch);*/

            //Aspect Ratio Fix #2 (float 16/9): Changes the UI aspect ratio/scale
            positions = FindSequence(ref buffer, ConvertToBytes(ratio_default), 0);

            if (!AssertEquals("Aspect Ratio Pattern 2", 26, positions.Count))
            {
                return false;
            }

            var ratio2Patch = ConvertToBytes(ratio);
            int i = 0;
            // Indexs 0 to 20 and 22 to 25 have no effects on UI.
            // Only index 21 has an effect, at least in the first released Steam version, so this patch might not be safe after game updates.
            int i_min = 21;
            int i_max = 21;
            foreach (int position in positions)
            {
                if (i >= i_min && i <= i_max)
                    Patch(ref buffer, position, ratio2Patch);
                ++i;
            }

            //Aspect Ratio Fix #3: Scales the UI in a way that I could not understand. The smaller the numbers are, the bigger the UI gets.
            /*var ratio3Pattern = ConvertToBytes((float)default_h).Concat(ConvertToBytes((float)default_w)).ToArray();
            positions = FindSequence(ref buffer, ratio3Pattern, 0);

            if (!AssertEquals("Aspect Ratio Pattern 3", 1, positions.Count))
            {
                return false;
            }

            var ratio3Patch = ConvertToBytes(ratioHeight).Concat(ConvertToBytes(ratioWidth)).ToArray();
            Patch(ref buffer, positions, ratio3Patch);*/

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

            var patternResolution = ConvertToBytes(RES_TO_REPLACE_W).Concat(ConvertToBytes(RES_TO_REPLACE_H)).ToArray();

            // Replace a resolution that is already ultrawide, in case there are some more hardcoded checks (you never know...)
            // which only calculate the FOV based on the resolution if you selected an ultrawide one.
            // Plus, at least we already have a base 21:9 aspect ratio to begin win, in case we failed to
            // patch some aspect ratio values for the UI.
            var positions = FindSequence(ref buffer, patternResolution, 0);

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
