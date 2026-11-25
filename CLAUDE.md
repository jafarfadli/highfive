**Ready Set Seoul** (named on 2026-10-19; before that *Be Korean!*, and the working title *Seoul First Week*, 서울 첫 주, which stays as the Korean subtitle) is a first-person 3D simulation game where international students rehearse their first week in Korea. Players walk a Myeongdong street, enter a clinic, an immigration office and a bank, and get through each counter visit by **choosing what to say to Korean-speaking staff NPCs**. Every staff line and every answer comes with a translation in the player's own language.

Built for the 2026 Global Start-up Design Thinking Hackathon (Chung-Ang University, Seoul, 29 Sep – 2 Oct 2026) and now prepared for the Global Startup Competition 2026 (Vietnam, 8–11 November 2026; see `references/COMPETITION.md` and `PRODUCT.md`). The category is **immersive media × AI**, delivered as a **game**. The pitch is: "We train the *situation*, not the vocabulary." Papago translates words and Duolingo teaches vocabulary; this game rehearses the flow of a real counter visit under real pressure.

**In scope, in priority order:**
1. Hub: one Myeongdong street block (from OSM) that the player walks in first person, fenced by invisible barriers.
2. Clinic scenario, complete from end to end. This is the emotional core of the demo.
3. Immigration office scenario (Alien Registration Card).
4. Results screen: progress, wrong answers, time, what to remember, and a phrase card to take into real life.
5. Bank scenario: demonstrates the procedure-dependency mechanic.
6. Home screen and scenario select (places × parts). Each place gets more parts over time; unfinished ones show as "Coming soon".
7. Logbook: for every finished part, what to bring, the phrase card, key words and the whole conversation, so a player who forgot a little can check instead of replaying.

**Out of scope for the hackathon:** open-world navigation beyond the block, character customisation, multiplayer, free-text or voice input, a general translation feature, legal or visa advice, VR headset builds, and the phone camera form-reader. XR and the camera feature appear in the pitch as the roadmap.

## Working rules

These hold for every change to the current build.

- **Decisions, not input.** Conversations are multiple choice. Don't add typing, push-to-talk or a points score unless the team decides to.
- **Every answer is something a real student might say.** Wrong answers are realistic mistakes that teach something, never absurd filler (see *Writing answer choices*).
- **Data first.** Levels, dialogue, documents and dependencies live in JSON (`Data/Procedures/`, `Resources/levels.json`). Code reads the data; it never hard-codes a step.
- **Scenes are generated.** Change the builder and its kits (`SeoulSceneBuilder`, `BuildKit`, `TextureKit`, `RoomAtlas`, `LookKit`, `BuildingSkin`, `FacadeDresser`, `StreetProps`, `InteriorKit`, `CharacterKit`, `InkKit`, and `HumanBuilder` for people), then rebuild (*Seoul First Week → Build All Scenes*). Hand edits to scenes are lost.
- **A level always starts outside.** Story card → street → building → results. Pause → Reset and Results → Retry restart from the story card on the street, never from inside the building.
- **One thing to do at a time.** The HUD shows the scenario title, a progress bar and the single current to-do, top-left. No checklists, no "0/12 answered" counters.
- **Nothing floats, nothing overlaps.** Props stand on the pavement; windows are left out wherever a door, sign or prop covers the wall; street furniture is squared to the facades and kept clear of buildings and doors; seated people have their feet on the floor; a monitor never stands between a staff member and the customer.
- **Follow the UI design system below** for every screen, and the **visual standards** for everything in the 3D world.
- **Test in play mode, then clean up.** Play-test with `Editor/PlaytestTools`. The editor recompiles *during* play mode, so stop play mode before editing scripts. The team also plays the build, so after automated runs delete only the telemetry rows your test sessions wrote, and put back the player's own progress (`PlayerPrefs` key `sfw.progress`) instead of wiping it.

## Game design

### Core loop (every level)
1. On the scenario select screen, pick a place and a part. A black **story card** shows the background in large type (why the player is going there today). Click anywhere to start.
2. The player starts on the street about 40 m down the road from the building, so the walk is part of the level; only its door is open and marked.
3. Inside the bank or the immigration office, take a queue number (번호표); the number board and PA call the player to a counter. A neighbourhood clinic works the way it really does: no ticket; the player walks up to the reception and registers, sits and waits while the masked-name monitor and the nurse's calls take the patients ahead in, walks into 1진료실 when their own name is called, and walks back out to the reception to pay.
4. The staff NPC speaks Korean, with a subtitle and a translation. The player picks one of 2–4 answers, each shown in Korean, romanization and translation (click, or keys 1–4). R replays the line slowly.
5. A wrong answer gets a realistic reaction from the staff member plus a short note explaining the lesson. That answer is crossed out and the player tries again. Asking the staff to repeat or explain is allowed and is not counted as a mistake. It is never a game-over, except when the procedure itself blocks the player (see below).
6. Some steps are actions rather than lines: going to the doctor's room, leaving the counter to fill in a form and coming back, entering a PIN on a keypad, or putting a finger on the scanner.
7. The level ends with a **results screen**: progress, wrong answers, time, the notes from any mistakes, and a phrase card. The player can retry (from the street) or return to the scenario select.
8. A finished part opens in the **Logbook** (scenario select: the top-right button, or the pill on a done part card). It is built entirely from the procedure file, in three tabs: *Before you go* (the lesson; what to bring, i.e. the documents handed over with right answers, where alternatives in one step become "A 또는 B", card or cash collapse into one chip and a form filled in at the office says so; what you leave with; the phrase card), *Key words*, and *Conversation* phase by phase (staff line, right answers with the document handed over, ways to ask with the staff reply, and mistakes with their notes). Every line has a listen button that plays its baked voice. Unfinished parts are listed but locked.

### Pressure (what makes it immersive)
- The street has a crowd ambience; inside, other visitors wait in the seats and other numbers are called before the player's.
- PA announcements and a live number board.
- Background music sets the mood per place (menus, street, interiors).

### Procedure dependencies
Quests chain the way real procedures do. For example, the bank asks for an Alien Registration Card, so a player who goes to the bank first is politely turned away and learns the order. Dependencies are defined in procedure and level data (see below), not in code.

### Players and languages
- Primary persona: a 20-year-old Vietnamese D-4 language-course student in their first six months in Seoul.
- Player languages (UI, translations and notes): Vietnamese, Indonesian, English. The player picks one on the Home screen.
- NPC language: Korean, using the polite register real staff use (해요체 / 합쇼체).
- The player's facts, which every answer must agree with: name Minh, born 15 March 2005, D-4 language-course visa, lives in the dormitory at 동작구 흑석로 84, Korean phone 010-1234-5678. Carries a passport, certificate of enrollment, ID photo, overseas card and cash from the start.

## Conversation data

Staff are **scripted and grounded**: everything that can happen at a counter is in the procedure file for that location, and game state changes only through the result of the answer the player picks.

