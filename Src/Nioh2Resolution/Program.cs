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

        // 16:9
        private const int DEFAULT_AR_RES_W = 1920;
        private const int DEFAULT_AR_RES_H = 1080;
        private const float DEFAULT_AR = DEFAULT_AR_RES_W / (float)DEFAULT_AR_RES_H;

        // Max supported is probably 43:16 (2.3888...). Though it might be 2.4, I'm not sure.
        private const int MAX_AR_RES_1_W = 2560;
        private const int MAX_AR_RES_1_H = 1080;
        private const float MAX_AR_1 = MAX_AR_RES_1_W / (float)MAX_AR_RES_1_H;
        private const int MAX_AR_RES_2_W = 3440;
        private const int MAX_AR_RES_2_H = 1440;
        private const float MAX_AR_2 = MAX_AR_RES_2_W / (float)MAX_AR_RES_2_H;

        private static int RES_TO_REPLACE_W = 3440;
        private static int RES_TO_REPLACE_H = 1440;

        public static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");

            Console.WriteLine("Welcome to the Nioh 2 Resolution patcher!\n");

            int res_to_replace = ReadInt("Select the resolution you want to replace.\n1 for 1280x720, 2 for 1920x1080, 3 for 3440x1440 (suggested for Ultrawide).", 1);
            if (res_to_replace <= 1 || res_to_replace > 3)
            {
                RES_TO_REPLACE_W = 1280;
                RES_TO_REPLACE_H = 720;
            }
            else if (res_to_replace == 2)
            {
                RES_TO_REPLACE_W = 1920;
                RES_TO_REPLACE_H = 1080;
            }
            else if (res_to_replace == 3)
            {
                RES_TO_REPLACE_W = 3440;
                RES_TO_REPLACE_H = 1440;
            }

            Console.WriteLine("\nPlease enter your desired resolution.\n");

            int width = ReadInt("Width", RES_TO_REPLACE_W);
            int height = ReadInt("Height", RES_TO_REPLACE_H);

            bool patch_UI = false;
            float ratio = width / (float)height;
            float tolerance = 0.0001f;
            if (ratio + tolerance < DEFAULT_AR)
            {
                Console.WriteLine("");
                if (ReadBool("Your desired aspect ratio is below the minimum official supported (parts of the UI might not be visible).\nWould you like to apply an EXPERIMENTAL fix to scale down the UI?", false))
                {
                    patch_UI = true;
                }
                else
                {
                    Console.WriteLine("");
                    if (ReadBool("Would you like this app to find the maximum 16:9 resolution contained by your screen?\nThat way you could play borderless with black bars by putting a black background behind the game", false))
                    {
                        height = (int)Math.Round(width / DEFAULT_AR);
                    }
                }
            }
            else if (ratio - tolerance > DEFAULT_AR && ratio + tolerance < MAX_AR_1)
            {
                Console.WriteLine("\nYour aspect ratio is in between supported ones (16:9 and 21:9). UI might no scale or anchor correctly.");
            }
            else if (ratio - tolerance > MAX_AR_2)
            {
                Console.WriteLine("\nYour aspect ratio is above 43:16 (21:9), the officially supported max.\nUI will work but it won't scale or anchor perfectly.");
            }

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
                Console.WriteLine($"\nUnpacking of {EXE_FILE} failed (will continue).");

                File.Copy(EXE_FILE, EXE_FILE_UNPACKED, true);
            }

            Console.WriteLine($"\nPatching resolution to {width}x{height}...");

            var buffer = File.ReadAllBytes(EXE_FILE_UNPACKED);

            bool UI_patch_failed = false;
            result = PatchExe(ref buffer, width, height, patch_UI, ref UI_patch_failed);

            if (!result)
            {
                Console.WriteLine("\nPatching failed, consider restoring a backup and try again.");

                File.Delete(EXE_FILE_UNPACKED);

                Exit();

                return;
            }
            else if (UI_patch_failed)
            {
                Console.WriteLine("\nUI failed to patch, resolution was patched nonetheless.");
            }

            Console.WriteLine($"\nBacking up {EXE_FILE}...");

            File.Copy(EXE_FILE, EXE_FILE_BACKUP, true);

            Console.WriteLine($"\nReplacing {EXE_FILE}...");

            File.WriteAllBytes(EXE_FILE, buffer);
            File.Delete(EXE_FILE_UNPACKED);

            Console.WriteLine($"\nDone! Don't forget to set the game resolution to {RES_TO_REPLACE_W}x{RES_TO_REPLACE_H} and restart the game.\nYou can also do it from the config file.");

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
                Console.WriteLine($"-> Processing {EXE_FILE} failed (file might not be encrypted)!");

                return false;
            }

            return true;
        }

        private static bool PatchExe(ref byte[] buffer, int width, int height, bool patch_UI, ref bool UI_patch_failed)
        {
            UI_patch_failed = !PatchAspectRatio(ref buffer, width, height, patch_UI);
            return PatchResolution(ref buffer, width, height);
        }

        // UI scales decently (not perfectly as some things end up scaled uncorrectly or anchored to wrong places),
        // but overall it's decent. Hopefully someone will find the hex values responsible for anchoring/scaling based on res,
        // or make sense of the values here.
        // When at aspect ratios less wide than than 16:9 (e.g. 16:10, 4:3), we scale the UI otherwise it would be anchored
        // around 16:9 and be partially hidden. It should not cause stretching (though some menus could look wronger, in game UI will be better).
        // It's possible to stretch the UI at aspect ratios wider than 21:9 but I didn't feel like it was needed, as it would mostly look worse.
        //Source: http://www.wsgf.org/forums/viewtopic.php?f=64&t=32376&start=110
        private static bool PatchAspectRatio(ref byte[] buffer, int width, int height, bool patch_UI)
        {
            //float scale = (width / (float)height) / MAX_AR_2;
            //width = (int)Math.Round(width / scale);
            float ratio = width / (float)height;
            double doubleRatio = width / (double)height;

            double double_ratio_default = (double)DEFAULT_AR_RES_W / (double)DEFAULT_AR_RES_H;
            float ratioWidth = DEFAULT_AR_RES_W;
            float ratioHeight = DEFAULT_AR_RES_H;

            bool scale_UI = false;

            if (ratio < DEFAULT_AR)
            {
                ratioHeight = ratioWidth / ratio;
                scale_UI = true;
            }
            else
            {
                ratioWidth = ratioHeight * ratio;
            }

            if (!scale_UI || !patch_UI)
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
            positions = FindSequence(ref buffer, ConvertToBytes(DEFAULT_AR), 0);

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
            /*var ratio3Pattern = ConvertToBytes((float)DEFAULT_AR_RES_H).Concat(ConvertToBytes((float)DEFAULT_AR_RES_W)).ToArray();
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
            var patternResolution1080pUltrawide = ConvertToBytes(1280).Concat(ConvertToBytes(720)).ToArray(); // Found 2
            var patternResolution1440p = ConvertToBytes(2560).Concat(ConvertToBytes(1440)).ToArray(); // Found 2
            var patternResolution1440pUltrawide = ConvertToBytes(3440).Concat(ConvertToBytes(1440)).ToArray(); // Found 2
            var patternResolution2160p = ConvertToBytes(3840).Concat(ConvertToBytes(2160)).ToArray(); // Found 2

            var patternResolution = ConvertToBytes(RES_TO_REPLACE_W).Concat(ConvertToBytes(RES_TO_REPLACE_H)).ToArray();

            // Replace a resolution that is already ultrawide, in case there are some more hardcoded checks (you never know...)
            // which only calculate the FOV based on the resolution if you selected an ultrawide one.
            // Plus, at least we already have a base 21:9 aspect ratio to begin win, in case we failed to
            // patch some aspect ratio values for the UI.
            var positions = FindSequence(ref buffer, patternResolution, 0);

            bool replacing_1280x720 = RES_TO_REPLACE_W == 1280 && RES_TO_REPLACE_H == 720;
            bool replacing_1920x1080 = RES_TO_REPLACE_W == 1920 && RES_TO_REPLACE_H == 1080;

            int i1 = 0;
            int i2 = 1;
            int expected_results = 2;

            if (replacing_1280x720)
            {
                i1 = 0;
                i2 = 1;
                expected_results = 3;
            }
            else if (replacing_1920x1080)
            {
                i1 = 1;
                i2 = 2;
                expected_results = 4;
            }

            if (!AssertEquals("patternResolution", expected_results, positions.Count))
            {
                return false;
            }

            var resolution = ConvertToBytes(width).Concat(ConvertToBytes(height)).ToArray();
            var windowResolution = resolution;
            var internalResolution = windowResolution;

            // Window resolution
            Patch(ref buffer, positions[i1], windowResolution);

            // Internal resolution (don't scale it by any value as it can already been scaled from the game settings, and it seems to work even after overwriting resolutions)
            Patch(ref buffer, positions[i2], internalResolution);

            /*// Patch resolution text (doesn't work, text is likely is an asset)
            string resolutionText = $"{RES_TO_REPLACE_W} x {RES_TO_REPLACE_H}";
            string customResolutionText = $"{width} x {height}";
            bool ultrawide = (RES_TO_REPLACE_W / (float)RES_TO_REPLACE_H) > 1.78; // 16/9 with tolerance
            if (ultrawide)
            {
                resolutionText += " (Ultrawide)";
                customResolutionText += " (Patch res)"; // Needs to be of the same length of course
            }
            var patternResolutionText = ConvertToBytes(resolutionText);
            var patchResolutionText = ConvertToBytes(customResolutionText);
            positions = FindSequence(ref buffer, patternResolutionText, 0);

            Patch(ref buffer, positions, patchResolutionText);*/

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

        private static byte[] ConvertToBytes(string value)
        {
            // Unicode specifically for Nioh 2 (not UTF8 nor ASCII)
            byte[] bytes = System.Text.Encoding.Unicode.GetBytes(value);
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
