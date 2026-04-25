# WatchlistParser

The goal of this parser is to pull the watchlist from My Anime List, strip it down and sort it.

This parser originally used an offline watchlisted (exported from a streaming site) and requested information for each show from My Anime List. The streaming site does not exist anymore, therefore i switched to MAL.

## Build

> Requires .Net 8.0+ SDK

`dotnet build` in the current directory, or `make build`.

## Usage

> Requires .Net 8.0 Runtime

`WatchlistParser [OutputPath] [CredentialsPath]`

Both arguments are optional. If no output is specified, `WatchlistProcessed.json` is the default. If no credentials file is specified, `Credentials.json` is the default.

`make run` both builds the tool and runs it. The paths are specified to take `../AnimeWatchlist.json` as output and `Credentials.json` as credential file.