# pong

## Scope
- 2 players
- multiple balls
- x-platform

## Needs work
- Exception handling
- More flexible handling of message parameters
- More flexible handling of player numbers
- Hardcoded configuration
- UDP for performance perhaps
- VR support ;)

## Time
4/8 30min - read spec, chose stack, setup repo, setup env, conceptual brainstorming
- git, vscode, pyenv, poetry, pygame, asyncio streams

5/8 10min - wslg (linux guis in wsl2) only win insider, switching to c#
- monogame, async sockets

5/8 10min - installed monogame vs extension, setup new repo, created first file layouts
5/8 30min - started with server socket and game object structures
5/8 2hrs - set up some project scopes, coding
- server "finished", client socket programming "finished", shared project done

## Conceptual brainstorming
client key press -> server -> broadcast new position -> client draws paddles
 - key press version will probably cause input lag, keeping local player position locally
server checks for collision -> broadcast ball position -> client draws ball
server checks for goal -> updates score -> resets ball
 