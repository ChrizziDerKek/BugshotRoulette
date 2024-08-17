# BugshotRoulette
A clone of the famous game "Buckshot Roulette" that was written in C# and supports multiplayer with up to 5 players.

For that I used a websocket server that forwards all received packets to every other connected client.

The code isn't the prettiest I ever wrote and it probably has bugs, hence the name.

# How to play
- Start the BSR_Server.exe file
- Start up to 5 BSR_Client.exe files
- Input a name in the textbox
- Click "Connect"
- Once everyone is connected, any player can click "Start Game" to start the game
- When the game is started, a player can either use an item, shoot another player or shoot themselves
- When everyone except one player is dead, the player that's left won the game

# Screenshots

## Title Screen
![1](Screenshots/1.png?raw=true "Title Screen")

## Started Game

![2](Screenshots/2.png?raw=true "Started Game")

## Another Round

![3](Screenshots/3.png?raw=true "Another Round")

## End Screen

![4](Screenshots/4.png?raw=true "End Screen")

## Server Log

![5](Screenshots/5.png?raw=true "Server Log")
