# Progress Report 2 - Forsaken

## Summarize Proposed Goals and Prior Feedback

### 1. What goals did you originally plan to meet by this deadline? How did you delegate tasks to meet those goals?

For this checkpoint, we planned to solidify core game systems that could act as a functional foundation. Our goal was to go beyond static features and start developing features that could be demonstrated in real-time gameplay. Specifically, we planned to:

- Implement a **Revival Mechanic** that lets players bring defeated enemies back to life with the ability to assign names to them.
- Add **Combat Mechanics** for both melee and ranged attacks.
- Start basic **Enemy AI** with unique behaviors.
- Establish a **super basic level** for testing purposes.
- Create interactive **Chests** that hold item prefabs.
- Implement a **semi-working Checkpoint System** that handles respawning.
- Add basic **player and enemy animations** (work-in-progress).
- Build a **Title Screen** (also work-in-progress).
- Make the **Player Movement** feel smoother and more responsive.

**Task Delegation:**

- **Andrew**: player mechanics, player/enemy animations, combat (bow and melee), revival system, AI behavior, chests, and responsive movement. Currently handling the Hotbar redesign and item system overhaul.
- **Nhien**: level design and layout, experimenting with tile layers and player movement restrictions.
- **Kien**: early version of the title screen.
- **Philip**: revive object where it stores the location of the player so when they die they spawn where they touched it. Helped with all of the GitHub issues.
- **Phoenix**: music/audio composition and implementation.

### 2. What feedback did you receive on your last progress report? How did you incorporate that feedback?

We received specific and useful feedback from Dr. Currin regarding the scope and clarity of our project. The key points were:

- Focus on core gameplay functionality: Get one level fully functional, including combat and enemy interactions.
- Build out a single weapon and enemy type first.
- Clarify task delegation, particularly with combat and level design.
- Defer narrative and complex systems (like revival vs. steal) until core mechanics are ready.
- Create a contingency plan for stretch goals like the power-stealing mechanic.
- Document assets properly as we go.

**Our responses to the feedback include:**

- Scaled back narrative work to prioritize combat, movement, and interactivity.
- Completed work on boss revival.
- Were considering making items that allow the player to scale in power.
- Increased clarity in who is working on what, reinforced through GitHub issues and regular check-ins.
- Actively organizing and listing art/audio sources (see bottom of report).
- Dedicated our efforts to finalizing a fully working level, with responsive movement, animated enemies, and engaging combat.
- Leveraged lessons from our previous 2D platformer project for faster prototyping and logic reuse.

---

## Evaluate Progress Made Since Last Progress Report

### 1. Which of the goals you outlined for this point in your last progress report have been met? Which have you made progress toward but not yet met?

**Goals Fully Completed:**

- **Revival Mechanic**: Players can revive defeated enemies and assign names. This functionality is fully integrated into gameplay. This mechanic adds a level of emergent storytelling and strategy.
- **Combat (Bow + Melee)**: Both systems are functional, tested, and implemented. Melee uses close-range hitbox detection; bow uses projectile logic with cooldown management.
- **Flying Demon AI**: The enemy behaves dynamically: it flies toward the player when they run away and away from the player when approached directly. This is our first proof-of-concept AI with personality.
- **Chests**: Interactable and store item prefabs. These will later be essential for progression and upgrades.
- **Checkpoint System**: Currently functional. When the player touches a checkpoint and dies, they respawn at that point. It needs further polish but works.
- **Player Animations**: Implemented across walking, attacking, and idling using the RVROS animation asset pack.
- **Enemy Animations**: Rough framework complete; fine-tuning ongoing using sprites from Ansimuz and Admurin.
- **Player Movement**: Significantly improved. Movements now feel tight and responsive, with smooth keyboard input handling. Includes dashing and jump buffering.

**Goals In Progress:**

- **Title Screen**: Functional prototype exists, with working input handling, but lacks visual and audio polish.
- **Inventory System**: Initially too complicated. We're reworking this into a streamlined Hotbar System that better fits the game's pacing. It will integrate with chests and item pickups.
- **Level Design**: A simple layout is complete for testing, but we are facing Unity-specific issues with tile layers and sorting. The map currently lacks visual variety and cohesion.

---

### 2. What strategies have been working well for your group? What successes have you had? Are you ready for your demo?

**Effective Strategies:**

- Frequent Check-Ins: Weekly syncs and almost daily updates in our group chat helped keep momentum high. We also used whatsapp and google docs and trello to keep track of everything.
- Clearly Delegated Tasks: Everyone knows what they're responsible for, and we’ve had minimal overlap or miscommunication. We updated our GitHub Projects and assigned cards for clarity.
- Version Control & GitHub: We’ve used Issues, pull requests, and branching to avoid conflicts. We've also started writing better commit messages for traceability.

**Successes:**

- Combat systems feel satisfying and responsive.
- AI for Flying Demon creates a memorable and reactive encounter.
- The revival mechanic adds a layer of strategy and customization.
- Chests + items introduce the potential for power scaling and replayability.
- Player movement now supports edge cases like cancelling attacks into dashes and vice versa.

