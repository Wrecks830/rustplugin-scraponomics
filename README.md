Adds ATM UI with simple, intuitive functionality to vending machines and bandit vendors where players may deposit or withdraw their precious scrap for a small fee.

Players are rewarded interest daily.

* UI adjusts properly to any resolution
* Option to restrict ATM access from player placed vending machines
* Option to automatically reset data on map wipe
* Set fees, starting balance and interest rate

## Usage

The UI will appear when using Vending Machine or Bandit Vendor:
![](https://i.imgur.com/HtCAotG.png)

An interest rate of 0.10 or 10% means 50 scrap in the bank will reward 5 scrap after 1 day.

## Configuration

```json
{
  "feesFraction": 0.05,
  "startingBalance": 50,
  "allowPlayerVendingMachines": false,
  "resetOnMapWipe": true,
  "interestRate": 0.1
}
```

## Localization

```json
{
  "PaidBrokerage": "Paid the brokerage fee of {0} scrap.",
  "Deposit": "Deposit",
  "Withdraw": "Withdraw",
  "Balance": "Balance: {0} scrap",
  "Amount": "amount",
  "ATM": "ATM",
  "RewardInterst": "You've earned {0} scrap in interest."
}
```

## Developer API

```csharp
private object SetBalance(ulong userId, int balance);  // returns true if successful, else null
private object GetBalance(ulong userId);  // returns int if successful, else null
```

## Credits

CUI inspired by **Skipcast**'s Furnace Splitter plugin