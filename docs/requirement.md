# Ship Game

 I want to experiment with different type of game mechanics, to test if the game could be enjoyable and fun. Lately I've been playing incremental idle games, and rogue-like games. These games have in common that are not demanding and require litle to not attention at all. You can play them when you are tired, between work sessions, when listening to podcasts, and still enjoy the game. I appreciate long complex games, but sometimes you just want to play with fun mechanics and feel you are growing and wining, instead of feeling stuck of penalized by the game, life is already difficult as it is.

In that sense, I believe a marriage between random generation, research trees, and metroidvania mechanics could intersect into a fun game:

- Adventures or quests within the game should have a random factor, to allow re-playability. These simple games do not require a lot of manual care, handcrafted levels, because their goal is not to captivate the user, but to enjoy.
- The user should feel it's growing, and it's getting stronger, faster, resourceful. Normally research trees, and resource trees are a good way for the user to feel progress and to motivate playing. Getting upgrades, new abilities and this in time opening new sectors and parts of the game.
- This is where the metroidvania enters the scene. I believe users get more involved when they feel the world as a real open world that can be accessed, more than a level structure. But not all parts of the world are simple or easy. You need more tech to access. Either because enemies are more difficult, or because there are actually physical limits that must be conquered with tools and upgrades.

## World Building

What we exposed so far could be used for a fantasy game, or sci-fi one, it's interchangeable. In this case, I've been remembering the old game Star Control 2, and I've been always involved in fractal world and galaxy generation, and sci-fi literature, which naturally, oriented my imagination in that direction.

I want a ship game where you pilot a space ship in a space setting. I don't have a story in mind yet, I want to first try mechanics, but basically I envision a lobby room kind of scenario where the user can review the galaxy map, the research and item trees, buy and sell products, and plan next adventures, and then the actual play view.

The play view it's a top down space view where you pilot a 2D ship in a solar system or space scene, a you must fight other ships, salvage items from wreckages, mine asteroids, access space stations and other future additions. It would be awesome to have destructive terrain for asteroids.

We could have different stars, using real star nomination, with different solar systems, and different difficulties. Star Color could represent the system composition and difficulty. Not all star types should be accessible, the ship should need special shielding, materials and engines to travel. Same with galaxy core distance. Getting to the galaxy core should be more difficult, you should find more alien races, stars and black holes impose heavier gravity, and the ship should break if not prepared.

We should have different alien races, specialized in science, war, trade, spirituality. Working or fighting them should provide some improvements to the user, or even items.

## Mechanics

- The ship can be piloted with keyboard or a joystick. You should be able to move and fight.
  - The ship should have a shield that can be temporarily depleted against attacks, and need time to recharge. The maximum value of coverage, and depletion and repletion time should be modified with upgrades and research trees.
  - The ship should have especial motion tools, like moving faster over a period of time, or even "teleporting" or moving at light speed that could even act as an attact on other ships. This is also an ability that must be obtained.
  - The ship should have weapon slots. Weapon wise, we should have bullet, laser, plasma and misile types. We could even add more, the more variety the better.
  - The ship could get some helping smaller ships to help, orbiting the main ship and help in the fight.
  - Some of the tech research should let the user choose between spreading attacks, following attacks, etc.
  - Ship could have fuel, and your missions could require you to get back to base to re-stock, and sail away again.

- User should be able to mine asteroids for ores, and we should have different level of ores, in increasing difficulty, to access more complex tech and items.
- We could allow the ship to "orbit" a planet, and investigate resources with probes, and then construct mining facilities to extract resources. This mechanic should be enabled mid game, after some research, but should allow a steady income.
- In a future scope we could add some level of automation, constructing resource planets, and then some other planets that build items for you, and some other that can be sold, and you can connect them, as a global galactic industry. And then defend the routes from attacks.
- Star systems should have mineral types, alien race types if any, planets and moons, and some rare events like black holes, past lost wars with wreakages, ship stations, and other items and objects.
  - Star, planets, moon and maybe even asteroids could be generated with perlin noise or some good random fractal generators.
  - Star system should also have some nebula particle systems for visual purposes, to look beautiful. Depending on the star types we chould change colors and types.
- Game should save progress.

## Technology

I was thinking around this, and we could try to use Monogame as framework, as I'm very fluent on c#. Now Monogame has a new version with a native backend, and support for vulkan, and it's a cross platform framework. It has support for graphics, keyboard, sounds, you name it.

## Art

I was envisioning a 2D pixel art look and feel. We can have shaders for bloom and light effects, like lasers, or even maybe some pixel shadow, but I want to keep a simple and appealing look and feel. The game should accept sprite atlas with images for the ship, weapons, attack types, research icons, ores, etc.

Music wise, I want space music, a little vaporwave, a little like Vangelis on Blade Runner, should feel out of this world.

## Screens

I don't want something super complex. Maybe a studio name introduction, stating that the game uses monogame and any other open source mention to keep authors visible, and then transition to a start screen, that can move with parallax, between a planet, starts and the ship, entering from the sides, and maybe they could move a little and different speeds when the user move mouse. Should have a Start/Continue entry, Options, Credits and Exit options. Later in the future we could have an introduction scene where we explain the lore, but then we go directly to the game stage, to that lobby screen where we have the galactic map, and the options to navigate to different game mechanics like the research lab.
