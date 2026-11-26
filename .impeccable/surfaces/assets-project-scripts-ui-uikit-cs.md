---
version: 1
slug: "assets-project-scripts-ui-uikit-cs"
primary_target: "Assets/_Project/Scripts/UI/UIKit.cs"
related_targets: ["Assets/_Project/Scripts/UI"]
---

# Surface brief: Ready Set Seoul game UI and world look

Scope: every screen of the game (Home, scenario select, story card, HUD, conversation, keypad and form, pause and settings, results, logbook, dialogs, toasts, scene transitions) and the 3D look that sits behind them. Mode: Experience on Home and scenario select; Operate in the HUD and conversation (the player completes counter tasks).

Audience and job: international students rehearsing their first counter visits in Seoul; booth judges playing a 10-minute part on a laptop. One decision or one action on screen at a time.

## Direction contract

Revision 2026-11-25 (user): the UI is pulled towards the 3D world. Palette Signboard replaces Persimmon, with every value sampled from the street: shop-fascia teal (brush #1B6F68, deep #0F4A45, light text on it), late-sun gold (glow #E9B445, ink text on it), blue-violet ink #1A1D26 and night #1E2636, warm paper #F2EADB and kraft #D9C6A5. Shapes, type and motion are unchanged. Read every colour mention below through this palette (orange → teal, mustard → gold).

Revision 2026-10-18 (user): one palette (Persimmon) instead of two themes; firmer, more highlighted and less papery (no tape, torn edges or paper texture; thick outlines, solid shadows, orange title bands, mustard highlighters); a retro poster title screen; solid bold reading text (Patrick Hand and Gaegu removed); no compass; golden-hour light and an inked toon sky. Where the contract below disagrees, the revision and CLAUDE.md ("UI design system: Retro Ink") win.

THESIS: Your first week in Seoul as a hand-inked graphic novel: paper, brush ink and halftone carry every screen and the 3D street, and Seoul's own signboards organise where you go. Refuses the flat translucent dark-panel HUD and rounded-pill menus of every sim.

OWN-WORLD: Toasted or newsprint paper panels with torn, ink-lined edges; dry-brush strips (dark night strips, persimmon or cobalt active strips); inked keycaps; speech balloons; red-pen circle and slash marks; stamps, tape, sticky notes and thermal queue tickets. Bangers and Black Han Sans for display, Noto Sans KR for reading, Patrick Hand and Gaegu for annotations. Two analogous themes: Persimmon (brick to marigold) and Hangang Night (cyan to cobalt), each one ink, one paper, one brush family.

STORY: The player reads one balloon, picks one strip, and sees it marked by hand (circle right, slash and sticky-note lesson wrong); every place is a signboard on the street.

FIRST VIEWPORT: Home: the 3D Myeongdong street in ink behind; "READY SET SEOUL!" lettered huge over a persimmon brush swash at left third, 서울 첫 주 tag beneath; language tags and two theme paint chips stacked under it; a hand-lettered Start tag bottom centre.

FORM: Brief-pinned comic-ink world (references/art), topology from the assigned signage-stack candidate (5 of 7: projecting signboards, lit or shuttered); raises: state is a mark not a hue (cutting bench), two inks per theme with modelling by shadow shape (noir one-sheet), one continuous stroke drawing on (neon). Seed eea965af.

FINISH: unreviewed and undocumented is unfinished; this build ends with the finish review, the verdict, DESIGN.md, and every shipping raster carrying its provenance
