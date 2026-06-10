<!-- SEED: re-run /impeccable document once there's code to capture real component tokens. -->
---
name: Anthology
description: A cinematic personal media tracker
colors:
  void: "oklch(8% 0.01 220)"
  abyss: "oklch(12% 0.015 220)"
  slate: "oklch(18% 0.015 220)"
  smoke: "oklch(24% 0.02 220)"
  ash: "oklch(32% 0.015 220)"
  teal: "oklch(72% 0.14 195)"
  teal-dim: "oklch(55% 0.10 195)"
  teal-glow: "oklch(80% 0.10 195)"
  film-amber: "oklch(74% 0.14 75)"
  game-electric: "oklch(68% 0.16 255)"
  book-sage: "oklch(65% 0.08 150)"
  music-violet: "oklch(68% 0.16 300)"
  text-primary: "oklch(95% 0.005 220)"
  text-secondary: "oklch(72% 0.01 220)"
  text-muted: "oklch(50% 0.015 220)"
  danger: "oklch(65% 0.20 25)"
  success: "oklch(72% 0.14 155)"
typography:
  display:
    fontFamily: "Plus Jakarta Sans, system-ui, sans-serif"
    fontSize: "clamp(2rem, 5vw, 3.5rem)"
    fontWeight: 700
    lineHeight: 1.1
    letterSpacing: "-0.02em"
  headline:
    fontFamily: "Plus Jakarta Sans, system-ui, sans-serif"
    fontSize: "1.5rem"
    fontWeight: 600
    lineHeight: 1.3
    letterSpacing: "-0.01em"
  title:
    fontFamily: "Plus Jakarta Sans, system-ui, sans-serif"
    fontSize: "1.125rem"
    fontWeight: 600
    lineHeight: 1.4
  body:
    fontFamily: "Plus Jakarta Sans, system-ui, sans-serif"
    fontSize: "0.9375rem"
    fontWeight: 400
    lineHeight: 1.6
  label:
    fontFamily: "Plus Jakarta Sans, system-ui, sans-serif"
    fontSize: "0.8125rem"
    fontWeight: 500
    lineHeight: 1.4
    letterSpacing: "0.01em"
rounded:
  sm: "6px"
  md: "10px"
  lg: "16px"
  full: "9999px"
spacing:
  xs: "4px"
  sm: "8px"
  md: "16px"
  lg: "24px"
  xl: "40px"
  section: "64px"
---

# Design System: Anthology

## 1. Overview

**Creative North Star: "The Midnight Gallery"**

A personal gallery visited after dark. Each piece of media is spotlit against deep obsidian walls. The architecture recedes; the collection speaks. You walk through rooms of film, music, books, and games, each with its own ambient temperature but the same exacting curator behind them all.

The system is dense but never hurried. Information earns its place through clear hierarchy, not through whitespace alone. Surfaces are dark, nearly black, tinted cool. Interactive elements pulse with teal energy. Each media type carries a subtle chromatic identity, like galleries in a museum wing-coded by lighting temperature.

This is not a utility. It is not a dashboard. It is not a SaaS product. It is a personal space that rewards time spent. The UI should feel like something you designed yourself and are proud to show someone.

**Key Characteristics:**
- Dark-first: near-black canvases that let cover art dominate
- Content as architecture: posters, covers, and artwork are structural, not decorative
- Earned density: lots of visible information, every piece with clear rank
- Chromatic identity: media types are distinguishable by ambient color without breaking coherence
- Micro-choreography: transitions feel alive but never theatrical

## 2. Colors

A full-palette strategy anchored by cool teal, with per-media-type accents that provide character without fragmenting the system. The dark canvas is the unifying force; accents are guests in the space, not owners of it.

### Primary

- **Teal** (oklch(72% 0.14 195)): The brand accent. Interactive elements, active navigation, primary CTAs, ratings, progress indicators. The one color that appears regardless of which media type is in context.
- **Teal Dim** (oklch(55% 0.10 195)): Pressed states, visited indicators, background tints behind teal-on-teal text.
- **Teal Glow** (oklch(80% 0.10 195)): Hover highlights, focus rings, subtle luminescence on interactive elements.

### Secondary (Media Accents)

