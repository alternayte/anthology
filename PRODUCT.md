# Product

## Register

product

## Users

Media enthusiasts who actively consume across multiple formats — film, TV, books, games, music — and want a single place to track, rate, and reflect on everything they experience. Self-hosters who value ownership of their data. They arrive mid-session (just finished a film, midway through a series) and want fast, frictionless logging. They browse their own history for patterns and revisit old entries for nostalgia.

## Product Purpose

A self-hosted, event-sourced social media tracker. The personal collection presented as a cinematic experience — diary, library, and discovery unified under one roof. Success looks like: a user opens Anthology instead of opening five separate apps, and feels pride in the collection they've built.

## Brand Personality

**Sleek, cinematic, immersive.**

The content IS the interface. Cover art, posters, and album artwork dominate; chrome recedes. The UI feels like walking through a personal museum where every piece is lit perfectly. Dense with information but never cluttered — every element earns its space.

## References

- **Letterboxd**: dark media-forward aesthetic, poster grids that let artwork breathe, the blend of personal tracking with social discovery, star ratings as a core interaction
- **Steam (modernized)**: information density done right, library as a living collection, the sense of an always-updating personal catalog, activity feeds
- Combine both: a modern, unified version of what Steam should look like if rebuilt today with Letterboxd's visual taste

## Anti-references

- Generic SaaS dashboards (Stripe, Linear-style white minimalism — too sterile, no personality)
- Cluttered legacy apps (old IMDB, old Steam — walls of text, poor hierarchy, dated patterns)
- Childish/playful apps (Duolingo-style candy colors, bubbly rounded shapes — wrong tone)
- Corporate/enterprise (Microsoft-style utilitarian gray — soulless)
- Current IMDB (dated, ad-cluttered, information overload without visual hierarchy)

## Design Principles

1. **Content as hero** — artwork, posters, and covers are the primary visual element. UI chrome exists to frame them, never to compete.
2. **Earned density** — show a lot of information, but every piece has clear hierarchy and breathing room. Dense ≠ cluttered.
3. **One collection, many facets** — the system is unified across media types but each type gets subtle visual character (color accents, layout tweaks) without breaking coherence.
4. **Immediacy** — logging should feel instant. The UI should never make the user wait or hunt. Micro-interactions confirm actions before the brain doubts.
5. **Cinematic atmosphere** — dark, moody, immersive. The app should feel like a space you want to spend time in, not a utility you tolerate.

## Accessibility & Inclusion

- WCAG AA compliance (contrast ratios, keyboard navigation, screen reader support)
- Micro animations for feedback and polish, with `prefers-reduced-motion` respected
- Dark-first but accessible — ensure sufficient contrast even in low-lightness palettes
- Color not used as the sole indicator of state (icons/labels as backup)
