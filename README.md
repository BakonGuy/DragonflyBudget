# Dragonfly 🜲

Finance tracking should be boring, private, and free. Dragonfly is a native Windows
desktop budget thing I made because every other option wanted my data in the cloud or
money in their pocket. No accounts, no network, no subscriptions — just an `.exe` that
saves a JSON file on your machine and stays the hell out of your way.

It's a dead-simple **budget watcher / planner** written in C# / WPF. You type stuff in,
it remembers it. That's the whole deal.

## Download it

Grab the latest installer from the
**[Releases page](https://github.com/BakonGuy/DragonflyBudget/releases/latest)**.
Run the installer, launch Dragonfly, and you're done — no SDK, no command line,
nothing else to install.

> If you'd rather build from source, see [Building from source](#building-from-source) below.

## The screens

**Dashboard** — bank + cash totals, what needs attention in the next 7 days,
pending money moves, and a projected end-of-month figure. Edit account
balances and cash on hand right here (just type and press Enter or click away).

**Accounts** — bank accounts and credit cards side by side. Set APR, credit
limit, minimum payment terms, and a goal month for each card. Click any account
for a balance-over-time graph (manual edits vs automatic bill payments).

**Bills** — recurring or one-off bills with a due day. Link a bill to a bank
or card so its balance updates automatically when you mark it paid. Credit card
bills work both ways: the bill draws from a bank and pays down the card. Autopay
bills are auto-marked paid on their due date (you can override the rare failure).
Use the ‹ › arrows to flip through months — every month keeps its own history.

**Pending** — expected deposits and withdrawals where the date or amount may be
fuzzy ("bonus, probably next two weeks", "tax refund sometime in April"). Mark
them Cleared when they actually happen.

**Budgets** — flexible-spend categories (groceries, eating out, etc.) with a
monthly cap. Log what you spend each month and see a progress bar against your
cap.

**Debts to Pay** — simple IOU tracking (people, medical bills, whatever) with
progress bars. Archived ones move to a "Paid off" section you can restore.

**Repayment** — credit cards and loans with interest. Shows payoff time and
total interest at your current payment, the minimum payment, and the monthly
payment needed to hit a goal date (plus how much interest that saves).

**Payoff Plan** — picks which debt to throw extra money at each month. Avalanche
(saves the most interest) or snowball (smallest balance first). Enter how much
extra you can put toward debt and it tells you, in plain words, what to pay and
what that gets you.

## You never reset anything

To stop a recurring bill, give it an **Ends** month instead of deleting it —
history stays intact. Switching months never loses data; each month simply
remembers its own paid/cleared state.

---

## Building from source

### Requirements

- Windows 10 or 11
- The [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

### Build & run

1. Download or clone this repository.
2. Double-click **`Dragonfly.bat`** — it builds and launches the app in one step.

Or from the command line:

```
dotnet run -c Release
```

The compiled binary ends up at `bin\Release\net10.0-windows\Dragonfly.exe`.

### Where your data lives

```
%LOCALAPPDATA%\OvertorqueCreations\Dragonfly\Saved\dragonfly-data.json
```

One small JSON file. Every save also keeps a rolling daily backup
(`backup-YYYY-MM-DD.json`, last 14 days) under `Saved\Backups\`.
Copy the whole `Saved` folder to back up everything.

(The app installs to `%LOCALAPPDATA%\OvertorqueCreations\Dragonfly` — data lives
in `Saved\` to stay separate from the program files. Data from Dragonfly 1.0 is
moved into `Saved\` automatically on first launch.)

### Project layout

| Folder | What's in there |
|---|---|
| `Models\` | Data classes (bills, accounts, loans, etc.) |
| `Services\` | Budget logic, persistence, update checking |
| `Views\` | WPF windows, controls, and `Theme.xaml` |
