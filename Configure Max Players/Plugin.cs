using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Multiplayer;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using Image = UnityEngine.UI.Image;

namespace ConfigureMaxPlayers
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInProcess("Human.exe")]
    class Plugin : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger;
		internal static MenuSelector playerCountSelector;
		internal static ConfigFile theConfig;
		internal static ConfigEntry<int> configMaxPlayerRange;
		internal static MultiplayerLobbySettingsMenu mpLobbySettingsMenu;
		internal static GameObject rightArrow;

        private void Awake()
        {
            // Plugin startup logic
            Logger = base.Logger;
            Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
			theConfig = Config;

			configMaxPlayerRange = theConfig.Bind("General",
											   "Max Player Range",
											   64,
											   "Set the maximum range for maximum player count in online lobby settings");

			Shell.RegisterCommand("maxplayerrange",
								  new System.Action<string>((string txt) =>
															{
																if (!string.IsNullOrEmpty(txt))
																{
																	string[] words = txt.Split(new char[]
																	{
																		' '
																	}, System.StringSplitOptions.RemoveEmptyEntries);

																	if (words.Length != 1 || !int.TryParse(words[0], out int newMax))
																		return;

																	newMax = Mathf.Max(8, newMax); // prevent setting max player range to less than 8, the game's default max range
																	if (Options.lobbyMaxPlayers > newMax)
																		mpLobbySettingsMenu.MaxPlayersChanged(newMax - 2); // if the current max player count is higher than the new max player range, set it to the new max player range (subtract 2 since the in-game display starts at 2 but the actual value starts at 0)
																	SetMaxPlayerRange(playerCountSelector, newMax, true); // update max player range in-game and update config
																}
															 }),
								  "maxplayerrange <number>\r\nSet maximum range for maximum players for online lobby settings");
			Shell.RegisterCommand("maxplayers",
								  new System.Action<string>((string txt) =>
															{
																if (!string.IsNullOrEmpty(txt))
																{
																	string[] words = txt.Split(new char[]
																	{
																		' '
																	}, System.StringSplitOptions.RemoveEmptyEntries);

																	if (words.Length != 1 || !int.TryParse(words[0], out int newMax))
																		return;

																	newMax = Mathf.Max(2, newMax); // prevent setting max player count to less than 2, the game's default minimum range
																	mpLobbySettingsMenu.MaxPlayersChanged(newMax - 2);
																	playerCountSelector.selectedIndex = newMax - 2; // set the selected index to the new max player count (subtract 2 since the in-game display starts at 2 but the actual value starts at 0)
																	SetMaxPlayerRange(playerCountSelector, newMax); // update max player range in-game but not config
																}
															}),
								  "maxplayers <number>\r\nSet the maximum player count in online lobby settings");
        }

		private IEnumerator Start()
		{
			yield return new WaitUntil(() =>
									   {
										   if (FindObjectsOfType<MenuSelector>().Length == 0)
											   return false;
										   playerCountSelector = FindObjectsOfType<MenuSelector>().
										   Where(menuSelector => menuSelector.name == "PlayerCountSelector").First();
										   
										   return playerCountSelector;
									   });
			yield return new WaitUntil(() =>
									   {
										   mpLobbySettingsMenu = FindObjectOfType<MultiplayerLobbySettingsMenu>();
										   
										   return mpLobbySettingsMenu;
									   });
			yield return new WaitUntil(() =>
									   {
										   rightArrow = playerCountSelector.GetComponentsInChildren<Image>().
										   Where(image => image.name == "Right").First().gameObject;
										   
										   return rightArrow;
									   });
			if (Options.lobbyMaxPlayers > configMaxPlayerRange.Value) // if the current max player count is higher than the max player range in config, set it to the max player range in config
				mpLobbySettingsMenu.MaxPlayersChanged(configMaxPlayerRange.Value - 2); // subtract 2 since the in-game display starts at 2 but the actual value starts at 0
			SetMaxPlayerRange(playerCountSelector, configMaxPlayerRange.Value);
		}

		protected static void SetMaxPlayerRange(MenuSelector instance, int newMaxPlayerCount, bool updateConfig = false) // change the maximum for the range for max player count that can be set in lobby settings
        {
			newMaxPlayerCount = Mathf.Max(8, newMaxPlayerCount); // prevent setting max player count to less than 8, the game's default max range
			int oldMaxPlayerCount = instance.optionLabels.Length + 1; // 2-8 by default, so add 1 to get the actual max player count (since list starts at 0 but displays as 2 in-game)
			int amtToChange = newMaxPlayerCount - oldMaxPlayerCount;

			if (amtToChange != 0)
			{
				Logger.LogInfo($"{(amtToChange > 0 ? "Increasing" : "Decreasing")} maximum number of players from {oldMaxPlayerCount} to {newMaxPlayerCount} ({(amtToChange > 0 ? "+" + amtToChange : amtToChange)})");
				TextMeshProUGUI firstTextObject = instance.optionLabels[0];
				List<TextMeshProUGUI> optionLabelsList = instance.optionLabels.ToList();

				for (int i = 0; i < amtToChange; i++) // if positive
				{
					TextMeshProUGUI numberText = Clone_TextMeshProUGUI(firstTextObject);
					string numberString = (instance.optionLabels.Length + i + 2).ToString(); // + 2 since list starts at 0 but displays as 2 in-game

					numberText.SetText(numberString);
					numberText.name = numberString;
					optionLabelsList.Add(numberText);
				}
				for (int i = 0; i < -amtToChange; i++) // if negative
				{
					GameObject oldNumberTextObject = optionLabelsList.ElementAt(optionLabelsList.Count - 1).gameObject;
					optionLabelsList.RemoveAt(optionLabelsList.Count - 1); // remove extras
					Destroy(oldNumberTextObject); // destroy unused object to prevent memory leak
				}

				rightArrow.transform.SetSiblingIndex(optionLabelsList.Count + 1); // move the right arrow to the end of the list, so that the new numbers display properly (otherwise they display right of the right arrow)
				instance.optionLabels = optionLabelsList.ToArray();
				Logger.LogInfo($"Successfully set max player count to {newMaxPlayerCount}!");

				if (updateConfig)
				{
					configMaxPlayerRange.Value = newMaxPlayerCount; // update max player range in config
					theConfig.Save(); // save config changes
				}
			}

            instance.RebindValue();
		}

        private static TextMeshProUGUI Clone_TextMeshProUGUI(TextMeshProUGUI textObject)
        {
			GameObject cloneTextGameObject = Instantiate(textObject).gameObject;

			cloneTextGameObject.transform.SetParent(textObject.transform.parent);
			cloneTextGameObject.transform.SetPositionAndRotation(textObject.transform.position, textObject.transform.rotation);
			cloneTextGameObject.transform.localScale = textObject.transform.localScale;

			return cloneTextGameObject.GetComponent<TextMeshProUGUI>();
        }
	}
}