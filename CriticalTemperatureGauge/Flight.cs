﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KSP.UI.Screens;
using ToolbarControl_NS;

namespace CriticalTemperatureGauge
{
	/// <summary>
	/// Main add-on class.
	/// </summary>
	[KSPAddon(KSPAddon.Startup.Flight, false)]
	public class Flight : MonoBehaviour
	{
		readonly Window _gaugeWindow = new GaugeWindow();
		readonly Highlighter _highlighter = new Highlighter();
		readonly SettingsWindow _settingsWindow = new SettingsWindow();
		ToolbarControl _toolbarControl;

		// Toolbar control:

		void CreateToolbarControl()
		{
			if(_toolbarControl == null)
			{
				_toolbarControl = gameObject.AddComponent<ToolbarControl>();
				_toolbarControl.AddToAllToolbars(
					onTrue: OnButtonToggle,
					onFalse: OnButtonToggle,
					visibleInScenes: ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.MAPVIEW,
					nameSpace: Static.PluginId,
					toolbarId: $"{Static.PluginId}Settings",
					largeToolbarIcon: $"{Static.TexturePath}ToolbarIcon-57",
          smallToolbarIcon: $"{Static.TexturePath}ToolbarIcon-24",
					toolTip: Static.PluginTitle);
			}
		}

		void DestroyToolbarControl()
		{
			if(_toolbarControl != null)
			{
				_toolbarControl.OnDestroy();
				Destroy(_toolbarControl);
				_toolbarControl = null;
			}
		}

		void OnButtonToggle()
		{
			_toolbarControl.SetFalse(makeCall: false);
			_settingsWindow.Toggle();
		}

		// KSP events:

		public void Start()
		{
			Debug.Log($"{Static.PluginId}: Entering scene {HighLogic.LoadedScene}.");
			CreateToolbarControl();
		}

		public void Awake()
		{
			GameEvents.onShowUI.Add(OnShowUI);
			GameEvents.onHideUI.Add(OnHideUI);
		}

		public void OnDestroy()
		{
			try
			{
				Static.Settings.Save();
				GameEvents.onShowUI.Remove(OnShowUI);
				GameEvents.onHideUI.Remove(OnHideUI);
				_gaugeWindow.Hide();
				_settingsWindow.Hide();
				Static.CriticalPartState = null;
				DestroyToolbarControl();
				Debug.Log($"{Static.PluginId}: Exiting scene {HighLogic.LoadedScene}.");
			}
			catch(Exception exception)
			{
				Debug.Log($"{Static.PluginId}: Exception during exiting scene {HighLogic.LoadedScene}: {exception}");
			}
		}

		void OnShowUI()
		{
			_gaugeWindow.CanShow = true;
			_settingsWindow.CanShow = true;
		}

		void OnHideUI()
		{
			_gaugeWindow.CanShow = false;
			_settingsWindow.CanShow = false;
		}

		public void OnGUI()
		{
			_gaugeWindow.DrawGUI();
			_settingsWindow.DrawGUI();
		}

		public void Update()
		{
			var vessel = FlightGlobals.ActiveVessel;
			if(vessel != null)
			{
				// Updating critical part state
				var criticalPartState = GetCriticalPartState(vessel);
				//criticalPartState?.UpdateTemperatureRates(Static.CriticalPartState);
				Static.CriticalPartState = criticalPartState;

				// Determining if the gauge should be shown or hidden
				if(!_gaugeWindow.IsLogicallyVisible &&
					criticalPartState != null &&
					(criticalPartState.Index > Static.Settings.GaugeShowingThreshold ||
					Static.Settings.AlwaysShowGauge))
				{
					_gaugeWindow.Show();
				}
				else if(_gaugeWindow.IsLogicallyVisible &&
					(criticalPartState == null ||
					criticalPartState.Index < Static.Settings.GaugeHidingThreshold &&
					!Static.Settings.AlwaysShowGauge))
				{
					_gaugeWindow.Hide();
				}

				// Highlighting the critical part if needed
				_highlighter.SetHighlightedPart(
					Static.Settings.HighlightCriticalPart &&
					criticalPartState != null &&
					(_highlighter.IsThereHighlightedPart &&
					criticalPartState.Index > Static.Settings.GaugeHidingThreshold ||
					criticalPartState.Index > Static.Settings.GaugeShowingThreshold)
						? criticalPartState.part
						: null);
			}
		}

		/// <summary>Finds the part with the greatest Temp/TempLimit ratio.</summary>
		/// <param name="vessel">Current vessel.</param>
		/// <returns>Critical part state.</returns>
		static PartTemperatureState GetCriticalPartState(Vessel vessel) =>
			vessel.parts
				.Where(IsPartNotIgnored)
				.Select(GetPartState)
				.OfType<PartTemperatureState>()
				.OrderByDescending(partState => partState.Index)
				.FirstOrDefault();

		/// <summary>Gets parameters of a part.</summary>
		/// <param name="part">A vessel part.</param>
		/// <returns>Part state.</returns>
		static PartTemperatureState GetPartState(Part part) =>
			part.Modules.GetModules<PartTemperatureState>().FirstOrDefault();

		/// <summary>Determines if the part has a module containing in the exclusion list.</summary>
		/// <param name="part">A vessel part.</param>
		/// <returns><c>false</c> if the part is ignored; <c>true</c> otherwise.</returns>
		static bool IsPartNotIgnored(Part part) =>
			!(Static.Settings.UseExclusionList &&
				Static.Settings.ExclusionListItems.Any(moduleName => part.Modules.Contains(moduleName)));
	}
}
