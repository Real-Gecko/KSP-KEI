using System.Text;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using KSP.UI.Screens;
using KSP.IO;

namespace KEI
{
    [KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
    class KEI : MonoBehaviour
    {
        private class AvailableExperiment
        {
            public ScienceExperiment experiment;
            public float possibleGain;
            public bool done;
        }

        public static KEI Instance;
        private bool isActive;

        private List<AvailableExperiment> availableExperiments;
        private List<ScienceExperiment> unlockedExperiments;
        private List<string> kscBiomes;
        private CelestialBody HomeBody;

        //GUI related members
        private ApplicationLauncherButton appLauncherButton;
        private int mainWindowId;
        private Rect mainWindowRect;
        private bool mainWindowVisible;
        private Vector2 mainWindowScrollPosition;

        //Public procedures
        public void Awake()
        {
            if (Instance != null)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            isActive = false;
            if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER || HighLogic.CurrentGame.Mode == Game.Modes.SCIENCE_SANDBOX)
            {
                isActive = true;
                availableExperiments = new List<AvailableExperiment>();
                unlockedExperiments = new List<ScienceExperiment>();
                kscBiomes = new List<string>();
                mainWindowRect = new Rect();
                mainWindowScrollPosition = new Vector2();
                mainWindowVisible = false;
            }
        }

        private static string[] excludedExperiments;

        public void ModuleManagerPostLoad()
        {
            if (excludedExperiments != null)
                return;
            Debug.Log("ModuleManagerPostLoad");
            List<string> expList = new List<string>();
            ConfigNode[] excludedNode = GameDatabase.Instance.GetConfigNodes("KEI_EXCLUDED_EXPERIMENTS");
            if (excludedNode != null)
            {
                int len1 = excludedNode.Length;
                for (int i = 0; i < len1; i++)
                {
                    string[] types = excludedNode[i].GetValues("experiment");
                    expList.AddRange(types);
                }
                excludedExperiments = expList.ToArray();
            }
            else
            {
                Debug.Log("Missing config file");
                excludedExperiments = expList.ToArray();
            }
        }

        public void Start()
        {
            if (isActive)
            {
                HomeBody = FlightGlobals.GetHomeBody();
                mainWindowId = GUIUtility.GetControlID(FocusType.Passive);
                mainWindowRect.width = 400;
                mainWindowRect.x = (Screen.width - 400) / 2;
                mainWindowRect.y = Screen.height / 4;
                mainWindowScrollPosition.Set(0, 0);

                GameEvents.onGUIApplicationLauncherReady.Add(OnAppLauncherReady);
                GameEvents.OnKSCFacilityUpgraded.Add(OnKSCFacilityUpgraded);
                GameEvents.onGUIRnDComplexSpawn.Add(SwitchOff);
                GameEvents.onGUIAstronautComplexSpawn.Add(SwitchOff);

                ModuleManagerPostLoad();
            }
        }

        void OnDestroy()
        {
            if (isActive)
            {
                GameEvents.onGUIApplicationLauncherReady.Remove(OnAppLauncherReady);
                GameEvents.OnKSCFacilityUpgraded.Remove(OnKSCFacilityUpgraded);
                GameEvents.onGUIRnDComplexSpawn.Remove(SwitchOff);
                GameEvents.onGUIAstronautComplexSpawn.Remove(SwitchOff);
                if (appLauncherButton != null)
                    ApplicationLauncher.Instance.RemoveModApplication(appLauncherButton);
            }
        }

        private void OnKSCFacilityUpgraded(Upgradeables.UpgradeableFacility facility, int level)
        {
            appLauncherButton.SetFalse();
        }

        private void SwitchOff()
        {
            appLauncherButton.SetFalse();
        }

        public void OnGUI()
        {
            if (!isActive) return;
            if (mainWindowVisible)
            {
                mainWindowRect = GUILayout.Window(
                    mainWindowId,
                    mainWindowRect,
                    RenderMainWindow,
                    HomeBody.theName + " Environmental Institute",
                    GUILayout.ExpandWidth(true),
                    GUILayout.ExpandHeight(true)
                );
            }
        }

        private void GetKscBiomes()
        {
            // Find KSC biomes - stolen from [x] Science source code :D
            kscBiomes.Clear();
            kscBiomes = UnityEngine.Object.FindObjectsOfType<Collider>()
                .Where(x => x.gameObject.layer == 15)
                .Select(x => x.gameObject.tag)
                .Where(x => x != "Untagged")
                .Where(x => !x.Contains("KSC_Runway_Light"))
                .Where(x => !x.Contains("KSC_Pad_Flag_Pole"))
                .Where(x => !x.Contains("Ladder"))
                .Select(x => Vessel.GetLandedAtString(x))
                .Select(x => x.Replace(" ", ""))
                .Distinct()
                .ToList();
        }

        // List available experiments
        private void GetExperiments()
        {
            unlockedExperiments.Clear();
            availableExperiments.Clear();

            List<AvailablePart> parts = PartLoader.Instance.loadedParts;

            // EVA Reports available from the beginning
            unlockedExperiments.Add(ResearchAndDevelopment.GetExperiment("evaReport"));

            // To take surface samples from other worlds you need to upgrade Astronaut Complex and R&D
            // But to take surface samples from home you need to only upgrade R&D
            if (ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.ResearchAndDevelopment) > 0.0)
                unlockedExperiments.Add(ResearchAndDevelopment.GetExperiment("surfaceSample"));

            foreach (var part in parts.Where(x => ResearchAndDevelopment.PartTechAvailable(x) && x.manufacturer != "Station Science Directorate"))
            {
                // Part has some modules
                if (part.partPrefab.Modules != null)
                {
                    // Check science modules
                    foreach (ModuleScienceExperiment ex in part.partPrefab.Modules.OfType<ModuleScienceExperiment>())
                    {
                        // Remove experiments with empty ids, by [Kerbas-ad-astra](https://github.com/Kerbas-ad-astra)
                        // Remove Surface Experiments Pack experiments not meant to run in atmosphere
                        if (ex.experimentID == null)
                        {
                            Debug.Log("name: " + part.name + "   experimentID is null");
                        }
                        if (ex.experimentID != null && ex.experimentID != "" && !excludedExperiments.Contains(ex.experimentID))
                        {
                            unlockedExperiments.AddUnique<ScienceExperiment>(ResearchAndDevelopment.GetExperiment(ex.experimentID));
                        }
                    }
                }
            }
        }

