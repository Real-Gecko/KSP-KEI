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
		public static KEI Instance;
		private bool isActive = false;
		private bool firstRun = true;
		private bool isEnabled = false;
		private PluginConfiguration config;

		//GUI related members
		private ApplicationLauncherButton appLauncherButton;
		private bool showSettings = false;
		private Rect rectSettings = new Rect();
		private Rect RectSettings
		{
			get
			{
				rectSettings.x = (Screen.width - rectSettings.width) / 2;
				rectSettings.y = (Screen.height - rectSettings.height) / 2;
				return rectSettings;
			}
			set
			{
				rectSettings = value;
			}
		}

		//Public procedures
		public void Awake()
		{
			if (Instance != null) {
				Destroy (this);
				return;
			}
			Instance = this;
			if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER || HighLogic.CurrentGame.Mode == Game.Modes.SCIENCE_SANDBOX)
			{
				isActive = true;
				config = PluginConfiguration.CreateForType<KEI> ();
				config.load ();
				isEnabled = config.GetValue<bool>(HighLogic.CurrentGame.Title, false);
			}
		}

		public void Start()
		{
			if (isActive)
			{
				GameEvents.OnKSCFacilityUpgraded.Add(OnKSCFacilityUpgraded);
				GameEvents.OnTechnologyResearched.Add(OnTechnologyResearched);
				GameEvents.onGUIApplicationLauncherReady.Add(OnAppLauncherReady);
			}
		}

		void OnDestroy()
		{
			if (isActive)
			{
				GameEvents.OnKSCFacilityUpgraded.Remove(OnKSCFacilityUpgraded);
				GameEvents.OnTechnologyResearched.Remove(OnTechnologyResearched);
				GameEvents.onGUIApplicationLauncherReady.Remove(OnAppLauncherReady);
				if (appLauncherButton != null) {
					ApplicationLauncher.Instance.RemoveModApplication (appLauncherButton);
				}
			}
		}

		public void OnGUI()
		{
			if (!isActive) return;

			if (showSettings) {
				GUI.skin = UnityEngine.GUI.skin;
				RectSettings = GUILayout.Window(
					1248597845,
					RectSettings,
					SettingsDialog,
					"Kerbin Environmental Institute",
					GUILayout.ExpandWidth(true),
					GUILayout.ExpandHeight(true)
				);
			}

			if (!isEnabled) return;
			if (!firstRun) return;
			if (ResearchAndDevelopment.Instance != null && PartLoader.Instance != null) {
				RunExperiments();
				firstRun = false;
			}
		}

		//Private procedures
		private void RunExperiments() {
			if (!isEnabled) return;

			List<ScienceExperiment> experiments = new List<ScienceExperiment>();
			List<AvailablePart> parts = PartLoader.Instance.parts;

			// EVA Reports available from the beginning
			experiments.Add(ResearchAndDevelopment.GetExperiment("evaReport"));

			// To take surface samples from other worlds you need to upgrade Astronaut Complex and R&D
			// But to take surface samples from home you need to only upgrade R&D
			if (ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.ResearchAndDevelopment) >= 0.5)
				experiments.Add(ResearchAndDevelopment.GetExperiment("surfaceSample"));

			foreach (var part in parts.Where(x => ResearchAndDevelopment.PartTechAvailable(x)))
			{
				// Part has some modules
				if (part.partPrefab.Modules != null)
				{
					// Check through science modules
					foreach (ModuleScienceExperiment ex in part.partPrefab.Modules.OfType<ModuleScienceExperiment>())
					{
						experiments.AddUnique<ScienceExperiment>(ResearchAndDevelopment.GetExperiment(ex.experimentID));
					}
				}
			}
			GainScience(experiments);
		}

		private void OnKSCFacilityUpgraded(Upgradeables.UpgradeableFacility facility, int level) {
			if (!isEnabled) return;

			List<AvailablePart> parts = PartLoader.Instance.parts;
			List<ScienceExperiment> experiments = new List<ScienceExperiment>();

			// To take surface samples from other worlds you need to upgrade Astronaut Complex and R&D
			// But to take surface samples from home you need to only upgrade R&D
			if (ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.ResearchAndDevelopment) >= 0.5)
				experiments.Add(ResearchAndDevelopment.GetExperiment("surfaceSample"));

			// R&D upgraded we have to grab surface samples first
			if (facility.id == "SpaceCenter/ResearchAndDevelopment" && level == 1) {
				GainScience(experiments);
			}

			// EVA Reports available from the beginning
			experiments.Add(ResearchAndDevelopment.GetExperiment("evaReport"));

			// Find list of all unlocked experiments
			foreach (var part in parts.Where(x => ResearchAndDevelopment.PartTechAvailable(x)))
			{
				if (part.partPrefab.Modules != null) // part has some modules
				{
					// Check through science modules
					foreach (ModuleScienceExperiment module in part.partPrefab.Modules.OfType<ModuleScienceExperiment>())
					{
						experiments.AddUnique<ScienceExperiment>(ResearchAndDevelopment.GetExperiment(module.experimentID));
					}
				}
			}
			GainScience(experiments, facility, level);
		}

		private void OnTechnologyResearched(GameEvents.HostTargetAction<RDTech, RDTech.OperationResult> hta) {
			if (!isEnabled) return;

			if (hta.target == RDTech.OperationResult.Successful) // research was successfull
			{
				List<ScienceExperiment> experiments = new List<ScienceExperiment>();
				List<AvailablePart> parts = hta.host.partsAssigned;

				// To take surface samples from other worlds you need to upgrade Astronaut Complex and R&D
				// But to take surface samples from home you need to only upgrade R&D
				if (ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.ResearchAndDevelopment) >= 0.5)
					experiments.Add(ResearchAndDevelopment.GetExperiment("surfaceSample"));

				// EVA Reports available from the beginning
				experiments.Add(ResearchAndDevelopment.GetExperiment("evaReport"));

				foreach (AvailablePart part in parts)
				{
					if (part.partPrefab.Modules != null) // part has some modules
					{
						// Check through science modules
						foreach (ModuleScienceExperiment ex in part.partPrefab.Modules.OfType<ModuleScienceExperiment>())
						{
							experiments.AddUnique<ScienceExperiment>(ResearchAndDevelopment.GetExperiment(ex.experimentID));
						}
					}
				}
				GainScience(experiments);
			}
		}

		private void GainScience(List<ScienceExperiment> experiments, Upgradeables.UpgradeableFacility facility = null, int level = 0)
		{
			List<string> kscBiomes;
			CelestialBody Kerbin;
			float totalGain = 0.0f;

			// Find KSC biomes - stolen from [x] Science source code :D
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

			// Find da Kerbin
			Kerbin = FlightGlobals.Bodies.Find(x => x.bodyName == "Kerbin");

			// Let's complete all da experiments in all KSC biomes
			foreach (var experiment in experiments)
			{
				float gain = 0.0f;
				foreach (var biome in kscBiomes)
				{
					ScienceSubject subject = ResearchAndDevelopment.GetExperimentSubject(
						experiment,
						ExperimentSituations.SrfLanded,
						Kerbin,
						biome
					);
					if (subject.science < subject.scienceCap)
					{
						// We want to get full science reward
						subject.subjectValue = 1.0f;

						gain += ResearchAndDevelopment.Instance.SubmitScienceData(
							subject.scienceCap * subject.dataScale * HighLogic.CurrentGame.Parameters.Career.ScienceGainMultiplier,
							subject
						);
					}
				}
				totalGain += gain;
				if (gain >= 0.01f && facility == null) Report(experiment, gain);
			}
			if (facility != null && totalGain >= 0.01f) Report(facility, level, totalGain);
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

		private void Report(Upgradeables.UpgradeableFacility facility, int level, float gain)
		{
			string[] template = KSP.IO.File.ReadAllLines<KEI>("facilityUpgrade.msg");
			StringBuilder msg = new StringBuilder();
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
					toggleSettingsDialog,
					toggleSettingsDialog,
					null,
					null,
					null,
					null,
					ApplicationLauncher.AppScenes.SPACECENTER,
					GameDatabase.Instance.GetTexture("KEI/Textures/kei-icon", false)
				);
			}
		}

		private void toggleSettingsDialog()
		{
			showSettings = !showSettings;
		}

		private void SaveSettings () {
			appLauncherButton.SetFalse();
			isEnabled = config.GetValue<bool> (HighLogic.CurrentGame.Title);
			firstRun = true;
			config.save ();
		}

		private void SettingsDialog(int id)
		{
			GUILayout.BeginVertical();
			config[HighLogic.CurrentGame.Title] = GUILayout.Toggle(
				config.GetValue<bool>(HighLogic.CurrentGame.Title),
				"Make those lazybones work",
				GUILayout.Width(210)
			);
			if (GUILayout.Button ("Save & Close", GUILayout.Height (30)))
				SaveSettings ();
			GUILayout.EndVertical();
			GUI.DragWindow ();
		}
	}
}
