# Implement click-triggered fireballs

- Date (UTC): 2026-09-30T13:55:42Z
- Branch: `ImplementFireballs`
- Target: `main`
- PR: https://github.com/dkjb634/SkillAtlas/pull/5

## Summary

Add a playful fireball interaction to the web interface. A primary click anywhere in the viewport launches a meteor from above toward that exact location, where it produces a layered BOOM animation and a locally synthesized explosion sound without blocking the page's normal controls.

## Key changes

- Added a pointer-transparent, fixed fireball layer so effects can appear over the interface without intercepting interactions.
- Added concurrent meteor flights with randomized approach angles and distance-aware timing.
- Added impact flashes, shockwave rings, sparks, BOOM lettering, and automatic effect cleanup.
- Synthesized each explosion with Web Audio oscillator and filtered-noise sources, avoiding an external sound asset.
- Added a reduced-motion path that skips meteor flight and shortens impact animations.

## Code highlights

### `SkillsAtlas.Web/wwwroot/app.js`

```javascript
window.addEventListener("pointerdown", (event) => {
  if (!event.isPrimary || event.button !== 0) return;
  const x = event.clientX;
  const y = event.clientY;
  if (reducedMotion.matches) {
    explode(x, y);
    return;
  }

  const startX = Math.max(-80, Math.min(window.innerWidth + 80, x + (Math.random() - .5) * 520));
  const startY = -90;
  const dx = x - startX;
  const dy = y - startY;
```

### `SkillsAtlas.Web/wwwroot/styles.css`

```css
.fireball-layer {
  position: fixed;
  inset: 0;
  z-index: 3000;
  overflow: hidden;
  pointer-events: none;
}

@keyframes impact-boom {
  0% { opacity: 0; transform: translate(-50%, -50%) rotate(-8deg) scale(.25); }
  30% { opacity: 1; transform: translate(-50%, -74%) rotate(-8deg) scale(1.28); }
  100% { opacity: 0; transform: translate(-50%, -130%) rotate(2deg) scale(.82); }
}
```

## Verification

- `node --check SkillsAtlas.Web/wwwroot/app.js`: passed.
- `git diff --check`: passed.
- `dotnet test SkillsAtlas.sln --no-restore`: not run because `dotnet` is not installed in the environment.