**Demo Readiness:**

We are semi demo-ready. Our final polishing tasks include refining collision layers, adjusting combat feel (hit feedback), and resolving a few visual bugs and mechanics. We plan to showcase some of our game mechanics such as: spawn in → explore → fight → revive → respawn → loot chest.

---

### 3. Did you run into any issues in this part of the project? What were those issues? What have you done to try to address them?

**Level Design Issues:**

We’re still trying to resolve sprite layering and player collision with tiles. Sometimes the player appears "under" or "above" tiles they shouldn't. This makes exploration feel glitchy.

**Fix:** Nhien is rebuilding the level using different tilemap layers and Unity's built-in sorting system. We're also testing each new layout on multiple resolutions.

**Inventory Complexity:**

The original inventory design had too many UI panels and options, which interrupted the flow of gameplay.

**Fix:** Andrew is redesigning it as a minimal Hotbar with mouse/keyboard compatibility, drag-and-drop support, and keybindings for quick use.

**Enemy AI Expansion:**

Making enemies feel unique is time-consuming. Our goal was to have at least 3 different AI behaviors, but we found balancing and bugs difficult to manage.

**Fix:** We’re starting with template AI scripts and using scriptable objects to define stats and behavior patterns. We’re prioritizing interaction over complexity.

---

## Set/Refine Goals for Next Checkpoint

### 1. What were your original goals for the next checkpoint?

- Finalize the inventory system.
- Finish one complete level.
- Add more enemy types with unique AI.
- Upgrade title screen/pause menu with working buttons.
- Integrate music into gameplay.

**Things we are going to look out for Progress Report 3:**
- Begin narrative development.
- Flesh out more levels

### 2. Do you need to modify those original goals? If so, what are your new goals?

Yes. Based on time constraints and feedback, we’ve revised our priorities:

**Revised Goals:**

- Focus entirely on one polished level with working AI Enemys, smooth combat, and responsive movement.
- Fully implement and polish the Hotbar system.
- Finalize and style the title screen.
- Add Phoenix’s music to key scenes and add sound cues to combat and revival.
- **Pause narrative implementation until all mechanics are solid.**
- Begin working toward one simple boss encounter (if time permits).

---

## Outline Plans to Meet Goals

### 1. What tasks are required to meet the goals you have for your next checkpoint?

- Fix tilemap layering, collisions, and walkable areas.
- Add polish and final visuals to the title screen.
- Finish coding the Hotbar inventory and link it to usable items.
- Build out two more enemy types with distinct behavior (one grounded, one ranged).
- Trigger Phoenix’s music/sound based on in-game actions (e.g., death, loot, combat).
- Conduct playtesting sessions, record feedback, and iterate on pain points.
- Create a feedback form for demo playtesters.

### 2. How are you delegating those tasks? Who is responsible for what?

- **Andrew:**
  - Finish Hotbar system and item interactions
  - Polish combat system
  - Expand enemy AI (add at least two new types)

- **Nhien:**
  - Rebuild level using correct layering and collision groups
  - Test and verify walkable paths and visual layering
  - Add decorations and props to enhance visual clarity

- **Kien:**
  - Finalize the title screen visuals
  - Add transition buttons and menu functionality
  - Implement settings menu if time allows
  - Assist with playtesting and feedback analysis

- **Philip:**
  - Test full respawn loops in context of the level
  - Add feedback animation or particle effect to checkpoints
  - Possible Dialogue
  - Polishing menu

- **Phoenix:**
  - Finish composing background and menu themes
  - Create layered soundtracks depending on proximity or event

We’ll continue regular meetings and address new issues as they come up. Our priority now is to have a stable and polished gameplay demo for the next next showcase. After the demo, we will return to polishing our level and enemies and extending our mechanics.

---

## Assets Used

We are documenting assets here to stay ahead of final citation requirements:

- **Player Animations**: [rvros.itch.io/animated-pixel-hero](https://rvros.itch.io/animated-pixel-hero)
- **Background + Flying Demon Enemy**: [ansimuz.itch.io/gothicvania-patreon-collection](https://ansimuz.itch.io/gothicvania-patreon-collection)
- **Chest Animations**: [admurin.itch.io/free-chest-animations](https://admurin.itch.io/free-chest-animations)
- **Slime Enemy**: [admurin.itch.io/enemy-slime-1](https://admurin.itch.io/enemy-slime-1)
- **Item Icons**: [alexs-assets.itch.io/16x16-rpg-item-pack](https://alexs-assets.itch.io/16x16-rpg-item-pack)
- **Respawn Checkpoint**: [Five pixel art images of trees](https://babanagi.itch.io/free-pixelart-tree-bundle) 
- **Title Font**: [Kian Wrote This](https://fontmeme.com/pixel-fonts/)

Future assets will continue to be added and documented as we expand enemy types, items, and environments.