- **Film Amber** (oklch(74% 0.14 75)): Warm gold. Film-specific badges, the diary timeline accent, star ratings in film context. Evokes theater marquee warmth.
- **Game Electric** (oklch(68% 0.16 255)): Cool blue-violet. Achievement markers, playtime indicators, game library accents. High-energy, modern.
- **Book Sage** (oklch(65% 0.08 150)): Muted earthy green. Reading progress, book-specific tags, literary metadata. Calm, analog.
- **Music Violet** (oklch(68% 0.16 300)): Rich purple. Listening activity, album-specific highlights, playlist markers. Atmospheric, evening-coded.

### Neutral

- **Void** (oklch(8% 0.01 220)): Page body, the deepest background. Nearly black, barely tinted cool.
- **Abyss** (oklch(12% 0.015 220)): Primary surface background. Cards at rest, sidebars, content containers.
- **Slate** (oklch(18% 0.015 220)): Elevated surfaces, modal backgrounds, dropdown panels.
- **Smoke** (oklch(24% 0.02 220)): Hover states, active row highlights, input field backgrounds.
- **Ash** (oklch(32% 0.015 220)): Borders, dividers, inactive icons.
- **Text Primary** (oklch(95% 0.005 220)): Headlines, titles, emphasis text. Near-white with cool tint.
- **Text Secondary** (oklch(72% 0.01 220)): Body text, descriptions, supporting copy.
- **Text Muted** (oklch(50% 0.015 220)): Timestamps, metadata, placeholder text, disabled labels.

### Named Rules

