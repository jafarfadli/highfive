---
name: Ready Set Go!
description: Your first week in Seoul as a retro comic print. Bold ink lines, flat colour taken from the street itself, solid shadows and halftone over an inked Myeongdong.
colors:
  brush: "#1B6F68"
  brush-deep: "#0F4A45"
  on-brush: "#F7F1E6"
  glow: "#E9B445"
  ink: "#1A1D26"
  ink-soft: "#4B5263"
  night: "#1E2636"
  paper: "#F2EADB"
  paper-deep: "#D9C6A5"
  balloon: "#FBF6EC"
  ground: "#D9C6A5"
typography:
  display:
    fontFamily: "Bangers, Black Han Sans"
    fontSize: "228px"
    fontWeight: 400
    lineHeight: 0.95
  headline:
    fontFamily: "Bangers, Black Han Sans"
    fontSize: "64px"
    fontWeight: 400
    lineHeight: 0.95
  headline-ko:
    fontFamily: "Black Han Sans"
    fontSize: "62px"
    fontWeight: 400
    lineHeight: 0.95
  numeral:
    fontFamily: "Bangers"
    fontSize: "70px"
    fontWeight: 400
    lineHeight: 0.95
  title:
    fontFamily: "Noto Sans KR"
    fontSize: "36px"
    fontWeight: 700
    lineHeight: 1.05
  answer:
    fontFamily: "Noto Sans KR"
    fontSize: "30px"
    fontWeight: 700
    lineHeight: 1.05
  body:
    fontFamily: "Noto Sans KR"
    fontSize: "24px"
    fontWeight: 700
    lineHeight: 1.05
  translation:
    fontFamily: "Noto Sans KR"
    fontSize: "22px"
    fontWeight: 400
    lineHeight: 1.05
  label:
    fontFamily: "Bangers"
    fontSize: "24px"
    fontWeight: 400
    lineHeight: 0.95
rounded:
  doc: "2px"
  note: "4px"
  sign: "5px"
  button: "6px"
  panel: "7px"
  balloon: "30px"
spacing:
  tight: "12px"
  stack: "18px"
  chip: "22px"
  note: "26px"
  hud: "40px"
  page: "56px"
components:
  button-primary:
    backgroundColor: "{colors.brush}"
    textColor: "{colors.on-brush}"
    typography: "{typography.label}"
    height: "76px"
  button-paper:
    backgroundColor: "{colors.paper}"
    textColor: "{colors.ink}"
    typography: "{typography.label}"
    rounded: "{rounded.button}"
    height: "76px"
  button-paper-chosen:
    backgroundColor: "{colors.glow}"
    textColor: "{colors.ink}"
    typography: "{typography.body}"
    rounded: "{rounded.button}"
    height: "68px"
  button-night:
    backgroundColor: "{colors.night}"
    textColor: "{colors.paper}"
    typography: "{typography.label}"
    height: "48px"
  answer-strip:
    backgroundColor: "{colors.night}"
    textColor: "{colors.paper}"
    typography: "{typography.answer}"
    height: "92px"
  answer-strip-hover:
    backgroundColor: "{colors.brush}"
    textColor: "{colors.on-brush}"
    typography: "{typography.answer}"
    height: "92px"
  answer-strip-tried:
    backgroundColor: "{colors.ink-soft}"
    textColor: "{colors.paper-deep}"
    typography: "{typography.answer}"
    height: "92px"
  doc-chip:
    backgroundColor: "{colors.glow}"
    textColor: "{colors.ink}"
    typography: "{typography.body}"
  speech-balloon:
    backgroundColor: "{colors.balloon}"
    textColor: "{colors.ink}"
    typography: "{typography.title}"
    rounded: "{rounded.balloon}"
    padding: "52px 46px 30px"
    width: "1180px"
  card:
    backgroundColor: "{colors.paper}"
    textColor: "{colors.ink}"
    rounded: "{rounded.panel}"
    padding: "30px 32px"
  title-band:
    backgroundColor: "{colors.brush}"
    textColor: "{colors.on-brush}"
    typography: "{typography.headline}"
  highlight:
    backgroundColor: "{colors.glow}"
    textColor: "{colors.ink}"
    typography: "{typography.body}"
  sticky-note:
    backgroundColor: "{colors.glow}"
    textColor: "{colors.ink}"
    typography: "{typography.body}"
    rounded: "{rounded.note}"
    padding: "26px"
  night-plate:
    backgroundColor: "{colors.night}"
    textColor: "{colors.paper}"
    typography: "{typography.body}"
  keycap:
    backgroundColor: "{colors.paper}"
    textColor: "{colors.ink}"
    typography: "{typography.label}"
    size: "42px"
