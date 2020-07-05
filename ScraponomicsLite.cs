
using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{

    [Info("ScraponomicsLite", "haggbart", "0.3.0")]
    [Description("Scraponomics Lite")]
    internal class ScraponomicsLite : RustPlugin
    {
        
        #region localization
        
        private const string LOC_PAID_BROKERAGE = "PaidBrokerage";
        private const string LOC_DEPOSIT = "Deposit";
        private const string LOC_WITHDRAW = "Withdraw";
        private const string LOC_AMOUNT = "Amount";
        private const string LOC_BALANCE = "Balance";
        private const string LOC_ATM = "ATM";
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [LOC_PAID_BROKERAGE] = "Paid the brokerage fee of {0} scrap.",
                [LOC_DEPOSIT] = "Deposit",
                [LOC_WITHDRAW] = "Withdraw",
                [LOC_BALANCE] = "Balance: {0} scrap",
                [LOC_AMOUNT] = "amount",
                [LOC_ATM] = "ATM"
            }, this);
        }

        #endregion localization


        #region data
        private void SaveData() =>
            Interface.Oxide.DataFileSystem.WriteObject(Title, _playerBalances);
        
        private void ReadData() =>
            _playerBalances = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerData>>(Title);

        private static Dictionary<ulong, PlayerData> _playerBalances;
        private static readonly Dictionary<ulong, PlayerPreference> _playerPrefs = new Dictionary<ulong, PlayerPreference>();

        private class PlayerData
        {
            public int scrap { get; set; }
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
        }
        
        protected override void LoadDefaultConfig() => Config.WriteObject(GetDefaultConfig(), true);
        private new void SaveConfig() => Config.WriteObject(config, true);
        
        private static PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {
                feesFraction = 0.05f,
                startingBalance = 50,
                allowPlayerVendingMachines = false,
                resetOnMapWipe = true
            };
        }
        
        #endregion config
        
        #region init

        private void Init()
        {

            config = Config.ReadObject<PluginConfig>();

            SaveConfig();
            
            ReadData();
        }

        private void InitPlayerData(BasePlayer player)
        {
            var playerbalances = new PlayerData
            {
                scrap = config.startingBalance
            };
            _playerBalances.Add(player.userID, playerbalances);
        }
        
        private static void InitPlayerPerference(BasePlayer player)
        {
            var playerPreference = new PlayerPreference
            {
                amount = 100
            };
            _playerPrefs.Add(player.userID, playerPreference);
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
        
        #region hooks
        
        private void OnServerSave() => SaveData();
        
        private void OnNewSave(string filename)
        {
            if (!config.resetOnMapWipe) return;
            _playerBalances = new Dictionary<ulong, PlayerData>();
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                DestroyGuiAll(player);
            }
            SaveData();
        }

        private void OnOpenVendingShop(VendingMachine machine, BasePlayer player)
        {
            if (!(machine is NPCVendingMachine) && !config.allowPlayerVendingMachines) return;
            
            if (!_playerBalances.ContainsKey(player.userID))
            {
                InitPlayerData(player);
            }
            
            if (!_playerPrefs.ContainsKey(player.userID))
            {
                InitPlayerPerference(player);
            }
            
            NextTick(() => CreateUi(player)); 
        }
        
        private void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity)
        {
            if (entity is VendingMachine)
            {
                DestroyGuiAll(player);
            }
        }

        private static void DestroyGuiAll(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "BankUI");
        }
        
        #endregion hooks

        #region bank CUI
        
        private const string CONTENT_COLOR = "0.7 0.7 0.7 1.0";
        private const int CONTENT_SIZE = 10;
        private const string TOGGLE_BUTTON_COLOR = "0.415 0.5 0.258 0.4";
        private const string TOGGLE_BUTTON_TEXT_COLOR = "0.607 0.705 0.431";
        private const string BUTTON_COLOR = "0.75 0.75 0.75 0.3";
        private const string BUTTON_TEXT_COLOR = "0.77 0.68 0.68 1";
        private const string ANCHOR_MIN = "0.5 0.0";
        private const string ANCHOR_MAX = "0.67 0.0";
        private const string OFFSET_MIN = "193 16";
        private const string OFFSET_MAX = "200 97";

        private void CreateUi(BasePlayer player)
        {
            if (!player.inventory.loot.IsLooting()) return;

            int amount = _playerPrefs[player.userID].amount;
            

            double nextDecrement = amount / 1.5;
            double nextIncrement = amount * 1.5;
            
            CuiHelper.DestroyUi(player, "BankUI");
            
            var cuiElementContainer = new CuiElementContainer
            {
                {
                    new CuiPanel
                    {
                        Image = new CuiImageComponent {Color = "0 0 0 0"},
                        RectTransform =
                        {
                            AnchorMin = ANCHOR_MIN, AnchorMax = ANCHOR_MAX,
                            OffsetMin = OFFSET_MIN, OffsetMax = OFFSET_MAX
                        }
                    },
                    "Hud.Menu", "BankUI"
                },
                {
                    new CuiPanel
                    {
                        Image = new CuiImageComponent {Color = "0.75 0.75 0.75 0.2"},
                        RectTransform = {AnchorMin = "0 0.775", AnchorMax = "1 1"}
                    },
                    "BankUI", "header"
                },
                {
                    new CuiLabel
                    {
                        RectTransform = {AnchorMin = "0.051 0", AnchorMax = "1 0.95"},
                        Text = {Text = lang.GetMessage(
                            LOC_ATM, this, player.UserIDString), 
                            Align = TextAnchor.MiddleLeft, Color = "0.77 0.7 0.7 1", FontSize = 13}
                    },
                    "header"
                },
                {
                    new CuiPanel
                    {
                        Image = new CuiImageComponent {Color = "0.65 0.65 0.65 0.15"},
                        RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0.74"}
                    },
                    "BankUI", "content"
                },
                {
                    new CuiLabel
                    {
                        RectTransform = {AnchorMin = "0.02 0.7", AnchorMax = "0.98 1"},
                        Text =
                        {
                            // Text = "Balance: " + _playerBalances[player.userID].scrap + " scrap",
                            Text = string.Format(lang.GetMessage(LOC_BALANCE, this, 
                                player.UserIDString), _playerBalances[player.userID].scrap),
                            Align = TextAnchor.MiddleLeft,
                            Color = CONTENT_COLOR,
                            FontSize = CONTENT_SIZE
                        }
                    },
                    "content"
                },
                {
                    new CuiButton
                    {
                        RectTransform = {AnchorMin = "0.02 0.4", AnchorMax = "0.25 0.7"},
                        Button = {Command = "deposit " + amount, Color = TOGGLE_BUTTON_COLOR},
                        Text =
                        {
                            Align = TextAnchor.MiddleCenter,
                            Text = lang.GetMessage(LOC_DEPOSIT, this, player.UserIDString),
                            Color = TOGGLE_BUTTON_TEXT_COLOR,
                            FontSize = 11
                        }
                    },
                    "content"
                },
                {
                    new CuiButton
                    {
                        RectTransform = {AnchorMin = "0.27 0.4", AnchorMax = "0.52 0.7"},
                        Button = {Command = "withdraw " + amount, Color = BUTTON_COLOR},
                        Text = {Align = TextAnchor.MiddleCenter, Text = lang.GetMessage(
                            LOC_WITHDRAW, this, player.UserIDString), Color = CONTENT_COLOR, FontSize = 11}
                    },
                    "content"
                },
                {
                    new CuiButton
                    {
                        RectTransform = {AnchorMin = "0.02 0.05", AnchorMax = "0.07 0.35"},
                        Button = {Command = "setamount " + nextDecrement, Color = BUTTON_COLOR},
                        Text =
                        {
                            Align = TextAnchor.MiddleCenter,
                            Text = "<",
                            Color = BUTTON_TEXT_COLOR,
                            FontSize = CONTENT_SIZE
                        }
                    },
                    "content"
                },
                {
                    new CuiLabel
                    {
                        RectTransform = {AnchorMin = "0.08 0.05", AnchorMax = "0.19 0.35"},
                        Text =
                        {
                            Align = TextAnchor.MiddleCenter,
                            Text = amount.ToString(),
                            Color = CONTENT_COLOR,
                            FontSize = CONTENT_SIZE
                        }
                    },
                    "content"
                },
                {
                    new CuiButton
                    {
                        RectTransform = {AnchorMin = "0.19 0.05", AnchorMax = "0.25 0.35"},
                        Button = {Command = "setamount " + nextIncrement, Color = BUTTON_COLOR},
                        Text =
                        {
                            Align = TextAnchor.MiddleCenter,
                            Text = ">",
                            Color = BUTTON_TEXT_COLOR,
                            FontSize = CONTENT_SIZE
                        }
                    },
                    "content"
                },
                {
                    new CuiLabel
                    {
                        RectTransform = {AnchorMin = "0.27 0.05", AnchorMax = "1 0.35"},
                        Text =
                        {
                            Align = TextAnchor.MiddleLeft,
                            Text = lang.GetMessage(LOC_AMOUNT, this, player.UserIDString),
                            Color = CONTENT_COLOR,
                            FontSize = CONTENT_SIZE
                        }
                    },
                    "content"
                }
            };

            CuiHelper.AddUi(player, cuiElementContainer);
        }

        [ConsoleCommand("setamount")]
        private void CmdSetAmount(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || arg.Args.Length != 1 || 
                !(player.inventory.loot.entitySource is VendingMachine)) return;
            
            double amount;
            if (!double.TryParse(arg.Args[0], out amount)) return;
            
            amount = Math.Round(Convert.ToDouble(arg.Args[0]) / 10) * 10;
            
            if (amount < 10) amount = 10;
            else if (amount > 1000) amount = 1000;

            if (arg.Args.Length != 1) return;
            _playerPrefs[player.userID].amount = (short) amount;
            CreateUi(player);
        }

        [ConsoleCommand("deposit")]
        private void Cmd_deposit(ConsoleSystem.Arg arg)
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
            _playerBalances[player.userID].scrap += amount;
            player.inventory.Take(null, -932201673, amount);
            CreateUi(player);
        }

        [ConsoleCommand("withdraw")]
        private void Cmd_withdraw(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || arg.Args.Length != 1 || 
                !(player.inventory.loot.entitySource is VendingMachine)) return;

            int amount;
            if (!int.TryParse(arg.Args[0], out amount)) return;

            int balance = _playerBalances[player.userID].scrap;
            if (balance < amount) amount = balance;
            var tax = (int)Math.Round(amount * config.feesFraction);

            if (tax < 1) tax = 1;
            if (amount < 2) return;
            _playerBalances[player.userID].scrap -= amount + tax;
            CreateUi(player);
            Item item = ItemManager.CreateByItemID(-932201673, amount);
            player.inventory.GiveItem(item);
            SendReply(player, string.Format(
                lang.GetMessage(LOC_PAID_BROKERAGE, this, player.UserIDString), tax));
        }
        
        #endregion bank CUI

        #region API

        private object SetBalance(ulong userId, int balance)
        {
            if (!_playerBalances.ContainsKey(userId) && !TryInitPlayer(userId)) return null;

            _playerBalances[userId].scrap = balance;
            return true;
        }

        private object GetBalance(ulong userId)
        {
            if (!_playerBalances.ContainsKey(userId) && !TryInitPlayer(userId)) return null;
            return _playerBalances[userId].scrap;
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
