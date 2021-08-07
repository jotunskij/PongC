# pong

## Scope
- 2 players
- multiple balls
- variable ball radius
- x-platform
- get it working.. ish

## Known bugs
- If NrOfBalls > 1 the messaging is buggy (doesn't work)
- First ball of game is not picked up by clients

## Needs work
- Tests
- Exception handling
- More flexible handling of messages + regex
- More flexible handling of players (position, number, input)
- More dynamic approach to configuration
- More robust socket programming
- Refactoring/clean up - function sizes, file splitting, code formatting, comments..
- Batch messaging (because of multiball)
- Better collision detection
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

5/8 1hr - coding, refactoring
- worked on server game loop (collisions, message sending, scoring..)

6/8 1hr15min - monogame texturing, drawing, input handling

7/8 15min - debugging session

7/8 1hr30min - finalization work, finalized documentation
- IVector2, message optimizations, ball radius, nr of balls, score

Total: 7hr20min + a few minutes here and there

## Conceptual brainstorming
client key press -> server -> broadcast new position -> client draws paddles
 - key press version will probably cause input lag, keeping player position locally

server checks for collision -> broadcast ball position -> client draws ball

server checks for goal -> updates score -> resets ball
 
