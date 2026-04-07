using BepInEx;
using HarmonyLib;
using System.Linq;
using UnityEngine;

namespace CrewFetch
{
    [BepInPlugin("com.quorix.crewfetchmod", "Crew Fetch Mod", "1.0.0")]
    public class CrewFetch : BaseUnityPlugin
    {
        private void Awake()
        {
            Logger.LogInfo("Crew Fetch Mod by Quorix loaded and initialized.");

            var harmony = new Harmony("com.quorix.crewfetchmod");
            harmony.PatchAll();
        }
    }

    // --- PATCH FOR ADD COMMAND TO "OTHER" MENU ---
    [HarmonyPatch(typeof(Terminal))]
    internal class HelpMenuPatch
    {
        [HarmonyPatch("Awake")]
        [HarmonyPostfix]
        private static void AddToHelpMenu(Terminal __instance)
        {
            TerminalKeyword otherKeyword = __instance.terminalNodes.allKeywords.FirstOrDefault(k => k.word == "other");

            if (otherKeyword != null && otherKeyword.specialKeywordResult != null)
            {
                string helpText = otherKeyword.specialKeywordResult.displayText;

                if (!helpText.Contains(">CREWFETCH"))
                {
                    otherKeyword.specialKeywordResult.displayText = helpText.TrimEnd('\n') +
                        "\n\n>CREWFETCH\nDisplays system and ship information.\n\n";
                }
            }
        }
    }

    // --- PATCH FOR COMMAND CREWFETCH ---
    [HarmonyPatch(typeof(Terminal))]
    internal class CrewFetchCommandPatch
    {
        private static TerminalNode crewfetchNode;

        [HarmonyPatch("Awake")]
        [HarmonyPostfix]
        private static void AddCrewfetchCommand(Terminal __instance)
        {
            if (__instance.terminalNodes.allKeywords.Any(k => k.word == "crewfetch")) return;

            crewfetchNode = ScriptableObject.CreateInstance<TerminalNode>();
            crewfetchNode.name = "CrewfetchCommandNode";
            crewfetchNode.displayText = "Gathering system info...\n\n";
            crewfetchNode.clearPreviousText = true;

            TerminalKeyword myKeyword = ScriptableObject.CreateInstance<TerminalKeyword>();
            myKeyword.name = "CrewfetchCommand";
            myKeyword.word = "crewfetch";
            myKeyword.isVerb = false;
            myKeyword.specialKeywordResult = crewfetchNode;

            var keywordsList = __instance.terminalNodes.allKeywords.ToList();
            keywordsList.Add(myKeyword);
            __instance.terminalNodes.allKeywords = keywordsList.ToArray();
        }

        [HarmonyPatch("LoadNewNode")]
        [HarmonyPrefix]
        private static void OnLoadNewNode(ref TerminalNode node, Terminal __instance)
        {
            if (node != null && node == crewfetchNode)
            {
                node.displayText = GenerateCrewfetchText(__instance) + "\n\n";
            }
        }

        private static string GenerateCrewfetchText(Terminal terminal)
        {
            int alivePlayers = StartOfRound.Instance.allPlayerScripts.Count(p => p.isPlayerControlled && !p.isPlayerDead);
            int totalPlayers = StartOfRound.Instance.allPlayerScripts.Count(p => p.isPlayerControlled || p.isPlayerDead);

            GrabbableObject[] allItems = Object.FindObjectsOfType<GrabbableObject>();
            int scrapCount = 0;
            int totalScrapValue = 0;

            foreach (var item in allItems)
            {
                if (item.itemProperties.isScrap && item.isInShipRoom)
                {
                    scrapCount++;
                    totalScrapValue += item.scrapValue;
                }
            }

            string moonName = StartOfRound.Instance.currentLevel.PlanetName;
            string weather = StartOfRound.Instance.currentLevel.currentWeather.ToString();
            int credits = terminal.groupCredits;

            int quota = TimeOfDay.Instance.profitQuota;
            int fulfilled = TimeOfDay.Instance.quotaFulfilled;
            int daysLeft = TimeOfDay.Instance.daysUntilDeadline;

            return $@"
            system@company-os
            -----------------
            Current Moon: {moonName} ({weather})
            Credits Balance: ${credits}
   ______   [ CREW STATUS ]
  / ____/   Alive Employees: {alivePlayers} / {totalPlayers}
 / /     
/ /___      [ SHIP INVENTORY ]
\____/      Scrap Collected (Items): {scrapCount}
            Estimated Value: ${totalScrapValue}
            
            [ COMPANY QUOTA ]
            Fulfilled: ${fulfilled} / ${quota}
            Days Left: {daysLeft}
";
        }
    }
}
