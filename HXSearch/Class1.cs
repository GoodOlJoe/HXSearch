namespace HXSearch
{
    public class Class1
    {
        List<string> inputs =
            [
                // many different physical inputs and outputs 
                "E:\\All\\Documents\\Line 6\\Tones\\Helix\\Backup - Current Setlists\\2020 02 02\\Templates\\Gtr+Vox+Bas+Keys.hlx",

                // parallel outputs
                "C:\\Users\\PCAUDI~1\\AppData\\Local\\Temp\\SAB-SAB b exits.hlx",

                // interconnect split followed by join
                "E:\\All\\Documents\\Line 6\\Tones\\Helix\\Backup - Whole System\\3.80 2024 12 07 Helix Floor Backup with 3.80\\Setlist1-FACTORY 1\\Preset120-You Shall Pass.hlx",

                "C:\\Users\\PCAUDI~1\\AppData\\Local\\Temp\\Nvr Gng Bk Loopr.hlx",
                "C:\\Users\\PCAUDI~1\\AppData\\Local\\Temp\\in outs.hlx",
                "E:\\All\\Documents\\Line 6\\Tones\\Helix\\Backup - Whole System\\3.80 2024 12 16 with my presets\\Setlist6-Sandbox\\Preset021-US Double Nrm.hlx",
                "E:\\All\\Documents\\Line 6\\Tones\\Helix\\Backup - Whole System\\3.80 2024 12 16 with my presets\\Setlist8-TEMPLATES\\Preset016-MIDI Bass Pedals.hlx",

                // # regular split followed by interconnect split
                "E:\\All\\Documents\\Line 6\\Tones\\Helix\\Backup - Whole System\\3.80 2024 12 07 Helix Floor Backup with 3.80\\Setlist2-FACTORY 2\\Preset047-Unicorns Forever.hlx",

                // # two parallel sections, second one is an "interconnect split" (meaning, via output 1A to Input 2A+B)
                "E:\\All\\Documents\\Line 6\\Tones\\Helix\\Backup - Whole System\\2.92 2020 11 22 BEFORE UPGRADE 2.9 TO 3.0\\Setlist1-FACTORY 1\\Preset101-Sunbather.hlx",

                // nested parallel sections
                "E:\\All\\Documents\\Line 6\\Tones\\Helix\\Backup - Whole System\\2.81 2019 08 12(2)\\Setlist2-FACTORY 2\\Preset083-Unicorn In A Box.hlx",

                // SABJ with no A
                "E:\\All\\Documents\\Line 6\\Tones\\Helix\\Backup - Whole System\\3.80 2024 12 16 with my presets\\Setlist1-FACTORY 1\\Preset050-BIG DUBB.hlx",

                // SABJ - SABJ but dsp1 SABJ has no S or A modules and no external input. So it will have null S or A on the intermediate lists
                "E:\\All\\Documents\\Line 6\\Tones\\Helix\\Backup - Whole System\\3.80 2024 12 16 with my presets\\Setlist1-FACTORY 1\\Preset056-WATERS IN HELL.hlx",

                // SAB with no A, feeding to ABJ
                "C:\\Users\\PCAUDI~1\\AppData\\Local\\Temp\\sab no a - abj.hlx",

                "C:\\Users\\PCAUDI~1\\AppData\\Local\\Temp\\sab - abj.hlx",
                "E:\\All\\Documents\\Line 6\\Tones\\Helix\\Backup - Whole System\\3.80 2024 12 16 with my presets\\Setlist1-FACTORY 1\\Preset042-BAS_Hire Me!.hlx",

                // "working" but not sure if I should do as described in the comment below
                // doesn't crash but illustrates that i'm not treating an input as a split when multiple paths start from teh same input. Not sure i've thought this through but probably I should be doing that
                "E:\\All\\Documents\\Line 6\\Tones\\Helix\\Backup - Whole System\\3.80 2024 12 07 Helix Floor Backup with 3.80\\Setlist2-FACTORY 2\\Preset051-Knife Fight.hlx",

                // currently not working

                // SAB with no A -- doesn't crash but doesn't handle parallel section right.    
                // I think I need to generalize the solution to the 2A+B outputs where I insert
                // an implied join. But instead of doing it in that one case, I need to do it
                // whenever I have an open Split and I hit a terminal output on both paths.
                "C:\\Users\\PCAUDI~1\\AppData\\Local\\Temp\\sab no a.hlx",

                "E:\\All\\Documents\\Line 6\\Tones\\Helix\\Backup - Whole System\\3.80 2024 12 16 with my presets\\Setlist1-FACTORY 1\\Preset043-Justice Fo Y'all.hlx",
                "C:\\Users\\PCAUDI~1\\AppData\\Local\\Temp\\New Preset.hlx",
                "C:\\Users\\PCAUDI~1\\AppData\\Local\\Temp\\x.hlx",
                "C:\\Users\\PCAUDI~1\\AppData\\Local\\Temp\\y.hlx",
                "E:\\All\\Documents\\Line 6\\Tones\\Helix\\Backup - Whole System\\2.92 2020 11 22 BEFORE UPGRADE 2.9 TO 3.0\\Setlist1-FACTORY 1\\Preset101-Sunbather.hlx"
            ];
        public void Test()
        {

            string outFQN = Path.Combine([Environment.CurrentDirectory, "..", "..", "..", "..", "out.txt"]);
            if (File.Exists(outFQN)) { File.Delete(outFQN); }
            foreach (string fqn in inputs)
            {
                Console.WriteLine(fqn);
                Preset pre = new(fqn);
                List<string> a = pre.DisplayAll(showConnections: false);
                File.AppendAllLines(outFQN, a);
            }
        }
    }
}
