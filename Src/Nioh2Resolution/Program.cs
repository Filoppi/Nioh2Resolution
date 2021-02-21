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
        private const double DEFAULT_AR_DOUBLE = DEFAULT_AR_RES_W / (double)DEFAULT_AR_RES_H;

        // "Lower" max supported AR is 64:27 (2.370370...).
        // This is the AR they have hardcoded for Widescreen as far as patch 1.26.
        // UI will slowly drift for any other AR that isn't this or 16:9.
        private const int MAX_AR_RES_1_W = 2560;
        private const int MAX_AR_RES_1_H = 1080;
        private const float MAX_AR_1 = MAX_AR_RES_1_W / (float)MAX_AR_RES_1_H;
        private const double MAX_AR_1_DOUBLE = MAX_AR_RES_1_W / (double)MAX_AR_RES_1_H;
        // Max supported AR is 43:18 (2.3888...)
        private const int MAX_AR_RES_2_W = 3440;
        private const int MAX_AR_RES_2_H = 1440;
        private const float MAX_AR_2 = MAX_AR_RES_2_W / (float)MAX_AR_RES_2_H;
        private const double MAX_AR_2_DOUBLE = MAX_AR_RES_2_W / (double)MAX_AR_RES_2_H;
        // Guessed generic 21:9 AR
        private const int MAX_AR_GUESS_W = 21;
        private const int MAX_AR_GUESS_H = 9;
        private const float MAX_AR_GUESS = MAX_AR_GUESS_W / (float)MAX_AR_GUESS_H;
        private const double MAX_AR_GUESS_DOUBLE = MAX_AR_GUESS_W / (double)MAX_AR_GUESS_H;

        private const float AR_TOLERANCE = 0.0001f;

        private static int RES_TO_REPLACE_W = 1280;
        private static int RES_TO_REPLACE_H = 720;

        public static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");

            Console.WriteLine("Welcome to the Nioh 2 Resolution patcher!\n");

            int res_to_replace = ReadInt("Select the resolution you want to replace.\n1 for 1280x720, 2 for 1920x1080, 3 for 3440x1440.", 1);
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

            Console.WriteLine("\nPlease enter your desired resolution (it might not work if it's higher than your screen).");

            int width = ReadInt("Width", RES_TO_REPLACE_W);
            int height = ReadInt("Height", RES_TO_REPLACE_H);

            bool patch_UI = false;
            bool ask_for_UI_patch = false;
            bool ask_for_black_bars = false;
            float ratio = width / (float)height;
            if (ratio + AR_TOLERANCE < DEFAULT_AR)
            {
                Console.WriteLine("\nYour aspect ratio is below the minimum official supported.");
                ask_for_UI_patch = true;
                ask_for_black_bars = true;
            }
            else if (ratio - AR_TOLERANCE > DEFAULT_AR && ratio + AR_TOLERANCE < MAX_AR_1)
            {
                Console.WriteLine("\nYour aspect ratio is in between supported ones (16:9 and 21:9).");
                ask_for_UI_patch = true;
            }
            else if (ratio - AR_TOLERANCE > MAX_AR_1)
            {
                Console.WriteLine("\nYour aspect ratio is above 64:27 (~21:9), the officially supported max.");
                ask_for_UI_patch = true;
            }

            if (ask_for_UI_patch || ask_for_black_bars)
            {
                Console.WriteLine("The UI might not scale or anchor correctly, it might be partially hidden and also drift as you play.");
                /* This has been disabled for now because while it fixes the UI drifting above 21:9 and also makes the whole UI visible at 16:9, it makes some UI elements unselectable both with a mouse or with a controller, effectively not allowing you to proceed.
                if (ask_for_UI_patch &&
                    ReadBool("The UI might not scale or anchor correctly, it might be partially hidden and also drift as you play.\nThere is an EXPERIMANTAL fix available, it might shrink the UI in some places but it will fix the drifting and\nimprove the in game UI.\nWould you like to apply it?", false))
                {
                    patch_UI = true;
                }
                else if (ask_for_black_bars)
                {
                    Console.WriteLine("");
                    if (ReadBool("Would you like me to find the maximum 16:9 resolution contained by your screen?\nThat way you could play borderless with black bars by putting a black background behind the game", false))
                    {
                        height = (int)Math.Round(width / DEFAULT_AR);
                    }
                }*/
            }

            if (File.Exists(EXE_FILE_BACKUP))
            {
                Console.WriteLine($"\nA backup of {EXE_FILE} has been found, it was created the last time this patcher ran succesfully.");

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
            bool success = PatchResolution(ref buffer, width, height);
            return success;
        }

        private static bool PatchAspectRatio(ref byte[] buffer, int width, int height, bool patch_UI)
        {
            if (!patch_UI)
            {
                return false;
            }

            float ratio = width / (float)height;
            float patch_ratio = DEFAULT_AR;

            float ratioWidth = DEFAULT_AR_RES_W;
            float ratioHeight = DEFAULT_AR_RES_H;
            // Scaling MAX_AR_2 will make the UI slightly drift with mission restarts/cutscenes and so on...
            float target_max_AR = MAX_AR_1;

            if (ratio < DEFAULT_AR)
            {
                // Changing patch_ratio and ratioWidth didn't lead me anywhere better
                ratioHeight = ratioWidth / ratio;
            }
            else
            {
                // Nioh 2 compares MAX_AR_1 to DEFAULT_AR to compute how much to shift the UI of.
                // This calculation breaks at any other aspect ratio (including the supported MAX_AR_2)
                // and makes the UI shift/drift with time. If we patch some hardcoded values,
                // we can fix the UI drifting at the cost of "more broken" menus and stretched AR
                // in some cutscenes.
                ratioWidth *= target_max_AR / DEFAULT_AR;
                patch_ratio = target_max_AR;
            }

            /* Some random patches that didn't seem to make any difference
            var positions = FindSequence(ref buffer, ConvertToBytes(MAX_AR_1_DOUBLE)); // 0
            positions = FindSequence(ref buffer, ConvertToBytes(MAX_AR_2_DOUBLE)); // 0
            positions = FindSequence(ref buffer, ConvertToBytes(MAX_AR_GUESS_DOUBLE)); // 0
            positions = FindSequence(ref buffer, ConvertToBytes(MAX_AR_1)); // 0
            positions = FindSequence(ref buffer, ConvertToBytes(MAX_AR_2)); // 0
            positions = FindSequence(ref buffer, ConvertToBytes(MAX_AR_GUESS)); // 0

            positions = FindSequence(ref buffer, ConvertToBytes(DEFAULT_AR_DOUBLE / MAX_AR_1_DOUBLE)); // 2
            Patch(ref buffer, positions, ConvertToBytes(DEFAULT_AR_DOUBLE / ratio_double));
            positions = FindSequence(ref buffer, ConvertToBytes(DEFAULT_AR_DOUBLE / MAX_AR_2_DOUBLE)); // 0
            Patch(ref buffer, positions, ConvertToBytes(DEFAULT_AR_DOUBLE / ratio_double));
            positions = FindSequence(ref buffer, ConvertToBytes(DEFAULT_AR_DOUBLE / MAX_AR_GUESS_DOUBLE)); // 0
            Patch(ref buffer, positions, ConvertToBytes(DEFAULT_AR_DOUBLE / ratio_double));
            positions = FindSequence(ref buffer, ConvertToBytes(MAX_AR_1_DOUBLE / DEFAULT_AR_DOUBLE)); // 1
            Patch(ref buffer, positions, ConvertToBytes(ratio_double / DEFAULT_AR_DOUBLE));
            positions = FindSequence(ref buffer, ConvertToBytes(MAX_AR_2_DOUBLE / DEFAULT_AR_DOUBLE)); // 2
            Patch(ref buffer, positions, ConvertToBytes(ratio_double / DEFAULT_AR_DOUBLE));
            positions = FindSequence(ref buffer, ConvertToBytes(MAX_AR_GUESS_DOUBLE / DEFAULT_AR_DOUBLE)); // 0
            Patch(ref buffer, positions, ConvertToBytes(ratio_double / DEFAULT_AR_DOUBLE));
            positions = FindSequence(ref buffer, ConvertToBytes(DEFAULT_AR / MAX_AR_1)); // 59
            // Some index between 49 and 52 AND also between 55 and 58 makes the game crash or infinite load
            Patch(ref buffer, positions, 0, 48, ConvertToBytes(DEFAULT_AR / ratio));
            Patch(ref buffer, positions, 53, 54, ConvertToBytes(DEFAULT_AR / ratio));
            positions = FindSequence(ref buffer, ConvertToBytes(DEFAULT_AR / MAX_AR_2)); // 0
            Patch(ref buffer, positions, ConvertToBytes(DEFAULT_AR / ratio));
            positions = FindSequence(ref buffer, ConvertToBytes(DEFAULT_AR / MAX_AR_GUESS)); // 0
            Patch(ref buffer, positions, ConvertToBytes(DEFAULT_AR / ratio));
            positions = FindSequence(ref buffer, ConvertToBytes(MAX_AR_1 / DEFAULT_AR)); // 2
            Patch(ref buffer, positions, ConvertToBytes(ratio / DEFAULT_AR));
            positions = FindSequence(ref buffer, ConvertToBytes(MAX_AR_2 / DEFAULT_AR)); // 5
            Patch(ref buffer, positions, ConvertToBytes(ratio / DEFAULT_AR));
            positions = FindSequence(ref buffer, ConvertToBytes(MAX_AR_GUESS / DEFAULT_AR)); // 13
            Patch(ref buffer, positions, ConvertToBytes(ratio / DEFAULT_AR));
            //TODO unhardcode 32:9 (my AR)
            positions = FindSequence(ref buffer, ConvertToBytes(MAX_AR_GUESS_H / (double)MAX_AR_GUESS_W)); // 0
            Patch(ref buffer, positions, ConvertToBytes(16.0 / 32.0));
            positions = FindSequence(ref buffer, ConvertToBytes(MAX_AR_GUESS_W / (double)MAX_AR_GUESS_H)); // 2
            Patch(ref buffer, positions, ConvertToBytes(32.0 / 16.0));
            positions = FindSequence(ref buffer, ConvertToBytes(MAX_AR_GUESS_H / (float)MAX_AR_GUESS_W)); // 0
            Patch(ref buffer, positions, ConvertToBytes(16.0f / 32.0f));
            positions = FindSequence(ref buffer, ConvertToBytes(MAX_AR_GUESS_W / (float)MAX_AR_GUESS_H)); // 13
            Patch(ref buffer, positions, ConvertToBytes(32.0f / 16.0f));*/

            // Aspect Ratio Fix #1 (double 16/9): Disabled as it seems to have no effect on UI
            /*var positions = FindSequence(ref buffer, ConvertToBytes(DEFAULT_AR_DOUBLE));

            if (!AssertEquals("Aspect Ratio Pattern 1", 1, positions.Count))
            {
                return false;
            }

            var ratio1Patch = ConvertToBytes((double)patch_ratio);
            Patch(ref buffer, positions, ratio1Patch);*/
            
            // First, make sure all the positions are as expected...

            var positions2 = FindSequence(ref buffer, ConvertToBytes(DEFAULT_AR));
            if (!AssertEquals("Aspect Ratio Pattern 2", 26, positions2.Count))
            {
                return false;
            }

            var ratio3Pattern = ConvertToBytes((float)DEFAULT_AR_RES_H).Concat(ConvertToBytes((float)DEFAULT_AR_RES_W)).ToArray();
            var positions3 = FindSequence(ref buffer, ratio3Pattern);

            if (!AssertEquals("Aspect Ratio Pattern 3", 1, positions3.Count))
            {
                return false;
            }

            // Then, apply the patches...

            // Aspect Ratio Fix #2 (float 16/9): Changes the UI aspect ratio/scale
            var ratio2Patch = ConvertToBytes(patch_ratio);
            // Indexs 0 to 20 and 22 to 25 have no effects on UI.
            // Only index 21 has an effect, at least until ver 1.25 (this patch might not be safe for later updates).
            Patch(ref buffer, positions2, 21, 21, ratio2Patch);

            // Aspect Ratio Fix #3: Scales the UI in some ways, needs to have the same AR as the Fix #2 or it causes UI drifting and stretched UI.
            // Changing the height seems to have a different effect than changhing the width (they are used differently).
            // The smaller the numbers are, the bigger the UI gets.
            var ratio3Patch = ConvertToBytes(ratioHeight).Concat(ConvertToBytes(ratioWidth)).ToArray();
            Patch(ref buffer, positions3, ratio3Patch);

            return true;
        }

        // Had no success with this
        private static bool PatchFPS(ref byte[] buffer, int targetFPS = 240)
        {
            string FPSText = "120";
            string customFPSText = "240"; // Needs to be of the same length of course
            var patternFPSText = ConvertToBytes(FPSText);
            var patchFPSText = ConvertToBytes(customFPSText);
            var positions_text = FindSequence(ref buffer, patternFPSText);
            Patch(ref buffer, positions_text, patchFPSText);

            char targetFPSi = (char)targetFPS;
            float targetFPSf = (float)targetFPS;
            float targetDTf = 1.0f / targetFPSf;
            double targetFPSd = (double)targetFPS;
            double targetDTd = 1.0 / targetFPSd;

            //To review: try char and short. Try little endians?
            char FPSi = (char)120;
            float FPSf = 120.0f;
            float DTf = 1.0f / FPSf;
            double FPSd = 120.0;
            double DTd = 1.0 / FPSd;
            var positions120FPSi = FindSequence(ref buffer, ConvertToBytes(FPSi));
            //Patch(ref buffer, positions120FPSi, ConvertToBytes(targetFPSi));
            var positions120FPSf = FindSequence(ref buffer, ConvertToBytes(FPSf));
            //Patch(ref buffer, positions120FPSf, ConvertToBytes(targetFPSf));
            var positions120DTf = FindSequence(ref buffer, ConvertToBytes(DTf));
            //Patch(ref buffer, positions120DTf, ConvertToBytes(targetDTf));
            var positions120FPSd = FindSequence(ref buffer, ConvertToBytes(FPSd));
            //Patch(ref buffer, positions120FPSd, ConvertToBytes(targetFPSd));
            var positions120DTd = FindSequence(ref buffer, ConvertToBytes(DTd));
            //Patch(ref buffer, positions120DTd, ConvertToBytes(targetDTd));
            FPSi = (char)60;
            FPSf = 60.0f;
            DTf = 1.0f / FPSf;
            FPSd = 60.0;
            DTd = 1.0 / FPSd;
            var positions60FPSi = FindSequence(ref buffer, ConvertToBytes(FPSi));
            var positions60FPSf = FindSequence(ref buffer, ConvertToBytes(FPSf));
            var positions60DTf = FindSequence(ref buffer, ConvertToBytes(DTf));
            var positions60FPSd = FindSequence(ref buffer, ConvertToBytes(FPSd));
            var positions60DTd = FindSequence(ref buffer, ConvertToBytes(DTd));
            FPSi = (char)30;
            FPSf = 30.0f;
            DTf = 1.0f / FPSf;
            FPSd = 30.0;
            DTd = 1.0 / FPSd;
            var positions30FPSi = FindSequence(ref buffer, ConvertToBytes(FPSi));
            var positions30FPSf = FindSequence(ref buffer, ConvertToBytes(FPSf));
            var positions30DTf = FindSequence(ref buffer, ConvertToBytes(DTf));
            var positions30FPSd = FindSequence(ref buffer, ConvertToBytes(FPSd));
            var positions30DTd = FindSequence(ref buffer, ConvertToBytes(DTd));

            List<List<int>> FPSi_list = new List<List<int>>();
            FPSi_list.Add(positions120FPSi);
            FPSi_list.Add(positions60FPSi);
            FPSi_list.Add(positions30FPSi);
            List<List<int>> FPSf_list = new List<List<int>>();
            FPSf_list.Add(positions120FPSf);
            FPSf_list.Add(positions60FPSf);
            FPSf_list.Add(positions30FPSf);
            List<List<int>> DTf_list = new List<List<int>>();
            DTf_list.Add(positions120DTf);
            DTf_list.Add(positions60DTf);
            DTf_list.Add(positions30DTf);
            List<List<int>> FPSd_list = new List<List<int>>();
            FPSd_list.Add(positions120FPSd);
            FPSd_list.Add(positions60FPSd);
            FPSd_list.Add(positions30FPSd);
            List<List<int>> DTd_list = new List<List<int>>();
            DTd_list.Add(positions120DTd);
            DTd_list.Add(positions60DTd);
            DTd_list.Add(positions30DTd);
            var FPSi_results = FindClosePositions(ref FPSi_list, 64);
            var FPSf_results = FindClosePositions(ref FPSf_list, 1024);
            var DTf_results = FindClosePositions(ref DTf_list, 1024);
            var FPSd_results = FindClosePositions(ref FPSd_list, 1024);
            var DTd_results = FindClosePositions(ref DTd_list, 1024);

            Patch(ref buffer, FPSi_results[0], ConvertToBytes(targetFPSi));
            Patch(ref buffer, FPSf_results[0], ConvertToBytes(targetFPSf));
            Patch(ref buffer, DTf_results[0], ConvertToBytes(targetDTf));
            Patch(ref buffer, FPSd_results[0], ConvertToBytes(targetFPSd));
            Patch(ref buffer, DTd_results[0], ConvertToBytes(targetDTd));

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
            var patternResolution1080pUltrawide = ConvertToBytes(2560).Concat(ConvertToBytes(1080)).ToArray(); // Found 2
            var patternResolution1440p = ConvertToBytes(2560).Concat(ConvertToBytes(1440)).ToArray(); // Found 2
            var patternResolution1440pUltrawide = ConvertToBytes(3440).Concat(ConvertToBytes(1440)).ToArray(); // Found 2
            var patternResolution2160p = ConvertToBytes(3840).Concat(ConvertToBytes(2160)).ToArray(); // Found 2

            var patternResolution = ConvertToBytes(RES_TO_REPLACE_W).Concat(ConvertToBytes(RES_TO_REPLACE_H)).ToArray();
            var positions = FindSequence(ref buffer, patternResolution);
            patternResolution = ConvertToBytes(RES_TO_REPLACE_W).Concat(ConvertToBytes(RES_TO_REPLACE_H)).ToArray();
            positions = FindSequence(ref buffer, patternResolution);

            bool replacing_1280x720 = RES_TO_REPLACE_W == 1280 && RES_TO_REPLACE_H == 720;
            bool replacing_1920x1080 = RES_TO_REPLACE_W == 1920 && RES_TO_REPLACE_H == 1080;

            var resolution = ConvertToBytes(width).Concat(ConvertToBytes(height)).ToArray();

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

            var windowResolution = resolution;
            var internalResolution = windowResolution;

            // Window resolution
            Patch(ref buffer, positions[i1], windowResolution);
            // Internal resolution (don't scale it by any value as it can already been scaled from the game settings, and it seems to work even after overwriting resolutions)
            Patch(ref buffer, positions[i2], internalResolution);

            /* Patch resolution text (doesn't work, text is likely is an asset, at least english)
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
            var positions_text = FindSequence(ref buffer, patternResolutionText);
            Patch(ref buffer, positions_text, patchResolutionText);

            patternResolutionText = ConvertToBytes("1440");
            patchResolutionText = ConvertToBytes($"{height}");
            positions_text = FindSequence(ref buffer, patternResolutionText);
            Patch(ref buffer, positions_text, patchResolutionText);*/

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

        private static byte[] ConvertToBytes(char value)
        {
            byte[] bytes = BitConverter.GetBytes(value);

            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return bytes;
        }

        private static byte[] ConvertToBytes(short value)
        {
            byte[] bytes = BitConverter.GetBytes(value);

            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return bytes;
        }

        private static byte[] ConvertToBytes(Int32 value)
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

        private static List<List<int>> FindClosePositions(ref List<List<int>> positions_lists, uint tolerance)
        {
            List<List<int>> out_list = new List<List<int>>();
            foreach (List<int> positions in positions_lists)
            {
                out_list.Add(new List<int>());
            }

            // Limited to 3 lists...
            int i1 = 0;
            foreach (List<int> positions1 in positions_lists)
            {
                foreach (int position1 in positions1)
                {
                    int i2 = 0;
                    foreach (List<int> positions2 in positions_lists)
                    {
                        if (i1 != i2)
                        {
                            foreach (int position2 in positions2)
                            {
                                if (Math.Abs(position1 - position2) > 0
                                    && Math.Abs(position1 - position2) <= tolerance)
                                {
                                    int i3 = 0;
                                    foreach (List<int> positions3 in positions_lists)
                                    {
                                        if (i1 != i3 && i2 != i3)
                                        {
                                            foreach (int position3 in positions3)
                                            {
                                                if ((Math.Abs(position1 - position3) > 0
                                                    && Math.Abs(position1 - position3) <= tolerance)
                                                    || (Math.Abs(position2 - position3) > 0
                                                    && Math.Abs(position2 - position3) <= tolerance))
                                                {
                                                    if (!out_list[i1].Contains(position1))
                                                        out_list[i1].Add(position1);
                                                    if (!out_list[i2].Contains(position2))
                                                        out_list[i2].Add(position2);
                                                    if (!out_list[i3].Contains(position3))
                                                        out_list[i3].Add(position3);
                                                }
                                            }
                                        }
                                        ++i3;
                                    }
                                }
                            }
                        }
                        ++i2;
                    }
                }
                ++i1;
            }
            // Actually, keep all the lists in so we know the original order/index
            //while (out_list.Count > 0 && out_list.First().Count == 0)
            //    out_list.RemoveAt(0);
            //while (out_list.Count > 0 && out_list.Last().Count == 0)
            //    out_list.RemoveAt(out_list.Count - 1);
            return out_list;
        }

        //Source: https://stackoverflow.com/questions/283456/byte-array-pattern-search
        private static List<int> FindSequence(ref byte[] buffer, byte[] pattern, int startIndex = 0)
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

        private static void Patch(ref byte[] buffer, List<int> positions, int i_min, int i_max, byte[] patchBytes)
        {
            int i = 0;
            foreach (int position in positions)
            {
                if (i >= i_min && i <= i_max)
                    Patch(ref buffer, position, patchBytes);
                ++i;
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