**The Ambient Rule.** Media accents appear ONLY when that media type is contextually relevant (viewing a film, in the books section, on a game's detail page). They never compete. In mixed-media views (home feed, search results), only the primary teal is used. Accents are contextual atmosphere, not navigation aids.

**The Tinted Neutral Rule.** No pure `#000` or `#fff`. Every neutral carries a 0.01-0.02 chroma tint toward hue 220 (cool blue). This prevents the palette from feeling sterile and keeps dark surfaces cohesive.

## 3. Typography

**Primary Font:** Plus Jakarta Sans (with system-ui fallback)

**Character:** Warm, modern, humanist. Enough personality to feel curated without being quirky. Excellent legibility at small sizes for metadata-dense views, and enough weight contrast to anchor display headings. The tracking tightens at large sizes for cinematic presence, loosens at label size for clarity.

### Hierarchy

- **Display** (700, clamp(2rem, 5vw, 3.5rem), 1.1, -0.02em): Hero sections, collection titles, feature headlines. Used sparingly.
- **Headline** (600, 1.5rem, 1.3, -0.01em): Section titles, card group headers, page titles. The workhorse heading.
- **Title** (600, 1.125rem, 1.4): Card titles, list item names, dialog headers. Mid-weight anchor.
- **Body** (400, 0.9375rem, 1.6): Descriptions, reviews, long-form text. Capped at 65ch line length.
- **Label** (500, 0.8125rem, 1.4, +0.01em): Metadata, timestamps, ratings, tags, navigation items. Dense but legible.

### Named Rules

**The Negative Tracking Rule.** Display and Headline sizes use negative letter-spacing (-0.02em, -0.01em). This creates cinematic density at large scale. Body and Label use neutral or positive tracking for readability.

## 4. Elevation

The system is flat by default. Depth is communicated through tonal layering (void → abyss → slate → smoke), not through shadows. Shadows appear only as response to state: hover, focus, or explicit elevation (modals, dropdowns, toasts).

### Shadow Vocabulary

- **Hover Lift** (`0 8px 32px oklch(0% 0 0 / 0.4)`): Cards on hover, interactive surfaces gaining attention. Subtle but noticeable depth shift.
- **Overlay** (`0 16px 48px oklch(0% 0 0 / 0.6), 0 2px 8px oklch(0% 0 0 / 0.3)`): Modals, dialogs, dropdown menus. Creates clear layer separation.
- **Glow** (`0 0 24px oklch(72% 0.14 195 / 0.15)`): Focus rings, active accent elements. Teal-tinted ambient glow rather than hard borders.

### Named Rules

**The Flat-By-Default Rule.** Surfaces are flat at rest. The tonal ramp (void → abyss → slate → smoke) communicates layer; shadows confirm interaction. If a surface has a shadow without user interaction causing it, the shadow is wrong.

## 5. Components

### Buttons

- **Shape:** Gently rounded (6px radius), medium density
- **Primary:** Teal background (oklch(72% 0.14 195)), void text, 12px 24px padding. The only solid-filled interactive element on most screens.
- **Hover:** Background shifts to teal-glow (oklch(80% 0.10 195)), subtle lift shadow, 150ms ease-out transition
- **Focus:** 2px teal-glow ring offset by 2px from edge, no background change
- **Ghost:** Transparent background, teal text + teal border (1px), same radius. Hover fills with smoke background.
- **Danger:** Danger red background (oklch(65% 0.20 25)), used only for destructive confirmations

### Cards / Containers

- **Corner Style:** 10px radius (md)
- **Background:** Abyss (oklch(12% 0.015 220)) against void page body
- **Border:** 1px ash (oklch(32% 0.015 220)) at 40% opacity, only when card needs separation from identical-tone siblings
- **Hover:** Lift shadow + background shift to slate, 200ms ease-out
- **Internal Padding:** 16px (md) default, 24px (lg) for feature cards
- **Media cards:** Poster/cover art bleeds to card edge (no internal padding on the image). Text content below has standard padding. Art is structural.

### Inputs / Fields

- **Style:** Smoke background (oklch(24% 0.02 220)), 1px ash border, 6px radius
- **Focus:** Border transitions to teal, glow shadow appears, 150ms
- **Placeholder:** Text-muted color, label weight
- **Error:** Border transitions to danger red, subtle red glow replaces teal

### Navigation

- **Style:** Label weight (500), label size (0.8125rem), text-secondary color at rest
- **Active:** Text-primary + teal underline or left indicator (2px, contextual)
- **Hover:** Text-primary, no background change (the text brightening IS the feedback)
- **Mobile:** Bottom tab bar with icon + label, teal fill on active icon

### Media Grid (Signature Component)

The poster/cover grid is Anthology's defining pattern. 2:3 aspect ratio containers for film/TV/games, square for music, 3:4 for books. Art fills the container entirely. Title appears below as a label-weight caption. Hover reveals a quick-action overlay (rate, log, add to list) with a 60% void scrim and 200ms fade. Grid gaps are tight (sm: 8px) to create the gallery-wall density.

### Star Rating (Signature Component)

Half-star precision. Stars render in the contextual media accent (film-amber for films, teal for mixed contexts). Hover previews the rating with a 60ms stagger fill animation. Confirmed rating pulses once (scale 1.0 → 1.05 → 1.0, 200ms) as micro-feedback.

## 6. Do's and Don'ts

### Do:

- **Do** let cover art dominate. Minimum 60% of a media detail page should be artwork or artwork-derived color (background tint extracted from the poster).
- **Do** use the tonal ramp for hierarchy. Void → Abyss → Slate → Smoke is the depth stack. More important content sits on lighter tones.
- **Do** use media accents only in their home context. Film-amber in the film section, game-electric in the games section, teal everywhere else.
- **Do** keep body text at 65-75ch max width. Dense doesn't mean wall-to-wall text.
- **Do** use micro-animations for state confirmation: rating fills, toast slides, hover lifts. Each under 300ms, ease-out-quart.
- **Do** tint neutrals. Every gray carries chroma 0.01-0.02 toward hue 220.
- **Do** respect `prefers-reduced-motion` by disabling all non-essential transitions.

### Don't:

- **Don't** use generic SaaS whitespace. Anthology is dense by design. Large empty expanses waste the gallery wall.
- **Don't** create cluttered text walls. Earned density means everything has hierarchy. If two elements are the same visual weight, one is wrong.
- **Don't** use childish rounded shapes. Max radius is 16px (lg) for major containers. No pill shapes on cards. No bubbly UI.
- **Don't** use corporate/enterprise gray. Every neutral is tinted. Flat untinted `#333` or `#666` is forbidden.
- **Don't** add side-stripe borders (border-left > 1px as accent). Use background tints or leading icons instead.
- **Don't** use gradient text (background-clip: text). Solid color only for all text.
- **Don't** use glassmorphism/blur decoratively. If backdrop-filter appears, it must solve a real layering problem (overlay on dynamic artwork), not just look cool.
- **Don't** create identical card grids with icon + heading + text. The media grid uses ARTWORK as the primary element, not abstract icons.
- **Don't** apply choreographed motion to routine navigation. Staggered grid reveals are for first-load and section transitions only, not every page change.
- **Don't** use pure `#000000` or `#ffffff` anywhere. Tinted neutrals only.
- **Don't** let the app look like current IMDB: no ad-shaped holes in layouts, no information overload without visual hierarchy, no dated typography.
