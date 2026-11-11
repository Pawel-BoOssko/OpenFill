# OpenFill Telegram mobile bridge (OpenFill 1.62)

Project owner: OpenFill. Integration documented by GPT-6, 2026-11-11 22:03:17 CET.

## Components
- .NET MobileCardGateway and MobileCardPipeServer in src/OpenFill.Core/Hosting/: expose human interaction cards over a local Windows named pipe.
- telegram_bridge.py: poll Telegram Bot API, persist routing in SQLite WAL, deliver cards, apply replies over validated IPC.
- windows_dpapi.py: protect bot credentials and pending answers within the same Windows account.
- bridge_supervisor.py: restart worker after failure, read DPAPI credentials, log worker starts.
- configure_bot.py: interactive one-time bot setup and pairing-code generation.
- Install-Bridge.ps1: create scheduled task OpenFillTelegramBridge at Windows logon.
- tests/test_telegram_bridge.py and tests/TelegramGateway/: unit and gateway tests.

## Build and test (Windows)
Run from repository root with Python 3.12 and .NET SDK 10:

    python -m unittest discover -s tools/telegram/tests -p test_telegram_bridge.py -v
    dotnet build OpenFill__v0.1__2026-10-04_1911.sln -c Release
    dotnet run --project tests/TelegramGateway/GatewayTests.csproj -c Release

Configure bot once as the intended Windows user:

    python tools/telegram/configure_bot.py

Install bridge at logon (after resolving pythonw.exe):

    powershell -ExecutionPolicy Bypass -File tools/telegram/Install-Bridge.ps1

The tested bot on PBOX1 is @OpenFillTelefonBot. Do not commit runtime credentials, SQLite state, pairing codes or bot tokens.

## Routing
- Telegram Reply refers to the exact bot message and uniquely identifies the card.
- A plain non-command message is assigned only when exactly one delivered, open, unexpired ask card exists in the paired private chat.
- When two cards are open, the user must use Reply to select a card.
- Confirm actions require inline-button callbacks, never a plain text message.
- Telegram updates are deduplicated; IPC validates instance, task and card IDs.
- Expired cards and stale instances cannot accept answers.
- Bridge uses long-polling, without inbound TCP listener. MainForm local sound is independent.

## Current implementation
Version 1.62 sources compile; Python unit tests 16/16 and C# gateway tests 15/15 pass on PBOX1.
The live PBOX1 bridge includes the plain-message routing fix.
OpenFill.exe installed earlier was built from 1.60 sources; changing this repository version does not by itself install a 1.61 production binary.
Speaker playback, confirm (disabled in current OpenFill settings), code prompts and simultaneous-card replies need final physical acceptance.
The source branch feature/openfill-telegram-20261111 is intentionally separate from main.

## Telegram delivery receipts (1.62)

Each text message received from the paired user gets a contextual Telegram reply:

- Accepted response: the message was routed to and accepted by the OpenFill task via IPC.
- Not routed: the bot received the message but no unique active card could be resolved.
- Rejected: OpenFill IPC rejected the response; no success claim is made.

Receipts use reply_parameters to link the acknowledgment to the source Telegram message.
They are persisted in the receipts SQLite table, deduplicated by Telegram update ID, and
are not blindly retried when delivery outcome is unknown. An IPC outage delays any
positive acknowledgment until OpenFill actually accepts the answer.

The bot token and the user's answer text are never written to receipt contents.
