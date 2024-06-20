using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Oxide.Core.Libraries.Covalence;
using Newtonsoft.Json;

//1.0.1 Adds Scrap Leaderboard on interval that lists top 5 balances. 
//1.0.2 Added Config options for announcement interval adjustment and number of players to announce.
//1.1.0 Added SFX on ui press. Removed negative balance possibility, with an announcement (cont. below)
//1.1.0 cont. That the player cant afford the fee. Added color and Scraponomics tag. & centered ui, added coloring.
//1.1.1 Changed SFX from register fx to scrap slot machine fx. Changed some coloring.
//1.1.2 Added checks for 0 Balances to be skipped on announcement.
//1.1.3 Added Discord Logging for top balances on announce.
namespace Oxide.Plugins
{
    [Info("Scraponomics Lite", "haggbart, Wrecks", "1.1.3")]
    [Description("Adds ATM UI with simple, intuitive functionality to vending machines and bandit vendors")]
    internal class ScraponomicsLite : RustPlugin
    {
        #region localization

        private const string LOC_PAID_BROKERAGE = "PaidBrokerage";
        private const string LOC_DEPOSIT = "Deposit";
        private const string LOC_WITHDRAW = "Withdraw";
        private const string LOC_TOTAL = "Total";
        private const string LOC_BALANCE = "Balance";
        private const string LOC_ATM = "SCRAP ATM";
        private const string LOC_REWARD_INTEREST = "RewardInterest";
        private const string LOC_INSUFFICIENT_FUNDS = "InsufficientFunds";
        private string announce = "assets/prefabs/misc/casino/slotmachine/effects/payout.prefab";
        private string increment = "assets/prefabs/tools/detonator/effects/unpress.prefab";
        private string deposit = "assets/prefabs/deployable/dropbox/effects/submit_items.prefab";

        private string withdraw =
            "assets/prefabs/deployable/vendingmachine/effects/vending-machine-purchase-human.prefab";

