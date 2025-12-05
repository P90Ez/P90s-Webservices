# WatchlistParser

The goal of this parser is to convert the watchlist exported from a certain website (which shall not be named) to the format required to display the data on my custom watchlist.

This is done by requesting the english and japanese titles, as well as the url of a preview image from MyAnimeList.

## Build

> Requires .Net 8.0+ SDK

`dotnet build` in the current directory, or `make build`.

## Usage

> Requires .Net 8.0 Runtime

`WatchlistParser [Input] [Output]`

Both arguments are optional. If no input is specified, `WatchlistRaw.json` is the default value. If no output is specified, `WatchlistProcessed.json` is the default.

`make run` both builds the tool and run it. The paths are specified to take `./WatchlistRaw.json` as input and output `../AnimeWatchlist.json`, which can directly be accesses by my custom watchlist.

## Todo

- Add an update mechanism, to only add shows that are not present in the output file
- Improve performance (async etc.)
- Improve error handling and add retries on API disconnect / timeout