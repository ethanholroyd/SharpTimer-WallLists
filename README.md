<a name="readme-top"></a>

> [!IMPORTANT]
> Plugin credits go to [K4ryuu](https://github.com/K4ryuu)! They made the base plugin; I made some changes for it to work with SharpTimer and added a few extra features.

<!-- PROJECT LOGO -->
<br />
<div align="center">
  <h1 align="center">KitsuneLab©</h1>
  <h3 align="center">SharpTimer Wall Lists</h3>
  <a align="center">Provides two lists to display on the map, a player Points List, and a Map Records List. Dynamically updates on either map start or a given interval.</a><br>
  <img src="https://files.catbox.moe/pkarm0.png" alt="" style="margin: 0;">

  <p align="center">
    <br />
    <a href="https://github.com/M-archand/SharpTimer-WallLists/releases/tag/1.0.0">Download</a>
  </p>
</div>

<!-- ABOUT THE PROJECT -->

### Dependencies

To use this server addon, you'll need the following dependencies installed:

- [**CounterStrikeSharp**](https://github.com/roflmuffin/CounterStrikeSharp/releases): CounterStrikeSharp allows you to write server plugins in C# for Counter-Strike 2/Source2/CS2
- [**K4-WorldText-API**](https://github.com/K4ryuu/K4-WorldText-API): This is a shared developer API to handle world texts (even multiline, dynamically changing and config saved ones) easily, but in the other hand end users can also use it to spawn world texts as they want and manage them per map.
- [**SharpTimer**](https://github.com/Letaryat/poor-sharptimer): SharpTimer is a "simple" Surf/KZ/Bhop/MG/Deathrun/etc. CS2 Timer plugin using CounterStrikeSharp.
  
<p align="right">(<a href="#readme-top">back to top</a>)</p>

<!-- COMMANDS -->

## Commands (root)

- !pointslist - Create a points list in front of the player and save it to config file.
- !maplist    - Create a map records list for the current map in front of the player and save it to config file.
- !remlist  - Remove the closest list from your position. (100 units max) <br>**You may have to use the !remlist command twice to remove the list from the world view.

<!-- CONFIG -->

## Roadmap

- A config file will be generated on first use located in _/addons/counterstrikesharp/configs/SharpTimer-WallLists_
- The coordinates are saved in json files, located in _/addons/counterstrikesharp/configs/SharpTimer-WallLists/maps_

<!-- ROADMAP -->

## Roadmap

- [X] Update for SharpTimer usage.
- [X] Add color configs. See [here](https://i.sstatic.net/lsuz4.png) for color names.
- [X] Add font size & scale to config.
- [X] Add SQLite & PostgreSQL support. // 1=MySQL, 2=SQLite, 3=PostgreSQL
- [X] Merge MapList/PointsList plugins into a single plugin.
- [ ] Add database table prefix to config to support latest version of SharpTimer.
- [ ] Fix inconsistent results for !remlist

<p align="right">(<a href="#readme-top">back to top</a>)</p>

<!-- LICENSE -->

## License

Distributed under the GPL-3.0 License. See `LICENSE.md` for more information.

<p align="right">(<a href="#readme-top">back to top</a>)</p>
