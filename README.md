Adds ATM UI with simple, intuitive functionality to vending machines and bandit vendors where players may deposit or withdraw their precious scrap for a small fee.

* UI adjusts properly to any resolution
* Option to restrict ATM access from player placed vending machines
* Option to automatically reset data on map wipe
* Set fees and starting balance


## Usage
The UI will appear when using Vending Machine or Bandit Vendor:
![](https://i.imgur.com/iQdHX7A.png)


## Configuration
```json
{
  "feesFraction": 0.05,
  "startingBalance": 50,
  "allowPlayerVendingMachines": false,
  "resetOnMapWipe": true
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
  "ATM": "ATM"
}
```


## Developer API
```csharp
private object SetBalance(ulong userId, int balance)  // returns true if successful, else null

private object GetBalance(ulong userId)  // returns int if successful, else null
```