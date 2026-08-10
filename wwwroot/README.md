# Ready To Go Travel launch pages — v3

Drop-in replacement for the v2 pack.

## v3 visual fixes

- Restores the full coder/traveller illustration on desktop.
- Fixes external SVG `viewBox` casing so the artwork scales correctly when referenced as a file.
- Reworks the desktop layout so the illustration owns the right side instead of being squeezed out by the copy column.
- Locks each H1 to exactly two deliberate lines on desktop:
  - `readytogo.travel`: `We’re getting` / `ready to go.`
  - `readytogotravel.co`: `Your next trip` / `is loading.`
- Keeps the headline tracking readable while retaining the heavy RTGT style.
- Removes the hard horizontal colour split through the hero and uses a smoother sky gradient.
- Fixes the Stay informed mail icon:
  - explicit white SVG strokes;
  - SVG geometry centred around the actual viewBox centre;
  - container uses flex centring;
  - the text selector no longer overrides the icon container's layout.
- CSS and SVG files remain separate assets.

## Existing backend retained

The Worker, D1 schema and signup endpoint are unchanged.

Both projects are already configured with:

- D1 binding: `DB`
- D1 database: `rtgt-launch-signups`
- D1 database ID: `7d70a93b-35a1-4a79-b9fd-d71d13637291`
- compatibility date: `2026-08-08`

## Replace in place

Replace the contents of your existing folders with the matching v3 folders:

```text
readytogo.travel/
readytogotravel.co/
```

If your existing `.wrangler/` local state contains test signups you want to keep, do not delete that hidden directory when replacing source files. It is not included in this ZIP.

Then test either site with:

```bash
npm install
npm run dev
```

The remote D1 database does not need to be created again.
