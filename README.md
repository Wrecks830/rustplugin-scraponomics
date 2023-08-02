Adds ATM UI with simple, intuitive functionality to vending machines and bandit vendors where players may deposit or withdraw their precious scrap for a small fee.

Players are rewarded interest daily.

* UI adjusts properly to any resolution
* Option to restrict ATM access from player placed vending machines
* Option to automatically reset data on map wipe
* Set fees, starting balance and interest rate
* Scrap Leaderboard announces 5 players at an interval of 12 minutes.
* SFX on UI Usage.
* Negative Balances are Prevented and warn players they cannot withdraw and why.

## Usage

The UI will appear when using Vending Machine or Bandit Vendor:


![](https://media.discordapp.net/attachments/1131387423838961747/1136414047860949072/Screenshot_2023-08-02_163104.png)

This "Announcement" will show up in chat and announce the Top 5 Balances.


![](https://cdn.discordapp.com/attachments/1136078788279677009/1136081650028138616/image.png)

An interest rate of 0.10 or 10% means 50 scrap in the bank will reward 5 scrap after 1 day.
## Commands

/ScrapAnnounce - Will announce leaderboard at will.

## Configuration


```json
{
  "allowPlayerVendingMachines": false,
  "feesFraction": 0.05,
  "interestRate": 0.1,
  "leaderboardAnnounceIntervalSeconds": 600,
  "leaderboardAnnouncePlayerCount": 6,
  "resetOnMapWipe": false,
  "startingBalance": 50
}
```

## Localization

```json
{
                [LOC_PAID_BROKERAGE] = "<color=#FF5733>[Scraponomics]</color> You paid the Brokerage fee of {0} Scrap.",
                [LOC_DEPOSIT] = "Deposit",
                [LOC_WITHDRAW] = "Withdraw",
                [LOC_BALANCE] = "Balance : <color=#FF5733>{0}</color> Scrap",
                [LOC_TOTAL] = "Total",
                [LOC_ATM] = "SCRAP ATM",
                [LOC_REWARD_INTEREST] = "<color=#FF5733>[Scraponomics]</color> You've earned {0} Scrap in interest.",
                [LOC_INSUFFICIENT_FUNDS] = "<color=#FF5733>[Scraponomics]</color> Insufficient funds to cover Fees and Withdraw amount."
}
```

## Developer API

```csharp
private object SetBalance(ulong userId, int balance);  // returns true if successful, else null
private object GetBalance(ulong userId);  // returns int if successful, else null
```

## Credits

CUI inspired by **Skipcast**'s Furnace Splitter plugin

Thanks to https://github.com/haggbart For giving me permission to add the leaderboard feature! - Wrecks
