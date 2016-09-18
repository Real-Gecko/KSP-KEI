//using System;
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
			if (Instance != null) {
				Destroy (this);
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

		public void Start()
		{
			if (isActive)
			{
				mainWindowId = GUIUtility.GetControlID(FocusType.Passive);
				mainWindowRect.width = 400;
				mainWindowRect.x = (Screen.width - 400) / 2;
				mainWindowRect.y = Screen.height / 4;
				mainWindowScrollPosition.Set(0, 0);

				GameEvents.OnKSCFacilityUpgraded.Add(OnKSCFacilityUpgraded);
				GameEvents.OnTechnologyResearched.Add(OnTechnologyResearched);
				GameEvents.onGUIApplicationLauncherReady.Add(OnAppLauncherReady);
				GameEvents.onGUIRnDComplexSpawn.Add(onGUIRnDComplexSpawn);
			}
		}

		void OnDestroy()
		{
			if (isActive)
			{
				GameEvents.OnKSCFacilityUpgraded.Remove(OnKSCFacilityUpgraded);
				GameEvents.OnTechnologyResearched.Remove(OnTechnologyResearched);
				GameEvents.onGUIApplicationLauncherReady.Remove(OnAppLauncherReady);
				GameEvents.onGUIRnDComplexSpawn.Remove(onGUIRnDComplexSpawn);
				if (appLauncherButton != null)
					ApplicationLauncher.Instance.RemoveModApplication (appLauncherButton);
			}
		}

		private void OnKSCFacilityUpgraded(Upgradeables.UpgradeableFacility facility, int level)
		{
			appLauncherButton.SetFalse();
		}

		private void OnTechnologyResearched(GameEvents.HostTargetAction<RDTech, RDTech.OperationResult> hta)
		{
			// Research was successfull
			if (hta.target == RDTech.OperationResult.Successful)
			{
			}
		}

		private void onGUIRnDComplexSpawn()
		{
			appLauncherButton.SetFalse();
		}

		public void OnGUI()
		{
			if (!isActive) return;
			if (mainWindowVisible) {
				GUI.skin = UnityEngine.GUI.skin;
				mainWindowRect = GUILayout.Window(
					mainWindowId,
					mainWindowRect,
					RenderMainWindow,
					"Kerbin Environmental Institute",
					GUILayout.ExpandWidth(true),
					GUILayout.ExpandHeight(true)
				);
			}
		}

		private void GetKscBiomes() {
			// Find home body
			HomeBody = FlightGlobals.GetHomeBody();

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

		private void GetExperiments() {
			unlockedExperiments.Clear();
			availableExperiments.Clear();

			// List available experiments
			List<AvailablePart> parts = PartLoader.Instance.parts;

			// EVA Reports available from the beginning
			unlockedExperiments.Add(ResearchAndDevelopment.GetExperiment("evaReport"));

			// To take surface samples from other worlds you need to upgrade Astronaut Complex and R&D
			// But to take surface samples from home you need to only upgrade R&D
			if (ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.ResearchAndDevelopment) >= 0.5)
				unlockedExperiments.Add(ResearchAndDevelopment.GetExperiment("surfaceSample"));

			foreach (var part in parts.Where(x => ResearchAndDevelopment.PartTechAvailable(x) && x.manufacturer != "Station Science Directorate"))
			{
				// Part has some modules
				if (part.partPrefab.Modules != null)
				{
					// Check science modules
					foreach (ModuleScienceExperiment ex in part.partPrefab.Modules.OfType<ModuleScienceExperiment>())
						if (!ex.experimentID.Equals(""))
						{
							unlockedExperiments.AddUnique<ScienceExperiment>(ResearchAndDevelopment.GetExperiment(ex.experimentID));
						}
				}
			}
			// Remove Surface Experiments Pack experiments not meant to run in atmosphere
			unlockedExperiments.Remove(ResearchAndDevelopment.GetExperiment("SEP_SolarwindSpectrum"));
			unlockedExperiments.Remove(ResearchAndDevelopment.GetExperiment("SEP_CCIDscan"));
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
						else {
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
			if (KSP.IO.File.Exists<KEI>(experiment.id + ".msg"))
			{
				template = KSP.IO.File.ReadAllLines<KEI>(experiment.id + ".msg");
			}
			else
			{
				template = KSP.IO.File.ReadAllLines<KEI>("unknownExperiment.msg");
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
		private void OnAppLauncherReady() {
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

		private void ShowMainWindow() {
			GetKscBiomes();
			GetExperiments();
			GainScience(unlockedExperiments, true);
			mainWindowVisible = true;
		}

		private void HideMainWindow() {
			mainWindowVisible = false;
		}

		private void RenderMainWindow(int windowId) {
			GUILayout.BeginVertical();
			if (availableExperiments.Count > 0)
			{
				mainWindowScrollPosition = GUILayout.BeginScrollView(mainWindowScrollPosition, GUILayout.Height(Screen.height/2));
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
			else {
				GUILayout.Label("Nothing to do here, go research something.");
			}
			if (GUILayout.Button("Close", GUILayout.Height(25)))
				appLauncherButton.SetFalse();
			GUILayout.EndVertical();
			GUI.DragWindow();
		}
	}
}
