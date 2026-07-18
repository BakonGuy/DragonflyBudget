# Dragonfly 🜲

A dead-simple **native Windows desktop** budget watcher/planner (C# / WPF).
No accounts, no network, no sync — everything is typed in by you and saved
automatically on your machine.

## Requirements

- Windows 10 or 11
- The [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (needed to
  build the app the first time)

## Build it

1. Download or clone this repository.
2. Double-click **`Dragonfly.bat`**.

This batch file builds and runs the app; Once built, the program lives at
`bin\Release\net9.0-windows\Dragonfly.exe`

Prefer the command line? From the repo folder:

```
dotnet run -c Release
```

## Where your data lives

`%APPDATA%\Dragonfly\dragonfly-data.json` — one small JSON file.
Every save also keeps a rolling daily backup (`backup-YYYY-MM-DD.json`, last 14
days) in the same folder. Copy that folder to back up everything.

## The five screens

- **Dashboard** — bank + cash totals, what needs attention in the next 7 days,
  pending money moves, and a projected end-of-month figure. Edit account
  balances and cash right here (just type and press Enter or click away).
- **Bills** — recurring or one-off bills with a due day. Mark each month Paid /
  Partial, or override an amount for a single month. Use ‹ › to move between
  months — every month keeps its own paid history.
- **Pending** — expected deposits/withdrawals where the date or amount may be
  fuzzy ("bonus, probably next two weeks"). Mark them Cleared when they happen.
- **Debts to Pay** — a running list of what you owe (people, medical, IOUs) with
  progress bars. Finished ones move to a "Paid off" list you can restore.
- **Repayment** — interest-bearing debts (cards, car loans). Shows payoff time
  and total interest at your current payment, plus the monthly payment needed to
  hit a goal date and how much interest that saves.

## You never reset anything

To stop a recurring bill, give it an **Ends** month instead of deleting it —
history stays intact. Switching months never loses data; each month simply
remembers its own paid/cleared state.

## Building from source

From the repo root:

```
dotnet build -c Release
```

Business logic lives in `Models\` and `Services\` (plain C#, UI-agnostic); the
WPF UI is in `Views\` with the theme in `Theme.xaml`.
