# Ship Game

I want to experiment with different types of game mechanics, to test if the game could be enjoyable and fun. Lately I've been playing incremental idle games and rogue-like games. These games have in common that they are not demanding and require little to no attention at all. You can play them when you are tired, between work sessions, when listening to podcasts, and still enjoy the game. I appreciate long complex games, but sometimes you just want to play with fun mechanics and feel you are growing and winning, instead of feeling stuck or penalized by the game; life is already difficult as it is.

In that sense, I believe a marriage between random generation, research trees, and Metroidvania mechanics could intersect into a fun game:

- Adventures or quests within the game should have a random factor, to allow replayability. These simple games do not require a lot of manual care or handcrafted levels, because their goal is not to captivate the user with set-piece design, but to let them enjoy the loop.
- The user should feel they are growing, and getting stronger, faster, and more resourceful. Normally research trees and resource trees are a good way for the user to feel progress and to stay motivated. Getting upgrades and new abilities over time opens new sectors and parts of the game.
- This is where the Metroidvania idea enters. I believe users get more involved when they feel the world as a real open world that can be accessed, more than a level structure. But not all parts of the world are simple or easy. You need more tech to access them, either because enemies are more difficult, or because there are physical limits that must be overcome with tools and upgrades.

## World Building

What we exposed so far could be used for a fantasy game or a sci-fi one; it's interchangeable. In this case, I've been remembering the old game Star Control 2, and I've always been involved in fractal world and galaxy generation and sci-fi literature, which naturally oriented my imagination in that direction.

I want a ship game where you pilot a spaceship in a space setting. I don't have a story in mind yet; I want to first try mechanics. Basically I envision a lobby-room kind of scenario where the user can review the galaxy map, the research and item trees, buy and sell products, and plan next adventures — and then the actual play view.

The play view is a top-down space view where you pilot a 2D ship in a solar system or space scene, and you must fight other ships, salvage items from wreckages, mine asteroids, access space stations, and other future additions. It would be awesome to have destructive terrain for asteroids.

We could have different stars, using real star nomenclature, with different solar systems and different difficulties. Star color could represent the system composition and difficulty. Not all star types should be accessible; the ship should need special shielding, materials, and engines to travel. Same with galaxy-core distance. Getting to the galaxy core should be more difficult: you should find more alien races; stars and black holes impose heavier gravity; and the ship should break if not prepared.

We should have different alien races, specialized in science, war, trade, and spirituality. Working with or fighting them should provide some improvements to the user, or even items.

## Mechanics

- The ship can be piloted with keyboard or a joystick. You should be able to move and fight.
  - The ship should have a shield that can be temporarily depleted against attacks, and needs time to recharge. The maximum coverage, depletion rate, and recharge time should be modified with upgrades and research trees.
  - The ship should have special motion tools, like moving faster over a period of time, or even "teleporting" / moving at light speed that could even act as an attack on other ships. This is also an ability that must be obtained.
  - The ship should have weapon slots. Weapon-wise, we should have bullet, laser, plasma, and missile types. We could even add more — the more variety the better.
  - The ship could get some helping smaller ships that orbit the main ship and help in the fight.
  - Some of the tech research should let the user choose between spreading attacks, following attacks, etc.
  - The ship could have fuel, and missions could require you to get back to base to restock, and sail away again.

- The user should be able to mine asteroids for ores, and we should have different levels of ores, in increasing difficulty, to access more complex tech and items.
- We could allow the ship to "orbit" a planet, investigate resources with probes, and then construct mining facilities to extract resources. This mechanic should be enabled mid-game, after some research, but should allow a steady income.
- In a future scope we could add some level of automation: constructing resource planets, then some other planets that build items for you, and some that produce goods that can be sold; you can connect them as a global galactic industry, and then defend the routes from attacks.
- Star systems should have mineral types, alien race types if any, planets and moons, and some rare events like black holes, past lost wars with wreckages, ship stations, and other items and objects.
  - Stars, planets, moons, and maybe even asteroids could be generated with Perlin noise or some good random fractal generators.
  - Star systems should also have some nebula particle systems for visual purposes, to look beautiful. Depending on the star types we could change colors and types.
- The game should save progress.

## Technology

I was thinking around this, and we could try to use MonoGame as the framework, as I'm very fluent in C#. MonoGame has a new version with a native backend and support for Vulkan, and it's a cross-platform framework. It has support for graphics, keyboard, sounds — you name it.

## Art

I was envisioning a 2D pixel-art look and feel. We can have shaders for bloom and light effects (like lasers), or even maybe some pixel shadow, but I want to keep a simple and appealing look and feel. The game should accept a sprite atlas with images for the ship, weapons, attack types, research icons, ores, etc.

Music-wise, I want space music — a little vaporwave, a little like Vangelis on Blade Runner — that should feel out of this world.

## Screens

I don't want something super complex. Maybe a studio-name introduction, stating that the game uses MonoGame and any other open-source mentions to keep authors visible, and then a transition to a start screen that can move with parallax between a planet, stars, and the ship entering from the sides. They could move a little at different speeds when the user moves the mouse. It should have Start/Continue, Options, Credits, and Exit. Later we could have an introduction scene where we explain the lore, but then we go directly to the game stage: the lobby screen with the galactic map, and options to navigate to different game mechanics like the research lab.