---

# Design System: Ready Set Go!

## Overview

**Creative North Star: "The Retro Comic Print"**

Every screen is a page from a retro comic print about a student's first week in Seoul. Surfaces are flat colour with a thick ink outline and a solid ink shadow offset down-right, the way a cheap four-colour print registers a panel. Seoul's own paperwork and signage are the furniture: queue tickets, application forms, stamps, notes, projecting signboards. The Home screen is one page of key art: the people of the game standing in 명동길, a nurse asking her question in a balloon, and the title in extruded block letters on a fading halftone field. The 3D Myeongdong street behind the UI is drawn in the same ink (cel tones, ink outlines, halftone) under a toon sky, so the print never floats over a different world.

The system is built in code, not from sprites. Every shape is a procedural mesh (`InkShape`) that keeps only a trace of the hand-inked origin: 35 % of the wobble, 15 % of the raggedness, 20 % of the grain and 30 % of the dry brush a shape asks for. This revision is firmer and less papery than its hand-inked predecessor. Tape, torn edges and paper texture are gone, and there is one palette. Highlights do the pointing: titles sit on teal bands, the current to-do on a glow highlighter, and whatever an answer hands over on a glow chip. State is carried by a drawn mark as well as by colour.

Density is low and the focus is single: one line of dialogue, up to four answers, one to-do. The world rejects the flat translucent dark-glass HUD and the capsule menus of every sim, and it rejects thin or handwritten reading text.

All sizes are canvas pixels on the 1920 × 1080 reference canvas (uGUI `CanvasScaler`, scale with screen size, match 0.5), not CSS pixels.

**Key Characteristics:**
- Flat fill, 4–5px ink outline, solid ink shadow down-right on every card, balloon, keycap, ticket and note.
- One palette, Signboard, sampled from the 3D street: the teal of the shop fascias, the gold of the late sun, the blue-violet of the shade, and warm paper.
- Teal bands, glow highlighters and glow chips point at what matters.
- Korean is the largest line in every exchange; small text is Noto Sans KR Bold, solid, at least 22px.
- Shape changes step at 12 fps; transforms snap with a hard overshoot; states and screens never cross-fade.

## Colors

One palette, Signboard, defined by role in `UI/Theme.cs` (enum `Role`). Every value is taken from the 3D street the UI sits on (revised 2026-11-25, replacing the warm Persimmon palette, which read as a different game from the world behind it): the teal of Myeongdong's shop fascias and the clinic and bank fittings, the late-afternoon sun, the blue-violet shade of the street canyon, and warm paper and pavement beige. The 3D world itself is unchanged.

### Primary
- **Fascia Teal** (brush): the one active thing on a screen: title bands, the hovered or picked answer, the primary button, progress, drawn rings and ticks, the 완료 stamp, the selected Logbook tab and part.
- **Deep Teal** (brush-deep): the block under extruded lettering, the 다시 해 봐요 stamp, emphasis inside text.
- **Paper on Teal** (on-brush): the text colour on a brush fill. Text on a glow fill is always ink.

### Secondary
- **Sun Gold** (glow): highlights. The to-do highlighter, document chips, notes (lessons, warnings, missing documents), hover stars, the slash across a tried answer, the Home start bar, the chosen language, and the third signboard.

