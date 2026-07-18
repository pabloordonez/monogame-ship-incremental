# Initial Plan Prompt

I want you to read the @docs/requirement.md document and plan for an MVP.
The main purpose of the MVP is to validate funness, not to include the entire thing. You must chose what mechanics to include and I'll validate it.
I will need you to define ship parts, upgrades and research tree, with names and definition to include.

The MVP must include an agent workflow to accomplish the first MVP. I need you to separate the game into systems and to define each system well for agents to take it.

In terms of high level workflow, I think we need first one agent to define the main app structure and solution. Then we can split the rest into 4 agents. One will work on the assets and asset project following the new Monogame content builder structure, and the other will take systems one by one and start developing, until all systems and parts are done.

For each workflow, I want a develop argent, and then an adversary agent to validate the code from the first as reviewer, and to propose changes and fixes, keeping best coding practices, avoiding redundancies, or inconsistencies, making sure the code is to the point, fast and secure. Prefer entity component system over a lot of inheritance, composition over inheritance when possible. And then last another agent that apply the review changes.

If you can also take the art, I would like you to spin an agent to create the first tentative pixel art for the ship, items, icons, etc. and to create the music for the game following the art guidelines.

Can you help me with that? Please produce all the necessary documentation and plans in the @docs  folder to keep track of the plan and for later execution.

Make sure this plan favors future updates? I want something that I can test and then iterate on. Whatever is created should be easily updated and progressively constructed, adding more and more layers. Please make sure the MVP is not a disposable prototype, but a strong base to construct on top of.
