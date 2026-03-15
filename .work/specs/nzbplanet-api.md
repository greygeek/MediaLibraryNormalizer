# Api Help Topics - The Best NZB Index

# Api

( **Api Url:** _Api.nzbplanet.net_ )  
( **Australia:** _Nzbplanet.org_ )

  

Here lives the documentation for the api for accessing nzb and index data. Api functions can be called by either logged in users, or by providing an apikey.

Your credentials should be provided as `&apikey=63ccfa0b53591f2ade8bb5b536ddb898`

  

## Available Functions

Use the parameter ?t= to specify the function being called.

### **User Functions**

-   **Cart** `[rss?t=-2&del=1](/rss?t=-2&dl=1&i=1&r=63ccfa0b53591f2ade8bb5b536ddb898&del=1)`  
    Returns the items in a users cart in the form of an rss feed. The optional parameter &del=1 will remove the items from the cart after the feed is requested.
-   **CartAdd** `[?t=cartadd&id=9ca52909ba9b9e5e6758d815fef4ecda](https://api.nzbplanet.net/api?t=cartadd&id=6f657a1b220249984bb2ae64ad077a59&apikey=63ccfa0b53591f2ade8bb5b536ddb898)`  
    Adds an nzb to a users cart.
-   **CartDelete** `[?t=cartdel&id=9ca52909ba9b9e5e6758d815fef4ecda](https://api.nzbplanet.net/api?t=cartdel&id=6f657a1b220249984bb2ae64ad077a59&apikey=63ccfa0b53591f2ade8bb5b536ddb898)`  
    Remove an nzb from a users cart.

  

  
  

### **Search Functions**

-   **Search** `[?t=search&q=linux](https://api.nzbplanet.net/api?t=search&q=linux&apikey=63ccfa0b53591f2ade8bb5b536ddb898)`  
    Returns a list of nzbs matching a query. You can also filter by site category or group name by including a comma separated list as follows `[?t=search&cat=1000,2000&group=a.b.multimedia](https://api.nzbplanet.net/api?t=search&cat=1000,2000&group=a.b.multimedia&apikey=63ccfa0b53591f2ade8bb5b536ddb898)`. Include `&extended=1` to return extended information in the search results.
  
-   **TV** `[?t=tvsearch&q=beverly%20hillbillies&season=1&ep=1](https://api.nzbplanet.net/api?t=tvsearch&q=beverly%20hillbillies&season=1&ep=1&apikey=63ccfa0b53591f2ade8bb5b536ddb898)`  
    Returns a list of nzbs matching a query, category, tvrageid, season or episode. You can also filter by site category by including a comma separated list of categories as follows `[?t=tvsearch&rid=25056&cat=5000](https://api.nzbplanet.net/api?t=tvsearch&rid=25056&cat=5000&apikey=63ccfa0b53591f2ade8bb5b536ddb898)`. Include `&extended=1` to return extended information in the search results.  
    \- **Tvrage**: `rid=25056`  
    \- **Thetvdb**: `tvdbid=153021`  
    \- **TvMaze**: `tvmazeid=73`
  
-   **Movies** `[?t=movie&imdbid=0023010](https://api.nzbplanet.net/api?t=movie&imdbid=0023010&apikey=63ccfa0b53591f2ade8bb5b536ddb898)`  
    Returns a list of nzbs matching a query, an imdbid and optionally a category or genre. Filter by site category by including a comma separated list of categories as follows `[?t=movie&imdbid=0023010&cat=2030,2040&genre=Romance](https://api.nzbplanet.net/api?t=movie&imdbid=0023010&cat=2030,2040&genre=Romance&apikey=63ccfa0b53591f2ade8bb5b536ddb898)`. Include `&extended=1` to return extended information in the search results.
  
-   **Music** `[?t=music&artist=Jack](https://api.nzbplanet.net/api?t=music&artist=Jack&apikey=63ccfa0b53591f2ade8bb5b536ddb898)`  
    Returns a list of nzbs matching an audio based query and optionally a category. Filter by site category by including a comma separated list of categories as follows `[?t=music&artist=Jack&cat=2030,2040](https://api.nzbplanet.net/api?t=music&artist=Jack&cat=2030,2040&apikey=63ccfa0b53591f2ade8bb5b536ddb898)`. Include `&extended=1` to return extended information in the search results. Other search parameters include artist, album, label, year, genre (supports comma separated list).
  
-   **Book** `[?t=book&author=Daniel](https://api.nzbplanet.net/api?t=book&author=Daniel&apikey=63ccfa0b53591f2ade8bb5b536ddb898)`  
    Returns a list of nzbs matching a book based query. Include &extended=1 to return extended information in the search results. Other search parameters include title.
  
-   **Details** `[?t=details&id=6f657a1b220249984bb2ae64ad077a59](https://api.nzbplanet.net/api?t=details&id=6f657a1b220249984bb2ae64ad077a59&apikey=63ccfa0b53591f2ade8bb5b536ddb898)`  
    Returns detailed information about an nzb.
  
-   **GetNfo** `[?t=getnfo&id=6f657a1b220249984bb2ae64ad077a59](https://api.nzbplanet.net/api?t=getnfo&id=6f657a1b220249984bb2ae64ad077a59&apikey=63ccfa0b53591f2ade8bb5b536ddb898)`  
    Returns an nfo file for an nzb. Optional parameter &raw=1 returns just the nfo file without the rss container.
  
-   **Comments** `[?t=comments&id=6f657a1b220249984bb2ae64ad077a59](https://api.nzbplanet.net/api?t=comments&id=6f657a1b220249984bb2ae64ad077a59&apikey=63ccfa0b53591f2ade8bb5b536ddb898)`  
    Returns comments for an nzb.

  

  
  

### **NZB Functions**

-   **Get** `[?t=get&id=9ca52909ba9b9e5e6758d815fef4ecda](https://api.nzbplanet.net/api?t=get&id=6f657a1b220249984bb2ae64ad077a59&apikey=63ccfa0b53591f2ade8bb5b536ddb898)`  
    Downloads the nzb file associated with an Id.
-   **CommentAdd** `[?t=comments&id=6f657a1b220249984bb2ae64ad077a59&text=comment](https://api.nzbplanet.net/api?t=commentadd&id=6f657a1b220249984bb2ae64ad077a59&text=comment&apikey=63ccfa0b53591f2ade8bb5b536ddb898)`  
    Adds a comment to an nzb.

  

  
  

## **Output Format**

Obviously not appropriate to functions which return an nzb file.

-   Xml (default) `?t=search&q=linux&o=xml`  
    Returns the data in an xml document.
-   Json `?t=search&q=linux&o=json`  
    Returns the data in a json object.

  

  
  

## **Extended Attributes**

Using the attrs tag and a comma separated list of supported values, extended information can be returned in the search results.  
For example `?attrs=files,poster,group`. Note that not every attribute is available for every release type. Below is a list of some of the supported attributes. To return all known attributes per release use the parameter `?extended=1`. See the API specification for a full list.

-   files
-   poster
-   group
-   team
-   grabs
-   password
-   comments
-   usenetdate
-   info
-   year
-   season
-   episode
-   rageid
-   tvtitle
-   tvairdate
-   video
-   audio
-   resolution
-   framerate
-   language
-   subs
-   imdb
-   imdbscore
-   imdbtitle
-   imdbtagline
-   imdbplot
-   imdbyear
-   imdbdirector
-   imdbactors
-   genre
-   artist
-   album
-   publisher
-   tracks
-   coverurl
-   backdropcoverurl
-   review

For more advanced info on our API see [here](https://newznab.readthedocs.io/en/latest/misc/api.html)