# Add pointer-following spotlight

- Date (UTC): 2026-09-30T13:36:01Z
- Branch: `AddLightSpotToCursor`
- Target: `main`
- PR: pending

## Summary

Add a cursor-following spotlight to the Skills Atlas UI. A dark radial overlay tracks the pointer, while the mouse wheel changes its radius.

## Key changes

- Added the overlay element to the page and styles for a fixed, pointer-transparent radial gradient.
- Added pointer listeners and bounded wheel-based radius adjustment.
- Included the task summary at `memory/AddLightSpotToCursor_2026-09-30_15:34.md`.

## Code highlights

### `SkillsAtlas.Web/wwwroot/app.js`
```javascript
const minimumRadius = 60;
const maximumRadius = 420;
let radius = 100;

window.addEventListener("pointermove", moveLight, { passive: true });
window.addEventListener("pointerdown", moveLight, { passive: true });
```

### `SkillsAtlas.Web/wwwroot/styles.css`
```css
.light-spot {
  position: fixed;
  inset: 0;
  pointer-events: none;
  background: radial-gradient(
    circle at var(--light-x) var(--light-y),
    transparent 0 var(--light-radius),
    rgba(0, 0, 0, .52) var(--light-fade),
    #000 var(--light-edge)
  );
}
```

## Verification

- `git diff --check`: passed
- Tests: not run