        private void GainScience(List<ScienceExperiment> experiments, bool analyze)
        {
            // Let's get science objects in all KSC biomes
            foreach (var experiment in experiments.Where(x => x.IsAvailableWhile(ExperimentSituations.SrfLanded, HomeBody)))
            {
                float gain = 0.0f;
                foreach (var biome in kscBiomes)
                {
                    ScienceSubject subject = ResearchAndDevelopment.GetExperimentSubject(
                        experiment,
                        ExperimentSituations.SrfLanded,
                        HomeBody,
                        biome
                    );
                    if (subject.science < subject.scienceCap)
                    {
                        if (analyze)
                        {
                            gain += (subject.scienceCap - subject.science) * HighLogic.CurrentGame.Parameters.Career.ScienceGainMultiplier;
                        }
                        else
                        {
                            // We want to get full science reward
                            subject.subjectValue = 1.0f;

                            gain += ResearchAndDevelopment.Instance.SubmitScienceData(
                                subject.scienceCap * subject.dataScale,
                                subject
                            );
                        }
                    }
                }
                if (gain >= 0.01f)
                {
                    if (analyze)
                        availableExperiments.Add(
                            new AvailableExperiment
                            {
                                experiment = experiment,
                                possibleGain = gain,
                                done = false
                            }
                        );
                    else
                        Report(experiment, gain);
                }
            }
        }

        private void Report(ScienceExperiment experiment, float gain)
        {
            StringBuilder msg = new StringBuilder();
            string[] template;
            if (File.Exists<KEI>(experiment.id + ".msg"))
            {
                template = File.ReadAllLines<KEI>(experiment.id + ".msg");
            }
            else
            {
                template = File.ReadAllLines<KEI>("unknownExperiment.msg");
                msg.AppendLine("Top Secret info! Project " + experiment.experimentTitle);
                msg.AppendLine("Eat after reading");
                msg.AppendLine("And drink some coffee");
                msg.AppendLine("****");
            }
            foreach (var line in template)
            {
                msg.AppendLine(line);
            }
            msg.AppendLine("");
            msg.AppendLine(string.Format("<color=#B4D455>Total science gain: {0}</color>", gain.ToString("0.00")));

            MessageSystem.Message message = new MessageSystem.Message(
                "New Email",
                msg.ToString(),
                MessageSystemButton.MessageButtonColor.GREEN,
                MessageSystemButton.ButtonIcons.MESSAGE
            );
            MessageSystem.Instance.AddMessage(message);
        }

        //GUI related functions
        private void OnAppLauncherReady()
        {
            if (appLauncherButton == null)
            {
                appLauncherButton = ApplicationLauncher.Instance.AddModApplication(
                    ShowMainWindow,
                    HideMainWindow,
                    null,
                    null,
                    null,
                    null,
                    ApplicationLauncher.AppScenes.SPACECENTER,
                    GameDatabase.Instance.GetTexture("KEI/Textures/kei-icon", false)
                );
            }
        }

        private void ShowMainWindow()
        {
            GetKscBiomes();
            GetExperiments();
            GainScience(unlockedExperiments, true);
            mainWindowVisible = true;
        }

        private void HideMainWindow()
        {
            mainWindowVisible = false;
        }

        private void RenderMainWindow(int windowId)
        {
            GUILayout.BeginVertical();
            if (availableExperiments.Count > 0)
            {
                mainWindowScrollPosition = GUILayout.BeginScrollView(mainWindowScrollPosition, GUILayout.Height(Screen.height / 2));
                //				foreach (var available in availableExperiments)
                for (var i = 0; i < availableExperiments.Count; i++)
                {
                    if (availableExperiments[i].done) GUI.enabled = false;
                    if (GUILayout.Button(availableExperiments[i].experiment.experimentTitle + " " + availableExperiments[i].possibleGain.ToString("0.00"), GUILayout.Height(25)))
                    {
                        var l = new List<ScienceExperiment>();
                        l.Add(availableExperiments[i].experiment);
                        GainScience(l, false);
                        availableExperiments[i].done = true;
                    }
                    if (!GUI.enabled) GUI.enabled = true;
                }
                GUILayout.EndScrollView();
                GUILayout.Space(10);
                if (availableExperiments.Where(x => !x.done).Count() == 0) GUI.enabled = false;
                if (GUILayout.Button("Make me happy! " + availableExperiments.Where(x => !x.done).Select(x => x.possibleGain).Sum().ToString("0.00"), GUILayout.Height(25)))
                {
                    availableExperiments.ForEach(x => x.done = true);
                    GainScience(availableExperiments.Select(x => x.experiment).ToList(), false);
                }
                if (!GUI.enabled) GUI.enabled = true;
            }
            else
            {
                GUILayout.Label("Nothing to do here, go research something.");
            }
            if (GUILayout.Button("Close", GUILayout.Height(25)))
                appLauncherButton.SetFalse();
            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private void Log(string message)
        {
            Debug.Log("KEI debug: " + message);
        }
    }
}