- `Data/Procedures/<location>.json` lists ordered steps. A step has the staff member, the location in the building (`counter`, `doctor`, …), an optional PA announcement, an optional `task` (the player's to-do from that step on, shown in the HUD), the Korean line (`npc`) with its translation, and the answer `choices`.
- Each choice has `ko`, `rom`, `meaning`, a `result`, the staff `reply` and its translation, a `note` explaining the lesson of a wrong answer, and optionally `give` (a document handed over), `needs` / `unless` (flags that show or hide it) and `grants`.
- Results: `advance` (correct, next step), `retry` (wrong: counted as a mistake, note shown, crossed out), `ask` (asking to repeat or explain: staff answer, crossed out, not a mistake), `block` (the procedure turns the player away and ends the level) and `fetch` (the player has to go and get something, such as filling in a form, then come back).
- Other step fields: `type: keypad` for PIN entry (`confirmPrevious` checks the second entry), `fetch` (document, station and objective), `grants` / `takes` for documents, and `after` for a closing line.
- Procedure fields: `queue.counter` is the counter the PA calls the player to (every counter in a building has its own trigger and seat, so each part can use a different one); `queue.mode` is `ticket` (default: 번호표, number board, `ahead` people before the player) or `walkin` (the clinic: straight to the reception, then `queue.names` are called into the treatment room by the doctor step's `announce` before the player; the player walks through the room's door, a `RoomEntrance`, and back out to the counter to pay); `carry` lists documents the player brought from home for this visit (they must be named in the level's story; the HUD shows them on arrival); `grants` are shown on the results screen; `blockedLesson` explains a `block`; `phrases` is the phrase card; `glossary` holds the part's key words for the Logbook (10–12 entries of `ko`, `rom`, `meaning`, each a word or short expression that occurs in the dialogue; pairs the player confuses go in one entry, such as `부터 · 까지`).
- Answers are shuffled each time they are shown. Choices that need a document the player doesn't have are hidden, which is how the bank turns away a player without an ARC.
- `Resources/levels.json` lists places and their parts (`id`, `level` = part number, `procedure`, `scene`, `entrance`, `available`, `needs`, `title`, `story`). Parts of one place share its building scene: the scene's `ScenarioDirector` holds every procedure file of that place and runs the one named by the current level. Adding a part is a data task (procedure file, level entry, add the file to the builder's `Director(...)` list, re-bake voices); a new location also needs a room in the builder.
- Current parts: clinic 1 *See a doctor* (counter 1 + consulting room), clinic 2 *Get a medical certificate* (counter 2 + doctor), immigration 1 *Get a residence card* (counter 3), immigration 2 *Extend your stay* (counter 2, needs the ARC), bank 1 *Open a bank account* (counter 2, needs the ARC), bank 2 *Send money home* (counter 3, needs the bankbook). In player-facing text, a window is always a "counter" (창구, quầy, loket). `needs` only warns on the scenario select and on the street; the procedure itself turns the player away with a `block` choice.
- Voice: every line the game can speak is **pre-rendered with OmniVoice** (k2-fsa; code Apache-2.0, model weights CC-BY-NC: fine for the competition demo, a commercial release must re-bake with a commercially licensed model) into `Resources/Voice/<key>.ogg`, so voices ship with the build and work on any platform, offline. `BakedVoice` finds a line by `md5("<voice>|normal|<text>")` (or `|slow|` for the "Hear again" replay); PA calls with random queue numbers are stitched from baked pieces. Lines that aren't baked fall back to runtime TTS: OpenAI when a key is configured, otherwise the macOS Korean voices, otherwise subtitles only. All clips are normalised to one loudness; staff voices play almost 2D (spatial blend 0.12) so they stay loud at the counter, and the music ducks to 25 % while staff speak.
- **After editing any dialogue, phrases or key words, re-bake:** `~/.cache/seoul-sim/omnivoice-venv/bin/python -I Tools/voice/bake_voices.py` (only new lines are rendered; unused files are removed; phrases bake in the `player` voice and key words in the `pa` voice; about 9 s a line on an Apple Silicon Mac). Every character has its own voice: `VOICES` describes it (gender, age, pitch) and `Tools/voice/anchors/<voice>.wav` keeps the designed reference clip, so re-bakes sound the same; a new staff id gets an anchor designed on its first bake. Each take is checked with Whisper and rendered again if it isn't heard as the line; lines still unclear after three takes are listed in `Tools/voice/bake_report.txt`. Numbers are spelled out in Hangul before synthesis. Setup is in `Tools/voice/README.md`.
- Runtime fallback voices on macOS: prefer **Yuna**, the clearest macOS Korean voice. The Eloquence voices (Sandy, Eddy, Reed, Flo, …) are quieter and more robotic.

### Writing answer choices
- Every option, right or wrong, must be something a real foreign student could plausibly say at that moment. Test: would a teacher recognise it as a common learner mistake?
- Good wrong answers teach one thing: the wrong register (반말 without -요), easily confused words (부터 / 까지, 처음 오셨어요 / 처음 뵙겠습니다, 식전 / 식후, 기침 / 콧물), the wrong document (학생증 instead of 재학증명서), a procedural misunderstanding (asking reception for medicine, thinking insurance means you pay nothing), or a vague answer where staff need specifics.
- Never write options no one would say: "I'm fine" when the clinic asks about symptoms, "from tomorrow" when asked how long you've been sick, obvious lies that contradict the player's facts, or rude nonsense.
- Every `retry` has a `note`: one or two sentences naming the lesson and the correct phrase. The staff `reply` is what real staff would say back, politely steering the player.
- Use `ask` for honest help-seeking ("네? 다시 한번만 말씀해 주세요", "천천히 말씀해 주시겠어요?", "…가 뭐예요?"). It teaches that asking is fine.
- Keep 2–4 options per step and at most one or two correct ones. Translations are short and plain in all three languages.
- Give each phase of a visit a specific `task` ("Register at the reception", "See the doctor in room 1", "Pay at the reception"). Don't lump different staff into one generic to-do.

### Procedure accuracy
Procedure files are drafts until a Korean teammate verifies them against official sources (e.g., HiKorea, the clinic's or bank's official guides). Each file carries `verified: true|false` and a `sources` list. Only verified content goes into the demo path.

## UI design system: "Retro Ink"

### Overview
**Creative North Star: your first week in Seoul as a retro comic print.** Bold ink lines, flat colour, hard solid shadows and halftone, with Seoul's own paperwork and signage as the furniture: queue tickets, application forms, stamps, notes, projecting signboards. It grew out of the hand-inked direction in `references/art/` and the art bible (published under the earlier name, *Be Korean! Art Bible*: https://claude.ai/artifact/GkwvbEPKuKtJ68oqEnywdM); since 2026-10-18 it is firmer and less papery (no tape, torn edges or paper texture) and has one palette.

**Key characteristics:**
- Every card, balloon, keycap, ticket and note is a flat fill with a thick ink outline and a solid ink shadow offset down-right (never blurred, never translucent); choices and actions are bold brush bars.
- Highlights do the pointing: titles sit on orange bands, the current to-do on a mustard highlighter, what an answer hands over on a mustard chip.
- State is a **mark** as well as a colour: a drawn ○ for right or chosen, a slash for wrong, a 완료 stamp for done, a note for a lesson or a missing document, a shutter and a 준비중 sticker for coming soon.
- Text is solid and bold enough to read at a glance: Korean is the largest line in every exchange; romanization and translation sit under it in solid ink, never in a thin or translucent face.
- Motion is drawn: things slap down, stamp, draw on and peel off. States and screens never cross-fade (a slap or peel may ramp its own opacity as part of the motion).

### Palette (`UI/Theme.cs`)
One palette, Persimmon: a retro print inside one warm band of the colour wheel with near-black ink and cream. It colours the UI only; the 3D world keeps its own colours (see *Visual standards*).

| Role | Colour | Used for |
|---|---|---|
| Ink | `#1E0F0A` | lines, solid shadows, all text on light fills |
| Ink soft | `#4A2618` | tried answers, minor marks |
| Paper | `#FCEBCB` | cards, keycaps, buttons, plaques |
| Paper deep | `#F2C47E` | kraft pages, locked rows, muted lettering |
| Brush | `#EE5A24` | the one active thing: title bands, hovered or picked answer, primary button, progress, marks, the sunburst |
| Brush deep | `#B0381A` | pressed, the block under extruded lettering |
| Glow | `#FFC23A` | highlights: the to-do band, document chips, notes (lessons, warnings), stars, the title sun, chosen options on orange |
| Night | `#2B1610` | answer strips, name strips, dark plates under text on the 3D scene |
| Balloon | `#FFF8EA` | speech balloons, tickets, forms, logo letters |
| On brush | `#1E0F0A` | text on a brush or glow fill |
| Ground | `#F2C47E` | plain full-screen backdrops (UI captures) |

Code never uses literal colours: graphics take a `Role` (`Themed`, `InkShape.fillRole`, `UIKit.Words(... Role ...)`). `Palette` remains only as a shim for older call sites and maps onto roles.

### Lettering (`Resources/Fonts`, wired by `InkKit`)
- **Bangers**, display (Latin and Vietnamese): titles, banners, big numbers, keycaps, button words. Lettered with `UIKit.Lettering` (an `InkText` outline plus a hard offset shadow, both scaled to the type: light letters get an ink outline of 5 % of the size, dark and brush letters none; the shadow sits 5.5 % right and 6.5 % down, in ink, or in the brush colour under dark letters). `InkText.extrude` turns it into retro block lettering (the logo). Hangul falls back to Black Han Sans.
- **Black Han Sans**, Hangul display: place names, signboards, stamps, 번호표.
- **Noto Sans KR**, reading: Bold for staff lines, answers, labels, hints, romanization and anything at 24 px or less; Regular only for translations under a bold Korean line.

Sizes at 1080p: logo 170 / 260; screen titles 58–64; banners 72; staff line 36; answers 30; body 21–24; labels and hints at least 22; keycaps 18–26 (PIN keys 36). Small text is solid (alpha 1) in Ink on light fills or Paper on Night; text on the 3D scene sits on a Night plate.

**The Korean First Rule** still holds: in a conversation element Korean is the largest line; romanization and translation follow below it, smaller, in solid ink.

### Parts kit (`UI/UIKitInk.cs`, `UI/InkShape.cs`)
All shapes are procedural meshes (`InkShape`), not sprites, so any size works. The retro print keeps only a trace of the hand-inked wobble (35 %), raggedness (15 %), paper grain (20 %) and dry brush (30 %) a shape asks for (`InkShape` constants); outlines are 4–5 px and shadows solid ink.
- `PaperPanel`: a flat card with a thick ink outline and a solid offset shadow.
- `BrushStrip`: a bold brush bar, painted left to right (`reveal`). Answer strips, name strips, title bands, primary buttons, banners, progress, plates.
- `SpeechBalloon`: a balloon with a tail pointing at the speaker.
- `StickyNote`: a glow note with an outline and a solid shadow; `Highlight`: a glow highlighter band behind a line of text; `Rays`: a retro sunburst; `DocIcon`: a small document.
- `CircleMark`, `PenStroke`, `Arrow`, `Dot`: the drawn marks; `StampMark`: a round rubber stamp; `Keycap`: an inked key with a label (keys, and a part's number beside its title, never a "PART N" label above it).
- `InkButton` (Primary = brush bar, Paper = card, Night = dark bar) with `InkHover`: tilts and lifts off its shadow on hover or focus, slams flat when pressed.
- `InkSlider`, `InkScrim` (ink wash with halftone dots behind modals).

### Screens
- **Home:** one page of key art. The camera stands low in 명동길 (it drifts a little) with the people this game is about in the right half of the frame: the student (the player, from Vietnam) talking with the clinic nurse, the immigration officer and the bank teller beside them (built into the Home scene as `Cast_…` people that pedestrians walk around); the nurse asks "어디가 불편하세요?" in a speech balloon with its translation. The left column sits on a halftone field that fades out before the street (`HalftoneField`): "READY SET / SEOUL!" in big extruded block letters (cream, ink outline, rust block), the 서울 첫 주 tag, the tagline on a card, the hint language as a dropdown ("Language: English ▼", opening a short list upward), and a breathing mustard "▶ 시작 · Click anywhere to start" bar.
- **Scenario select:** "어디 갈까요?" on an orange band; each place is a projecting signboard (돌출간판, one syllable per row, an ink emblem) with its local name on a plaque under it, and its parts beside it as cards with the part number as a brush keycap beside the title (best fit, two lines at most). Done → 완료 stamp and a Logbook bar; needs a document → note; not built → shutter slats and a 준비중 sticker. Logbook top right, Esc · Back bottom left, reset progress as an underlined note.
- **Story card:** an ink page with a glow caption box (place, part) and the story in bold type on a card.
- **HUD:** a quest card top-left (the title on an orange band, place · part, a brush progress bar, the one to-do on a mustard highlighter); the thermal 번호표 ticket that prints down from its slot and gets circled when it's your turn; a night prompt bar with an E keycap; the controls help on a night plate bottom-left (drops away in three steps); banners lettered over a brush swash with the small line on a night plate, peeled off in steps when done (and at once when a conversation opens, so a name call never covers the staff member or the answers). **No compass or markers:** the to-do, the one open door with its beacon, the number board and the PA show the way.
- **Conversation** (laid out so the staff member stays in view, like a story-game dialogue): what they say runs in a dark dialogue box along the bottom (1300 wide: the name on an orange band over its top edge, the Korean line in cream, the translation in mustard, R keycap "hear again"); the answers stack up on the right above it (660 wide, bottom-aligned) as night bars with keycaps 1–4 that light to orange with ★★ on hover, the Korean line, romanization · meaning, and for an answer that hands something over a mustard chip with a document icon and "Hand over: 여권 · Passport" in the player's language (wrapping when long); the pick gets a drawn ○ around its keycap, the rest peel away; a tried answer is set aside but stays opaque (an ink-soft bar, muted lettering, a brush slash and an × keycap); the lesson of a wrong answer is a note on the left above the box. The PIN pad is a card of inked keys, 340 × 560, right of the staff member and left of the toast column.
- **Pause, settings, dialogs, form:** cream cards with a thick outline and a big solid shadow, the title in cream letters on an orange band; settings hold Music, SFX, Sensitivity and Calm animation (reduced motion); the application form is a balloon-paper sheet with drawn tick boxes.
- **Results:** a kraft page with the teacher's stamp (참 잘했어요! / 다시 해 봐요) and the lessons as notes stacked down the page under it; a report card (progress, wrong answers, time in Bangers numbers, brush progress, the lesson strip, documents received); the phrase card; Save / Retry / Scenarios →.
- **Logbook:** an orange title band, a kraft index of parts with their numbers as keycaps (the selected part turns orange and its keycap paper; locked parts are opaque kraft), a card page with the place in Black Han Sans, tabs as keycap buttons (selected = orange), phases as night bars, answers marked ✓ ? × ! in dots, mistake notes as notes, listen buttons as drawn play triangles.
- **Toasts:** cards (✓) or notes (!) slapped onto the top-right corner (a column 420 wide from x 1456, clear of the PIN pad), peeled off after a few seconds. **Scene changes:** an ink sheet with a brush edge sweeps across the screen (`Overlay.FadeTo`).

### Motion (`UI/Motion.cs`)
Shape changes (drawing on, wiping in, printing) step at 12 fps like hand-drawn animation; transforms (slap, stamp, peel) snap with a hard overshoot. All motion runs on unscaled time and stops when its target is destroyed. **Calm animation** (`Settings.ReducedMotion`) jumps to end states and stops the boil, shakes, the turning sunburst and the breathing start bar.
- `Slap` (0.26 s): a card arrives from 1.18× and a few degrees off, lands at its resting tilt.
- `Draw` (about 0.3 s, stepped): rings, strokes and strips draw on (`InkShape.reveal`).
- `Stamp` (0.3 s + shake): 2.3× → 0.88× → 1, then the page shakes (the logo lands this way).
- `Peel` (0.42 s): lifts, turns and drops out of frame.
- `Print` (0.5 s, stepped): the queue ticket feeds out of its slot. `Pop`, `SlideIn`, `Shake`.

### Do's and Don'ts
**Do:** use roles and the parts kit; keep Korean first and largest; keep small text bold, solid and at least 22 px; put text on the 3D scene on a night plate; point with highlights (orange bands, mustard highlighters and chips); show one current to-do; mark state with ○, slashes, stamps and notes as well as colour; slap, draw and peel instead of fading.
**Don't:** bring back paper texture, torn edges or tape; use translucent shadows or text; add a second theme or literal colours; use a thin or handwriting face for reading text; add a compass, checklists, counters or scores to the HUD; cross-fade; use emoji (the fonts can't draw them: inside text use ✓ × ← ▶ ★, and draw standalone icons with the kit's strokes, `Arrow`, `CircleMark`, `PenStroke`, `DocIcon`); let a dark translucent wash do the work of a card (UI blends in linear space, so a 60 % ink wash barely darkens); show state with CanvasGroup alpha (use an opaque softer fill); put a small label above a title. uGUI best fit only shrinks text whose `verticalOverflow` is `Truncate`.

## Visual standards (3D world)

The world is low-poly and built from primitives, but never flat-shaded: every large surface is textured, every object has a real-world size, and the Korean details carry the identity. The target is "the Myeongdong a student will walk down next week": dense Hangul signage, bright cosmetics shops, food carts, grey office interiors with a queue-number board.

### The inked look (2026-10-18)
The 3D world is drawn like the UI's graphic novel **without changing its colours**: the street, buildings, signs and interiors keep the palette they had (the team approved it); only shading, lines and textures changed.
- **Cel lighting** (`Art/Shaders/InkLighting.hlsl`, used by Surface, Character and Hair): direct light in three flat tones (shadow 0, half 0.36, lit 0.8 of the light; the levels are the averages of the smooth N·L they replace, so overall brightness stays); ambient in three tones by facing (up, sides, down: `CelAmbient`); crisp specular spots; sky reflections, SSAO and fog as in URP Lit.
- **Halftone** in the shade: screen-space dots (7 px at 1080p) where the sun doesn't reach, weighted by how strongly the sun outshines the ambient (so a weak indoor sun doesn't dot every wall), and on faces turned away from the ambient light; only within 25–60 m. Deep shade near the camera (6–18 m) is hatched as well: 45° lines 5 px apart at 1080p, 0.9–1.6 px wide as the shade deepens. Faces and hands stay clean; `_DetailAlbedoMapScale` holds the strength per material.
- **Ink outlines** (`Art/Shaders/InkOutline.shader`, a URP Full Screen Pass renderer feature named "Ink" that `InkKit.EnsureOutline` installs on every renderer, after the opaque world and the sky, before transparents): silhouettes from the Laplacian of inverse depth (zero on any flat surface whatever its slope, so the ground stays clean), creases from the scene normals. Two weights, as in a comic: a silhouette straddles its edge, 2.2 px a side near the camera (4–5 px in all at 1080p) thinning to 1 px a side by 20 m; a crease is one pixel. The line is a deep shade of what it outlines and fades from 30 m to 110 m; plus a faint screen-fixed paper grain. `InkKit.EnsureOutline` owns the material's values, so a stale material can't keep old widths. Glass, world-space text and UI are drawn after it and stay clean.
- **Illustrated textures** (`TextureKit.Ink`, applied to every generated texture): tone cut into five flat steps around the average (the material colour stays), grains, chips and stains kept as crisp patches, joints and seams drawn as ink lines from the height field, hatching over the darkest patches, drawn scuffs on close-range textures (pavement, floors, wood, walls, metal), normal relief calmed so cel shading doesn't speckle. Close-range textures are larger: pavement 2048, floors, wood, wall paint, plastic, fabric and metal 1024.

### Look presets (`LookKit`)
Each scene gets one preset: a global Volume, the sky and ambient light. Tonemapping is Neutral everywhere; bloom picks up emissive signs and lamps (threshold 1.05).
- **Street** (Hub), late-afternoon gold for a storybook mood: a strong warm sun from the south-south-west (rotation 50°, 22°, intensity 2.0, `#FFC88A`, shadow strength 0.92) high enough to reach down into 명동길 (the street is a canyon: any sun below about 40° leaves it all in shade), cool blue-violet trilight ambient (`#8C9ED2` / `#C2A690` / `#5F5049`), warm haze fog 60–340 m (`#ECCBA9`), and the toon sky (`Sky_Seoul`, shader *Sky Toon*: a deep blue zenith over a peach horizon, the sun with a glow, inked clouds drifting overhead); exposure +0.1, contrast 20, saturation 18, temperature +12, bloom 0.75 above 0.95, vignette 0.28, blue-violet shadows and warm highlights.
- **Interior** (clinic, immigration, bank): bright office light; trilight ambient, a weak sun, six ceiling point lights (range 8 m, intensity 1.05); exposure +0.3, contrast 14, saturation 8, temperature +3, bloom 0.45, vignette 0.2.
- **Menu** (Home, Select): the street light and sky with a matching grade (contrast 18, saturation 14, temperature +12, bloom 0.7, vignette 0.32) plus a Gaussian depth of field from 14 m to 70 m, so the UI reads over a soft background.
- No realtime reflection probes: URP's probe atlas throws in edit-mode renders, and the stale entries persist until the scene is reopened. Glossy surfaces reflect the sky.

### Materials and textures (`TextureKit`, `RoomAtlas`)
- Every textured surface comes from `TextureKit`: tileable albedo (alpha = smoothness) with a normal map painted from a height field, saved under `Art/Generated/Textures` and generated only when missing (*Seoul First Week → Repaint Textures* regenerates them). Each texture knows how many metres one repeat covers.
- **`TextureKit.Surface`** (shader `SeoulFirstWeek/Surface`): the default for boxes, floors, walls, counters and props. The texture is projected triplanar in metres, so primitives need no UVs and keep the same texel density at any size; object-space projection for anything that moves or rotates.
- **`TextureKit.LitUV`** (the Surface shader with `_UV_MAPPED`): for meshes that carry UVs in metres, i.e. the buildings (`BuildingSkin` unwraps them) and the pavement (UVs across and along the street, so the pavers follow its bends). The normal map shades the surface but stays out of the depth-normals texture, so the ink pass outlines geometry, not every brick joint.
- **`Mats.Lit`** (flat colour on the cel Surface shader) for small parts under about 0.3 m; **`Mats.Emissive`** and **`Mats.Glass`** stay URP Lit for screens, lights and glass. People have their own textures and shaders (see *People*). When a material's shader changes, `BuildKit`'s persist hook moves the saved material asset over.
- Textures are light and neutral and take their colour from the material tint, so one texture serves every building colour (brick is the exception). A new kind of surface gets a new generator in `TextureKit`; noise must be periodic so it tiles.
- Catalogue: granite pavers (명동길), asphalt, facade tile / brick / stone / concrete / metal panel / plaster, green urethane roof paint (most Seoul rooftops) and concrete roofs, vinyl / terrazzo / stone / speckled granite floors, carpet tiles, 600 mm mineral-fibre ceiling tiles (텍스), wall paint, light and dark wood, fabric, brushed and perforated metal, plastic, rubber mat.

### Shaders (`Art/Shaders`)
- **`SeoulFirstWeek/Surface`**: cel-shaded, triplanar with the Ben Golus whiteout normal blend (or mesh UVs with `_UV_MAPPED`). `_BaseMap` tiling x = repeats per metre; reuses URP Lit inputs, shadow and depth passes (SRP Batcher compatible, SSAO works).
- **`SeoulFirstWeek/Ink Outline`**: the full-screen ink pass (see *The inked look*). **`SeoulFirstWeek/UI Ink`**: the UI's ink material (print grain, bristles, grunge, halftone per vertex).
- **`SeoulFirstWeek/Sky Toon`**: the skybox: a gradient sky, the sun (direction set by `LookKit` from the scene's sun) and fractal-noise clouds cut with a crisp edge, shaded in two flat tones with a warm rim towards the sun and halftone in the shade, and outlined in ink; they drift slowly. `LookKit` sets every value of `Sky_Seoul.mat`.
- **`SeoulFirstWeek/Interior Window`**: fake rooms behind flat glass. Each pane carries its room in vertex data: TEXCOORD1 = (atlas cell, lights 0..1, width m, height m), TEXCOORD2 = (depth m, blind 0..1); tangent = the viewer's right. It adds a glass tint, blinds and a Fresnel sky/street reflection, and writes depth and normals.
- **`SeoulFirstWeek/Character`**: every body, garment and face in one cel-shaded material. Fabric, normal and face texture arrays chosen per vertex (UV1); PBR with normal maps that need no mesh tangents (cotangent frame from screen derivatives), a flat warm band at the skin's terminator (no soft wrap) and a velvety rim on knits, wool, corduroy and fleece.
- **`SeoulFirstWeek/Hair`**: a flat two-tone mass: strand textures tinted by the vertex colour, thin inked strand lines where the texture dips, painted occlusion as one step, cel ambient and cel-toned diffuse, and one flat Kajiya-Kay shine band cut by the strands (direction from the screen derivatives of UV.v, so no tangents). Keep the shine modest: in low sun broad highlights on dark hair read as pale patches.
- `RoomAtlas` paints the rooms (4 × 2 cells each): upper floors (office, home, 학원 classroom, café, storage, PC방, clinic, 노래방) and shops (cosmetics, convenience store, café, clothing, phones, restaurant, pharmacy, 분식). In every cell the bottom 14 % is floor and the top 6 % ceiling; side walls continue the edges of the back wall, so shelves may run to the edges but single features must stay clear of them.
- New shaders follow the same pattern: URP Lit inputs or their own `UnityPerMaterial` shared by every pass, fog, a DepthOnly and a DepthNormals pass.

### Buildings and facades (`BuildingSkin`, `FacadeDresser`)
- Every OSM building gets flat-shaded, unwrapped copies of its mesh, a cladding (tile 27 %, plaster 20 %, stone 16 %, metal panel 14 %, concrete 12 %, brick 11 %) in a tint from `WallPalette`, and a roof (65 % green paint, otherwise concrete).
- Walls facing 명동길 get shop fronts: lit shop glass (the atlas cell follows the shop name), mullions, a door with pull handles, a fascia with a LED strip under it, at most one poster per pane and never on the door, and blade signs only for names with Hangul.
- Every building storey has one use (office, home, academy, café, storage, PC방, clinic, karaoke) that sets its room cell, whether the lights are on, and its signage: sign bands in the spandrels of storeys 1–4 on the street side, business names spelt one syllable per window, and tall stacked signs (돌출 간판) listing the floors.
- Detail falls off with distance from the street: full frames, sills, slab bands and air-conditioner units within 24 m or on street-facing walls; a flat frame up to 55 m; only the glass beyond, and walls under 7 m are skipped there.
- World text (`WorldText`) fits its rect with best fit, which cannot go below 8 px. So labels under 30 cm tall get a finer canvas, just fine enough for about 40 px of height or 14 px per line. Don't go finer: every glyph size shares one dynamic font atlas, and large Hangul glyphs overflow it.
- Signage is generic and fictional: Hangul business names, English only where Myeongdong would have it (TAX FREE, EXCHANGE, COSMETICS, SALE). No real brands, and no Chinese or Japanese characters (the font can't be trusted with them).

### Street furniture (`StreetProps`)
- Catalogue at real sizes: steel bollard with a reflective band (0.85 m), general waste and recycling bins (1.0 m), A-frame boards (0.95 m), sidewalk sales tables, delivery scooters with a box, utility cabinets, manholes, paired lamp-post banners, food stalls with a price board, bare bulbs and steam.
- Placement: squared to the facade, 0.3–0.8 m in front of it, at least 2 m from the next prop, 3.6 m from any door and 2.5 m from spawn and approach points, never poking into a building (`HitsBuilding`), always on the pavement (`GroundY`).
- Bollards mark every place where the invisible barrier crosses a side street or recess, and a row closes both ends of the playable street.

### People (`HumanBuilder`, `HumanRig`)
- **One skinned mesh per person, built from code.** `HumanBuilder.Build(parent, name, look, collider, detailed)` makes the joint hierarchy (body, hips, knees, torso, shoulders, elbows, head) and one `SkinnedMeshRenderer` whose bones are those joints; `HumanRig` animates the joints. Everything a person wears is lofted around the body: `HumanBody` (body, trousers, skirts, tops, jackets and coats, sleeves, hands, shoes, ties, scarves, lanyards, stethoscope, bags) and `HumanHead` (head, painted face, ears, hair, hats, masks, glasses, earphones), with the geometry helpers in `HumanMesh`. In the editor, `BuildKit` saves the meshes under `Art/Generated/Meshes/People`.
- **Looks** (`HumanLook`): sex, age, height, build, skin, face slot, hair style and colour, top, outer layer, bottom, shoes, hat, bag, glasses, mask and extras. Crowds draw archetypes (student, office worker, trendy, 아줌마, 아저씨, senior, school kid, foreigner) with weights per place (`Random(rng, Crowd.Street | Clinic | Immigration | Bank)`; the immigration office is mostly foreigners). Staff use presets: `Nurse`, `Doctor`, `Officer`, `Teller`, `Guard`, `Vendor`. Tellers wear office clothes with the bank's green lanyard, because Korean banks dropped teller uniforms in 2019–21; the immigration guide is an `Officer` in a yellow vest. Indoors people sit, so only crossbody bags stay on; a tote or shoulder bag hangs against the hip under the right arm, and the rig holds that arm a little out over it (`shoulderBag`).
- **Korean style**, as a Seoul street dresses: short puffers and long padded coats, wool and trench coats, cardigans, knit vests, blazers, wide slacks and jeans, long skirts, chunky sneakers, bucket hats and visors, KF94 masks, round glasses; two-block, comma and side-part cuts for men; long straight hair, bobs, layered cuts, ponytails, low buns and short perms for women.
- **Proportions.** 1.9 rig units tall at scale 1 (hips 0.95, thigh 0.42, knee-to-sole 0.53, shoulders 1.46, elbows 1.18, neck 1.53), scaled to the person's height. The head centre sits `FaceMap.CenterUp` (0.17) above the neck joint, the neck is about 0.6 of the head's width (`NeckRx`), and the jaw narrows only below its angle (wider for men). Below the waist every garment is widened (`OverLegs`) so trouser legs, thighs and skirts never poke through tops, jackets, coats or aprons, and trouser legs merge into the seat at the top. Sleeve heads round over the deltoid into the shoulder seam (`SleeveHead`): never cap a sleeve with a tall dome, it reads as a puff sleeve.
- **Faces and hair.** `CharacterKit` paints 16 face slots (young and adult women and men, an 아줌마 with tattooed brows, grandparents, school kids, foreigners), flat for the inked look (feature planes as hard-edged tone steps, brows as solid tapered strokes) and projected onto the head from the front; `FaceMap` holds the feature positions shared by the head sculpt (eye sockets, nose, lips, chin, cheekbones) and the painter. Close-up people (`detailed`) also get a mouth that opens while talking and blinking eyelids. Hair is a shell over the skull (hairline and thickness per style) plus hair cards (double-sided strand ribbons) for long hair, bangs and fringes, with hanging hair kept behind the shoulders. Korean men's hair is long and full: about 3 cm of top layer falling forward into bangs just above the brows (two-block dandy cut), swept to one side and curling in (comma hair) or opening into curtain bangs (가르마펌); locks round the sides and back cover the temples and hang over the undercut (`SideLocks`); a 2:8 side part gets a sweep from the parting, plus a side-swept fringe for men under middle age (`Sweep`). Locks are laid on top of the shell (`ShellAt`), and none are made under a hat with a crown.
- **Textures and shaders.** `CharacterKit` paints texture arrays into `Art/Generated/Characters` (16 fabrics with normal maps, the faces, hair strands) and keeps the materials in `Resources/HumanStyle.asset`; *Seoul First Week → Repaint Characters* regenerates them (a build only paints missing ones). Each vertex says what it is made of: COLOR = tint with baked occlusion in alpha, UV0 = fabric repeats or the face projection, UV1 = (texture slice, type: cloth, skin, face, soft cloth). `SeoulFirstWeek/Character` and `SeoulFirstWeek/Hair` light them (see *Shaders*).
- **Detail levels.** Staff and visitors about 13k vertices for men and 15k for women (at most about 18k); pedestrians use the light version (`detailed: false`, about 6k, no mouth or eyelids), whose hair shell keeps 10 rows and a 1.5 mm allowance so its flat quads don't let the scalp show through thin hair.
- **Poses.** Walking bends the knees; **sitting** puts the thighs on the seat and solves the thigh slope (or a forward shin) so the feet reach the floor, hands resting on the thighs; **typing** keeps the forearms level at desk height; **talking at the desk** rests the forearms on the desk edge with the upper arms turned (`Arm(pitch, roll, twist)`) so the hands come together; **phone** holds it in the right hand in front of the chest while the other hand rests in the lap; talking adds nods and a gesturing hand.
- Seats: a seat anchor sits on the floor 6–7 cm behind the chair's origin (about 9 cm behind the seat centre, so the back meets the backrest), and the rig's `seatHeight` is the top of the seat (`InteriorKit.ChairSeat` 0.44 m, `OfficeSeat` 0.47 m). Builders call `rig.Pose(0)` so scenes are saved already posed.
- Staff sit at their desks: the keyboard at the desk edge in front of them, the monitor beside it and turned towards them. The thinking bubble hangs above the head.
- **Checking people.** `PlaytestTools.Studio(path, looks, closeUp, pose, yaw, width, height, detailed)` photographs a row of looks in a throwaway studio scene (full figures or head and shoulders; poses stand, walk, sit, type, talk). Check the front, side, back and seated, and both detail levels, before rebuilding the scenes.

### Interiors (`SeoulSceneBuilder.Shell`, `Counters`, `InteriorKit`)
The three places must never look alike. They were rebuilt on 2026-10-18 from research on about 150 photos of Korean clinics, bank branches and immigration halls: a small, warm neighbourhood 내과, a big, cool civil-affairs hall, and a polished bank branch. The shell is shared; everything that makes a place recognisable comes from its own builder.
- **Shell (every room):**
  - Floor, walls and ceiling slab, and the glazed street wall with the captured street backdrop 5 m outside (`CaptureBackdrops` photographs 명동길 from each entrance).
  - Automatic sliding doors (`AutoDoor`): the leaves slide when anyone comes near, and a blocker on Ignore Raycast keeps the doorway shut to the player. With them come the EXIT sign (비상구), the door sensor and a rubber mat.
  - By the door: a hand sanitiser and an umbrella bagger (우산 비닐).
  - Six point lights in the room's colour temperature.
  - The counters (`Counters`): extent, window positions, name plates, acrylic screens, and solid partitions or glass dividers per room.
  - The exit and the player.
  - Ceilings, wainscot, boards, kiosks, seats, props and people are added per place.
- **Counter styles.** High: the clinic reception, a 1.05 m ledge in front of a 0.74 m desk; the patient stands, visitors stand. Low: bank and public-office counters at 0.74 m with a customer chair; the player sits (`PlayerSeat` on the anchor, camera eye height 1.2 m, a short fade) and so do visitors. The doctor's room seats the player on a round stool across the desk.
- **Waiting seats** (`SeatRow`) come in blocks facing the counters, with walking lanes between them (`Room.SeatGaps`).
  - Visitors walk seat → lane → aisle → counter, so every lane must run clear from the entrance zone to the aisle. A kiosk by the counters sends arrivals along the aisle instead (`QueueCrowd`).
  - Each row's parts are merged into one mesh per material, with one collider per block; the seat anchors stay.
  - Kinds: moulded shells, perforated steel beam seats with armrests (`BeamSeat`), and upholstered sofas (`Sofa`, which returns its anchors).
- **Alive:**
  - Wall clocks keep local time (`WallClock`). TVs and waiting monitors cycle pages (`ScreenCycler`, through `WallTv(..., pages)`). LED tickers scroll (`Ticker`, `TickerStrip`).
  - A counter can have its own red LED number display (`QueueBoard.window`, `HangingCounterSign`). A main board can list the last number of every counter (`QueueBoard.rows`, `MainBoard`).
  - A few people stay put where real ones would (`Extra`): on a clinic bench, at the form table, at the ATM, at the slip desk, at the information desk and by the photo booth. Keep them off the lanes, the aisle and the counter spots.
  - At the clinic, new arrivals register at the reception before they sit down (`QueueCrowd.reception`), called patients walk into 1진료실 (`CallToRoom`), and the patient before comes out to pay.
- **Clinic (튼튼내과의원, 14 × 10 m, ceiling 2.7 m):**
  - Finishes: ivory walls with a light-oak wainscot and a mint rail, a beige porcelain floor, and a gypsum ceiling with downlights and a lit cove over the sofas.
  - Reception: a stone-white ledge floating on a glow strip, in front of an oak slat wall with a backlit sign and designation plaques. Desk signs read 접수·수납, 서류·예약 and 진료 시 본인 확인 필수 (clinics must check ID since 2024-05-20), with ribboned orchids beside them.
  - No ticket machine and no number board: the clinic runs as a walk-in place (`queue.mode`). Beside the counter hangs the masked-name waiting monitor (`WaitingBoard`, 진료 대기 현황, 김*수); the player's name joins it after registering and lights up.
  - Three treatment-room doors carry lit 진료중 / 검사중 boxes.
  - Waiting area: sofa islands facing reception and the TV. Along the walls:
    - a built-in tufted bench with a senior on an IV drip and a woman on her phone, and the health TV over it;
    - a height scale, the blood-pressure machine and the 정수기;
    - a parent and child on a second bench;
    - by the door, the October flu-shot X-banner and a leaflet rack.
  - 1진료실, in the south-east corner: the desk with two monitors, diplomas, a glass-fronted medicine cabinet, an exam bed with its paper roll and curtain, a sink, an x-ray viewer, a clock and a half-lowered roller blind. Its door is the `RoomEntrance` the player walks through when called; called patients walk in there too, and the ones before come out and pay at the other counter.
- **Immigration (출입국 민원센터, 22 × 15 m, ceiling 3.0 m):**
  - Finishes: cool troffers on a 600 grid, terrazzo, and beige laminate with a navy rail.
  - Six counters sit behind tall acrylic with beige partitions. Each has a blue hanging sign in Korean and English with its red LED number, a name plate, a fingerprint scanner and piles of paperwork.
  - The main board hangs over the aisle. An LED ticker lists the four steps: reservation, form, documents, revenue stamp.
  - Four rows of perforated steel beam seats; 18 people are seated at the start.
  - By the door: two ticket kiosks, the information desk with its officer, a guide in a yellow vest, and the process easel (업무처리 순서).
  - The form table (`FetchStation`), with a sample form and glue sticks, stands under the notice board.
  - East wall: the photo booth, the certificate kiosk, the copier and the 수입인지 machine.
  - HiKorea TVs, two big clocks, and welcome banners in Korean, English, Vietnamese and Indonesian.
- **Bank (새싹은행 명동지점, 18 × 13 m, ceiling 3.2 m):**
  - Finishes: speckled granite, and a gypsum ceiling with linear lights and a light-oak slat field over the benches. The slats run towards the counters, so from the door you look along them.
  - Four teller desks with green partitions (frosted tops) and hanging LED counter signs. Each desk has a PIN pad, a banknote counter, a passbook printer, a companion stool, and an orchid or a leaflet stand.
  - An oak slat wall behind the tellers carries the backlit logo and the call screen.
  - Green benches stand in rows.
  - The lit 365 ATM corner sits behind glass by the door, with someone at an ATM.
  - The ticket kiosk stands by the guard (청원경찰), who watches the player.
  - Along the walls: exchange and deposit rate boards, a scrolling ticker, the slip desk with a senior writing, newspapers, a 정수기 and a coffee corner, a phishing warning and a lunch-hour X-banner.
  - The glass VIP room has a carpet, armchairs and a floor lamp.
- Keep walking lines clear: the path from the door to the ticket machine, the lanes, the aisle in front of the counters and every counter spot.

### Performance budget
Measured at 1920 × 1080 with post-processing on the development Mac: Hub about 10 ms per frame, interiors about 6 ms. The Hub holds about 600k vertices in about 1,400 renderers and about 300 world-space text canvases. The interiors hold 150–190k vertices in 600–700 renderers, with 45–95 text canvases. Keep facade geometry batched per material (`FacadeDresser.Batch`), keep world text near the street, give small props and facade details no shadows, and use at most seven point lights per interior. People are skinned meshes (see *People*): the 38 or so pedestrians cost the Hub about 2 ms and the people in an interior under 1 ms (edit-mode render timing). With the inked look (cel shading, the two-weight ink pass, halftone and hatching, 2048 pavement, the toon sky) a 1920 × 1080 frame measured 7.9–9.2 ms in the Hub, 8.4 ms on Home and 6.8–7.9 ms in the clinic (play mode, 2026-10-18). Every *Build All Scenes* ends by pruning generated materials, textures and meshes that no scene uses.

### Checking the look
Take screenshots with `PlaytestTools.Shot` / `Capture`, or a temporary camera with `UniversalAdditionalCameraData.renderPostProcessing` on; `PlaytestTools.UIShot` renders a piece of UI over a backdrop in either theme. Visitors and pedestrians only exist in play mode, so check seated poses and conversation framing there. The first render after a shader edit can come out tinted cyan or blue while Unity compiles variants asynchronously; render again before judging colours.

## Technical architecture (Unity)

- **Unity 6 (6000.4), URP.** Build a **desktop build** (macOS/Windows) for the demo. The offline voices rely on macOS `say`, so web builds are out of scope.
- **Input System actions** only: Move, Look, LookHold (hold the left mouse button to look around), Sprint, Interact, Document (answer keys 1–4), Repeat and Pause. The first-person controller reads actions rather than raw mouse and keyboard, so the rig can later be swapped for an XR Origin. This is the "XR-ready" claim in the pitch. PushToTalk, Hint and Type are still in the actions asset but unused.
- **Scenes are generated by code.** `SeoulFirstWeek.EditorTools.SeoulSceneBuilder` (menu *Seoul First Week → Build All Scenes*) builds Home, Select, Hub, the three interiors and Results, sets the build scene order and prunes unused generated assets. Hand edits to scenes are overwritten on the next build, so change the builder instead. Home and Select are copies of the Hub with a drifting camera behind the UI. The kits it uses: `BuildKit` (primitives, signs, player), `TextureKit` and `RoomAtlas` (textures, materials), `LookKit` (post-processing, sky), `BuildingSkin` (building UVs and cladding), `FacadeDresser` (windows, shop fronts, signage), `StreetProps` and `InteriorKit` (furniture), `CharacterKit` (people's textures and materials; the people themselves come from `HumanBuilder`).
- **Hub build order:** look (sun, fog, sky, volume) → pavement → entrances → building skins → street props (lamps with banners, planters, stalls, banners, street furniture, manholes) → facade dressing (shop fronts and stacked signs, then windows that skip anything covering the wall) → invisible barriers with bollards → street backdrops for the interiors → navmesh → pedestrians. Props are placed with `GroundY`, which lands on the pavement. The interiors are built after the Hub because they use its backdrops.
- The Hub corridor is closed by a continuous wall of invisible box colliders on the Ignore Raycast layer, so interaction rays pass through it; the wall steps back behind each door. The imported building meshes are single-sided and can't be relied on to stop the player.
- **Audio.** Music: one persistent `Music` player picks a track per scene from `Resources/Audio/Music/` (`menu` for Home, Select and Results; `outdoor` for the Hub; `indoor` for every interior), keeps playing across scenes that share a track, crossfades otherwise, loops with a crossfade at the seam, and ducks under staff speech. A missing track is silence. Tracks are trimmed of long fade-out outros before import (so the loop seam doesn't dip) and level-matched; `indoor` plays at 70 % so it stays under dialogue. Ambience: only the street, `Resources/Audio/crowd`. Sound effects are synthesised (`ProceduralAudio`). The Music slider controls music; SFX controls effects and the crowd. `AudioImportRules` streams music and keeps short clips in memory.
- Every scene-serialized MonoBehaviour lives in a file named after its class; otherwise it saves as a missing script.
- UI is built from code following the design system above: `UI/UIKit.cs` (canvases, layout helpers), `UI/UIKitInk.cs` (the parts kit and lettering), `UI/InkShape.cs`, `UI/InkText.cs`, `UI/InkHover.cs`, `UI/Motion.cs`, `UI/Theme.cs` and `UI/Themed.cs`. `Editor/InkKit.cs` paints the UI's print texture (`Art/Generated/UI/InkNoise.png`), sets up the fonts' fallbacks, writes `Resources/InkStyle.asset` and installs the 3D ink pass; *Seoul First Week → Repaint UI Ink* repaints it, and every *Build All Scenes* runs it. The Logbook (`UI/Logbook.cs`) reads the procedure files through `LevelSelectScreen.procedures`, which the builder fills with every file in `Data/Procedures/` when it builds the menus.
- Progress flags (`done:<level id>`, `doc:<document>`) and settings (music, SFX, look sensitivity, language) are stored in PlayerPrefs. Progress can be reset from the scenario select screen.
- Network calls use `async`/`await` with `UnityWebRequest` and keep the main thread free.
- API keys (only the optional OpenAI voice uses one) live in a git-ignored local config file (`Assets/StreamingAssets/secrets.json`, see `secrets.example.json`) or the `OPENAI_API_KEY` environment variable, and never in scenes, prefabs or source.
- Every finished level appends a row to a local CSV log (`<persistentDataPath>/telemetry/levels.csv`: level, outcome, progress, wrong answers, time, language, procedure verified) for the user test at the venue.
- `Editor/PlaytestTools.cs` drives automated play-tests (stepping frames, playing a level through, capturing screenshots with UI). Play mode stalls when the editor is in the background, so tests advance frames with `EditorApplication.Step`.
- `Editor/ClipRecorder.cs` records footage for the pitch video: it steps play mode at a fixed 30 fps (`Time.captureFramerate`), saves every frame as a JPEG with the UI, and rebuilds the sound track from `Audio/AudioTap` (every voice, PA call and sound effect reports itself there), because live audio can't be captured while the editor steps; ffmpeg joins frames and audio.wav. Staff lines therefore wait in game time (`NpcActor.PlayAsync`), not on the audio device. Input injection doesn't reach the game while the editor is paused, so recordings move the player through its `CharacterController` along a NavMesh path. The UI is recorded by rendering the overlay canvases through the camera, so the menus' depth of field would blur it wherever the street behind is far; with depth of field on, each frame is put together from the blurred world and a sharp render of the UI, whose coverage is measured over black and white. Clips go to `Recordings/` (git-ignored).

### Project layout
```
Assets/_Project/
  Scenes/          Home, Select, Hub, Clinic, Immigration, Bank, Results (generated)
  Scripts/         Audio/, Conversation/ (voice), Core/, Editor/, Player/, Procedure/, Quest/, UI/, Telemetry/, World/
  Data/Procedures/ one file per location (steps, choices, outcomes, verified, sources)
  Resources/       levels.json (places, parts, stories), Fonts/ (Noto Sans KR, Bangers, Black Han Sans),
                   Voice/ (baked OmniVoice lines, generated), Audio/ (crowd ambience, Music/menu|outdoor|indoor),
                   HumanStyle.asset (people's materials), InkStyle.asset (UI ink material, noise and fonts)
  Art/Environment/Myeongdong/   OSM-derived block, decimated
  Art/Generated/   meshes (people in Meshes/People), materials, textures, street backdrops,
                   Characters/ (fabric, face and hair texture arrays) and UI/ (InkNoise, UIInk and InkOutline
                   materials) written by the scene builder
  Art/Shaders/     SeoulFirstWeek/Surface (cel, triplanar or UV), Interior Window, Character, Hair, Ink Outline,
                   UI Ink, Sky Toon, and the shared InkLighting.hlsl
  Settings/Look/   post-processing profiles (Street, Interior, Menu) and the sky material
  Art/Interiors/   interior assets (interiors are currently built from primitives by the builder)
  Art/Characters/  character assets (unused: people are built by HumanBuilder, see People)
```
Outside `Assets/`: `Tools/voice/` holds the voice bake script and its setup notes.

C# namespace: `SeoulFirstWeek.*`.

## Environment and assets

- **Hub source:** a Myeongdong/Jung-gu block imported from **OpenStreetMap** via the Blosm Blender add-on (ODbL). Export from Blender as FBX or glTF in metres, decimate the facades, and keep only the street the player walks. Use OSM-derived geometry only. Google Photorealistic 3D Tiles are excluded because Google's terms forbid offline use and extraction.
- **Ground:** the imported roads float 0.4 m above the ground and stop short of some buildings, so the builder lays one paved surface from facade to facade over the playable street. Everything on the street stands on it.
- **Building placement is fictional.** The clinic, immigration office and bank sit on the Myeongdong block for gameplay. They use generic names and invented logos, with no real bank or agency branding.
- **Style:** consistent low-poly with procedural textures (see *Visual standards*). Korean identity comes from the details: Hangul signage stacked up the facades, lit shop fronts, food carts, bollards and recycling bins, queue-number machines and boards, glass-partitioned counters, rows of waiting chairs.
- **Asset sources:** CC0 first (Kenney, Quaternius, Poly Haven), then CC-BY with attribution.
- **Credits:** record every third-party asset, including "© OpenStreetMap contributors", in `CREDITS.md` when it is added.