### Neutral
- **Ink** (ink): every outline, every solid shadow, all text on light fills, scrims, the scene-change sheet.
- **Slate** (ink-soft): the strip of an already-tried answer, minor marks, secondary inline text, the label of a button that is not ready yet.
- **Paper** (paper): cards, keycaps, paper buttons, plaques, and lettering on dark strips.
- **Kraft** (paper-deep): the results page, locked rows, muted lettering on a tried answer, a button that is not ready yet (the form's sign button before a box is ticked).
- **Night** (night): answer strips, name strips, night buttons, and the plate under any text that sits on the 3D scene.
- **Balloon** (balloon): speech balloons, the queue ticket, the form, the logo letters.
- **Ground** (ground): the plain backdrop for UI captures with no world behind.

### Named Rules
**The One Brush Rule.** At most one element per screen region is lit teal at a time; hover moves the brush, it never adds a second one.

**The Roles Only Rule.** Code never writes a literal colour into the UI; graphics take a `Role` (`Themed`, `InkShape.fillRole`, `UIKit.Words`).

**The Highlight Points Rule.** Teal bands mark titles and glow marks what to do or hand over; nothing else uses a highlighter.

## Typography

**Display Font:** Bangers (Hangul falls back to Black Han Sans)
**Hangul Display Font:** Black Han Sans
**Body Font:** Noto Sans KR, Bold for nearly everything, Regular only for translations

**Character:** Bangers and Black Han Sans are poster type that only letter short words; Noto Sans KR Bold carries every line a player has to read, solid and heavy enough to read from a booth.

### Hierarchy
- **Display** (Bangers, 136 / 236px, extruded): the "READY SET / GO!" logo only: balloon letters with an ink outline (5.5 % of size) and a solid deep-teal block 15 / 22px deep.
- **Headline** (Bangers, 58–84px, lettered): dialog and pause titles on a teal band (64), banners (72), the Logbook title (58), "LEVEL COMPLETE!" (84).
- **Headline Korean** (Black Han Sans, 32–62px): 어디 갈까요?, signboard syllables (44–52), place names, 서울 첫 주 (48), 번호표, stamps; the queue number is 104.
- **Numeral** (Bangers, 70px): results stats.
- **Title** (Noto Sans KR Bold, 34px): the staff line in the dialogue box. HUD quest titles are Bangers 36 on a teal band.
- **Answer** (Noto Sans KR Bold, 30px): the Korean of each answer strip.
- **Body** (Noto Sans KR Bold, 22–26px): romanization, hints, labels, notes, to-dos, chips, prompts.
- **Translation** (Noto Sans KR Regular, 22px): the translation under a bold Korean line, and nothing else.
- **Label** (Bangers, 18–36px): keycap labels and button words, upper case.

### Named Rules
**The Korean First Rule.** In any conversation element the Korean line is the largest text; romanization and translation sit directly beneath it, smaller, in solid ink.

**The Solid Small Text Rule.** Text below headline size is Noto Sans KR, alpha 1, at least 22px: ink on light fills, paper on night. Text over the 3D scene sits on a night plate.

**The Lettering Rule.** Display type is lettered: light letters get an ink outline of 5 % of the size, dark and brush letters none; the hard shadow sits 5.5 % right and 6.5 % down, in ink, or in brush under dark letters.

## Layout

Fixed 1920 × 1080 reference canvas, scaled to the screen with width and height weighted equally; no breakpoints. Screens are composed in absolute anchors with uGUI stacks for lists. Fixed jobs own the corners: the quest card top-left, toasts top-right (a 420px column), Back (Esc keycap) bottom-left, language cards bottom-left and the start bar bottom-right on Home, the primary action bottom-right on Results.

Rhythm: 12px between answer strips, 18px in card stacks, 22px between chooser cards, 26px inside notes and between part cards, 40px HUD margin, 56px page margin. The conversation column is 1180px wide, 34px off the bottom. Cards and signs sit at small resting tilts (−2° to +3°; the logo −7°) so pages look placed by hand; strips and keycaps stay level.

**The One To-Do Rule.** The HUD shows a title on a teal band, place · part, one brush progress bar and one current to-do on a highlighter. No compass, markers, checklists, counters or scores.

## Elevation & Depth

Depth is print registration: every card casts a solid, unblurred ink shadow down and to the right. The default is (8, −9) at full opacity; balloons take (9, −10), notes and tags (7, −8), buttons (6, −7), keycaps a solid base straight down (18 % of the label size). Hover lifts a piece off its shadow, which grows ×1.6; pressing slams it flat onto the shadow. Full values are in the sidecar.

Modals sit on an ink scrim (62 % ink plus halftone dots), never dark glass. Scene changes are a sheet of ink with a torn brush-coloured leading edge, turned −8°, sweeping across the screen.

**The world behind the print.** The 3D street keeps its own colours (the UI palette is sampled from them) and is drawn in ink: three flat light tones (0, 0.36, 0.8), screen-space halftone (7px pitch at 1080p, 25–60 m) and hatching (5px, darkest shade, 6–18 m), ink silhouettes 2.2px a side near the camera, thinning to 1px by 20 m and fading out from 30 to 110 m. The street is lit late-afternoon gold (a strong warm sun, cool blue-violet ambient, warm haze), and a toon sky (`Sky Toon`: deep blue zenith, peach horizon, sun glow, flat two-tone clouds outlined in ink) sits over it. Grades are punchy: higher contrast and saturation, a warm temperature, strong bloom on signs.

**The Solid Shadow Rule.** Shadows have offset, full opacity and no blur. A soft or translucent shadow has left the world.

## Shapes

Edges are firm, with only a trace of wobble. Outlines are 4–4.5px on cards and balloons (keycaps max(3, 13 % of size)). Radii are small (doc icon 2px, note 4px, signboard 5px, button 6px, card 7px); the speech balloon alone is round (30px), with a tail pointing at the speaker.

Strips have no outline: a single bold brush bar that can be painted left to right (`reveal`), with only a little dryness at the ends. Marks are open strokes: the ring overshoots its start, slashes and arrows taper at both ends. Stamps are two rings printed with missing ink. The queue ticket has zigzag edges. The sunburst (`Rays`) is alternating sectors from a centre, half ray and half gap.

**The No Pill Rule.** No capsule buttons, no soft rounded rectangles; every surface is an `InkShape` with an ink line.

## Components

### Buttons
Firm, printed and loud.
- **Primary:** a teal brush bar with on-brush Bangers lettering, one per screen. A primary that is not ready yet is an opaque kraft bar with slate lettering, never a see-through one.
- **Paper:** a paper card, 4px ink line, 6px radius.
- **Night:** a dark bar with paper lettering.
- **Chosen option:** a paper button lit glow with a ✓ before its label (the language list on Home).
- **Hover / Focus:** the body tilts −1.5°, scales to 1.04, and its shadow grows ×1.6. **Pressed:** it slams 1.5 % smaller onto its shadow.

### Conversation Layout (signature)
Laid out like a story-game dialogue so the staff member stays in view in the middle of the screen:
- **Dialogue box:** a night card 1300px wide along the bottom (4.5px line, 10px radius, solid shadow); the speaker's name on a teal band over its top-left edge; the Korean line in Bold 34 paper; the translation in Regular 24 glow; an R keycap and "hear again" at the top-right, and under it a SPACE keycap and "say it" (hold to speak; a glow ring drawn round the keycap swells with the voice while listening, "listening…", then "…" while it is heard). After an answer said out loud, a glow line under its Korean: "You said: … · 86% · unclear: 요".
- **Answers:** a column 660px wide on the right, bottom-aligned 296px up, growing upward.
- **Lesson note:** a glow note on the left at the same height.

### Answer Strips (signature)
- **Rest:** a night bar (its height follows the text), a paper keycap 1–4, Korean in Bold 28, then romanization · translation in Bold 22 below it.
- **Hand-over chip:** an answer that hands a document over carries a glow chip under its text: an ink-outlined tag with a `DocIcon` and "Hand over: 여권 · Passport" in the player's language, wrapping onto a second line when long.
- **Hover / Focus:** the bar turns teal, text turns on-brush, two glow ★ appear; the whole answer tilts −0.8° and lifts to 1.02.
- **Picked:** an ink ring is drawn round its keycap; the others peel away.
- **Tried:** a slate bar, kraft lettering, an × keycap and a glow slash (glow, like the lesson note it belongs to, and visible on slate). Opaque, never dimmed with alpha.

### Situation picker (story card)
On a personalised visit the story card's sheet asks "What brings you here today?" and paper cards hang under it, one per situation, each a brush keycap with its number and the situation's title (Bold 32); hover tilts and lifts them, a pick slaps the sheet down with that situation's story and the start bar appears.

### Speech Balloon
Balloon, 4.5px ink line, solid shadow, a tail pointing at the speaker: used on Home over the cast (the nurse's question with its translation), not in the counter conversation.

### Cards / Containers
- **Card:** paper, 4.5px ink line, solid shadow. Internal padding 26–56px.
- **Title band:** a teal brush bar carrying a Bangers title (paper letters in dialogs and pause, on-brush on HUD and Logbook).
- **Note:** glow, 4px ink line, 4px radius, solid (7, −8) shadow.
- **Signboard (돌출간판):** a tall board cycling teal, night and glow, one Hangul syllable per row, an ink emblem on top, and the local name on a paper plaque hanging under it. Parts hang beside it as cards, each with its number as a brush keycap beside the title. Done → 완료 stamp; locked → shutter slats and a 준비중 sticker.
- **Queue ticket:** balloon paper with zigzag edges; prints down from its slot; circled when it is the player's turn.

### Highlights and Plates
- **Highlight:** a glow brush band behind a single line of text (the HUD to-do with its drawn arrow).
- **Night plate:** a night bar behind any text that sits over the 3D scene (controls help, banner small lines, labels on Home).

### Keycaps
Paper (or brush for a part number), ink line max(3, 13 % of size), solid ink base, Bangers label, for every key the player can press: 1–4, E, R, Esc.

### Inputs
The slider is an ink line, a teal fill and a paper blob handle. The PIN pad is a 340 × 560 card of inked keys. The application form is a balloon sheet with drawn tick boxes; its sign button stays an opaque kraft bar until the right box is ticked. A to-do keeps a short last word (a counter or room number) on the line before it (`UIKit.NoOrphan`).

### Home Key Art (signature)
One page that shows the game:
- **Scene:** a low camera in 명동길 that drifts a little. The cast stands in the right half of the frame: the student (the player) talking with the clinic nurse, and the immigration officer and the bank teller beside them. The nurse's question "어디가 불편하세요?" sits in a speech balloon over her head, with its translation.
- **Left column:** set on a `HalftoneField`, ink dots at 62 % that fade out by the middle of the screen so the street stays clear. It holds:
  - "READY SET / GO!" in extruded block letters (136 / 236px) at −6°;
  - 서울 첫 주 on a night tag;
  - the tagline on a card;
  - the language dropdown ("Language: English ▼", a list opening upward with the chosen one glow and ticked);
  - a glow start bar that breathes (±2.5 %).

## Do's and Don'ts

### Do:
- **Do** take every colour from a `Role`.
- **Do** build surfaces from the kit (`PaperPanel`, `BrushStrip`, `SpeechBalloon`, `StickyNote`, `Highlight`, `HalftoneField`, `DocIcon`, `Keycap`, `StampMark`, `InkButton`; `Rays` is available for a burst but no screen uses it now).
- **Do** keep small text Noto Sans KR Bold, solid and at least 22px; use Regular only for a translation under a bold Korean line.
- **Do** put text that sits on the 3D scene on a night plate.
- **Do** point with highlights: teal title bands, glow highlighters and glow chips.
- **Do** mark state with a drawn ring, slash, stamp or note as well as colour, and show a softer state with an opaque softer fill.
- **Do** step shape changes at 12 fps and snap transforms with overshoot (`Slap` 0.26 s, `Stamp` 0.3 s, `Peel` 0.42 s); honour Calm animation.
- **Do** draw standalone icons with the kit's strokes (`Arrow`, `CircleMark`, `PenStroke`, `DocIcon`).

### Don't:
- **Don't** bring back paper texture, torn edges or tape.
- **Don't** use translucent shadows or translucent text, or show state with CanvasGroup alpha.
- **Don't** add a second theme or literal colours.
- **Don't** use a thin or handwritten face for reading text.
- **Don't** add a compass, checklists, counters or scores to the HUD.
- **Don't** cross-fade states or screens; scene changes are the ink sheet.
- **Don't** let a dark translucent wash do a card's job (UI blends in linear space, so a 60 % ink wash barely darkens).
- **Don't** put a small label above a title; a part number is a keycap beside it.
- **Don't** use emoji; the fonts cannot draw them.