        private string insufficientfunds = "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab";


        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [LOC_PAID_BROKERAGE] = "<color=#FF5733>[Scraponomics]</color> You paid the Brokerage fee of {0} Scrap.",
                [LOC_DEPOSIT] = "Deposit",
                [LOC_WITHDRAW] = "Withdraw",
                [LOC_BALANCE] = "Balance : <color=#FF5733>{0}</color> Scrap",
                [LOC_TOTAL] = "Total",
                [LOC_ATM] = "SCRAP ATM",
                [LOC_REWARD_INTEREST] = "<color=#FF5733>[Scraponomics]</color> You've earned {0} Scrap in interest.",
                [LOC_INSUFFICIENT_FUNDS] =
                    "<color=#FF5733>[Scraponomics]</color> Insufficient funds to cover Fees and Withdraw amount."
            }, this);
        }

        #endregion localization 

        #region data

        private void SaveData() =>
            Interface.Oxide.DataFileSystem.WriteObject(Name, playerData);

        private void ReadData() =>
            playerData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerData>>(Name);

        private static Dictionary<ulong, PlayerData> playerData;

        private static readonly Dictionary<ulong, PlayerPreference> playerPrefs =
            new Dictionary<ulong, PlayerPreference>();

        private class PlayerData
        {
            public int scrap { get; set; }
            public DateTime lastInterest = DateTime.UtcNow;
        }

        private class PlayerPreference
        {
            public int amount { get; set; }
        }

        #endregion data

        #region config

        private PluginConfig config;

        private class PluginConfig
        {
            public float feesFraction;
            public int startingBalance;
            public bool allowPlayerVendingMachines;
            public bool resetOnMapWipe;
            public float interestRate;
            public int leaderboardAnnounceIntervalSeconds;
            public int leaderboardAnnouncePlayerCount;
            public string discordwebhookURL;
        }

        protected override void LoadDefaultConfig()
        {
            config = GetDefaultConfig();

            if (Config.Get("feesFraction") == null)
                Config.Set("feesFraction", config.feesFraction);
            if (Config.Get("startingBalance") == null)
                Config.Set("startingBalance", config.startingBalance);
            if (Config.Get("allowPlayerVendingMachines") == null)
                Config.Set("allowPlayerVendingMachines", config.allowPlayerVendingMachines);
            if (Config.Get("resetOnMapWipe") == null)
                Config.Set("resetOnMapWipe", config.resetOnMapWipe);
            if (Config.Get("interestRate") == null)
                Config.Set("interestRate", config.interestRate);
            if (Config.Get("leaderboardAnnounceIntervalSeconds") == null)
                Config.Set("leaderboardAnnounceIntervalSeconds", config.leaderboardAnnounceIntervalSeconds);
            if (Config.Get("leaderboardAnnouncePlayerCount") == null)
                Config.Set("leaderboardAnnouncePlayerCount", config.leaderboardAnnouncePlayerCount);
            if (Config.Get("discordwebhookURL") == null)
                Config.Set("discordwebhookURL", config.discordwebhookURL);

            SaveConfig();
        }


        private static PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {
                feesFraction = 0.05f,
                startingBalance = 50,
                allowPlayerVendingMachines = false,
                resetOnMapWipe = true,
                interestRate = 0.10f,
                leaderboardAnnounceIntervalSeconds = 720,
                leaderboardAnnouncePlayerCount = 5
            };
        }

        #endregion config

        #region init

        private void Init()
        {
            config = Config.ReadObject<PluginConfig>();
            AddCovalenceCommand("scrapannounce", "CmdScrapAnnounce");

            if (!config.resetOnMapWipe)
            {
                Unsubscribe(nameof(OnNewSave));
            }

            SaveConfig();
            ReadData();
        }

        private void InitPlayerData(BasePlayer player)
        {
            var playerbalances = new PlayerData
            {
                scrap = config.startingBalance
            };
            playerData.Add(player.userID, playerbalances);
        }

        private static void InitPlayerPerference(BasePlayer player)
        {
            var playerPreference = new PlayerPreference
            {
                amount = 100
            };
            playerPrefs.Add(player.userID, playerPreference);
        }

        private void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                DestroyGuiAll(player);
            }

            SaveData();
        }

        #endregion init

        #region methods

        private void DoInterest(BasePlayer player)
        {
            PlayerData data = playerData[player.userID];

            if (data.scrap < 1) return;

            TimeSpan timeSinceLastInterest = DateTime.UtcNow - data.lastInterest;
            if (timeSinceLastInterest.Days == 0)
            {
                return;
            }

            int interest = (int)(data.scrap * Math.Pow(config.interestRate + 1.0f,
                timeSinceLastInterest.TotalSeconds / 86400.0)) - data.scrap;

            if (interest < 1) return;
            data.scrap += interest;
            data.lastInterest = DateTime.UtcNow;

            SendReply(player, lang.GetMessage(LOC_REWARD_INTEREST, this, player.UserIDString), interest);
        }

        #endregion methods

        #region hooks

        private void OnServerSave()
        {
            SaveData();
        }

        private void OnNewSave(string filename)
        {
            playerData = new Dictionary<ulong, PlayerData>();
            SaveData();
        }

        private void OnVendingShopOpened(VendingMachine machine, BasePlayer player)
        {
            if (!(machine is NPCVendingMachine) && !config.allowPlayerVendingMachines) return;

            if (!playerData.ContainsKey(player.userID))
            {
                InitPlayerData(player);
            }

            DoInterest(player);

            NextTick(() => CreateUi(player));
        }

        private void OnLootEntityEnd(BasePlayer player, VendingMachine machine)
        {
            DestroyGuiAll(player);
        }

        private static void DestroyGuiAll(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, CUI_BANK_NAME);
        }

        #endregion hooks

        #region bank CUI

        private const int CUI_MAIN_FONTSIZE = 10;
        private const string CUI_MAIN_FONT_COLOR = "0.7 0.7 0.7 1.0";
        private const string CUI_GREEN_BUTTON_COLOR = "0.415 0.5 0.258 0.4";
        private const string CUI_GREEN_BUTTON_FONT_COLOR = "0.607 0.705 0.431";
        private const string CUI_GRAY_BUTTON_COLOR = "0.75 0.75 0.75 0.3";
        private const string CUI_BUTTON_FONT_COLOR = "0.77 0.68 0.68 1";
        private const string CUI_BANK_NAME = "BankUI";
        private const string CUI_BANK_HEADER_NAME = "header";
        private const string CUI_BANK_CONTENT_NAME = "content";

        private const string ANCHOR_MIN = "0.5 0.0";
        private const string ANCHOR_MAX = "0.67 0.0";
        private const string OFFSET_MIN = "193 16";
        private const string OFFSET_MAX = "200 97";

        private void CreateUi(BasePlayer player)
        {
            if (!player.inventory.loot.IsLooting()) return;

            if (!playerPrefs.ContainsKey(player.userID))
            {
                InitPlayerPerference(player);
            }

            int amount = playerPrefs[player.userID].amount;

            double nextDecrement = amount / 1.5;
            double nextIncrement = amount * 1.5;

            CuiHelper.DestroyUi(player, CUI_BANK_NAME);

            var bankCui = new CuiElementContainer
            {
                {
                    new CuiPanel // main panel
                    {
                        Image = new CuiImageComponent { Color = "0 0 0 0" },
                        RectTransform =
                        {
                            AnchorMin = ANCHOR_MIN, AnchorMax = ANCHOR_MAX,
                            OffsetMin = OFFSET_MIN, OffsetMax = OFFSET_MAX
                        }
                    },
                    "Hud.Menu", CUI_BANK_NAME
                },
                {
                    new CuiPanel // header
                    {
                        Image = new CuiImageComponent { Color = "0.75 0.75 0.75 0.35" },
                        RectTransform = { AnchorMin = "0 0.775", AnchorMax = "1 1" }
                    },
                    CUI_BANK_NAME, CUI_BANK_HEADER_NAME
                },
                {
                    new CuiLabel // header label
                    {
                        RectTransform = { AnchorMin = "0.051 0", AnchorMax = "1 0.95" },
                        Text =
                        {
                            Text = lang.GetMessage(
                                LOC_ATM, this, player.UserIDString),
                            Align = TextAnchor.MiddleCenter, Color = "1.000 0.341 0.200", FontSize = 13
                        }
                    },
                    CUI_BANK_HEADER_NAME
                },
                {
                    new CuiPanel // content panel
                    {
                        Image = new CuiImageComponent { Color = "0.65 0.65 0.65 0.25" },
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.74" }
                    },
                    CUI_BANK_NAME, CUI_BANK_CONTENT_NAME
                },
                {
                    new CuiLabel // balance label
                    {
                        RectTransform = { AnchorMin = "0.02 0.7", AnchorMax = "0.98 1" },
                        Text =
                        {
                            Text = string.Format(lang.GetMessage(LOC_BALANCE, this,
                                player.UserIDString), playerData[player.userID].scrap),
                            Align = TextAnchor.MiddleCenter,
                            Color = CUI_MAIN_FONT_COLOR,
                            FontSize = CUI_MAIN_FONTSIZE
                        }
                    },
                    CUI_BANK_CONTENT_NAME
                },
                {
                    new CuiButton // deposit button
                    {
                        RectTransform = { AnchorMin = "0.28 0.4", AnchorMax = "0.48 0.7" },
                        Button = { Command = "sc.deposit " + amount, Color = CUI_GREEN_BUTTON_COLOR },
                        Text =
                        {
                            Align = TextAnchor.MiddleCenter,
                            Text = lang.GetMessage(LOC_DEPOSIT, this, player.UserIDString),
                            Color = CUI_GREEN_BUTTON_FONT_COLOR,
                            FontSize = 11
                        }
                    },
                    CUI_BANK_CONTENT_NAME
                },
                {
                    new CuiButton // withdraw button
                    {
                        RectTransform = { AnchorMin = "0.49 0.4", AnchorMax = "0.73 0.7" },
                        Button = { Command = "sc.withdraw " + amount, Color = CUI_GRAY_BUTTON_COLOR },
                        Text =
                        {
                            Align = TextAnchor.MiddleCenter, Text = lang.GetMessage(
                                LOC_WITHDRAW, this, player.UserIDString),
                            Color = CUI_MAIN_FONT_COLOR, FontSize = 11
                        }
                    },
                    CUI_BANK_CONTENT_NAME
                },
                {
                    new CuiButton // decrement button
                    {
                        RectTransform = { AnchorMin = "0.35 0.05", AnchorMax = "0.40 0.35" },
                        Button = { Command = "sc.setamount " + nextDecrement, Color = CUI_GRAY_BUTTON_COLOR },
                        Text =
                        {
                            Align = TextAnchor.MiddleCenter,
                            Text = "<",
                            Color = CUI_BUTTON_FONT_COLOR,
                            FontSize = CUI_MAIN_FONTSIZE
                        }
                    },
                    CUI_BANK_CONTENT_NAME
                },
                {
                    new CuiLabel // amount label
                    {
                        RectTransform = { AnchorMin = "0.42 0.05", AnchorMax = "0.53 0.35" },
                        Text =
                        {
                            Align = TextAnchor.MiddleCenter,
                            Text = amount.ToString(),
                            Color = CUI_MAIN_FONT_COLOR,
                            FontSize = CUI_MAIN_FONTSIZE
                        }
                    },
                    CUI_BANK_CONTENT_NAME
                },
                {
                    new CuiButton // increment button
                    {
                        RectTransform = { AnchorMin = "0.57 0.05", AnchorMax = "0.62 0.35" },
                        Button = { Command = "sc.setamount " + nextIncrement, Color = CUI_GRAY_BUTTON_COLOR },
                        Text =
                        {
                            Align = TextAnchor.MiddleCenter,
                            Text = ">",
                            Color = CUI_BUTTON_FONT_COLOR,
                            FontSize = CUI_MAIN_FONTSIZE
                        }
                    },
                    CUI_BANK_CONTENT_NAME
                },
                {
                    new CuiLabel // amount text label
                    {
                        RectTransform = { AnchorMin = "0.65 0.05", AnchorMax = "0.75 0.35" },
                        Text =
                        {
                            Align = TextAnchor.MiddleLeft,
                            Text = lang.GetMessage(LOC_TOTAL, this, player.UserIDString),
                            Color = CUI_MAIN_FONT_COLOR,
                            FontSize = CUI_MAIN_FONTSIZE
                        }
                    },
                    CUI_BANK_CONTENT_NAME
                }
            };

            CuiHelper.AddUi(player, bankCui);
        }

        [ConsoleCommand("sc.setamount")]
        private void CmdSetAmount(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || arg.Args.Length != 1 ||
                !(player.inventory.loot.entitySource is VendingMachine)) return;

            double amount;
            if (!double.TryParse(arg.Args[0], out amount)) return;

            amount = Math.Round(amount / 10) * 10;

            if (amount < 10) amount = 10;
            else if (amount > 1000) amount = 1000;

            if (arg.Args.Length != 1) return;

            if (!playerPrefs.ContainsKey(player.userID))
            {
                InitPlayerPerference(player);
            }

            playerPrefs[player.userID].amount = (short)amount;
            CreateUi(player);

            // sfx on ui interaction
            TriggerIncrementEffect(player);
        }

        private void TriggerIncrementEffect(BasePlayer player)
        {
            EffectNetwork.Send(new Effect(increment, player.transform.position, Vector3.zero), player.net.connection);
        }


        [ConsoleCommand("sc.deposit")]
        private void CmdDeposit(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || arg.Args.Length != 1 ||
                !(player.inventory.loot.entitySource is VendingMachine)) return;

            int amount;
            if (!int.TryParse(arg.Args[0], out amount)) return;

            if (player.inventory.GetAmount(-932201673) < amount)
            {
                amount = player.inventory.GetAmount(-932201673);
            }

            if (amount == 0) return;

            if (!playerData.ContainsKey(player.userID))
            {
                InitPlayerData(player);
            }

            playerData[player.userID].scrap += amount;
            player.inventory.Take(null, -932201673, amount);
            CreateUi(player);

            // sfx on ui deposit interaction
            TriggerDepositEffect(player);
        }

        private void TriggerDepositEffect(BasePlayer player)
        {
            EffectNetwork.Send(new Effect(deposit, player.transform.position, Vector3.zero), player.net.connection);
        }

        [ConsoleCommand("sc.withdraw")]
        private void CmdWithdraw(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || arg.Args.Length != 1 ||
                !(player.inventory.loot.entitySource is VendingMachine)) return;

            int amount;
            if (!int.TryParse(arg.Args[0], out amount)) return;

            if (!playerData.ContainsKey(player.userID))
            {
                InitPlayerData(player);
            }

            int balance = playerData[player.userID].scrap;
            int tax = (int)Math.Round(amount * config.feesFraction);

            if (balance < amount + tax)
            {
                SendReply(player, string.Format(
                    lang.GetMessage(LOC_INSUFFICIENT_FUNDS, this, player.UserIDString)));
                TriggerInsufficientFundsEffect(player);
                return;
            }

            playerData[player.userID].scrap -= amount + tax;
            CreateUi(player);
            Item item = ItemManager.CreateByItemID(-932201673, amount);
            if (item == null) return;
            player.inventory.GiveItem(item);
            SendReply(player, string.Format(
                lang.GetMessage(LOC_PAID_BROKERAGE, this, player.UserIDString), tax));

            // sfx on ui withdraw interaction
            TriggerWithdrawEffect(player);
        }


        private void TriggerWithdrawEffect(BasePlayer player)
        {
            EffectNetwork.Send(new Effect(withdraw, player.transform.position, Vector3.zero), player.net.connection);
        }

        private void TriggerInsufficientFundsEffect(BasePlayer player)
        {
            EffectNetwork.Send(new Effect(insufficientfunds, player.transform.position, Vector3.zero),
                player.net.connection);
        }


        // Command to announce at will.
        [Command("scrapannounce")]
        private void CmdScrapAnnounce(IPlayer player, string cmd, string[] args)
        {
            if (player.IsAdmin)
            {
                AnnounceTopBalances();
            }
            else
            {
                player.Reply("You don't have permission to use this command.");
            }
        }

        #endregion bank CUI

        #region Announce Top Balances

        private void OnServerInitialized()
        {
            timer.Every(config.leaderboardAnnounceIntervalSeconds, AnnounceTopBalances);
        }

        private void AnnounceTopBalances()
        {
            var topBalances = playerData.OrderByDescending(kv => kv.Value.scrap)
                .Take(config.leaderboardAnnouncePlayerCount)
                .ToList();

            if (topBalances.Count == 0 || topBalances.All(kv => kv.Value.scrap == 0))
            {
                return;
            }

            string message = "<size=16><color=#FF5733>Scrap Leaderboard</color></size>\n";
            for (int i = 0; i < topBalances.Count; i++)
            {
                var kv = topBalances[i];
                var playerData = kv.Value;
                var playerID = kv.Key;
                var playerName = covalence.Players.FindPlayerById(playerID.ToString())?.Name ?? playerID.ToString();
                if (playerData.scrap > 0)
                {
                    message +=
                        $"<color=#ff3375>{i + 1}.</color> <color=#ffbd33>{playerName}</color> with <color=#ff5733>{playerData.scrap}</color> Scrap.\n";
                }
            }

            PrintToChat(message);
            foreach (var player in BasePlayer.activePlayerList)
            {
                EffectNetwork.Send(new Effect(announce, player.transform.position, Vector3.zero));
            }


            var topBalancesStringKeys = topBalances
                .Select(kv => new KeyValuePair<string, PlayerData>(kv.Key.ToString(), kv.Value)).ToList();


            SendDiscordTopBalances(topBalancesStringKeys);
        }

        #endregion Announce Top Balances

        #region Discord

        private void SendDiscordTopBalances(List<KeyValuePair<string, PlayerData>> topBalances)
        {
            string webhookUrl = Convert.ToString(config.discordwebhookURL);

            if (topBalances == null || topBalances.Count == 0 || string.IsNullOrEmpty(webhookUrl) ||
                !webhookUrl.Contains("/api/webhooks"))
            {
                Puts("Failed to send Discord Message. Webhook URL or content is invalid.");
                return;
            }


            var discordMessage = new
            {
                embeds = new[]
                {
                    new
                    {
                        title = "Scrap Leaderboard",
                        color = 15158332,
                        fields = topBalances.Select((kv, index) => new
                        {
                            name =
                                $"#{index + 1} {covalence.Players.FindPlayerById(kv.Key.ToString())?.Name ?? kv.Key.ToString()}",
                            value = $"Balance: {kv.Value.scrap} Scrap",
                            inline = false
                        }).ToArray()
                    }
                }
            };

            // Convert the message to JSON
            string content = JsonConvert.SerializeObject(discordMessage);

            if (string.IsNullOrEmpty(content))
            {
                return;
            }


            // Send the message to Discord
            webrequest.Enqueue(webhookUrl, content, (code, response) =>
            {
                if (code != 204)
                {
                    Puts($"Discord responded with code {code}. Response: {response}");
                }
            }, this, RequestMethod.POST, new Dictionary<string, string> { ["Content-Type"] = "application/json" });
        }


        private const string DiscordJson = @"{
    ""embeds"":[{
        ""fields"": [
            {
                ""name"": ""${message.field.name}"",
                ""value"": ""${message}""
            }
        ]
    }]
}";

        #endregion

        #region API

        private object SetBalance(ulong userId, int balance)
        {
            if (!playerData.ContainsKey(userId) && !TryInitPlayer(userId)) return null;

            playerData[userId].scrap = balance;
            return true;
        }

        private object GetBalance(ulong userId)
        {
            if (!playerData.ContainsKey(userId) && !TryInitPlayer(userId)) return null;
            return playerData[userId].scrap;
        }

        private bool TryInitPlayer(ulong userId)
        {
            BasePlayer player = BasePlayer.FindByID(userId);
            if (player == null) return false;
            InitPlayerData(player);
            return true;
        }

        #endregion API
    }
}
