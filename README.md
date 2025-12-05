# P90s Redirects

This project started out as a replacement for a hosted YOURLS instance. I did not want to spend more money on hosting (for something so trivial), so I tried to build a static redirect system usable with Github pages or Cloudflare pages.

I had some more ideas and therefore a custom anime watchlist was added. Maybe there will be more in the future.

## Redirects

The few lines of code in `index.html` provide a framework to redirect from `YOURDOMAIN.com/example` to `example.com`. The redirects are specified in `redirects.json`.

I ended up using Cloudflare pages, as I already use Cloudflare for managing my domain(s).

When using this with Cloudflare Pages, you have to set up a transform rule with the following expression: `(http.request.full_uri wildcard "https://YOURDOMAIN.com*" and http.request.uri.path ne "/redirects.json")`\
This is required, so that Cloudflare serves the content from `YOURDOMAIN.com/` (with the exception of the `redirects.json` file), but in code the path will still look the same (`YOURDOMAIN.com/discord` for example).

## Watchlist

I do not manage my watchlist on MyAnimeList but I still wanted an easy way to share and have a look at my watchlist. Conveniently the website I am using for tracking the shows I watch provides a tool to export the watchlist as a json file. Therefore the idea was to write a tool that parses the exported watchlist and adds additional information (see `WatchlistParser`), and to build a simple website which displays the data (`watchlist.html`).

> Make sure to update Cloudflare transform rules when also using redirects. Exclude `watchlist` and `AnimeWatchlist.json`: `(http.request.full_uri wildcard "https://YOURDOMAIN.com*" and http.request.uri.path ne "/redirects.json") and http.request.uri.path ne /watchlist and http.request.uri.path ne AnimeWatchlist.json`