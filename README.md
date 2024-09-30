# BugshotRoulette
A clone of the famous game "Buckshot Roulette" that was written in C# and supports multiplayer with up to 5 players.

For that I used a websocket server that forwards all received packets to every other connected client.

The code isn't the prettiest I ever wrote and it probably has bugs, hence the name.

# Setup
- Start the BSR_Server.exe file
- Start up to 5 BSR_Client.exe files
- Input a name in the textbox
- Click "Connect"
- Once everyone is connected, any player can click "Start Game" to start the game
- When the game is started, a player can either use an item, shoot another player or shoot themselves
- When everyone except one player is dead, the player that's left won the game

# How to play
- Every player will start with a certain number of lives
- The shotgun will be loaded with live and blank rounds, you will see the number of lives and blanks, but the order of them will be random
- You can either shoot yourself or another player with the shotgun
- If you shoot yourself with a blank, every other turn will get skipped and you can shoot again
- Otherwise a live will deal 1 damage to the target
- If there are no bullets left in the shotgun, another round will start
- At the start of a round, every player will receive items randomly and the shotgun will be reloaded
- This cycle repeats until only 1 player is alive
- These items are available
  - Handcuffs: Skips the enemy's turns so you can shoot 2 times
  - Cigarettes: Restores 1 live
  - Saw: Saws off the shotgun's barrel so that your next shot deals 2 damage, can be combined with gunpowder
  - Magnifying: Shows you the bullet that's currently loaded
  - Beer: Racks the bullet that's currently loaded
  - Inverter: Inverts the currently loaded bullet, a live becomes a blank and vice versa
  - Medicine: Has a 50/50 chance of restoring 2 lives or losing 1
  - Phone: Shows you a random bullet
  - Adrenaline: Allows you to steal an enemy's item which you will use immediately
  - Magazine: Regenerates the bullets like at the start of a round, won't spawn new items
  - Gunpowder: Has a 50/50 chance of dealing 3 damage to your target or 2 to yourself, can be combined with the saw
  - Bullet: Inserts a random bullet at the end of the barrel
  - Trashbin: Allows you to throw away an item and receive a different one, bypasses any item limits
  - Heroine: An enemy can't use any items next round
  - Katana: An enemy can only use 1 item next round
  - Swapper: Swaps all your items with the ones of an enemy
  - Hat: Hides the bullet display for everyone

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
