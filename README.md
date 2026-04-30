# Note: This is WIP project of MapEditorReborn for LabAPI. Join discord for more info.

[![MapEditorReborn](https://i.imgur.com/CeemJnt.png)](https://discord.gg/JwAfeSd79u)

<h1 align="center">MapEditorReborn (LabAPI edition)</h1>
<h3 align="center"><a href="https://store.steampowered.com/app/700330/SCP_Secret_Laboratory/">SCP: Secret Laboratory</a> plugin allowing to spawn and modify various objects.</h3>
<div align="center">
    
<img src="https://img.shields.io/github/downloads/Michal78900/MapEditorReborn/total?style=for-the-badge&logo=github" alt="Downloads">
<a href="https://discord.gg/JwAfeSd79u">
    <img src="https://img.shields.io/discord/947849283514814486?style=for-the-badge&logo=discord" alt="Chat on Discord">
</a>    

<div align="center">
<h4>Fork downloads:</h4>
<img src="https://img.shields.io/github/downloads/Smer4k/ProjectMER/total?style=for-the-badge&logo=github" alt="Downloads">
</div>

</div>

# Installation
Put your [`MapEditorReborn.dll`](https://github.com/Michal78900/ProjectMER/releases/latest) file in `LabAPI-beta/plugins` path.
Once your plugin will load, it will create directory `LapAPI-beta/configs/ProjectMER`; This directory will contain two sub-directories **Schematics** and **Maps**

**[Full MER tutorial](https://docs.google.com/document/d/10V2PnqobeBFb2xTFIHSGmM2KK9_h2wethiVQdcjyhGc/edit?usp=sharing)**

**More support can be found on a [Discord](https://discord.gg/JwAfeSd79u) server**

# New in the fork:
- Waypoints are fixed
- Added all objects from AdminToys, Doors, MirrorObjects, Clutter, PlayerBlocker, CullingParent and Trigger for schematics in Unity
- Actions for Trigger and Interactable (as in AMERT, but more simplified and less functional)
- Cooldown for teleports is now different for each player
- Clutter - spawn an object with some chance
- PlayerBlocker - blocks the player, but not objects and bullets.
- CullingParent - optimization for schematics, removes the rendering of child objects if they have disappeared behind the fog distances. (Works on the client side)
- Trigger - is triggered when the player enters/exits and he is still in it. (For actions or plugins)

# Credits
- Plugin made by [Michal78900](https://github.com/Michal78900)
- Original plugin idea and code overhaul by [Killers0992](https://github.com/Killers0992)
- Another code overhaul and documentation by [Nao](https://github.com/NaoUnderscore)
- Testing the plugin by Cegła, The Jukers server staff and others